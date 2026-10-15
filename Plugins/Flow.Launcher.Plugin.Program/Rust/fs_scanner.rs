use windows_sys::Win32::Foundation::INVALID_HANDLE_VALUE;
use windows_sys::Win32::Storage::FileSystem::{
    FindClose, FindFirstFileExW, FindNextFileW, FindExInfoBasic,
    FIND_FIRST_EX_LARGE_FETCH, FILE_ATTRIBUTE_DIRECTORY, FILE_ATTRIBUTE_REPARSE_POINT,
    WIN32_FIND_DATAW,
};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativeFileItem {
    pub path: [u16; 512],
    pub path_len: u32,
    pub is_directory: u32,
    pub file_size: u64,
    pub modified_time: u64,
}

#[inline(always)]
fn is_dot_or_dotdot(name: &[u16]) -> bool {
    (name.len() >= 1 && name[0] == b'.' as u16 && name.get(1).map_or(true, |&c| c == 0))
        || (name.len() >= 2 && name[0] == b'.' as u16 && name[1] == b'.' as u16 && name.get(2).map_or(true, |&c| c == 0))
}

pub(crate) fn scan_dir_internal(
    dir_path: &[u16],
    search_query: &[u16],
    recursive: bool,
    max_results: usize,
    out_items: &mut Vec<NativeFileItem>,
) {
    if out_items.len() >= max_results {
        return;
    }

    let mut search_spec = Vec::with_capacity(dir_path.len() + 3);
    search_spec.extend_from_slice(dir_path);
    if !search_spec.is_empty() && *search_spec.last().unwrap() != b'\\' as u16 && *search_spec.last().unwrap() != b'/' as u16 {
        search_spec.push(b'\\' as u16);
    }
    let base_len = search_spec.len();
    search_spec.push(b'*' as u16);
    search_spec.push(0);

    let mut find_data: WIN32_FIND_DATAW = unsafe { std::mem::zeroed() };

    let handle = unsafe {
        FindFirstFileExW(
            search_spec.as_ptr(),
            FindExInfoBasic,
            &mut find_data as *mut _ as *mut _,
            windows_sys::Win32::Storage::FileSystem::FindExSearchNameMatch,
            std::ptr::null(),
            FIND_FIRST_EX_LARGE_FETCH,
        )
    };

    if handle == INVALID_HANDLE_VALUE {
        return;
    }

    let mut subdirs: Vec<Vec<u16>> = Vec::new();

    loop {
        let name_len = find_data.cFileName.iter().position(|&c| c == 0).unwrap_or(find_data.cFileName.len());
        let name = &find_data.cFileName[..name_len];

        if !is_dot_or_dotdot(name) {
            let is_dir = (find_data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
            let is_reparse = (find_data.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0;

            let mut full_path = Vec::with_capacity(base_len + name_len);
            full_path.extend_from_slice(&search_spec[..base_len]);
            full_path.extend_from_slice(name);

            // Check if matches search_query (case-insensitive substring)
            let mut matches = search_query.is_empty();
            if !matches {
                // simple UTF-16 case-insensitive search
                let mut q_lower = Vec::with_capacity(search_query.len());
                for &c in search_query {
                    q_lower.push(if c >= b'A' as u16 && c <= b'Z' as u16 { c + 32 } else { c });
                }
                let mut n_lower = Vec::with_capacity(name.len());
                for &c in name {
                    n_lower.push(if c >= b'A' as u16 && c <= b'Z' as u16 { c + 32 } else { c });
                }

                if n_lower.windows(q_lower.len()).any(|w| w == q_lower.as_slice()) {
                    matches = true;
                }
            }

            if matches && out_items.len() < max_results {
                let mut item: NativeFileItem = unsafe { std::mem::zeroed() };
                let p_len = std::cmp::min(full_path.len(), 511);
                item.path[..p_len].copy_from_slice(&full_path[..p_len]);
                item.path[p_len] = 0;
                item.path_len = p_len as u32;
                item.is_directory = if is_dir { 1 } else { 0 };
                item.file_size = ((find_data.nFileSizeHigh as u64) << 32) | (find_data.nFileSizeLow as u64);
                item.modified_time = ((find_data.ftLastWriteTime.dwHighDateTime as u64) << 32) | (find_data.ftLastWriteTime.dwLowDateTime as u64);

                out_items.push(item);
            }

            if recursive && is_dir && !is_reparse && out_items.len() < max_results {
                subdirs.push(full_path);
            }
        }

        let next = unsafe { FindNextFileW(handle, &mut find_data) };
        if next == 0 {
            break;
        }
    }

    unsafe {
        FindClose(handle);
    }

    if recursive {
        for subdir in subdirs {
            scan_dir_internal(&subdir, search_query, true, max_results, out_items);
            if out_items.len() >= max_results {
                break;
            }
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn flow_scan_directory(
    dir_ptr: *const u16,
    dir_len: usize,
    search_ptr: *const u16,
    search_len: usize,
    recursive: i32,
    max_results: usize,
    out_items: *mut NativeFileItem,
    out_count: *mut usize,
) -> i32 {
    if dir_ptr.is_null() || out_items.is_null() || out_count.is_null() || max_results == 0 {
        return 0;
    }

    let dir = unsafe { std::slice::from_raw_parts(dir_ptr, dir_len) };
    let search = if !search_ptr.is_null() && search_len > 0 {
        unsafe { std::slice::from_raw_parts(search_ptr, search_len) }
    } else {
        &[]
    };

    let mut results: Vec<NativeFileItem> = Vec::with_capacity(std::cmp::min(max_results, 512));
    scan_dir_internal(dir, search, recursive != 0, max_results, &mut results);

    let count = std::cmp::min(results.len(), max_results);
    unsafe {
        let out_slice = std::slice::from_raw_parts_mut(out_items, count);
        out_slice.copy_from_slice(&results[..count]);
        *out_count = count;
    }

    1
}
