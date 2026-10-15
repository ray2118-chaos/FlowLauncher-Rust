use std::path::{Path, PathBuf};
use crate::lnk::parse_lnk_bytes;
use crate::pe::{flow_extract_pe_info, NativePeInfo};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativeIndexedProgram {
    pub name: [u16; 260],
    pub name_len: u32,
    pub full_path: [u16; 512],
    pub full_path_len: u32,
    pub target_path: [u16; 512],
    pub target_path_len: u32,
    pub args: [u16; 260],
    pub args_len: u32,
    pub description: [u16; 260],
    pub description_len: u32,
    pub program_type: u32, // 0 = Exe, 1 = Lnk, 2 = Url, 3 = AppPaths
}

impl Default for NativeIndexedProgram {
    fn default() -> Self {
        Self {
            name: [0; 260],
            name_len: 0,
            full_path: [0; 512],
            full_path_len: 0,
            target_path: [0; 512],
            target_path_len: 0,
            args: [0; 260],
            args_len: 0,
            description: [0; 260],
            description_len: 0,
            program_type: 0,
        }
    }
}

fn copy_str_to_u16_buf(text: &str, buf: &mut [u16], len: &mut u32) {
    let utf16: Vec<u16> = text.encode_utf16().collect();
    let copy_len = std::cmp::min(utf16.len(), buf.len() - 1);
    buf[..copy_len].copy_from_slice(&utf16[..copy_len]);
    buf[copy_len] = 0;
    *len = copy_len as u32;
}

fn copy_u16_slice_to_buf(slice: &[u16], buf: &mut [u16], len: &mut u32) {
    let copy_len = std::cmp::min(slice.len(), buf.len() - 1);
    buf[..copy_len].copy_from_slice(&slice[..copy_len]);
    buf[copy_len] = 0;
    *len = copy_len as u32;
}

fn process_program_file(path: &Path, ext: &str, out_programs: &mut Vec<NativeIndexedProgram>) {
    let mut program = NativeIndexedProgram::default();
    let full_path_str = path.to_string_lossy();
    copy_str_to_u16_buf(&full_path_str, &mut program.full_path, &mut program.full_path_len);

    let file_stem = path.file_stem().and_then(|s| s.to_str()).unwrap_or("");
    copy_str_to_u16_buf(file_stem, &mut program.name, &mut program.name_len);

    if ext == "lnk" {
        program.program_type = 1;
        if let Ok(bytes) = std::fs::read(path) {
            if let Some((target, desc, args)) = parse_lnk_bytes(&bytes) {
                if !target.is_empty() {
                    let target_path = Path::new(&target);
                    if target_path.exists() {
                        copy_str_to_u16_buf(&target, &mut program.target_path, &mut program.target_path_len);
                        copy_str_to_u16_buf(&args, &mut program.args, &mut program.args_len);

                        if !desc.is_empty() {
                            copy_str_to_u16_buf(&desc, &mut program.description, &mut program.description_len);
                        } else if target.to_lowercase().ends_with(".exe") {
                            let mut pe_info: NativePeInfo = unsafe { std::mem::zeroed() };
                            let target_u16: Vec<u16> = target.encode_utf16().collect();
                            unsafe {
                                flow_extract_pe_info(target_u16.as_ptr(), target_u16.len(), &mut pe_info);
                            }
                            if pe_info.description_len > 0 {
                                copy_u16_slice_to_buf(
                                    &pe_info.description[..pe_info.description_len as usize],
                                    &mut program.description,
                                    &mut program.description_len,
                                );
                            }
                        }
                    } else {
                        return; // Shortcut target missing
                    }
                }
            }
        }
    } else if ext == "exe" {
        program.program_type = 0;
        copy_str_to_u16_buf(&full_path_str, &mut program.target_path, &mut program.target_path_len);

        let mut pe_info: NativePeInfo = unsafe { std::mem::zeroed() };
        let path_u16: Vec<u16> = full_path_str.encode_utf16().collect();
        unsafe {
            flow_extract_pe_info(path_u16.as_ptr(), path_u16.len(), &mut pe_info);
        }
        if pe_info.description_len > 0 {
            copy_u16_slice_to_buf(
                &pe_info.description[..pe_info.description_len as usize],
                &mut program.description,
                &mut program.description_len,
            );
        }
    } else if ext == "url" {
        program.program_type = 2;
    }

    out_programs.push(program);
}

fn scan_dir_recursive(
    dir_path: &Path,
    suffixes: &[String],
    out_programs: &mut Vec<NativeIndexedProgram>,
    max_items: usize,
) {
    if out_programs.len() >= max_items {
        return;
    }

    let entries = match std::fs::read_dir(dir_path) {
        Ok(e) => e,
        Err(_) => return,
    };

    for entry in entries.flatten() {
        if out_programs.len() >= max_items {
            break;
        }

        let path = entry.path();
        if path.is_dir() {
            scan_dir_recursive(&path, suffixes, out_programs, max_items);
        } else if path.is_file() {
            let ext = match path.extension().and_then(|s| s.to_str()) {
                Some(e) => e.to_lowercase(),
                None => continue,
            };

            if !suffixes.iter().any(|s| s.eq_ignore_ascii_case(&ext)) {
                continue;
            }

            process_program_file(&path, &ext, out_programs);
        }
    }
}

/// Recursively scans given directories for installed programs, resolving shortcuts
/// and PE metadata in parallel with zero COM overhead.
#[no_mangle]
pub unsafe extern "C" fn flow_scan_programs_in_dirs(
    dir_paths_utf16: *const *const u16,
    dir_lens: *const usize,
    dir_count: usize,
    suffixes_utf16: *const *const u16,
    suffix_lens: *const usize,
    suffix_count: usize,
    out_items: *mut NativeIndexedProgram,
    max_items: usize,
    out_count: *mut usize,
) -> i32 {
    if dir_paths_utf16.is_null() || dir_count == 0 || out_items.is_null() || out_count.is_null() || max_items == 0 {
        return 0;
    }

    let mut suffixes: Vec<String> = Vec::new();
    if !suffixes_utf16.is_null() && suffix_count > 0 {
        for i in 0..suffix_count {
            let s_ptr = *suffixes_utf16.add(i);
            let s_len = *suffix_lens.add(i);
            if !s_ptr.is_null() && s_len > 0 {
                let slice = std::slice::from_raw_parts(s_ptr, s_len);
                if let Ok(s) = String::from_utf16(slice) {
                    suffixes.push(s.to_lowercase());
                }
            }
        }
    }

    if suffixes.is_empty() {
        suffixes = vec!["lnk".to_string(), "exe".to_string(), "url".to_string()];
    }

    let mut out_programs = Vec::with_capacity(std::cmp::min(max_items, 1024));

    for i in 0..dir_count {
        if out_programs.len() >= max_items {
            break;
        }

        let d_ptr = *dir_paths_utf16.add(i);
        let d_len = *dir_lens.add(i);
        if !d_ptr.is_null() && d_len > 0 {
            let slice = std::slice::from_raw_parts(d_ptr, d_len);
            if let Ok(d_str) = String::from_utf16(slice) {
                let path = PathBuf::from(d_str);
                if path.is_dir() {
                    scan_dir_recursive(&path, &suffixes, &mut out_programs, max_items);
                }
            }
        }
    }

    let return_count = std::cmp::min(out_programs.len(), max_items);
    let out_slice = std::slice::from_raw_parts_mut(out_items, return_count);
    out_slice.copy_from_slice(&out_programs[..return_count]);

    *out_count = return_count;
    1
}
