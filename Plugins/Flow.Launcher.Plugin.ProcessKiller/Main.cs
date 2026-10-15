using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Flow.Launcher.Plugin.ProcessKiller.ViewModels;
using Flow.Launcher.Plugin.ProcessKiller.Views;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISettingProvider
    {
        internal static PluginInitContext Context { get; private set; }

        private Settings _settings;

        private readonly ProcessHelper processHelper = new();

        private SettingsViewModel _viewModel;

        public void Init(PluginInitContext context)
        {
            Context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();
            _viewModel = new SettingsViewModel(_settings);
        }

        public List<Result> Query(Query query)
        {
            return CreateResultsFromQuery(query);
        }

        public string GetTranslatedPluginTitle()
        {
            return Localize.flowlauncher_plugin_processkiller_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_processkiller_plugin_description();
        }

        public List<Result> LoadContextMenus(Result result)
        {
            var menuOptions = new List<Result>();
            var procName = result.ContextData as string;
            var processPath = result.SubTitle;

            if (!string.IsNullOrEmpty(procName))
            {
                menuOptions.Add(new Result
                {
                    Title = Localize.flowlauncher_plugin_processkiller_kill_instances(),
                    SubTitle = !string.IsNullOrEmpty(processPath) ? processPath : procName,
                    Action = _ =>
                    {
                        if (Flow.Launcher.Infrastructure.Native.FlowNativeMethods.IsAvailable)
                        {
                            Flow.Launcher.Infrastructure.Native.FlowNativeMethods.KillProcessesByName(procName);
                        }
                        Context.API.ReQuery();
                        return true;
                    },
                    IcoPath = !string.IsNullOrEmpty(result.IcoPath) ? result.IcoPath : "Images/processkiller.png"
                });
            }

            return menuOptions;
        }

        private List<Result> CreateResultsFromQuery(Query query)
        {
            // Get all non-system processes via native snapshot
            var allProcessList = processHelper.GetMatchingProcesses();
            if (allProcessList == null || allProcessList.Count == 0)
            {
                return null;
            }

            var searchTerm = query.Search;
            var processlist = new List<ProcessResult>();

            foreach (var p in allProcessList)
            {
                var progressNameIdTitle = p.GetProcessNameIdTitle();
                var windowTitle = p.WindowTitle;
                var hasWindowTitle = !string.IsNullOrEmpty(windowTitle);

                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    processlist.Add(new ProcessResult
                    {
                        Pid = p.Pid,
                        ProcessName = p.Name,
                        ExePath = p.ExePath,
                        WindowTitle = windowTitle,
                        MemoryMb = p.MemoryMb,
                        Score = hasWindowTitle && _settings.PutVisibleWindowProcessesTop ? 200 : 0,
                        Title = hasWindowTitle && _settings.ShowWindowTitle ? windowTitle : progressNameIdTitle,
                        TitleMatch = null,
                        Tooltip = progressNameIdTitle
                    });
                }
                else
                {
                    if (hasWindowTitle)
                    {
                        var windowTitleMatch = Context.API.FuzzySearch(searchTerm, windowTitle);
                        var processNameIdMatch = Context.API.FuzzySearch(searchTerm, progressNameIdTitle);
                        var score = Math.Max(windowTitleMatch.Score, processNameIdMatch.Score);
                        if (score > 0)
                        {
                            if (_settings.PutVisibleWindowProcessesTop)
                            {
                                score += 200;
                            }
                            processlist.Add(new ProcessResult
                            {
                                Pid = p.Pid,
                                ProcessName = p.Name,
                                ExePath = p.ExePath,
                                WindowTitle = windowTitle,
                                MemoryMb = p.MemoryMb,
                                Score = score,
                                Title = _settings.ShowWindowTitle ? windowTitle : progressNameIdTitle,
                                TitleMatch = score == windowTitleMatch.Score ? windowTitleMatch : null,
                                Tooltip = progressNameIdTitle
                            });
                        }
                    }
                    else
                    {
                        var processNameIdMatch = Context.API.FuzzySearch(searchTerm, progressNameIdTitle);
                        var score = processNameIdMatch.Score;
                        if (score > 0)
                        {
                            processlist.Add(new ProcessResult
                            {
                                Pid = p.Pid,
                                ProcessName = p.Name,
                                ExePath = p.ExePath,
                                WindowTitle = string.Empty,
                                MemoryMb = p.MemoryMb,
                                Score = score,
                                Title = progressNameIdTitle,
                                TitleMatch = processNameIdMatch,
                                Tooltip = progressNameIdTitle
                            });
                        }
                    }
                }
            }

            var results = new List<Result>();
            foreach (var pr in processlist)
            {
                var iconPath = !string.IsNullOrEmpty(pr.ExePath) ? pr.ExePath : "Images/processkiller.png";
                var subtitle = !string.IsNullOrEmpty(pr.ExePath)
                    ? (pr.MemoryMb > 0 ? $"{pr.ExePath} • {pr.MemoryMb} MB" : pr.ExePath)
                    : (pr.MemoryMb > 0 ? $"{pr.ProcessName} • {pr.MemoryMb} MB" : pr.ProcessName);

                results.Add(new Result
                {
                    IcoPath = iconPath,
                    Title = pr.Title,
                    TitleToolTip = pr.Tooltip,
                    SubTitle = subtitle,
                    TitleHighlightData = pr.TitleMatch?.MatchData,
                    Score = pr.Score,
                    ContextData = pr.ProcessName,
                    AutoCompleteText = $"{Context.CurrentPluginMetadata.ActionKeyword}{Plugin.Query.TermSeparator}{pr.ProcessName}",
                    Action = _ =>
                    {
                        ProcessHelper.TryKill(pr.Pid, pr.ProcessName);
                        Context.API.ReQuery();
                        return true;
                    }
                });
            }

            // Order results by title
            var sortedResults = results.OrderBy(x => x.Title).ToList();

            // Quick option to kill all instances of the same executable
            if (processlist.Count > 1 && !string.IsNullOrEmpty(searchTerm))
            {
                var firstTarget = processlist.First().ProcessName;
                if (processlist.All(x => string.Equals(x.ProcessName, firstTarget, StringComparison.OrdinalIgnoreCase)))
                {
                    sortedResults.Insert(0, new Result
                    {
                        IcoPath = sortedResults.FirstOrDefault()?.IcoPath ?? "Images/processkiller.png",
                        Title = Localize.flowlauncher_plugin_processkiller_kill_all(firstTarget),
                        SubTitle = Localize.flowlauncher_plugin_processkiller_kill_all_count(processlist.Count),
                        Score = 300,
                        Action = _ =>
                        {
                            if (Flow.Launcher.Infrastructure.Native.FlowNativeMethods.IsAvailable)
                            {
                                Flow.Launcher.Infrastructure.Native.FlowNativeMethods.KillProcessesByName(firstTarget);
                            }
                            else
                            {
                                foreach (var p in processlist)
                                {
                                    ProcessHelper.TryKill(p.Pid, p.ProcessName);
                                }
                            }
                            Context.API.ReQuery();
                            return true;
                        }
                    });
                }
            }

            return sortedResults;
        }

        public Control CreateSettingPanel()
        {
            return new SettingsControl(_viewModel);
        }
    }
}
