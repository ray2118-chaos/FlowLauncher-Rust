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

use rusqlite::{Connection, OpenFlags};

#[no_mangle]
pub unsafe extern "C" fn flow_parse_firefox_places(
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

    let original_path = PathBuf::from(&path_str);
    if !original_path.is_file() {
        return 0;
    }

    let mut bookmarks: Vec<(String, String)> = Vec::with_capacity(std::cmp::min(max_items, 1024));
    let query_sql = "SELECT moz_places.url, moz_bookmarks.title FROM moz_places INNER JOIN moz_bookmarks ON (moz_bookmarks.fk NOT NULL AND moz_bookmarks.title NOT NULL AND moz_bookmarks.fk = moz_places.id) ORDER BY moz_places.visit_count DESC";

    let mut read_success = false;
    if let Ok(conn) = Connection::open_with_flags(&original_path, OpenFlags::SQLITE_OPEN_READ_ONLY | OpenFlags::SQLITE_OPEN_URI) {
        let _ = conn.busy_timeout(std::time::Duration::from_millis(300));
        if let Ok(mut stmt) = conn.prepare(query_sql) {
            if let Ok(rows) = stmt.query_map([], |row| {
                let url: String = row.get(0)?;
                let title: String = row.get(1)?;
                Ok((title, url))
            }) {
                for item in rows.flatten() {
                    bookmarks.push(item);
                    if bookmarks.len() >= max_items {
                        break;
                    }
                }
                read_success = true;
            }
        }
    }

    // If locked by Firefox, make a fast snapshot copy
    if !read_success {
        let temp_dir = std::env::temp_dir();
        let unique_id = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .map(|d| d.as_nanos())
            .unwrap_or(0);
        let temp_db = temp_dir.join(format!("flow_places_{}.sqlite", unique_id));
        let temp_wal = temp_dir.join(format!("flow_places_{}.sqlite-wal", unique_id));

        let _ = std::fs::copy(&original_path, &temp_db);
        let wal_path = original_path.with_extension("sqlite-wal");
        if wal_path.exists() {
            let _ = std::fs::copy(&wal_path, &temp_wal);
        }

        if let Ok(conn) = Connection::open_with_flags(&temp_db, OpenFlags::SQLITE_OPEN_READ_ONLY | OpenFlags::SQLITE_OPEN_URI) {
            let _ = conn.busy_timeout(std::time::Duration::from_millis(500));
            if let Ok(mut stmt) = conn.prepare(query_sql) {
                if let Ok(rows) = stmt.query_map([], |row| {
                    let url: String = row.get(0)?;
                    let title: String = row.get(1)?;
                    Ok((title, url))
                }) {
                    for item in rows.flatten() {
                        bookmarks.push(item);
                        if bookmarks.len() >= max_items {
                            break;
                        }
                    }
                }
            }
        }

        let _ = std::fs::remove_file(temp_db);
        let _ = std::fs::remove_file(temp_wal);
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

use std::sync::RwLock;
use crate::fuzzy::fuzzy_match_utf16;

#[repr(C)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct NativeBookmarkMatch {
    pub id: u32,
    pub score: i32,
}

struct StoredBookmark {
    id: u32,
    title: Vec<u16>,
    url: Vec<u16>,
}

static BOOKMARK_CACHE: RwLock<Vec<StoredBookmark>> = RwLock::new(Vec::new());

#[no_mangle]
pub unsafe extern "C" fn flow_bookmarks_clear() -> i32 {
    if let Ok(mut cache) = BOOKMARK_CACHE.write() {
        cache.clear();
        1
    } else {
        0
    }
}

#[no_mangle]
pub unsafe extern "C" fn flow_bookmarks_register(
    id: u32,
    title_ptr: *const u16,
    title_len: usize,
    url_ptr: *const u16,
    url_len: usize,
) -> i32 {
    let title = if !title_ptr.is_null() && title_len > 0 {
        std::slice::from_raw_parts(title_ptr, title_len).to_vec()
    } else {
        Vec::new()
    };

    let url = if !url_ptr.is_null() && url_len > 0 {
        std::slice::from_raw_parts(url_ptr, url_len).to_vec()
    } else {
        Vec::new()
    };

    if let Ok(mut cache) = BOOKMARK_CACHE.write() {
        cache.push(StoredBookmark { id, title, url });
        1
    } else {
        0
    }
}

#[no_mangle]
pub unsafe extern "C" fn flow_bookmarks_search(
    query_ptr: *const u16,
    query_len: usize,
    out_matches: *mut NativeBookmarkMatch,
    max_matches: usize,
    out_count: *mut usize,
) -> i32 {
    if query_ptr.is_null() || query_len == 0 || out_matches.is_null() || out_count.is_null() || max_matches == 0 {
        return 0;
    }

    let query = std::slice::from_raw_parts(query_ptr, query_len);
    let cache_lock = match BOOKMARK_CACHE.read() {
        Ok(guard) => guard,
        Err(_) => return 0,
    };

    let mut scratch_indices = [0i32; 64];
    let mut matches: Vec<NativeBookmarkMatch> = Vec::with_capacity(std::cmp::min(cache_lock.len(), max_matches * 2));

    for item in cache_lock.iter() {
        let mut best_score = 0;

        if !item.title.is_empty() {
            let (success, score, _) = fuzzy_match_utf16(query, &item.title, true, &mut scratch_indices);
            if success && score > best_score {
                best_score = score;
            }
        }

        if best_score < 30 && !item.url.is_empty() {
            let (success, score, _) = fuzzy_match_utf16(query, &item.url, true, &mut scratch_indices);
            if success && (score - 20) > best_score {
                best_score = score - 20;
            }
        }

        if best_score > 0 {
            matches.push(NativeBookmarkMatch {
                id: item.id,
                score: best_score,
            });
        }
    }

    matches.sort_unstable_by(|a, b| b.score.cmp(&a.score));

    let return_count = std::cmp::min(matches.len(), max_matches);
    let out_slice = std::slice::from_raw_parts_mut(out_matches, return_count);
    out_slice.copy_from_slice(&matches[..return_count]);

    *out_count = return_count;
    1
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_bookmark_cache_and_search() {
        unsafe {
            flow_bookmarks_clear();

            let title1: Vec<u16> = "GitHub - Flow Launcher".encode_utf16().collect();
            let url1: Vec<u16> = "https://github.com/Flow-Launcher/Flow.Launcher".encode_utf16().collect();
            flow_bookmarks_register(0, title1.as_ptr(), title1.len(), url1.as_ptr(), url1.len());

            let title2: Vec<u16> = "Rust Programming Language".encode_utf16().collect();
            let url2: Vec<u16> = "https://www.rust-lang.org/".encode_utf16().collect();
            flow_bookmarks_register(1, title2.as_ptr(), title2.len(), url2.as_ptr(), url2.len());

            let query: Vec<u16> = "flow".encode_utf16().collect();
            let mut matches = [NativeBookmarkMatch { id: 0, score: 0 }; 4];
            let mut count = 0usize;

            let ok = flow_bookmarks_search(query.as_ptr(), query.len(), matches.as_mut_ptr(), matches.len(), &mut count);
            assert_eq!(ok, 1);
            assert_eq!(count, 1);
            assert_eq!(matches[0].id, 0);
            assert!(matches[0].score > 0);
        }
    }
}

