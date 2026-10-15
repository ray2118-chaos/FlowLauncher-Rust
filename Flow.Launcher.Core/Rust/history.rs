/// Native History & User Selection Frequency Engine
/// Fast zero-allocation hash computation and in-memory MRU/frequency weighting.

use std::collections::HashMap;
use std::sync::{OnceLock, RwLock};

pub const HASH_MULTIPLIER: i32 = 31;
pub const HASH_INITIAL: i32 = 23;

#[inline(always)]
fn hash_utf16_slice(slice: &[u16], mut start: i32) -> i32 {
    for &ch in slice {
        start = start.wrapping_mul(HASH_MULTIPLIER).wrapping_add(ch as i32);
    }
    start
}

/// Generates hash code compatible with Flow Launcher's UserSelectedRecord
#[no_mangle]
pub unsafe extern "C" fn flow_history_hash_utf16(
    str_ptr: *const u16,
    str_len: usize,
    initial: i32,
) -> i32 {
    if str_ptr.is_null() || str_len == 0 {
        return initial;
    }

    let slice = std::slice::from_raw_parts(str_ptr, str_len);
    hash_utf16_slice(slice, initial)
}

static HISTORY_MAP: OnceLock<RwLock<HashMap<i32, i32>>> = OnceLock::new();

fn get_history() -> &'static RwLock<HashMap<i32, i32>> {
    HISTORY_MAP.get_or_init(|| RwLock::new(HashMap::new()))
}

#[no_mangle]
pub unsafe extern "C" fn flow_history_clear() -> i32 {
    if let Ok(mut records) = get_history().write() {
        records.clear();
        1
    } else {
        0
    }
}

#[no_mangle]
pub unsafe extern "C" fn flow_history_record_add(key_with_query: i32, key_without_query: i32) -> i32 {
    if let Ok(mut records) = get_history().write() {
        *records.entry(key_with_query).or_insert(0) += 1;
        *records.entry(key_without_query).or_insert(0) += 1;
        1
    } else {
        0
    }
}

#[no_mangle]
pub unsafe extern "C" fn flow_history_get_count(key_with_query: i32, key_without_query: i32) -> i32 {
    if let Ok(records) = get_history().read() {
        let count_with_query = records.get(&key_with_query).copied().unwrap_or(0);
        let count_without_query = records.get(&key_without_query).copied().unwrap_or(0);
        count_with_query * 5 + count_without_query
    } else {
        0
    }
}

#[no_mangle]
pub unsafe extern "C" fn flow_history_record_count() -> usize {
    if let Ok(records) = get_history().read() {
        records.len()
    } else {
        0
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_history_hash() {
        let text: Vec<u16> = "Terminal".encode_utf16().collect();
        let hash = unsafe { flow_history_hash_utf16(text.as_ptr(), text.len(), HASH_INITIAL) };
        assert_ne!(hash, HASH_INITIAL);

        // Deterministic check
        let hash2 = unsafe { flow_history_hash_utf16(text.as_ptr(), text.len(), HASH_INITIAL) };
        assert_eq!(hash, hash2);
    }

    #[test]
    fn test_history_frequency_mru() {
        unsafe {
            flow_history_clear();
            assert_eq!(flow_history_record_count(), 0);

            flow_history_record_add(101, 202);
            flow_history_record_add(101, 202);

            let score = flow_history_get_count(101, 202);
            // 2 * 5 + 2 = 12
            assert_eq!(score, 12);

            flow_history_clear();
            assert_eq!(flow_history_get_count(101, 202), 0);
        }
    }
}
