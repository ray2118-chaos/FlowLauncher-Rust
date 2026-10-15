using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin.Program.Programs;

namespace Flow.Launcher.Plugin.Program.Views
{
    public partial class AppHotkeyDialog : Window
    {
        private readonly IProgram _program;
        private readonly Settings _settings;
        private AppCustomShortcut _shortcut;
        private string _recordedHotkey = string.Empty;

        public string RecordedHotkey => _recordedHotkey;

        public AppHotkeyDialog(IProgram program, AppCustomShortcut shortcut, Settings settings)
        {
            _program = program ?? throw new ArgumentNullException(nameof(program));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _shortcut = shortcut;

            InitializeComponent();

            txtAppName.Text = _program.Name;
            _ = LoadProgramIconAsync();

            if (!string.IsNullOrWhiteSpace(_shortcut?.Hotkey))
            {
                _recordedHotkey = _shortcut.Hotkey;
                RenderHotkeyPills(_recordedHotkey);
                btnSave.IsEnabled = true;
            }
        }

        private async Task LoadProgramIconAsync()
        {
            try
            {
                if (Main.Context?.API != null)
                {
                    string path = !string.IsNullOrWhiteSpace(_program.IcoPath) ? _program.IcoPath : _program.Location;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var img = await Main.Context.API.LoadImageAsync(path);
                        if (img != null)
                        {
                            imgAppIcon.Source = img;
                            return;
                        }
                    }

                    var fallback = await Main.Context.API.LoadImageAsync("Images/program.png");
                    if (fallback != null)
                    {
                        imgAppIcon.Source = fallback;
                    }
                }
            }
            catch
            {
                // Fallback to default or empty icon
            }
        }

        private Key _lastModifierKeyDown = Key.None;
        private DateTime _lastModifierKeyDownTime = DateTime.MinValue;

        private void BtnPresetDouble_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                _recordedHotkey = tag;
                RenderHotkeyPills(_recordedHotkey);
                btnSave.IsEnabled = true;
                txtStatusMessage.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            bool win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0 || key == Key.LeftCtrl || key == Key.RightCtrl;
            bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0 || key == Key.LeftAlt || key == Key.RightAlt;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 || key == Key.LeftShift || key == Key.RightShift;

            if (key == Key.Escape && !win && !ctrl && !alt && !shift)
            {
                DialogResult = false;
                Close();
                return;
            }

            if (key == Key.Enter && btnSave.IsEnabled)
            {
                BtnSave_OnClick(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // If the user pressed a modifier key
            if (IsModifierKey(key))
            {
                var now = DateTime.UtcNow;
                if (key == _lastModifierKeyDown && (now - _lastModifierKeyDownTime).TotalMilliseconds <= 500)
                {
                    // User tapped the same modifier twice!
                    string modName = key switch
                    {
                        Key.LeftCtrl or Key.RightCtrl => "Ctrl",
                        Key.LeftAlt or Key.RightAlt => "Alt",
                        Key.LeftShift or Key.RightShift => "Shift",
                        Key.LWin or Key.RWin => "Win",
                        _ => "Ctrl"
                    };

                    _recordedHotkey = $"{modName} + {modName}";
                    RenderHotkeyPills(_recordedHotkey);
                    btnSave.IsEnabled = true;
                    txtStatusMessage.Visibility = Visibility.Collapsed;
                    _lastModifierKeyDown = Key.None;
                    e.Handled = true;
                    return;
                }

                _lastModifierKeyDown = key;
                _lastModifierKeyDownTime = now;

                RenderModifierPreview(ctrl, alt, shift, win);
                e.Handled = true;
                return;
            }

            _lastModifierKeyDown = Key.None;

            // Non-modifier key pressed with or without modifiers
            var model = new HotkeyModel(alt, shift, win, ctrl, key);
            string hotkeyStr = model.ToString();

            if (model.Validate(true))
            {
                _recordedHotkey = hotkeyStr;
                RenderHotkeyPills(_recordedHotkey);

                if (AppHotkeyManager.CheckAvailability(_recordedHotkey, out string reason))
                {
                    txtStatusMessage.Visibility = Visibility.Collapsed;
                    txtStatusMessage.Text = string.Empty;
                    btnSave.IsEnabled = true;
                }
                else
                {
                    txtStatusMessage.Visibility = Visibility.Visible;
                    txtStatusMessage.Text = $"Warning: '{_recordedHotkey}' might already be in use ({reason}).";
                    btnSave.IsEnabled = true;
                }
            }
            else
            {
                RenderIncompleteKey(hotkeyStr);
                btnSave.IsEnabled = false;
            }

            e.Handled = true;
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            // Do not clear the successfully recorded hotkey on key up
        }

        private static bool IsModifierKey(Key key)
        {
            return key is Key.LeftCtrl or Key.RightCtrl
                       or Key.LeftAlt or Key.RightAlt
                       or Key.LeftShift or Key.RightShift
                       or Key.LWin or Key.RWin;
        }

        private void RenderModifierPreview(bool ctrl, bool alt, bool shift, bool win)
        {
            var parts = new List<string>();
            if (win) parts.Add("Win");
            if (ctrl) parts.Add("Ctrl");
            if (alt) parts.Add("Alt");
            if (shift) parts.Add("Shift");

            if (parts.Count == 0) return;

            pillsContainer.Children.Clear();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                {
                    pillsContainer.Children.Add(CreatePlusText());
                }
                pillsContainer.Children.Add(CreatePillBorder(parts[i], isAccent: true));
            }
            pillsContainer.Children.Add(CreatePlusText());
            pillsContainer.Children.Add(new TextBlock
            {
                Text = "Press a key...",
                FontStyle = FontStyles.Italic,
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            });

            btnSave.IsEnabled = false;
        }

        private void RenderIncompleteKey(string text)
        {
            pillsContainer.Children.Clear();
            pillsContainer.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        private void RenderHotkeyPills(string hotkey)
        {
            pillsContainer.Children.Clear();
            if (string.IsNullOrWhiteSpace(hotkey))
            {
                pillsContainer.Children.Add(txtPlaceholder);
                return;
            }

            var parts = hotkey.Split(new[] { "+", " " }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    pillsContainer.Children.Add(CreatePlusText());
                }
                pillsContainer.Children.Add(CreatePillBorder(parts[i], isAccent: false));
            }
        }

        private Border CreatePillBorder(string keyText, bool isAccent)
        {
            var border = new Border
            {
                Style = (Style)FindResource("KeyPillBorder")
            };
            if (isAccent)
            {
                border.BorderBrush = System.Windows.Media.Brushes.DodgerBlue;
            }

            var text = new TextBlock
            {
                Text = keyText,
                Style = (Style)FindResource("KeyPillText")
            };
            border.Child = text;
            return border;
        }

        private TextBlock CreatePlusText()
        {
            return new TextBlock
            {
                Text = "+",
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 2, 0)
            };
        }

        private void BtnSave_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_recordedHotkey))
            {
                DialogResult = false;
                Close();
                return;
            }

            if (_shortcut == null)
            {
                _shortcut = new AppCustomShortcut
                {
                    UniqueIdentifier = _program.UniqueIdentifier,
                    Name = _program.Name,
                    Location = _program.Location,
                    Enabled = true
                };
                _settings.CustomShortcuts.Add(_shortcut);
            }

            _shortcut.Hotkey = _recordedHotkey;
            _shortcut.Enabled = true;

            bool registered = AppHotkeyManager.RegisterOrUpdateShortcut(_shortcut, () => Main._allProgramsSnapshot, out string error);
            if (!registered && !string.IsNullOrEmpty(error))
            {
                Main.Context.API.ShowMsgError("Hotkey Registration Failed", error);
            }
            else
            {
                Main.Context.API.SaveSettingJsonStorage<Settings>();
            }

            DialogResult = true;
            Close();
        }

        private void BtnClear_OnClick(object sender, RoutedEventArgs e)
        {
            _recordedHotkey = string.Empty;
            if (_shortcut != null)
            {
                _shortcut.Hotkey = string.Empty;
                AppHotkeyManager.UnregisterShortcut(_shortcut.UniqueIdentifier);

                if (string.IsNullOrWhiteSpace(_shortcut.Alias))
                {
                    _settings.CustomShortcuts.Remove(_shortcut);
                }

                Main.Context.API.SaveSettingJsonStorage<Settings>();
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
