use std::fs::File;
use std::io::Read;

#[repr(C)]
pub struct LnkInfo {
    pub target: [u16; 512],
    pub target_len: u32,
    pub description: [u16; 512],
    pub description_len: u32,
    pub arguments: [u16; 512],
    pub arguments_len: u32,
}

fn read_u16_le(data: &[u8], offset: usize) -> Option<u16> {
    if offset + 2 <= data.len() {
        Some(u16::from_le_bytes([data[offset], data[offset + 1]]))
    } else {
        None
    }
}

fn read_u32_le(data: &[u8], offset: usize) -> Option<u32> {
    if offset + 4 <= data.len() {
        Some(u32::from_le_bytes([
            data[offset],
            data[offset + 1],
            data[offset + 2],
            data[offset + 3],
        ]))
    } else {
        None
    }
}

pub fn parse_lnk_bytes(data: &[u8]) -> Option<(String, String, String)> {
    if data.len() < 76 {
        return None;
    }

    let header_size = read_u32_le(data, 0)?;
    if header_size != 0x4C {
        return None;
    }

    let flags = read_u32_le(data, 0x14)?;
    let has_id_list = (flags & 0x01) != 0;
    let has_link_info = (flags & 0x02) != 0;
    let has_name = (flags & 0x04) != 0;
    let has_rel_path = (flags & 0x08) != 0;
    let has_working_dir = (flags & 0x10) != 0;
    let has_arguments = (flags & 0x20) != 0;
    let has_icon_loc = (flags & 0x40) != 0;
    let is_unicode = (flags & 0x80) != 0;

    let mut cursor = 76usize;

    // 1. Skip IDList if present
    if has_id_list {
        let id_list_size = read_u16_le(data, cursor)? as usize;
        cursor += 2 + id_list_size;
    }

    let mut target_path = String::new();

    // 2. LinkInfo structure
    if has_link_info && cursor + 28 <= data.len() {
        let link_info_start = cursor;
        let link_info_size = read_u32_le(data, cursor)? as usize;
        let header_size = read_u32_le(data, cursor + 4)? as usize;
        let local_base_path_offset = read_u32_le(data, cursor + 16)? as usize;

        let mut found_unicode = false;
        if header_size >= 0x24 && cursor + 32 <= data.len() {
            let local_base_path_offset_unicode = read_u32_le(data, cursor + 28)? as usize;
            if local_base_path_offset_unicode > 0 {
                let start = link_info_start + local_base_path_offset_unicode;
                let mut u16_chars = Vec::new();
                let mut pos = start;
                while pos + 2 <= data.len() && pos < link_info_start + link_info_size {
                    let ch = read_u16_le(data, pos).unwrap_or(0);
                    if ch == 0 {
                        break;
                    }
                    u16_chars.push(ch);
                    pos += 2;
                }
                if let Ok(s) = String::from_utf16(&u16_chars) {
                    target_path = s;
                    found_unicode = true;
                }
            }
        }

        if !found_unicode && local_base_path_offset > 0 {
            let start = link_info_start + local_base_path_offset;
            let mut ansi_bytes = Vec::new();
            let mut pos = start;
            while pos < data.len() && pos < link_info_start + link_info_size {
                let b = data[pos];
                if b == 0 {
                    break;
                }
                ansi_bytes.push(b);
                pos += 1;
            }
            target_path = String::from_utf8_lossy(&ansi_bytes).to_string();
        }

        cursor = link_info_start + link_info_size;
    }

    // Helper to read string data
    let read_string = |cursor: &mut usize| -> Option<String> {
        let count = read_u16_le(data, *cursor)? as usize;
        *cursor += 2;
        if is_unicode {
            let byte_count = count * 2;
            if *cursor + byte_count > data.len() {
                return None;
            }
            let mut u16_vec = Vec::with_capacity(count);
            for i in 0..count {
                u16_vec.push(read_u16_le(data, *cursor + i * 2)?);
            }
            *cursor += byte_count;
            String::from_utf16(&u16_vec).ok()
        } else {
            if *cursor + count > data.len() {
                return None;
            }
            let s = String::from_utf8_lossy(&data[*cursor..*cursor + count]).to_string();
            *cursor += count;
            Some(s)
        }
    };

    let mut description = String::new();
    let mut arguments = String::new();

    if has_name {
        if let Some(s) = read_string(&mut cursor) {
            description = s;
        }
    }
    if has_rel_path {
        let _ = read_string(&mut cursor);
    }
    if has_working_dir {
        let _ = read_string(&mut cursor);
    }
    if has_arguments {
        if let Some(s) = read_string(&mut cursor) {
            arguments = s;
        }
    }
    if has_icon_loc {
        let _ = read_string(&mut cursor);
    }

    // 3. ExtraData blocks (search for EnvironmentVariableDataBlock)
    while cursor + 8 <= data.len() {
        let block_size = read_u32_le(data, cursor).unwrap_or(0) as usize;
        if block_size < 4 {
            break;
        }
        let signature = read_u32_le(data, cursor + 4).unwrap_or(0);
        if signature == 0xA0000001 && cursor + 268 + 520 <= data.len() {
            // EnvironmentVariableDataBlock TargetUnicode
            let start = cursor + 268;
            let mut u16_vec = Vec::new();
            for i in 0..260 {
                let ch = read_u16_le(data, start + i * 2).unwrap_or(0);
                if ch == 0 {
                    break;
                }
                u16_vec.push(ch);
            }
            if let Ok(env_target) = String::from_utf16(&u16_vec) {
                if !env_target.is_empty() && target_path.is_empty() {
                    target_path = env_target;
                }
            }
        }
        cursor += block_size;
    }

    Some((target_path, description, arguments))
}

/// Parses `.lnk` file directly from disk via fast binary read
#[no_mangle]
pub unsafe extern "C" fn flow_read_lnk(
    path_utf16: *const u16,
    path_len: usize,
    out_info: *mut LnkInfo,
) -> i32 {
    if path_utf16.is_null() || path_len == 0 || out_info.is_null() {
        return 0;
    }

    let slice = unsafe { std::slice::from_raw_parts(path_utf16, path_len) };
    let path_str = match String::from_utf16(slice) {
        Ok(s) => s,
        Err(_) => return 0,
    };

    let mut file = match File::open(&path_str) {
        Ok(f) => f,
        Err(_) => return 0,
    };

    let mut buffer = Vec::new();
    if file.read_to_end(&mut buffer).is_err() {
        return 0;
    }

    if let Some((target, desc, args)) = parse_lnk_bytes(&buffer) {
        let info = unsafe { &mut *out_info };

        let copy_str = |src: &str, dest: &mut [u16; 512], out_len: &mut u32| {
            let u16_units: Vec<u16> = src.encode_utf16().collect();
            let count = u16_units.len().min(511);
            for i in 0..count {
                dest[i] = u16_units[i];
            }
            dest[count] = 0;
            *out_len = count as u32;
        };

        copy_str(&target, &mut info.target, &mut info.target_len);
        copy_str(&desc, &mut info.description, &mut info.description_len);
        copy_str(&args, &mut info.arguments, &mut info.arguments_len);

        1
    } else {
        0
    }
}
