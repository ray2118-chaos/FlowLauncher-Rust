#[derive(Debug, Clone)]
pub struct EmojiEntry {
    pub glyph: &'static str,
    pub name: &'static str,
    pub category: &'static str,
    pub keywords: &'static [&'static str],
    pub codepoint: &'static str,
}

pub struct EmojiMatcher;

impl EmojiMatcher {
    /// Check whether a search query matches the emoji name or any associated keywords
    pub fn matches(entry: &EmojiEntry, query: &str) -> bool {
        let q = query.to_lowercase();
        if entry.name.to_lowercase().contains(&q) {
            return true;
        }
        for kw in entry.keywords {
            if kw.to_lowercase().contains(&q) {
                return true;
            }
        }
        false
    }
}
