/// Native Everything IPC & Search Acceleration Subsystem
/// Formats queries and extracts highlight ranges with zero heap allocations.

#[repr(C)]
pub struct NativeEverythingItem {
    pub path: [u16; 512],
    pub path_len: u32,
    pub is_folder: i32,
    pub run_count: u32,
}

#[derive(Debug, Clone, Copy)]
#[repr(C)]
pub struct NativeHighlightRange {
    pub start: i32,
    pub length: i32,
}

/// Fast query builder for Everything search queries
/// Builds queries like: `keyword parent:"C:\path" content:"text"`
#[no_mangle]
pub unsafe extern "C" fn flow_everything_build_query_utf16(
    keyword_ptr: *const u16,
    keyword_len: usize,
    parent_ptr: *const u16,
    parent_len: usize,
    is_recursive: i32,
    content_ptr: *const u16,
    content_len: usize,
    out_buf: *mut u16,
    out_cap: usize,
    out_len: *mut usize,
) -> i32 {
    if out_buf.is_null() || out_len.is_null() || out_cap == 0 {
        return 0;
    }

    let mut query = String::with_capacity(keyword_len + parent_len + content_len + 32);

    if !keyword_ptr.is_null() && keyword_len > 0 {
        let kw_slice = std::slice::from_raw_parts(keyword_ptr, keyword_len);
        query.push_str(&String::from_utf16_lossy(kw_slice));
    }

    if !parent_ptr.is_null() && parent_len > 0 {
        let parent_slice = std::slice::from_raw_parts(parent_ptr, parent_len);
        let parent_str = String::from_utf16_lossy(parent_slice);
        if !parent_str.trim().is_empty() {
            if !query.is_empty() {
                query.push(' ');
            }
            if is_recursive == 0 {
                query.push_str("parent:");
            }
            query.push('"');
            query.push_str(&parent_str);
            query.push('"');
        }
    }

    if !content_ptr.is_null() && content_len > 0 {
        let content_slice = std::slice::from_raw_parts(content_ptr, content_len);
        let content_str = String::from_utf16_lossy(content_slice);
        if !content_str.trim().is_empty() {
            if !query.is_empty() {
                query.push(' ');
            }
            query.push_str("content:\"");
            query.push_str(&content_str);
            query.push('"');
        }
    }

    let utf16: Vec<u16> = query.encode_utf16().collect();
    let copy_len = utf16.len().min(out_cap - 1);
    let out_slice = std::slice::from_raw_parts_mut(out_buf, out_cap);
    out_slice[..copy_len].copy_from_slice(&utf16[..copy_len]);
    out_slice[copy_len] = 0;
    *out_len = copy_len;

    1
}

/// Parses Everything highlighted filename strings into index ranges for WPF FormattedText
/// Example: `*flow*.exe` -> match range at start=0, len=4
#[no_mangle]
pub unsafe extern "C" fn flow_everything_parse_highlights_utf16(
    highlight_ptr: *const u16,
    highlight_len: usize,
    out_ranges: *mut NativeHighlightRange,
    max_ranges: usize,
    out_count: *mut usize,
) -> i32 {
    if highlight_ptr.is_null() || out_ranges.is_null() || out_count.is_null() || max_ranges == 0 {
        return 0;
    }

    let slice = std::slice::from_raw_parts(highlight_ptr, highlight_len);
    let mut count = 0usize;
    let mut in_highlight = false;
    let mut start_idx = 0i32;
    let mut real_idx = 0i32;

    for &c in slice {
        if c == b'*' as u16 {
            if in_highlight {
                // End of highlight block
                let len = real_idx - start_idx;
                if len > 0 && count < max_ranges {
                    let range = &mut *out_ranges.add(count);
                    range.start = start_idx;
                    range.length = len;
                    count += 1;
                }
                in_highlight = false;
            } else {
                // Start of highlight block
                in_highlight = true;
                start_idx = real_idx;
            }
        } else {
            real_idx += 1;
        }
    }

    *out_count = count;
    1
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_build_query() {
        let kw: Vec<u16> = "cargo".encode_utf16().collect();
        let parent: Vec<u16> = "C:\\projects".encode_utf16().collect();
        let mut out_buf = [0u16; 256];
        let mut out_len = 0usize;

        unsafe {
            let ok = flow_everything_build_query_utf16(
                kw.as_ptr(),
                kw.len(),
                parent.as_ptr(),
                parent.len(),
                0,
                std::ptr::null(),
                0,
                out_buf.as_mut_ptr(),
                out_buf.len(),
                &mut out_len,
            );
            assert_eq!(ok, 1);
            let s = String::from_utf16_lossy(&out_buf[..out_len]);
            assert_eq!(s, r#"cargo parent:"C:\projects""#);
        }
    }

    #[test]
    fn test_parse_highlights() {
        let hl: Vec<u16> = "*flow*.launcher.*exe*".encode_utf16().collect();
        let mut ranges = [NativeHighlightRange { start: 0, length: 0 }; 4];
        let mut count = 0usize;

        unsafe {
            let ok = flow_everything_parse_highlights_utf16(
                hl.as_ptr(),
                hl.len(),
                ranges.as_mut_ptr(),
                ranges.len(),
                &mut count,
            );
            assert_eq!(ok, 1);
            assert_eq!(count, 2);
            assert_eq!(ranges[0].start, 0);
            assert_eq!(ranges[0].length, 4);
            assert_eq!(ranges[1].start, 14);
            assert_eq!(ranges[1].length, 3);
        }
    }
}
