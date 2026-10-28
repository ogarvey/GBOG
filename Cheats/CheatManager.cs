using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GBOG.Memory;

namespace GBOG.Cheats;

public sealed class CheatManager
{
    private readonly object _syncLock = new();
    private readonly List<CheatEntry> _cheats = new();

    private volatile GameGeniePatch[] _activeGameGenie = Array.Empty<GameGeniePatch>();
    private volatile GameSharkPatch[] _activeGameShark = Array.Empty<GameSharkPatch>();
    private volatile bool _hasActiveGameGenie = false;
    private volatile bool _hasActiveGameShark = false;
    private volatile bool _masterEnable = true;

    public bool MasterEnable
    {
        get => _masterEnable;
        set
        {
            _masterEnable = value;
            RebuildActivePatches();
        }
    }

    public bool HasActiveGameGenie => _masterEnable && _hasActiveGameGenie;
    public bool HasActiveGameShark => _masterEnable && _hasActiveGameShark;

    public int TotalCount
    {
        get
        {
            lock (_syncLock) return _cheats.Count;
        }
    }

    public int ActiveCount
    {
        get
        {
            if (!_masterEnable) return 0;
            return _activeGameGenie.Length + _activeGameShark.Length;
        }
    }

    public List<CheatEntry> GetCheatsSnapshot()
    {
        lock (_syncLock)
        {
            var list = new List<CheatEntry>(_cheats.Count);
            foreach (var c in _cheats)
            {
                list.Add(c.Clone());
            }
            return list;
        }
    }

    public void AddCheat(CheatEntry cheat)
    {
        lock (_syncLock)
        {
            _cheats.Add(cheat);
            RebuildActivePatches();
        }
    }

    public bool RemoveCheat(string id)
    {
        lock (_syncLock)
        {
            int idx = _cheats.FindIndex(c => c.Id == id);
            if (idx >= 0)
            {
                _cheats.RemoveAt(idx);
                RebuildActivePatches();
                return true;
            }
            return false;
        }
    }

    public void SetCheatEnabled(string id, bool enabled)
    {
        lock (_syncLock)
        {
            var cheat = _cheats.Find(c => c.Id == id);
            if (cheat != null && cheat.Enabled != enabled)
            {
                cheat.Enabled = enabled;
                RebuildActivePatches();
            }
        }
    }

    public void SetAllEnabled(bool enabled)
    {
        lock (_syncLock)
        {
            foreach (var c in _cheats)
            {
                c.Enabled = enabled;
            }
            RebuildActivePatches();
        }
    }

    public void ClearCheats()
    {
        lock (_syncLock)
        {
            _cheats.Clear();
            RebuildActivePatches();
        }
    }

    public void LoadCheats(IEnumerable<CheatEntry> cheats)
    {
        lock (_syncLock)
        {
            _cheats.Clear();
            foreach (var c in cheats)
            {
                _cheats.Add(c);
            }
            RebuildActivePatches();
        }
    }

    private void RebuildActivePatches()
    {
        var ggList = new List<GameGeniePatch>();
        var gsList = new List<GameSharkPatch>();

        if (_masterEnable)
        {
            foreach (var c in _cheats)
            {
                if (!c.Enabled || !c.IsValid) continue;

                c.EnsureLinesFromLegacy();

                foreach (var line in c.Lines)
                {
                    if (!line.IsValid) continue;

                    if (line.Type == CheatType.GameGenie)
                    {
                        ggList.Add(new GameGeniePatch(line.Address, line.Value, line.HasCompare, line.CompareValue));
                    }
                    else if (line.Type == CheatType.GameShark)
                    {
                        gsList.Add(new GameSharkPatch(line.Address, line.Value, line.Bank));
                    }
                }
            }
        }

        var ggArray = ggList.ToArray();
        var gsArray = gsList.ToArray();

        _activeGameGenie = ggArray;
        _activeGameShark = gsArray;
        _hasActiveGameGenie = ggArray.Length > 0;
        _hasActiveGameShark = gsArray.Length > 0;
    }

    /// <summary>
    /// Intercepts a ROM read at the given address, replacing the data if an active Game Genie code matches.
    /// Fast lock-free execution for emulation loop.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ApplyGameGenie(ushort address, byte originalRomByte)
    {
        var patches = _activeGameGenie;
        for (int i = 0; i < patches.Length; i++)
        {
            ref readonly var p = ref patches[i];
            if (p.Address == address)
            {
                if (!p.HasCompare || p.CompareValue == originalRomByte)
                {
                    return p.NewValue;
                }
            }
        }
        return originalRomByte;
    }

    /// <summary>
    /// Intercepts a RAM read at the given address, replacing the data if an active GameShark code matches.
    /// Fast lock-free execution for emulation loop.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ApplyGameSharkRead(ushort address, byte originalRamByte)
    {
        var patches = _activeGameShark;
        for (int i = 0; i < patches.Length; i++)
        {
            ref readonly var p = ref patches[i];
            if (p.Address == address)
            {
                return p.NewValue;
            }
        }
        return originalRamByte;
    }

    /// <summary>
    /// Periodically writes GameShark values to memory at frame / VBlank boundaries, mirroring GameShark hardware.
    /// </summary>
    public void ApplyGameSharkRamFrame(GBMemory memory)
    {
        if (!_masterEnable || !_hasActiveGameShark) return;

        var patches = _activeGameShark;
        for (int i = 0; i < patches.Length; i++)
        {
            ref readonly var p = ref patches[i];
            memory.WriteRamCheat(p.Address, p.NewValue, p.Bank);
        }
    }
}
