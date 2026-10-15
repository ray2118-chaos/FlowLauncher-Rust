using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Flow.Launcher.Infrastructure.Native;

namespace Flow.Launcher.Plugin.Clipboard
{
    public class Main : IPlugin, IContextMenu, IPluginI18n
    {
        internal static PluginInitContext Context { get; private set; } = null!;
        private const string IcoPath = "Images/clipboard.png";
        private DispatcherTimer _clipboardPoller = null!;
        private string _lastObservedText = string.Empty;

        public string GetTranslatedPluginTitle() => "Clipboard History";

        public string GetTranslatedPluginDescription() => "Ultra-fast Rust-powered clipboard history manager with preview and instant paste";

        public void Init(PluginInitContext context)
        {
            Context = context;

            // Start background poller for seamless clipboard capture
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                _clipboardPoller = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500),
                };
                _clipboardPoller.Tick += (s, e) => CheckClipboard();
                _clipboardPoller.Start();
            });
        }

        private void CheckClipboard()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    var text = System.Windows.Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text) && text != _lastObservedText)
                    {
                        _lastObservedText = text;
                        if (FlowNativeMethods.IsAvailable)
                        {
                            FlowNativeMethods.AddClipboardHistory(text);
                        }
                    }
                }
            }
            catch
            {
                // Clipboard may be locked by another application
            }
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            if (!FlowNativeMethods.IsAvailable)
            {
                results.Add(new Result
                {
                    Title = "Clipboard History",
                    SubTitle = "Native acceleration unavailable",
                    IcoPath = IcoPath,
                });
                return results;
            }

            var searchTerm = query.Search.Trim();
            var nativeItems = FlowNativeMethods.SearchClipboardHistory(searchTerm, 50);

            if (nativeItems.Count == 0)
            {
                results.Add(new Result
                {
                    Title = string.IsNullOrEmpty(searchTerm) ? "Clipboard History is Empty" : "No Matching Clipboard Items",
                    SubTitle = "Copied text and snippets will appear here automatically",
                    IcoPath = IcoPath,
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe77f"),
                });
                return results;
            }

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            for (int i = 0; i < nativeItems.Count; i++)
            {
                var item = nativeItems[i];
                var preview = item.GetPreview();
                var id = item.Id;
                var charCount = item.CharCount;

                var diffSec = Math.Max(0, (nowMs - (long)item.TimestampMs) / 1000);
                string timeStr;
                if (diffSec < 60) timeStr = "Just now";
                else if (diffSec < 3600) timeStr = $"{diffSec / 60}m ago";
                else if (diffSec < 86400) timeStr = $"{diffSec / 3600}h ago";
                else timeStr = $"{diffSec / 86400}d ago";

                results.Add(new Result
                {
                    Title = preview.Replace("\r", " ").Replace("\n", " "),
                    SubTitle = $"{charCount} chars • {timeStr} • Press Enter to paste",
                    IcoPath = IcoPath,
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe77f"),
                    Score = 300 - i,
                    Action = _ =>
                    {
                        if (FlowNativeMethods.GetClipboardText(id, out var fullText))
                        {
                            try
                            {
                                System.Windows.Clipboard.SetText(fullText);
                                Context.API.HideMainWindow();
                            }
                            catch (Exception ex)
                            {
                                Context.API.ShowMsgBox($"Failed to copy text: {ex.Message}");
                            }
                        }
                        return true;
                    },
                    ContextData = item,
                });
            }

            return results;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var menus = new List<Result>();
            if (selectedResult.ContextData is NativeClipboardItem item)
            {
                menus.Add(new Result
                {
                    Title = "Copy to Clipboard",
                    SubTitle = "Copy full snippet to clipboard",
                    IcoPath = IcoPath,
                    Action = _ =>
                    {
                        if (FlowNativeMethods.GetClipboardText(item.Id, out var fullText))
                        {
                            System.Windows.Clipboard.SetText(fullText);
                        }
                        return true;
                    }
                });

                menus.Add(new Result
                {
                    Title = "Clear All Clipboard History",
                    SubTitle = "Remove all cached clipboard snippets",
                    IcoPath = IcoPath,
                    Action = _ =>
                    {
                        FlowNativeMethods.ClearClipboardHistory();
                        return true;
                    }
                });
            }

            return menus;
        }
    }
}
