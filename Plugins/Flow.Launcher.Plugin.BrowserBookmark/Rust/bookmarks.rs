use std::fs::File;
use std::io::BufReader;
use std::path::PathBuf;
use serde_json::Value;

#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativeBookmarkItem {
    pub title: [u16; 260],
    pub title_len: u32,
    pub url: [u16; 1024],
    pub url_len: u32,
}

fn collect_bookmarks_recursive(val: &Value, out_list: &mut Vec<(String, String)>, max_items: usize) {
    if out_list.len() >= max_items {
        return;
    }

    if let Some(children) = val.get("children").and_then(|c| c.as_array()) {
        for child in children {
            if out_list.len() >= max_items {
                break;
            }

            let item_type = child.get("type").and_then(|t| t.as_str()).unwrap_or("");
            if item_type == "url" {
                let name = child.get("name").and_then(|n| n.as_str()).unwrap_or("");
                let url = child.get("url").and_then(|u| u.as_str()).unwrap_or("");
                if !url.is_empty() {
                    out_list.push((name.to_string(), url.to_string()));
                }
            } else if item_type == "folder" || item_type == "workspace" {
                collect_bookmarks_recursive(child, out_list, max_items);
            }
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn flow_parse_chromium_bookmarks(
    path_ptr: *const u16,
    path_len: usize,
    out_items: *mut NativeBookmarkItem,
    max_items: usize,
    out_count: *mut usize,
) -> i32 {
    if path_ptr.is_null() || out_items.is_null() || out_count.is_null() || max_items == 0 {
        return 0;
    }

    let path_slice = unsafe { std::slice::from_raw_parts(path_ptr, path_len) };
    let path_str = match String::from_utf16(path_slice) {
        Ok(s) => s,
        Err(_) => return 0,
    };

    let file_path = PathBuf::from(path_str);
    let file = match File::open(&file_path) {
        Ok(f) => f,
        Err(_) => return 0,
    };

    let reader = BufReader::new(file);
    let json_val: Value = match serde_json::from_reader(reader) {
        Ok(v) => v,
        Err(_) => return 0,
    };

    let mut bookmarks = Vec::with_capacity(std::cmp::min(max_items, 1024));

    if let Some(roots) = json_val.get("roots").and_then(|r| r.as_object()) {
        for (_root_name, root_val) in roots {
            collect_bookmarks_recursive(root_val, &mut bookmarks, max_items);
            if bookmarks.len() >= max_items {
                break;
            }
        }
    }

    let count = std::cmp::min(bookmarks.len(), max_items);
    let out_slice = unsafe { std::slice::from_raw_parts_mut(out_items, count) };

    for (i, (title_str, url_str)) in bookmarks.iter().take(count).enumerate() {
        let item = &mut out_slice[i];
        item.title_len = 0;
        item.url_len = 0;

        let title_utf16: Vec<u16> = title_str.encode_utf16().collect();
        let t_len = std::cmp::min(title_utf16.len(), 259);
        item.title[..t_len].copy_from_slice(&title_utf16[..t_len]);
        item.title[t_len] = 0;
        item.title_len = t_len as u32;

        let url_utf16: Vec<u16> = url_str.encode_utf16().collect();
        let u_len = std::cmp::min(url_utf16.len(), 1023);
        item.url[..u_len].copy_from_slice(&url_utf16[..u_len]);
        item.url[u_len] = 0;
        item.url_len = u_len as u32;
    }

    unsafe {
        *out_count = count;
    }

    1
}
