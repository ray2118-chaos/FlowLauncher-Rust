// Native Kernel Directory Change Notification Watcher
// Zero-overhead Win32 change listener using kernel notification handles.

#[cfg(windows)]
extern "system" {
    fn FindFirstChangeNotificationW(
        lpPathName: *const u16,
        bWatchSubtree: i32,
        dwNotifyFilter: u32,
    ) -> *mut std::ffi::c_void;
    fn FindNextChangeNotification(hChangeHandle: *mut std::ffi::c_void) -> i32;
    fn FindCloseChangeNotification(hChangeHandle: *mut std::ffi::c_void) -> i32;
    fn WaitForSingleObject(hHandle: *mut std::ffi::c_void, dwMilliseconds: u32) -> u32;
}

#[cfg(windows)]
const INVALID_HANDLE_VALUE: *mut std::ffi::c_void = -1isize as *mut std::ffi::c_void;
const FILE_NOTIFY_CHANGE_FILE_NAME: u32 = 0x00000001;
const FILE_NOTIFY_CHANGE_DIR_NAME: u32 = 0x00000002;
const FILE_NOTIFY_CHANGE_LAST_WRITE: u32 = 0x00000010;
const WAIT_OBJECT_0: u32 = 0;

/// Creates a kernel change notification handle for the specified directory
#[no_mangle]
pub unsafe extern "C" fn flow_fs_watcher_create(
    dir_path_ptr: *const u16,
    dir_path_len: usize,
    watch_subtree: i32,
) -> isize {
    if dir_path_ptr.is_null() || dir_path_len == 0 {
        return 0;
    }

    #[cfg(windows)]
    {
        let mut null_terminated = [0u16; 1024];
        let copy_len = dir_path_len.min(1023);
        let slice = std::slice::from_raw_parts(dir_path_ptr, copy_len);
        null_terminated[..copy_len].copy_from_slice(slice);
        null_terminated[copy_len] = 0;

        let filter = FILE_NOTIFY_CHANGE_FILE_NAME | FILE_NOTIFY_CHANGE_DIR_NAME | FILE_NOTIFY_CHANGE_LAST_WRITE;
        let h = FindFirstChangeNotificationW(
            null_terminated.as_ptr(),
            if watch_subtree != 0 { 1 } else { 0 },
            filter,
        );

        if h.is_null() || h == INVALID_HANDLE_VALUE {
            0
        } else {
            h as isize
        }
    }

    #[cfg(not(windows))]
    {
        0
    }
}

/// Checks if changes occurred in the directory (non-blocking).
/// Returns 1 if changes were signaled, 0 if no change, or -1 on error.
#[no_mangle]
pub unsafe extern "C" fn flow_fs_watcher_check(handle: isize) -> i32 {
    if handle == 0 || handle == -1 {
        return -1;
    }

    #[cfg(windows)]
    {
        let h = handle as *mut std::ffi::c_void;
        let res = WaitForSingleObject(h, 0);
        if res == WAIT_OBJECT_0 {
            // Reset for next notification
            let _ = FindNextChangeNotification(h);
            1
        } else {
            0
        }
    }

    #[cfg(not(windows))]
    {
        0
    }
}

/// Closes and releases the kernel change notification handle
#[no_mangle]
pub unsafe extern "C" fn flow_fs_watcher_close(handle: isize) -> i32 {
    if handle == 0 || handle == -1 {
        return 0;
    }

    #[cfg(windows)]
    {
        let h = handle as *mut std::ffi::c_void;
        let ok = FindCloseChangeNotification(h);
        if ok != 0 { 1 } else { 0 }
    }

    #[cfg(not(windows))]
    {
        1
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_fs_watcher_lifecycle() {
        let temp_dir = std::env::temp_dir();
        let path_str = temp_dir.to_string_lossy();
        let path_utf16: Vec<u16> = path_str.encode_utf16().collect();

        unsafe {
            let handle = flow_fs_watcher_create(path_utf16.as_ptr(), path_utf16.len(), 0);
            #[cfg(windows)]
            {
                assert_ne!(handle, 0);
                assert_ne!(handle, -1);

                // Initial poll should not error
                let check = flow_fs_watcher_check(handle);
                assert!(check >= 0);

                let closed = flow_fs_watcher_close(handle);
                assert_eq!(closed, 1);
            }
        }
    }
}
