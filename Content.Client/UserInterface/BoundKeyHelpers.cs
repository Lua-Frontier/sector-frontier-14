using System.Diagnostics.CodeAnalysis;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface;

public static class BoundKeyHelper
{
    public static string ShortKeyName(BoundKeyFunction keyFunction)
    {
        // need to use shortened key names so they fit in the buttons.
        return TryGetShortKeyName(keyFunction, out var name) ? Loc.GetString(name) : " ";
    }

    public static bool IsBound(BoundKeyFunction keyFunction)
    {
        return TryGetShortKeyName(keyFunction, out _);
    }

    private static string? DefaultShortKeyName(BoundKeyFunction keyFunction)
    {
        var name = FormattedMessage.EscapeText(IoCManager.Resolve<IInputManager>().GetKeyFunctionButtonString(keyFunction));
        return name.Length > 3 ? null : name;
    }

    public static bool TryGetShortKeyName(BoundKeyFunction keyFunction, [NotNullWhen(true)] out string? name)
    {
        if (IoCManager.Resolve<IInputManager>().TryGetKeyBinding(keyFunction, out var binding))
        {
            // can't possibly fit a modifier key in the top button, so omit it
            var key = binding.BaseKey;
            if (binding.Mod1 != Keyboard.Key.Unknown || binding.Mod2 != Keyboard.Key.Unknown ||
                binding.Mod3 != Keyboard.Key.Unknown)
            {
                name = null;
                return false;
            }

            name = null;
            name = key switch
            {
                // Lua start Force latin letters regardless of OS keyboard layout
                Keyboard.Key.A => "A",
                Keyboard.Key.B => "B",
                Keyboard.Key.C => "C",
                Keyboard.Key.D => "D",
                Keyboard.Key.E => "E",
                Keyboard.Key.F => "F",
                Keyboard.Key.G => "G",
                Keyboard.Key.H => "H",
                Keyboard.Key.I => "I",
                Keyboard.Key.J => "J",
                Keyboard.Key.K => "K",
                Keyboard.Key.L => "L",
                Keyboard.Key.M => "M",
                Keyboard.Key.N => "N",
                Keyboard.Key.O => "O",
                Keyboard.Key.P => "P",
                Keyboard.Key.Q => "Q",
                Keyboard.Key.R => "R",
                Keyboard.Key.S => "S",
                Keyboard.Key.T => "T",
                Keyboard.Key.U => "U",
                Keyboard.Key.V => "V",
                Keyboard.Key.W => "W",
                Keyboard.Key.X => "X",
                Keyboard.Key.Y => "Y",
                Keyboard.Key.Z => "Z", // Lua end
                Keyboard.Key.Apostrophe => "'",
                Keyboard.Key.Comma => ",",
                Keyboard.Key.Delete => "Del",
                Keyboard.Key.Down => "Dwn",
                Keyboard.Key.Escape => "Esc",
                Keyboard.Key.Equal => "=",
                Keyboard.Key.Home => "Hom",
                Keyboard.Key.Insert => "Ins",
                Keyboard.Key.Left => "Lft",
                Keyboard.Key.Menu => "Men",
                Keyboard.Key.Minus => "-",
                Keyboard.Key.Num0 => "0",
                Keyboard.Key.Num1 => "1",
                Keyboard.Key.Num2 => "2",
                Keyboard.Key.Num3 => "3",
                Keyboard.Key.Num4 => "4",
                Keyboard.Key.Num5 => "5",
                Keyboard.Key.Num6 => "6",
                Keyboard.Key.Num7 => "7",
                Keyboard.Key.Num8 => "8",
                Keyboard.Key.Num9 => "9",
                Keyboard.Key.Pause => "||",
                Keyboard.Key.Period => ".",
                Keyboard.Key.Return => "Ret",
                Keyboard.Key.Right => "Rgt",
                Keyboard.Key.Slash => "/",
                Keyboard.Key.Space => "Spc",
                Keyboard.Key.Tab => "Tab",
                Keyboard.Key.Tilde => "~",
                Keyboard.Key.BackSlash => "\\",
                Keyboard.Key.BackSpace => "Bks",
                Keyboard.Key.LBracket => "[",
                Keyboard.Key.MouseButton4 => "M4",
                Keyboard.Key.MouseButton5 => "M5",
                Keyboard.Key.MouseButton6 => "M6",
                Keyboard.Key.MouseButton7 => "M7",
                Keyboard.Key.MouseButton8 => "M8",
                Keyboard.Key.MouseButton9 => "M9",
                Keyboard.Key.MouseLeft => "ML",
                Keyboard.Key.MouseMiddle => "MM",
                Keyboard.Key.MouseRight => "MR",
                Keyboard.Key.NumpadDecimal => "N.",
                Keyboard.Key.NumpadDivide => "N/",
                Keyboard.Key.NumpadEnter => "Ent",
                Keyboard.Key.NumpadMultiply => "*",
                Keyboard.Key.NumpadNum0 => "0",
                Keyboard.Key.NumpadNum1 => "1",
                Keyboard.Key.NumpadNum2 => "2",
                Keyboard.Key.NumpadNum3 => "3",
                Keyboard.Key.NumpadNum4 => "4",
                Keyboard.Key.NumpadNum5 => "5",
                Keyboard.Key.NumpadNum6 => "6",
                Keyboard.Key.NumpadNum7 => "7",
                Keyboard.Key.NumpadNum8 => "8",
                Keyboard.Key.NumpadNum9 => "9",
                Keyboard.Key.NumpadSubtract => "N-",
                Keyboard.Key.PageDown => "PgD",
                Keyboard.Key.PageUp => "PgU",
                Keyboard.Key.RBracket => "]",
                Keyboard.Key.SemiColon => ";",
                _ => DefaultShortKeyName(keyFunction)
            };
            return name != null;
        }

        name = null;
        return false;
    }
}
