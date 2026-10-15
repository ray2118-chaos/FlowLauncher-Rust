/// Fast Native URL Parser & Validator for Flow Launcher
/// Replaces managed System.Uri allocations and Regex parsing on every keystroke.

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum UrlSchemeType {
    None = 0,
    Http = 1,
    Https = 2,
    ColonOnly = 3,       // e.g. about:blank, chrome:settings
    BrowserInternal = 4, // e.g. chrome://settings, file:///
    BareIp = 5,          // e.g. 192.168.1.1:8080
    Domain = 6,          // e.g. github.com, www.google.com
}

/// Checks if input is a valid numeric string like "123.456" that should NOT be treated as a URL
fn is_plain_number(s: &str) -> bool {
    let mut has_digit = false;
    let mut dot_count = 0;
    for b in s.bytes() {
        if b.is_ascii_digit() {
            has_digit = true;
        } else if b == b'.' {
            dot_count += 1;
        } else {
            return false;
        }
    }
    has_digit && dot_count <= 1
}

/// Checks if a string is a valid IPv4 address (four 0..255 octets, not 0.0.0.0)
fn is_valid_ipv4(s: &str) -> bool {
    let parts: Vec<&str> = s.split('.').collect();
    if parts.len() != 4 {
        return false;
    }

    let mut is_all_zero = true;
    for p in parts {
        if p.is_empty() || p.len() > 3 {
            return false;
        }
        if let Ok(num) = p.parse::<u16>() {
            if num > 255 {
                return false;
            }
            if num != 0 {
                is_all_zero = false;
            }
        } else {
            return false;
        }
    }

    !is_all_zero
}

/// Main URL validation logic
pub fn is_url(raw: &str) -> (bool, UrlSchemeType) {
    let input = raw.trim();
    if input.is_empty() || input.len() > 2048 {
        return (false, UrlSchemeType::None);
    }

    // Exclude plain numbers like 123 or 12.34
    if is_plain_number(input) {
        return (false, UrlSchemeType::None);
    }

    // 1. Check Bare IPv4 (e.g. 192.168.1.1, 127.0.0.1:8080/path)
    let ip_candidate = input.split(&['/', '?', '#'][..]).next().unwrap_or(input);
    let host_part = ip_candidate.split(':').next().unwrap_or(ip_candidate);
    if is_valid_ipv4(host_part) {
        // If there's a port, ensure it's valid
        if let Some(port_str) = ip_candidate.strip_prefix(host_part).and_then(|s| s.strip_prefix(':')) {
            if !port_str.is_empty() && port_str.chars().all(|c| c.is_ascii_digit()) {
                if let Ok(port) = port_str.parse::<u32>() {
                    if port > 0 && port <= 65535 {
                        return (true, UrlSchemeType::BareIp);
                    }
                }
                return (false, UrlSchemeType::None);
            }
        } else {
            return (true, UrlSchemeType::BareIp);
        }
    }

    // 2. Colon-only browser schemes: "about:", "chrome:", "brave:", "edge:", "opera:", "vivaldi:", "data:"
    let lower = input.to_lowercase();
    let colon_schemes = ["about:", "chrome:", "brave:", "edge:", "opera:", "vivaldi:", "data:"];
    for cs in &colon_schemes {
        if lower.starts_with(cs) {
            let rest = &input[cs.len()..];
            if !rest.starts_with("//") && !rest.is_empty() && !rest.contains(char::is_whitespace) {
                return (true, UrlSchemeType::ColonOnly);
            }
        }
    }

    // 3. Double-slash schemes
    let double_slash_schemes = [
        ("http://", UrlSchemeType::Http),
        ("https://", UrlSchemeType::Https),
        ("ftp://", UrlSchemeType::BrowserInternal),
        ("file://", UrlSchemeType::BrowserInternal),
        ("chrome://", UrlSchemeType::BrowserInternal),
        ("chrome-extension://", UrlSchemeType::BrowserInternal),
        ("moz-extension://", UrlSchemeType::BrowserInternal),
        ("edge://", UrlSchemeType::BrowserInternal),
        ("brave://", UrlSchemeType::BrowserInternal),
        ("vivaldi://", UrlSchemeType::BrowserInternal),
    ];

    for (prefix, st) in &double_slash_schemes {
        if lower.starts_with(prefix) {
            let after_scheme = &input[prefix.len()..];
            if !after_scheme.is_empty() && !after_scheme.contains(char::is_whitespace) {
                if *st == UrlSchemeType::Http || *st == UrlSchemeType::Https {
                    let host_cand = after_scheme.split(&['/', '?', '#'][..]).next().unwrap_or(after_scheme);
                    let host_clean = host_cand.split(':').next().unwrap_or(host_cand);
                    if host_clean.is_empty() || host_clean.starts_with('.') || host_clean.ends_with('.') {
                        return (false, UrlSchemeType::None);
                    }
                    if is_valid_ipv4(host_clean) || host_clean.to_lowercase() == "localhost" {
                        return (true, *st);
                    }
                    if !host_clean.contains('.') {
                        return (false, UrlSchemeType::None);
                    }
                    let labels: Vec<&str> = host_clean.split('.').collect();
                    if labels.iter().any(|l| l.is_empty()) {
                        return (false, UrlSchemeType::None);
                    }
                    let tld = labels.last().unwrap();
                    if tld.len() < 2 || !tld.chars().all(|c| c.is_ascii_alphabetic()) {
                        return (false, UrlSchemeType::None);
                    }
                }
                return (true, *st);
            }
        }
    }

    // 4. Standard Domain with TLD (e.g. "github.com", "sub.domain.co.uk", "localhost", "localhost:3000")
    if lower == "localhost" || lower.starts_with("localhost:") || lower.starts_with("localhost/") {
        return (true, UrlSchemeType::Domain);
    }

    // Extract authority / domain part (before '/', '?', '#')
    let domain_candidate = input.split(&['/', '?', '#'][..]).next().unwrap_or(input);
    let domain_without_port = domain_candidate.split(':').next().unwrap_or(domain_candidate);

    // Domain must contain at least one dot, no whitespace, valid characters
    if domain_without_port.contains('.') && !domain_without_port.contains(char::is_whitespace) {
        let labels: Vec<&str> = domain_without_port.split('.').collect();
        if labels.len() >= 2 {
            // Check that all labels are valid domain characters (alphanumeric or hyphen, not starting/ending with hyphen)
            let mut labels_valid = true;
            for label in &labels {
                if label.is_empty() || label.starts_with('-') || label.ends_with('-') {
                    labels_valid = false;
                    break;
                }
                if !label.chars().all(|c| c.is_ascii_alphanumeric() || c == '-') {
                    labels_valid = false;
                    break;
                }
            }

            // TLD (last label) must be at least 2 alphabetic characters
            if labels_valid {
                let tld = labels.last().unwrap();
                if tld.len() >= 2 && tld.chars().all(|c| c.is_ascii_alphabetic()) {
                    return (true, UrlSchemeType::Domain);
                }
            }
        }
    }

    (false, UrlSchemeType::None)
}

/// Zero-copy C-ABI export for validating URLs from UTF-16
#[no_mangle]
pub unsafe extern "C" fn flow_url_validate_utf16(
    url_ptr: *const u16,
    url_len: usize,
    out_scheme_type: *mut i32,
) -> i32 {
    if url_ptr.is_null() || url_len == 0 {
        return 0;
    }

    let slice = std::slice::from_raw_parts(url_ptr, url_len);
    let s = String::from_utf16_lossy(slice);

    let (valid, scheme) = is_url(&s);
    if !out_scheme_type.is_null() {
        *out_scheme_type = scheme as i32;
    }

    if valid { 1 } else { 0 }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_valid_urls() {
        assert!(is_url("https://google.com").0);
        assert!(is_url("http://localhost:8080").0);
        assert!(is_url("github.com/Flow-Launcher/Flow.Launcher").0);
        assert!(is_url("192.168.1.1:80").0);
        assert!(is_url("about:blank").0);
        assert!(is_url("chrome://settings").0);
        assert!(is_url("sub.domain.co.uk/path?arg=1#hash").0);
    }

    #[test]
    fn test_invalid_urls() {
        assert!(!is_url("").0);
        assert!(!is_url("12345").0);
        assert!(!is_url("12.34").0);
        assert!(!is_url("hello world").0);
        assert!(!is_url("some text with spaces.com").0);
        assert!(!is_url("0.0.0.0").0);
        assert!(!is_url("http://.com").0);
    }
}
