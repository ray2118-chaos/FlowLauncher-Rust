/// Native LZ4 Fast Block Compressor & Decompressor
/// High-speed zero-allocation compression for icon, image, and query cache blocks.

const HASH_LOG: usize = 12;
const HASH_SIZE: usize = 1 << HASH_LOG;
const MIN_MATCH: usize = 4;

#[inline(always)]
fn hash4(bytes: &[u8]) -> usize {
    let val = u32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]]);
    ((val.wrapping_mul(2654435761)) >> (32 - HASH_LOG)) as usize
}

/// Compresses a raw byte buffer using standard LZ4 block encoding
pub fn lz4_compress_slice(src: &[u8], dst: &mut [u8]) -> Option<usize> {
    if src.is_empty() {
        return Some(0);
    }
    if dst.len() < src.len() / 4 + 16 {
        return None;
    }

    let mut hash_table = [0usize; HASH_SIZE];
    let mut ip = 0usize;
    let mut anchor = 0usize;
    let mut op = 0usize;
    let src_len = src.len();
    let mflimit = if src_len > 12 { src_len - 12 } else { 0 };

    while ip < mflimit {
        let h = hash4(&src[ip..ip + 4]);
        let ref_pos = hash_table[h];
        hash_table[h] = ip;

        // Check for match
        if ref_pos > 0 && ip - ref_pos < 65535 && src[ref_pos..ref_pos + 4] == src[ip..ip + 4] {
            // Find match length
            let mut match_len = MIN_MATCH;
            while ip + match_len < src_len && src[ref_pos + match_len] == src[ip + match_len] {
                match_len += 1;
            }

            // Encode literal length
            let lit_len = ip - anchor;
            let token_pos = op;
            if op >= dst.len() {
                return None;
            }
            op += 1;

            let mut token = 0u8;
            if lit_len >= 15 {
                token |= 0xF0;
                let mut rem = lit_len - 15;
                while rem >= 255 {
                    if op >= dst.len() { return None; }
                    dst[op] = 255;
                    op += 1;
                    rem -= 255;
                }
                if op >= dst.len() { return None; }
                dst[op] = rem as u8;
                op += 1;
            } else {
                token |= ((lit_len as u8) << 4) & 0xF0;
            }

            // Copy literals
            if op + lit_len > dst.len() {
                return None;
            }
            dst[op..op + lit_len].copy_from_slice(&src[anchor..anchor + lit_len]);
            op += lit_len;

            // Encode match offset (16-bit little endian)
            let offset = (ip - ref_pos) as u16;
            if op + 2 > dst.len() {
                return None;
            }
            dst[op] = (offset & 0xFF) as u8;
            dst[op + 1] = ((offset >> 8) & 0xFF) as u8;
            op += 2;

            // Encode match length
            let match_len_token = match_len - MIN_MATCH;
            if match_len_token >= 15 {
                token |= 0x0F;
                let mut rem = match_len_token - 15;
                while rem >= 255 {
                    if op >= dst.len() { return None; }
                    dst[op] = 255;
                    op += 1;
                    rem -= 255;
                }
                if op >= dst.len() { return None; }
                dst[op] = rem as u8;
                op += 1;
            } else {
                token |= (match_len_token as u8) & 0x0F;
            }

            dst[token_pos] = token;

            ip += match_len;
            anchor = ip;
        } else {
            ip += 1;
        }
    }

    // Write final literals
    let last_literals = src_len - anchor;
    if op >= dst.len() {
        return None;
    }
    let token_pos = op;
    op += 1;

    let mut token = 0u8;
    if last_literals >= 15 {
        token |= 0xF0;
        let mut rem = last_literals - 15;
        while rem >= 255 {
            if op >= dst.len() { return None; }
            dst[op] = 255;
            op += 1;
            rem -= 255;
        }
        if op >= dst.len() { return None; }
        dst[op] = rem as u8;
        op += 1;
    } else {
        token |= ((last_literals as u8) << 4) & 0xF0;
    }
    dst[token_pos] = token;

    if op + last_literals > dst.len() {
        return None;
    }
    dst[op..op + last_literals].copy_from_slice(&src[anchor..src_len]);
    op += last_literals;

    Some(op)
}

/// Decompresses an LZ4 block buffer into the destination slice
pub fn lz4_decompress_slice(src: &[u8], dst: &mut [u8]) -> Option<usize> {
    if src.is_empty() {
        return Some(0);
    }

    let mut ip = 0usize;
    let mut op = 0usize;
    let src_len = src.len();
    let dst_len = dst.len();

    while ip < src_len {
        let token = src[ip];
        ip += 1;

        // 1. Literal length
        let mut lit_len = ((token >> 4) & 0x0F) as usize;
        if lit_len == 15 {
            while ip < src_len {
                let s = src[ip] as usize;
                ip += 1;
                lit_len += s;
                if s != 255 {
                    break;
                }
            }
        }

        // Copy literals
        if ip + lit_len > src_len || op + lit_len > dst_len {
            return None;
        }
        dst[op..op + lit_len].copy_from_slice(&src[ip..ip + lit_len]);
        ip += lit_len;
        op += lit_len;

        if ip >= src_len {
            break;
        }

        // 2. Match offset
        if ip + 2 > src_len {
            return None;
        }
        let offset = (src[ip] as usize) | ((src[ip + 1] as usize) << 8);
        ip += 2;
        if offset == 0 || offset > op {
            return None;
        }

        // 3. Match length
        let mut match_len = (token & 0x0F) as usize + MIN_MATCH;
        if (token & 0x0F) == 15 {
            while ip < src_len {
                let s = src[ip] as usize;
                ip += 1;
                match_len += s;
                if s != 255 {
                    break;
                }
            }
        }

        if op + match_len > dst_len {
            return None;
        }

        // Copy match bytes (accounting for potential overlap)
        let match_start = op - offset;
        for i in 0..match_len {
            dst[op + i] = dst[match_start + i];
        }
        op += match_len;
    }

    Some(op)
}

#[no_mangle]
pub unsafe extern "C" fn flow_lz4_compress(
    src_ptr: *const u8,
    src_len: usize,
    dst_buf: *mut u8,
    dst_cap: usize,
    out_len: *mut usize,
) -> i32 {
    if src_ptr.is_null() || dst_buf.is_null() || out_len.is_null() || dst_cap == 0 {
        return 0;
    }

    let src = std::slice::from_raw_parts(src_ptr, src_len);
    let dst = std::slice::from_raw_parts_mut(dst_buf, dst_cap);

    match lz4_compress_slice(src, dst) {
        Some(len) => {
            *out_len = len;
            1
        }
        None => 0,
    }
}

#[no_mangle]
pub unsafe extern "C" fn flow_lz4_decompress(
    src_ptr: *const u8,
    src_len: usize,
    dst_buf: *mut u8,
    dst_cap: usize,
    out_len: *mut usize,
) -> i32 {
    if src_ptr.is_null() || dst_buf.is_null() || out_len.is_null() || dst_cap == 0 {
        return 0;
    }

    let src = std::slice::from_raw_parts(src_ptr, src_len);
    let dst = std::slice::from_raw_parts_mut(dst_buf, dst_cap);

    match lz4_decompress_slice(src, dst) {
        Some(len) => {
            *out_len = len;
            1
        }
        None => 0,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_lz4_roundtrip() {
        let original = b"Flow Launcher - Search Anything, Launch Fast! Flow Launcher - Search Anything, Launch Fast!";
        let mut compressed = vec![0u8; original.len() * 2 + 32];
        let mut decompressed = vec![0u8; original.len() + 16];

        let mut comp_len = 0usize;
        let mut decomp_len = 0usize;

        unsafe {
            let ok1 = flow_lz4_compress(
                original.as_ptr(),
                original.len(),
                compressed.as_mut_ptr(),
                compressed.len(),
                &mut comp_len,
            );
            assert_eq!(ok1, 1);
            assert!(comp_len > 0);
            assert!(comp_len < original.len()); // Verified compression!

            let ok2 = flow_lz4_decompress(
                compressed.as_ptr(),
                comp_len,
                decompressed.as_mut_ptr(),
                decompressed.len(),
                &mut decomp_len,
            );
            assert_eq!(ok2, 1);
            assert_eq!(decomp_len, original.len());
            assert_eq!(&decompressed[..decomp_len], &original[..]);
        }
    }
}
