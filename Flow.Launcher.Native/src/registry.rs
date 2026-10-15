use std::path::Path;
use windows_sys::Win32::System::Environment::ExpandEnvironmentStringsW;
use windows_sys::Win32::System::Registry::{
    RegCloseKey, RegEnumKeyExW, RegOpenKeyExW, RegQueryValueExW,
    HKEY, HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE, KEY_READ,
};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativeRegistryApp {
    pub path: [u16; 512],
    pub path_len: u32,
}

fn to_utf16_null(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

fn expand_and_validate(raw_val: &[u16]) -> Option<Vec<u16>> {
    let mut expanded: [u16; 512] = [0; 512];
    let len = unsafe {
        ExpandEnvironmentStringsW(raw_val.as_ptr(), expanded.as_mut_ptr(), 512)
    };
    if len == 0 || len > 512 {
        return None;
    }

    let mut start = 0;
    let mut end = len as usize - 1; // exclude null

    // Trim quotes and whitespace
    while start < end && (expanded[start] == b'"' as u16 || expanded[start] == b' ' as u16) {
        start += 1;
    }
    while end > start && (expanded[end - 1] == b'"' as u16 || expanded[end - 1] == b' ' as u16) {
        end -= 1;
    }

    if start >= end {
        return None;
    }

    let trimmed = &expanded[start..end];
    if let Ok(path_str) = String::from_utf16(trimmed) {
        if Path::new(&path_str).is_file() {
            return Some(trimmed.to_vec());
        }
    }
    None
}

fn scan_key_app_paths(root: HKEY, subkey_name: &str, out_apps: &mut Vec<Vec<u16>>, max_items: usize) {
    let subkey_utf16 = to_utf16_null(subkey_name);
    let mut h_key: HKEY = std::ptr::null_mut();

    let res = unsafe {
        RegOpenKeyExW(root, subkey_utf16.as_ptr(), 0, KEY_READ, &mut h_key)
    };
    if res != 0 || h_key.is_null() {
        return;
    }

    let mut index = 0u32;
    let mut subkey_buf: [u16; 256] = [0; 256];

    loop {
        if out_apps.len() >= max_items {
            break;
        }

        let mut subkey_len = subkey_buf.len() as u32;
        let enum_res = unsafe {
            RegEnumKeyExW(
                h_key,
                index,
                subkey_buf.as_mut_ptr(),
                &mut subkey_len,
                std::ptr::null_mut(),
                std::ptr::null_mut(),
                std::ptr::null_mut(),
                std::ptr::null_mut(),
            )
        };

        if enum_res != 0 {
            break;
        }

        // Open child key
        let mut h_child: HKEY = std::ptr::null_mut();
        let child_open = unsafe {
            RegOpenKeyExW(h_key, subkey_buf.as_ptr(), 0, KEY_READ, &mut h_child)
        };

        if child_open == 0 && !h_child.is_null() {
            let mut val_buf: [u16; 512] = [0; 512];
            let mut val_bytes = (val_buf.len() * 2) as u32;

            let query_res = unsafe {
                RegQueryValueExW(
                    h_child,
                    std::ptr::null(), // default value
                    std::ptr::null_mut(),
                    std::ptr::null_mut(),
                    val_buf.as_mut_ptr() as *mut u8,
                    &mut val_bytes,
                )
            };

            if query_res == 0 && val_bytes >= 2 {
                let char_len = (val_bytes / 2) as usize;
                if let Some(valid_path) = expand_and_validate(&val_buf[..char_len]) {
                    if !out_apps.iter().any(|existing| existing == &valid_path) {
                        out_apps.push(valid_path);
                    }
                }
            }

            unsafe {
                RegCloseKey(h_child);
            }
        }

        index += 1;
    }

    unsafe {
        RegCloseKey(h_key);
    }
}

#[no_mangle]
pub unsafe extern "C" fn flow_scan_registry_app_paths(
    out_items: *mut NativeRegistryApp,
    max_items: usize,
    out_count: *mut usize,
) -> i32 {
    if out_items.is_null() || out_count.is_null() || max_items == 0 {
        return 0;
    }

    let mut apps: Vec<Vec<u16>> = Vec::with_capacity(std::cmp::min(max_items, 256));
    const APP_PATHS: &str = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths";

    scan_key_app_paths(HKEY_LOCAL_MACHINE, APP_PATHS, &mut apps, max_items);
    scan_key_app_paths(HKEY_CURRENT_USER, APP_PATHS, &mut apps, max_items);

    let count = std::cmp::min(apps.len(), max_items);
    let out_slice = unsafe { std::slice::from_raw_parts_mut(out_items, count) };

    for (i, path_vec) in apps.iter().take(count).enumerate() {
        let item = &mut out_slice[i];
        let p_len = std::cmp::min(path_vec.len(), 511);
        item.path[..p_len].copy_from_slice(&path_vec[..p_len]);
        item.path[p_len] = 0;
        item.path_len = p_len as u32;
    }

    unsafe {
        *out_count = count;
    }

    1
}
