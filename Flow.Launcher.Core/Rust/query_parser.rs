/// Native Query Parser & Tokenizer
/// Fast zero-allocation query segmentation for action keywords and search terms.

#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct NativeQueryTerm {
    pub start: u32,
    pub length: u32,
}

#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct NativeParsedQuery {
    pub is_home_query: i32,
    pub has_action_keyword: i32,
    pub search_offset: u32,
    pub search_length: u32,
    pub term_count: u32,
}

fn is_whitespace(c: u16) -> bool {
    c == ' ' as u16 || c == '\t' as u16 || c == '\r' as u16 || c == '\n' as u16
}

/// Tokenizes a UTF-16 query string into search offsets and term ranges with zero heap allocations
#[no_mangle]
pub unsafe extern "C" fn flow_query_tokenize_utf16(
    query_ptr: *const u16,
    query_len: usize,
    action_keyword_ptr: *const u16,
    action_keyword_len: usize,
    out_terms: *mut NativeQueryTerm,
    max_terms: usize,
    out_parsed: *mut NativeParsedQuery,
) -> i32 {
    if out_parsed.is_null() {
        return 0;
    }

    let parsed = &mut *out_parsed;
    parsed.is_home_query = 0;
    parsed.has_action_keyword = 0;
    parsed.search_offset = 0;
    parsed.search_length = 0;
    parsed.term_count = 0;

    if query_ptr.is_null() || query_len == 0 {
        parsed.is_home_query = 1;
        return 1;
    }

    let query = std::slice::from_raw_parts(query_ptr, query_len);

    // Find first non-whitespace
    let mut trim_start = 0;
    while trim_start < query_len && is_whitespace(query[trim_start]) {
        trim_start += 1;
    }

    // Find last non-whitespace
    let mut trim_end = query_len;
    while trim_end > trim_start && is_whitespace(query[trim_end - 1]) {
        trim_end -= 1;
    }

    if trim_start >= trim_end {
        parsed.is_home_query = 1;
        return 1;
    }

    let trimmed = &query[trim_start..trim_end];
    let trimmed_len = trimmed.len();

    // Check if trimmed query starts with action keyword
    let mut matched_action_kw = false;
    if !action_keyword_ptr.is_null() && action_keyword_len > 0 && trimmed_len >= action_keyword_len {
        let kw_slice = std::slice::from_raw_parts(action_keyword_ptr, action_keyword_len);
        if &trimmed[..action_keyword_len] == kw_slice {
            // Must either be exact match or followed by whitespace
            if trimmed_len == action_keyword_len || is_whitespace(trimmed[action_keyword_len]) {
                matched_action_kw = true;
            }
        }
    }

    let mut term_count = 0usize;
    let terms_slice = if !out_terms.is_null() && max_terms > 0 {
        std::slice::from_raw_parts_mut(out_terms, max_terms)
    } else {
        &mut []
    };

    if matched_action_kw {
        parsed.has_action_keyword = 1;

        // Skip action keyword and following whitespace to find search part
        let mut s_idx = action_keyword_len;
        while s_idx < trimmed_len && is_whitespace(trimmed[s_idx]) {
            s_idx += 1;
        }

        parsed.search_offset = (trim_start + s_idx) as u32;
        parsed.search_length = (trimmed_len - s_idx) as u32;

        // Parse terms in the search part
        let mut idx = s_idx;
        while idx < trimmed_len && term_count < terms_slice.len() {
            while idx < trimmed_len && is_whitespace(trimmed[idx]) {
                idx += 1;
            }
            if idx >= trimmed_len {
                break;
            }
            let start = idx;
            while idx < trimmed_len && !is_whitespace(trimmed[idx]) {
                idx += 1;
            }
            terms_slice[term_count] = NativeQueryTerm {
                start: (trim_start + start) as u32,
                length: (idx - start) as u32,
            };
            term_count += 1;
        }
    } else {
        parsed.has_action_keyword = 0;
        parsed.search_offset = trim_start as u32;
        parsed.search_length = trimmed_len as u32;

        // Parse all terms in trimmed query
        let mut idx = 0;
        while idx < trimmed_len && term_count < terms_slice.len() {
            while idx < trimmed_len && is_whitespace(trimmed[idx]) {
                idx += 1;
            }
            if idx >= trimmed_len {
                break;
            }
            let start = idx;
            while idx < trimmed_len && !is_whitespace(trimmed[idx]) {
                idx += 1;
            }
            terms_slice[term_count] = NativeQueryTerm {
                start: (trim_start + start) as u32,
                length: (idx - start) as u32,
            };
            term_count += 1;
        }
    }

    parsed.term_count = term_count as u32;
    1
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_query_tokenize_home() {
        let mut parsed = NativeParsedQuery::default();
        let mut terms = [NativeQueryTerm::default(); 8];
        unsafe {
            let ok = flow_query_tokenize_utf16(
                std::ptr::null(),
                0,
                std::ptr::null(),
                0,
                terms.as_mut_ptr(),
                terms.len(),
                &mut parsed,
            );
            assert_eq!(ok, 1);
            assert_eq!(parsed.is_home_query, 1);
        }
    }

    #[test]
    fn test_query_tokenize_with_action_keyword() {
        let q: Vec<u16> = ">   ping    google.com   -n 20".encode_utf16().collect();
        let kw: Vec<u16> = ">".encode_utf16().collect();
        let mut parsed = NativeParsedQuery::default();
        let mut terms = [NativeQueryTerm::default(); 8];

        unsafe {
            let ok = flow_query_tokenize_utf16(
                q.as_ptr(),
                q.len(),
                kw.as_ptr(),
                kw.len(),
                terms.as_mut_ptr(),
                terms.len(),
                &mut parsed,
            );
            assert_eq!(ok, 1);
            assert_eq!(parsed.is_home_query, 0);
            assert_eq!(parsed.has_action_keyword, 1);
            assert_eq!(parsed.term_count, 4); // "ping", "google.com", "-n", "20"

            let first_term = &q[terms[0].start as usize..(terms[0].start + terms[0].length) as usize];
            assert_eq!(String::from_utf16_lossy(first_term), "ping");

            let second_term = &q[terms[1].start as usize..(terms[1].start + terms[1].length) as usize];
            assert_eq!(String::from_utf16_lossy(second_term), "google.com");
        }
    }

    #[test]
    fn test_query_tokenize_global_no_action_kw() {
        let q: Vec<u16> = "file.txt file2 file3".encode_utf16().collect();
        let mut parsed = NativeParsedQuery::default();
        let mut terms = [NativeQueryTerm::default(); 8];

        unsafe {
            let ok = flow_query_tokenize_utf16(
                q.as_ptr(),
                q.len(),
                std::ptr::null(),
                0,
                terms.as_mut_ptr(),
                terms.len(),
                &mut parsed,
            );
            assert_eq!(ok, 1);
            assert_eq!(parsed.has_action_keyword, 0);
            assert_eq!(parsed.term_count, 3);
        }
    }
}
