use xxhash_rust::xxh3::{xxh3_64, xxh3_128};

/// Hashes raw byte buffer using XXH3 64-bit algorithm.
/// Extremely fast: ~15 GB/s on modern x86_64 CPUs.
#[no_mangle]
pub unsafe extern "C" fn flow_hash_xxh3_64(data: *const u8, len: usize) -> u64 {
    if data.is_null() || len == 0 {
        return 0;
    }
    let slice = unsafe { std::slice::from_raw_parts(data, len) };
    xxh3_64(slice)
}

/// Hashes raw byte buffer using XXH3 128-bit algorithm, writing the 16 bytes into out_hash.
#[no_mangle]
pub unsafe extern "C" fn flow_hash_xxh3_128(data: *const u8, len: usize, out_hash: *mut u8) -> i32 {
    if data.is_null() || len == 0 || out_hash.is_null() {
        return 0;
    }
    let slice = unsafe { std::slice::from_raw_parts(data, len) };
    let hash = xxh3_128(slice);
    let bytes = hash.to_le_bytes();
    unsafe {
        std::ptr::copy_nonoverlapping(bytes.as_ptr(), out_hash, 16);
    }
    1
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_xxh3_64() {
        let data = b"Flow Launcher Test String";
        let hash = unsafe { flow_hash_xxh3_64(data.as_ptr(), data.len()) };
        assert_ne!(hash, 0);

        let data2 = b"Flow Launcher Test String";
        let hash2 = unsafe { flow_hash_xxh3_64(data2.as_ptr(), data2.len()) };
        assert_eq!(hash, hash2);
    }
}
