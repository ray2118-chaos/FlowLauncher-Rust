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
        public fixed ushort ExePath[512];
        public uint ExePathLen;
        public fixed ushort WindowTitle[260];
        public uint WindowTitleLen;
        public uint MemWorkingSetMb;

        public string GetName()
        {
            if (NameLen == 0) return string.Empty;
            fixed (ushort* ptr = Name)
            {
                return new string((char*)ptr, 0, (int)Math.Min(NameLen, 259));
            }
        }

        public string GetExePath()
        {
            if (ExePathLen == 0) return string.Empty;
            fixed (ushort* ptr = ExePath)
            {
                return new string((char*)ptr, 0, (int)Math.Min(ExePathLen, 511));
            }
        }

        public string GetWindowTitle()
        {
            if (WindowTitleLen == 0) return string.Empty;
            fixed (ushort* ptr = WindowTitle)
            {
                return new string((char*)ptr, 0, (int)Math.Min(WindowTitleLen, 259));
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
    public unsafe struct NativeShellEntry
    {
        public fixed ushort Name[260];
        public uint NameLen;
        public int IsDirectory;

        public string GetName()
        {
            if (NameLen == 0) return string.Empty;
            fixed (ushort* ptr = Name)
            {
                return new string((char*)ptr, 0, (int)Math.Min(NameLen, 259));
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeHighlightRange
    {
        public int Start;
        public int Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeFileMeta
    {
        public ulong FileSize;
        public ulong CreatedTimeMs;
        public ulong ModifiedTimeMs;
        public int IsDirectory;
        public int IsReadOnly;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeColorPalette
    {
        public uint Primary;
        public uint Accent;
        public uint Background;
        public uint TextPrimary;
        public int IsDark;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeJsonRpcItem
    {
        public fixed ushort Title[260];
        public uint TitleLen;
        public fixed ushort Subtitle[260];
        public uint SubtitleLen;
        public fixed ushort IconPath[260];
        public uint IconPathLen;
        public int Score;

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

        public string GetIconPath()
        {
            if (IconPathLen == 0) return string.Empty;
            fixed (ushort* ptr = IconPath)
            {
                return new string((char*)ptr, 0, (int)Math.Min(IconPathLen, 259));
            }
        }
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
    public struct NativeBookmarkMatch
    {
        public uint Id;
        public int Score;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeUwpAppInfo
    {
        public fixed ushort AppId[128];
        public uint AppIdLen;
        public fixed ushort LogoUri[260];
        public uint LogoUriLen;
        public fixed ushort PreviewUri[260];
        public uint PreviewUriLen;
        public int CanElevate;

        public string GetAppId()
        {
            if (AppIdLen == 0) return string.Empty;
            fixed (ushort* ptr = AppId)
            {
                return new string((char*)ptr, 0, (int)Math.Min(AppIdLen, 127));
            }
        }

        public string GetLogoUri()
        {
            if (LogoUriLen == 0) return string.Empty;
            fixed (ushort* ptr = LogoUri)
            {
                return new string((char*)ptr, 0, (int)Math.Min(LogoUriLen, 259));
            }
        }

        public string GetPreviewUri()
        {
            if (PreviewUriLen == 0) return string.Empty;
            fixed (ushort* ptr = PreviewUri)
            {
                return new string((char*)ptr, 0, (int)Math.Min(PreviewUriLen, 259));
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeTextMetrics
    {
        public float Width;
        public float Height;
        public uint LineCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeQueryTerm
    {
        public uint Start;
        public uint Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeParsedQuery
    {
        public int IsHomeQuery;
        public int HasActionKeyword;
        public uint SearchOffset;
        public uint SearchLength;
        public uint TermCount;
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
    public struct NativeSettingMatch
    {
        public uint Id;
        public int Score;
        public uint MatchType; // 0 = Name, 1 = Area, 2 = Alt
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeIndexedProgram
    {
        public fixed ushort Name[260];
        public uint NameLen;
        public fixed ushort FullPath[512];
        public uint FullPathLen;
        public fixed ushort TargetPath[512];
        public uint TargetPathLen;
        public fixed ushort Args[260];
        public uint ArgsLen;
        public fixed ushort Description[260];
        public uint DescriptionLen;
        public uint ProgramType; // 0 = Exe, 1 = Lnk, 2 = Url, 3 = AppPaths

        public string GetName()
        {
            if (NameLen == 0) return string.Empty;
            fixed (ushort* ptr = Name)
            {
                return new string((char*)ptr, 0, (int)Math.Min(NameLen, 259));
            }
        }

        public string GetFullPath()
        {
            if (FullPathLen == 0) return string.Empty;
            fixed (ushort* ptr = FullPath)
            {
                return new string((char*)ptr, 0, (int)Math.Min(FullPathLen, 511));
            }
        }

        public string GetTargetPath()
        {
            if (TargetPathLen == 0) return string.Empty;
            fixed (ushort* ptr = TargetPath)
            {
                return new string((char*)ptr, 0, (int)Math.Min(TargetPathLen, 511));
            }
        }

        public string GetArgs()
        {
            if (ArgsLen == 0) return string.Empty;
            fixed (ushort* ptr = Args)
            {
                return new string((char*)ptr, 0, (int)Math.Min(ArgsLen, 259));
            }
        }

        public string GetDescription()
        {
            if (DescriptionLen == 0) return string.Empty;
            fixed (ushort* ptr = Description)
            {
                return new string((char*)ptr, 0, (int)Math.Min(DescriptionLen, 259));
            }
        }
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
        private static extern int flow_get_processes(NativeProcessItem* outItems, uint maxItems, int filterSystem, uint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_sys_parse_command(ushort* queryPtr, nuint queryLen, NativeSysParsedCommand* outCmd);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_kill_process(uint pid);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_read_lnk(ushort* pathUtf16, nuint pathLen, NativeLnkInfo* outInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_clear_windows_settings();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_register_windows_setting(
            uint id,
            ushort* namePtr,
            nuint nameLen,
            ushort* areaPtr,
            nuint areaLen,
            ushort* altPtr,
            nuint altLen,
            uint appType);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_search_windows_settings(
            ushort* queryPtr,
            nuint queryLen,
            NativeSettingMatch* outMatches,
            nuint maxMatches,
            nuint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_scan_programs_in_dirs(
            ushort** dirPathsUtf16,
            nuint* dirLens,
            nuint dirCount,
            ushort** suffixesUtf16,
            nuint* suffixLens,
            nuint suffixCount,
            NativeIndexedProgram* outItems,
            nuint maxItems,
            nuint* outCount);

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
        private static extern int flow_parse_firefox_places(
            ushort* pathPtr,
            nuint pathLen,
            NativeBookmarkItem* outItems,
            nuint maxItems,
            nuint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int flow_sys_execute_command(uint actionId);


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

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_emoji_score_full(
            ushort* glyphPtr,
            nuint glyphLen,
            ushort* namePtr,
            nuint nameLen,
            ushort* queryPtr,
            nuint queryLen,
            ushort* kwPtr,
            nuint kwLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_url_validate_utf16(
            ushort* urlPtr,
            nuint urlLen,
            int* outSchemeType);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_shell_expand_env_utf16(
            ushort* srcPtr,
            nuint srcLen,
            ushort* dstBuf,
            nuint dstCap,
            nuint* outLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_shell_autocomplete_dir(
            ushort* dirPtr,
            nuint dirLen,
            ushort* prefixPtr,
            nuint prefixLen,
            NativeShellEntry* outEntries,
            nuint maxEntries,
            nuint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_gui_get_accent_color(uint* outColor);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_gui_extract_palette_rgba(byte* rgbaPtr, nuint pixelCount, NativeColorPalette* outPalette);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_rank_indices(int* scoresPtr, nuint count, int* outIndices);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_everything_build_query_utf16(
            ushort* keywordPtr,
            nuint keywordLen,
            ushort* parentPtr,
            nuint parentLen,
            int isRecursive,
            ushort* contentPtr,
            nuint contentLen,
            ushort* outBuf,
            nuint outCap,
            nuint* outLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_everything_parse_highlights_utf16(
            ushort* highlightPtr,
            nuint highlightLen,
            NativeHighlightRange* outRanges,
            nuint maxRanges,
            nuint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_preview_get_meta_utf16(
            ushort* pathPtr,
            nuint pathLen,
            NativeFileMeta* outMeta);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_preview_format_size_utf16(
            ulong bytes,
            ushort* outBuf,
            nuint outCap,
            nuint* outLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_preview_read_text_head_utf16(
            ushort* pathPtr,
            nuint pathLen,
            nuint maxBytes,
            ushort* outBuf,
            nuint outCap,
            nuint* outLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_jsonrpc_parse_response_utf8(
            byte* jsonPtr,
            nuint jsonLen,
            NativeJsonRpcItem* outItems,
            nuint maxItems,
            nuint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_uwp_parse_manifest_utf16(
            ushort* manifestPathPtr,
            nuint manifestPathLen,
            NativeUwpAppInfo* outApps,
            nuint maxApps,
            nuint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_bookmarks_clear();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_bookmarks_register(
            uint id,
            ushort* titlePtr,
            nuint titleLen,
            ushort* urlPtr,
            nuint urlLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_bookmarks_search(
            ushort* queryPtr,
            nuint queryLen,
            NativeBookmarkMatch* outMatches,
            nuint maxMatches,
            nuint* outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_gui_measure_text_utf16(
            ushort* textPtr,
            nuint textLen,
            ushort* fontNamePtr,
            nuint fontNameLen,
            float fontSize,
            int fontWeight,
            int isItalic,
            NativeTextMetrics* outMetrics);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_lz4_compress(
            byte* srcPtr,
            nuint srcLen,
            byte* dstBuf,
            nuint dstCap,
            nuint* outLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_lz4_decompress(
            byte* srcPtr,
            nuint srcLen,
            byte* dstBuf,
            nuint dstCap,
            nuint* outLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe IntPtr flow_fs_watcher_create(
            ushort* dirPathPtr,
            nuint dirPathLen,
            int watchSubtree);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_fs_watcher_check(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_fs_watcher_close(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_query_tokenize_utf16(
            ushort* queryPtr,
            nuint queryLen,
            ushort* actionKeywordPtr,
            nuint actionKeywordLen,
            NativeQueryTerm* outTerms,
            nuint maxTerms,
            NativeParsedQuery* outParsed);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_history_hash_utf16(
            ushort* strPtr,
            nuint strLen,
            int initial);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_history_clear();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_history_record_add(int keyWithQuery, int keyWithoutQuery);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int flow_history_get_count(int keyWithQuery, int keyWithoutQuery);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe nuint flow_history_record_count();

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

        public static int GetProcesses(Span<NativeProcessItem> buffer, bool filterSystem = true)
        {
            if (!IsAvailable || buffer.IsEmpty) return 0;
            fixed (NativeProcessItem* ptr = buffer)
            {
                uint count = 0;
                var ok = flow_get_processes(ptr, (uint)buffer.Length, filterSystem ? 1 : 0, &count);
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

        public static bool ParseFirefoxBookmarks(string path, Span<NativeBookmarkItem> buffer, out int count)
        {
            count = 0;
            if (!IsAvailable || string.IsNullOrEmpty(path) || buffer.IsEmpty) return false;

            fixed (char* ptr = path)
            fixed (NativeBookmarkItem* outPtr = buffer)
            {
                nuint outCount = 0;
                var ok = flow_parse_firefox_places((ushort*)ptr, (nuint)path.Length, outPtr, (nuint)buffer.Length, &outCount);
                if (ok != 0)
                {
                    count = (int)outCount;
                    return true;
                }
            }
            return false;
        }

        public const uint SYS_ACTION_SHUTDOWN = 1;
        public const uint SYS_ACTION_RESTART = 2;
        public const uint SYS_ACTION_RESTART_ADVANCED = 3;
        public const uint SYS_ACTION_LOGOFF = 4;
        public const uint SYS_ACTION_LOCK = 5;
        public const uint SYS_ACTION_SLEEP = 6;
        public const uint SYS_ACTION_HIBERNATE = 7;
        public const uint SYS_ACTION_EMPTY_RECYCLE_BIN = 8;

        public static bool ExecuteSystemCommand(uint actionId)
        {
            if (!IsAvailable) return false;
            return flow_sys_execute_command(actionId) != 0;
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

        #region Native Windows Settings

        public static void ClearWindowsSettings()
        {
            if (!IsAvailable) return;
            flow_clear_windows_settings();
        }

        public static bool RegisterWindowsSetting(
            uint id,
            string name,
            string area,
            string altNames,
            uint appType)
        {
            if (!IsAvailable) return false;

            fixed (char* namePtr = name)
            fixed (char* areaPtr = area)
            fixed (char* altPtr = altNames)
            {
                return flow_register_windows_setting(
                    id,
                    (ushort*)namePtr,
                    (nuint)(name?.Length ?? 0),
                    (ushort*)areaPtr,
                    (nuint)(area?.Length ?? 0),
                    (ushort*)altPtr,
                    (nuint)(altNames?.Length ?? 0),
                    appType) != 0;
            }
        }

        public static int SearchWindowsSettings(string query, Span<NativeSettingMatch> outMatches)
        {
            if (!IsAvailable || string.IsNullOrEmpty(query) || outMatches.IsEmpty) return 0;

            fixed (char* qPtr = query)
            fixed (NativeSettingMatch* mPtr = outMatches)
            {
                nuint count = 0;
                var ok = flow_search_windows_settings(
                    (ushort*)qPtr,
                    (nuint)query.Length,
                    mPtr,
                    (nuint)outMatches.Length,
                    &count);

                return ok != 0 ? (int)count : 0;
            }
        }

        #endregion

        #region Native Program Indexer

        public static int ScanProgramsInDirs(
            IReadOnlyList<string> directories,
            IReadOnlyList<string> suffixes,
            Span<NativeIndexedProgram> outBuffer)
        {
            if (!IsAvailable || directories == null || directories.Count == 0 || outBuffer.IsEmpty)
                return 0;

            var dirHandles = new GCHandle[directories.Count];
            ushort** dirPointers = stackalloc ushort*[directories.Count];
            nuint* dirLens = stackalloc nuint[directories.Count];

            for (int i = 0; i < directories.Count; i++)
            {
                var s = directories[i];
                dirHandles[i] = GCHandle.Alloc(s, GCHandleType.Pinned);
                dirPointers[i] = (ushort*)dirHandles[i].AddrOfPinnedObject();
                dirLens[i] = (nuint)s.Length;
            }

            int sufCount = suffixes?.Count ?? 0;
            var sufHandles = sufCount > 0 ? new GCHandle[sufCount] : null;
            ushort** sufPointers = stackalloc ushort*[Math.Max(sufCount, 1)];
            nuint* sufLens = stackalloc nuint[Math.Max(sufCount, 1)];

            if (sufCount > 0)
            {
                for (int i = 0; i < sufCount; i++)
                {
                    var s = suffixes![i];
                    sufHandles![i] = GCHandle.Alloc(s, GCHandleType.Pinned);
                    sufPointers[i] = (ushort*)sufHandles[i].AddrOfPinnedObject();
                    sufLens[i] = (nuint)s.Length;
                }
            }

            try
            {
                fixed (NativeIndexedProgram* pOut = outBuffer)
                {
                    nuint count = 0;
                    var ok = flow_scan_programs_in_dirs(
                        dirPointers,
                        dirLens,
                        (nuint)directories.Count,
                        sufPointers,
                        sufLens,
                        (nuint)sufCount,
                        pOut,
                        (nuint)outBuffer.Length,
                        &count);

                    return ok != 0 ? (int)count : 0;
                }
            }
            finally
            {
                for (int i = 0; i < dirHandles.Length; i++)
                {
                    if (dirHandles[i].IsAllocated) dirHandles[i].Free();
                }
                if (sufHandles != null)
                {
                    for (int i = 0; i < sufHandles.Length; i++)
                    {
                        if (sufHandles[i].IsAllocated) sufHandles[i].Free();
                    }
                }
            }
        }

        #endregion

        #region Native Emoji

        public static unsafe int ScoreEmoji(
            ReadOnlySpan<char> glyph,
            ReadOnlySpan<char> name,
            ReadOnlySpan<char> query,
            ReadOnlySpan<char> keywordsJoined)
        {
            if (!IsAvailable || query.IsEmpty) return 0;

            fixed (char* gPtr = glyph)
            fixed (char* nPtr = name)
            fixed (char* qPtr = query)
            fixed (char* kwPtr = keywordsJoined)
            {
                return flow_emoji_score_full(
                    (ushort*)gPtr, (nuint)glyph.Length,
                    (ushort*)nPtr, (nuint)name.Length,
                    (ushort*)qPtr, (nuint)query.Length,
                    (ushort*)kwPtr, (nuint)keywordsJoined.Length);
            }
        }

        #endregion

        #region Native URL

        public static unsafe bool ValidateUrl(ReadOnlySpan<char> url, out int schemeType)
        {
            schemeType = 0;
            if (!IsAvailable || url.IsEmpty) return false;

            fixed (char* uPtr = url)
            {
                int st = 0;
                var res = flow_url_validate_utf16((ushort*)uPtr, (nuint)url.Length, &st);
                schemeType = st;
                return res != 0;
            }
        }

        #endregion

        #region Native Shell

        public static unsafe string ExpandEnvironmentVariables(string src)
        {
            if (!IsAvailable || string.IsNullOrEmpty(src))
                return Environment.ExpandEnvironmentVariables(src);

            Span<char> buf = stackalloc char[1024];
            fixed (char* sPtr = src)
            fixed (char* dPtr = buf)
            {
                nuint outLen = 0;
                var ok = flow_shell_expand_env_utf16((ushort*)sPtr, (nuint)src.Length, (ushort*)dPtr, (nuint)buf.Length, &outLen);
                if (ok != 0 && outLen > 0)
                {
                    return new string(dPtr, 0, (int)outLen);
                }
            }

            return Environment.ExpandEnvironmentVariables(src);
        }

        public static unsafe int AutocompleteDirectory(
            string dir,
            string prefix,
            Span<NativeShellEntry> outEntries)
        {
            if (!IsAvailable || string.IsNullOrEmpty(dir) || outEntries.IsEmpty)
                return 0;

            fixed (char* dPtr = dir)
            fixed (char* pPtr = prefix)
            fixed (NativeShellEntry* ePtr = outEntries)
            {
                nuint count = 0;
                var ok = flow_shell_autocomplete_dir(
                    (ushort*)dPtr, (nuint)dir.Length,
                    pPtr != null ? (ushort*)pPtr : null, (nuint)(prefix?.Length ?? 0),
                    ePtr, (nuint)outEntries.Length,
                    &count);

                return ok != 0 ? (int)count : 0;
            }
        }

        #endregion

        #region Native GUI & Theme

        public static unsafe bool GetAccentColor(out uint color)
        {
            color = 0;
            if (!IsAvailable) return false;
            uint c = 0;
            var ok = flow_gui_get_accent_color(&c);
            if (ok != 0)
            {
                color = c;
                return true;
            }
            return false;
        }

        public static unsafe bool ExtractPalette(ReadOnlySpan<byte> rgbaBytes, int pixelCount, out NativeColorPalette palette)
        {
            palette = default;
            if (!IsAvailable || rgbaBytes.IsEmpty || pixelCount <= 0) return false;
            fixed (byte* ptr = rgbaBytes)
            {
                NativeColorPalette p = default;
                var ok = flow_gui_extract_palette_rgba(ptr, (nuint)pixelCount, &p);
                if (ok != 0)
                {
                    palette = p;
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Native Result Ranking

        public static unsafe bool RankIndices(ReadOnlySpan<int> scores, Span<int> outIndices)
        {
            if (!IsAvailable || scores.IsEmpty || outIndices.Length < scores.Length) return false;
            fixed (int* sPtr = scores)
            fixed (int* oPtr = outIndices)
            {
                return flow_rank_indices(sPtr, (nuint)scores.Length, oPtr) != 0;
            }
        }

        #endregion

        #region Native Everything IPC

        public static unsafe string BuildEverythingQuery(string keyword, string parent, bool isRecursive, string content = null)
        {
            if (!IsAvailable)
            {
                var q = keyword ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    if (!string.IsNullOrEmpty(q)) q += " ";
                    if (!isRecursive) q += "parent:";
                    q += $"\"{parent}\"";
                }
                if (!string.IsNullOrWhiteSpace(content))
                {
                    if (!string.IsNullOrEmpty(q)) q += " ";
                    q += $"content:\"{content}\"";
                }
                return q;
            }

            Span<char> buf = stackalloc char[1024];
            fixed (char* kPtr = keyword)
            fixed (char* pPtr = parent)
            fixed (char* cPtr = content)
            fixed (char* outPtr = buf)
            {
                nuint outLen = 0;
                var ok = flow_everything_build_query_utf16(
                    kPtr != null ? (ushort*)kPtr : null, (nuint)(keyword?.Length ?? 0),
                    pPtr != null ? (ushort*)pPtr : null, (nuint)(parent?.Length ?? 0),
                    isRecursive ? 1 : 0,
                    cPtr != null ? (ushort*)cPtr : null, (nuint)(content?.Length ?? 0),
                    (ushort*)outPtr, (nuint)buf.Length,
                    &outLen);

                if (ok != 0 && outLen > 0)
                {
                    return new string(outPtr, 0, (int)outLen);
                }
            }
            return keyword ?? string.Empty;
        }

        public static unsafe int ParseEverythingHighlights(string highlightedText, Span<NativeHighlightRange> outRanges)
        {
            if (!IsAvailable || string.IsNullOrEmpty(highlightedText) || outRanges.IsEmpty) return 0;
            fixed (char* hPtr = highlightedText)
            fixed (NativeHighlightRange* rPtr = outRanges)
            {
                nuint count = 0;
                var ok = flow_everything_parse_highlights_utf16(
                    (ushort*)hPtr, (nuint)highlightedText.Length,
                    rPtr, (nuint)outRanges.Length,
                    &count);
                return ok != 0 ? (int)count : 0;
            }
        }

        #endregion

        #region Native File Preview & Metadata

        public static unsafe bool GetFileMeta(string filePath, out NativeFileMeta meta)
        {
            meta = default;
            if (!IsAvailable || string.IsNullOrEmpty(filePath)) return false;
            fixed (char* pPtr = filePath)
            {
                NativeFileMeta m = default;
                var ok = flow_preview_get_meta_utf16((ushort*)pPtr, (nuint)filePath.Length, &m);
                if (ok != 0)
                {
                    meta = m;
                    return true;
                }
            }
            return false;
        }

        public static unsafe string FormatFileSize(ulong bytes)
        {
            if (!IsAvailable)
            {
                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
                if (bytes < 1024 * 1024 * 1024) return $"{(bytes / (1024.0 * 1024.0)):F1} MB";
                return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
            }

            Span<char> buf = stackalloc char[64];
            fixed (char* bPtr = buf)
            {
                nuint outLen = 0;
                var ok = flow_preview_format_size_utf16(bytes, (ushort*)bPtr, (nuint)buf.Length, &outLen);
                if (ok != 0 && outLen > 0)
                {
                    return new string(bPtr, 0, (int)outLen);
                }
            }
            return $"{bytes} B";
        }

        public static unsafe string ReadTextPreviewHead(string filePath, int maxBytes = 4096)
        {
            if (!IsAvailable || string.IsNullOrEmpty(filePath)) return null;
            Span<char> buf = stackalloc char[4096];
            fixed (char* pPtr = filePath)
            fixed (char* bPtr = buf)
            {
                nuint outLen = 0;
                var ok = flow_preview_read_text_head_utf16(
                    (ushort*)pPtr, (nuint)filePath.Length,
                    (nuint)maxBytes,
                    (ushort*)bPtr, (nuint)buf.Length,
                    &outLen);
                if (ok != 0 && outLen > 0)
                {
                    return new string(bPtr, 0, (int)outLen);
                }
            }
            return null;
        }

        #endregion

        #region Native JSON-RPC Streaming

        public static unsafe int ParseJsonRpcResponse(ReadOnlySpan<byte> utf8Json, Span<NativeJsonRpcItem> outItems)
        {
            if (!IsAvailable || utf8Json.IsEmpty || outItems.IsEmpty) return 0;
            fixed (byte* jPtr = utf8Json)
            fixed (NativeJsonRpcItem* iPtr = outItems)
            {
                nuint count = 0;
                var ok = flow_jsonrpc_parse_response_utf8(
                    jPtr, (nuint)utf8Json.Length,
                    iPtr, (nuint)outItems.Length,
                    &count);
                return ok != 0 ? (int)count : 0;
            }
        }

        #endregion

        #region Native UWP Manifest Indexing

        public static unsafe int ParseUwpManifest(string manifestOrDirPath, Span<NativeUwpAppInfo> outApps)
        {
            if (!IsAvailable || string.IsNullOrEmpty(manifestOrDirPath) || outApps.IsEmpty) return 0;
            fixed (char* pPtr = manifestOrDirPath)
            fixed (NativeUwpAppInfo* aPtr = outApps)
            {
                nuint count = 0;
                var ok = flow_uwp_parse_manifest_utf16(
                    (ushort*)pPtr, (nuint)manifestOrDirPath.Length,
                    aPtr, (nuint)outApps.Length,
                    &count);
                return ok != 0 ? (int)count : 0;
            }
        }

        #endregion

        #region Native In-Memory Bookmark Indexing

        public static unsafe bool ClearBookmarksCache()
        {
            if (!IsAvailable) return false;
            return flow_bookmarks_clear() != 0;
        }

        public static unsafe bool RegisterBookmark(uint id, string title, string url)
        {
            if (!IsAvailable || string.IsNullOrEmpty(title)) return false;
            fixed (char* tPtr = title)
            fixed (char* uPtr = url)
            {
                var ok = flow_bookmarks_register(
                    id,
                    (ushort*)tPtr, (nuint)title.Length,
                    uPtr != null ? (ushort*)uPtr : null, (nuint)(url?.Length ?? 0));
                return ok != 0;
            }
        }

        public static unsafe int SearchBookmarks(string query, Span<NativeBookmarkMatch> outMatches)
        {
            if (!IsAvailable || string.IsNullOrEmpty(query) || outMatches.IsEmpty) return 0;
            fixed (char* qPtr = query)
            fixed (NativeBookmarkMatch* mPtr = outMatches)
            {
                nuint count = 0;
                var ok = flow_bookmarks_search(
                    (ushort*)qPtr, (nuint)query.Length,
                    mPtr, (nuint)outMatches.Length,
                    &count);
                return ok != 0 ? (int)count : 0;
            }
        }

        #endregion

        #region Native Text Metrics

        public static unsafe bool MeasureText(
            ReadOnlySpan<char> text,
            string fontName,
            float fontSize,
            int fontWeight,
            bool isItalic,
            out NativeTextMetrics metrics)
        {
            metrics = default;
            if (!IsAvailable) return false;

            fixed (char* tPtr = text)
            fixed (char* fPtr = fontName)
            {
                NativeTextMetrics m = default;
                var ok = flow_gui_measure_text_utf16(
                    (ushort*)tPtr, (nuint)text.Length,
                    fPtr != null ? (ushort*)fPtr : null, (nuint)(fontName?.Length ?? 0),
                    fontSize,
                    fontWeight,
                    isItalic ? 1 : 0,
                    &m);

                if (ok != 0)
                {
                    metrics = m;
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Native LZ4 Compression & Cache

        public static unsafe bool CompressLz4(ReadOnlySpan<byte> source, Span<byte> destination, out int compressedLength)
        {
            compressedLength = 0;
            if (!IsAvailable || source.IsEmpty || destination.IsEmpty) return false;

            fixed (byte* sPtr = source)
            fixed (byte* dPtr = destination)
            {
                nuint outLen = 0;
                var ok = flow_lz4_compress(sPtr, (nuint)source.Length, dPtr, (nuint)destination.Length, &outLen);
                if (ok != 0 && outLen > 0)
                {
                    compressedLength = (int)outLen;
                    return true;
                }
            }
            return false;
        }

        public static unsafe bool DecompressLz4(ReadOnlySpan<byte> compressed, Span<byte> destination, out int decompressedLength)
        {
            decompressedLength = 0;
            if (!IsAvailable || compressed.IsEmpty || destination.IsEmpty) return false;

            fixed (byte* cPtr = compressed)
            fixed (byte* dPtr = destination)
            {
                nuint outLen = 0;
                var ok = flow_lz4_decompress(cPtr, (nuint)compressed.Length, dPtr, (nuint)destination.Length, &outLen);
                if (ok != 0 && outLen > 0)
                {
                    decompressedLength = (int)outLen;
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Native Kernel Directory Watcher

        public static unsafe IntPtr CreateDirectoryWatcher(string dirPath, bool watchSubtree = false)
        {
            if (!IsAvailable || string.IsNullOrEmpty(dirPath)) return IntPtr.Zero;

            fixed (char* pPtr = dirPath)
            {
                return flow_fs_watcher_create((ushort*)pPtr, (nuint)dirPath.Length, watchSubtree ? 1 : 0);
            }
        }

        public static bool CheckDirectoryWatcher(IntPtr watcherHandle)
        {
            if (!IsAvailable || watcherHandle == IntPtr.Zero) return false;
            return flow_fs_watcher_check(watcherHandle) == 1;
        }

        public static bool CloseDirectoryWatcher(IntPtr watcherHandle)
        {
            if (!IsAvailable || watcherHandle == IntPtr.Zero) return false;
            return flow_fs_watcher_close(watcherHandle) != 0;
        }

        #endregion

        #region Native Query Parser & Tokenizer

        public static unsafe bool TokenizeQuery(
            ReadOnlySpan<char> query,
            ReadOnlySpan<char> actionKeyword,
            Span<NativeQueryTerm> outTerms,
            out NativeParsedQuery parsed)
        {
            parsed = default;
            if (!IsAvailable)
            {
                return TokenizeQueryFallback(query, actionKeyword, outTerms, out parsed);
            }

            fixed (char* qPtr = query)
            fixed (char* kwPtr = actionKeyword)
            fixed (NativeQueryTerm* termsPtr = outTerms)
            fixed (NativeParsedQuery* pParsed = &parsed)
            {
                var ok = flow_query_tokenize_utf16(
                    (ushort*)qPtr,
                    (nuint)query.Length,
                    actionKeyword.IsEmpty ? null : (ushort*)kwPtr,
                    (nuint)actionKeyword.Length,
                    termsPtr,
                    (nuint)outTerms.Length,
                    pParsed);
                return ok != 0;
            }
        }

        private static bool TokenizeQueryFallback(
            ReadOnlySpan<char> query,
            ReadOnlySpan<char> actionKeyword,
            Span<NativeQueryTerm> outTerms,
            out NativeParsedQuery parsed)
        {
            parsed = default;
            if (query.IsEmpty)
            {
                parsed.IsHomeQuery = 1;
                return true;
            }

            int trimStart = 0;
            while (trimStart < query.Length && char.IsWhiteSpace(query[trimStart])) trimStart++;
            int trimEnd = query.Length;
            while (trimEnd > trimStart && char.IsWhiteSpace(query[trimEnd - 1])) trimEnd--;

            if (trimStart >= trimEnd)
            {
                parsed.IsHomeQuery = 1;
                return true;
            }

            var trimmed = query.Slice(trimStart, trimEnd - trimStart);
            bool matchedKw = false;
            if (!actionKeyword.IsEmpty && trimmed.Length >= actionKeyword.Length)
            {
                if (trimmed.Slice(0, actionKeyword.Length).SequenceEqual(actionKeyword))
                {
                    if (trimmed.Length == actionKeyword.Length || char.IsWhiteSpace(trimmed[actionKeyword.Length]))
                    {
                        matchedKw = true;
                    }
                }
            }

            int termCount = 0;
            if (matchedKw)
            {
                parsed.HasActionKeyword = 1;
                int sIdx = actionKeyword.Length;
                while (sIdx < trimmed.Length && char.IsWhiteSpace(trimmed[sIdx])) sIdx++;

                parsed.SearchOffset = (uint)(trimStart + sIdx);
                parsed.SearchLength = (uint)(trimmed.Length - sIdx);

                int idx = sIdx;
                while (idx < trimmed.Length && termCount < outTerms.Length)
                {
                    while (idx < trimmed.Length && char.IsWhiteSpace(trimmed[idx])) idx++;
                    if (idx >= trimmed.Length) break;
                    int start = idx;
                    while (idx < trimmed.Length && !char.IsWhiteSpace(trimmed[idx])) idx++;
                    outTerms[termCount++] = new NativeQueryTerm { Start = (uint)(trimStart + start), Length = (uint)(idx - start) };
                }
            }
            else
            {
                parsed.HasActionKeyword = 0;
                parsed.SearchOffset = (uint)trimStart;
                parsed.SearchLength = (uint)trimmed.Length;

                int idx = 0;
                while (idx < trimmed.Length && termCount < outTerms.Length)
                {
                    while (idx < trimmed.Length && char.IsWhiteSpace(trimmed[idx])) idx++;
                    if (idx >= trimmed.Length) break;
                    int start = idx;
                    while (idx < trimmed.Length && !char.IsWhiteSpace(trimmed[idx])) idx++;
                    outTerms[termCount++] = new NativeQueryTerm { Start = (uint)(trimStart + start), Length = (uint)(idx - start) };
                }
            }

            parsed.TermCount = (uint)termCount;
            return true;
        }

        #endregion

        #region Native History & Selection Hash Engine

        public static unsafe int ComputeHistoryHash(ReadOnlySpan<char> text, int initial = 23)
        {
            if (text.IsEmpty) return initial;

            if (IsAvailable)
            {
                fixed (char* ptr = text)
                {
                    return flow_history_hash_utf16((ushort*)ptr, (nuint)text.Length, initial);
                }
            }

            unchecked
            {
                int start = initial;
                for (int i = 0; i < text.Length; i++)
                {
                    start = start * 31 + text[i];
                }
                return start;
            }
        }

        public static bool RecordUserSelection(int keyWithQuery, int keyWithoutQuery)
        {
            if (!IsAvailable) return false;
            return flow_history_record_add(keyWithQuery, keyWithoutQuery) != 0;
        }

        public static int GetUserSelectedCount(int keyWithQuery, int keyWithoutQuery)
        {
            if (!IsAvailable) return 0;
            return flow_history_get_count(keyWithQuery, keyWithoutQuery);
        }

        public static bool ClearUserSelectionHistory()
        {
            if (!IsAvailable) return false;
            return flow_history_clear() != 0;
        }

        #endregion
    }
}
