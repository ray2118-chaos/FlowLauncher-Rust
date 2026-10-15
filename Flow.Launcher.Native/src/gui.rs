//! Native Windows GUI & DWM window composition subsystem for Flow Launcher.
//! Replaces manual C# Win32 P/Invokes with fast, safe, zero-allocation native calls.

use std::ffi::c_void;
use windows_sys::Win32::Foundation::HWND;
use windows_sys::Win32::System::LibraryLoader::{GetModuleHandleW, GetProcAddress, LoadLibraryW};

type BOOL = i32;
const FALSE: BOOL = 0;

#[repr(u32)]
#[derive(Copy, Clone, Debug, PartialEq, Eq)]
pub enum AccentState {
    Disabled = 0,
    EnableGradient = 1,
    EnableTransparentGradient = 2,
    EnableBlurBehind = 3,
    EnableAcrylicBlurBehind = 4,
    EnableHostBackdrop = 5,
    InvalidState = 6,
}

#[repr(C)]
struct AccentPolicy {
    accent_state: u32,
    accent_flags: u32,
    gradient_color: u32,
    animation_id: u32,
}

#[repr(C)]
struct WindowCompositionAttributeData {
    attribute: u32, // WCA_ACCENT_POLICY = 19
    data: *const c_void,
    size_of_data: usize,
}

type PfnSetWindowCompositionAttribute = unsafe extern "system" fn(HWND, *const WindowCompositionAttributeData) -> BOOL;
type PfnDwmSetWindowAttribute = unsafe extern "system" fn(HWND, u32, *const c_void, u32) -> i32;

/// Applies native Windows 10/11 acrylic blur or gradient accent policy to a window handle.
/// `hwnd`: Window handle
/// `enable`: 1 to enable acrylic blur, 0 to disable
/// `gradient_color`: 32-bit ARGB/ABGR tint color
#[no_mangle]
pub unsafe extern "C" fn flow_gui_set_window_blur(hwnd: isize, enable: i32, gradient_color: u32) -> i32 {
    if hwnd == 0 {
        return 0;
    }

    // Resolve SetWindowCompositionAttribute dynamically from user32.dll
    let user32_name: [u16; 11] = [
        'u' as u16, 's' as u16, 'e' as u16, 'r' as u16, '3' as u16, '2' as u16,
        '.' as u16, 'd' as u16, 'l' as u16, 'l' as u16, 0,
    ];
    let h_user32 = GetModuleHandleW(user32_name.as_ptr());
    if h_user32.is_null() {
        return 0;
    }

    let proc_name = b"SetWindowCompositionAttribute\0";
    let pfn = GetProcAddress(h_user32, proc_name.as_ptr());
    if pfn.is_none() {
        return 0;
    }

    let set_window_composition: PfnSetWindowCompositionAttribute = std::mem::transmute(pfn);

    let policy = if enable != 0 {
        AccentPolicy {
            accent_state: AccentState::EnableAcrylicBlurBehind as u32,
            accent_flags: 2, // DRAW_ALL_BORDERS
            gradient_color,
            animation_id: 0,
        }
    } else {
        AccentPolicy {
            accent_state: AccentState::Disabled as u32,
            accent_flags: 0,
            gradient_color: 0,
            animation_id: 0,
        }
    };

    let data = WindowCompositionAttributeData {
        attribute: 19, // WCA_ACCENT_POLICY
        data: &policy as *const _ as *const c_void,
        size_of_data: std::mem::size_of::<AccentPolicy>(),
    };

    let res = set_window_composition(hwnd as HWND, &data);
    if res != FALSE { 1 } else { 0 }
}

/// Sets native DWM window corner preference (e.g. rounded corners on Windows 11).
/// `corner_preference`:
///   0 = DWMWCP_DEFAULT
///   1 = DWMWCP_DONOTROUND
///   2 = DWMWCP_ROUND
///   3 = DWMWCP_ROUNDSMALL
#[no_mangle]
pub unsafe extern "C" fn flow_gui_set_window_corner_preference(hwnd: isize, corner_preference: u32) -> i32 {
    if hwnd == 0 {
        return 0;
    }

    let dwm_name: [u16; 11] = [
        'd' as u16, 'w' as u16, 'm' as u16, 'a' as u16, 'p' as u16, 'i' as u16,
        '.' as u16, 'd' as u16, 'l' as u16, 'l' as u16, 0,
    ];
    let h_dwm = LoadLibraryW(dwm_name.as_ptr());
    if h_dwm.is_null() {
        return 0;
    }

    let proc_name = b"DwmSetWindowAttribute\0";
    let pfn = GetProcAddress(h_dwm, proc_name.as_ptr());
    if pfn.is_none() {
        return 0;
    }

    let dwm_set_attr: PfnDwmSetWindowAttribute = std::mem::transmute(pfn);
    const DWMWA_WINDOW_CORNER_PREFERENCE: u32 = 33;

    let pref = corner_preference;
    let hr = dwm_set_attr(
        hwnd as HWND,
        DWMWA_WINDOW_CORNER_PREFERENCE,
        &pref as *const _ as *const c_void,
        std::mem::size_of::<u32>() as u32,
    );

    if hr >= 0 { 1 } else { 0 }
}

/// Sets native DWM system backdrop type (Windows 11 22H2+).
/// `backdrop_type`:
///   0 = DWMSBT_AUTO
///   1 = DWMSBT_NONE
///   2 = DWMSBT_MAINWINDOW (Mica)
///   3 = DWMSBT_TRANSIENTWINDOW (Acrylic)
///   4 = DWMSBT_TABBEDWINDOW (Mica Alt)
#[no_mangle]
pub unsafe extern "C" fn flow_gui_set_window_backdrop(hwnd: isize, backdrop_type: u32) -> i32 {
    if hwnd == 0 {
        return 0;
    }

    let dwm_name: [u16; 11] = [
        'd' as u16, 'w' as u16, 'm' as u16, 'a' as u16, 'p' as u16, 'i' as u16,
        '.' as u16, 'd' as u16, 'l' as u16, 'l' as u16, 0,
    ];
    let h_dwm = LoadLibraryW(dwm_name.as_ptr());
    if h_dwm.is_null() {
        return 0;
    }

    let proc_name = b"DwmSetWindowAttribute\0";
    let pfn = GetProcAddress(h_dwm, proc_name.as_ptr());
    if pfn.is_none() {
        return 0;
    }

    let dwm_set_attr: PfnDwmSetWindowAttribute = std::mem::transmute(pfn);
    const DWMWA_SYSTEMBACKDROP_TYPE: u32 = 38;

    let btype = backdrop_type;
    let hr = dwm_set_attr(
        hwnd as HWND,
        DWMWA_SYSTEMBACKDROP_TYPE,
        &btype as *const _ as *const c_void,
        std::mem::size_of::<u32>() as u32,
    );

    if hr >= 0 { 1 } else { 0 }
}

/// Sets native DWM border color.
/// `color`: 0x00RRGGBB or 0xFFFFFFFF for default
#[no_mangle]
pub unsafe extern "C" fn flow_gui_set_window_border_color(hwnd: isize, color: u32) -> i32 {
    if hwnd == 0 {
        return 0;
    }

    let dwm_name: [u16; 11] = [
        'd' as u16, 'w' as u16, 'm' as u16, 'a' as u16, 'p' as u16, 'i' as u16,
        '.' as u16, 'd' as u16, 'l' as u16, 'l' as u16, 0,
    ];
    let h_dwm = LoadLibraryW(dwm_name.as_ptr());
    if h_dwm.is_null() {
        return 0;
    }

    let proc_name = b"DwmSetWindowAttribute\0";
    let pfn = GetProcAddress(h_dwm, proc_name.as_ptr());
    if pfn.is_none() {
        return 0;
    }

    let dwm_set_attr: PfnDwmSetWindowAttribute = std::mem::transmute(pfn);
    const DWMWA_BORDER_COLOR: u32 = 34;

    let c = color;
    let hr = dwm_set_attr(
        hwnd as HWND,
        DWMWA_BORDER_COLOR,
        &c as *const _ as *const c_void,
        std::mem::size_of::<u32>() as u32,
    );

    if hr >= 0 { 1 } else { 0 }
}

#[repr(C)]
pub struct NativeColorPalette {
    pub primary: u32,
    pub accent: u32,
    pub background: u32,
    pub text_primary: u32,
    pub is_dark: i32,
}

type PfnDwmGetColorizationColor = unsafe extern "system" fn(*mut u32, *mut BOOL) -> i32;

/// Retrieves the Windows system accent color from DWM
#[no_mangle]
pub unsafe extern "C" fn flow_gui_get_accent_color(out_color: *mut u32) -> i32 {
    if out_color.is_null() {
        return 0;
    }

    let dwm_name: [u16; 11] = [
        'd' as u16, 'w' as u16, 'm' as u16, 'a' as u16, 'p' as u16, 'i' as u16,
        '.' as u16, 'd' as u16, 'l' as u16, 'l' as u16, 0,
    ];
    let h_dwm = LoadLibraryW(dwm_name.as_ptr());
    if h_dwm.is_null() {
        return 0;
    }

    let proc_name = b"DwmGetColorizationColor\0";
    let pfn = GetProcAddress(h_dwm, proc_name.as_ptr());
    if pfn.is_none() {
        return 0;
    }

    let get_colorization: PfnDwmGetColorizationColor = std::mem::transmute(pfn);
    let mut color = 0u32;
    let mut opaque = 0i32;
    let hr = get_colorization(&mut color, &mut opaque);
    if hr >= 0 {
        *out_color = color;
        1
    } else {
        0
    }
}

/// Extracts dominant colors and determines light/dark luminance recommendation from RGBA pixel buffers
#[no_mangle]
pub unsafe extern "C" fn flow_gui_extract_palette_rgba(
    rgba_ptr: *const u8,
    pixel_count: usize,
    out_palette: *mut NativeColorPalette,
) -> i32 {
    if rgba_ptr.is_null() || out_palette.is_null() || pixel_count == 0 {
        return 0;
    }

    let slice = std::slice::from_raw_parts(rgba_ptr, pixel_count * 4);
    let mut sum_r: u64 = 0;
    let mut sum_g: u64 = 0;
    let mut sum_b: u64 = 0;
    let mut valid_samples: u64 = 0;

    // Sample pixels evenly (step by max(1, count / 1024))
    let step = (pixel_count / 1024).max(1);
    for i in (0..pixel_count).step_by(step) {
        let idx = i * 4;
        let a = slice[idx + 3] as u64;
        if a > 128 {
            sum_r += slice[idx] as u64;
            sum_g += slice[idx + 1] as u64;
            sum_b += slice[idx + 2] as u64;
            valid_samples += 1;
        }
    }

    if valid_samples == 0 {
        return 0;
    }

    let avg_r = (sum_r / valid_samples) as u8;
    let avg_g = (sum_g / valid_samples) as u8;
    let avg_b = (sum_b / valid_samples) as u8;

    // Relative perceived luminance (ITU-R BT.601)
    let luminance = 0.299 * (avg_r as f64) + 0.587 * (avg_g as f64) + 0.114 * (avg_b as f64);
    let is_dark = if luminance < 128.0 { 1 } else { 0 };

    let p = &mut *out_palette;
    p.primary = ((avg_r as u32) << 16) | ((avg_g as u32) << 8) | (avg_b as u32) | 0xFF000000;
    p.accent = p.primary;
    p.background = if is_dark != 0 { 0xFF1E1E1E } else { 0xFFF5F5F5 };
    p.text_primary = if is_dark != 0 { 0xFFFFFFFF } else { 0xFF1A1A1A };
    p.is_dark = is_dark;

    1
}
