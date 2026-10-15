using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Flow.Launcher.Plugin.Program.Programs;

namespace Flow.Launcher.Plugin.Program.Views
{
    public partial class AppAliasDialog : Window
    {
        private readonly IProgram _program;
        private readonly Settings _settings;
        private AppCustomShortcut _shortcut;

        public AppAliasDialog(IProgram program, AppCustomShortcut shortcut, Settings settings)
        {
            _program = program ?? throw new ArgumentNullException(nameof(program));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _shortcut = shortcut;

            InitializeComponent();

            txtAppName.Text = _program.Name;
            _ = LoadProgramIconAsync();

            if (!string.IsNullOrWhiteSpace(_shortcut?.Alias))
            {
                tbAlias.Text = _shortcut.Alias;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            tbAlias.Focus();
            tbAlias.SelectAll();
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
                // Fallback
            }
        }

        private void TbAlias_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnSave_OnClick(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }

        private void BtnSave_OnClick(object sender, RoutedEventArgs e)
        {
            string alias = tbAlias.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(alias))
            {
                BtnRemove_OnClick(sender, e);
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

            _shortcut.Alias = alias;
            Main.Context.API.SaveSettingJsonStorage<Settings>();
            Main.ResetCache();

            DialogResult = true;
            Close();
        }

        private void BtnRemove_OnClick(object sender, RoutedEventArgs e)
        {
            if (_shortcut != null)
            {
                _shortcut.Alias = string.Empty;
                if (string.IsNullOrWhiteSpace(_shortcut.Hotkey))
                {
                    _settings.CustomShortcuts.Remove(_shortcut);
                }

                Main.Context.API.SaveSettingJsonStorage<Settings>();
                Main.ResetCache();
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
