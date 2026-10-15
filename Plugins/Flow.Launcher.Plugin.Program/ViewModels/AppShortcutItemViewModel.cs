using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using Flow.Launcher.Plugin.Program.Programs;

namespace Flow.Launcher.Plugin.Program.ViewModels
{
    public class AppShortcutItemViewModel : INotifyPropertyChanged
    {
        private string _alias = string.Empty;
        private string _hotkey = string.Empty;
        private bool _enabled = true;
        private ImageSource _icon;
        private bool _iconLoadAttempted;

        public IProgram Program { get; }

        public string UniqueIdentifier => Program.UniqueIdentifier;
        public string Name => Program.Name;
        public string Location => Program.Location;
        public string IcoPath => Program.IcoPath;

        public ImageSource Icon
        {
            get
            {
                if (_icon == null && !_iconLoadAttempted)
                {
                    _iconLoadAttempted = true;
                    _ = LoadIconAsync();
                }
                return _icon;
            }
            private set
            {
                _icon = value;
                OnPropertyChanged();
            }
        }

        private async Task LoadIconAsync()
        {
            try
            {
                if (Main.Context?.API != null)
                {
                    string path = !string.IsNullOrWhiteSpace(IcoPath) ? IcoPath : Location;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var img = await Main.Context.API.LoadImageAsync(path);
                        if (img != null)
                        {
                            Icon = img;
                            return;
                        }
                    }

                    var fallback = await Main.Context.API.LoadImageAsync("Images/program.png");
                    if (fallback != null)
                    {
                        Icon = fallback;
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }

        public string Alias
        {
            get => _alias;
            set
            {
                if (_alias != value)
                {
                    _alias = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasAlias));
                    OnPropertyChanged(nameof(AliasDisplayText));
                }
            }
        }

        public string Hotkey
        {
            get => _hotkey;
            set
            {
                if (_hotkey != value)
                {
                    _hotkey = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasHotkey));
                    OnPropertyChanged(nameof(HotkeyDisplayText));
                }
            }
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasAlias => !string.IsNullOrWhiteSpace(_alias);
        public bool HasHotkey => !string.IsNullOrWhiteSpace(_hotkey);

        public string AliasDisplayText => HasAlias ? _alias : "+ Add Alias";
        public string HotkeyDisplayText => HasHotkey ? _hotkey : "+ Record Hotkey";

        public AppShortcutItemViewModel(IProgram program, AppCustomShortcut shortcut)
        {
            Program = program;
            if (shortcut != null)
            {
                _alias = shortcut.Alias ?? string.Empty;
                _hotkey = shortcut.Hotkey ?? string.Empty;
                _enabled = shortcut.Enabled;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
