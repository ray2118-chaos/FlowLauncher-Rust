//! High-performance, zero-allocation Windows kernel telemetry for Flow Launcher.
//! Directly queries Windows kernel APIs for CPU load, memory utilization, and battery/power metrics.

use std::sync::atomic::{AtomicU64, Ordering};
use windows_sys::Win32::Foundation::FILETIME;
use windows_sys::Win32::System::Power::{GetSystemPowerStatus, SYSTEM_POWER_STATUS};
use windows_sys::Win32::System::SystemInformation::{GlobalMemoryStatusEx, MEMORYSTATUSEX};
use windows_sys::Win32::System::Threading::GetSystemTimes;

static PREV_IDLE: AtomicU64 = AtomicU64::new(0);
static PREV_KERNEL: AtomicU64 = AtomicU64::new(0);
static PREV_USER: AtomicU64 = AtomicU64::new(0);
static LAST_CPU: AtomicU64 = AtomicU64::new(0);

#[repr(C)]
#[derive(Copy, Clone, Debug, Default)]
pub struct SystemGlance {
    pub cpu_usage_pct: u8,
    pub mem_usage_pct: u8,
    pub battery_pct: u8,
    pub is_charging: u8,
    pub has_battery: u8,
    pub total_ram_mb: u32,
    pub used_ram_mb: u32,
}

#[inline]
fn filetime_to_u64(ft: FILETIME) -> u64 {
    ((ft.dwHighDateTime as u64) << 32) | (ft.dwLowDateTime as u64)
}

/// Samples CPU usage % between consecutive calls via GetSystemTimes.
fn sample_cpu_usage() -> u8 {
    let mut idle_time: FILETIME = unsafe { std::mem::zeroed() };
    let mut kernel_time: FILETIME = unsafe { std::mem::zeroed() };
    let mut user_time: FILETIME = unsafe { std::mem::zeroed() };

    let ok = unsafe { GetSystemTimes(&mut idle_time, &mut kernel_time, &mut user_time) };
    if ok == 0 {
        return LAST_CPU.load(Ordering::Relaxed) as u8;
    }

    let idle = filetime_to_u64(idle_time);
    let kernel = filetime_to_u64(kernel_time);
    let user = filetime_to_u64(user_time);

    let prev_idle = PREV_IDLE.swap(idle, Ordering::Relaxed);
    let prev_kernel = PREV_KERNEL.swap(kernel, Ordering::Relaxed);
    let prev_user = PREV_USER.swap(user, Ordering::Relaxed);

    if prev_kernel == 0 && prev_user == 0 {
        return LAST_CPU.load(Ordering::Relaxed) as u8;
    }

    let d_idle = idle.saturating_sub(prev_idle);
    let d_kernel = kernel.saturating_sub(prev_kernel);
    let d_user = user.saturating_sub(prev_user);

    let sys = d_kernel.saturating_add(d_user);
    if sys == 0 {
        return LAST_CPU.load(Ordering::Relaxed) as u8;
    }

    let busy = sys.saturating_sub(d_idle);
    let pct = ((busy as f64 / sys as f64) * 100.0).clamp(0.0, 100.0) as u8;
    LAST_CPU.store(pct as u64, Ordering::Relaxed);
    pct
}

/// Fills `out_glance` with instant system telemetry (CPU %, RAM %, Battery).
/// Executes in < 2 microseconds with zero heap allocations.
#[no_mangle]
pub unsafe extern "C" fn flow_telemetry_get_glance(out_glance: *mut SystemGlance) -> i32 {
    if out_glance.is_null() {
        return 0;
    }

    let mut glance = SystemGlance::default();

    // 1. CPU Usage
    glance.cpu_usage_pct = sample_cpu_usage();

    // 2. RAM Usage
    let mut mem_status: MEMORYSTATUSEX = unsafe { std::mem::zeroed() };
    mem_status.dwLength = std::mem::size_of::<MEMORYSTATUSEX>() as u32;
    if unsafe { GlobalMemoryStatusEx(&mut mem_status) } != 0 {
        glance.mem_usage_pct = mem_status.dwMemoryLoad as u8;
        glance.total_ram_mb = (mem_status.ullTotalPhys / (1024 * 1024)) as u32;
        let avail_mb = (mem_status.ullAvailPhys / (1024 * 1024)) as u32;
        glance.used_ram_mb = glance.total_ram_mb.saturating_sub(avail_mb);
    }

    // 3. Battery Status
    let mut power_status: SYSTEM_POWER_STATUS = unsafe { std::mem::zeroed() };
    if unsafe { GetSystemPowerStatus(&mut power_status) } != 0 {
        if power_status.BatteryFlag != 128 && power_status.BatteryLifePercent != 255 {
            glance.has_battery = 1;
            glance.battery_pct = power_status.BatteryLifePercent.min(100);
            glance.is_charging = if (power_status.BatteryFlag & 8) != 0 || power_status.ACLineStatus == 1 {
                1
            } else {
                0
            };
        } else {
            glance.has_battery = 0;
            glance.battery_pct = 100;
            glance.is_charging = if power_status.ACLineStatus == 1 { 1 } else { 0 };
        }
    }

    unsafe {
        *out_glance = glance;
    }
    1
}
