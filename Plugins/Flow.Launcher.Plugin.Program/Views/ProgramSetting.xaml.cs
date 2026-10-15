using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.Plugin.Program.Views.Models;
using Flow.Launcher.Plugin.Program.Views.Commands;
using Flow.Launcher.Plugin.Program.Programs;
using System.ComponentModel;
using System.Windows.Data;
using Flow.Launcher.Plugin.Program.ViewModels;
using System.Collections.ObjectModel;

namespace Flow.Launcher.Plugin.Program.Views
{
    /// <summary>
    /// Interaction logic for ProgramSetting.xaml
    /// </summary>
    public partial class ProgramSetting : UserControl
    {
        private readonly PluginInitContext context;
        private readonly Settings _settings;
        private GridViewColumnHeader _lastHeaderClicked;
        private ListSortDirection _lastDirection;

        // We do not save all program sources to settings, so using
        // this as temporary holder for displaying all loaded programs sources.
        internal static List<ProgramSource> ProgramSettingDisplayList { get; set; }

        public bool EnableDescription
        {
            get => _settings.EnableDescription;
            set
            {
                Main.ResetCache();
                _settings.EnableDescription = value;
            }
        }

        public bool HideAppsPath
        {
            get => _settings.HideAppsPath;
            set
            {
                Main.ResetCache();
                _settings.HideAppsPath = value;
            }
        }

        public bool HideUninstallers
        {
            get => _settings.HideUninstallers;
            set
            {
                Main.ResetCache();
                _settings.HideUninstallers = value;
            }
        }

        public bool HideDuplicatedWindowsApp
        {
            get => _settings.HideDuplicatedWindowsApp;
            set
            {
                Main.ResetCache();
                _settings.HideDuplicatedWindowsApp = value;
            }
        }

        public bool EnableRegistrySource
        {
            get => _settings.EnableRegistrySource;
            set
            {
                _settings.EnableRegistrySource = value;
                ReIndexing();
            }
        }

        public bool EnableStartMenuSource
        {
            get => _settings.EnableStartMenuSource;
            set
            {
                _settings.EnableStartMenuSource = value;
                ReIndexing();
            }
        }

        public bool EnablePATHSource
        {
            get => _settings.EnablePathSource;
            set
            {
                _settings.EnablePathSource = value;
                ReIndexing();
            }
        }

        public bool EnableUWP
        {
            get => _settings.EnableUWP;
            set
            {
                _settings.EnableUWP = value;
                ReIndexing();
            }
        }

        public bool ShowUWPCheckbox => UWPPackage.SupportUWP();

        public ProgramSetting(PluginInitContext context, Settings settings)
        {
            this.context = context;
            _settings = settings;
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ProgramSettingDisplayList = ProgramSettingDisplay.LoadProgramSources();
            programSourceView.ItemsSource = ProgramSettingDisplayList;

            ViewRefresh();
            LoadAppShortcuts();
        }

        private void ViewRefresh()
        {
            if (programSourceView.Items.Count == 0
                && btnProgramSourceStatus.Visibility == Visibility.Visible
                && btnEditProgramSource.Visibility == Visibility.Visible)
            {
                btnProgramSourceStatus.Visibility = Visibility.Hidden;
                btnEditProgramSource.Visibility = Visibility.Hidden;
                btnDeleteProgramSource.Visibility = Visibility.Hidden;
            }

            if (programSourceView.Items.Count > 0
                && btnProgramSourceStatus.Visibility == Visibility.Hidden
                && btnEditProgramSource.Visibility == Visibility.Hidden)
            {
                btnProgramSourceStatus.Visibility = Visibility.Visible;
                btnEditProgramSource.Visibility = Visibility.Visible;
                btnDeleteProgramSource.Visibility = Visibility.Visible;
            }

            programSourceView.Items.Refresh();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void ReIndexing()
        {
            ViewRefresh();
            indexingPanel.Visibility = Visibility.Visible;
            await Main.IndexProgramsAsync();
            indexingPanel.Visibility = Visibility.Collapsed;
            LoadAppShortcuts();
        }

        private void btnAddProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = new AddProgramSourceViewModel(context, _settings);
            var add = new AddProgramSource(vm);
            if (add.ShowDialog() ?? false)
            {
                ReIndexing();
            }

            ViewRefresh();
        }

        private void DeleteProgramSources(List<ProgramSource> itemsToDelete)
        {
            itemsToDelete.ForEach(t1 => _settings.ProgramSources
                .Remove(_settings.ProgramSources
                    .Where(x => x.UniqueIdentifier == t1.UniqueIdentifier)
                    .FirstOrDefault()));
            itemsToDelete.ForEach(x => ProgramSettingDisplayList.Remove(x));

            ReIndexing();
        }

        private void btnEditProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedProgramSource = programSourceView.SelectedItem as ProgramSource;
            EditProgramSource(selectedProgramSource);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void EditProgramSource(ProgramSource selectedProgramSource)
        {
            if (selectedProgramSource == null)
            {
                string msg = context.API.GetTranslation("flowlauncher_plugin_program_pls_select_program_source");
                context.API.ShowMsgBox(msg);
            }
            else
            {
                var vm = new AddProgramSourceViewModel(context, _settings, selectedProgramSource);
                var add = new AddProgramSource(vm);
                int selectedIndex = programSourceView.SelectedIndex;
                // https://stackoverflow.com/questions/16789360/wpf-listbox-items-with-changing-hashcode
                // Or it can't be unselected after changing Location
                programSourceView.UnselectAll();
                if (add.ShowDialog() ?? false)
                {
                    if (selectedProgramSource.Enabled)
                    {
                        await ProgramSettingDisplay.SetProgramSourcesStatusAsync(new List<ProgramSource> { selectedProgramSource },
                            true); // sync status in win32, uwp and disabled
                        ProgramSettingDisplay.RemoveDisabledFromSettings();
                    }
                    else
                    {
                        await ProgramSettingDisplay.SetProgramSourcesStatusAsync(new List<ProgramSource> { selectedProgramSource },
                            false);
                        ProgramSettingDisplay.StoreDisabledInSettings();
                    }

                    ReIndexing();
                }

                programSourceView.SelectedIndex = selectedIndex;
            }
        }

        private void btnReindex_Click(object sender, RoutedEventArgs e)
        {
            ReIndexing();
        }

        private void BtnProgramSuffixes_OnClick(object sender, RoutedEventArgs e)
        {
            var p = new ProgramSuffixes(context, _settings);
            if (p.ShowDialog() ?? false)
            {
                ReIndexing();
            }
        }

        private void programSourceView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void programSourceView_Drop(object sender, DragEventArgs e)
        {
            var directories = (string[])e.Data.GetData(DataFormats.FileDrop);

            var directoriesToAdd = new List<ProgramSource>();

            if (directories != null && directories.Length > 0)
            {
                foreach (string directory in directories)
                {
                    if (Directory.Exists(directory)
                        && !ProgramSettingDisplayList.Any(x =>
                            x.UniqueIdentifier.Equals(directory, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        var source = new ProgramSource(directory);

                        directoriesToAdd.Add(source);
                    }
                }

                if (directoriesToAdd.Count > 0)
                {
                    directoriesToAdd.ForEach(_settings.ProgramSources.Add);
                    directoriesToAdd.ForEach(ProgramSettingDisplayList.Add);

                    ViewRefresh();
                    ReIndexing();
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void btnLoadAllProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            await ProgramSettingDisplay.DisplayAllProgramsAsync();

            ViewRefresh();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void btnProgramSourceStatus_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedItems = programSourceView
                .SelectedItems.Cast<ProgramSource>()
                .ToList();

            if (selectedItems.Count == 0)
            {
                context.API.ShowMsgBox(context.API.GetTranslation("flowlauncher_plugin_program_pls_select_program_source"));
                return;
            }

            if (HasMoreOrEqualEnabledItems(selectedItems))
            {
                await ProgramSettingDisplay.SetProgramSourcesStatusAsync(selectedItems, false);

                ProgramSettingDisplay.StoreDisabledInSettings();
            }
            else
            {
                await ProgramSettingDisplay.SetProgramSourcesStatusAsync(selectedItems, true);

                ProgramSettingDisplay.RemoveDisabledFromSettings();
            }

            if (await selectedItems.IsReindexRequiredAsync())
                ReIndexing();

            programSourceView.SelectedItems.Clear();

            ViewRefresh();
        }

        private void ProgramSourceView_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            programSourceView.SelectedItems.Clear();
        }

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            ListSortDirection direction;

            if (e.OriginalSource is GridViewColumnHeader headerClicked)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else
                        {
                            direction = ListSortDirection.Ascending;
                        }
                    }

                    var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
                    var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;

                    Sort(sortBy, direction);

                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            var dataView = CollectionViewSource.GetDefaultView(programSourceView.ItemsSource);

            dataView.SortDescriptions.Clear();
            var sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        private static bool HasMoreOrEqualEnabledItems(List<ProgramSource> items)
        {
            var enableCount = items.Where(x => x.Enabled).Count();
            return enableCount >= items.Count - enableCount;
        }

        private void programSourceView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItems = programSourceView
                .SelectedItems.Cast<ProgramSource>()
                .ToList();

            if (HasMoreOrEqualEnabledItems(selectedItems))
            {
                btnProgramSourceStatus.Content = context.API.GetTranslation("flowlauncher_plugin_program_disable");
            }
            else
            {
                btnProgramSourceStatus.Content = context.API.GetTranslation("flowlauncher_plugin_program_enable");
            }
        }

        private void programSourceView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is ProgramSource)
            {
                var selectedProgramSource = programSourceView.SelectedItem as ProgramSource;
                EditProgramSource(selectedProgramSource);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void btnDeleteProgramSource_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedItems = programSourceView
                .SelectedItems.Cast<ProgramSource>()
                .ToList();

            if (selectedItems.Count == 0)
            {
                context.API.ShowMsgBox(context.API.GetTranslation("flowlauncher_plugin_program_pls_select_program_source"));
                return;
            }

            if (!IsAllItemsUserAdded(selectedItems))
            {
                context.API.ShowMsgBox(context.API.GetTranslation("flowlauncher_plugin_program_delete_program_source_select_user_added"));
                return;
            }

            if (context.API.ShowMsgBox(context.API.GetTranslation("flowlauncher_plugin_program_delete_program_source"),
                string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                return;
            }

            DeleteProgramSources(selectedItems);

            if (await selectedItems.IsReindexRequiredAsync())
                ReIndexing();

            programSourceView.SelectedItems.Clear();

            ViewRefresh();
        }

        private bool IsAllItemsUserAdded(List<ProgramSource> items)
        {
            return items.All(x => _settings.ProgramSources.Any(y => y.UniqueIdentifier == x.UniqueIdentifier));
        }

        private void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var listView = sender as ListView;
            var gView = listView.View as GridView;

            var workingWidth =
                listView.ActualWidth - SystemParameters.VerticalScrollBarWidth; // take into account vertical scrollbar

            if (workingWidth <= 0) return;

            var col1 = 0.25;
            var col2 = 0.15;
            var col3 = 0.60;

            gView.Columns[0].Width = workingWidth * col1;
            gView.Columns[1].Width = workingWidth * col2;
            gView.Columns[2].Width = workingWidth * col3;
        }

        #region Raycast App Shortcuts & Aliases

        public ObservableCollection<AppShortcutItemViewModel> AppShortcutsList { get; } = new();
        public ICollectionView AppShortcutsView { get; private set; }

        private void LoadAppShortcuts()
        {
            var programs = Main._allProgramsSnapshot;
            if (programs.Count == 0 && ((Main._win32s?.Count ?? 0) > 0 || (Main._uwps?.Count ?? 0) > 0))
            {
                Main.UpdateProgramsSnapshot();
                programs = Main._allProgramsSnapshot;
            }

            var shortcutsDict = _settings.CustomShortcuts?
                .Where(s => !string.IsNullOrEmpty(s.UniqueIdentifier))
                .ToDictionary(s => s.UniqueIdentifier, System.StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, AppCustomShortcut>(System.StringComparer.OrdinalIgnoreCase);

            var items = programs
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name))
                .Select(p =>
                {
                    shortcutsDict.TryGetValue(p.UniqueIdentifier, out var shortcut);
                    return new AppShortcutItemViewModel(p, shortcut);
                })
                .OrderByDescending(vm => vm.HasHotkey || vm.HasAlias)
                .ThenBy(vm => vm.Name)
                .ToList();

            AppShortcutsList.Clear();
            foreach (var item in items)
            {
                AppShortcutsList.Add(item);
            }

            AppShortcutsView = CollectionViewSource.GetDefaultView(AppShortcutsList);
            AppShortcutsView.Filter = FilterAppShortcuts;
            appShortcutsView.ItemsSource = AppShortcutsView;

            UpdateAppCountText();
        }

        private bool FilterAppShortcuts(object item)
        {
            if (item is not AppShortcutItemViewModel vm) return false;

            string filter = tbAppFilter?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(filter)) return true;

            return vm.Name.Contains(filter, System.StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrEmpty(vm.Location) && vm.Location.Contains(filter, System.StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrEmpty(vm.Alias) && vm.Alias.Contains(filter, System.StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrEmpty(vm.Hotkey) && vm.Hotkey.Contains(filter, System.StringComparison.OrdinalIgnoreCase));
        }

        private void TbAppFilter_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            AppShortcutsView?.Refresh();
            UpdateAppCountText();
        }

        private void UpdateAppCountText()
        {
            if (txtAppCount == null) return;
            int count = AppShortcutsView?.Cast<object>().Count() ?? AppShortcutsList.Count;
            txtAppCount.Text = $"Showing {count} applications";
        }

        private void BtnRefreshApps_OnClick(object sender, RoutedEventArgs e)
        {
            LoadAppShortcuts();
        }

        private void BtnEditAlias_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not AppShortcutItemViewModel vm)
                return;

            var shortcut = _settings.CustomShortcuts.FirstOrDefault(s => s.UniqueIdentifier == vm.UniqueIdentifier);
            var dlg = new AppAliasDialog(vm.Program, shortcut, _settings);
            if (dlg.ShowDialog() == true)
            {
                var updated = _settings.CustomShortcuts.FirstOrDefault(s => s.UniqueIdentifier == vm.UniqueIdentifier);
                vm.Alias = updated?.Alias ?? string.Empty;
                AppShortcutsView?.Refresh();
            }
        }

        private void BtnClearAlias_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not AppShortcutItemViewModel vm)
                return;

            var shortcut = _settings.CustomShortcuts.FirstOrDefault(s => s.UniqueIdentifier == vm.UniqueIdentifier);
            if (shortcut != null)
            {
                shortcut.Alias = string.Empty;
                if (string.IsNullOrWhiteSpace(shortcut.Hotkey))
                {
                    _settings.CustomShortcuts.Remove(shortcut);
                }
                context.API.SaveSettingJsonStorage<Settings>();
                Main.ResetCache();
            }

            vm.Alias = string.Empty;
            AppShortcutsView?.Refresh();
        }

        private void BtnRecordHotkey_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not AppShortcutItemViewModel vm)
                return;

            var shortcut = _settings.CustomShortcuts.FirstOrDefault(s => s.UniqueIdentifier == vm.UniqueIdentifier);
            var dlg = new AppHotkeyDialog(vm.Program, shortcut, _settings);
            if (dlg.ShowDialog() == true)
            {
                var updated = _settings.CustomShortcuts.FirstOrDefault(s => s.UniqueIdentifier == vm.UniqueIdentifier);
                vm.Hotkey = updated?.Hotkey ?? string.Empty;
                AppShortcutsView?.Refresh();
            }
        }

        private void BtnClearHotkey_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not AppShortcutItemViewModel vm)
                return;

            var shortcut = _settings.CustomShortcuts.FirstOrDefault(s => s.UniqueIdentifier == vm.UniqueIdentifier);
            if (shortcut != null)
            {
                shortcut.Hotkey = string.Empty;
                AppHotkeyManager.UnregisterShortcut(vm.UniqueIdentifier);

                if (string.IsNullOrWhiteSpace(shortcut.Alias))
                {
                    _settings.CustomShortcuts.Remove(shortcut);
                }
                context.API.SaveSettingJsonStorage<Settings>();
            }

            vm.Hotkey = string.Empty;
            AppShortcutsView?.Refresh();
        }

        private void AppShortcutEnabled_OnChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not AppShortcutItemViewModel vm)
                return;

            var shortcut = _settings.CustomShortcuts.FirstOrDefault(s => s.UniqueIdentifier == vm.UniqueIdentifier);
            if (shortcut == null)
            {
                shortcut = new AppCustomShortcut
                {
                    UniqueIdentifier = vm.UniqueIdentifier,
                    Name = vm.Name,
                    Location = vm.Location,
                    Alias = vm.Alias,
                    Hotkey = vm.Hotkey,
                    Enabled = vm.Enabled
                };
                _settings.CustomShortcuts.Add(shortcut);
            }
            else
            {
                shortcut.Enabled = vm.Enabled;
            }

            if (shortcut.Enabled && !string.IsNullOrWhiteSpace(shortcut.Hotkey))
            {
                AppHotkeyManager.RegisterOrUpdateShortcut(shortcut, () => Main._allProgramsSnapshot, out _);
            }
            else
            {
                AppHotkeyManager.UnregisterShortcut(shortcut.UniqueIdentifier);
            }

            context.API.SaveSettingJsonStorage<Settings>();
            Main.ResetCache();
        }

        private void AppShortcutsView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (appShortcutsView.View is GridView gView)
            {
                double available = appShortcutsView.ActualWidth - SystemParameters.VerticalScrollBarWidth - 16;
                if (available > 200)
                {
                    gView.Columns[0].Width = System.Math.Max(200, available - 130 - 180 - 75);
                    gView.Columns[1].Width = 130;
                    gView.Columns[2].Width = 180;
                    gView.Columns[3].Width = 75;
                }
            }
        }

        #endregion
    }
}
