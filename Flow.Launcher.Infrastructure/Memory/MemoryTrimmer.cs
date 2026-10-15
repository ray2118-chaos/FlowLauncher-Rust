using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Native;

namespace Flow.Launcher.Infrastructure.Memory
{
    public static class MemoryTrimmer
    {
        private static readonly string ClassName = nameof(MemoryTrimmer);
        private static CancellationTokenSource _cts;
        private static readonly object _lock = new();

        /// <summary>
        /// Optional predicate to verify whether memory can be trimmed safely (e.g. when main window is hidden).
        /// </summary>
        public static Func<bool> CanTrim { get; set; }

        /// <summary>
        /// Schedules a memory trim after a debounce delay.
        /// If the user reopens the launcher before the delay expires, call CancelTrim() to prevent unnecessary work.
        /// </summary>
        /// <param name="delayMilliseconds">Delay in milliseconds before performing trim.</param>
        public static void ScheduleTrim(int delayMilliseconds = 1500)
        {
            lock (_lock)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                var token = _cts.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(delayMilliseconds, token).ConfigureAwait(false);
                        if (!token.IsCancellationRequested)
                        {
                            TrimMemory();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when user re-activates the window before delay expires
                    }
                    catch (System.Exception ex)
                    {
                        Log.Exception(ClassName, "Error during scheduled memory trim", ex);
                    }
                }, token);
            }
        }

        /// <summary>
        /// Cancels any pending scheduled memory trim.
        /// </summary>
        public static void CancelTrim()
        {
            lock (_lock)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// Executes an aggressive memory trim immediately, collecting managed GC generations,
        /// compacting the Large Object Heap (LOH), and reclaiming OS working set pages.
        /// </summary>
        public static void TrimMemory()
        {
            if (CanTrim != null && !CanTrim())
            {
                return;
            }

            try
            {
                long beforeWorkingSet = 0;
                try
                {
                    using var proc = Process.GetCurrentProcess();
                    beforeWorkingSet = proc.WorkingSet64;
                }
                catch
                {
                    // Ignore
                }

                // Instruct GC to compact Large Object Heap on next collection
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

                // Aggressively collect all generations
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

                // Trim unmanaged and WPF working set from physical memory back to Windows OS
                FlowNativeMethods.TrimWorkingSet();

                long afterWorkingSet = 0;
                try
                {
                    using var proc = Process.GetCurrentProcess();
                    afterWorkingSet = proc.WorkingSet64;
                }
                catch
                {
                    // Ignore
                }

                if (beforeWorkingSet > 0 && afterWorkingSet > 0)
                {
                    var reclaimedMb = (beforeWorkingSet - afterWorkingSet) / (1024.0 * 1024.0);
                    var currentMb = afterWorkingSet / (1024.0 * 1024.0);
                    Log.Info(ClassName, $"TrimMemory complete. Current Working Set: {currentMb:F2} MB (Reclaimed: {reclaimedMb:F2} MB)");
                }
            }
            catch (System.Exception ex)
            {
                Log.Exception(ClassName, "Failed to trim memory", ex);
            }
        }
    }
}
