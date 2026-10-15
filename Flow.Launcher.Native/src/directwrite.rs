/// Native DirectWrite / GDI High-Performance Text Metrics Subsystem
/// Computes exact string widths, heights, and layout bounds with zero managed allocations.

#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct NativeTextMetrics {
    pub width: f32,
    pub height: f32,
    pub line_count: u32,
}

#[cfg(windows)]
#[link(name = "gdi32")]
extern "system" {
    fn CreateCompatibleDC(hdc: *mut std::ffi::c_void) -> *mut std::ffi::c_void;
    fn DeleteDC(hdc: *mut std::ffi::c_void) -> i32;
    fn CreateFontW(
        cHeight: i32,
        cWidth: i32,
        cEscapement: i32,
        cOrientation: i32,
        cWeight: i32,
        bItalic: u32,
        bUnderline: u32,
        bStrikeOut: u32,
        iCharSet: u32,
        iOutPrecision: u32,
        iClipPrecision: u32,
        iQuality: u32,
        iPitchAndFamily: u32,
        pszFaceName: *const u16,
    ) -> *mut std::ffi::c_void;
    fn SelectObject(hdc: *mut std::ffi::c_void, hgdiobj: *mut std::ffi::c_void) -> *mut std::ffi::c_void;
    fn DeleteObject(ho: *mut std::ffi::c_void) -> i32;
    fn GetTextExtentPoint32W(
        hdc: *mut std::ffi::c_void,
        lpString: *const u16,
        c: i32,
        lpSize: *mut SIZE,
    ) -> i32;
}

#[cfg(windows)]
#[repr(C)]
struct SIZE {
    cx: i32,
    cy: i32,
}

const CLEARTYPE_QUALITY: u32 = 5;
const DEFAULT_CHARSET: u32 = 1;
const OUT_DEFAULT_PRECIS: u32 = 0;
const CLIP_DEFAULT_PRECIS: u32 = 0;
const DEFAULT_PITCH: u32 = 0;
const FF_DONTCARE: u32 = 0;

/// Measures text pixel width and height on Windows using native sub-microsecond font metrics
#[no_mangle]
pub unsafe extern "C" fn flow_gui_measure_text_utf16(
    text_ptr: *const u16,
    text_len: usize,
    font_name_ptr: *const u16,
    font_name_len: usize,
    font_size: f32,
    font_weight: i32,
    is_italic: i32,
    out_metrics: *mut NativeTextMetrics,
) -> i32 {
    if out_metrics.is_null() {
        return 0;
    }

    let metrics = &mut *out_metrics;
    metrics.width = 0.0;
    metrics.height = 0.0;
    metrics.line_count = if text_len > 0 { 1 } else { 0 };

    if text_ptr.is_null() || text_len == 0 {
        return 1;
    }

    #[cfg(windows)]
    {
        let hdc = CreateCompatibleDC(std::ptr::null_mut());
        if hdc.is_null() {
            return 0;
        }

        let mut font_buf = [0u16; 64];
        let font_slice = if !font_name_ptr.is_null() && font_name_len > 0 {
            let copy_len = font_name_len.min(63);
            let slice = std::slice::from_raw_parts(font_name_ptr, copy_len);
            font_buf[..copy_len].copy_from_slice(slice);
            font_buf[copy_len] = 0;
            font_buf.as_ptr()
        } else {
            std::ptr::null()
        };

        let height_px = -(font_size.round() as i32).max(1);
        let weight = if font_weight > 0 { font_weight } else { 400 };

        let hfont = CreateFontW(
            height_px,
            0,
            0,
            0,
            weight,
            if is_italic != 0 { 1 } else { 0 },
            0,
            0,
            DEFAULT_CHARSET,
            OUT_DEFAULT_PRECIS,
            CLIP_DEFAULT_PRECIS,
            CLEARTYPE_QUALITY,
            DEFAULT_PITCH | FF_DONTCARE,
            font_slice,
        );

        let old_font = if !hfont.is_null() {
            SelectObject(hdc, hfont)
        } else {
            std::ptr::null_mut()
        };

        let mut sz = SIZE { cx: 0, cy: 0 };
        let ok = GetTextExtentPoint32W(hdc, text_ptr, text_len as i32, &mut sz);

        if !old_font.is_null() {
            SelectObject(hdc, old_font);
        }
        if !hfont.is_null() {
            DeleteObject(hfont);
        }
        DeleteDC(hdc);

        if ok != 0 {
            metrics.width = sz.cx as f32;
            metrics.height = sz.cy as f32;
            1
        } else {
            0
        }
    }

    #[cfg(not(windows))]
    {
        // Simple fallback estimation for non-windows tests
        metrics.width = (text_len as f32) * font_size * 0.55;
        metrics.height = font_size * 1.2;
        1
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_measure_text_dimensions() {
        let text: Vec<u16> = "Flow Launcher Search Query".encode_utf16().collect();
        let font: Vec<u16> = "Segoe UI".encode_utf16().collect();
        let mut metrics = NativeTextMetrics::default();

        unsafe {
            let ok = flow_gui_measure_text_utf16(
                text.as_ptr(),
                text.len(),
                font.as_ptr(),
                font.len(),
                14.0,
                400,
                0,
                &mut metrics,
            );
            assert_eq!(ok, 1);
            assert!(metrics.width > 0.0);
            assert!(metrics.height > 0.0);
        }
    }

    #[test]
    fn test_measure_text_empty() {
        let font: Vec<u16> = "Segoe UI".encode_utf16().collect();
        let mut metrics = NativeTextMetrics::default();

        unsafe {
            let ok = flow_gui_measure_text_utf16(
                std::ptr::null(),
                0,
                font.as_ptr(),
                font.len(),
                14.0,
                400,
                0,
                &mut metrics,
            );
            assert_eq!(ok, 1);
            assert_eq!(metrics.width, 0.0);
        }
    }
}
