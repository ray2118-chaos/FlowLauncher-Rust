using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Snap
{
    public class Main : IPlugin
    {
        private PluginInitContext? _context;
        private const string IconPath = "Images\\snap.png";

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            var targetHwnd = WindowSnapHelper.GetTopTargetWindow();
            var targetTitle = WindowSnapHelper.GetWindowTitle(targetHwnd);
            var titleSuffix = string.IsNullOrEmpty(targetTitle) ? "Active Window" : $"\"{targetTitle}\"";

            var search = query.Search.Trim().ToLowerInvariant();

            var snapCommands = new List<(string Key, string Title, string SubTitle, string Glyph, WindowSnapHelper.SnapAction Action, int BaseScore)>
            {
                ("left", $"Snap Left: {titleSuffix}", "Tile window to the left half (50% width)", "\xe896", WindowSnapHelper.SnapAction.LeftHalf, 100),
                ("right", $"Snap Right: {titleSuffix}", "Tile window to the right half (50% width)", "\xe897", WindowSnapHelper.SnapAction.RightHalf, 95),
                ("top", $"Snap Top: {titleSuffix}", "Tile window to the top half (50% height)", "\xe70e", WindowSnapHelper.SnapAction.TopHalf, 90),
                ("bottom", $"Snap Bottom: {titleSuffix}", "Tile window to the bottom half (50% height)", "\xe70d", WindowSnapHelper.SnapAction.BottomHalf, 85),
                ("center", $"Center Window: {titleSuffix}", "Center window on screen with 75% size", "\xe740", WindowSnapHelper.SnapAction.Center, 80),
                ("max", $"Maximize: {titleSuffix}", "Expand window to fill the entire display", "\xe922", WindowSnapHelper.SnapAction.Maximize, 78),
                ("min", $"Minimize: {titleSuffix}", "Minimize window to taskbar", "\xe921", WindowSnapHelper.SnapAction.Minimize, 75),
                ("next", $"Move to Next Monitor: {titleSuffix}", "Send window across to the adjacent monitor", "\xe72b", WindowSnapHelper.SnapAction.NextMonitor, 72),
                ("prev", $"Move to Prev Monitor: {titleSuffix}", "Send window to the previous monitor", "\xe72b", WindowSnapHelper.SnapAction.PrevMonitor, 70),
                ("topleft", $"Top-Left Quarter: {titleSuffix}", "Quarter tile: top-left corner", "\xe740", WindowSnapHelper.SnapAction.TopLeftQuarter, 65),
                ("topright", $"Top-Right Quarter: {titleSuffix}", "Quarter tile: top-right corner", "\xe740", WindowSnapHelper.SnapAction.TopRightQuarter, 64),
                ("bottomleft", $"Bottom-Left Quarter: {titleSuffix}", "Quarter tile: bottom-left corner", "\xe740", WindowSnapHelper.SnapAction.BottomLeftQuarter, 63),
                ("bottomright", $"Bottom-Right Quarter: {titleSuffix}", "Quarter tile: bottom-right corner", "\xe740", WindowSnapHelper.SnapAction.BottomRightQuarter, 62),
                ("third left", $"Left Third: {titleSuffix}", "33% width snapped to left edge", "\xe896", WindowSnapHelper.SnapAction.LeftThird, 60),
                ("third center", $"Center Third: {titleSuffix}", "33% width centered on display", "\xe740", WindowSnapHelper.SnapAction.CenterThird, 58),
                ("third right", $"Right Third: {titleSuffix}", "33% width snapped to right edge", "\xe897", WindowSnapHelper.SnapAction.RightThird, 56),
                ("two thirds left", $"Left Two-Thirds: {titleSuffix}", "66% width snapped to left edge", "\xe896", WindowSnapHelper.SnapAction.LeftTwoThirds, 54),
                ("two thirds right", $"Right Two-Thirds: {titleSuffix}", "66% width snapped to right edge", "\xe897", WindowSnapHelper.SnapAction.RightTwoThirds, 52),
            };

            foreach (var cmd in snapCommands)
            {
                if (string.IsNullOrEmpty(search))
                {
                    results.Add(CreateResult(cmd.Title, cmd.SubTitle, cmd.Glyph, cmd.Action, targetHwnd, cmd.BaseScore));
                }
                else
                {
                    var matchKey = _context?.API.FuzzySearch(search, cmd.Key);
                    var matchTitle = _context?.API.FuzzySearch(search, cmd.Title);
                    var score = Math.Max(matchKey?.Score ?? 0, matchTitle?.Score ?? 0);

                    if (cmd.Key.Contains(search, StringComparison.OrdinalIgnoreCase) || cmd.Key.StartsWith(search, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 300);
                    }

                    if (score > 0)
                    {
                        var res = CreateResult(cmd.Title, cmd.SubTitle, cmd.Glyph, cmd.Action, targetHwnd, score);
                        if (matchKey?.Score >= (matchTitle?.Score ?? 0) && matchKey?.MatchData != null)
                        {
                            res.TitleHighlightData = matchKey.MatchData;
                        }
                        results.Add(res);
                    }
                }
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }

        private Result CreateResult(string title, string subtitle, string glyph, WindowSnapHelper.SnapAction action, IntPtr hwnd, int score)
        {
            return new Result
            {
                Title = title,
                SubTitle = subtitle,
                IcoPath = IconPath,
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons, Segoe MDL2 Assets", Glyph: glyph),
                Score = score,
                Action = _ =>
                {
                    WindowSnapHelper.PerformSnap(hwnd, action);
                    return true;
                }
            };
        }
    }

    internal static class WindowSnapHelper
    {
        public enum SnapAction
        {
            LeftHalf,
            RightHalf,
            TopHalf,
            BottomHalf,
            TopLeftQuarter,
            TopRightQuarter,
            BottomLeftQuarter,
            BottomRightQuarter,
            Center,
            Maximize,
            Minimize,
            NextMonitor,
            PrevMonitor,
            LeftThird,
            CenterThird,
            RightThird,
            LeftTwoThirds,
            RightTwoThirds
        }

        public static IntPtr GetTopTargetWindow()
        {
            var currentPid = Process.GetCurrentProcess().Id;
            IntPtr targetHwnd = IntPtr.Zero;

            EnumWindows((hwnd, lParam) =>
            {
                if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;

                GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == currentPid) return true;

                var title = GetWindowTitle(hwnd);
                if (string.IsNullOrWhiteSpace(title)) return true;

                var className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                var cls = className.ToString();

                if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow")
                {
                    return true;
                }

                // Check for cloaked window (e.g. UWP apps suspended)
                if (DwmGetWindowAttribute(hwnd, 14 /* DWMWA_CLOAKED */, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                {
                    return true;
                }

                GetWindowRect(hwnd, out var rect);
                if (rect.Right - rect.Left < 100 || rect.Bottom - rect.Top < 100)
                {
                    return true;
                }

                targetHwnd = hwnd;
                return false; // Stop enumeration, top target found
            }, IntPtr.Zero);

            return targetHwnd;
        }

        public static string GetWindowTitle(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return string.Empty;
            var sb = new StringBuilder(256);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static void PerformSnap(IntPtr hwnd, SnapAction action)
        {
            if (hwnd == IntPtr.Zero)
            {
                hwnd = GetTopTargetWindow();
                if (hwnd == IntPtr.Zero) return;
            }

            if (action == SnapAction.Maximize)
            {
                ShowWindow(hwnd, 3 /* SW_MAXIMIZE */);
                SetForegroundWindow(hwnd);
                return;
            }

            if (action == SnapAction.Minimize)
            {
                ShowWindow(hwnd, 6 /* SW_MINIMIZE */);
                return;
            }

            var hMon = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
            var monInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(hMon, ref monInfo)) return;

            var work = monInfo.rcWork;
            var (wx, wy, ww, wh) = (work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top);

            int x = wx, y = wy, w = ww, h = wh;
            var halfW = ww / 2;
            var halfH = wh / 2;
            var thirdW = ww / 3;
            var twoThirdsW = (ww * 2) / 3;

            switch (action)
            {
                case SnapAction.LeftHalf:
                    x = wx; y = wy; w = halfW; h = wh;
                    break;
                case SnapAction.RightHalf:
                    x = wx + halfW; y = wy; w = ww - halfW; h = wh;
                    break;
                case SnapAction.TopHalf:
                    x = wx; y = wy; w = ww; h = halfH;
                    break;
                case SnapAction.BottomHalf:
                    x = wx; y = wy + halfH; w = ww; h = wh - halfH;
                    break;
                case SnapAction.TopLeftQuarter:
                    x = wx; y = wy; w = halfW; h = halfH;
                    break;
                case SnapAction.TopRightQuarter:
                    x = wx + halfW; y = wy; w = ww - halfW; h = halfH;
                    break;
                case SnapAction.BottomLeftQuarter:
                    x = wx; y = wy + halfH; w = halfW; h = wh - halfH;
                    break;
                case SnapAction.BottomRightQuarter:
                    x = wx + halfW; y = wy + halfH; w = ww - halfW; h = wh - halfH;
                    break;
                case SnapAction.Center:
                    w = (ww * 3) / 4;
                    h = (wh * 3) / 4;
                    x = wx + (ww - w) / 2;
                    y = wy + (wh - h) / 2;
                    break;
                case SnapAction.LeftThird:
                    x = wx; y = wy; w = thirdW; h = wh;
                    break;
                case SnapAction.CenterThird:
                    x = wx + thirdW; y = wy; w = thirdW; h = wh;
                    break;
                case SnapAction.RightThird:
                    x = wx + ww - thirdW; y = wy; w = thirdW; h = wh;
                    break;
                case SnapAction.LeftTwoThirds:
                    x = wx; y = wy; w = twoThirdsW; h = wh;
                    break;
                case SnapAction.RightTwoThirds:
                    x = wx + ww - twoThirdsW; y = wy; w = twoThirdsW; h = wh;
                    break;
                case SnapAction.NextMonitor:
                case SnapAction.PrevMonitor:
                    MoveToAdjacentMonitor(hwnd, hMon, action == SnapAction.NextMonitor);
                    return;
            }

            ShowWindow(hwnd, 9 /* SW_RESTORE */);
            SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, 0x0040 /* SWP_SHOWWINDOW */);
            SetForegroundWindow(hwnd);
        }

        private static void MoveToAdjacentMonitor(IntPtr hwnd, IntPtr currentMon, bool next)
        {
            var monitors = new List<MONITORINFO>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    monitors.Add(mi);
                }
                return true;
            }, IntPtr.Zero);

            if (monitors.Count <= 1) return;

            var curInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(currentMon, ref curInfo);

            var curIndex = monitors.FindIndex(m => m.rcMonitor.Left == curInfo.rcMonitor.Left && m.rcMonitor.Top == curInfo.rcMonitor.Top);
            if (curIndex < 0) curIndex = 0;

            var targetIndex = next ? (curIndex + 1) % monitors.Count : (curIndex - 1 + monitors.Count) % monitors.Count;
            var targetMon = monitors[targetIndex];

            GetWindowRect(hwnd, out var curRect);
            var curWork = curInfo.rcWork;
            var targetWork = targetMon.rcWork;

            // Maintain relative scale and offset
            double relX = (double)(curRect.Left - curWork.Left) / Math.Max(1, curWork.Right - curWork.Left);
            double relY = (double)(curRect.Top - curWork.Top) / Math.Max(1, curWork.Bottom - curWork.Top);
            double relW = (double)(curRect.Right - curRect.Left) / Math.Max(1, curWork.Right - curWork.Left);
            double relH = (double)(curRect.Bottom - curRect.Top) / Math.Max(1, curWork.Bottom - curWork.Top);

            var twW = targetWork.Right - targetWork.Left;
            var twH = targetWork.Bottom - targetWork.Top;

            int newW = Math.Max(200, (int)(relW * twW));
            int newH = Math.Max(150, (int)(relH * twH));
            int newX = targetWork.Left + (int)(relX * twW);
            int newY = targetWork.Top + (int)(relY * twH);

            ShowWindow(hwnd, 9 /* SW_RESTORE */);
            SetWindowPos(hwnd, IntPtr.Zero, newX, newY, newW, newH, 0x0040 /* SWP_SHOWWINDOW */);
            SetForegroundWindow(hwnd);
        }

        #region Win32 P/Invoke
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
        #endregion
    }
}
