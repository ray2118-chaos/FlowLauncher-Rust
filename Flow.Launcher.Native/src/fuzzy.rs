/// Native Fuzzy & Acronym Matcher for Flow Launcher
/// Operates directly on UTF-16 code units with zero heap allocations during query execution.

#[repr(C)]
pub struct FuzzyMatchResult {
    pub success: i32,
    pub score: i32,
    pub match_count: u32,
}

#[inline(always)]
fn is_word_separator(c: u16) -> bool {
    c == b' ' as u16 || c == b'_' as u16 || c == b'-' as u16 || c == b'.' as u16 || c == b'/' as u16 || c == b'\\' as u16
}

#[inline(always)]
fn to_lower_ascii(c: u16) -> u16 {
    if c >= b'A' as u16 && c <= b'Z' as u16 {
        c + 32
    } else {
        c
    }
}

pub fn fuzzy_match_utf16(
    query: &[u16],
    target: &[u16],
    ignore_case: bool,
    out_indices: &mut [i32],
) -> (bool, i32, usize) {
    if query.is_empty() || target.is_empty() {
        return (false, 0, 0);
    }

    let mut q_idx = 0usize;
    let mut t_idx = 0usize;
    let mut match_count = 0usize;
    let mut score: i32 = 0;
    let mut consecutive = 0i32;

    while q_idx < query.len() && t_idx < target.len() {
        let q_char = if ignore_case { to_lower_ascii(query[q_idx]) } else { query[q_idx] };
        let t_char = if ignore_case { to_lower_ascii(target[t_idx]) } else { target[t_idx] };

        if q_char == t_char {
            if match_count < out_indices.len() {
                out_indices[match_count] = t_idx as i32;
                match_count += 1;
            }

            // Scoring bonuses
            let mut bonus = 10i32;

            // Start of word bonus
            if t_idx == 0 || is_word_separator(target[t_idx - 1]) {
                bonus += 30;
            } else if target[t_idx] >= b'A' as u16 && target[t_idx] <= b'Z' as u16 {
                // CamelCase boundary
                bonus += 20;
            }

            // Consecutive matches bonus
            if consecutive > 0 {
                bonus += consecutive * 15;
            }

            score += bonus;
            consecutive += 1;
            q_idx += 1;
        } else {
            consecutive = 0;
        }
        t_idx += 1;
    }

    if q_idx == query.len() {
        // Full query matched
        // Normalize score between 0 and 100+
        let length_penalty = (target.len() as i32 - query.len() as i32).max(0);
        let final_score = (score - length_penalty).max(1);
        (true, final_score, match_count)
    } else {
        (false, 0, 0)
    }
}

#[no_mangle]
pub unsafe extern "C" fn flow_fuzzy_match(
    query_ptr: *const u16,
    query_len: usize,
    target_ptr: *const u16,
    target_len: usize,
    ignore_case: i32,
    out_indices: *mut i32,
    max_indices: usize,
    out_result: *mut FuzzyMatchResult,
) -> i32 {
    if query_ptr.is_null() || target_ptr.is_null() || out_result.is_null() {
        return 0;
    }

    let query = unsafe { std::slice::from_raw_parts(query_ptr, query_len) };
    let target = unsafe { std::slice::from_raw_parts(target_ptr, target_len) };
    let indices_slice = if !out_indices.is_null() && max_indices > 0 {
        unsafe { std::slice::from_raw_parts_mut(out_indices, max_indices) }
    } else {
        &mut []
    };

    let (matched, score, count) = fuzzy_match_utf16(query, target, ignore_case != 0, indices_slice);

    unsafe {
        (*out_result).success = if matched { 1 } else { 0 };
        (*out_result).score = score;
        (*out_result).match_count = count as u32;
    }

    1
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativeTargetItem {
    pub ptr: *const u16,
    pub len: usize,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativeMatchScore {
    pub index: u32,
    pub score: i32,
}

#[no_mangle]
pub unsafe extern "C" fn flow_fuzzy_match_batch(
    query_ptr: *const u16,
    query_len: usize,
    targets_ptr: *const NativeTargetItem,
    target_count: usize,
    ignore_case: i32,
    min_score: i32,
    out_scores: *mut NativeMatchScore,
    max_results: usize,
    out_count: *mut usize,
) -> i32 {
    if query_ptr.is_null() || targets_ptr.is_null() || out_scores.is_null() || out_count.is_null() {
        return 0;
    }

    let query = unsafe { std::slice::from_raw_parts(query_ptr, query_len) };
    let targets = unsafe { std::slice::from_raw_parts(targets_ptr, target_count) };

    let mut matched_items: Vec<NativeMatchScore> = Vec::with_capacity(std::cmp::min(target_count, max_results.max(64)));

    for (i, target_item) in targets.iter().enumerate() {
        if target_item.ptr.is_null() || target_item.len == 0 {
            continue;
        }
        let target = unsafe { std::slice::from_raw_parts(target_item.ptr, target_item.len) };
        let (matched, score, _) = fuzzy_match_utf16(query, target, ignore_case != 0, &mut []);
        if matched && score >= min_score {
            matched_items.push(NativeMatchScore {
                index: i as u32,
                score,
            });
        }
    }

    // Sort descending by score
    matched_items.sort_unstable_by(|a, b| b.score.cmp(&a.score));

    let to_copy = std::cmp::min(matched_items.len(), max_results);
    unsafe {
        let out_slice = std::slice::from_raw_parts_mut(out_scores, to_copy);
        out_slice.copy_from_slice(&matched_items[..to_copy]);
        *out_count = to_copy;
    }

    1
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_fuzzy_match() {
        let q: Vec<u16> = "chr".encode_utf16().collect();
        let t: Vec<u16> = "Google Chrome".encode_utf16().collect();
        let mut indices = vec![0i32; 10];
        let (matched, score, count) = fuzzy_match_utf16(&q, &t, true, &mut indices);
        assert!(matched);
        assert!(score > 0);
        assert_eq!(count, 3);
    }
}
