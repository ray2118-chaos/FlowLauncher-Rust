use std::sync::RwLock;
use std::time::{SystemTime, UNIX_EPOCH};

#[repr(C)]
pub struct NativeClipboardItem {
    pub id: u32,
    pub preview: [u16; 260],
    pub preview_len: u32,
    pub char_count: u32,
    pub timestamp_ms: u64,
}

struct ClipEntry {
    id: u32,
    text: String,
    preview: Vec<u16>,
    char_count: u32,
    timestamp_ms: u64,
    hash: u64,
}

struct ClipboardHistory {
    items: Vec<ClipEntry>,
    next_id: u32,
    max_capacity: usize,
}

impl ClipboardHistory {
    fn new(capacity: usize) -> Self {
        Self {
            items: Vec::with_capacity(capacity),
            next_id: 1,
            max_capacity: capacity,
        }
    }

    fn add(&mut self, text: String) -> u32 {
        let trimmed = text.trim();
        if trimmed.is_empty() {
            return 0;
        }

        let hash = xxhash_rust::xxh3::xxh3_64(text.as_bytes());

        // Check if identical to most recent entry
        if let Some(first) = self.items.first_mut() {
            if first.hash == hash {
                first.timestamp_ms = SystemTime::now()
                    .duration_since(UNIX_EPOCH)
                    .unwrap_or_default()
                    .as_millis() as u64;
                return first.id;
            }
        }

        // Deduplicate existing item further down by removing it and moving to front
        if let Some(pos) = self.items.iter().position(|item| item.hash == hash) {
            let mut existing = self.items.remove(pos);
            existing.timestamp_ms = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap_or_default()
                .as_millis() as u64;
            self.items.insert(0, existing);
            return self.items[0].id;
        }

        // Prepare preview (single line, sanitized, max 255 chars)
        let single_line = trimmed.replace('\r', "").replace('\n', " ↵ ");
        let preview_str: String = single_line.chars().take(255).collect();
        let preview: Vec<u16> = preview_str.encode_utf16().collect();

        let id = self.next_id;
        self.next_id = self.next_id.wrapping_add(1);

        let entry = ClipEntry {
            id,
            char_count: text.chars().count() as u32,
            preview,
            text,
            timestamp_ms: SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap_or_default()
                .as_millis() as u64,
            hash,
        };

        self.items.insert(0, entry);
        if self.items.len() > self.max_capacity {
            self.items.truncate(self.max_capacity);
        }

        id
    }

    fn clear(&mut self) {
        self.items.clear();
    }
}

static CLIPBOARD: RwLock<Option<ClipboardHistory>> = RwLock::new(None);

fn with_clipboard<F, R>(f: F) -> R
where
    F: FnOnce(&mut ClipboardHistory) -> R,
{
    let mut guard = CLIPBOARD.write().unwrap();
    let history = guard.get_or_insert_with(|| ClipboardHistory::new(256));
    f(history)
}

fn with_clipboard_read<F, R>(f: F) -> R
where
    F: FnOnce(&ClipboardHistory) -> R,
{
    let mut guard = CLIPBOARD.write().unwrap();
    let history = guard.get_or_insert_with(|| ClipboardHistory::new(256));
    f(history)
}

#[no_mangle]
pub unsafe extern "C" fn flow_clipboard_add(
    text_utf16: *const u16,
    text_len: usize,
) -> u32 {
    if text_utf16.is_null() || text_len == 0 {
        return 0;
    }

    let slice = unsafe { std::slice::from_raw_parts(text_utf16, text_len) };
    let text = match String::from_utf16(slice) {
        Ok(s) => s,
        Err(_) => return 0,
    };

    with_clipboard(|cb| cb.add(text))
}

#[no_mangle]
pub unsafe extern "C" fn flow_clipboard_search(
    query_utf16: *const u16,
    query_len: usize,
    out_items: *mut NativeClipboardItem,
    max_items: u32,
    out_count: *mut u32,
) -> i32 {
    if out_items.is_null() || out_count.is_null() || max_items == 0 {
        return 0;
    }

    let query_str = if !query_utf16.is_null() && query_len > 0 {
        let slice = unsafe { std::slice::from_raw_parts(query_utf16, query_len) };
        String::from_utf16(slice).unwrap_or_default().to_lowercase()
    } else {
        String::new()
    };

    let query_clean = query_str.trim();

    with_clipboard_read(|cb| {
        let mut count = 0u32;

        for item in &cb.items {
            if count >= max_items {
                break;
            }

            let matches = if query_clean.is_empty() {
                true
            } else {
                item.text.to_lowercase().contains(query_clean)
            };

            if matches {
                let out = unsafe { &mut *out_items.add(count as usize) };
                out.id = item.id;
                out.char_count = item.char_count;
                out.timestamp_ms = item.timestamp_ms;

                let copy_len = item.preview.len().min(259);
                out.preview[..copy_len].copy_from_slice(&item.preview[..copy_len]);
                out.preview[copy_len] = 0;
                out.preview_len = copy_len as u32;

                count += 1;
            }
        }

        unsafe {
            *out_count = count;
        }
        1
    })
}

#[no_mangle]
pub unsafe extern "C" fn flow_clipboard_get_text(
    id: u32,
    out_buf: *mut u16,
    max_len: usize,
    out_len: *mut usize,
) -> i32 {
    if out_buf.is_null() || out_len.is_null() || max_len == 0 {
        return 0;
    }

    with_clipboard_read(|cb| {
        if let Some(item) = cb.items.iter().find(|i| i.id == id) {
            let utf16: Vec<u16> = item.text.encode_utf16().collect();
            let copy_len = utf16.len().min(max_len - 1);
            let slice = unsafe { std::slice::from_raw_parts_mut(out_buf, max_len) };
            slice[..copy_len].copy_from_slice(&utf16[..copy_len]);
            slice[copy_len] = 0;
            unsafe {
                *out_len = copy_len;
            }
            1
        } else {
            0
        }
    })
}

#[no_mangle]
pub unsafe extern "C" fn flow_clipboard_clear() -> i32 {
    with_clipboard(|cb| {
        cb.clear();
        1
    })
}

#[no_mangle]
pub unsafe extern "C" fn flow_clipboard_count() -> u32 {
    with_clipboard_read(|cb| cb.items.len() as u32)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_clipboard() {
        let mut history = ClipboardHistory::new(10);
        let id1 = history.add("Hello world".to_string());
        assert_eq!(id1, 1);
        assert_eq!(history.items.len(), 1);

        // Duplicate test
        let id2 = history.add("Hello world".to_string());
        assert_eq!(id2, 1);
        assert_eq!(history.items.len(), 1);

        // Add second
        let id3 = history.add("Another clipboard clip".to_string());
        assert_eq!(id3, 2);
        assert_eq!(history.items.len(), 2);
    }
}
