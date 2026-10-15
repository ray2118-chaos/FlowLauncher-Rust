using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Flow.Launcher.Infrastructure.Native;
using Flow.Launcher.Plugin.WindowsSettings.Classes;
using Flow.Launcher.Plugin.WindowsSettings.Helper;
using Flow.Launcher.Plugin.WindowsSettings.Properties;

namespace Flow.Launcher.Plugin.WindowsSettings
{
    /// <summary>
    /// Main class of this plugin that implement all used interfaces.
    /// </summary>
    public sealed class Main : IPlugin, IContextMenu, IPluginI18n, IDisposable
    {
        /// <summary>
        /// The name of this assembly.
        /// </summary>
        private readonly string _assemblyName;

        /// <summary>
        /// The initial context for this plugin (contains API and meta-data).
        /// </summary>
        internal static PluginInitContext Context { get; private set; } = null!;
        private PluginInitContext? _context;

        /// <summary>
        /// The path to the icon for windows setting result.
        /// </summary>
        private const string windowSettingsIconPath = "Images/WindowsSettings.light.png";

        /// <summary>
        /// The path to the icon for control panel result.
        /// </summary>
        private const string controlPanelIconPath = "Images/ControlPanel_Small.png";

        /// <summary>
        /// Indicate that the plugin is disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// List that contain all settings.
        /// </summary>
        private IEnumerable<WindowsSetting>? _settingsList;

        /// <summary>
        /// List that contains translated string
        /// </summary>
        private IEnumerable<WindowsSetting> _translatedSettingList = new List<WindowsSetting>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> class.
        /// </summary>
        public Main()
        {
            _assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? Name;
        }

        /// <summary>
        /// Gets the localized name.
        /// </summary>
        public string Name => Resources.PluginTitle;

        /// <summary>
        /// Gets the localized description.
        /// </summary>
        public string Description => Resources.PluginDescription;

        /// <summary>
        /// Initialize the plugin with the given <see cref="PluginInitContext"/>.
        /// </summary>
        /// <param name="context">The <see cref="PluginInitContext"/> for this plugin.</param>
        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Context = _context;

            _settingsList = JsonSettingsListHelper.ReadAllPossibleSettings();
            _settingsList = UnsupportedSettingsHelper.FilterByBuild(_settingsList);

            Log.Init(_context.API);
            ResultHelper.Init(_context.API);

            _translatedSettingList = TranslationHelper.TranslateAllSettings(_settingsList);
            SyncNativeSettings();
        }

        /// <summary>
        /// Return a filtered list, based on the given query.
        /// </summary>
        /// <param name="query">The query to filter the list.</param>
        /// <returns>A filtered list, can be empty when nothing was found.</returns>
        public List<Result> Query(Query query)
         {
            var newList = ResultHelper.GetResultList(_translatedSettingList, query, windowSettingsIconPath, controlPanelIconPath);
            return newList;
        }

        public void OnCultureInfoChanged(CultureInfo newCulture)
        {
            _translatedSettingList = TranslationHelper.TranslateAllSettings(_settingsList);
            SyncNativeSettings();
        }

        private void SyncNativeSettings()
        {
            if (!Flow.Launcher.Infrastructure.Native.FlowNativeMethods.IsAvailable || _translatedSettingList == null) return;

            Flow.Launcher.Infrastructure.Native.FlowNativeMethods.ClearWindowsSettings();
            var list = _translatedSettingList as IList<WindowsSetting> ?? _translatedSettingList.ToList();
            for (uint i = 0; i < list.Count; i++)
            {
                var item = list[(int)i];
                var altNames = item.AltNames != null ? string.Join(' ', item.AltNames) : string.Empty;
                var appType = item.Type == "AppSettingsApp" ? 0u : (item.Type == "ControlPanelApp" ? 1u : 2u);

                Flow.Launcher.Infrastructure.Native.FlowNativeMethods.RegisterWindowsSetting(
                    i,
                    item.Name ?? string.Empty,
                    item.Area ?? string.Empty,
                    altNames,
                    appType);
            }
        }

        /// <summary>
        /// Return a list context menu entries for a given <see cref="Result"/> (shown at the right side of the result).
        /// </summary>
        /// <param name="selectedResult">The <see cref="Result"/> for the list with context menu entries.</param>
        /// <returns>A list context menu entries.</returns>
        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return ContextMenuHelper.GetContextMenu(selectedResult, _assemblyName);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Wrapper method for <see cref="Dispose"/> that dispose additional objects and events form the plugin itself.
        /// </summary>
        /// <param name="disposing">Indicate that the plugin is disposed.</param>
        private void Dispose(bool disposing)
        {
            if (_disposed || !disposing)
            {
                return;
            }

            _disposed = true;
        }

        /// <summary>
        /// Gets the localized name.
        /// </summary>
        public string GetTranslatedPluginTitle()
        {
            return Name;
        }

        /// <summary>
        /// Gets the localized description.
        /// </summary>
        public string GetTranslatedPluginDescription()
        {
            return Description;
        }
    }
}
