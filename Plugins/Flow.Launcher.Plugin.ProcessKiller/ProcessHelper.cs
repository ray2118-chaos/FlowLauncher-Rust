using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Flow.Launcher.Infrastructure.Native;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal class ProcessEntry
    {
        public uint Pid { get; init; }
        public string Name { get; init; } = string.Empty;
        public string ExePath { get; init; } = string.Empty;
        public string WindowTitle { get; init; } = string.Empty;
        public uint MemoryMb { get; init; }

        public string GetProcessNameIdTitle() => $"{Name} - {Pid}";
    }

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
        /// Returns all running non-system processes via native ToolHelp snapshot with zero GC overhead
        /// </summary>
        public List<ProcessEntry> GetMatchingProcesses()
        {
            if (FlowNativeMethods.IsAvailable)
            {
                var items = new NativeProcessItem[1024];
                int count = FlowNativeMethods.GetProcesses(items, filterSystem: true);
                if (count > 0)
                {
                    var processlist = new List<ProcessEntry>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var name = items[i].GetName();
                        if (string.IsNullOrEmpty(name)) continue;

                        var cleanName = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                            ? name[..^4]
                            : name;

                        processlist.Add(new ProcessEntry
                        {
                            Pid = items[i].Pid,
                            Name = cleanName,
                            ExePath = items[i].GetExePath(),
                            WindowTitle = items[i].GetWindowTitle(),
                            MemoryMb = items[i].MemWorkingSetMb
                        });
                    }
                    return processlist;
                }
            }

            // Fallback for non-native environments
            var fallbackList = new List<ProcessEntry>();
            foreach (var p in Process.GetProcesses())
            {
                if (IsSystemProcessOrFlowLauncher(p)) continue;
                fallbackList.Add(new ProcessEntry
                {
                    Pid = (uint)p.Id,
                    Name = p.ProcessName,
                    ExePath = TryGetProcessFilename(p),
                    WindowTitle = p.MainWindowTitle,
                    MemoryMb = 0
                });
            }
            return fallbackList;
        }

        public static void TryKill(uint pid, string procName)
        {
            try
            {
                if (FlowNativeMethods.IsAvailable && FlowNativeMethods.KillProcess(pid))
                {
                    return;
                }

                using var p = Process.GetProcessById((int)pid);
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(50);
                }
            }
            catch (Exception e)
            {
                Main.Context.API.LogException(ClassName, $"Failed to kill process {procName} ({pid})", e);
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
