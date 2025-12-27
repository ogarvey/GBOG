using System;
using System.Collections.Generic;
using System.Linq;

namespace GBOG.CPU
{
    public class JoypadMacroExecutor
    {
        public enum JoypadButton
        {
            Right = 0,
            Left = 1,
            Up = 2,
            Down = 3,
            A = 4,
            B = 5,
            Select = 6,
            Start = 7
        }

        private struct MacroCommand
        {
            public bool[] Buttons;
            public int HoldFrames;
            public int GapFrames;
        }

        private readonly Queue<MacroCommand> _commandQueue = new();
        private MacroCommand? _currentCommand;
        private int _remainingFrames;
        private bool _isGap;

        public bool IsRunning => _currentCommand != null || _commandQueue.Count > 0;
        public int QueueCount => _commandQueue.Count;

        public void Clear()
        {
            _commandQueue.Clear();
            _currentCommand = null;
            _remainingFrames = 0;
            _isGap = false;
        }

        public bool ParseAndEnqueue(string macroText, out string error)
        {
            error = string.Empty;
            var lines = macroText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var newCommands = new List<MacroCommand>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    error = $"Line {i + 1}: Invalid format. Expected '[Buttons] [HoldFrames] [GapFrames]'";
                    return false;
                }

                var buttons = new bool[8];
                var buttonParts = parts[0].Split('+');
                foreach (var bp in buttonParts)
                {
                    switch (bp.ToUpperInvariant())
                    {
                        case "R": case "RIGHT": buttons[(int)JoypadButton.Right] = true; break;
                        case "L": case "LEFT": buttons[(int)JoypadButton.Left] = true; break;
                        case "U": case "UP": buttons[(int)JoypadButton.Up] = true; break;
                        case "D": case "DOWN": buttons[(int)JoypadButton.Down] = true; break;
                        case "A": buttons[(int)JoypadButton.A] = true; break;
                        case "B": buttons[(int)JoypadButton.B] = true; break;
                        case "S": case "SELECT": buttons[(int)JoypadButton.Select] = true; break;
                        case "T": case "START": buttons[(int)JoypadButton.Start] = true; break;
                        default:
                            error = $"Line {i + 1}: Unknown button '{bp}'";
                            return false;
                    }
                }

                if (!int.TryParse(parts[1], out int hold) || hold < 1)
                {
                    error = $"Line {i + 1}: Invalid hold frames '{parts[1]}'";
                    return false;
                }

                int gap = 0;
                if (parts.Length >= 3 && (!int.TryParse(parts[2], out gap) || gap < 0))
                {
                    error = $"Line {i + 1}: Invalid gap frames '{parts[2]}'";
                    return false;
                }

                newCommands.Add(new MacroCommand { Buttons = buttons, HoldFrames = hold, GapFrames = gap });
            }

            foreach (var cmd in newCommands)
            {
                _commandQueue.Enqueue(cmd);
            }

            return true;
        }

        public void Update(bool[] targetKeys)
        {
            if (_currentCommand == null)
            {
                if (_commandQueue.Count == 0) return;
                _currentCommand = _commandQueue.Dequeue();
                _remainingFrames = _currentCommand.Value.HoldFrames;
                _isGap = false;
            }

            if (!_isGap)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (_currentCommand.Value.Buttons[i])
                    {
                        targetKeys[i] = true;
                    }
                }

                _remainingFrames--;
                if (_remainingFrames <= 0)
                {
                    if (_currentCommand.Value.GapFrames > 0)
                    {
                        _isGap = true;
                        _remainingFrames = _currentCommand.Value.GapFrames;
                    }
                    else
                    {
                        _currentCommand = null;
                    }
                }
            }
            else
            {
                _remainingFrames--;
                if (_remainingFrames <= 0)
                {
                    _currentCommand = null;
                    _isGap = false;
                }
            }
        }
    }
}
