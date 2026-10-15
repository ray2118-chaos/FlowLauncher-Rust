use std::fs::File;
use std::io::{Read, Seek, SeekFrom};
use std::path::PathBuf;

#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativePeInfo {
    pub description: [u16; 260],
    pub description_len: u32,
    pub subsystem: u32, // 2 = GUI, 3 = CUI (Console), 0 = unknown
    pub is_64bit: u32,
}

#[link(name = "version")]
extern "system" {
    fn GetFileVersionInfoSizeW(lptstrFilename: *const u16, lpdwHandle: *mut u32) -> u32;
    fn GetFileVersionInfoW(lptstrFilename: *const u16, dwHandle: u32, dwLen: u32, lpData: *mut u8) -> i32;
    fn VerQueryValueW(pBlock: *const u8, lpSubBlock: *const u16, lplpBuffer: *mut *mut u16, puLen: *mut u32) -> i32;
}

fn to_utf16_null(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

pub fn extract_pe_headers(file_path: &PathBuf, out_info: &mut NativePeInfo) {
    let mut file = match File::open(file_path) {
        Ok(f) => f,
        Err(_) => return,
    };

    let mut buf = [0u8; 1024];
    let bytes_read = match file.read(&mut buf) {
        Ok(n) => n,
        Err(_) => return,
    };

    if bytes_read < 64 {
        return;
    }

    // Check DOS magic 'MZ'
    if buf[0] != 0x4D || buf[1] != 0x5A {
        return;
    }

    // Read e_lfanew
    let e_lfanew = u32::from_le_bytes([buf[60], buf[61], buf[62], buf[63]]) as usize;
    if e_lfanew + 96 > bytes_read {
        if file.seek(SeekFrom::Start(e_lfanew as u64)).is_err() {
            return;
        }
        let mut pe_buf = [0u8; 256];
        if file.read(&mut pe_buf).unwrap_or(0) < 96 {
            return;
        }
        parse_pe_optional(&pe_buf, out_info);
        return;
    }

    parse_pe_optional(&buf[e_lfanew..], out_info);
}

fn parse_pe_optional(pe_slice: &[u8], out_info: &mut NativePeInfo) {
    if pe_slice.len() < 96 {
        return;
    }
    // Check PE signature 'PE\0\0' (0x50, 0x45, 0x00, 0x00)
    if pe_slice[0] != 0x50 || pe_slice[1] != 0x45 || pe_slice[2] != 0 || pe_slice[3] != 0 {
        return;
    }

    // Machine at offset 4
    let machine = u16::from_le_bytes([pe_slice[4], pe_slice[5]]);
    if machine == 0x8664 || machine == 0xAA64 {
        out_info.is_64bit = 1;
    } else if machine == 0x14C {
        out_info.is_64bit = 0;
    }

    // Optional header magic at offset 24
    let opt_magic = u16::from_le_bytes([pe_slice[24], pe_slice[25]]);
    if opt_magic == 0x20B {
        out_info.is_64bit = 1;
    } else if opt_magic == 0x10B {
        out_info.is_64bit = 0;
    }

    // Subsystem is at offset 24 + 68 = 92
    let subsystem = u16::from_le_bytes([pe_slice[92], pe_slice[93]]);
    out_info.subsystem = subsystem as u32;
}

pub fn extract_file_description(path_null: &[u16], out_info: &mut NativePeInfo) {
    let size = unsafe { GetFileVersionInfoSizeW(path_null.as_ptr(), std::ptr::null_mut()) };
    if size == 0 || size > 2_000_000 {
        return;
    }

    let mut data = vec![0u8; size as usize];
    let ok = unsafe { GetFileVersionInfoW(path_null.as_ptr(), 0, size, data.as_mut_ptr()) };
    if ok == 0 {
        return;
    }

    // Try translation subblock first
    let trans_subblock = to_utf16_null(r"\VarFileInfo\Translation");
    let mut trans_ptr: *mut u16 = std::ptr::null_mut();
    let mut trans_len: u32 = 0;

    let mut found_desc = false;

    let res = unsafe {
        VerQueryValueW(
            data.as_ptr(),
            trans_subblock.as_ptr(),
            &mut trans_ptr,
            &mut trans_len,
        )
    };

    if res != 0 && !trans_ptr.is_null() && trans_len >= 4 {
        let lang = unsafe { *trans_ptr };
        let code = unsafe { *trans_ptr.add(1) };
        let subblock_str = format!(r"\StringFileInfo\{:04x}{:04x}\FileDescription", lang, code);
        let subblock_u16 = to_utf16_null(&subblock_str);

        let mut desc_ptr: *mut u16 = std::ptr::null_mut();
        let mut desc_len: u32 = 0;
        let desc_res = unsafe {
            VerQueryValueW(
                data.as_ptr(),
                subblock_u16.as_ptr(),
                &mut desc_ptr,
                &mut desc_len,
            )
        };

        if desc_res != 0 && !desc_ptr.is_null() && desc_len > 0 {
            unsafe {
                copy_description(desc_ptr, desc_len as usize, out_info);
            }
            found_desc = true;
        }
    }

    if !found_desc {
        // Fallback to common locale subblocks
        const FALLBACKS: &[&str] = &[
            r"\StringFileInfo\040904b0\FileDescription",
            r"\StringFileInfo\040904E4\FileDescription",
            r"\StringFileInfo\000004b0\FileDescription",
            r"\StringFileInfo\04090000\FileDescription",
        ];

        for fb in FALLBACKS {
            let fb_u16 = to_utf16_null(fb);
            let mut desc_ptr: *mut u16 = std::ptr::null_mut();
            let mut desc_len: u32 = 0;
            let desc_res = unsafe {
                VerQueryValueW(
                    data.as_ptr(),
                    fb_u16.as_ptr(),
                    &mut desc_ptr,
                    &mut desc_len,
                )
            };

            if desc_res != 0 && !desc_ptr.is_null() && desc_len > 0 {
                unsafe {
                    copy_description(desc_ptr, desc_len as usize, out_info);
                }
                break;
            }
        }
    }
}

unsafe fn copy_description(desc_ptr: *mut u16, len: usize, out_info: &mut NativePeInfo) {
    let slice = std::slice::from_raw_parts(desc_ptr, len);
    let trimmed_len = slice.iter().position(|&c| c == 0).unwrap_or(slice.len());
    let copy_len = std::cmp::min(trimmed_len, 259);
    out_info.description[..copy_len].copy_from_slice(&slice[..copy_len]);
    out_info.description[copy_len] = 0;
    out_info.description_len = copy_len as u32;
}

#[no_mangle]
pub unsafe extern "C" fn flow_extract_pe_info(
    path_ptr: *const u16,
    path_len: usize,
    out_info: *mut NativePeInfo,
) -> i32 {
    if path_ptr.is_null() || out_info.is_null() || path_len == 0 {
        return 0;
    }

    let path_slice = unsafe { std::slice::from_raw_parts(path_ptr, path_len) };
    let mut path_null = Vec::with_capacity(path_len + 1);
    path_null.extend_from_slice(path_slice);
    if *path_null.last().unwrap() != 0 {
        path_null.push(0);
    }

    let mut info: NativePeInfo = unsafe { std::mem::zeroed() };

    if let Ok(path_str) = String::from_utf16(path_slice) {
        let p = PathBuf::from(path_str);
        extract_pe_headers(&p, &mut info);
    }

    extract_file_description(&path_null, &mut info);

    unsafe {
        *out_info = info;
    }

    1
}
