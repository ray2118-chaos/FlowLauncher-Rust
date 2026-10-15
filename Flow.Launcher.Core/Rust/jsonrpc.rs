/// Native JSON-RPC Fast Streaming Parser for Python & Node.js Plugins
/// Parses JSON-RPC response messages from external subprocesses with zero managed object overhead.

#[derive(Debug, Clone, Copy)]
#[repr(C)]
pub struct NativeJsonRpcItem {
    pub title: [u16; 260],
    pub title_len: u32,
    pub subtitle: [u16; 260],
    pub subtitle_len: u32,
    pub icon_path: [u16; 260],
    pub icon_path_len: u32,
    pub score: i32,
}

/// Parses JSON-RPC response UTF-8 bytes into fixed contiguous NativeJsonRpcItem memory buffers
#[no_mangle]
pub unsafe extern "C" fn flow_jsonrpc_parse_response_utf8(
    json_ptr: *const u8,
    json_len: usize,
    out_items: *mut NativeJsonRpcItem,
    max_items: usize,
    out_count: *mut usize,
) -> i32 {
    if json_ptr.is_null() || out_items.is_null() || out_count.is_null() || max_items == 0 || json_len == 0 {
        return 0;
    }

    let slice = std::slice::from_raw_parts(json_ptr, json_len);
    let parsed: serde_json::Value = match serde_json::from_slice(slice) {
        Ok(v) => v,
        Err(_) => return 0,
    };

    let items = match parsed.get("result").and_then(|r| r.as_array()) {
        Some(list) => list,
        None => {
            *out_count = 0;
            return 1;
        }
    };

    let count = items.len().min(max_items);
    for i in 0..count {
        let obj = &items[i];
        let dst = &mut *out_items.add(i);

        // Title (case-insensitive lookup)
        let title_val = obj.get("Title").or_else(|| obj.get("title")).and_then(|v| v.as_str()).unwrap_or("");
        let title_utf16: Vec<u16> = title_val.encode_utf16().collect();
        let t_len = title_utf16.len().min(259);
        dst.title[..t_len].copy_from_slice(&title_utf16[..t_len]);
        dst.title[t_len] = 0;
        dst.title_len = t_len as u32;

        // Subtitle
        let sub_val = obj.get("SubTitle").or_else(|| obj.get("subtitle")).or_else(|| obj.get("subTitle")).and_then(|v| v.as_str()).unwrap_or("");
        let sub_utf16: Vec<u16> = sub_val.encode_utf16().collect();
        let s_len = sub_utf16.len().min(259);
        dst.subtitle[..s_len].copy_from_slice(&sub_utf16[..s_len]);
        dst.subtitle[s_len] = 0;
        dst.subtitle_len = s_len as u32;

        // IconPath
        let icon_val = obj.get("IcoPath").or_else(|| obj.get("icoPath")).or_else(|| obj.get("iconPath")).and_then(|v| v.as_str()).unwrap_or("");
        let icon_utf16: Vec<u16> = icon_val.encode_utf16().collect();
        let ic_len = icon_utf16.len().min(259);
        dst.icon_path[..ic_len].copy_from_slice(&icon_utf16[..ic_len]);
        dst.icon_path[ic_len] = 0;
        dst.icon_path_len = ic_len as u32;

        // Score
        let score_val = obj.get("Score").or_else(|| obj.get("score")).and_then(|v| v.as_i64()).unwrap_or(0);
        dst.score = score_val as i32;
    }

    *out_count = count;
    1
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_jsonrpc_response() {
        let json = br#"{"result":[{"title":"Test Title","subTitle":"Test Sub","icoPath":"test.png","score":95}]}"#;
        let mut items = [NativeJsonRpcItem {
            title: [0; 260],
            title_len: 0,
            subtitle: [0; 260],
            subtitle_len: 0,
            icon_path: [0; 260],
            icon_path_len: 0,
            score: 0,
        }; 4];
        let mut count = 0usize;

        unsafe {
            let ok = flow_jsonrpc_parse_response_utf8(
                json.as_ptr(),
                json.len(),
                items.as_mut_ptr(),
                items.len(),
                &mut count,
            );
            assert_eq!(ok, 1);
            assert_eq!(count, 1);
            assert_eq!(items[0].score, 95);
            assert_eq!(String::from_utf16_lossy(&items[0].title[..items[0].title_len as usize]), "Test Title");
            assert_eq!(String::from_utf16_lossy(&items[0].subtitle[..items[0].subtitle_len as usize]), "Test Sub");
        }
    }
}
