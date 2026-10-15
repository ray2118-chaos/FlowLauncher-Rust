use crate::fuzzy::fuzzy_match_utf16;

#[derive(Debug, Clone)]
pub struct EmojiEntry {
    pub glyph: &'static str,
    pub name: &'static str,
    pub category: &'static str,
    pub keywords: &'static [&'static str],
    pub codepoint: &'static str,
}

pub struct EmojiMatcher;

impl EmojiMatcher {
    /// Calculate relevance score for an emoji candidate given a search query
    pub fn score(name: &str, keywords: &[&str], query: &str) -> i32 {
        let q = query.trim();
        if q.is_empty() {
            return 0;
        }

        let q_lower = q.to_lowercase();
        let name_lower = name.to_lowercase();

        // 1. Exact name match
        if name_lower == q_lower {
            return 400;
        }

        // 2. Exact word / prefix match in name
        if name_lower.starts_with(&q_lower) {
            return 300;
        }

        // 3. Name word boundary match (e.g. "ro" matches "Rocket")
        for word in name_lower.split_whitespace() {
            if word.starts_with(&q_lower) {
                return 280;
            }
        }

        // 4. Keyword exact match
        for kw in keywords {
            let kw_lower = kw.to_lowercase();
            if kw_lower == q_lower {
                return 350;
            }
        }

        // 5. Keyword prefix match
        for kw in keywords {
            let kw_lower = kw.to_lowercase();
            if kw_lower.starts_with(&q_lower) {
                return 220;
            }
        }

        // 6. Name contains substring
        if name_lower.contains(&q_lower) {
            return 200;
        }

        // 7. Keyword contains substring
        for kw in keywords {
            let kw_lower = kw.to_lowercase();
            if kw_lower.contains(&q_lower) {
                return 160;
            }
        }

        // 8. Fuzzy score fallback
        let name_u16: Vec<u16> = name.encode_utf16().collect();
        let q_u16: Vec<u16> = q.encode_utf16().collect();
        let mut indices = [0i32; 64];
        let (matched, score, _) = fuzzy_match_utf16(&q_u16, &name_u16, true, &mut indices);
        if matched {
            return score;
        }

        0
    }

    /// Check whether a search query matches the emoji name or any associated keywords
    pub fn matches(entry: &EmojiEntry, query: &str) -> bool {
        Self::score(entry.name, entry.keywords, query) > 0
    }
}

/// Zero-copy C-ABI fast scoring function for UTF-16 strings directly from C#
#[no_mangle]
pub unsafe extern "C" fn flow_emoji_score_utf16(
    name_ptr: *const u16,
    name_len: usize,
    query_ptr: *const u16,
    query_len: usize,
) -> i32 {
    if name_ptr.is_null() || query_ptr.is_null() || name_len == 0 || query_len == 0 {
        return 0;
    }
    let name_slice = std::slice::from_raw_parts(name_ptr, name_len);
    let query_slice = std::slice::from_raw_parts(query_ptr, query_len);

    let name = String::from_utf16_lossy(name_slice);
    let query = String::from_utf16_lossy(query_slice);

    EmojiMatcher::score(&name, &[], &query)
}

/// Full C-ABI scoring function accepting glyph, name, keywords buffer, and query in UTF-16
#[no_mangle]
pub unsafe extern "C" fn flow_emoji_score_full(
    glyph_ptr: *const u16,
    glyph_len: usize,
    name_ptr: *const u16,
    name_len: usize,
    query_ptr: *const u16,
    query_len: usize,
    kw_ptr: *const u16,
    kw_len: usize,
) -> i32 {
    if query_ptr.is_null() || query_len == 0 {
        return 0;
    }
    let query_slice = std::slice::from_raw_parts(query_ptr, query_len);
    let query = String::from_utf16_lossy(query_slice);
    let q = query.trim();
    if q.is_empty() {
        return 0;
    }

    // Direct glyph match (e.g. searching with the emoji symbol directly)
    if !glyph_ptr.is_null() && glyph_len > 0 {
        let glyph_slice = std::slice::from_raw_parts(glyph_ptr, glyph_len);
        if glyph_slice == query_slice {
            return 500;
        }
    }

    if name_ptr.is_null() || name_len == 0 {
        return 0;
    }

    let name_slice = std::slice::from_raw_parts(name_ptr, name_len);
    let name = String::from_utf16_lossy(name_slice);

    let kw_str = if !kw_ptr.is_null() && kw_len > 0 {
        let kw_slice = std::slice::from_raw_parts(kw_ptr, kw_len);
        String::from_utf16_lossy(kw_slice)
    } else {
        String::new()
    };

    let keywords: Vec<&str> = if kw_str.is_empty() {
        Vec::new()
    } else {
        kw_str.split_whitespace().collect()
    };

    EmojiMatcher::score(&name, &keywords, q)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_emoji_exact_match() {
        assert_eq!(EmojiMatcher::score("Rocket", &["space", "launch"], "rocket"), 400);
    }

    #[test]
    fn test_emoji_keyword_match() {
        assert_eq!(EmojiMatcher::score("Fire", &["flame", "hot", "lit"], "flame"), 350);
        assert_eq!(EmojiMatcher::score("Fire", &["flame", "hot", "lit"], "fla"), 220);
    }

    #[test]
    fn test_emoji_prefix_and_word_boundary() {
        assert_eq!(EmojiMatcher::score("Red Heart", &["love"], "red"), 300);
        assert_eq!(EmojiMatcher::score("Red Heart", &["love"], "hea"), 280);
    }

    #[test]
    fn test_flow_emoji_score_full() {
        let name: Vec<u16> = "Smiling Face".encode_utf16().collect();
        let query: Vec<u16> = "smil".encode_utf16().collect();
        let kw: Vec<u16> = "happy joy grin".encode_utf16().collect();

        unsafe {
            let score = flow_emoji_score_full(
                std::ptr::null(),
                0,
                name.as_ptr(),
                name.len(),
                query.as_ptr(),
                query.len(),
                kw.as_ptr(),
                kw.len(),
            );
            assert_eq!(score, 300);
        }
    }
}
