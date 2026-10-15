/// Native File Previewer & Metadata Subsystem
/// Fast file metadata extraction, size formatting, and text preview streaming.

use std::fs::File;
use std::io::Read;

#[repr(C)]
pub struct NativeFileMeta {
    pub file_size: u64,
    pub created_time_ms: u64,
    pub modified_time_ms: u64,
    pub is_directory: i32,
    pub is_readonly: i32,
}

#[cfg(windows)]
extern "system" {
    fn GetFileAttributesExW(
        lpFileName: *const u16,
        fInfoLevelId: u32,
        lpFileInformation: *mut WIN32_FILE_ATTRIBUTE_DATA,
    ) -> i32;
}

#[cfg(windows)]
#[repr(C)]
#[allow(non_snake_case)]
struct WIN32_FILE_ATTRIBUTE_DATA {
    dwFileAttributes: u32,
    ftCreationTime: [u32; 2],
    ftLastAccessTime: [u32; 2],
    ftLastWriteTime: [u32; 2],
    nFileSizeHigh: u32,
    nFileSizeLow: u32,
}

const FILE_ATTRIBUTE_DIRECTORY: u32 = 0x00000010;
const FILE_ATTRIBUTE_READONLY: u32 = 0x00000001;

/// Retrieves file size and timestamps in a single native Win32 syscall
#[no_mangle]
pub unsafe extern "C" fn flow_preview_get_meta_utf16(
    path_ptr: *const u16,
    path_len: usize,
    out_meta: *mut NativeFileMeta,
) -> i32 {
    if path_ptr.is_null() || out_meta.is_null() || path_len == 0 {
        return 0;
    }

    #[cfg(windows)]
    {
        let mut null_terminated = [0u16; 1024];
        let copy_len = path_len.min(1023);
        let path_slice = std::slice::from_raw_parts(path_ptr, copy_len);
        null_terminated[..copy_len].copy_from_slice(path_slice);
        null_terminated[copy_len] = 0;

        let mut data: WIN32_FILE_ATTRIBUTE_DATA = std::mem::zeroed();
        let ok = GetFileAttributesExW(null_terminated.as_ptr(), 0, &mut data);
        if ok != 0 {
            let meta = &mut *out_meta;
            meta.file_size = ((data.nFileSizeHigh as u64) << 32) | (data.nFileSizeLow as u64);
            meta.is_directory = if (data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0 { 1 } else { 0 };
            meta.is_readonly = if (data.dwFileAttributes & FILE_ATTRIBUTE_READONLY) != 0 { 1 } else { 0 };

            // Convert Windows FILETIME (100-ns intervals since 1601) to UNIX epoch ms
            let to_unix_ms = |ft: [u32; 2]| -> u64 {
                let intervals = ((ft[1] as u64) << 32) | (ft[0] as u64);
                if intervals > 116444736000000000 {
                    (intervals - 116444736000000000) / 10000
                } else {
                    0
                }
            };

            meta.created_time_ms = to_unix_ms(data.ftCreationTime);
            meta.modified_time_ms = to_unix_ms(data.ftLastWriteTime);
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

/// Formats byte count into human-readable string ("1.5 GB", "340 KB", "12 B")
#[no_mangle]
pub unsafe extern "C" fn flow_preview_format_size_utf16(
    bytes: u64,
    out_buf: *mut u16,
    out_cap: usize,
    out_len: *mut usize,
) -> i32 {
    if out_buf.is_null() || out_len.is_null() || out_cap < 16 {
        return 0;
    }

    let formatted = if bytes < 1024 {
        format!("{} B", bytes)
    } else if bytes < 1024 * 1024 {
        format!("{:.1} KB", bytes as f64 / 1024.0)
    } else if bytes < 1024 * 1024 * 1024 {
        format!("{:.1} MB", bytes as f64 / (1024.0 * 1024.0))
    } else {
        format!("{:.2} GB", bytes as f64 / (1024.0 * 1024.0 * 1024.0))
    };

    let utf16: Vec<u16> = formatted.encode_utf16().collect();
    let copy_len = utf16.len().min(out_cap - 1);
    let out_slice = std::slice::from_raw_parts_mut(out_buf, out_cap);
    out_slice[..copy_len].copy_from_slice(&utf16[..copy_len]);
    out_slice[copy_len] = 0;
    *out_len = copy_len;

    1
}

/// Safely reads the head of a text file (up to max_bytes) without loading entire file into RAM
#[no_mangle]
pub unsafe extern "C" fn flow_preview_read_text_head_utf16(
    path_ptr: *const u16,
    path_len: usize,
    max_bytes: usize,
    out_buf: *mut u16,
    out_cap: usize,
    out_len: *mut usize,
) -> i32 {
    if path_ptr.is_null() || out_buf.is_null() || out_len.is_null() || out_cap == 0 {
        return 0;
    }

    let path_slice = std::slice::from_raw_parts(path_ptr, path_len);
    let path = String::from_utf16_lossy(path_slice);

    let mut file = match File::open(&path) {
        Ok(f) => f,
        Err(_) => return 0,
    };

    let limit = max_bytes.min(8192);
    let mut byte_buf = vec![0u8; limit];
    let bytes_read = match file.read(&mut byte_buf) {
        Ok(n) => n,
        Err(_) => return 0,
    };

    byte_buf.truncate(bytes_read);

    // Convert losslessly to UTF-8 string
    let text = String::from_utf8_lossy(&byte_buf);
    let utf16: Vec<u16> = text.encode_utf16().collect();
    let copy_len = utf16.len().min(out_cap - 1);

    let out_slice = std::slice::from_raw_parts_mut(out_buf, out_cap);
    out_slice[..copy_len].copy_from_slice(&utf16[..copy_len]);
    out_slice[copy_len] = 0;
    *out_len = copy_len;

    1
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_format_size() {
        let mut buf = [0u16; 32];
        let mut len = 0usize;

        unsafe {
            flow_preview_format_size_utf16(500, buf.as_mut_ptr(), buf.len(), &mut len);
            assert_eq!(String::from_utf16_lossy(&buf[..len]), "500 B");

            flow_preview_format_size_utf16(1024 * 1536, buf.as_mut_ptr(), buf.len(), &mut len);
            assert_eq!(String::from_utf16_lossy(&buf[..len]), "1.5 MB");
        }
    }
}
