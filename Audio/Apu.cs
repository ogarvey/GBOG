using System;

namespace GBOG.Audio;

public sealed class Apu
{
    // DMG base clock
    public const int BaseClockHz = 4_194_304;

    private readonly byte[] _regs = new byte[0x30]; // FF10-FF3F
    private readonly byte[] _waveRam = new byte[0x10]; // FF30-FF3F

    private bool _powered;

    // Hardware mode differences (DMG/MGB vs CGB).
    // Set by GBMemory.InitialiseGame based on cartridge + user preference.
    public bool IsCgb { get; set; }

    // CPU bus accesses happen partway through an M-cycle, while we tick the APU at the end of
    // each M-cycle. For the extremely timing-sensitive DMG CH3 wave RAM behavior, apply a
    // small phase offset when checking the CPU-access window.
    private const int DmgCh3CpuAccessPhaseOffsetCycles = 2;

    // Frame sequencer
    private int _frameSeqCycleCounter;
    private int _frameSeqStep;

    // Audio output timing
    private readonly int _sampleRate;
    private readonly int _cyclesPerSampleFixed; // 16.16 fixed-point cycles/sample
    private int _sampleCycleAccFixed;

    private readonly short[] _mixBuffer;
    private int _mixBufferIndex;

    private IAudioSink? _sink;

    private readonly PulseChannel _ch1;
    private readonly PulseChannel _ch2;
    private readonly WaveChannel _ch3;
    private readonly NoiseChannel _ch4;

    private readonly DcBlockFilter _hpfL;
    private readonly DcBlockFilter _hpfR;

    public Apu(int sampleRate = 44100, int bufferFrames = 2048)
    {
        _sampleRate = sampleRate;
        _cyclesPerSampleFixed = (int)Math.Round((BaseClockHz * 65536.0) / sampleRate);
        _mixBuffer = new short[bufferFrames * 2];

        _ch1 = new PulseChannel(hasSweep: true);
        _ch2 = new PulseChannel(hasSweep: false);
        _ch3 = new WaveChannel(_waveRam);
        _ch4 = new NoiseChannel();

        _hpfL = new DcBlockFilter(sampleRate);
        _hpfR = new DcBlockFilter(sampleRate);

        // Initialize to a reasonable post-boot state for normal gameplay.
        // Note: NR52 power-cycling behavior (writing bit7) differs from initial power-up.
        HardResetToPostBootState();
    }

    public void SetSink(IAudioSink? sink) => _sink = sink;

    public bool Powered => _powered;

    private void HardResetToPostBootState()
    {
        _powered = true;
        // Keep wave RAM unchanged per Pan Docs.

        _frameSeqCycleCounter = 0;
        _frameSeqStep = 0;

        _ch1.Reset(poweredOn: true);
        _ch2.Reset(poweredOn: true);
        _ch3.Reset(poweredOn: true);
        _ch4.Reset(poweredOn: true);

        // Initialize register-backed state to match the emulator's post-boot IO defaults
        // (as set in GBMemory.InitialiseIORegisters), without triggering channels.
        Array.Clear(_regs);

        _regs[0x00] = 0x80; // FF10 NR10
        _regs[0x01] = 0xBF; // FF11 NR11
        _regs[0x02] = 0xF3; // FF12 NR12
        _regs[0x03] = 0xC1; // FF13 NR13 (write-only, but emulator historically seeded a value)
        _regs[0x04] = 0xBF; // FF14 NR14 (read mask supplies 0xBF anyway)

        _regs[0x05] = 0xFF; // FF15 (N/A)
        _regs[0x06] = 0x3F; // FF16 NR21
        _regs[0x09] = 0xBF; // FF19 NR24

        _regs[0x0A] = 0x7F; // FF1A NR30
        _regs[0x0B] = 0xFF; // FF1B NR31
        _regs[0x0C] = 0x9F; // FF1C NR32
        _regs[0x0E] = 0xBF; // FF1E NR34

        _regs[0x10] = 0xFF; // FF20 NR41
        _regs[0x13] = 0xBF; // FF23 NR44

        _regs[0x14] = 0x77; // FF24 NR50
        _regs[0x15] = 0xF3; // FF25 NR51
        _regs[0x16] = 0x80; // FF26 NR52 (bit7 only in our storage)

        // Mirror these defaults into the channel state machines.
        _ch1.WriteNr10(_regs[0x00]);
        _ch1.WriteNr11(_regs[0x01]);
        _ch1.WriteNr12(_regs[0x02]);
        _ch1.WriteNr13(_regs[0x03]);
        _ch1.WriteNr14(_regs[0x04]);

        _ch2.WriteNr11(_regs[0x06]);
        _ch2.WriteNr14(_regs[0x09]);

        _ch3.WriteNr30(_regs[0x0A]);
        _ch3.WriteNr31(_regs[0x0B]);
        _ch3.WriteNr32(_regs[0x0C]);
        _ch3.WriteNr34(_regs[0x0E]);

        _ch4.WriteNr41(_regs[0x10]);
        _ch4.WriteNr44(_regs[0x13]);

        _ch1.RefreshDac();
        _ch2.RefreshDac();
        _ch3.RefreshDac();
        _ch4.RefreshDac();
    }

    private void PowerOnFromNr52()
    {
        _powered = true;

        // Powering on via NR52 clears all APU registers (NR10-NR51) to 0
        // (wave RAM unaffected) and resets the frame sequencer.
        Array.Clear(_regs);
        _frameSeqCycleCounter = 0;
        _frameSeqStep = 0;

        // Reset channel state machines but preserve length counters on DMG.
        bool resetLengthCounters = IsCgb;
        _ch1.OnPowerOn(resetLengthCounters);
        _ch2.OnPowerOn(resetLengthCounters);
        _ch3.OnPowerOn(resetLengthCounters);
        _ch4.OnPowerOn(resetLengthCounters);
    }

    public void PowerOff()
    {
        _powered = false;

        // Turning off clears all APU registers (NR10-NR51) and makes them read-only until re-enabled.
        // Wave RAM is unaffected.
        Array.Clear(_regs);

        // On DMG, length counters are preserved across power-off; on CGB they reset.
        bool resetLengthCounters = IsCgb;
        _ch1.OnPowerOff(resetLengthCounters);
        _ch2.OnPowerOff(resetLengthCounters);
        _ch3.OnPowerOff(resetLengthCounters);
        _ch4.OnPowerOff(resetLengthCounters);

        // Note: DIV-APU not modeled via DIV edges; our frame sequencer is cycle-based.
    }

    public byte ReadRegister(ushort address)
    {
        if (address >= 0xFF30 && address <= 0xFF3F)
        {
            return ReadWaveRam((byte)(address - 0xFF30));
        }

        if (address < 0xFF10 || address > 0xFF26)
        {
            return 0xFF;
        }

        int i = address - 0xFF10;
        byte v = _regs[i];

        // If powered off, registers (except NR52 and wave RAM) are read-only and effectively cleared.
        if (!_powered && address != 0xFF26)
        {
            v = 0x00;
        }

        return address switch
        {
            0xFF10 => (byte)(0x80 | (v & 0x7F)),
            0xFF11 => (byte)(0x3F | (v & 0xC0)),
            0xFF12 => v,
            0xFF13 => 0xFF,
            0xFF14 => (byte)(0xBF | (v & 0x40)),

            0xFF15 => 0xFF,
            0xFF16 => (byte)(0x3F | (v & 0xC0)),
            0xFF17 => v,
            0xFF18 => 0xFF,
            0xFF19 => (byte)(0xBF | (v & 0x40)),

            0xFF1A => (byte)(0x7F | (v & 0x80)),
            0xFF1B => 0xFF,
            0xFF1C => (byte)(0x9F | (v & 0x60)),
            0xFF1D => 0xFF,
            0xFF1E => (byte)(0xBF | (v & 0x40)),

            0xFF1F => 0xFF,
            0xFF20 => 0xFF,
            0xFF21 => v,
            0xFF22 => v,
            0xFF23 => (byte)(0xBF | (v & 0x40)),

            0xFF24 => v,
            0xFF25 => v,
            0xFF26 => ReadNr52(),

            _ => v,
        };
    }

    private byte ReadNr52()
    {
        byte status = 0x70;
        if (_powered)
        {
            status |= 0x80;
        }

        if (_ch1.Active) status |= 0x01;
        if (_ch2.Active) status |= 0x02;
        if (_ch3.Active) status |= 0x04;
        if (_ch4.Active) status |= 0x08;

        return status;
    }

    public void WriteRegister(ushort address, byte value)
    {
        if (address >= 0xFF30 && address <= 0xFF3F)
        {
            WriteWaveRam((byte)(address - 0xFF30), value);
            return;
        }

        if (address < 0xFF10 || address > 0xFF26)
        {
            return;
        }

        // If powered off, only NR52 is writable, plus length-timer loads on DMG, plus wave RAM.
        // Pan Docs / dmg_sound: length counters persist on DMG and can be written while off.
        if (!_powered && address != 0xFF26)
        {
            bool isLengthLoadRegister = address is 0xFF11 or 0xFF16 or 0xFF1B or 0xFF20;
            if (!(isLengthLoadRegister && !IsCgb))
            {
                return;
            }
            // Do not store the written value in _regs while powered off; only update the internal length counter.
            switch (address)
            {
                case 0xFF11:
                    _ch1.WriteNr11(value);
                    break;
                case 0xFF16:
                    _ch2.WriteNr11(value);
                    break;
                case 0xFF1B:
                    _ch3.WriteNr31(value);
                    break;
                case 0xFF20:
                    _ch4.WriteNr41(value);
                    break;
            }
            return;
        }

        int idx = address - 0xFF10;

        byte oldValue = _regs[idx];

        switch (address)
        {
            case 0xFF26:
            {
                bool enable = (value & 0x80) != 0;
                if (enable && !_powered)
                {
                    PowerOnFromNr52();
                }
                else if (!enable && _powered)
                {
                    PowerOff();
                }
                // Keep stored reg value for debugging; only bit7 is meaningful.
                _regs[idx] = (byte)(value & 0x80);
                return;
            }
        }

        _regs[idx] = value;

        // Length enable extra clocking quirk (DMG/CGB-04/05):
        // If the next frame sequencer step does NOT clock length (odd step),
        // and length enable transitions 0->1, decrement length once.
        // Additionally, trigger events that load length from 0 can load max-1 in that case.
        bool nextStepClocksLength = (_frameSeqStep & 1) == 0;
        bool oldLenEnabled = (oldValue & 0x40) != 0;
        bool newLenEnabled = (value & 0x40) != 0;
        bool trigger = (value & 0x80) != 0;
        bool lengthEnableTransition = !oldLenEnabled && newLenEnabled;
        bool doExtraLengthClockOnEnable = lengthEnableTransition && !nextStepClocksLength;

        // Dispatch to channel logic
        switch (address)
        {
            // CH1
            case 0xFF10:
                _ch1.WriteNr10(value);
                break;
            case 0xFF11:
                _ch1.WriteNr11(value);
                break;
            case 0xFF12:
                _ch1.WriteNr12(value);
                break;
            case 0xFF13:
                _ch1.WriteNr13(value);
                break;
            case 0xFF14:
                _ch1.WriteNr14(value);
                if (doExtraLengthClockOnEnable)
                {
                    _ch1.ClockLength();
                }
                if ((value & 0x80) != 0)
                {
                    // Period uses NR13/NR14 bits 0-2.
                    _ch1.Trigger(extraLengthClockIfReloadingFromZero: (!nextStepClocksLength && newLenEnabled));
                }
                break;

            // CH2
            case 0xFF16:
                _ch2.WriteNr11(value);
                break;
            case 0xFF17:
                _ch2.WriteNr12(value);
                break;
            case 0xFF18:
                _ch2.WriteNr13(value);
                break;
            case 0xFF19:
                _ch2.WriteNr14(value);
                if (doExtraLengthClockOnEnable)
                {
                    _ch2.ClockLength();
                }
                if ((value & 0x80) != 0)
                {
                    _ch2.Trigger(extraLengthClockIfReloadingFromZero: (!nextStepClocksLength && newLenEnabled));
                }
                break;

            // CH3
            case 0xFF1A:
                _ch3.WriteNr30(value);
                break;
            case 0xFF1B:
                _ch3.WriteNr31(value);
                break;
            case 0xFF1C:
                _ch3.WriteNr32(value);
                break;
            case 0xFF1D:
                _ch3.WriteNr33(value);
                break;
            case 0xFF1E:
                _ch3.WriteNr34(value);
                if (doExtraLengthClockOnEnable)
                {
                    _ch3.ClockLength();
                }
                if ((value & 0x80) != 0)
                {
                    if (_ch3.Active && !IsCgb && _ch3.IsDmgWaveFetchProximityWindow(windowCycles: 0, phaseOffsetCycles: 0))
                    {
                        ApplyDmgWaveTriggerCorruption();
                    }
                    _ch3.Trigger(extraLengthClockIfReloadingFromZero: (!nextStepClocksLength && newLenEnabled));
                }
                break;

            // CH4
            case 0xFF20:
                _ch4.WriteNr41(value);
                break;
            case 0xFF21:
                _ch4.WriteNr42(value);
                break;
            case 0xFF22:
                _ch4.WriteNr43(value);
                break;
            case 0xFF23:
                _ch4.WriteNr44(value);
                if (doExtraLengthClockOnEnable)
                {
                    _ch4.ClockLength();
                }
                if ((value & 0x80) != 0)
                {
                    _ch4.Trigger(extraLengthClockIfReloadingFromZero: (!nextStepClocksLength && newLenEnabled));
                }
                break;

            // Global
            case 0xFF24:
            case 0xFF25:
                break;
        }

        // DAC rules: disabling a DAC forces channel off.
        _ch1.RefreshDac();
        _ch2.RefreshDac();
        _ch3.RefreshDac();
        _ch4.RefreshDac();
    }

    private void ApplyDmgWaveTriggerCorruption()
    {
        // DMG wave RAM corruption when retriggering CH3 while active.
        // See gbdev wiki / Pan Docs: it rewrites the first bytes based on the currently read byte.
        int byteIndex = _ch3.GetDmgWaveCorruptionSourceByteIndex(phaseOffsetCycles: 0);
        if ((uint)byteIndex >= 16u)
        {
            return;
        }

        if (byteIndex <= 3)
        {
            _waveRam[0] = _waveRam[byteIndex];
            return;
        }

        int aligned = byteIndex & ~3;
        for (int i = 0; i < 4; i++)
        {
            _waveRam[i] = _waveRam[(aligned + i) & 0x0F];
        }
    }

    private byte ReadWaveRam(byte index)
    {
        if (!_ch3.Active)
        {
            return _waveRam[index & 0x0F];
        }

        // DMG timing window: accesses only work very close to the channel's own wave RAM read.
        // Use the wave timer phase as an approximation, since CPU/APU timing in this emulator
        // is instruction-granular.
        if (!IsCgb && !_ch3.IsCpuWaveRamWindowDmg(windowCycles: 2, phaseOffsetCycles: DmgCh3CpuAccessPhaseOffsetCycles))
        {
            return 0xFF;
        }

        // While playing, the CPU effectively accesses the byte CH3 is currently reading.
        // This is the behavior relied on by dmg_sound wave tests.
        int current = _ch3.GetDmgCpuVisibleWaveByteIndex(phaseOffsetCycles: DmgCh3CpuAccessPhaseOffsetCycles);
        if ((uint)current >= 16u)
        {
            return 0xFF;
        }
        return _waveRam[current];
    }

    private void WriteWaveRam(byte index, byte value)
    {
        if (!_ch3.Active)
        {
            _waveRam[index & 0x0F] = value;
            return;
        }

        if (!IsCgb && !_ch3.IsCpuWaveRamWindowDmg(windowCycles: 2, phaseOffsetCycles: DmgCh3CpuAccessPhaseOffsetCycles))
        {
            return;
        }

        int current = _ch3.GetDmgCpuVisibleWaveByteIndex(phaseOffsetCycles: DmgCh3CpuAccessPhaseOffsetCycles);
        if ((uint)current >= 16u)
        {
            return;
        }
        _waveRam[current] = value;
    }

    public void Step(int baseCycles)
    {
        if (!_powered)
        {
            // Still advance sample clock to keep sink fed with silence if desired.
            GenerateSamples(baseCycles, silent: true);
            return;
        }

        // Tick channel timers at base clock.
        _ch1.Step(baseCycles);
        _ch2.Step(baseCycles);
        _ch3.Step(baseCycles);
        _ch4.Step(baseCycles);

        // Frame sequencer at 512 Hz: 8192 base cycles per step.
        _frameSeqCycleCounter += baseCycles;
        while (_frameSeqCycleCounter >= 8192)
        {
            _frameSeqCycleCounter -= 8192;
            ClockFrameSequencer();
        }

        GenerateSamples(baseCycles, silent: false);
    }

    private void ClockFrameSequencer()
    {
        // Steps 0-7.
        // Length: steps 0,2,4,6
        // Sweep: steps 2,6
        // Envelope: step 7
        switch (_frameSeqStep)
        {
            case 0:
            case 2:
            case 4:
            case 6:
                _ch1.ClockLength();
                _ch2.ClockLength();
                _ch3.ClockLength();
                _ch4.ClockLength();
                break;
        }

        if (_frameSeqStep == 2 || _frameSeqStep == 6)
        {
            _ch1.ClockSweep();
        }

        if (_frameSeqStep == 7)
        {
            _ch1.ClockEnvelope();
            _ch2.ClockEnvelope();
            _ch4.ClockEnvelope();
        }

        _frameSeqStep = (_frameSeqStep + 1) & 7;
    }

    private void GenerateSamples(int baseCycles, bool silent)
    {
        // Fixed-point accumulator: add baseCycles in 16.16, compare to cycles/sample.
        _sampleCycleAccFixed += baseCycles << 16;

        while (_sampleCycleAccFixed >= _cyclesPerSampleFixed)
        {
            _sampleCycleAccFixed -= _cyclesPerSampleFixed;

            short l, r;
            if (silent)
            {
                l = 0;
                r = 0;
            }
            else
            {
                MixOneSample(out l, out r);
            }

            _mixBuffer[_mixBufferIndex++] = l;
            _mixBuffer[_mixBufferIndex++] = r;

            if (_mixBufferIndex >= _mixBuffer.Length)
            {
                FlushMixBuffer();
            }
        }
    }

    private void FlushMixBuffer()
    {
        if (_sink == null)
        {
            _mixBufferIndex = 0;
            return;
        }

        _sink.WriteSamples(_mixBuffer.AsSpan(0, _mixBufferIndex));
        _mixBufferIndex = 0;
    }

    private void MixOneSample(out short outL, out short outR)
    {
        byte nr50 = _regs[0x24 - 0x10];
        byte nr51 = _regs[0x25 - 0x10];

        float ch1 = _ch1.GetAnalogSample();
        float ch2 = _ch2.GetAnalogSample();
        float ch3 = _ch3.GetAnalogSample();
        float ch4 = _ch4.GetAnalogSample();

        float mixL = 0;
        float mixR = 0;

        if ((nr51 & 0x10) != 0) mixL += ch1;
        if ((nr51 & 0x20) != 0) mixL += ch2;
        if ((nr51 & 0x40) != 0) mixL += ch3;
        if ((nr51 & 0x80) != 0) mixL += ch4;

        if ((nr51 & 0x01) != 0) mixR += ch1;
        if ((nr51 & 0x02) != 0) mixR += ch2;
        if ((nr51 & 0x04) != 0) mixR += ch3;
        if ((nr51 & 0x08) != 0) mixR += ch4;

        int volL = (nr50 >> 4) & 0x07;
        int volR = nr50 & 0x07;

        float ampL = (volL + 1) / 8f;
        float ampR = (volR + 1) / 8f;

        mixL *= ampL;
        mixR *= ampR;

        // HPF / DC block
        mixL = _hpfL.Process(mixL);
        mixR = _hpfR.Process(mixR);

        // Soft clamp
        mixL = Math.Clamp(mixL, -1.0f, 1.0f);
        mixR = Math.Clamp(mixR, -1.0f, 1.0f);

        outL = (short)(mixL * short.MaxValue);
        outR = (short)(mixR * short.MaxValue);
    }

    private sealed class DcBlockFilter
    {
        private float _cap;
        private readonly float _charge;

        public DcBlockFilter(int sampleRate)
        {
            // Pan Docs reference: DMG charge factor at 44.1kHz is ~0.996.
            // Derive from 0.999958^(4194304/rate) so it adapts to sample rate.
            _charge = (float)Math.Pow(0.999958, BaseClockHz / (double)sampleRate);
        }

        public float Process(float input)
        {
            float output = input - _cap;
            _cap = input - output * _charge;
            return output;
        }
    }

    private static float DigitalToAnalog(int digital0To15)
    {
        // Pan Docs: DAC maps 0..15 linearly to +1..-1 (negative slope), centered at 7.5.
        return (7.5f - digital0To15) / 7.5f;
    }

    private sealed class PulseChannel
    {
        private readonly bool _hasSweep;

        // Registers / config
        private int _duty; // 0-3
        private int _lengthCounter; // 0-64
        private bool _lengthEnabled;

        private int _initialVolume;
        private bool _envelopeIncrease;
        private int _envelopePeriod;

        private int _period11; // 0-2047

        // Internal state
        private bool _active;
        private bool _dacEnabled;

        private int _volume;
        private int _envelopeTimer;

        private int _dutyStep;
        private int _freqTimerCycles;

        // Sweep (CH1 only)
        private int _sweepPace;
        private bool _sweepNegate;
        private int _sweepShift;
        private int _sweepTimer;
        private bool _sweepEnabled;
        private int _sweepShadow;
        private bool _sweepNegateUsedSinceTrigger;

        public PulseChannel(bool hasSweep)
        {
            _hasSweep = hasSweep;
            Reset(poweredOn: true);
        }

        public bool Active => _active;

        public void Reset(bool poweredOn)
        {
            _active = false;
            _dacEnabled = true;

            _duty = 0;
            _lengthCounter = 0;
            _lengthEnabled = false;

            _initialVolume = 0;
            _envelopeIncrease = false;
            _envelopePeriod = 0;

            _period11 = 0;

            _volume = 0;
            _envelopeTimer = 0;

            _dutyStep = 0;
            _freqTimerCycles = 0;

            _sweepPace = 0;
            _sweepNegate = false;
            _sweepShift = 0;
            _sweepTimer = 0;
            _sweepEnabled = false;
            _sweepShadow = 0;
            _sweepNegateUsedSinceTrigger = false;
        }

        public void OnPowerOff(bool resetLengthCounter)
        {
            _active = false;
            _dacEnabled = false;
            _lengthEnabled = false;
            _duty = 0;
            if (resetLengthCounter)
            {
                _lengthCounter = 0;
            }

            _initialVolume = 0;
            _envelopeIncrease = false;
            _envelopePeriod = 0;

            _period11 = 0;

            _volume = 0;
            _envelopeTimer = 0;

            _dutyStep = 0;
            _freqTimerCycles = 0;

            _sweepPace = 0;
            _sweepNegate = false;
            _sweepShift = 0;
            _sweepTimer = 0;
            _sweepEnabled = false;
            _sweepShadow = 0;
            _sweepNegateUsedSinceTrigger = false;
        }

        public void OnPowerOn(bool resetLengthCounter)
        {
            // Registers are cleared and channels stay disabled.
            OnPowerOff(resetLengthCounter);
        }

        public void RefreshDac()
        {
            // DAC enabled if NRx2 & 0xF8 != 0 (upper 5 bits).
            _dacEnabled = (_initialVolume != 0) || _envelopeIncrease;
            if (!_dacEnabled)
            {
                _active = false;
            }
        }

        public void WriteNr10(byte value)
        {
            if (!_hasSweep)
            {
                return;
            }
            bool oldNegate = _sweepNegate;
            _sweepPace = (value >> 4) & 0x07;
            _sweepNegate = (value & 0x08) != 0;
            _sweepShift = value & 0x07;

            // Quirk: clearing negate after it has been used in a sweep calculation disables CH1.
            if (oldNegate && !_sweepNegate && _sweepNegateUsedSinceTrigger)
            {
                _active = false;
            }
        }

        public void WriteNr11(byte value)
        {
            _duty = (value >> 6) & 0x03;
            int lenLoad = value & 0x3F;
            _lengthCounter = 64 - lenLoad;
        }

        public void WriteNr12(byte value)
        {
            _initialVolume = (value >> 4) & 0x0F;
            _envelopeIncrease = (value & 0x08) != 0;
            _envelopePeriod = value & 0x07;
        }

        public void WriteNr13(byte value)
        {
            _period11 = (_period11 & 0x700) | value;
        }

        public void WriteNr14(byte value)
        {
            _lengthEnabled = (value & 0x40) != 0;
            _period11 = (_period11 & 0x0FF) | ((value & 0x07) << 8);
        }

        public void Trigger(bool extraLengthClockIfReloadingFromZero)
        {
            bool hadDac = _dacEnabled;

            _active = true;
            if (_lengthCounter == 0)
            {
                _lengthCounter = extraLengthClockIfReloadingFromZero ? 63 : 64;
            }

            _volume = _initialVolume;
            _envelopeTimer = (_envelopePeriod == 0) ? 0 : _envelopePeriod;

            // Reload frequency timer.
            _freqTimerCycles = Math.Max(4, (2048 - _period11) * 4);

            if (_hasSweep)
            {
                _sweepShadow = _period11;
                _sweepTimer = (_sweepPace == 0) ? 8 : _sweepPace;
                _sweepEnabled = (_sweepPace != 0) || (_sweepShift != 0);
                _sweepNegateUsedSinceTrigger = false;

                if (_sweepShift != 0)
                {
                    int newPeriod = CalculateSweepNewPeriod();
                    if (_sweepNegate)
                    {
                        _sweepNegateUsedSinceTrigger = true;
                    }
                    if (newPeriod > 2047)
                    {
                        _active = false;
                    }
                }
            }

            // If the DAC is off, the channel cannot remain enabled, but the trigger side-effects
            // (length reload, sweep init, etc.) still occur.
            if (!hadDac)
            {
                _active = false;
            }
        }

        public void Step(int baseCycles)
        {
            if (!_active)
            {
                return;
            }

            _freqTimerCycles -= baseCycles;
            while (_freqTimerCycles <= 0)
            {
                _freqTimerCycles += Math.Max(4, (2048 - _period11) * 4);
                _dutyStep = (_dutyStep + 1) & 7;
            }
        }

        public void ClockLength()
        {
            if (_lengthEnabled && _lengthCounter > 0)
            {
                _lengthCounter--;
                if (_lengthCounter == 0)
                {
                    _active = false;
                }
            }
        }

        public void ClockEnvelope()
        {
            if (_envelopePeriod == 0)
            {
                return;
            }

            if (_envelopeTimer > 0)
            {
                _envelopeTimer--;
            }

            if (_envelopeTimer == 0)
            {
                _envelopeTimer = _envelopePeriod;
                if (_envelopeIncrease)
                {
                    if (_volume < 15) _volume++;
                }
                else
                {
                    if (_volume > 0) _volume--;
                }
            }
        }

        public void ClockSweep()
        {
            if (!_hasSweep)
            {
                return;
            }

            if (!_sweepEnabled)
            {
                return;
            }

            if (_sweepTimer > 0)
            {
                _sweepTimer--;
            }

            if (_sweepTimer == 0)
            {
                _sweepTimer = (_sweepPace == 0) ? 8 : _sweepPace;

                if (_sweepPace == 0)
                {
                    return;
                }

                int newPeriod = CalculateSweepNewPeriod();
                if (_sweepNegate)
                {
                    _sweepNegateUsedSinceTrigger = true;
                }
                if (newPeriod > 2047)
                {
                    _active = false;
                    return;
                }

                if (_sweepShift != 0)
                {
                    _sweepShadow = newPeriod;
                    _period11 = newPeriod;
                    // Second overflow check
                    int check = CalculateSweepNewPeriod();
                    if (check > 2047)
                    {
                        _active = false;
                    }
                }
            }
        }

        private int CalculateSweepNewPeriod()
        {
            int delta = _sweepShadow >> _sweepShift;
            int newPeriod = _sweepNegate ? (_sweepShadow - delta) : (_sweepShadow + delta);
            return newPeriod;
        }

        public float GetAnalogSample()
        {
            if (!_active || !_dacEnabled)
            {
                return 0;
            }

            int dutyMask = _duty switch
            {
                0 => 0b0000_0001,
                1 => 0b0000_0011,
                2 => 0b0000_1111,
                3 => 0b1111_1100,
                _ => 0b0000_0001,
            };

            bool high = ((dutyMask >> (7 - _dutyStep)) & 1) != 0;
            int digital = high ? _volume : 0;
            return DigitalToAnalog(digital);
        }
    }

    private sealed class WaveChannel
    {
        private readonly byte[] _waveRam;

        private bool _dacEnabled;
        private bool _active;

        private int _lengthCounter; // 0-256
        private bool _lengthEnabled;

        private int _outputLevel; // 0-3
        private int _period11;

        private int _sampleIndex; // 0-31
        private int _freqTimerCycles;
        private int _sampleLatch; // 0..15
        private int _lastWaveByteIndex; // 0-15
        private byte _sampleBufferByte;
        private int _cyclesSinceLastWaveByteFetch;

        public int CyclesUntilNextWaveRamRead => _freqTimerCycles;

        public int CurrentWaveByteIndex => _lastWaveByteIndex & 0x0F;

        public int GetDmgWaveCorruptionSourceByteIndex(int phaseOffsetCycles = 0)
        {
            // The corruption depends on which wave byte CH3 is reading when retriggered.
            // With our stepping, the best approximation is to choose between the last fetched
            // byte (just after a fetch) and the next-to-be-fetched byte (just before a fetch)
            // based on the current timer phase.
            int period = CurrentPeriodCycles;
            if (period <= 0)
            {
                return CurrentWaveByteIndex;
            }

            int t = _freqTimerCycles - phaseOffsetCycles;
            if (t < 0)
            {
                t = 0;
            }
            if (t > period)
            {
                t = period;
            }

            int cyclesUntilNextFetch = t;
            int cyclesSinceLastFetch = period - t;

            if (cyclesUntilNextFetch <= cyclesSinceLastFetch)
            {
                // Trigger is closer to the next fetch: use the next sample's containing byte.
                int nextSampleIndex = (_sampleIndex + 1) & 31;
                return (nextSampleIndex >> 1) & 0x0F;
            }

            return CurrentWaveByteIndex;
        }

        public int GetDmgCpuVisibleWaveByteIndex(int phaseOffsetCycles = 0)
        {
            // While CH3 is running on DMG, CPU accesses effectively observe the last wave RAM
            // byte that CH3 fetched into its internal sample buffer.
            return CurrentWaveByteIndex;
        }

        public bool IsDmgWaveFetchProximityWindow(int windowCycles, int phaseOffsetCycles = 0)
        {
            int period = CurrentPeriodCycles;
            if (period <= 0)
            {
                return false;
            }

            int t = _freqTimerCycles - phaseOffsetCycles;
            if (t < 0)
            {
                t = 0;
            }
            if (t > period)
            {
                t = period;
            }

            int cyclesUntilNextFetch = t;
            int cyclesSinceLastFetch = period - t;
            return Math.Min(cyclesUntilNextFetch, cyclesSinceLastFetch) <= windowCycles;
        }

        public bool IsCpuWaveRamWindowDmg(int windowCycles, int phaseOffsetCycles = 0)
        {
            // dmg_sound wave tests time CPU accesses relative to CH3's internal wave RAM *byte*
            // fetches (e.g. "2 clocks later"). With cycle-granular APU stepping, track this
            // directly.
            int cycles = _cyclesSinceLastWaveByteFetch + phaseOffsetCycles;
            if (cycles < 0)
            {
                cycles = 0;
            }

            // windowCycles is the specific "N cycles after fetch" point.
            return cycles == windowCycles;
        }

        public WaveChannel(byte[] waveRam)
        {
            _waveRam = waveRam;
            Reset(poweredOn: true);
        }

        public bool Active => _active;

        public void Reset(bool poweredOn)
        {
            _dacEnabled = false;
            _active = false;
            _lengthCounter = 0;
            _lengthEnabled = false;
            _outputLevel = 0;
            _period11 = 0;
            _sampleIndex = 0;
            _freqTimerCycles = 0;
            _sampleLatch = 0;
            _lastWaveByteIndex = 0;
            _sampleBufferByte = 0;
            _cyclesSinceLastWaveByteFetch = 0;
        }

        public void OnPowerOff(bool resetLengthCounter)
        {
            _active = false;
            _dacEnabled = false;
            _lengthEnabled = false;
            _outputLevel = 0;
            _period11 = 0;
            _sampleIndex = 0;
            _freqTimerCycles = 0;
            _sampleLatch = 0;
            _lastWaveByteIndex = 0;
            _sampleBufferByte = 0;
            _cyclesSinceLastWaveByteFetch = 0;
            if (resetLengthCounter)
            {
                _lengthCounter = 0;
            }
        }

        public void OnPowerOn(bool resetLengthCounter)
        {
            _active = false;
            _dacEnabled = false;
            _lengthEnabled = false;
            _outputLevel = 0;
            _period11 = 0;
            _sampleIndex = 0;
            _freqTimerCycles = 0;
            _sampleLatch = 0;
            _lastWaveByteIndex = 0;
            _sampleBufferByte = 0;
            _cyclesSinceLastWaveByteFetch = 0;
            if (resetLengthCounter)
            {
                _lengthCounter = 0;
            }
        }

        public void RefreshDac()
        {
            if (!_dacEnabled)
            {
                _active = false;
            }
        }

        public void WriteNr30(byte value)
        {
            _dacEnabled = (value & 0x80) != 0;
            if (!_dacEnabled)
            {
                _active = false;
            }
        }

        public void WriteNr31(byte value)
        {
            _lengthCounter = 256 - value;
        }

        public void WriteNr32(byte value)
        {
            _outputLevel = (value >> 5) & 0x03;
        }

        public void WriteNr33(byte value)
        {
            _period11 = (_period11 & 0x700) | value;
        }

        public void WriteNr34(byte value)
        {
            _lengthEnabled = (value & 0x40) != 0;
            _period11 = (_period11 & 0x0FF) | ((value & 0x07) << 8);
        }

        public void Trigger(bool extraLengthClockIfReloadingFromZero)
        {
            bool hadDac = _dacEnabled;

            _active = true;
            if (_lengthCounter == 0)
            {
                _lengthCounter = extraLengthClockIfReloadingFromZero ? 255 : 256;
            }

            _sampleIndex = 0;
            _freqTimerCycles = Math.Max(2, (2048 - _period11) * 2);
            // Pan Docs: last sample buffer behavior is quirky; keep current _sampleLatch.
            _lastWaveByteIndex = 0;
            _cyclesSinceLastWaveByteFetch = 0;

            if (!hadDac)
            {
                _active = false;
            }
        }

        public void Step(int baseCycles)
        {
            if (!_active)
            {
                return;
            }

            if (_cyclesSinceLastWaveByteFetch < 1_000_000)
            {
                _cyclesSinceLastWaveByteFetch += baseCycles;
            }

            _freqTimerCycles -= baseCycles;
            while (_freqTimerCycles <= 0)
            {
                _freqTimerCycles += Math.Max(2, (2048 - _period11) * 2);
                _sampleIndex = (_sampleIndex + 1) & 31;

                // Hardware fetches a new byte from wave RAM only on even sample indices.
                if ((_sampleIndex & 1) == 0)
                {
                    int byteIndex = (_sampleIndex >> 1) & 0x0F;
                    _lastWaveByteIndex = byteIndex;
                    _sampleBufferByte = _waveRam[byteIndex];
                    _cyclesSinceLastWaveByteFetch = 0;
                    _sampleLatch = (_sampleBufferByte >> 4) & 0x0F;
                }
                else
                {
                    _sampleLatch = _sampleBufferByte & 0x0F;
                }
            }
        }

        private int CurrentPeriodCycles => Math.Max(2, (2048 - _period11) * 2);

        public void ClockLength()
        {
            if (_lengthEnabled && _lengthCounter > 0)
            {
                _lengthCounter--;
                if (_lengthCounter == 0)
                {
                    _active = false;
                }
            }
        }

        public float GetAnalogSample()
        {
            if (!_active || !_dacEnabled)
            {
                return 0;
            }

            int s = _sampleLatch;
            s = _outputLevel switch
            {
                0 => 0,
                1 => s,
                2 => s >> 1,
                3 => s >> 2,
                _ => s,
            };

            return DigitalToAnalog(s);
        }
    }

    private sealed class NoiseChannel
    {
        private bool _active;
        private bool _dacEnabled;

        private int _lengthCounter;
        private bool _lengthEnabled;

        private int _initialVolume;
        private bool _envelopeIncrease;
        private int _envelopePeriod;

        private int _volume;
        private int _envelopeTimer;

        private int _clockShift;
        private bool _width7;
        private int _clockDivider;

        private int _lfsr;
        private int _freqTimerCycles;

        public bool Active => _active;

        public NoiseChannel()
        {
            Reset(poweredOn: true);
        }

        public void Reset(bool poweredOn)
        {
            _active = false;
            _dacEnabled = true;

            _lengthCounter = 0;
            _lengthEnabled = false;

            _initialVolume = 0;
            _envelopeIncrease = false;
            _envelopePeriod = 0;

            _volume = 0;
            _envelopeTimer = 0;

            _clockShift = 0;
            _width7 = false;
            _clockDivider = 0;

            _lfsr = 0x7FFF;
            _freqTimerCycles = 0;
        }

        public void OnPowerOff(bool resetLengthCounter)
        {
            _active = false;
            _dacEnabled = false;
            _lengthEnabled = false;
            _initialVolume = 0;
            _envelopeIncrease = false;
            _envelopePeriod = 0;
            _volume = 0;
            _envelopeTimer = 0;
            _clockShift = 0;
            _width7 = false;
            _clockDivider = 0;
            _lfsr = 0x7FFF;
            _freqTimerCycles = 0;
            if (resetLengthCounter)
            {
                _lengthCounter = 0;
            }
        }

        public void OnPowerOn(bool resetLengthCounter)
        {
            _active = false;
            _dacEnabled = false;
            _lengthEnabled = false;
            _initialVolume = 0;
            _envelopeIncrease = false;
            _envelopePeriod = 0;
            _volume = 0;
            _envelopeTimer = 0;
            _clockShift = 0;
            _width7 = false;
            _clockDivider = 0;
            _lfsr = 0x7FFF;
            _freqTimerCycles = 0;
            if (resetLengthCounter)
            {
                _lengthCounter = 0;
            }
        }

        public void RefreshDac()
        {
            _dacEnabled = (_initialVolume != 0) || _envelopeIncrease;
            if (!_dacEnabled)
            {
                _active = false;
            }
        }

        public void WriteNr41(byte value)
        {
            int lenLoad = value & 0x3F;
            _lengthCounter = 64 - lenLoad;
        }

        public void WriteNr42(byte value)
        {
            _initialVolume = (value >> 4) & 0x0F;
            _envelopeIncrease = (value & 0x08) != 0;
            _envelopePeriod = value & 0x07;
        }

        public void WriteNr43(byte value)
        {
            _clockShift = (value >> 4) & 0x0F;
            _width7 = (value & 0x08) != 0;
            _clockDivider = value & 0x07;
        }

        public void WriteNr44(byte value)
        {
            _lengthEnabled = (value & 0x40) != 0;
        }

        public void Trigger(bool extraLengthClockIfReloadingFromZero)
        {
            bool hadDac = _dacEnabled;

            _active = true;
            if (_lengthCounter == 0)
            {
                _lengthCounter = extraLengthClockIfReloadingFromZero ? 63 : 64;
            }

            _volume = _initialVolume;
            _envelopeTimer = (_envelopePeriod == 0) ? 0 : _envelopePeriod;

            _lfsr = 0x7FFF;
            _freqTimerCycles = Math.Max(8, ComputePeriodCycles());

            if (!hadDac)
            {
                _active = false;
            }
        }

        public void Step(int baseCycles)
        {
            if (!_active)
            {
                return;
            }

            _freqTimerCycles -= baseCycles;
            while (_freqTimerCycles <= 0)
            {
                _freqTimerCycles += Math.Max(8, ComputePeriodCycles());
                TickLfsr();
            }
        }

        private int ComputePeriodCycles()
        {
            // Pan Docs: LFSR is clocked at 262144 / (divider * 2^shift)
            // Base cycles per tick: BaseClockHz / freq
            // => (BaseClockHz) / (262144 / (divider*2^shift)) = 16 * divider * 2^shift
            // divider=0 treated as 0.5 => 8 * 2^shift
            int divisor = _clockDivider == 0 ? 1 : _clockDivider;
            int baseCyclesPerTick = _clockDivider == 0 ? 8 : 16 * divisor;
            if (_clockShift >= 14)
            {
                // Shift 14/15 stops clocking in practice.
                return int.MaxValue / 2;
            }
            return baseCyclesPerTick << _clockShift;
        }

        private void TickLfsr()
        {
            int bit0 = _lfsr & 1;
            int bit1 = (_lfsr >> 1) & 1;
            int xnor = (bit0 == bit1) ? 1 : 0;

            _lfsr >>= 1;
            _lfsr |= (xnor << 14);
            if (_width7)
            {
                // Copy to bit 6 (i.e. bit 7 of 15-bit view)
                _lfsr = (_lfsr & ~(1 << 6)) | (xnor << 6);
            }
        }

        public void ClockLength()
        {
            if (_lengthEnabled && _lengthCounter > 0)
            {
                _lengthCounter--;
                if (_lengthCounter == 0)
                {
                    _active = false;
                }
            }
        }

        public void ClockEnvelope()
        {
            if (_envelopePeriod == 0)
            {
                return;
            }

            if (_envelopeTimer > 0)
            {
                _envelopeTimer--;
            }

            if (_envelopeTimer == 0)
            {
                _envelopeTimer = _envelopePeriod;
                if (_envelopeIncrease)
                {
                    if (_volume < 15) _volume++;
                }
                else
                {
                    if (_volume > 0) _volume--;
                }
            }
        }

        public float GetAnalogSample()
        {
            if (!_active || !_dacEnabled)
            {
                return 0;
            }

            // If bit0 is 0 output volume, else 0 (matches typical emu convention; exact polarity varies).
            int digital = ((_lfsr & 1) == 0) ? _volume : 0;
            return DigitalToAnalog(digital);
        }
    }
}
