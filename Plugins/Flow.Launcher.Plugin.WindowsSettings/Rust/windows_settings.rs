use std::sync::RwLock;
use crate::fuzzy::fuzzy_match_utf16;

#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativeSettingMatch {
    pub id: u32,
    pub score: i32,
    pub match_type: u32, // 0 = Name, 1 = Area, 2 = Alt
}

struct StoredSetting {
    id: u32,
    name: Vec<u16>,
    area: Vec<u16>,
    alt: Vec<u16>,
    _app_type: u32,
}

static SETTINGS_CACHE: RwLock<Vec<StoredSetting>> = RwLock::new(Vec::new());

#[no_mangle]
pub unsafe extern "C" fn flow_clear_windows_settings() -> i32 {
    if let Ok(mut cache) = SETTINGS_CACHE.write() {
        cache.clear();
        1
    } else {
        0
    }
}

#[no_mangle]
pub unsafe extern "C" fn flow_register_windows_setting(
    id: u32,
    name_ptr: *const u16,
    name_len: usize,
    area_ptr: *const u16,
    area_len: usize,
    alt_ptr: *const u16,
    alt_len: usize,
    app_type: u32,
) -> i32 {
    let name = if !name_ptr.is_null() && name_len > 0 {
        unsafe { std::slice::from_raw_parts(name_ptr, name_len).to_vec() }
    } else {
        Vec::new()
    };

    let area = if !area_ptr.is_null() && area_len > 0 {
        unsafe { std::slice::from_raw_parts(area_ptr, area_len).to_vec() }
    } else {
        Vec::new()
    };

    let alt = if !alt_ptr.is_null() && alt_len > 0 {
        unsafe { std::slice::from_raw_parts(alt_ptr, alt_len).to_vec() }
    } else {
        Vec::new()
    };

    if let Ok(mut cache) = SETTINGS_CACHE.write() {
        cache.push(StoredSetting {
            id,
            name,
            area,
            alt,
            _app_type: app_type,
        });
        1
    } else {
        0
    }
}

#[no_mangle]
pub unsafe extern "C" fn flow_search_windows_settings(
    query_ptr: *const u16,
    query_len: usize,
    out_matches: *mut NativeSettingMatch,
    max_matches: usize,
    out_count: *mut usize,
) -> i32 {
    if query_ptr.is_null() || query_len == 0 || out_matches.is_null() || out_count.is_null() || max_matches == 0 {
        return 0;
    }

    let query = unsafe { std::slice::from_raw_parts(query_ptr, query_len) };
    let cache_lock = match SETTINGS_CACHE.read() {
        Ok(guard) => guard,
        Err(_) => return 0,
    };

    let mut scratch_indices = [0i32; 64];
    let mut matches: Vec<NativeSettingMatch> = Vec::with_capacity(std::cmp::min(cache_lock.len(), max_matches));

    for item in cache_lock.iter() {
        // 1. Match Name
        if !item.name.is_empty() {
            let (success, score, _) = fuzzy_match_utf16(query, &item.name, true, &mut scratch_indices);
            if success && score > 0 {
                matches.push(NativeSettingMatch {
                    id: item.id,
                    score,
                    match_type: 0,
                });
                continue;
            }
        }

        // 2. Match Area
        if !item.area.is_empty() {
            let (success, score, _) = fuzzy_match_utf16(query, &item.area, true, &mut scratch_indices);
            if success && score > 0 {
                matches.push(NativeSettingMatch {
                    id: item.id,
                    score: (score - 10).max(1),
                    match_type: 1,
                });
                continue;
            }
        }

        // 3. Match Alt Names
        if !item.alt.is_empty() {
            let (success, score, _) = fuzzy_match_utf16(query, &item.alt, true, &mut scratch_indices);
            if success && score > 0 {
                matches.push(NativeSettingMatch {
                    id: item.id,
                    score: (score - 10).max(1),
                    match_type: 2,
                });
                continue;
            }
        }
    }

    // Sort descending by score
    matches.sort_by(|a, b| b.score.cmp(&a.score));

    let return_count = std::cmp::min(matches.len(), max_matches);
    let out_slice = unsafe { std::slice::from_raw_parts_mut(out_matches, return_count) };
    out_slice.copy_from_slice(&matches[..return_count]);

    unsafe {
        *out_count = return_count;
    }

    1
}
