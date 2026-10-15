using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Core.Plugin;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Flow.Launcher
{
    internal static class Notification
    {
        private static readonly string ClassName = nameof(Notification);

        internal static bool legacy = !Win32Helper.IsNotificationSupported();

        private static readonly ConcurrentDictionary<string, Action> _notificationActions = new();

        internal static void Install()
        {
            if (!legacy)
            {
                ToastNotificationManagerCompat.OnActivated += toastArgs =>
                {
                    var actionId = toastArgs.Argument; // Or use toastArgs.UserInput if using input
                    if (_notificationActions.TryGetValue(actionId, out var action))
                    {
                        action?.Invoke();
                    }
                };
            }
        }

        internal static void Uninstall()
        {
            if (!legacy)
            {
                _notificationActions.Clear();
                ToastNotificationManagerCompat.Uninstall();
            }
        }

        public static void Show(string title, string subTitle, string iconPath = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowInternal(title, subTitle, iconPath);
            });
        }

        private static string ResolveIconPath(string iconPath)
        {
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                if (Path.IsPathRooted(iconPath) && File.Exists(iconPath))
                {
                    return iconPath;
                }

                // Check relative to ProgramDirectory
                var programIcon = Path.Combine(Constant.ProgramDirectory, iconPath);
                if (File.Exists(programIcon))
                {
                    return programIcon;
                }

                // Check if any loaded plugin directory contains this relative path
                try
                {
                    var plugins = PluginManager.GetAllLoadedPlugins();
                    if (plugins != null)
                    {
                        foreach (var plugin in plugins)
                        {
                            if (!string.IsNullOrEmpty(plugin.Metadata?.PluginDirectory))
                            {
                                var pluginIcon = Path.Combine(plugin.Metadata.PluginDirectory, iconPath);
                                if (File.Exists(pluginIcon))
                                {
                                    return pluginIcon;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore plugin enumeration errors during early startup
                }
            }

            return Path.Combine(Constant.ProgramDirectory, "Images\\app.png");
        }

        private static void ShowInternal(string title, string subTitle, string iconPath = null)
        {
            // Handle notification for win7/8/early win10
            if (legacy)
            {
                LegacyShow(title, subTitle, iconPath);
                return;
            }

            // Using Windows Notification System
            var Icon = ResolveIconPath(iconPath);

            try
            {
                var builder = new ToastContentBuilder()
                    .AddText(title, hintMaxLines: 1)
                    .AddText(subTitle);

                if (Uri.TryCreate(Icon, UriKind.Absolute, out var uri))
                {
                    builder.AddAppLogoOverride(uri);
                }

                builder.Show();
            }
            catch (InvalidOperationException e)
            {
                // Temporary fix for the Windows 11 notification issue
                // Possibly from 22621.1413 or 22621.1485, judging by post time of #2024
                App.API.LogException(ClassName, "Notification InvalidOperationException Error", e);
                LegacyShow(title, subTitle, iconPath);
            }
            catch (Exception e)
            {
                App.API.LogException(ClassName, "Notification Error", e);
                LegacyShow(title, subTitle, iconPath);
            }
        }

        private static void LegacyShow(string title, string subTitle, string iconPath)
        {
            var msg = new Msg();
            msg.Show(title, subTitle, iconPath);
        }

        public static void ShowWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowInternalWithButton(title, buttonText, buttonAction, subTitle, iconPath);
            });
        }

        private static void ShowInternalWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath = null)
        {
            // Handle notification for win7/8/early win10
            if (legacy)
            {
                LegacyShowWithButton(title, buttonText, buttonAction, subTitle, iconPath);
                return;
            }

            // Using Windows Notification System
            var Icon = ResolveIconPath(iconPath);

            try
            {
                var guid = Guid.NewGuid().ToString();
                var builder = new ToastContentBuilder()
                    .AddText(title, hintMaxLines: 1)
                    .AddText(subTitle)
                    .AddButton(buttonText, ToastActivationType.Background, guid);

                if (Uri.TryCreate(Icon, UriKind.Absolute, out var uri))
                {
                    builder.AddAppLogoOverride(uri);
                }

                builder.Show();
                _notificationActions.AddOrUpdate(guid, buttonAction, (key, oldValue) => buttonAction);
            }
            catch (InvalidOperationException e)
            {
                // Temporary fix for the Windows 11 notification issue
                // Possibly from 22621.1413 or 22621.1485, judging by post time of #2024
                App.API.LogException(ClassName, "Notification InvalidOperationException Error", e);
                LegacyShowWithButton(title, buttonText, buttonAction, subTitle, iconPath);
            }
            catch (Exception e)
            {
                App.API.LogException(ClassName, "Notification Error", e);
                LegacyShowWithButton(title, buttonText, buttonAction, subTitle, iconPath);
            }
        }

        private static void LegacyShowWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath)
        {
            var msg = new MsgWithButton();
            msg.Show(title, buttonText, buttonAction, subTitle, iconPath);
        }
    }
}
