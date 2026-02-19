using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace DevCLT.WindowsApp.Services;

/// <summary>
/// Registers system-wide hotkeys via Win32 RegisterHotKey / UnregisterHotKey.
/// Supports fully customizable key combinations (e.g. "Ctrl+Alt+I", "Shift+F5", "Ctrl+Shift+Alt+K").
/// </summary>
public class HotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private const int HOTKEY_JORNADA = 9001;
    private const int HOTKEY_PAUSA = 9002;
    private const int HOTKEY_OVERTIME = 9003;
    private const int WM_HOTKEY = 0x0312;

    private IntPtr _windowHandle;
    private HwndSource? _source;
    private bool _registered;
    private bool _enabled = true;

    public event Action? JornadaHotkeyPressed;
    public event Action? PausaHotkeyPressed;
    public event Action? OvertimeHotkeyPressed;

    // Current key bindings (display strings)
    public string JornadaKey { get; private set; } = "Ctrl+Alt+I";
    public string PausaKey { get; private set; } = "Ctrl+Alt+P";
    public string OvertimeKey { get; private set; } = "Ctrl+Alt+X";
    public bool IsEnabled => _enabled;

    /// <summary>
    /// Registers all three global hotkeys for the given window handle.
    /// </summary>
    public void Register(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle);
        _source?.AddHook(HwndHook);

        if (_enabled)
            RegisterAllKeys();
    }

    /// <summary>
    /// Re-register hotkeys with new key bindings.
    /// </summary>
    public void UpdateConfiguration(bool enabled, string jornadaKey, string pausaKey, string overtimeKey)
    {
        UnregisterAllKeys();

        _enabled = enabled;
        JornadaKey = jornadaKey;
        PausaKey = pausaKey;
        OvertimeKey = overtimeKey;

        if (_enabled && _windowHandle != IntPtr.Zero)
            RegisterAllKeys();
    }

    public void Unregister()
    {
        UnregisterAllKeys();
        _source?.RemoveHook(HwndHook);
        _source = null;
    }

    private void RegisterAllKeys()
    {
        if (_windowHandle == IntPtr.Zero) return;

        RegisterSingleKey(HOTKEY_JORNADA, JornadaKey);
        RegisterSingleKey(HOTKEY_PAUSA, PausaKey);
        RegisterSingleKey(HOTKEY_OVERTIME, OvertimeKey);
        _registered = true;
    }

    private void UnregisterAllKeys()
    {
        if (!_registered || _windowHandle == IntPtr.Zero) return;

        UnregisterHotKey(_windowHandle, HOTKEY_JORNADA);
        UnregisterHotKey(_windowHandle, HOTKEY_PAUSA);
        UnregisterHotKey(_windowHandle, HOTKEY_OVERTIME);
        _registered = false;
    }

    private void RegisterSingleKey(int id, string keyCombo)
    {
        if (!TryParseHotkey(keyCombo, out uint modifiers, out uint vk))
            return;

        RegisterHotKey(_windowHandle, id, modifiers | MOD_NOREPEAT, vk);
    }

    /// <summary>
    /// Parses a hotkey string like "Ctrl+Alt+I" or "Shift+F5" into modifiers and virtual key code.
    /// Supports any combination of Ctrl, Alt, Shift, Win modifiers + any single key.
    /// </summary>
    public static bool TryParseHotkey(string keyCombo, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(keyCombo))
            return false;

        var parts = keyCombo.Split('+');
        foreach (var part in parts)
        {
            var p = part.Trim();
            switch (p.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= MOD_CONTROL;
                    break;
                case "ALT":
                    modifiers |= MOD_ALT;
                    break;
                case "SHIFT":
                    modifiers |= MOD_SHIFT;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    // Try to parse the key via WPF Key enum
                    if (Enum.TryParse<Key>(p, true, out var wpfKey))
                    {
                        vk = (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
                    }
                    else if (p.Length == 1 && char.IsLetterOrDigit(p[0]))
                    {
                        // Single char fallback
                        vk = (uint)char.ToUpperInvariant(p[0]);
                    }
                    else
                    {
                        return false;
                    }
                    break;
            }
        }

        return vk != 0;
    }

    /// <summary>
    /// Converts a WPF KeyEventArgs into a display string like "Ctrl+Alt+I".
    /// Used by the hotkey recorder in settings.
    /// </summary>
    public static string KeyEventToString(KeyEventArgs e)
    {
        var parts = new System.Collections.Generic.List<string>();

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");

        // Get the actual key (not the modifier)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Skip if only modifiers pressed
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
            return string.Join("+", parts) + "+...";

        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    /// <summary>
    /// Checks if the key in KeyEventArgs is a real key (not just a modifier).
    /// </summary>
    public static bool IsCompleteHotkey(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key != Key.LeftCtrl && key != Key.RightCtrl &&
               key != Key.LeftAlt && key != Key.RightAlt &&
               key != Key.LeftShift && key != Key.RightShift &&
               key != Key.LWin && key != Key.RWin;
    }

    /// <summary>
    /// Splits a hotkey string into individual key tokens for display. E.g. "Ctrl+Alt+I" → ["Ctrl", "Alt", "I"]
    /// </summary>
    public static string[] SplitForDisplay(string keyCombo)
    {
        if (string.IsNullOrWhiteSpace(keyCombo)) return Array.Empty<string>();
        return keyCombo.Split('+').Select(k => k.Trim()).Where(k => k.Length > 0).ToArray();
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _enabled)
        {
            int id = wParam.ToInt32();
            switch (id)
            {
                case HOTKEY_JORNADA:
                    JornadaHotkeyPressed?.Invoke();
                    handled = true;
                    break;
                case HOTKEY_PAUSA:
                    PausaHotkeyPressed?.Invoke();
                    handled = true;
                    break;
                case HOTKEY_OVERTIME:
                    OvertimeHotkeyPressed?.Invoke();
                    handled = true;
                    break;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
    }
}
