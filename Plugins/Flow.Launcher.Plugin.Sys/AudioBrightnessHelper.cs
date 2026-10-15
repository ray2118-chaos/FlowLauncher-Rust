using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Sys
{
    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IAudioEndpointVolume ppInterface);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    public static class AudioBrightnessHelper
    {
        private static readonly Guid AudioEndpointVolumeGuid = new("5CDF2C82-841E-4546-9722-0CF74078229A");
        private static int _cachedBrightness = 100;
        private static DateTime _lastFetchTime = DateTime.MinValue;
        private static ManagementEventWatcher? _brightnessWatcher;
        private static readonly object _brightnessLock = new();

        static AudioBrightnessHelper()
        {
            Task.Run(() =>
            {
                QueryLiveBrightness();
                StartBrightnessWatcher();
            });
        }

        private static IAudioEndpointVolume? GetEndpointVolume()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                // 0 = eRender, 1 = eMultimedia
                if (enumerator.GetDefaultAudioEndpoint(0, 1, out var dev) == 0 && dev != null)
                {
                    var iid = AudioEndpointVolumeGuid;
                    if (dev.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out var volume) == 0 && volume != null)
                    {
                        return volume;
                    }
                }
            }
            catch
            {
                // CoreAudio unavailable
            }
            return null;
        }

        public static bool GetVolume(out int percent, out bool isMuted)
        {
            percent = 0;
            isMuted = false;
            try
            {
                var vol = GetEndpointVolume();
                if (vol != null)
                {
                    if (vol.GetMasterVolumeLevelScalar(out var scalar) == 0)
                    {
                        percent = (int)Math.Round(scalar * 100f);
                        vol.GetMute(out isMuted);
                        return true;
                    }
                }
            }
            catch
            {
                // ignored
            }
            return false;
        }

        public static bool SetVolume(int percent)
        {
            try
            {
                var vol = GetEndpointVolume();
                if (vol != null)
                {
                    var clamped = Math.Clamp(percent, 0, 100);
                    var scalar = clamped / 100f;
                    var emptyGuid = Guid.Empty;
                    vol.SetMasterVolumeLevelScalar(scalar, ref emptyGuid);
                    // Automatically unmute if user explicitly sets volume > 0
                    if (clamped > 0)
                    {
                        vol.SetMute(false, ref emptyGuid);
                    }
                    SysHudOverlay.ShowFeedback($"Volume set to {clamped}%", clamped == 0 ? HudIconType.VolumeMuted : HudIconType.Volume, clamped == 0);
                    return true;
                }
            }
            catch
            {
                // ignored
            }
            return false;
        }

        public static bool AdjustVolume(int delta, out int newPercent)
        {
            newPercent = 0;
            if (GetVolume(out var current, out _))
            {
                newPercent = Math.Clamp(current + delta, 0, 100);
                return SetVolume(newPercent);
            }
            return false;
        }

        public static bool SetMute(bool mute)
        {
            try
            {
                var vol = GetEndpointVolume();
                if (vol != null)
                {
                    var emptyGuid = Guid.Empty;
                    vol.SetMute(mute, ref emptyGuid);
                    SysHudOverlay.ShowFeedback(mute ? "Volume muted" : "Volume unmuted", mute ? HudIconType.VolumeMuted : HudIconType.Volume, mute);
                    return true;
                }
            }
            catch
            {
                // ignored
            }
            return false;
        }

        public static bool ToggleMute(out bool newMuteState)
        {
            newMuteState = false;
            try
            {
                var vol = GetEndpointVolume();
                if (vol != null)
                {
                    if (vol.GetMute(out var currentMute) == 0)
                    {
                        newMuteState = !currentMute;
                        var emptyGuid = Guid.Empty;
                        vol.SetMute(newMuteState, ref emptyGuid);
                        vol.GetMasterVolumeLevelScalar(out var scalar);
                        var curPercent = (int)Math.Round(scalar * 100f);
                        SysHudOverlay.ShowFeedback(newMuteState ? "Volume muted" : $"Volume set to {curPercent}%", newMuteState ? HudIconType.VolumeMuted : HudIconType.Volume, newMuteState);
                        return true;
                    }
                }
            }
            catch
            {
                // ignored
            }
            return false;
        }

        private static void StartBrightnessWatcher()
        {
            try
            {
                var scope = new ManagementScope(@"root\wmi");
                var query = new WqlEventQuery("WmiMonitorBrightnessEvent");
                _brightnessWatcher = new ManagementEventWatcher(scope, query);
                _brightnessWatcher.EventArrived += (s, e) =>
                {
                    try
                    {
                        var prop = e.NewEvent.Properties["Brightness"];
                        if (prop?.Value != null)
                        {
                            var val = Convert.ToInt32(prop.Value);
                            lock (_brightnessLock)
                            {
                                _cachedBrightness = Math.Clamp(val, 0, 100);
                                _lastFetchTime = DateTime.UtcNow;
                            }
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                };
                _brightnessWatcher.Start();
            }
            catch
            {
                // Fallback if WQL event subscription isn't permitted
            }
        }

        public static int QueryLiveBrightness()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var val = Convert.ToInt32(obj["CurrentBrightness"]);
                    lock (_brightnessLock)
                    {
                        _cachedBrightness = Math.Clamp(val, 0, 100);
                        _lastFetchTime = DateTime.UtcNow;
                        return _cachedBrightness;
                    }
                }
            }
            catch
            {
                // Fallback to powershell CIM if direct System.Management query encounters driver quirk
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -Command \"Get-CimInstance -Namespace root/wmi -ClassName WmiMonitorBrightness | Select-Object -ExpandProperty CurrentBrightness\"",
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        var output = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit(2000);
                        if (int.TryParse(output, out var val))
                        {
                            lock (_brightnessLock)
                            {
                                _cachedBrightness = Math.Clamp(val, 0, 100);
                                _lastFetchTime = DateTime.UtcNow;
                                return _cachedBrightness;
                            }
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }

            return _cachedBrightness;
        }

        public static int GetBrightness()
        {
            // If cache is older than 500ms or uninitialized, refresh live from Windows Settings
            if (DateTime.UtcNow - _lastFetchTime > TimeSpan.FromMilliseconds(500))
            {
                return QueryLiveBrightness();
            }
            return _cachedBrightness;
        }

        public static void SetBrightness(int percent)
        {
            var clamped = Math.Clamp(percent, 0, 100);
            lock (_brightnessLock)
            {
                _cachedBrightness = clamped;
                _lastFetchTime = DateTime.UtcNow;
            }

            SysHudOverlay.ShowFeedback($"Brightness set to {clamped}%", HudIconType.Brightness);

            Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        obj.InvokeMethod("WmiSetBrightness", new object[] { 1, (byte)clamped });
                        return;
                    }
                }
                catch
                {
                    // PowerShell CIM fallback
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -WindowStyle Hidden -Command \"Get-CimInstance -Namespace root/wmi -ClassName WmiMonitorBrightnessMethods | Invoke-CimMethod -MethodName WmiSetBrightness -Arguments @{{Timeout = 1; Brightness = [byte]{clamped}}}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                        };
                        using var proc = Process.Start(psi);
                        proc?.WaitForExit(3000);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            });
        }

        public static int AdjustBrightness(int delta)
        {
            var cur = GetBrightness();
            var target = Math.Clamp(cur + delta, 0, 100);
            SetBrightness(target);
            return target;
        }
    }
}
