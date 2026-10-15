/// Native UWP AppxManifest Parser
/// Fast zero-allocation XML scanner for Windows Store & UWP application metadata.

use std::fs::File;
use std::io::Read;
use std::path::Path;

#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativeUwpAppInfo {
    pub app_id: [u16; 128],
    pub app_id_len: u32,
    pub logo_uri: [u16; 260],
    pub logo_uri_len: u32,
    pub preview_uri: [u16; 260],
    pub preview_uri_len: u32,
    pub can_elevate: i32,
}

impl Default for NativeUwpAppInfo {
    fn default() -> Self {
        Self {
            app_id: [0; 128],
            app_id_len: 0,
            logo_uri: [0; 260],
            logo_uri_len: 0,
            preview_uri: [0; 260],
            preview_uri_len: 0,
            can_elevate: 0,
        }
    }
}

fn copy_str_to_u16_buf(text: &str, buf: &mut [u16], len: &mut u32) {
    let utf16: Vec<u16> = text.encode_utf16().collect();
    let copy_len = std::cmp::min(utf16.len(), buf.len() - 1);
    buf[..copy_len].copy_from_slice(&utf16[..copy_len]);
    buf[copy_len] = 0;
    *len = copy_len as u32;
}

fn extract_attribute_value<'a>(tag_content: &'a str, attr_name: &str) -> Option<&'a str> {
    let pattern = format!("{}=\"", attr_name);
    if let Some(pos) = tag_content.find(&pattern) {
        let val_start = pos + pattern.len();
        if let Some(val_end) = tag_content[val_start..].find('"') {
            return Some(&tag_content[val_start..val_start + val_end]);
        }
    }

    // Also check single quotes: attr_name='...'
    let pattern_sq = format!("{}='", attr_name);
    if let Some(pos) = tag_content.find(&pattern_sq) {
        let val_start = pos + pattern_sq.len();
        if let Some(val_end) = tag_content[val_start..].find('\'') {
            return Some(&tag_content[val_start..val_start + val_end]);
        }
    }

    None
}

/// Parses UWP package manifest XML string into NativeUwpAppInfo items
pub fn parse_manifest_xml_string(xml: &str, out_apps: &mut [NativeUwpAppInfo]) -> usize {
    if out_apps.is_empty() {
        return 0;
    }

    // 1. Detect logo attribute names based on schema version
    let (small_logo_attr, big_logo_attr) = if xml.contains("http://schemas.microsoft.com/appx/manifest/foundation/windows10") {
        ("Square44x44Logo", "Square150x150Logo")
    } else if xml.contains("http://schemas.microsoft.com/appx/2013/manifest") {
        ("Square30x30Logo", "Square150x150Logo")
    } else if xml.contains("http://schemas.microsoft.com/appx/2010/manifest") {
        ("SmallLogo", "Logo")
    } else {
        ("Square44x44Logo", "Square150x150Logo")
    };

    // 2. Check package-wide allowElevation
    let package_can_elevate = xml.contains("Name=\"allowElevation\"") || xml.contains("Name='allowElevation'");

    // 3. Scan for <Application ...> ... </Application>
    let mut app_count = 0usize;
    let mut search_pos = 0;

    while let Some(app_start) = xml[search_pos..].find("<Application") {
        let app_abs_start = search_pos + app_start;
        let next_char = xml[app_abs_start + "<Application".len()..].chars().next();
        if let Some(c) = next_char {
            if c != ' ' && c != '\t' && c != '\r' && c != '\n' && c != '>' {
                // It was <Applications or similar, skip past it
                search_pos = app_abs_start + "<Application".len();
                continue;
            }
        }

        let app_end = match xml[app_abs_start..].find("</Application>") {
            Some(e) => app_abs_start + e + "</Application>".len(),
            None => {
                // Check if self-closing tag
                if let Some(sc) = xml[app_abs_start..].find("/>") {
                    app_abs_start + sc + 2
                } else {
                    break;
                }
            }
        };

        let app_block = &xml[app_abs_start..app_end];
        search_pos = app_end;

        // Extract opening <Application ...> tag
        let open_tag_end = app_block.find('>').unwrap_or(app_block.len());
        let open_tag = &app_block[..open_tag_end];

        let app_id = match extract_attribute_value(open_tag, "Id") {
            Some(id) => id,
            None => continue,
        };

        let mut app_info = NativeUwpAppInfo::default();
        copy_str_to_u16_buf(app_id, &mut app_info.app_id, &mut app_info.app_id_len);

        // Check if application can run elevated
        let entry_point = extract_attribute_value(open_tag, "EntryPoint").unwrap_or("");
        let is_full_trust = entry_point == "Windows.FullTrustApplication";
        let is_medium_il = open_tag.contains("TrustLevel=\"mediumIL\"") || open_tag.contains("TrustLevel='mediumIL'");

        app_info.can_elevate = if package_can_elevate || is_full_trust || is_medium_il { 1 } else { 0 };

        // Find VisualElements tag (e.g. <uap:VisualElements ...> or <m2:VisualElements ...>)
        if let Some(ve_start) = app_block.find("VisualElements") {
            // Find enclosing tag start
            let tag_start = app_block[..ve_start].rfind('<').unwrap_or(ve_start);
            let tag_end = app_block[tag_start..].find('>').map(|e| tag_start + e).unwrap_or(app_block.len());
            let ve_tag = &app_block[tag_start..tag_end];

            // Small logo
            let small_logo = extract_attribute_value(ve_tag, small_logo_attr)
                .or_else(|| extract_attribute_value(ve_tag, "Square44x44Logo"))
                .or_else(|| extract_attribute_value(ve_tag, "SmallLogo"))
                .unwrap_or("");
            copy_str_to_u16_buf(small_logo, &mut app_info.logo_uri, &mut app_info.logo_uri_len);

            // Big logo / Preview
            let big_logo = extract_attribute_value(ve_tag, big_logo_attr)
                .or_else(|| extract_attribute_value(ve_tag, "Square150x150Logo"))
                .or_else(|| extract_attribute_value(ve_tag, "Logo"))
                .unwrap_or(small_logo);
            copy_str_to_u16_buf(big_logo, &mut app_info.preview_uri, &mut app_info.preview_uri_len);
        }

        out_apps[app_count] = app_info;
        app_count += 1;
        if app_count >= out_apps.len() {
            break;
        }
    }

    app_count
}

/// Fast native parser for AppxManifest.xml
#[no_mangle]
pub unsafe extern "C" fn flow_uwp_parse_manifest_utf16(
    manifest_path_ptr: *const u16,
    manifest_path_len: usize,
    out_apps: *mut NativeUwpAppInfo,
    max_apps: usize,
    out_count: *mut usize,
) -> i32 {
    if manifest_path_ptr.is_null() || out_apps.is_null() || out_count.is_null() || max_apps == 0 || manifest_path_len == 0 {
        return 0;
    }

    let slice = std::slice::from_raw_parts(manifest_path_ptr, manifest_path_len);
    let path_str = match String::from_utf16(slice) {
        Ok(s) => s,
        Err(_) => return 0,
    };

    let manifest_path = {
        let p = Path::new(&path_str);
        if p.is_dir() {
            p.join("AppxManifest.xml")
        } else {
            p.to_path_buf()
        }
    };

    let mut file = match File::open(&manifest_path) {
        Ok(f) => f,
        Err(_) => return 0,
    };

    let mut xml = String::new();
    if file.read_to_string(&mut xml).is_err() {
        return 0;
    }

    let out_slice = std::slice::from_raw_parts_mut(out_apps, max_apps);
    let count = parse_manifest_xml_string(&xml, out_slice);
    *out_count = count;

    1
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_uwp_manifest_windows10() {
        let xml = r#"<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
  <Capabilities>
    <rescap:Capability Name="allowElevation" />
  </Capabilities>
  <Applications>
    <Application Id="App" Executable="Terminal.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="Windows Terminal"
                          Square44x44Logo="Images\Square44x44Logo.png"
                          Square150x150Logo="Images\Square150x150Logo.png"
                          Description="Terminal Application" />
    </Application>
  </Applications>
</Package>"#;

        let mut apps = [NativeUwpAppInfo::default(); 4];
        let count = parse_manifest_xml_string(xml, &mut apps);
        assert_eq!(count, 1);
        assert_eq!(apps[0].can_elevate, 1);

        let id_str = String::from_utf16_lossy(&apps[0].app_id[..apps[0].app_id_len as usize]);
        assert_eq!(id_str, "App");

        let logo_str = String::from_utf16_lossy(&apps[0].logo_uri[..apps[0].logo_uri_len as usize]);
        assert_eq!(logo_str, r"Images\Square44x44Logo.png");

        let preview_str = String::from_utf16_lossy(&apps[0].preview_uri[..apps[0].preview_uri_len as usize]);
        assert_eq!(preview_str, r"Images\Square150x150Logo.png");
    }

    #[test]
    fn test_parse_uwp_manifest_multiple_apps() {
        let xml = r#"<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
  <Applications>
    <Application Id="Calculator" EntryPoint="Calc.App">
      <VisualElements Square44x44Logo="Assets\Calc44.png" Square150x150Logo="Assets\Calc150.png" />
    </Application>
    <Application Id="Converter" EntryPoint="Windows.FullTrustApplication">
      <VisualElements Square44x44Logo="Assets\Conv44.png" Square150x150Logo="Assets\Conv150.png" />
    </Application>
  </Applications>
</Package>"#;

        let mut apps = [NativeUwpAppInfo::default(); 4];
        let count = parse_manifest_xml_string(xml, &mut apps);
        assert_eq!(count, 2);
        assert_eq!(apps[0].can_elevate, 0);
        assert_eq!(apps[1].can_elevate, 1);
    }
}
