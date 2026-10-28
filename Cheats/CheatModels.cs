using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GBOG.Cheats;

public enum CheatType
{
    GameGenie,
    GameShark
}

public sealed class CheatLine
{
    public string Code { get; set; } = string.Empty;
    public CheatType Type { get; set; } = CheatType.GameGenie;
    public ushort Address { get; set; }
    public byte Value { get; set; }
    public bool HasCompare { get; set; }
    public byte CompareValue { get; set; }
    public byte Bank { get; set; } = 1;

    [JsonIgnore]
    public bool IsValid { get; set; } = true;

    [JsonIgnore]
    public string? ErrorMessage { get; set; }

    [JsonIgnore]
    public string? WarningMessage { get; set; }

    public CheatLine Clone()
    {
        return new CheatLine
        {
            Code = Code,
            Type = Type,
            Address = Address,
            Value = Value,
            HasCompare = HasCompare,
            CompareValue = CompareValue,
            Bank = Bank,
            IsValid = IsValid,
            ErrorMessage = ErrorMessage,
            WarningMessage = WarningMessage
        };
    }
}

public sealed class CheatEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// One or more sub-code lines that comprise this cheat (e.g. 16-bit RAM patches or chained Game Genie codes).
    /// </summary>
    public List<CheatLine> Lines { get; set; } = new();

    // Legacy and serialization compatibility properties:
    public string Code
    {
        get => Lines.Count > 0 ? string.Join(Environment.NewLine, Lines.Select(l => l.Code)) : _code;
        set => _code = value;
    }
    private string _code = string.Empty;

    public CheatType Type
    {
        get => Lines.Count > 0 ? Lines[0].Type : _type;
        set => _type = value;
    }
    private CheatType _type = CheatType.GameGenie;

    public ushort Address
    {
        get => Lines.Count > 0 ? Lines[0].Address : _address;
        set => _address = value;
    }
    private ushort _address;

    public byte Value
    {
        get => Lines.Count > 0 ? Lines[0].Value : _value;
        set => _value = value;
    }
    private byte _value;

    public bool HasCompare
    {
        get => Lines.Count > 0 ? Lines[0].HasCompare : _hasCompare;
        set => _hasCompare = value;
    }
    private bool _hasCompare;

    public byte CompareValue
    {
        get => Lines.Count > 0 ? Lines[0].CompareValue : _compareValue;
        set => _compareValue = value;
    }
    private byte _compareValue;

    public byte Bank
    {
        get => Lines.Count > 0 ? Lines[0].Bank : _bank;
        set => _bank = value;
    }
    private byte _bank = 1;

    [JsonIgnore]
    public bool IsValid
    {
        get => Lines.Count > 0 ? Lines.All(l => l.IsValid) : _isValid;
        set => _isValid = value;
    }
    private bool _isValid = true;

    [JsonIgnore]
    public string? ErrorMessage { get; set; }

    [JsonIgnore]
    public string? WarningMessage { get; set; }

    public void EnsureLinesFromLegacy()
    {
        if (Lines.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(_code))
            {
                var rawLines = _code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var rl in rawLines)
                {
                    if (CheatDecoder.TryDecodeSingleLine(rl, out var cl, out _))
                    {
                        Lines.Add(cl);
                    }
                }
            }

            if (Lines.Count == 0 && (_address != 0 || !string.IsNullOrWhiteSpace(_code)))
            {
                Lines.Add(new CheatLine
                {
                    Code = _code,
                    Type = _type,
                    Address = _address,
                    Value = _value,
                    HasCompare = _hasCompare,
                    CompareValue = _compareValue,
                    Bank = _bank,
                    IsValid = _isValid
                });
            }
        }
    }

    public CheatEntry Clone()
    {
        var clone = new CheatEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            Description = Description,
            Enabled = Enabled,
            ErrorMessage = ErrorMessage,
            WarningMessage = WarningMessage,
            _code = _code,
            _type = _type,
            _address = _address,
            _value = _value,
            _hasCompare = _hasCompare,
            _compareValue = _compareValue,
            _bank = _bank,
            _isValid = _isValid
        };
        foreach (var line in Lines)
        {
            clone.Lines.Add(line.Clone());
        }
        return clone;
    }
}

public readonly struct GameGeniePatch
{
    public readonly ushort Address;
    public readonly byte NewValue;
    public readonly bool HasCompare;
    public readonly byte CompareValue;

    public GameGeniePatch(ushort address, byte newValue, bool hasCompare, byte compareValue)
    {
        Address = address;
        NewValue = newValue;
        HasCompare = hasCompare;
        CompareValue = compareValue;
    }
}

public readonly struct GameSharkPatch
{
    public readonly ushort Address;
    public readonly byte NewValue;
    public readonly byte Bank;

    public GameSharkPatch(ushort address, byte newValue, byte bank)
    {
        Address = address;
        NewValue = newValue;
        Bank = bank;
    }
}
