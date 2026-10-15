using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Infrastructure.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeProcessItem
    {
        public uint Pid;
        public fixed ushort Name[260];
        public uint NameLen;
        public fixed ushort WindowTitle[260];
        public uint WindowTitleLen;

        public string GetName()
        {
            fixed (ushort* ptr = Name)
            {
                return new string((char*)ptr, 0, (int)NameLen);
            }
        }

        public string GetWindowTitle()
        {
            if (WindowTitleLen == 0) return string.Empty;
            fixed (ushort* ptr = WindowTitle)
            {
                return new string((char*)ptr, 0, (int)WindowTitleLen);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeLnkInfo
    {
        public fixed ushort Target[512];
        public uint TargetLen;
        public fixed ushort Description[512];
        public uint DescriptionLen;
        public fixed ushort Arguments[512];
        public uint ArgumentsLen;

        public string GetTarget()
        {
            if (TargetLen == 0) return string.Empty;
            fixed (ushort* ptr = Target)
            {
                return new string((char*)ptr, 0, (int)TargetLen);
            }
        }

        public string GetDescription()
        {
            if (DescriptionLen == 0) return string.Empty;
            fixed (ushort* ptr = Description)
            {
                return new string((char*)ptr, 0, (int)DescriptionLen);
            }
        }

        public string GetArguments()
        {
            if (ArgumentsLen == 0) return string.Empty;
            fixed (ushort* ptr = Arguments)
            {
                return new string((char*)ptr, 0, (int)ArgumentsLen);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeFuzzyResult
    {
        public int Success;
        public int Score;
        public uint MatchCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeBookmarkItem
    {
        public fixed ushort Title[260];
        public uint TitleLen;
        public fixed ushort Url[1024];
        public uint UrlLen;

        public string GetTitle()
        {
            if (TitleLen == 0) return string.Empty;
            fixed (ushort* ptr = Title)
            {
                return new string((char*)ptr, 0, (int)TitleLen);
            }
        }

        public string GetUrl()
        {
            if (UrlLen == 0) return string.Empty;
            fixed (ushort* ptr = Url)
            {
                return new string((char*)ptr, 0, (int)UrlLen);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeFileItem
    {
        public fixed ushort Path[512];
        public uint PathLen;
        public uint IsDirectory;
        public ulong FileSize;
        public ulong ModifiedTime;

        public string GetPath()
        {
            if (PathLen == 0) return string.Empty;
            fixed (ushort* ptr = Path)
            {
                return new string((char*)ptr, 0, (int)PathLen);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeRegistryApp
    {
        public fixed ushort Path[512];
        public uint PathLen;

        public string GetPath()
        {
            if (PathLen == 0) return string.Empty;
            fixed (ushort* ptr = Path)
            {
                return new string((char*)ptr, 0, (int)PathLen);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativePeInfo
    {
        public fixed ushort Description[260];
        public uint DescriptionLen;
        public uint Subsystem;
        public uint Is64Bit;

        public string GetDescription()
        {
            if (DescriptionLen == 0) return string.Empty;
            fixed (ushort* ptr = Description)
            {
                return new string((char*)ptr, 0, (int)DescriptionLen);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeTargetItem
    {
        public ushort* Ptr;
        public nuint Len;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeMatchScore
    {
        public uint Index;
        public int Score;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeSystemGlance
    {
        public byte CpuUsagePct;
        public byte MemUsagePct;
        public byte BatteryPct;
        public byte IsCharging;
        public byte HasBattery;
        public uint TotalRamMb;
        public uint UsedRamMb;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeCalcQueryResult
    {
        public fixed ushort Title[260];
        public uint TitleLen;
        public fixed ushort Subtitle[260];
        public uint SubtitleLen;
        public fixed ushort CopyText[260];
        public uint CopyTextLen;
        public int IsCurrency;

        public string GetTitle()
        {
            if (TitleLen == 0) return string.Empty;
            fixed (ushort* ptr = Title)
            {
                return new string((char*)ptr, 0, (int)Math.Min(TitleLen, 259));
            }
        }

        public string GetSubtitle()
        {
            if (SubtitleLen == 0) return string.Empty;
            fixed (ushort* ptr = Subtitle)
            {
                return new string((char*)ptr, 0, (int)Math.Min(SubtitleLen, 259));
            }
        }

        public string GetCopyText()
        {
            if (CopyTextLen == 0) return string.Empty;
            fixed (ushort* ptr = CopyText)
            {
                return new string((char*)ptr, 0, (int)Math.Min(CopyTextLen, 259));
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeClipboardItem
    {
        public uint Id;
        public fixed ushort Preview[260];
        public uint PreviewLen;
        public uint CharCount;
        public ulong TimestampMs;

        public string GetPreview()
        {
            if (PreviewLen == 0) return string.Empty;
            fixed (ushort* ptr = Preview)
            {
                return new string((char*)ptr, 0, (int)Math.Min(PreviewLen, 259));
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeSysParsedCommand
    {
        public uint CmdType;
        public uint OpType;
        public int DeltaOrTarget;
        public fixed ushort Title[260];
        public uint TitleLen;
        public fixed ushort Subtitle[260];
        public uint SubtitleLen;

        public string GetTitle()
        {
            if (TitleLen == 0) return string.Empty;
            fixed (ushort* ptr = Title)
            {
                return new string((char*)ptr, 0, (int)Math.Min(TitleLen, 259));
            }
        }

        public string GetSubtitle()
        {
            if (SubtitleLen == 0) return string.Empty;
            fixed (ushort* ptr = Subtitle)
            {
                return new string((char*)ptr, 0, (int)Math.Min(SubtitleLen, 259));
            }
        }
    }

    public static unsafe class FlowNativeMethods
    {
        private const string DllName = "flow_launcher_native.dll";
        public static bool IsAvailable { get; }

        static FlowNativeMethods()
        {
            try
            {
                // Try resolving from base directory or assembly directory
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var dllPath = Path.Combine(baseDir, DllName);
                if (File.Exists(dllPath) && NativeLibrary.TryLoad(dllPath, out var handle))
                {
                    IsAvailable = flow_native_version() >= 100;
                    return;
                }

                if (NativeLibrary.TryLoad(DllName, typeof(FlowNativeMethods).Assembly, null, out handle))
                {
                    IsAvailable = flow_native_version() >= 100;
                    return;
                }

                IsAvailable = false;
            }
            catch
            {
                IsAvailable = false;
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint flow_native_version();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong flow_hash_xxh3_64(byte* data, nuint len);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_calc_eval(ushort* exprUtf16, nuint exprLen, double* outResult);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_calc_query(ushort* queryUtf16, nuint queryLen, NativeCalcQueryResult* outResult);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint flow_kill_processes_by_name(ushort* nameUtf16, nuint nameLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint flow_clipboard_add(ushort* textUtf16, nuint textLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_clipboard_search(ushort* queryUtf16, nuint queryLen, NativeClipboardItem* outItems, uint maxItems, uint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_clipboard_get_text(uint id, ushort* outBuf, nuint maxLen, nuint* outLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_clipboard_clear();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint flow_clipboard_count();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_telemetry_get_glance(NativeSystemGlance* outGlance);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_get_processes(NativeProcessItem* outItems, uint maxItems, uint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_sys_parse_command(ushort* queryPtr, nuint queryLen, NativeSysParsedCommand* outCmd);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_kill_process(uint pid);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_read_lnk(ushort* pathUtf16, nuint pathLen, NativeLnkInfo* outInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_fuzzy_match(
            ushort* queryPtr,
            nuint queryLen,
            ushort* targetPtr,
            nuint targetLen,
            int ignoreCase,
            int* outIndices,
            nuint maxIndices,
            NativeFuzzyResult* outResult);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_fuzzy_match_batch(
            ushort* queryPtr,
            nuint queryLen,
            NativeTargetItem* targetsPtr,
            nuint targetCount,
            int ignoreCase,
            int minScore,
            NativeMatchScore* outScores,
            nuint maxResults,
            nuint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_parse_chromium_bookmarks(
            ushort* pathPtr,
            nuint pathLen,
            NativeBookmarkItem* outItems,
            nuint maxItems,
            nuint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_scan_directory(
            ushort* dirPtr,
            nuint dirLen,
            ushort* searchPtr,
            nuint searchLen,
            int recursive,
            nuint maxResults,
            NativeFileItem* outItems,
            nuint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_scan_registry_app_paths(
            NativeRegistryApp* outItems,
            nuint maxItems,
            nuint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_extract_pe_info(
            ushort* pathPtr,
            nuint pathLen,
            NativePeInfo* outInfo);


        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_trim_working_set();

        // Native Animation Subsystem
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double flow_animation_eval_curve(uint curveType, double t);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double flow_animation_spring_step(
            double current,
            double target,
            double* velocity,
            double stiffness,
            double damping,
            double dt);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_animation_generate_spring_keyframes(
            double from,
            double to,
            uint durationMs,
            uint fps,
            double stiffness,
            double dampingRatio,
            double* outBuffer,
            nuint maxFrames,
            nuint* outCount);

        // Native GUI & Window Composition Subsystem
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_gui_set_window_blur(IntPtr hwnd, int enable, uint gradientColor);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_gui_set_window_corner_preference(IntPtr hwnd, uint cornerPreference);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_gui_set_window_backdrop(IntPtr hwnd, uint backdropType);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_gui_set_window_border_color(IntPtr hwnd, uint color);

        public static ulong HashXxh364(ReadOnlySpan<byte> data)
        {
            if (!IsAvailable || data.IsEmpty) return 0;
            fixed (byte* ptr = data)
            {
                return flow_hash_xxh3_64(ptr, (nuint)data.Length);
            }
        }

        public static bool EvaluateMath(ReadOnlySpan<char> expression, out double result)
        {
            result = 0;
            if (!IsAvailable || expression.IsEmpty) return false;
            fixed (char* ptr = expression)
            {
                double res = 0;
                var ok = flow_calc_eval((ushort*)ptr, (nuint)expression.Length, &res);
                if (ok != 0)
                {
                    result = res;
                    return true;
                }
            }
            return false;
        }

        public static bool CalculateQuery(ReadOnlySpan<char> query, out string title, out string subtitle, out string copyText, out bool isCurrency)
        {
            title = string.Empty;
            subtitle = string.Empty;
            copyText = string.Empty;
            isCurrency = false;
            if (!IsAvailable || query.IsEmpty) return false;

            fixed (char* ptr = query)
            {
                NativeCalcQueryResult res;
                if (flow_calc_query((ushort*)ptr, (nuint)query.Length, &res) != 0)
                {
                    title = res.GetTitle();
                    subtitle = res.GetSubtitle();
                    copyText = res.GetCopyText();
                    isCurrency = res.IsCurrency != 0;
                    return true;
                }
            }
            return false;
        }

        public static uint KillProcessesByName(string name)
        {
            if (!IsAvailable || string.IsNullOrEmpty(name)) return 0;
            fixed (char* ptr = name)
            {
                return flow_kill_processes_by_name((ushort*)ptr, (nuint)name.Length);
            }
        }

        public static bool ParseSysCommand(ReadOnlySpan<char> query, out NativeSysParsedCommand cmd)
        {
            cmd = default;
            if (!IsAvailable || query.IsEmpty) return false;

            fixed (char* ptr = query)
            {
                NativeSysParsedCommand result = default;
                var ok = flow_sys_parse_command((ushort*)ptr, (nuint)query.Length, &result);
                if (ok != 0)
                {
                    cmd = result;
                    return true;
                }
            }
            return false;
        }

        public static uint AddClipboardHistory(string text)
        {
            if (!IsAvailable || string.IsNullOrEmpty(text)) return 0;
            fixed (char* ptr = text)
            {
                return flow_clipboard_add((ushort*)ptr, (nuint)text.Length);
            }
        }

        public static List<NativeClipboardItem> SearchClipboardHistory(string query, int maxItems = 50)
        {
            var results = new List<NativeClipboardItem>();
            if (!IsAvailable) return results;

            var items = new NativeClipboardItem[maxItems];
            fixed (NativeClipboardItem* outItems = items)
            {
                uint count = 0;
                if (string.IsNullOrEmpty(query))
                {
                    if (flow_clipboard_search(null, 0, outItems, (uint)maxItems, &count) != 0)
                    {
                        for (int i = 0; i < count; i++) results.Add(items[i]);
                    }
                }
                else
                {
                    fixed (char* qPtr = query)
                    {
                        if (flow_clipboard_search((ushort*)qPtr, (nuint)query.Length, outItems, (uint)maxItems, &count) != 0)
                        {
                            for (int i = 0; i < count; i++) results.Add(items[i]);
                        }
                    }
                }
            }
            return results;
        }

        public static string GetClipboardHistoryText(uint id)
        {
            if (!IsAvailable || id == 0) return string.Empty;
            var buffer = new char[8192];
            fixed (char* ptr = buffer)
            {
                nuint outLen = 0;
                if (flow_clipboard_get_text(id, (ushort*)ptr, (nuint)buffer.Length, &outLen) != 0)
                {
                    return new string(ptr, 0, (int)outLen);
                }
            }
            return string.Empty;
        }

        public static bool GetClipboardText(uint id, out string text)
        {
            text = GetClipboardHistoryText(id);
            return !string.IsNullOrEmpty(text);
        }

        public static void ClearClipboardHistory()
        {
            if (IsAvailable) flow_clipboard_clear();
        }

        public static bool GetSystemGlance(out NativeSystemGlance glance)
        {
            glance = default;
            if (!IsAvailable) return false;
            fixed (NativeSystemGlance* ptr = &glance)
            {
                return flow_telemetry_get_glance(ptr) != 0;
            }
        }

        public static int GetProcesses(Span<NativeProcessItem> buffer)
        {
            if (!IsAvailable || buffer.IsEmpty) return 0;
            fixed (NativeProcessItem* ptr = buffer)
            {
                uint count = 0;
                var ok = flow_get_processes(ptr, (uint)buffer.Length, &count);
                return ok != 0 ? (int)count : 0;
            }
        }

        public static bool KillProcess(uint pid)
        {
            if (!IsAvailable) return false;
            return flow_kill_process(pid) != 0;
        }

        public static bool ReadLnk(string path, out string target, out string description, out string arguments)
        {
            target = string.Empty;
            description = string.Empty;
            arguments = string.Empty;

            if (!IsAvailable || string.IsNullOrEmpty(path)) return false;

            fixed (char* ptr = path)
            {
                NativeLnkInfo info = default;
                var ok = flow_read_lnk((ushort*)ptr, (nuint)path.Length, &info);
                if (ok != 0)
                {
                    target = info.GetTarget();
                    description = info.GetDescription();
                    arguments = info.GetArguments();
                    return true;
                }
            }
            return false;
        }

        public static bool FuzzyMatch(
            ReadOnlySpan<char> query,
            ReadOnlySpan<char> target,
            bool ignoreCase,
            Span<int> matchedIndices,
            out int score,
            out int matchCount)
        {
            score = 0;
            matchCount = 0;
            if (!IsAvailable || query.IsEmpty || target.IsEmpty) return false;

            fixed (char* qPtr = query)
            fixed (char* tPtr = target)
            fixed (int* idxPtr = matchedIndices)
            {
                NativeFuzzyResult res = default;
                var ok = flow_fuzzy_match(
                    (ushort*)qPtr,
                    (nuint)query.Length,
                    (ushort*)tPtr,
                    (nuint)target.Length,
                    ignoreCase ? 1 : 0,
                    idxPtr,
                    (nuint)matchedIndices.Length,
                    &res);

                if (ok != 0 && res.Success != 0)
                {
                    score = res.Score;
                    matchCount = (int)res.MatchCount;
                    return true;
                }
            }
            return false;
        }

        public static bool ParseChromiumBookmarks(string path, Span<NativeBookmarkItem> buffer, out int count)
        {
            count = 0;
            if (!IsAvailable || string.IsNullOrEmpty(path) || buffer.IsEmpty) return false;

            fixed (char* ptr = path)
            fixed (NativeBookmarkItem* outPtr = buffer)
            {
                nuint outCount = 0;
                var ok = flow_parse_chromium_bookmarks((ushort*)ptr, (nuint)path.Length, outPtr, (nuint)buffer.Length, &outCount);
                if (ok != 0)
                {
                    count = (int)outCount;
                    return true;
                }
            }
            return false;
        }

        public static bool ScanDirectory(string dirPath, string search, bool recursive, Span<NativeFileItem> buffer, out int count)
        {
            count = 0;
            if (!IsAvailable || string.IsNullOrEmpty(dirPath) || buffer.IsEmpty) return false;

            fixed (char* dirPtr = dirPath)
            fixed (char* sPtr = search)
            fixed (NativeFileItem* outPtr = buffer)
            {
                nuint outCount = 0;
                var ok = flow_scan_directory(
                    (ushort*)dirPtr,
                    (nuint)dirPath.Length,
                    string.IsNullOrEmpty(search) ? null : (ushort*)sPtr,
                    string.IsNullOrEmpty(search) ? 0 : (nuint)search.Length,
                    recursive ? 1 : 0,
                    (nuint)buffer.Length,
                    outPtr,
                    &outCount);

                if (ok != 0)
                {
                    count = (int)outCount;
                    return true;
                }
            }
            return false;
        }

        public static bool ScanRegistryAppPaths(Span<NativeRegistryApp> buffer, out int count)
        {
            count = 0;
            if (!IsAvailable || buffer.IsEmpty) return false;

            fixed (NativeRegistryApp* outPtr = buffer)
            {
                nuint outCount = 0;
                var ok = flow_scan_registry_app_paths(outPtr, (nuint)buffer.Length, &outCount);
                if (ok != 0)
                {
                    count = (int)outCount;
                    return true;
                }
            }
            return false;
        }

        public static bool ExtractPeInfo(string path, out string description, out uint subsystem, out bool is64Bit)
        {
            description = string.Empty;
            subsystem = 0;
            is64Bit = false;

            if (!IsAvailable || string.IsNullOrEmpty(path)) return false;

            fixed (char* ptr = path)
            {
                NativePeInfo info = default;
                var ok = flow_extract_pe_info((ushort*)ptr, (nuint)path.Length, &info);
                if (ok != 0)
                {
                    description = info.GetDescription();
                    subsystem = info.Subsystem;
                    is64Bit = info.Is64Bit != 0;
                    return true;
                }
            }
            return false;
        }


        public static bool TrimWorkingSet()
        {
            if (!IsAvailable) return false;
            return flow_trim_working_set() != 0;
        }

        #region Native Animation & Physics

        public static double EvaluateAnimationCurve(uint curveType, double t)
        {
            if (!IsAvailable)
            {
                // Fallback curve evaluation in C# if native DLL is unavailable
                t = Math.Clamp(t, 0.0, 1.0);
                return curveType switch
                {
                    0 => t < 0.5 ? 4.0 * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 3) / 2.0,
                    1 => 1.0 - Math.Pow(1.0 - t, 5),
                    2 => Math.Abs(t - 1.0) < 0.0001 ? 1.0 : 1.0 - Math.Pow(2.0, -10.0 * t),
                    _ => t
                };
            }
            return flow_animation_eval_curve(curveType, t);
        }

        public static double StepSpringAnimation(double current, double target, ref double velocity, double stiffness, double damping, double dt)
        {
            if (!IsAvailable) return target;
            fixed (double* vPtr = &velocity)
            {
                return flow_animation_spring_step(current, target, vPtr, stiffness, damping, dt);
            }
        }

        public static bool GenerateSpringKeyframes(double from, double to, uint durationMs, uint fps, double stiffness, double dampingRatio, Span<double> outFrames, out int count)
        {
            count = 0;
            if (!IsAvailable || outFrames.IsEmpty) return false;
            fixed (double* bufPtr = outFrames)
            {
                nuint outC = 0;
                int res = flow_animation_generate_spring_keyframes(from, to, durationMs, fps, stiffness, dampingRatio, bufPtr, (nuint)outFrames.Length, &outC);
                if (res != 0)
                {
                    count = (int)outC;
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Native GUI & Window Composition

        public static bool SetWindowBlur(IntPtr hwnd, bool enable, uint gradientColor = 0)
        {
            if (!IsAvailable || hwnd == IntPtr.Zero) return false;
            return flow_gui_set_window_blur(hwnd, enable ? 1 : 0, gradientColor) != 0;
        }

        public static bool SetWindowCornerPreference(IntPtr hwnd, uint preference)
        {
            if (!IsAvailable || hwnd == IntPtr.Zero) return false;
            return flow_gui_set_window_corner_preference(hwnd, preference) != 0;
        }

        public static bool SetWindowBackdrop(IntPtr hwnd, uint backdropType)
        {
            if (!IsAvailable || hwnd == IntPtr.Zero) return false;
            return flow_gui_set_window_backdrop(hwnd, backdropType) != 0;
        }

        public static bool SetWindowBorderColor(IntPtr hwnd, uint color)
        {
            if (!IsAvailable || hwnd == IntPtr.Zero) return false;
            return flow_gui_set_window_border_color(hwnd, color) != 0;
        }

        #endregion
    }
}
