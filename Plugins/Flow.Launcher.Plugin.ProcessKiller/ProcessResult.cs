using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal class ProcessResult
    {
        public uint Pid { get; init; }
        public string ProcessName { get; init; } = string.Empty;
        public string ExePath { get; init; } = string.Empty;
        public string WindowTitle { get; init; } = string.Empty;
        public uint MemoryMb { get; init; }
        public int Score { get; init; }
        public string Title { get; init; } = string.Empty;
        public MatchResult TitleMatch { get; init; }
        public string Tooltip { get; init; } = string.Empty;
    }
}
