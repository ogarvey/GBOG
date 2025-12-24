using GBOG.CPU.Opcodes;
using GBOG.Audio;
using GBOG.Graphics;
using GBOG.Memory;
using GBOG.Utils;
using Serilog;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection;
using Log = Serilog.Log;

namespace GBOG.CPU
{
    public struct Color
    {
        public byte R, G, B, A;
        public static readonly Color White = new Color { R = 255, G = 255, B = 255, A = 255 };
        // dmg-acid2 expects these exact 8-bit grayscale levels: $00, $55, $AA, $FF.
        public static readonly Color LightGray = new Color { R = 0xAA, G = 0xAA, B = 0xAA, A = 255 };
        public static readonly Color DarkGray = new Color { R = 0x55, G = 0x55, B = 0x55, A = 255 };
        public static readonly Color Black = new Color { R = 0, G = 0, B = 0, A = 255 };
        public static readonly Color Fallback = new Color { R = 255, G = 0, B = 255, A = 255 };
    }

    public class Gameboy
    {
        [Flags]
        internal enum OamAccessKind
        {
            None = 0,
            Read = 1,
            Write = 2,
        }

        #region Registers
        // The CPU has eight 8-bit registers.
        // These registers are named A, B, C, D, E, F, H and L.
        // Since they are 8-bit registers, they can only hold 8-bit values. 
        private byte[] _registers = new byte[8];
        private OpcodeHandler _opcodeHandler;
        private int _scanlineCounter;
        private byte[,,] _display;
        private byte[] _pixels;

        public GBMemory _memory { get; }
        public PPU _ppu { get; }
        public bool EnableCpuTrace { get; set; } = false;

    #if DEBUG
        // Aggressively pre-JIT most methods at ROM load to avoid one-off, in-game stalls.
        // This can increase ROM load time in Debug but keeps gameplay smooth.
        public bool AggressiveJitWarmup { get; set; } = true;
    #else
        public bool AggressiveJitWarmup { get; set; } = false;
    #endif

        public bool DoubleSpeed { get; private set; } = false;
        public Apu Apu { get; }
        private AudioOutput? _audioOutput;

        // When enabled, throttle emulation to real time.
        // This prevents audio buffer overflow (crackles) and keeps games at correct speed.
        public bool LimitSpeed { get; set; } = false;
        private int _mClockCount;
        private int _timerPeriod = 1024;

        public sealed class EmulationStats
        {
            public int BaseCyclesLastFrame { get; init; }
            public double TargetFrameMs { get; init; }
            public double HostFrameMs { get; init; }
            public double EmuWorkMs { get; init; }
            public double EmuWorkCpuMs { get; init; }
            public double ThrottleWaitMs { get; init; }
            public double SpeedMultiplier { get; init; }
            public ushort HotPc { get; init; }
            public int HotPcRepeats { get; init; }

            public int InstructionsThisFrame { get; init; }

            public bool HasThreadCpuCycles { get; init; }
            public ulong ThreadCpuCyclesThisFrame { get; init; }

            public long AllocBytesThisFrame { get; init; }

            public int Gc0Collections { get; init; }
            public int Gc1Collections { get; init; }
            public int Gc2Collections { get; init; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryThreadCycleTime(IntPtr threadHandle, out ulong cycleTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetThreadTimes(
            IntPtr hThread,
            out FILETIME lpCreationTime,
            out FILETIME lpExitTime,
            out FILETIME lpKernelTime,
            out FILETIME lpUserTime);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long FileTimeToLong(FILETIME ft)
        {
            return ((long)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
        }

        private static bool TryGetCurrentThreadCpuTime100ns(out long cpu100ns)
        {
            cpu100ns = 0;
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            IntPtr thread = GetCurrentThread();
            if (thread == IntPtr.Zero)
            {
                return false;
            }

            if (!GetThreadTimes(thread, out _, out _, out var kernel, out var user))
            {
                return false;
            }

            cpu100ns = FileTimeToLong(kernel) + FileTimeToLong(user);
            return true;
        }

        private static bool TryGetCurrentThreadCpuCycles(out ulong cycles)
        {
            cycles = 0;
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            IntPtr thread = GetCurrentThread();
            if (thread == IntPtr.Zero)
            {
                return false;
            }

            return QueryThreadCycleTime(thread, out cycles);
        }

        private volatile EmulationStats? _stats;
        public EmulationStats? Stats => _stats;

        internal OamAccessKind CurrentMCycleOamAccess { get; private set; } = OamAccessKind.None;
        internal bool CurrentMCycleIduInOamRange { get; private set; } = false;

        // The registers can be accessed individually:
        public byte A { get => _registers[0]; set => _registers[0] = value; }
        public byte B { get => _registers[1]; set => _registers[1] = value; }
        public byte C { get => _registers[2]; set => _registers[2] = value; }
        public byte D { get => _registers[3]; set => _registers[3] = value; }
        public byte E { get => _registers[4]; set => _registers[4] = value; }
        public byte F { get => _registers[5]; set => _registers[5] = value; }
        public byte H { get => _registers[6]; set => _registers[6] = value; }
        public byte L { get => _registers[7]; set => _registers[7] = value; }
        // However, the GameBoy can “combine” two registers in order to read and write 16-bit values. 
        // The valid combinations are AF, BC, DE and HL.
        public ushort AF
        {
            get => (ushort)((A << 8) | F);
            set
            {
                A = (byte)((value & 0xFF00) >> 8);
                F = (byte)(value & 0b11110000);
            }
        }

        public ushort BC
        {
            get => (ushort)((B << 8) | C);
            set
            {
                B = (byte)((value & 0xFF00) >> 8);
                C = (byte)(value & 0xFF);
            }
        }

        public ushort DE
        {
            get => (ushort)((D << 8) | E);
            set
            {
                D = (byte)((value & 0xFF00) >> 8);
                E = (byte)(value & 0xFF);
            }
        }

        public ushort HL
        {
            get => (ushort)((H << 8) | L);
            set
            {
                H = (byte)((value & 0xFF00) >> 8);
                L = (byte)(value & 0xFF);
            }
        }
        // The F register is a special register that is used for flags.
        // The flags are used to indicate the result of a comparison or operation.
        // The flags are:
        // Z - Zero flag
        // N - Subtract flag
        // HC - Half Carry flag
        // CF - Carry flag
        public bool Z { get => (F & 0b1000_0000) != 0; set => F = (byte)((F & 0b0111_1111) | (value ? 0b1000_0000 : 0)); }
        public bool N { get => (F & 0b0100_0000) != 0; set => F = (byte)((F & 0b1011_1111) | (value ? 0b0100_0000 : 0)); }
        public bool HC { get => (F & 0b0010_0000) != 0; set => F = (byte)((F & 0b1101_1111) | (value ? 0b0010_0000 : 0)); }
        public bool CF { get => (F & 0b0001_0000) != 0; set => F = (byte)((F & 0b1110_1111) | (value ? 0b0001_0000 : 0)); }

        // The GameBoy has two 16-bit registers: the stack pointer and the program counter.
        // The stack pointer is used to keep track of the current stack position.
        public ushort SP { get; set; }
        // The program counter is used to keep track of the current position in the program.
        public ushort PC { get; set; }
        public bool InterruptMasterEnabled { get; set; }
        private int _imeEnableDelayInstructions;
        public bool Halt { get; set; }
        public int DIVCounter { get; set; }
        #endregion

        public Gameboy()
        {
            //Log.Logger = new LoggerConfiguration()
            //  .WriteTo.File("log.txt",
            //  outputTemplate: "{Message:lj}{NewLine}{Exception}")
            //  .CreateLogger();
            _display = new byte[160, 144, 4];
            _pixels = new byte[160 * 144 * 4];

            Apu = new Apu(sampleRate: 44100);

            _opcodeHandler = new OpcodeHandler(this);
            _memory = new GBMemory(this);
            _ppu = new PPU(this);
            AF = 0x01B0;
            BC = 0x0013;
            DE = 0x00D8;
            HL = 0x014D;
            SP = 0xFFFE;
            PC = 0x0100;

            _memory.LY = 0x99;
        }

        public void ConfigureAudioOutput(bool enabled)
        {
            if (enabled)
            {
                if (_audioOutput != null)
                {
                    return;
                }
                _audioOutput = new AudioOutput(sampleRate: 44100);
                Apu.SetSink(_audioOutput);
            }
            else
            {
                if (_audioOutput == null)
                {
                    return;
                }
                Apu.SetSink(null);
                _audioOutput.Dispose();
                _audioOutput = null;
            }
        }
        public EventHandler<bool>? OnGraphicsRAMAccessed;
        private bool _exit = false;

        private async Task<bool> DoLoop()
        {
            const int MaxBaseCyclesPerFrame = 70224;
            const int CpuCyclesPerMCycle = 4;

            // Use a running target timestamp instead of measuring each frame independently.
            // This reduces jitter caused by variable work time + Windows sleep overshoot.
            ulong ticksPerBaseCycleFixed = ((ulong)Stopwatch.Frequency << 32) / (ulong)Apu.BaseClockHz;

            await Task.Factory.StartNew(() =>
            {
                try
                {
                    Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                }
                catch
                {
                    // Ignore if OS/runtime disallows priority changes.
                }

                long emuStartTicks = 0;
                ulong emuTicksAccFixed = 0;
                bool lastLimitSpeed = false;

                ushort hotPc = 0;
                int hotPcRepeats = 0;
                ushort lastFetchedPc = 0xFFFF;

                while (!_exit)
                {
                    long frameHostStart = Stopwatch.GetTimestamp();

                    long threadCpuStart100ns = 0;
                    bool hasThreadCpuStart = TryGetCurrentThreadCpuTime100ns(out threadCpuStart100ns);

                    ulong threadCpuCyclesStart = 0;
                    bool hasThreadCpuCyclesStart = TryGetCurrentThreadCpuCycles(out threadCpuCyclesStart);

                    long allocStart = GC.GetAllocatedBytesForCurrentThread();

                    int gc0Start = GC.CollectionCount(0);
                    int gc1Start = GC.CollectionCount(1);
                    int gc2Start = GC.CollectionCount(2);

                    bool limitSpeed = LimitSpeed;
                    if (limitSpeed && !lastLimitSpeed)
                    {
                        emuStartTicks = Stopwatch.GetTimestamp();
                        emuTicksAccFixed = 0;
                    }
                    lastLimitSpeed = limitSpeed;

                    int baseCyclesThisFrame = 0;
                    int instructionsThisFrame = 0;

                    while (baseCyclesThisFrame < MaxBaseCyclesPerFrame)
                    {

                        byte opcode;

                        if (EnableCpuTrace)
                        {
                            LogSystemState();
                        }
                        if (IsInterruptRequested())
                        {
                            Halt = false;
                            if (HandleInterrupts())
                            {
                                // Interrupt servicing takes 5 machine cycles = 20 t-cycles.
                                for (int n = 0; n < 5; n++)
                                {
                                    BeginMCycle();
                                    ApplyOamBugIfNeeded();
                                    int baseCycles = GetBaseCyclesFromCpuCycles(CpuCyclesPerMCycle);
                                    baseCyclesThisFrame += baseCycles;
                                    UpdateTimer(CpuCyclesPerMCycle);
                                    UpdateApu(baseCycles);
                                    _ppu.Step(baseCycles);
                                    _memory.TickBaseCycles(baseCycles);
                                }

                                // The interrupt is handled between instructions; don't also fetch/execute an opcode this iteration.
                                continue;
                            }
                        }
                        if (!Halt)
                        {
                            //Log.Information($"Register State: A: {A:X2} F: {F:X2} B: {B:X2} C: {C:X2} D: {D:X2} E: {E:X2} H: {H:X2} L: {L:X2} SP: {SP:X4} PC: 00:{PC:X4} PPU::: LY: {_memory.LY:X2} LCDC: {_memory.LCDC:X2} IE: {_memory.InterruptEnableRegister:X2} IF: {_memory.IF:X2}");

                            ushort fetchPc = PC;
                            if (fetchPc == lastFetchedPc)
                            {
                                hotPcRepeats++;
                            }
                            else
                            {
                                lastFetchedPc = fetchPc;
                                hotPc = fetchPc;
                                hotPcRepeats = 0;
                            }

                            opcode = _memory.ReadByte(PC++);
                            instructionsThisFrame++;
                            GBOpcode? op;
                            if (opcode == 0xCB)
                            {
                                op = _opcodeHandler.GetOpcode(opcode, true);
                            }
                            else
                            {
                                op = _opcodeHandler.GetOpcode(opcode, false);
                            }

                            var steps = op?.steps;
                            if (steps != null)
                            {
                                foreach (var step in steps)
                                {
                                    BeginMCycle();
                                    if (step(this))
                                    {
                                        ApplyOamBugIfNeeded();
                                        int baseCycles = GetBaseCyclesFromCpuCycles(CpuCyclesPerMCycle);
                                        baseCyclesThisFrame += baseCycles;
                                        UpdateTimer(CpuCyclesPerMCycle);
                                        UpdateApu(baseCycles);
								//UpdateGraphics(baseCycles);
								_ppu.Step(baseCycles);
                                        _memory.TickBaseCycles(baseCycles);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }

                                AdvanceImeDelayAfterInstruction();
                            }
                            //cycles = _opcodeHandler.HandleOpcode(opcode);
                        }
                        else
                        {
                            BeginMCycle();
                            // HALT: no CPU bus activity in our model, but still allow any pending
                            // OAM bug triggers from previous access to be applied (should be none).
                            ApplyOamBugIfNeeded();
                            int baseCycles = GetBaseCyclesFromCpuCycles(CpuCyclesPerMCycle);
                            baseCyclesThisFrame += baseCycles;
                            UpdateTimer(CpuCyclesPerMCycle);
                            UpdateApu(baseCycles);
							//UpdateGraphics(baseCycles);
							_ppu.Step(baseCycles);
                            _memory.TickBaseCycles(baseCycles);

                            // No instruction executed while halted; EI delay does not advance here.
                        }

                    }

                    long emuWorkEnd = Stopwatch.GetTimestamp();

                    double emuWorkCpuMs = double.NaN;
                    if (hasThreadCpuStart && TryGetCurrentThreadCpuTime100ns(out long threadCpuEnd100ns))
                    {
                        emuWorkCpuMs = (threadCpuEnd100ns - threadCpuStart100ns) / 10000.0;
                    }

                    bool hasThreadCpuCycles = false;
                    ulong threadCpuCyclesThisFrame = 0;
                    if (hasThreadCpuCyclesStart && TryGetCurrentThreadCpuCycles(out ulong threadCpuCyclesEnd))
                    {
                        hasThreadCpuCycles = true;
                        threadCpuCyclesThisFrame = threadCpuCyclesEnd - threadCpuCyclesStart;
                    }

                    long allocBytes = GC.GetAllocatedBytesForCurrentThread() - allocStart;

                    double targetFrameMs = baseCyclesThisFrame * 1000.0 / Apu.BaseClockHz;
                    double emuWorkMs = (emuWorkEnd - frameHostStart) * 1000.0 / Stopwatch.Frequency;
                    double waitMs = 0;

                    if (limitSpeed)
                    {
                        // Advance the target timestamp based on emulated time.
                        emuTicksAccFixed += (ulong)baseCyclesThisFrame * ticksPerBaseCycleFixed;
                        long targetTicks = emuStartTicks + (long)(emuTicksAccFixed >> 32);

                        long now0 = Stopwatch.GetTimestamp();
                        long remainingTicks0 = targetTicks - now0;
                        // If we're significantly behind (e.g., breakpoint / OS stall), resync.
                        // Prevents long catch-up runs that feel like stutter.
                        if (remainingTicks0 < -Stopwatch.Frequency / 4)
                        {
                            emuStartTicks = now0;
                            emuTicksAccFixed = 0;
                            continue;
                        }

                        while (true)
                        {
                            long now = Stopwatch.GetTimestamp();
                            long remainingTicks = targetTicks - now;
                            if (remainingTicks <= 0)
                            {
                                break;
                            }

                            // Sleep while we're comfortably ahead, then spin for sub-millisecond precision.
                            if (remainingTicks > Stopwatch.Frequency / 500) // > ~2ms
                            {
                                Thread.Sleep(1);
                            }
                            else
                            {
                                Thread.SpinWait(200);
                            }
                        }

                        long afterWait = Stopwatch.GetTimestamp();
                        waitMs = (afterWait - emuWorkEnd) * 1000.0 / Stopwatch.Frequency;
                    }

                    long frameHostEnd = Stopwatch.GetTimestamp();
                    double hostFrameMs = (frameHostEnd - frameHostStart) * 1000.0 / Stopwatch.Frequency;
                    double speed = hostFrameMs > 0 ? (targetFrameMs / hostFrameMs) : 0;

                    int gc0 = GC.CollectionCount(0) - gc0Start;
                    int gc1 = GC.CollectionCount(1) - gc1Start;
                    int gc2 = GC.CollectionCount(2) - gc2Start;

                    _stats = new EmulationStats
                    {
                        BaseCyclesLastFrame = baseCyclesThisFrame,
                        TargetFrameMs = targetFrameMs,
                        HostFrameMs = hostFrameMs,
                        EmuWorkMs = emuWorkMs,
                        EmuWorkCpuMs = emuWorkCpuMs,
                        ThrottleWaitMs = waitMs,
                        SpeedMultiplier = speed,
                        HotPc = hotPc,
                        HotPcRepeats = hotPcRepeats,

                        InstructionsThisFrame = instructionsThisFrame,

                        HasThreadCpuCycles = hasThreadCpuCycles,
                        ThreadCpuCyclesThisFrame = threadCpuCyclesThisFrame,

                        AllocBytesThisFrame = allocBytes,

                        Gc0Collections = gc0,
                        Gc1Collections = gc1,
                        Gc2Collections = gc2,
                    };
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BeginMCycle()
        {
            CurrentMCycleOamAccess = OamAccessKind.None;
            CurrentMCycleIduInOamRange = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MarkOamRead()
        {
            CurrentMCycleOamAccess |= OamAccessKind.Read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MarkOamWrite()
        {
            CurrentMCycleOamAccess |= OamAccessKind.Write;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MarkIdu(ushort valueBefore)
        {
            if (valueBefore >= 0xFE00 && valueBefore <= 0xFEFF)
            {
                CurrentMCycleIduInOamRange = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyOamBugIfNeeded()
        {
            // DMG-only OAM corruption bug. CGB does not exhibit it.
            if (_memory.IsCgb)
            {
                return;
            }

            if (CurrentMCycleOamAccess == OamAccessKind.None && !CurrentMCycleIduInOamRange)
            {
                return;
            }

            // Only relevant during visible scanlines while LCD is on and PPU is in Mode 2 (OAM scan).
            int row = _ppu.GetOamScanRowForCurrentMCycle();
            if (row < 0)
            {
                return;
            }

            bool read = (CurrentMCycleOamAccess & OamAccessKind.Read) != 0;
            bool write = (CurrentMCycleOamAccess & OamAccessKind.Write) != 0;

            if (read && CurrentMCycleIduInOamRange)
            {
                _ppu.ApplyOamCorruptionReadDuringIncDec(row);
            }
            else if (write)
            {
                // Write during IDU behaves like a single write.
                _ppu.ApplyOamCorruptionWrite(row);
            }
            else if (read)
            {
                _ppu.ApplyOamCorruptionRead(row);
            }
            else if (CurrentMCycleIduInOamRange)
            {
                _ppu.ApplyOamCorruptionWrite(row);
            }
        }

        private int GetBaseCyclesFromCpuCycles(int cpuCycles)
        {
            // In CGB double-speed, CPU runs twice as fast, while timer/PPU/APU remain on the base clock.
            // So each CPU t-cycle corresponds to 1/2 base cycles.
            return DoubleSpeed ? (cpuCycles / 2) : cpuCycles;
        }

        private void UpdateApu(int baseCycles)
        {
            Apu.Step(baseCycles);
        }

        public bool TrySwitchCgbSpeed()
        {
            if (!_memory.IsCgb || !_memory.Key1PrepareSpeedSwitch)
            {
                return false;
            }

            DoubleSpeed = !DoubleSpeed;
            _memory.ClearKey1SpeedSwitchPrepare();
            _memory.RefreshKey1();
            return true;
        }

        public byte[] GetDisplayArray()
        {
            return _ppu.Screen.GetBuffer();
        }

        private bool HandleInterrupts()
        {
            if (!InterruptMasterEnabled)
            {
                return false;
            }

            if (_memory.IFVBlank && _memory.IEVBlank)
            {
                _memory.IFVBlank = false;
                InterruptMasterEnabled = false;
                _imeEnableDelayInstructions = 0;
                _memory.WriteByte(--SP, (byte)(PC >> 8));
                _memory.WriteByte(--SP, (byte)(PC & 0xFF));
                PC = 0x40;
                return true;
            }
            if (_memory.IFLCDStat && _memory.IELCDStat)
            {
                _memory.IFLCDStat = false;
                InterruptMasterEnabled = false;
                _imeEnableDelayInstructions = 0;
                _memory.WriteByte(--SP, (byte)(PC >> 8));
                _memory.WriteByte(--SP, (byte)(PC & 0xFF));
                PC = 0x48;
                return true;
            }
            if (_memory.IFTimer && _memory.IETimer)
            {
                _memory.IFTimer = false;
                InterruptMasterEnabled = false;
                _imeEnableDelayInstructions = 0;
                _memory.WriteByte(--SP, (byte)(PC >> 8));
                _memory.WriteByte(--SP, (byte)(PC & 0xFF));
                PC = 0x50;
                return true;
            }
            if (_memory.IFSerial && _memory.IESerial)
            {
                _memory.IFSerial = false;
                InterruptMasterEnabled = false;
                _imeEnableDelayInstructions = 0;
                _memory.WriteByte(--SP, (byte)(PC >> 8));
                _memory.WriteByte(--SP, (byte)(PC & 0xFF));
                PC = 0x58;
                return true;
            }
            if (_memory.IFJoypad && _memory.IEJoypad)
            {
                _memory.IFJoypad = false;
                InterruptMasterEnabled = false;
                _imeEnableDelayInstructions = 0;
                _memory.WriteByte(--SP, (byte)(PC >> 8));
                _memory.WriteByte(--SP, (byte)(PC & 0xFF));
                PC = 0x60;
                return true;
            }

            return false;
        }

        public void ScheduleEnableInterrupts()
        {
            // EI enables IME after the *following* instruction executes.
            // Implemented as a 2-instruction delay so the decrement at end-of-instruction doesn't enable immediately.
            _imeEnableDelayInstructions = 2;
        }

        public void DisableInterruptsImmediate()
        {
            InterruptMasterEnabled = false;
            _imeEnableDelayInstructions = 0;
        }

        public void EnableInterruptsImmediate()
        {
            InterruptMasterEnabled = true;
            _imeEnableDelayInstructions = 0;
        }

        private void AdvanceImeDelayAfterInstruction()
        {
            if (_imeEnableDelayInstructions <= 0)
            {
                return;
            }

            _imeEnableDelayInstructions--;
            if (_imeEnableDelayInstructions == 0)
            {
                InterruptMasterEnabled = true;
            }
        }

        private bool IsInterruptRequested()
        {
            return (_memory.IF & _memory.InterruptEnableRegister) != 0;
        }

        private void UpdateTimer(int cycles)
        {
            // increment timer
            DIVCounter += cycles;

            while (DIVCounter >= 256)
            {
                DIVCounter -= 256;
                _memory.DIV++;
            }

            // is clock enabled?
            if (_memory.TimerEnabled)
            {
                _mClockCount -= cycles;
                while (_mClockCount <= 0)
                {
                    if (_timerPeriod == 0) SetClockFrequency();
                    _mClockCount += _timerPeriod;

                    // increment timer
                    _memory.TIMA++;
                    // check if timer overflows
                    if (_memory.TIMA == 0)
                    {
                        _memory.TIMA = _memory.TMA;
                        _memory.IFTimer = true;
                    }
                }
            }
        }

        public byte GetClockFrequency()
        {
            return (byte)(_memory.TAC & 0x3);
        }

        public void SetClockFrequency()
        {
            var frequency = GetClockFrequency();
            _timerPeriod = frequency switch
            {
                0 => 1024,
                1 => 16,
                2 => 64,
                3 => 256,
                _ => throw new Exception("Invalid clock frequency")
            };
            _mClockCount = _timerPeriod;
        }

        public void ResetTimerCounter()
        {
            _mClockCount = _timerPeriod;
        }

        public void UpdateGraphics(int cycles)
        {
            // during each scanline, the STAT interrupt can be called multiple times for each mode, but make sure it only fires once and only once per scanline,
            SetLCDStatus();

            if (_memory.LCDEnabled)
            {
                _scanlineCounter -= cycles;
            }
            else
            {
                return;
            }

            if (_scanlineCounter <= 0)
            {
                _memory.LY++;
                byte currentLine = _memory.LY;
                _scanlineCounter = 456;

                if (currentLine == 144)
                {
                    // VBlank
                    _memory.IFVBlank = true;
                }
                else if (currentLine > 153)
                {
                    _memory.LY = 0;
                }
                else if (currentLine < 144)
                {
                    DrawScanline();
                }
            }
        }
        public void RequestInterrupt(Interrupt interrupt)
        {
            byte req = (byte)(_memory.IF | 0xE0);
            _memory.IF = (byte)(req | 1 << (int)interrupt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawScanline()
        {
            if (_memory.BGDisplay)
            {
                DrawBackground();
            }

            if (_memory.SpriteDisplay)
            {
                DrawSprites();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawSprites()
        {
            bool use8x16 = _memory.OBJSize;

            for (int sprite = 0; sprite < 40; sprite++)
            {
                byte index = (byte)(sprite * 4);
                byte yPos = (byte)(_memory.ReadByte((ushort)(0xFE00 + index)) - 16);
                byte xPos = (byte)(_memory.ReadByte((ushort)(0xFE00 + index + 1)) - 8);
                byte tileLocation = _memory.ReadByte((ushort)(0xFE00 + index + 2));
                byte attributes = _memory.ReadByte((ushort)(0xFE00 + index + 3));

                bool yFlip = attributes.TestBit(6);
                bool xFlip = attributes.TestBit(5);

                int ySize = use8x16 ? 16 : 8;

                int scanline = _memory.LY;

                if (scanline >= yPos && scanline < (yPos + ySize))
                {
                    int line = scanline - yPos;

                    if (yFlip)
                    {
                        line -= ySize;
                        line *= -1;
                    }

                    line *= 2;
                    ushort dataAddress = (ushort)(0x8000 + (tileLocation * 16) + line);
                    byte data1 = _memory.ReadByte(dataAddress);
                    byte data2 = _memory.ReadByte((ushort)(dataAddress + 1));

                    for (int tilePixel = 7; tilePixel >= 0; tilePixel--)
                    {
                        int colorBit = tilePixel;

                        if (xFlip)
                        {
                            colorBit -= 7;
                            colorBit *= -1;
                        }

                        int colorNum = 0;
                        colorNum |= data2.TestBit(colorBit) ? 1 : 0;
                        colorNum <<= 1;
                        colorNum |= data1.TestBit(colorBit) ? 1 : 0;

                        Color color = GetColor((byte)colorNum, 0xFF48);

                        int red = 0;
                        int green = 0;
                        int blue = 0;
                        byte alpha = 255;

                        switch (color)
                        {
                            case Color color1 when color1.R == Color.White.R && color1.G == Color.White.G && color1.B == Color.White.B:
                                red = 255;
                                green = 255;
                                blue = 255;
                                alpha = 0;
                                break;
                            case Color color2 when color2.R == Color.LightGray.R && color2.G == Color.LightGray.G && color2.B == Color.LightGray.B:
                                red = 0xCC;
                                green = 0xCC;
                                blue = 0xCC;
                                break;
                            case Color color3 when color3.R == Color.DarkGray.R && color3.G == Color.DarkGray.G && color3.B == Color.DarkGray.B:
                                red = 0x77;
                                green = 0x77;
                                blue = 0x77;
                                break;
                            case Color color4 when color4.R == Color.Black.R && color4.G == Color.Black.G && color4.B == Color.Black.B:
                                red = 0;
                                green = 0;
                                blue = 0;
                                break;
                        }

                        int xPix = 0 - tilePixel;
                        xPix += 7;

                        int pixel = xPos + xPix;

                        if (scanline < 0 || scanline > 143 || pixel < 0 || pixel > 159)
                        {
                            continue;
                        }

                        _display[pixel, scanline, 0] = (byte)red;
                        _display[pixel, scanline, 1] = (byte)green;
                        _display[pixel, scanline, 2] = (byte)blue;
                        _display[pixel, scanline, 3] = alpha;

                        // alternatively, use the colorNum directly in a 160*144 array to represent the pixel
                        // use pixel and finalY to determine the position in the array
                    }
                    _pixels = Flatten(_display);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawBackground()
        {
            ushort tileData = 0;
            ushort backgroundMemory = 0;
            bool unsigned = true;

            byte scrollX = _memory.SCX;
            byte scrollY = _memory.SCY;
            byte windowX = _memory.WX;
            byte windowY = _memory.WY;

            bool usingWindow = false;

            if (_memory.WindowDisplayEnable)
            {
                if (windowY <= _memory.LY)
                    usingWindow = true;
            }

            if (_memory.BGWindowTileDataSelect)
            {
                tileData = 0x8000;
            }
            else
            {
                tileData = 0x8800;
                unsigned = false;
            }

            if (usingWindow)
            {
                if (_memory.WindowTileMapDisplaySelect)
                {
                    backgroundMemory = 0x9C00;
                }
                else
                {
                    backgroundMemory = 0x9800;
                }
            }
            else
            {
                if (_memory.BGWindowTileDataSelect)
                {
                    backgroundMemory = 0x9C00;
                }
                else
                {
                    backgroundMemory = 0x9800;
                }
            }

            byte yPos;

            if (usingWindow)
            {
                yPos = (byte)(_memory.LY - windowY);
            }
            else
            {
                yPos = (byte)(scrollY + _memory.LY);
            }


            for (int pixel = 0; pixel < 160; pixel++)
            {
                byte xPos = (byte)(pixel + scrollX);

                if (usingWindow)
                {
                    if (pixel >= windowX)
                    {
                        xPos = (byte)(pixel - windowX);
                      }
                }

                ushort tileRow = (ushort)((yPos / 8) * 32);
                ushort tileCol = (ushort)(xPos / 8);
                // tileData memory location to use
                ushort tileLocation = tileData;
                // backroundMemory = tilemap to use
                ushort tileAddress = (ushort)(backgroundMemory + tileRow + tileCol);
                int tileNum;

                if (unsigned)
                {
                    tileNum = _memory.ReadByte(tileAddress);
                }
                else
                {
                    tileNum = (sbyte)_memory.ReadByte(tileAddress);
                }


                if (unsigned)
                {
                    tileLocation += (ushort)(tileNum * 16);
                }
                else
                {
                    tileLocation += (ushort)((tileNum + 128) * 16);
                }

                byte line = (byte)(yPos % 8);
                line *= 2;

                byte data1 = _memory.ReadByte((ushort)(tileLocation + line));
                byte data2 = _memory.ReadByte((ushort)(tileLocation + line + 1));

                int colorBit = xPos % 8;
                colorBit -= 7;
                colorBit *= -1;

                int colorNum = 0;
                colorNum |= data2.TestBit(colorBit) ? 1 : 0;
                colorNum <<= 1;
                colorNum |= data1.TestBit(colorBit) ? 1 : 0;

                Color color = GetColor((byte)colorNum, 0xFF47);

                int red = 0;
                int green = 0;
                int blue = 0;

                switch (color)
                {
                    case Color color1 when color1.R == Color.White.R && color1.G == Color.White.G && color1.B == Color.White.B:
                        red = 255;
                        green = 255;
                        blue = 255;
                        break;
                    case Color color2 when color2.R == Color.LightGray.R && color2.G == Color.LightGray.G && color2.B == Color.LightGray.B:
                        red = 0xCC;
                        green = 0xCC;
                        blue = 0xCC;
                        break;
                    case Color color3 when color3.R == Color.DarkGray.R && color3.G == Color.DarkGray.G && color3.B == Color.DarkGray.B:
                        red = 0x77;
                        green = 0x77;
                        blue = 0x77;
                        break;
                    case Color color4 when color4.R == Color.Black.R && color4.G == Color.Black.G && color4.B == Color.Black.B:
                        red = 0;
                        green = 0;
                        blue = 0;
                        break;
                }

                int finalY = _memory.LY;

                if (finalY < 0 || finalY > 143 || pixel < 0 || pixel > 159)
                {
                    continue;
                }

                _display[pixel, finalY, 0] = (byte)red;
                _display[pixel, finalY, 1] = (byte)green;
                _display[pixel, finalY, 2] = (byte)blue;
                _display[pixel, finalY, 3] = 0xFF;


                // alternatively, use the colorNum directly in a 160*144 array to represent the pixel
                // use pixel and finalY to determine the position in the array
                //_pixels[pixel + finalY * 160] = (byte)colorNum;
            }
            _pixels = Flatten(_display);
        }

        private byte[] Flatten(byte[,,] display)
        {
            byte[] pixels = new byte[160 * 144 * 4];
            int index = 0;
            for (int y = 0; y < 144; y++)
            {
                for (int x = 0; x < 160; x++)
                {
                    pixels[index++] = display[x, y, 0];
                    pixels[index++] = display[x, y, 1];
                    pixels[index++] = display[x, y, 2];
                    pixels[index++] = display[x, y, 3];
                }
            }
            return pixels;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Color GetColor(byte colorNum, ushort address)
        {
            byte palette = _memory.ReadByte(address);
            byte hi = 0;
            byte lo = 0;

            switch (colorNum)
            {
                case 0:
                    hi = 1;
                    lo = 0;
                    break;
                case 1:
                    hi = 3;
                    lo = 2;
                    break;
                case 2:
                    hi = 5;
                    lo = 4;
                    break;
                case 3:
                    hi = 7;
                    lo = 6;
                    break;
            }

            int color = 0;
            color |= palette.TestBit(hi) ? 1 : 0;
            color <<= 1;
            color |= palette.TestBit(lo) ? 1 : 0;

            switch (color)
            {
                case 0:
                    return Color.White;
                case 1:
                    return Color.LightGray;
                case 2:
                    return Color.DarkGray;
                case 3:
                    return Color.Black;
            }

            return Color.Black;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetLCDStatus()
        {
            byte status = _memory.LCDStatus;

            if (!_memory.LCDEnabled)
            {
                _scanlineCounter = 456;
                _memory.LY = 0;
                status &= 0b1111_1100;
                status |= 0b01;
                _memory.LCDStatus = status;
                return;
            }

            byte currentLine = _memory.LY;
            byte currentMode = (byte)(status & 0b11);
            byte mode;
            bool reqInt = false;

            if (currentLine >= 144)
            {
                mode = 0;
                status |= 0b01;
                reqInt = status.TestBit(4);
            }
            else
            {
                int mode2Bounds = 456 - 80;
                int mode3Bounds = mode2Bounds - 172;

                if (_scanlineCounter >= mode2Bounds)
                {
                    mode = (byte)2;
                    status |= 0b10;
                    reqInt = status.TestBit(5);
                }
                else if (_scanlineCounter >= mode3Bounds)
                {
                    mode = (byte)3;
                    status |= 0b11;
                }
                else
                {
                    mode = (byte)0;
                    status &= 0b1111_1100;
                    reqInt = status.TestBit(3);
                }
            }

            if (reqInt && mode != currentMode)
            {
                _memory.IFLCDStat = true;
                _memory.IF = 0x02;
            }

            if (_memory.LY == _memory.LYC)
            {
                status |= 0b100;
                if (status.TestBit(6))
                {
                    _memory.IFLCDStat = true;
                    _memory.IF = 0x02;
                }
            }
            else
            {
                status &= 0b1111_1011;
            }

            _memory.LCDStatus = status;
        }

        public void LoadRom(string path)
        {
            // Open the file as a stream of bytes
            var rom = File.ReadAllBytes(path);

            // Create a buffer to hold the contents
            // Read the file into the buffer
            _memory.InitialiseGame(rom);

            // Set post-boot register state for DMG vs CGB so test ROMs that check the boot identifier work.
            // Many blargg ROMs use A's bit 4 to identify CGB.
            DoubleSpeed = false;
            if (_memory.IsCgb)
            {
                A = 0x11;
            }
            else
            {
                A = 0x01;
            }
            _memory.RefreshKey1();

            // Warm up likely-to-JIT hotspots so the first rendered frames don't hitch.
            // This doesn't advance emulation time; it just forces one-time compilation.
            _opcodeHandler.WarmupJit();
            _ppu.WarmupJit();
            Apu.Step(0);

            if (AggressiveJitWarmup)
            {
                WarmupJitAggressive();
            }
        }

        private void WarmupJitAggressive()
        {
            // JIT compilation uses unmanaged memory, so it can show up as a large CPU-time spike
            // with 0 managed allocations. Pre-JIT most of the emulator's code to shift that cost
            // to ROM load.
            try
            {
                var asm = typeof(Gameboy).Assembly;
                var seen = new HashSet<IntPtr>();

                foreach (var t in asm.GetTypes())
                {
                    string? name = t.FullName;
                    if (name == null || !name.StartsWith("GBOG.", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Prepare type initializer (static constructor) if present.
                    var cctor = t.TypeInitializer;
                    if (cctor != null && !cctor.ContainsGenericParameters)
                    {
                        var mh = cctor.MethodHandle;
                        if (mh.Value != IntPtr.Zero && seen.Add(mh.Value))
                        {
                            try { RuntimeHelpers.PrepareMethod(mh); } catch { }
                        }
                    }

                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

                    foreach (var m in t.GetMethods(flags))
                    {
                        if (m.IsAbstract || m.ContainsGenericParameters)
                        {
                            continue;
                        }

                        var mh = m.MethodHandle;
                        if (mh.Value == IntPtr.Zero || !seen.Add(mh.Value))
                        {
                            continue;
                        }

                        try { RuntimeHelpers.PrepareMethod(mh); } catch { }
                    }

                    foreach (var ctor in t.GetConstructors(flags))
                    {
                        if (ctor.ContainsGenericParameters)
                        {
                            continue;
                        }

                        var mh = ctor.MethodHandle;
                        if (mh.Value == IntPtr.Zero || !seen.Add(mh.Value))
                        {
                            continue;
                        }

                        try { RuntimeHelpers.PrepareMethod(mh); } catch { }
                    }
                }
            }
            catch
            {
                // Best-effort only.
            }
        }

        public async Task<bool> RunGame()
        {
            //LogSystemState();
            return await DoLoop();
        }

        public void EndGame()
        {
            _exit = true;
            ConfigureAudioOutput(false);
        }

        public event EventHandler<string>? LogAdded;
        private void LogSystemState()
        {

            // Format A: 01 F: B0 B: 00 C: 13 D: 00 E: D8 H: 01 L: 4D SP: FFFE PC: 00:0100 (00 C3 13 02)
            // Format: [registers] (mem[pc] mem[pc+1] mem[pc+2] mem[pc+3])
            // All of the values between A and PC are the hex-encoded values of the corresponding registers. 
            // The final values in brackets (00 C3 13 02) are the 4 bytes stored in the memory locations near PC (ie. the values at pc,pc+1,pc+2,pc+3).
            // The values in brackets are useful for debugging, as they show the next few bytes of the program.
            var text = $"A: {A:X2} F: {F:X2} B: {B:X2} C: {C:X2} D: {D:X2} E: {E:X2} H: {H:X2} L: {L:X2} SP: {SP:X4} PC: 00:{PC:X4} ({_memory.ReadByte(PC):X2} {_memory.ReadByte((ushort)(PC + 1)):X2} {_memory.ReadByte((ushort)(PC + 2)):X2} {_memory.ReadByte((ushort)(PC + 3)):X2})";

            Log.Information(text);

        }
    }
}
