/// Native Shell Subsystem for Flow Launcher
/// Provides blazing fast environment expansion, command tokenization, and Win32 path autocomplete.

#[repr(C)]
pub struct NativeShellEntry {
    pub name: [u16; 260],
    pub name_len: u32,
    pub is_directory: i32,
}

#[cfg(windows)]
extern "system" {
    fn ExpandEnvironmentStringsW(
        lpSrc: *const u16,
        lpDst: *mut u16,
        nSize: u32,
    ) -> u32;

    fn FindFirstFileW(
        lpFileName: *const u16,
        lpFindFileData: *mut WIN32_FIND_DATAW,
    ) -> isize;

    fn FindNextFileW(
        hFindFile: isize,
        lpFindFileData: *mut WIN32_FIND_DATAW,
    ) -> i32;

    fn FindClose(hFindFile: isize) -> i32;
}

#[cfg(windows)]
#[repr(C)]
#[allow(non_snake_case)]
struct WIN32_FIND_DATAW {
    dwFileAttributes: u32,
    ftCreationTime: [u32; 2],
    ftLastAccessTime: [u32; 2],
    ftLastWriteTime: [u32; 2],
    nFileSizeHigh: u32,
    nFileSizeLow: u32,
    dwReserved0: u32,
    dwReserved1: u32,
    cFileName: [u16; 260],
    cAlternateFileName: [u16; 14],
}

const FILE_ATTRIBUTE_DIRECTORY: u32 = 0x00000010;
const INVALID_HANDLE_VALUE: isize = -1;

/// Zero-copy environment expansion using Win32 ExpandEnvironmentStringsW
#[no_mangle]
pub unsafe extern "C" fn flow_shell_expand_env_utf16(
    src_ptr: *const u16,
    src_len: usize,
    dst_buf: *mut u16,
    dst_cap: usize,
    out_len: *mut usize,
) -> i32 {
    if src_ptr.is_null() || dst_buf.is_null() || out_len.is_null() || dst_cap == 0 {
        return 0;
    }

    #[cfg(windows)]
    {
        // Null-terminate src in temporary stack buffer if needed
        let mut null_terminated_src = [0u16; 1024];
        let copy_len = src_len.min(1023);
        let src_slice = std::slice::from_raw_parts(src_ptr, copy_len);
        null_terminated_src[..copy_len].copy_from_slice(src_slice);
        null_terminated_src[copy_len] = 0;

        let res = ExpandEnvironmentStringsW(
            null_terminated_src.as_ptr(),
            dst_buf,
            dst_cap as u32,
        );

        if res > 0 && (res as usize) <= dst_cap {
            *out_len = (res - 1) as usize; // Exclude null terminator
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

/// Enumerates filesystem entries in a directory using native Win32 FindFirstFileW
#[no_mangle]
pub unsafe extern "C" fn flow_shell_autocomplete_dir(
    dir_ptr: *const u16,
    dir_len: usize,
    filter_prefix_ptr: *const u16,
    filter_prefix_len: usize,
    out_entries: *mut NativeShellEntry,
    max_entries: usize,
    out_count: *mut usize,
) -> i32 {
    if dir_ptr.is_null() || out_entries.is_null() || out_count.is_null() || max_entries == 0 {
        return 0;
    }

    #[cfg(windows)]
    {
        let dir_slice = std::slice::from_raw_parts(dir_ptr, dir_len);
        let mut search_pattern = Vec::with_capacity(dir_len + 4);
        search_pattern.extend_from_slice(dir_slice);

        // Ensure trailing slash
        if !search_pattern.ends_with(&[b'\\' as u16]) && !search_pattern.ends_with(&[b'/' as u16]) {
            search_pattern.push(b'\\' as u16);
        }
        search_pattern.push(b'*' as u16);
        search_pattern.push(0);

        let filter_prefix: Vec<u16> = if !filter_prefix_ptr.is_null() && filter_prefix_len > 0 {
            let slice = std::slice::from_raw_parts(filter_prefix_ptr, filter_prefix_len);
            slice.iter().map(|&c| if c >= b'A' as u16 && c <= b'Z' as u16 { c + 32 } else { c }).collect()
        } else {
            Vec::new()
        };

        let mut find_data: WIN32_FIND_DATAW = std::mem::zeroed();
        let handle = FindFirstFileW(search_pattern.as_ptr(), &mut find_data);
        if handle == INVALID_HANDLE_VALUE {
            *out_count = 0;
            return 1;
        }

        let mut count = 0usize;
        loop {
            // Skip "." and ".."
            let name_first = find_data.cFileName[0];
            let name_second = find_data.cFileName[1];
            let is_dot = name_first == b'.' as u16 && (name_second == 0 || (name_second == b'.' as u16 && find_data.cFileName[2] == 0));

            if !is_dot {
                // Find length of filename
                let mut name_len = 0usize;
                while name_len < 260 && find_data.cFileName[name_len] != 0 {
                    name_len += 1;
                }

                // Check prefix match if filter is provided
                let mut matches = true;
                if !filter_prefix.is_empty() {
                    if name_len < filter_prefix.len() {
                        matches = false;
                    } else {
                        for i in 0..filter_prefix.len() {
                            let mut c = find_data.cFileName[i];
                            if c >= b'A' as u16 && c <= b'Z' as u16 {
                                c += 32;
                            }
                            if c != filter_prefix[i] {
                                matches = false;
                                break;
                            }
                        }
                    }
                }

                if matches {
                    let entry = &mut *out_entries.add(count);
                    entry.name[..name_len].copy_from_slice(&find_data.cFileName[..name_len]);
                    if name_len < 260 {
                        entry.name[name_len] = 0;
                    }
                    entry.name_len = name_len as u32;
                    entry.is_directory = if (find_data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0 { 1 } else { 0 };

                    count += 1;
                    if count >= max_entries {
                        break;
                    }
                }
            }

            if FindNextFileW(handle, &mut find_data) == 0 {
                break;
            }
        }

        FindClose(handle);
        *out_count = count;
        1
    }

    #[cfg(not(windows))]
    {
        0
    }
}

/// Tokenize a shell command line respecting quotes
pub fn tokenize_command(cmd: &str) -> (String, Vec<String>) {
    let mut parts = Vec::new();
    let mut current = String::new();
    let mut in_quotes = false;
    let mut chars = cmd.trim().chars().peekable();

    while let Some(c) = chars.next() {
        match c {
            '"' => in_quotes = !in_quotes,
            ' ' | '\t' if !in_quotes => {
                if !current.is_empty() {
                    parts.push(std::mem::take(&mut current));
                }
            }
            _ => current.push(c),
        }
    }
    if !current.is_empty() {
        parts.push(current);
    }

    if parts.is_empty() {
        (String::new(), Vec::new())
    } else {
        let program = parts.remove(0);
        (program, parts)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_tokenize_command() {
        let (prog, args) = tokenize_command(r#"git commit -m "initial commit" --amend"#);
        assert_eq!(prog, "git");
        assert_eq!(args, vec!["commit", "-m", "initial commit", "--amend"]);
    }
}
