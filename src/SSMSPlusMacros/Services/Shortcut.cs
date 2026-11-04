namespace SSMSPlusMacros.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;

    /// <summary>Visual Studio key notation ("Ctrl+Shift+F6") for single key strokes.</summary>
    public static class Shortcut
    {
        private static readonly Dictionary<Key, string> NamedKeys = new Dictionary<Key, string>
        {
            { Key.Insert, "Ins" }, { Key.Delete, "Del" }, { Key.Home, "Home" }, { Key.End, "End" },
            { Key.PageUp, "PgUp" }, { Key.PageDown, "PgDn" },
            { Key.Up, "Up Arrow" }, { Key.Down, "Down Arrow" }, { Key.Left, "Left Arrow" }, { Key.Right, "Right Arrow" },
            { Key.Space, "Space" }, { Key.Enter, "Enter" }, { Key.Back, "Bkspce" }, { Key.Tab, "Tab" },
            { Key.OemComma, "," }, { Key.OemPeriod, "." }, { Key.OemMinus, "-" }, { Key.OemPlus, "=" },
            { Key.OemQuestion, "/" }, { Key.OemSemicolon, ";" }, { Key.OemQuotes, "'" },
            { Key.OemOpenBrackets, "[" }, { Key.OemCloseBrackets, "]" }, { Key.OemPipe, "\\" }, { Key.OemTilde, "`" },
        };

        /// <summary>The notation of the key stroke, or null for keys that cannot be used (modifiers only, Esc, ...).</summary>
        public static string Format(Key key, ModifierKeys modifiers)
        {
            string keyName = GetKeyName(key);
            if (keyName == null)
                return null;

            var parts = new List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            parts.Add(keyName);
            return string.Join("+", parts);
        }

        /// <summary>Keys that type text (letters, digits, punctuation, space...) need Ctrl or Alt, else the editor could not type them anymore.</summary>
        public static bool NeedsCtrlOrAlt(string shortcut)
        {
            var strokeParts = Split(shortcut);
            if (strokeParts.Contains("CTRL") || strokeParts.Contains("ALT"))
                return false;

            var keyName = strokeParts.LastOrDefault() ?? string.Empty;
            bool isFunctionKey = keyName.Length > 1 && keyName[0] == 'F' && int.TryParse(keyName.Substring(1), out _);
            bool isNavigationKey = new[] { "INS", "DEL", "HOME", "END", "PGUP", "PGDN" }.Contains(keyName);
            return !isFunctionKey && !isNavigationKey;
        }

        /// <summary>Same key stroke(s), whatever the modifiers order or case.</summary>
        public static bool AreEqual(string keysA, string keysB)
        {
            return string.Equals(Normalize(keysA), Normalize(keysB), StringComparison.Ordinal);
        }

        /// <summary>First stroke of a chord ("Ctrl+K, Ctrl+C" gives "Ctrl+K").</summary>
        public static string FirstStroke(string keys)
        {
            var i = keys.IndexOf(", ", StringComparison.Ordinal);
            return i < 0 ? keys : keys.Substring(0, i);
        }

        public static bool IsChord(string keys)
        {
            return keys.IndexOf(", ", StringComparison.Ordinal) >= 0;
        }

        private static string Normalize(string keys)
        {
            return string.Join(", ", (keys ?? string.Empty)
                .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(stroke =>
                {
                    var parts = Split(stroke);
                    var modifiers = parts.Take(parts.Count - 1).OrderBy(p => p);
                    return string.Join("+", modifiers.Concat(parts.Skip(parts.Count - 1)));
                }));
        }

        private static List<string> Split(string stroke)
        {
            // "Ctrl++" is Ctrl and the + key
            var parts = new List<string>();
            var remaining = (stroke ?? string.Empty).Trim();
            while (true)
            {
                var i = remaining.IndexOf('+', 1);
                if (i < 0 || i == remaining.Length - 1)
                {
                    parts.Add(remaining.ToUpperInvariant());
                    return parts;
                }
                parts.Add(remaining.Substring(0, i).ToUpperInvariant());
                remaining = remaining.Substring(i + 1);
            }
        }

        private static string GetKeyName(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
                return key.ToString();
            if (key >= Key.D0 && key <= Key.D9)
                return ((char)('0' + (key - Key.D0))).ToString();
            if (key >= Key.F1 && key <= Key.F24)
                return "F" + (key - Key.F1 + 1);
            return NamedKeys.TryGetValue(key, out var name) ? name : null;
        }
    }
}
