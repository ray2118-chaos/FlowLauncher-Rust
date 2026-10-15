using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.Program.Programs;
using NHotkey;
using NHotkey.Wpf;

namespace Flow.Launcher.Plugin.Program
{
    public enum DoubleTapModifier
    {
        None,
        Ctrl,
        Alt,
        Shift,
        Win
    }

    public static class AppHotkeyManager
    {
        private const string ClassName = nameof(AppHotkeyManager);
        private static readonly HashSet<string> RegisteredKeys = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<DoubleTapModifier, List<AppCustomShortcut>> DoubleTapShortcuts = new();
        private static readonly object SyncLock = new();
        private static Func<IReadOnlyList<IProgram>> _getProgramsSnapshot;
        private static bool _globalKeyboardHookRegistered;

        // Double-tap tracking state
        private static DoubleTapModifier _lastModifierReleased = DoubleTapModifier.None;
        private static DateTime _lastModifierReleaseTime = DateTime.MinValue;
        private static DoubleTapModifier _activeModifierDown = DoubleTapModifier.None;
        private static bool _otherKeyPressed;

        #region Win32 Window Management APIs

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(nint hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(nint hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(nint hWnd);

        [DllImport("user32.dll")]
        private static extern nint GetWindow(nint hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(nint hWnd);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(nint hWnd);

        private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

        #endregion

        private static void RunOnDispatcher(Action action)
        {
            var app = Application.Current;
            if (app?.Dispatcher == null) return;
            if (app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished) return;

            if (app.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                app.Dispatcher.BeginInvoke(action);
            }
        }

        public static DoubleTapModifier ParseDoubleTapModifier(string hotkey)
        {
            if (string.IsNullOrWhiteSpace(hotkey)) return DoubleTapModifier.None;
            string clean = hotkey.Replace(" ", "").ToLowerInvariant();
            return clean switch
            {
                "ctrl+ctrl" or "doublectrl" => DoubleTapModifier.Ctrl,
                "alt+alt" or "doublealt" => DoubleTapModifier.Alt,
                "shift+shift" or "doubleshift" => DoubleTapModifier.Shift,
                "win+win" or "doublewin" => DoubleTapModifier.Win,
                _ => DoubleTapModifier.None
            };
        }

        public static void SyncAllHotkeys(Settings settings, Func<IReadOnlyList<IProgram>> getProgramsSnapshot)
        {
            if (settings?.CustomShortcuts == null) return;
            _getProgramsSnapshot = getProgramsSnapshot;

            EnsureGlobalKeyboardHook();

            RunOnDispatcher(() =>
            {
                lock (SyncLock)
                {
                    // Clear existing NHotkey registrations
                    foreach (var key in RegisteredKeys.ToList())
                    {
                        try
                        {
                            HotkeyManager.Current.Remove(key);
                        }
                        catch
                        {
                            // Ignored during cleanup
                        }
                    }
                    RegisteredKeys.Clear();
                    DoubleTapShortcuts.Clear();

                    // Register all enabled shortcuts with valid hotkeys
                    foreach (var shortcut in settings.CustomShortcuts)
                    {
                        if (!shortcut.Enabled || string.IsNullOrWhiteSpace(shortcut.Hotkey))
                            continue;

                        RegisterShortcutInternal(shortcut, getProgramsSnapshot);
                    }
                }
            });
        }

        private static void EnsureGlobalKeyboardHook()
        {
            if (_globalKeyboardHookRegistered) return;
            try
            {
                Main.Context?.API?.RegisterGlobalKeyboardCallback(OnGlobalKeyboardEvent);
                _globalKeyboardHookRegistered = true;
            }
            catch (Exception ex)
            {
                Main.Context?.API?.LogException(ClassName, "Failed to register global keyboard callback", ex);
            }
        }

        private static bool OnGlobalKeyboardEvent(int keyEvent, int vkCode, SpecialKeyState state)
        {
            const int VK_SHIFT = 0x10;
            const int VK_CONTROL = 0x11;
            const int VK_MENU = 0x12; // Alt
            const int VK_LWIN = 0x5B;
            const int VK_RWIN = 0x5C;
            const int VK_LSHIFT = 0xA0;
            const int VK_RSHIFT = 0xA1;
            const int VK_LCONTROL = 0xA2;
            const int VK_RCONTROL = 0xA3;
            const int VK_LMENU = 0xA4;
            const int VK_RMENU = 0xA5;

            var currentMod = DoubleTapModifier.None;
            if (vkCode is VK_CONTROL or VK_LCONTROL or VK_RCONTROL) currentMod = DoubleTapModifier.Ctrl;
            else if (vkCode is VK_MENU or VK_LMENU or VK_RMENU) currentMod = DoubleTapModifier.Alt;
            else if (vkCode is VK_SHIFT or VK_LSHIFT or VK_RSHIFT) currentMod = DoubleTapModifier.Shift;
            else if (vkCode is VK_LWIN or VK_RWIN) currentMod = DoubleTapModifier.Win;

            bool isKeyDown = keyEvent == (int)KeyEvent.WM_KEYDOWN || keyEvent == (int)KeyEvent.WM_SYSKEYDOWN;
            bool isKeyUp = keyEvent == (int)KeyEvent.WM_KEYUP || keyEvent == (int)KeyEvent.WM_SYSKEYUP;

            if (isKeyDown)
            {
                if (currentMod != DoubleTapModifier.None)
                {
                    _activeModifierDown = currentMod;
                }
                else
                {
                    _otherKeyPressed = true;
                    _lastModifierReleased = DoubleTapModifier.None;
                }
            }
            else if (isKeyUp)
            {
                if (currentMod != DoubleTapModifier.None)
                {
                    if (!_otherKeyPressed && _activeModifierDown == currentMod)
                    {
                        var now = DateTime.UtcNow;
                        if (_lastModifierReleased == currentMod && (now - _lastModifierReleaseTime).TotalMilliseconds <= 450)
                        {
                            // Double tap triggered!
                            _lastModifierReleased = DoubleTapModifier.None;
                            TriggerDoubleTapShortcut(currentMod);
                        }
                        else
                        {
                            _lastModifierReleased = currentMod;
                            _lastModifierReleaseTime = now;
                        }
                    }

                    _activeModifierDown = DoubleTapModifier.None;
                    _otherKeyPressed = false;
                }
            }

            return true;
        }

        private static void TriggerDoubleTapShortcut(DoubleTapModifier mod)
        {
            lock (SyncLock)
            {
                if (DoubleTapShortcuts.TryGetValue(mod, out var list))
                {
                    var shortcut = list.FirstOrDefault(s => s.Enabled);
                    if (shortcut != null)
                    {
                        var programs = _getProgramsSnapshot?.Invoke();
                        var program = programs?.FirstOrDefault(p => string.Equals(p.UniqueIdentifier, shortcut.UniqueIdentifier, StringComparison.OrdinalIgnoreCase));
                        if (program != null)
                        {
                            _ = Task.Run(() => ActivateOrLaunch(program));
                        }
                    }
                }
            }
        }

        private static bool RegisterShortcutInternal(AppCustomShortcut shortcut, Func<IReadOnlyList<IProgram>> getProgramsSnapshot)
        {
            _getProgramsSnapshot = getProgramsSnapshot;

            // Check if this is a double-tap shortcut (e.g. Ctrl + Ctrl)
            var doubleMod = ParseDoubleTapModifier(shortcut.Hotkey);
            if (doubleMod != DoubleTapModifier.None)
            {
                lock (SyncLock)
                {
                    if (!DoubleTapShortcuts.TryGetValue(doubleMod, out var list))
                    {
                        list = new List<AppCustomShortcut>();
                        DoubleTapShortcuts[doubleMod] = list;
                    }
                    list.RemoveAll(s => string.Equals(s.UniqueIdentifier, shortcut.UniqueIdentifier, StringComparison.OrdinalIgnoreCase));
                    list.Add(shortcut);
                }
                return true;
            }

            // Standard combination hotkey (e.g. Win + B)
            string hotkeyName = "Flow_App_" + shortcut.UniqueIdentifier;
            try
            {
                var model = new HotkeyModel(shortcut.Hotkey);
                if (!model.Validate(true))
                {
                    Main.Context?.API?.LogWarn(ClassName, $"Invalid hotkey '{shortcut.Hotkey}' for app '{shortcut.Name}'");
                    return false;
                }

                HotkeyManager.Current.AddOrReplace(hotkeyName, model.CharKey, model.ModifierKeys, (sender, e) =>
                {
                    if (!shortcut.Enabled) return;

                    var programs = getProgramsSnapshot?.Invoke();
                    var program = programs?.FirstOrDefault(p => string.Equals(p.UniqueIdentifier, shortcut.UniqueIdentifier, StringComparison.OrdinalIgnoreCase));
                    if (program != null)
                    {
                        _ = Task.Run(() => ActivateOrLaunch(program));
                    }
                    else
                    {
                        Main.Context?.API?.LogWarn(ClassName, $"Target program not found for hotkey '{shortcut.Hotkey}' ({shortcut.Name})");
                    }
                });

                RegisteredKeys.Add(hotkeyName);
                return true;
            }
            catch (Exception ex)
            {
                Main.Context?.API?.LogWarn(ClassName, $"Failed to register hotkey '{shortcut.Hotkey}' for '{shortcut.Name}': {ex.Message}");
                return false;
            }
        }

        public static bool RegisterOrUpdateShortcut(AppCustomShortcut shortcut, Func<IReadOnlyList<IProgram>> getProgramsSnapshot, out string error)
        {
            error = null;
            if (shortcut == null) return false;

            _getProgramsSnapshot = getProgramsSnapshot;
            EnsureGlobalKeyboardHook();

            if (string.IsNullOrWhiteSpace(shortcut.Hotkey) || !shortcut.Enabled)
            {
                UnregisterShortcut(shortcut.UniqueIdentifier);
                return true;
            }

            // Double tap hotkey (Ctrl + Ctrl, Alt + Alt, etc.)
            var doubleMod = ParseDoubleTapModifier(shortcut.Hotkey);
            if (doubleMod != DoubleTapModifier.None)
            {
                lock (SyncLock)
                {
                    if (!DoubleTapShortcuts.TryGetValue(doubleMod, out var list))
                    {
                        list = new List<AppCustomShortcut>();
                        DoubleTapShortcuts[doubleMod] = list;
                    }
                    list.RemoveAll(s => string.Equals(s.UniqueIdentifier, shortcut.UniqueIdentifier, StringComparison.OrdinalIgnoreCase));
                    list.Add(shortcut);
                }
                return true;
            }

            var app = Application.Current;
            if (app == null)
            {
                error = "Application dispatcher not available.";
                return false;
            }

            string hotkeyName = "Flow_App_" + shortcut.UniqueIdentifier;
            try
            {
                var model = new HotkeyModel(shortcut.Hotkey);
                if (!model.Validate(true))
                {
                    error = $"Invalid hotkey combination: '{shortcut.Hotkey}'.";
                    return false;
                }

                RunOnDispatcher(() =>
                {
                    lock (SyncLock)
                    {
                        try
                        {
                            HotkeyManager.Current.AddOrReplace(hotkeyName, model.CharKey, model.ModifierKeys, (sender, e) =>
                            {
                                if (!shortcut.Enabled) return;

                                var programs = getProgramsSnapshot?.Invoke();
                                var program = programs?.FirstOrDefault(p => string.Equals(p.UniqueIdentifier, shortcut.UniqueIdentifier, StringComparison.OrdinalIgnoreCase));
                                if (program != null)
                                {
                                    _ = Task.Run(() => ActivateOrLaunch(program));
                                }
                            });

                            RegisteredKeys.Add(hotkeyName);
                        }
                        catch (Exception ex)
                        {
                            Main.Context?.API?.LogWarn(ClassName, $"Failed to register '{shortcut.Hotkey}': {ex.Message}");
                        }
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to register '{shortcut.Hotkey}': {ex.Message}";
                return false;
            }
        }

        public static void UnregisterShortcut(string uniqueIdentifier)
        {
            if (string.IsNullOrEmpty(uniqueIdentifier)) return;

            string hotkeyName = "Flow_App_" + uniqueIdentifier;

            lock (SyncLock)
            {
                foreach (var list in DoubleTapShortcuts.Values)
                {
                    list.RemoveAll(s => string.Equals(s.UniqueIdentifier, uniqueIdentifier, StringComparison.OrdinalIgnoreCase));
                }
            }

            RunOnDispatcher(() =>
            {
                lock (SyncLock)
                {
                    if (RegisteredKeys.Contains(hotkeyName))
                    {
                        try
                        {
                            HotkeyManager.Current.Remove(hotkeyName);
                        }
                        catch
                        {
                            // Ignored
                        }
                        RegisteredKeys.Remove(hotkeyName);
                    }
                }
            });
        }

        public static bool CheckAvailability(string hotkeyStr, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(hotkeyStr))
            {
                reason = "Hotkey is empty.";
                return false;
            }

            // Double tap hotkeys are always available
            if (ParseDoubleTapModifier(hotkeyStr) != DoubleTapModifier.None)
            {
                return true;
            }

            try
            {
                var model = new HotkeyModel(hotkeyStr);
                if (!model.Validate(true))
                {
                    reason = "Incomplete or invalid key combination.";
                    return false;
                }

                var app = Application.Current;
                if (app?.Dispatcher == null) return true;

                if (app.Dispatcher.CheckAccess())
                {
                    string testName = "Flow_Hotkey_Test_" + Guid.NewGuid().ToString("N");
                    try
                    {
                        HotkeyManager.Current.AddOrReplace(testName, model.CharKey, model.ModifierKeys, (s, e) => { });
                        return true;
                    }
                    catch (Exception ex)
                    {
                        reason = ex.Message;
                        return false;
                    }
                    finally
                    {
                        try
                        {
                            HotkeyManager.Current.Remove(testName);
                        }
                        catch { }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }
        }

        #region Bring App To Front / Activate Or Launch

        public static void ActivateOrLaunch(IProgram program)
        {
            if (program == null) return;

            try
            {
                if (TryActivateRunningWindow(program))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Main.Context?.API?.LogException(ClassName, $"Failed to activate running window for '{program.Name}'", ex);
            }

            try
            {
                program.Launch(false);
            }
            catch (Exception ex)
            {
                Main.Context?.API?.LogException(ClassName, $"Failed to launch '{program.Name}'", ex);
            }
        }

        private static bool TryActivateRunningWindow(IProgram program)
        {
            string targetExe = null;
            if (program is Win32 win32)
            {
                string targetPath = !string.IsNullOrWhiteSpace(win32.LnkResolvedPath)
                    ? win32.LnkResolvedPath
                    : (!string.IsNullOrWhiteSpace(win32.ExecutablePath) ? win32.ExecutablePath : win32.FullPath);

                if (!string.IsNullOrWhiteSpace(targetPath))
                {
                    try { targetExe = Path.GetFileNameWithoutExtension(targetPath); } catch { }
                }
            }

            if (string.IsNullOrWhiteSpace(targetExe) && !string.IsNullOrWhiteSpace(program.Location))
            {
                try { targetExe = Path.GetFileNameWithoutExtension(program.Location); } catch { }
            }

            // 1. Search for running processes by executable name
            if (!string.IsNullOrWhiteSpace(targetExe))
            {
                Process[] processes;
                try
                {
                    processes = Process.GetProcessesByName(targetExe);
                }
                catch
                {
                    processes = Array.Empty<Process>();
                }

                if (processes.Length > 0)
                {
                    var pids = new HashSet<uint>();
                    foreach (var p in processes)
                    {
                        pids.Add((uint)p.Id);
                    }

                    nint targetHwnd = 0;

                    // First check process MainWindowHandle
                    foreach (var p in processes)
                    {
                        try
                        {
                            if (p.MainWindowHandle != 0 && IsWindowVisible(p.MainWindowHandle))
                            {
                                targetHwnd = p.MainWindowHandle;
                                break;
                            }
                        }
                        catch { }
                    }

                    // If MainWindowHandle not found, enumerate top-level windows
                    if (targetHwnd == 0)
                    {
                        EnumWindows((hwnd, lParam) =>
                        {
                            if (!IsWindowVisible(hwnd)) return true;
                            if (GetWindow(hwnd, 4 /* GW_OWNER */) != 0) return true;

                            GetWindowThreadProcessId(hwnd, out uint pid);
                            if (pids.Contains(pid))
                            {
                                int length = GetWindowTextLength(hwnd);
                                if (length > 0)
                                {
                                    targetHwnd = hwnd;
                                    return false; // found top window
                                }
                            }
                            return true;
                        }, 0);
                    }

                    if (targetHwnd != 0)
                    {
                        BringWindowToForeground(targetHwnd);
                        return true;
                    }
                }
            }

            // 2. Search windows by program name in window title (UWP or mismatched process name)
            if (!string.IsNullOrWhiteSpace(program.Name))
            {
                nint namedHwnd = 0;
                EnumWindows((hwnd, lParam) =>
                {
                    if (!IsWindowVisible(hwnd)) return true;
                    if (GetWindow(hwnd, 4 /* GW_OWNER */) != 0) return true;

                    int length = GetWindowTextLength(hwnd);
                    if (length > 0)
                    {
                        var sb = new StringBuilder(length + 1);
                        GetWindowText(hwnd, sb, sb.Capacity);
                        string title = sb.ToString();
                        if (title.Contains(program.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            namedHwnd = hwnd;
                            return false;
                        }
                    }
                    return true;
                }, 0);

                if (namedHwnd != 0)
                {
                    BringWindowToForeground(namedHwnd);
                    return true;
                }
            }

            return false;
        }

        private static void BringWindowToForeground(nint hwnd)
        {
            const int SW_RESTORE = 9;
            const int SW_SHOW = 5;

            if (IsIconic(hwnd))
            {
                ShowWindowAsync(hwnd, SW_RESTORE);
            }
            else
            {
                ShowWindowAsync(hwnd, SW_SHOW);
            }

            uint currentThread = GetCurrentThreadId();
            uint targetThread = GetWindowThreadProcessId(hwnd, out _);

            if (currentThread != targetThread)
            {
                AttachThreadInput(currentThread, targetThread, true);
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
                AttachThreadInput(currentThread, targetThread, false);
            }
            else
            {
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }
        }

        #endregion

        public static void Dispose()
        {
            if (_globalKeyboardHookRegistered)
            {
                try
                {
                    Main.Context?.API?.RemoveGlobalKeyboardCallback(OnGlobalKeyboardEvent);
                    _globalKeyboardHookRegistered = false;
                }
                catch { }
            }

            RunOnDispatcher(() =>
            {
                lock (SyncLock)
                {
                    foreach (var key in RegisteredKeys)
                    {
                        try
                        {
                            HotkeyManager.Current.Remove(key);
                        }
                        catch { }
                    }
                    RegisteredKeys.Clear();
                    DoubleTapShortcuts.Clear();
                }
            });
        }
    }
}
