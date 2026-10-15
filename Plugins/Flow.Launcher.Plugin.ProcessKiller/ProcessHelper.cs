using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Flow.Launcher.Infrastructure.Native;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal class ProcessHelper
    {
        private static readonly string ClassName = nameof(ProcessHelper);

        private readonly HashSet<string> _systemProcessList =
        [
            "conhost",
            "svchost",
            "idle",
            "system",
            "rundll32",
            "csrss",
            "lsass",
            "lsm",
            "smss",
            "wininit",
            "winlogon",
            "services",
            "spoolsv",
            "explorer"
        ];

        private const string FlowLauncherProcessName = "Flow.Launcher";

        private bool IsSystemProcessOrFlowLauncher(Process p) =>
            _systemProcessList.Contains(p.ProcessName.ToLower()) ||
            string.Equals(p.ProcessName, FlowLauncherProcessName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Get title based on process name and id
        /// </summary>
        public static string GetProcessNameIdTitle(Process p)
        {
            var sb = new StringBuilder();
            sb.Append(p.ProcessName);
            sb.Append(" - ");
            sb.Append(p.Id);
            return sb.ToString();
        }

        /// <summary>
        /// Returns a Process for every running non-system process via native ToolHelp snapshot
        /// </summary>
        public List<Process> GetMatchingProcesses()
        {
            if (FlowNativeMethods.IsAvailable)
            {
                var items = new NativeProcessItem[1024];
                int count = FlowNativeMethods.GetProcesses(items);
                if (count > 0)
                {
                    var processlist = new List<Process>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var name = items[i].GetName();
                        if (string.IsNullOrEmpty(name)) continue;
                        
                        var cleanName = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) 
                            ? name[..^4] 
                            : name;

                        if (_systemProcessList.Contains(cleanName.ToLower()) ||
                            string.Equals(cleanName, FlowLauncherProcessName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        try
                        {
                            processlist.Add(Process.GetProcessById((int)items[i].Pid));
                        }
                        catch
                        {
                            // Process may have exited
                        }
                    }
                    return processlist;
                }
            }

            var fallbackList = new List<Process>();
            foreach (var p in Process.GetProcesses())
            {
                if (IsSystemProcessOrFlowLauncher(p)) continue;
                fallbackList.Add(p);
            }
            return fallbackList;
        }

        /// <summary>
        /// Returns a dictionary of process IDs and their window titles via native ToolHelp/EnumWindows snapshot
        /// </summary>
        public static Dictionary<int, string> GetProcessesWithNonEmptyWindowTitle()
        {
            if (FlowNativeMethods.IsAvailable)
            {
                var items = new NativeProcessItem[1024];
                int count = FlowNativeMethods.GetProcesses(items);
                if (count > 0)
                {
                    var dict = new Dictionary<int, string>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var title = items[i].GetWindowTitle();
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            dict[(int)items[i].Pid] = title;
                        }
                    }
                    return dict;
                }
            }

            return [];
        }

        /// <summary>
        /// Returns all non-system processes whose file path matches the given processPath
        /// </summary>
        public IEnumerable<Process> GetSimilarProcesses(string processPath)
        {
            return GetMatchingProcesses().Where(p => TryGetProcessFilename(p) == processPath);
        }

        public static void TryKill(Process p)
        {
            try
            {
                if (FlowNativeMethods.IsAvailable && FlowNativeMethods.KillProcess((uint)p.Id))
                {
                    return;
                }

                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(50);
                }
            }
            catch (Exception e)
            {
                Main.Context.API.LogException(ClassName, $"Failed to kill process {p.ProcessName}", e);
            }
        }

        public static string TryGetProcessFilename(Process p)
        {
            try
            {
                return p.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
