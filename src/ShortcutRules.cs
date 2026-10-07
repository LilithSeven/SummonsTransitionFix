using System;

namespace SummonsTransitionFix
{
    public static class ShortcutRules
    {
        private static readonly string[] KeyNamesThatCannotBeAShortcut =
        {
            "None",
            "Escape",
            "LeftControl",
            "RightControl",
            "LeftShift",
            "RightShift",
            "LeftAlt",
            "RightAlt",
            "AltGr",
            "LeftCommand",
            "RightCommand",
            "LeftApple",
            "RightApple",
            "LeftWindows",
            "RightWindows",
            "CapsLock",
            "Numlock",
            "ScrollLock",
        };

        public static bool ModifiersMatch(bool needsCtrl, bool needsShift, bool needsAlt, bool ctrlDown, bool shiftDown, bool altDown)
        {
            return needsCtrl == ctrlDown && needsShift == shiftDown && needsAlt == altDown;
        }

        public static bool CanBeAShortcut(string? keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return false;
            if (keyName!.StartsWith("Mouse", StringComparison.Ordinal)) return false;
            if (keyName.StartsWith("Joystick", StringComparison.Ordinal)) return false;

            return Array.IndexOf(KeyNamesThatCannotBeAShortcut, keyName) < 0;
        }

        public static string Describe(string? keyName, bool needsCtrl, bool needsShift, bool needsAlt)
        {
            return (needsCtrl ? "Ctrl+" : string.Empty)
                + (needsShift ? "Shift+" : string.Empty)
                + (needsAlt ? "Alt+" : string.Empty)
                + (string.IsNullOrEmpty(keyName) ? "None" : keyName);
        }
    }
}
