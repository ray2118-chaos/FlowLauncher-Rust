use std::f64::consts::{E, PI, TAU};
use windows_sys::Win32::Foundation::SYSTEMTIME;
use windows_sys::Win32::System::SystemInformation::{GetLocalTime, GetSystemTime};

#[derive(Debug, PartialEq, Clone)]
enum Token {
    Number(f64),
    Ident(String),
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Caret,
    LParen,
    RParen,
    Comma,
}

struct Lexer<'a> {
    chars: Vec<char>,
    pos: usize,
    _marker: std::marker::PhantomData<&'a ()>,
}

impl<'a> Lexer<'a> {
    fn new(input: &'a str) -> Self {
        Self {
            chars: input.chars().collect(),
            pos: 0,
            _marker: std::marker::PhantomData,
        }
    }

    fn peek(&self) -> Option<char> {
        self.chars.get(self.pos).copied()
    }

    fn peek_at(&self, offset: usize) -> Option<char> {
        self.chars.get(self.pos + offset).copied()
    }

    fn advance(&mut self) -> Option<char> {
        let ch = self.chars.get(self.pos).copied();
        if ch.is_some() {
            self.pos += 1;
        }
        ch
    }

    fn tokenize(&mut self) -> Result<Vec<Token>, String> {
        let mut tokens = Vec::new();

        while let Some(ch) = self.peek() {
            if ch.is_whitespace() {
                self.advance();
                continue;
            }

            if ch.is_ascii_digit() || ch == '.' {
                tokens.push(self.read_number()?);
                continue;
            }

            if ch.is_alphabetic() || ch == '_' {
                tokens.push(self.read_ident());
                continue;
            }

            match ch {
                '+' => {
                    self.advance();
                    tokens.push(Token::Plus);
                }
                '-' => {
                    self.advance();
                    tokens.push(Token::Minus);
                }
                '*' => {
                    self.advance();
                    if self.peek() == Some('*') {
                        self.advance();
                        tokens.push(Token::Caret);
                    } else {
                        tokens.push(Token::Star);
                    }
                }
                '/' => {
                    self.advance();
                    tokens.push(Token::Slash);
                }
                '%' => {
                    self.advance();
                    tokens.push(Token::Percent);
                }
                '^' => {
                    self.advance();
                    tokens.push(Token::Caret);
                }
                '(' => {
                    self.advance();
                    tokens.push(Token::LParen);
                }
                ')' => {
                    self.advance();
                    tokens.push(Token::RParen);
                }
                ',' => {
                    self.advance();
                    tokens.push(Token::Comma);
                }
                _ => {
                    self.advance();
                }
            }
        }

        // Insert implicit multiplication:
        // Number followed by LParen or Ident: 2(3) -> 2 * (3), 2pi -> 2 * pi
        // RParen followed by Number, Ident, LParen: (2)(3) -> (2) * (3)
        let mut processed = Vec::with_capacity(tokens.len() * 2);
        for i in 0..tokens.len() {
            processed.push(tokens[i].clone());
            if i + 1 < tokens.len() {
                let current = &tokens[i];
                let next = &tokens[i + 1];
                let needs_mul = matches!(
                    (current, next),
                    (Token::Number(_), Token::LParen)
                        | (Token::Number(_), Token::Ident(_))
                        | (Token::RParen, Token::Number(_))
                        | (Token::RParen, Token::Ident(_))
                        | (Token::RParen, Token::LParen)
                );
                if needs_mul {
                    processed.push(Token::Star);
                }
            }
        }

        Ok(processed)
    }

    fn read_number(&mut self) -> Result<Token, String> {
        let mut s = String::new();
        let mut has_dot = false;
        let mut has_exp = false;

        while let Some(ch) = self.peek() {
            if ch.is_ascii_digit() {
                s.push(ch);
                self.advance();
            } else if ch == '.' && !has_dot && !has_exp {
                has_dot = true;
                s.push(ch);
                self.advance();
            } else if ch == ',' {
                // Check if this comma is a thousands separator in a number (e.g. 1,000 or 1,234,567)
                // It is a thousands separator if followed by 3 digits
                if self.peek_at(1).map_or(false, |c| c.is_ascii_digit())
                    && self.peek_at(2).map_or(false, |c| c.is_ascii_digit())
                    && self.peek_at(3).map_or(false, |c| c.is_ascii_digit())
                    && !self.peek_at(4).map_or(false, |c| c.is_ascii_digit())
                {
                    self.advance(); // Skip thousands separator comma
                } else {
                    break;
                }
            } else if (ch == 'e' || ch == 'E') && !has_exp {
                has_exp = true;
                s.push(ch);
                self.advance();
                if let Some(sign) = self.peek() {
                    if sign == '+' || sign == '-' {
                        s.push(sign);
                        self.advance();
                    }
                }
            } else {
                break;
            }
        }

        s.parse::<f64>()
            .map(Token::Number)
            .map_err(|_| format!("Invalid number: {}", s))
    }

    fn read_ident(&mut self) -> Token {
        let mut s = String::new();
        while let Some(ch) = self.peek() {
            if ch.is_alphanumeric() || ch == '_' {
                s.push(ch);
                self.advance();
            } else {
                break;
            }
        }
        Token::Ident(s.to_lowercase())
    }
}

struct Parser {
    tokens: Vec<Token>,
    pos: usize,
}

impl Parser {
    fn new(tokens: Vec<Token>) -> Self {
        Self { tokens, pos: 0 }
    }

    fn current(&self) -> Option<&Token> {
        self.tokens.get(self.pos)
    }

    fn advance(&mut self) -> Option<&Token> {
        let tok = self.tokens.get(self.pos);
        if tok.is_some() {
            self.pos += 1;
        }
        tok
    }

    fn parse_expression(&mut self) -> Result<f64, String> {
        self.parse_additive()
    }

    fn parse_additive(&mut self) -> Result<f64, String> {
        let mut left = self.parse_multiplicative()?;

        while let Some(tok) = self.current() {
            match tok {
                Token::Plus => {
                    self.advance();
                    let right = self.parse_multiplicative()?;
                    left += right;
                }
                Token::Minus => {
                    self.advance();
                    let right = self.parse_multiplicative()?;
                    left -= right;
                }
                _ => break,
            }
        }

        Ok(left)
    }

    fn parse_multiplicative(&mut self) -> Result<f64, String> {
        let mut left = self.parse_power()?;

        while let Some(tok) = self.current() {
            match tok {
                Token::Star => {
                    self.advance();
                    let right = self.parse_power()?;
                    left *= right;
                }
                Token::Slash => {
                    self.advance();
                    let right = self.parse_power()?;
                    if right == 0.0 {
                        return Err("Division by zero".to_string());
                    }
                    left /= right;
                }
                Token::Percent => {
                    self.advance();
                    let right = self.parse_power()?;
                    if right == 0.0 {
                        return Err("Modulo by zero".to_string());
                    }
                    left %= right;
                }
                _ => break,
            }
        }

        Ok(left)
    }

    fn parse_power(&mut self) -> Result<f64, String> {
        let left = self.parse_unary()?;

        if let Some(Token::Caret) = self.current() {
            self.advance();
            let right = self.parse_power()?;
            return Ok(left.powf(right));
        }

        Ok(left)
    }

    fn parse_unary(&mut self) -> Result<f64, String> {
        if let Some(tok) = self.current() {
            match tok {
                Token::Plus => {
                    self.advance();
                    return self.parse_unary();
                }
                Token::Minus => {
                    self.advance();
                    let val = self.parse_unary()?;
                    return Ok(-val);
                }
                _ => {}
            }
        }

        self.parse_primary()
    }

    fn parse_primary(&mut self) -> Result<f64, String> {
        match self.current().cloned() {
            Some(Token::Number(n)) => {
                self.advance();
                Ok(n)
            }
            Some(Token::Ident(name)) => {
                self.advance();
                match name.as_str() {
                    "pi" => Ok(PI),
                    "e" => Ok(E),
                    "tau" => Ok(TAU),
                    func_name => {
                        // Function call
                        if let Some(Token::LParen) = self.current() {
                            self.advance();
                            let mut args = Vec::new();
                            if self.current() != Some(&Token::RParen) {
                                args.push(self.parse_expression()?);
                                while let Some(Token::Comma) = self.current() {
                                    self.advance();
                                    args.push(self.parse_expression()?);
                                }
                            }
                            if let Some(Token::RParen) = self.current() {
                                self.advance();
                            } else {
                                return Err(format!("Expected ')' after function arguments for {}", func_name));
                            }

                            match func_name {
                                "pow" => {
                                    if args.len() != 2 {
                                        return Err("pow requires 2 arguments".to_string());
                                    }
                                    Ok(args[0].powf(args[1]))
                                }
                                "min" => {
                                    if args.is_empty() {
                                        return Err("min requires arguments".to_string());
                                    }
                                    Ok(args.into_iter().fold(f64::INFINITY, f64::min))
                                }
                                "max" => {
                                    if args.is_empty() {
                                        return Err("max requires arguments".to_string());
                                    }
                                    Ok(args.into_iter().fold(f64::NEG_INFINITY, f64::max))
                                }
                                "sqrt" => {
                                    if args.is_empty() || args[0] < 0.0 {
                                        return Err("Cannot take sqrt of negative number".to_string());
                                    }
                                    Ok(args[0].sqrt())
                                }
                                "cbrt" => Ok(args.first().copied().unwrap_or(0.0).cbrt()),
                                "sin" => Ok(args.first().copied().unwrap_or(0.0).sin()),
                                "cos" => Ok(args.first().copied().unwrap_or(0.0).cos()),
                                "tan" => Ok(args.first().copied().unwrap_or(0.0).tan()),
                                "asin" => Ok(args.first().copied().unwrap_or(0.0).asin()),
                                "acos" => Ok(args.first().copied().unwrap_or(0.0).acos()),
                                "atan" => Ok(args.first().copied().unwrap_or(0.0).atan()),
                                "sinh" => Ok(args.first().copied().unwrap_or(0.0).sinh()),
                                "cosh" => Ok(args.first().copied().unwrap_or(0.0).cosh()),
                                "tanh" => Ok(args.first().copied().unwrap_or(0.0).tanh()),
                                "ln" => {
                                    let arg = args.first().copied().unwrap_or(0.0);
                                    if arg <= 0.0 {
                                        return Err("ln of non-positive number".to_string());
                                    }
                                    Ok(arg.ln())
                                }
                                "log" | "log10" => {
                                    let arg = args.first().copied().unwrap_or(0.0);
                                    if arg <= 0.0 {
                                        return Err("log of non-positive number".to_string());
                                    }
                                    Ok(arg.log10())
                                }
                                "log2" => {
                                    let arg = args.first().copied().unwrap_or(0.0);
                                    if arg <= 0.0 {
                                        return Err("log2 of non-positive number".to_string());
                                    }
                                    Ok(arg.log2())
                                }
                                "exp" => Ok(args.first().copied().unwrap_or(0.0).exp()),
                                "abs" => Ok(args.first().copied().unwrap_or(0.0).abs()),
                                "round" => Ok(args.first().copied().unwrap_or(0.0).round()),
                                "floor" => Ok(args.first().copied().unwrap_or(0.0).floor()),
                                "ceil" => Ok(args.first().copied().unwrap_or(0.0).ceil()),
                                "deg" => Ok(args.first().copied().unwrap_or(0.0).to_degrees()),
                                "rad" => Ok(args.first().copied().unwrap_or(0.0).to_radians()),
                                _ => Err(format!("Unknown function: {}", func_name)),
                            }
                        } else {
                            Err(format!("Unknown constant or identifier: {}", name))
                        }
                    }
                }
            }
            Some(Token::LParen) => {
                self.advance();
                let val = self.parse_expression()?;
                if let Some(Token::RParen) = self.current() {
                    self.advance();
                    Ok(val)
                } else {
                    Err("Missing closing parenthesis ')'".to_string())
                }
            }
            _ => Err("Unexpected token or end of expression".to_string()),
        }
    }
}

pub fn evaluate(input: &str) -> Result<f64, String> {
    let mut lexer = Lexer::new(input);
    let tokens = lexer.tokenize()?;
    if tokens.is_empty() {
        return Err("Empty expression".to_string());
    }

    let mut parser = Parser::new(tokens);
    let result = parser.parse_expression()?;

    if parser.pos < parser.tokens.len() {
        return Err("Unexpected trailing characters".to_string());
    }

    if result.is_nan() || result.is_infinite() {
        return Err("Result is not a finite number".to_string());
    }

    Ok(result)
}

/// Native C-ABI export
#[no_mangle]
pub unsafe extern "C" fn flow_calc_eval(
    expr_utf16: *const u16,
    expr_len: usize,
    out_result: *mut f64,
) -> i32 {
    if expr_utf16.is_null() || expr_len == 0 || out_result.is_null() {
        return 0;
    }

    let slice = unsafe { std::slice::from_raw_parts(expr_utf16, expr_len) };
    let expr_str = match String::from_utf16(slice) {
        Ok(s) => s,
        Err(_) => return 0,
    };

    match evaluate(&expr_str) {
        Ok(val) => {
            unsafe {
                *out_result = val;
            }
            1
        }
        Err(_) => 0,
    }
}

#[repr(C)]
pub struct CalcQueryResult {
    pub title: [u16; 260],
    pub title_len: u32,
    pub subtitle: [u16; 260],
    pub subtitle_len: u32,
    pub copy_text: [u16; 260],
    pub copy_text_len: u32,
    pub is_currency: i32,
}

fn write_utf16_buf(buf: &mut [u16; 260], out_len: &mut u32, s: &str) {
    let utf16: Vec<u16> = s.encode_utf16().collect();
    let count = utf16.len().min(259);
    buf[..count].copy_from_slice(&utf16[..count]);
    buf[count] = 0;
    *out_len = count as u32;
}

pub fn format_number(val: f64) -> String {
    if val.is_nan() || val.is_infinite() {
        return "Error".to_string();
    }
    if val.fract() == 0.0 && val.abs() < 1e14 {
        let int_val = val as i64;
        let is_neg = int_val < 0;
        let s = int_val.abs().to_string();
        let mut formatted = String::new();
        let bytes = s.as_bytes();
        let len = bytes.len();
        for (i, &b) in bytes.iter().enumerate() {
            if i > 0 && (len - i) % 3 == 0 {
                formatted.push(',');
            }
            formatted.push(b as char);
        }
        if is_neg {
            format!("-{}", formatted)
        } else {
            formatted
        }
    } else {
        let s = format!("{:.4}", val);
        let trimmed = s.trim_end_matches('0').trim_end_matches('.');
        if let Some((int_part, frac_part)) = trimmed.split_once('.') {
            let int_num: i64 = int_part.parse().unwrap_or(0);
            let formatted_int = format_number(int_num as f64);
            format!("{}.{}", formatted_int, frac_part)
        } else {
            trimmed.to_string()
        }
    }
}

// -------------------------------------------------------------
// Currency Conversion Subsystem
// -------------------------------------------------------------

struct CurrencyMeta {
    code: &'static str,
    symbol: &'static str,
    usd_val: f64, // Value of 1 unit in USD
}

static CURRENCIES: &[CurrencyMeta] = &[
    CurrencyMeta { code: "USD", symbol: "$", usd_val: 1.0 },
    CurrencyMeta { code: "EUR", symbol: "€", usd_val: 1.085 },
    CurrencyMeta { code: "GBP", symbol: "£", usd_val: 1.282 },
    CurrencyMeta { code: "INR", symbol: "₹", usd_val: 1.0 / 83.35 },
    CurrencyMeta { code: "JPY", symbol: "¥", usd_val: 1.0 / 153.20 },
    CurrencyMeta { code: "CAD", symbol: "C$", usd_val: 0.725 },
    CurrencyMeta { code: "AUD", symbol: "A$", usd_val: 0.658 },
    CurrencyMeta { code: "CHF", symbol: "CHF", usd_val: 1.112 },
    CurrencyMeta { code: "CNY", symbol: "¥", usd_val: 0.138 },
    CurrencyMeta { code: "SGD", symbol: "S$", usd_val: 0.741 },
    CurrencyMeta { code: "NZD", symbol: "NZ$", usd_val: 0.602 },
    CurrencyMeta { code: "HKD", symbol: "HK$", usd_val: 0.128 },
    CurrencyMeta { code: "KRW", symbol: "₩", usd_val: 1.0 / 1375.0 },
    CurrencyMeta { code: "BRL", symbol: "R$", usd_val: 0.185 },
    CurrencyMeta { code: "MXN", symbol: "Mex$", usd_val: 0.055 },
    CurrencyMeta { code: "ZAR", symbol: "R", usd_val: 0.055 },
    CurrencyMeta { code: "AED", symbol: "AED", usd_val: 0.2723 },
    CurrencyMeta { code: "SAR", symbol: "SAR", usd_val: 0.2666 },
    CurrencyMeta { code: "SEK", symbol: "kr", usd_val: 0.0943 },
    CurrencyMeta { code: "NOK", symbol: "kr", usd_val: 0.0921 },
    CurrencyMeta { code: "TRY", symbol: "₺", usd_val: 0.0308 },
    CurrencyMeta { code: "IDR", symbol: "Rp", usd_val: 1.0 / 16250.0 },
    CurrencyMeta { code: "THB", symbol: "฿", usd_val: 0.0272 },
    CurrencyMeta { code: "PLN", symbol: "zł", usd_val: 0.252 },
    CurrencyMeta { code: "PHP", symbol: "₱", usd_val: 0.0172 },
    CurrencyMeta { code: "MYR", symbol: "RM", usd_val: 0.212 },
    CurrencyMeta { code: "TWD", symbol: "NT$", usd_val: 0.0308 },
    CurrencyMeta { code: "ILS", symbol: "₪", usd_val: 0.270 },
    CurrencyMeta { code: "DKK", symbol: "kr", usd_val: 0.145 },
    CurrencyMeta { code: "CZK", symbol: "Kč", usd_val: 0.043 },
    CurrencyMeta { code: "HUF", symbol: "Ft", usd_val: 0.0027 },
    CurrencyMeta { code: "PKR", symbol: "₨", usd_val: 0.00359 },
    CurrencyMeta { code: "BDT", symbol: "৳", usd_val: 0.0085 },
    CurrencyMeta { code: "VND", symbol: "₫", usd_val: 1.0 / 25400.0 },
    CurrencyMeta { code: "RUB", symbol: "₽", usd_val: 0.0108 },
    // Cryptocurrencies
    CurrencyMeta { code: "BTC", symbol: "₿", usd_val: 67_500.0 },
    CurrencyMeta { code: "ETH", symbol: "Ξ", usd_val: 3_500.0 },
    CurrencyMeta { code: "SOL", symbol: "SOL", usd_val: 155.0 },
    CurrencyMeta { code: "XRP", symbol: "XRP", usd_val: 0.58 },
    CurrencyMeta { code: "DOGE", symbol: "Ð", usd_val: 0.12 },
    CurrencyMeta { code: "ADA", symbol: "ADA", usd_val: 0.45 },
    CurrencyMeta { code: "BNB", symbol: "BNB", usd_val: 580.0 },
    CurrencyMeta { code: "USDT", symbol: "USDT", usd_val: 1.0 },
    CurrencyMeta { code: "USDC", symbol: "USDC", usd_val: 1.0 },
];

fn find_currency(token: &str) -> Option<&'static CurrencyMeta> {
    let clean = token.trim().to_uppercase();
    if clean.is_empty() {
        return None;
    }
    // Match code
    for c in CURRENCIES {
        if c.code == clean || c.symbol.to_uppercase() == clean {
            return Some(c);
        }
    }
    // Handle aliases
    match clean.as_str() {
        "$" | "DOLLAR" | "DOLLARS" | "BUCKS" => find_currency("USD"),
        "€" | "EURO" | "EUROS" => find_currency("EUR"),
        "£" | "POUND" | "POUNDS" => find_currency("GBP"),
        "₹" | "RUPEE" | "RUPEES" => find_currency("INR"),
        "¥" | "YEN" => find_currency("JPY"),
        "YUAN" => find_currency("CNY"),
        "BITCOIN" => find_currency("BTC"),
        "ETHEREUM" => find_currency("ETH"),
        "SOLANA" => find_currency("SOL"),
        _ => None,
    }
}

fn try_currency_convert(query: &str) -> Option<(String, String, String)> {
    let trimmed = query.trim();
    let clean = if let Some(rest) = trimmed.strip_prefix("convert ") {
        rest.trim()
    } else if let Some(rest) = trimmed.strip_prefix("Convert ") {
        rest.trim()
    } else {
        trimmed
    };
    let lower = clean.to_lowercase();
    let parts: Vec<&str> = if let Some(idx) = lower.find(" to ") {
        vec![&clean[..idx], &clean[idx + 4..]]
    } else if let Some(idx) = lower.find(" in ") {
        vec![&clean[..idx], &clean[idx + 4..]]
    } else if let Some(idx) = lower.find(" as ") {
        vec![&clean[..idx], &clean[idx + 4..]]
    } else {
        return None;
    };

    if parts.len() != 2 {
        return None;
    }

    let left = parts[0].trim();
    let right = parts[1].trim();

    let target_curr = find_currency(right)?;

    // Parse amount and source currency from left
    let mut amount = 1.0f64;
    let mut src_curr_token = left;

    // Check if left begins with a number (e.g. "120 usd", "50.5 eur", "$120")
    let first_char = left.chars().next()?;
    if first_char.is_ascii_digit() || first_char == '.' {
        let num_end = left
            .find(|c: char| !c.is_ascii_digit() && c != '.' && c != ',')
            .unwrap_or(left.len());
        let num_str = left[..num_end].replace(',', "");
        if let Ok(val) = num_str.parse::<f64>() {
            amount = val;
            src_curr_token = left[num_end..].trim();
        }
    } else if first_char == '$' || first_char == '€' || first_char == '£' || first_char == '₹' || first_char == '¥' {
        let sym = first_char.to_string();
        src_curr_token = left;
        let rest = left[sym.len()..].trim();
        if let Ok(val) = rest.replace(',', "").parse::<f64>() {
            amount = val;
        }
    }

    let src_curr = find_currency(src_curr_token)?;

    let val_in_usd = amount * src_curr.usd_val;
    let converted = val_in_usd / target_curr.usd_val;

    let title = format!("{} {}", format_number(converted), target_curr.code);
    let rate = src_curr.usd_val / target_curr.usd_val;
    let subtitle = format!(
        "{} {} = {} {} (1 {} ≈ {} {})",
        format_number(amount),
        src_curr.code,
        format_number(converted),
        target_curr.code,
        src_curr.code,
        format_number(rate),
        target_curr.code
    );
    let copy_text = format!("{:.2}", converted);

    Some((title, subtitle, copy_text))
}

// -------------------------------------------------------------
// Physical Unit Conversion Subsystem
// -------------------------------------------------------------

#[allow(dead_code)]
enum UnitCategory {
    Length,
    Mass,
    Digital,
    Time,
    Speed,
    Area,
    Volume,
    Temperature,
}

struct UnitMeta {
    name: &'static str,
    plural: &'static str,
    category: UnitCategory,
    factor: f64, // Factor to base SI unit
}

static UNITS: &[UnitMeta] = &[
    // Length (base = meter)
    UnitMeta { name: "m", plural: "meters", category: UnitCategory::Length, factor: 1.0 },
    UnitMeta { name: "meter", plural: "meters", category: UnitCategory::Length, factor: 1.0 },
    UnitMeta { name: "meters", plural: "meters", category: UnitCategory::Length, factor: 1.0 },
    UnitMeta { name: "km", plural: "kilometers", category: UnitCategory::Length, factor: 1000.0 },
    UnitMeta { name: "kilometer", plural: "kilometers", category: UnitCategory::Length, factor: 1000.0 },
    UnitMeta { name: "kilometers", plural: "kilometers", category: UnitCategory::Length, factor: 1000.0 },
    UnitMeta { name: "cm", plural: "centimeters", category: UnitCategory::Length, factor: 0.01 },
    UnitMeta { name: "centimeter", plural: "centimeters", category: UnitCategory::Length, factor: 0.01 },
    UnitMeta { name: "centimeters", plural: "centimeters", category: UnitCategory::Length, factor: 0.01 },
    UnitMeta { name: "mm", plural: "millimeters", category: UnitCategory::Length, factor: 0.001 },
    UnitMeta { name: "millimeter", plural: "millimeters", category: UnitCategory::Length, factor: 0.001 },
    UnitMeta { name: "millimeters", plural: "millimeters", category: UnitCategory::Length, factor: 0.001 },
    UnitMeta { name: "mi", plural: "miles", category: UnitCategory::Length, factor: 1609.344 },
    UnitMeta { name: "mile", plural: "miles", category: UnitCategory::Length, factor: 1609.344 },
    UnitMeta { name: "miles", plural: "miles", category: UnitCategory::Length, factor: 1609.344 },
    UnitMeta { name: "yd", plural: "yards", category: UnitCategory::Length, factor: 0.9144 },
    UnitMeta { name: "yard", plural: "yards", category: UnitCategory::Length, factor: 0.9144 },
    UnitMeta { name: "yards", plural: "yards", category: UnitCategory::Length, factor: 0.9144 },
    UnitMeta { name: "ft", plural: "feet", category: UnitCategory::Length, factor: 0.3048 },
    UnitMeta { name: "foot", plural: "feet", category: UnitCategory::Length, factor: 0.3048 },
    UnitMeta { name: "feet", plural: "feet", category: UnitCategory::Length, factor: 0.3048 },
    UnitMeta { name: "in", plural: "inches", category: UnitCategory::Length, factor: 0.0254 },
    UnitMeta { name: "inch", plural: "inches", category: UnitCategory::Length, factor: 0.0254 },
    UnitMeta { name: "inches", plural: "inches", category: UnitCategory::Length, factor: 0.0254 },

    // Mass (base = kg)
    UnitMeta { name: "kg", plural: "kg", category: UnitCategory::Mass, factor: 1.0 },
    UnitMeta { name: "kilogram", plural: "kilograms", category: UnitCategory::Mass, factor: 1.0 },
    UnitMeta { name: "kilograms", plural: "kilograms", category: UnitCategory::Mass, factor: 1.0 },
    UnitMeta { name: "g", plural: "grams", category: UnitCategory::Mass, factor: 0.001 },
    UnitMeta { name: "gram", plural: "grams", category: UnitCategory::Mass, factor: 0.001 },
    UnitMeta { name: "grams", plural: "grams", category: UnitCategory::Mass, factor: 0.001 },
    UnitMeta { name: "mg", plural: "milligrams", category: UnitCategory::Mass, factor: 1e-6 },
    UnitMeta { name: "milligram", plural: "milligrams", category: UnitCategory::Mass, factor: 1e-6 },
    UnitMeta { name: "milligrams", plural: "milligrams", category: UnitCategory::Mass, factor: 1e-6 },
    UnitMeta { name: "lb", plural: "lbs", category: UnitCategory::Mass, factor: 0.45359237 },
    UnitMeta { name: "lbs", plural: "lbs", category: UnitCategory::Mass, factor: 0.45359237 },
    UnitMeta { name: "pound", plural: "pounds", category: UnitCategory::Mass, factor: 0.45359237 },
    UnitMeta { name: "pounds", plural: "pounds", category: UnitCategory::Mass, factor: 0.45359237 },
    UnitMeta { name: "oz", plural: "ounces", category: UnitCategory::Mass, factor: 0.028349523 },
    UnitMeta { name: "ounce", plural: "ounces", category: UnitCategory::Mass, factor: 0.028349523 },
    UnitMeta { name: "ounces", plural: "ounces", category: UnitCategory::Mass, factor: 0.028349523 },
    UnitMeta { name: "ton", plural: "tons", category: UnitCategory::Mass, factor: 1000.0 },
    UnitMeta { name: "tonne", plural: "tonnes", category: UnitCategory::Mass, factor: 1000.0 },
    UnitMeta { name: "tonnes", plural: "tonnes", category: UnitCategory::Mass, factor: 1000.0 },
    UnitMeta { name: "stick butter", plural: "sticks of butter", category: UnitCategory::Mass, factor: 0.1133980925 },
    UnitMeta { name: "stick of butter", plural: "sticks of butter", category: UnitCategory::Mass, factor: 0.1133980925 },
    UnitMeta { name: "sticks butter", plural: "sticks of butter", category: UnitCategory::Mass, factor: 0.1133980925 },
    UnitMeta { name: "sticks of butter", plural: "sticks of butter", category: UnitCategory::Mass, factor: 0.1133980925 },

    // Kitchen & Volume (base = liter)
    UnitMeta { name: "l", plural: "liters", category: UnitCategory::Volume, factor: 1.0 },
    UnitMeta { name: "liter", plural: "liters", category: UnitCategory::Volume, factor: 1.0 },
    UnitMeta { name: "liters", plural: "liters", category: UnitCategory::Volume, factor: 1.0 },
    UnitMeta { name: "litre", plural: "litres", category: UnitCategory::Volume, factor: 1.0 },
    UnitMeta { name: "litres", plural: "litres", category: UnitCategory::Volume, factor: 1.0 },
    UnitMeta { name: "ml", plural: "milliliters", category: UnitCategory::Volume, factor: 0.001 },
    UnitMeta { name: "milliliter", plural: "milliliters", category: UnitCategory::Volume, factor: 0.001 },
    UnitMeta { name: "milliliters", plural: "milliliters", category: UnitCategory::Volume, factor: 0.001 },
    UnitMeta { name: "cl", plural: "centiliters", category: UnitCategory::Volume, factor: 0.01 },
    UnitMeta { name: "centiliter", plural: "centiliters", category: UnitCategory::Volume, factor: 0.01 },
    UnitMeta { name: "centiliters", plural: "centiliters", category: UnitCategory::Volume, factor: 0.01 },
    UnitMeta { name: "dl", plural: "deciliters", category: UnitCategory::Volume, factor: 0.1 },
    UnitMeta { name: "deciliter", plural: "deciliters", category: UnitCategory::Volume, factor: 0.1 },
    UnitMeta { name: "deciliters", plural: "deciliters", category: UnitCategory::Volume, factor: 0.1 },
    UnitMeta { name: "cup", plural: "cups", category: UnitCategory::Volume, factor: 0.2365882365 },
    UnitMeta { name: "cups", plural: "cups", category: UnitCategory::Volume, factor: 0.2365882365 },
    UnitMeta { name: "tbsp", plural: "tablespoons", category: UnitCategory::Volume, factor: 0.0147867648 },
    UnitMeta { name: "tbs", plural: "tablespoons", category: UnitCategory::Volume, factor: 0.0147867648 },
    UnitMeta { name: "tablespoon", plural: "tablespoons", category: UnitCategory::Volume, factor: 0.0147867648 },
    UnitMeta { name: "tablespoons", plural: "tablespoons", category: UnitCategory::Volume, factor: 0.0147867648 },
    UnitMeta { name: "tsp", plural: "teaspoons", category: UnitCategory::Volume, factor: 0.0049289216 },
    UnitMeta { name: "teaspoon", plural: "teaspoons", category: UnitCategory::Volume, factor: 0.0049289216 },
    UnitMeta { name: "teaspoons", plural: "teaspoons", category: UnitCategory::Volume, factor: 0.0049289216 },
    UnitMeta { name: "fl oz", plural: "fluid ounces", category: UnitCategory::Volume, factor: 0.0295735296 },
    UnitMeta { name: "floz", plural: "fluid ounces", category: UnitCategory::Volume, factor: 0.0295735296 },
    UnitMeta { name: "fluid ounce", plural: "fluid ounces", category: UnitCategory::Volume, factor: 0.0295735296 },
    UnitMeta { name: "fluid ounces", plural: "fluid ounces", category: UnitCategory::Volume, factor: 0.0295735296 },
    UnitMeta { name: "pt", plural: "pints", category: UnitCategory::Volume, factor: 0.473176473 },
    UnitMeta { name: "pint", plural: "pints", category: UnitCategory::Volume, factor: 0.473176473 },
    UnitMeta { name: "pints", plural: "pints", category: UnitCategory::Volume, factor: 0.473176473 },
    UnitMeta { name: "qt", plural: "quarts", category: UnitCategory::Volume, factor: 0.946352946 },
    UnitMeta { name: "quart", plural: "quarts", category: UnitCategory::Volume, factor: 0.946352946 },
    UnitMeta { name: "quarts", plural: "quarts", category: UnitCategory::Volume, factor: 0.946352946 },
    UnitMeta { name: "gal", plural: "gallons", category: UnitCategory::Volume, factor: 3.785411784 },
    UnitMeta { name: "gallon", plural: "gallons", category: UnitCategory::Volume, factor: 3.785411784 },
    UnitMeta { name: "gallons", plural: "gallons", category: UnitCategory::Volume, factor: 3.785411784 },
    UnitMeta { name: "pinch", plural: "pinches", category: UnitCategory::Volume, factor: 0.000355 },
    UnitMeta { name: "pinches", plural: "pinches", category: UnitCategory::Volume, factor: 0.000355 },
    UnitMeta { name: "dash", plural: "dashes", category: UnitCategory::Volume, factor: 0.00071 },
    UnitMeta { name: "dashes", plural: "dashes", category: UnitCategory::Volume, factor: 0.00071 },
    UnitMeta { name: "drop", plural: "drops", category: UnitCategory::Volume, factor: 0.00005 },
    UnitMeta { name: "drops", plural: "drops", category: UnitCategory::Volume, factor: 0.00005 },

    // Digital Data (base = bytes)
    UnitMeta { name: "b", plural: "bytes", category: UnitCategory::Digital, factor: 1.0 },
    UnitMeta { name: "byte", plural: "bytes", category: UnitCategory::Digital, factor: 1.0 },
    UnitMeta { name: "bytes", plural: "bytes", category: UnitCategory::Digital, factor: 1.0 },
    UnitMeta { name: "kb", plural: "KB", category: UnitCategory::Digital, factor: 1024.0 },
    UnitMeta { name: "kilobyte", plural: "kilobytes", category: UnitCategory::Digital, factor: 1024.0 },
    UnitMeta { name: "kilobytes", plural: "kilobytes", category: UnitCategory::Digital, factor: 1024.0 },
    UnitMeta { name: "mb", plural: "MB", category: UnitCategory::Digital, factor: 1024.0 * 1024.0 },
    UnitMeta { name: "megabyte", plural: "megabytes", category: UnitCategory::Digital, factor: 1024.0 * 1024.0 },
    UnitMeta { name: "megabytes", plural: "megabytes", category: UnitCategory::Digital, factor: 1024.0 * 1024.0 },
    UnitMeta { name: "gb", plural: "GB", category: UnitCategory::Digital, factor: 1024.0 * 1024.0 * 1024.0 },
    UnitMeta { name: "gigabyte", plural: "gigabytes", category: UnitCategory::Digital, factor: 1024.0 * 1024.0 * 1024.0 },
    UnitMeta { name: "gigabytes", plural: "gigabytes", category: UnitCategory::Digital, factor: 1024.0 * 1024.0 * 1024.0 },
    UnitMeta { name: "tb", plural: "TB", category: UnitCategory::Digital, factor: 1024.0 * 1024.0 * 1024.0 * 1024.0 },
    UnitMeta { name: "terabyte", plural: "terabytes", category: UnitCategory::Digital, factor: 1024.0 * 1024.0 * 1024.0 * 1024.0 },
    UnitMeta { name: "terabytes", plural: "terabytes", category: UnitCategory::Digital, factor: 1024.0 * 1024.0 * 1024.0 * 1024.0 },

    // Time (base = seconds)
    UnitMeta { name: "s", plural: "seconds", category: UnitCategory::Time, factor: 1.0 },
    UnitMeta { name: "sec", plural: "seconds", category: UnitCategory::Time, factor: 1.0 },
    UnitMeta { name: "second", plural: "seconds", category: UnitCategory::Time, factor: 1.0 },
    UnitMeta { name: "seconds", plural: "seconds", category: UnitCategory::Time, factor: 1.0 },
    UnitMeta { name: "min", plural: "minutes", category: UnitCategory::Time, factor: 60.0 },
    UnitMeta { name: "minute", plural: "minutes", category: UnitCategory::Time, factor: 60.0 },
    UnitMeta { name: "minutes", plural: "minutes", category: UnitCategory::Time, factor: 60.0 },
    UnitMeta { name: "h", plural: "hours", category: UnitCategory::Time, factor: 3600.0 },
    UnitMeta { name: "hr", plural: "hours", category: UnitCategory::Time, factor: 3600.0 },
    UnitMeta { name: "hour", plural: "hours", category: UnitCategory::Time, factor: 3600.0 },
    UnitMeta { name: "hours", plural: "hours", category: UnitCategory::Time, factor: 3600.0 },
    UnitMeta { name: "d", plural: "days", category: UnitCategory::Time, factor: 86400.0 },
    UnitMeta { name: "day", plural: "days", category: UnitCategory::Time, factor: 86400.0 },
    UnitMeta { name: "days", plural: "days", category: UnitCategory::Time, factor: 86400.0 },
    UnitMeta { name: "week", plural: "weeks", category: UnitCategory::Time, factor: 604800.0 },
    UnitMeta { name: "weeks", plural: "weeks", category: UnitCategory::Time, factor: 604800.0 },

    // Speed (base = m/s)
    UnitMeta { name: "mps", plural: "m/s", category: UnitCategory::Speed, factor: 1.0 },
    UnitMeta { name: "m/s", plural: "m/s", category: UnitCategory::Speed, factor: 1.0 },
    UnitMeta { name: "kmh", plural: "km/h", category: UnitCategory::Speed, factor: 1.0 / 3.6 },
    UnitMeta { name: "kph", plural: "km/h", category: UnitCategory::Speed, factor: 1.0 / 3.6 },
    UnitMeta { name: "km/h", plural: "km/h", category: UnitCategory::Speed, factor: 1.0 / 3.6 },
    UnitMeta { name: "mph", plural: "mph", category: UnitCategory::Speed, factor: 0.44704 },

    // Temperature (handled specially)
    UnitMeta { name: "c", plural: "°C", category: UnitCategory::Temperature, factor: 0.0 },
    UnitMeta { name: "degc", plural: "°C", category: UnitCategory::Temperature, factor: 0.0 },
    UnitMeta { name: "celsius", plural: "°C", category: UnitCategory::Temperature, factor: 0.0 },
    UnitMeta { name: "f", plural: "°F", category: UnitCategory::Temperature, factor: 0.0 },
    UnitMeta { name: "degf", plural: "°F", category: UnitCategory::Temperature, factor: 0.0 },
    UnitMeta { name: "fahrenheit", plural: "°F", category: UnitCategory::Temperature, factor: 0.0 },
    UnitMeta { name: "k", plural: "K", category: UnitCategory::Temperature, factor: 0.0 },
    UnitMeta { name: "kelvin", plural: "K", category: UnitCategory::Temperature, factor: 0.0 },
];

fn find_unit(token: &str) -> Option<&'static UnitMeta> {
    let clean = token.trim().to_lowercase();
    if clean.is_empty() {
        return None;
    }
    for u in UNITS {
        if u.name == clean || u.plural == clean {
            return Some(u);
        }
    }
    None
}

fn try_unit_convert(query: &str) -> Option<(String, String, String)> {
    let trimmed = query.trim();
    let clean = if let Some(rest) = trimmed.strip_prefix("convert ") {
        rest.trim()
    } else if let Some(rest) = trimmed.strip_prefix("Convert ") {
        rest.trim()
    } else {
        trimmed
    };
    let lower = clean.to_lowercase();
    let parts: Vec<&str> = if let Some(idx) = lower.find(" to ") {
        vec![&clean[..idx], &clean[idx + 4..]]
    } else if let Some(idx) = lower.find(" in ") {
        vec![&clean[..idx], &clean[idx + 4..]]
    } else if let Some(idx) = lower.find(" as ") {
        vec![&clean[..idx], &clean[idx + 4..]]
    } else {
        return None;
    };

    if parts.len() != 2 {
        return None;
    }

    let left = parts[0].trim();
    let right = parts[1].trim();

    let target_unit = find_unit(right)?;

    // Parse amount and source unit
    let mut amount = 1.0f64;
    let mut src_token = left;

    let num_end = left
        .find(|c: char| !c.is_ascii_digit() && c != '.' && c != '-' && c != ',')
        .unwrap_or(0);
    if num_end > 0 {
        let num_str = left[..num_end].replace(',', "");
        if let Ok(val) = num_str.parse::<f64>() {
            amount = val;
            src_token = left[num_end..].trim();
        }
    }

    let src_unit = find_unit(src_token)?;

    // Check same category
    match (&src_unit.category, &target_unit.category) {
        (UnitCategory::Temperature, UnitCategory::Temperature) => {
            let converted = match (src_unit.name, target_unit.name) {
                ("c" | "degc" | "celsius", "f" | "degf" | "fahrenheit") => amount * 9.0 / 5.0 + 32.0,
                ("f" | "degf" | "fahrenheit", "c" | "degc" | "celsius") => (amount - 32.0) * 5.0 / 9.0,
                ("c" | "degc" | "celsius", "k" | "kelvin") => amount + 273.15,
                ("k" | "kelvin", "c" | "degc" | "celsius") => amount - 273.15,
                ("f" | "degf" | "fahrenheit", "k" | "kelvin") => (amount - 32.0) * 5.0 / 9.0 + 273.15,
                ("k" | "kelvin", "f" | "degf" | "fahrenheit") => (amount - 273.15) * 9.0 / 5.0 + 32.0,
                _ => amount,
            };
            let title = format!("{} {}", format_number(converted), target_unit.plural);
            let subtitle = format!(
                "{} {} = {} {}",
                format_number(amount),
                src_unit.plural,
                format_number(converted),
                target_unit.plural
            );
            let copy_text = format!("{:.2}", converted);
            Some((title, subtitle, copy_text))
        }
        (a, b) if std::mem::discriminant(a) == std::mem::discriminant(b) => {
            let base_val = amount * src_unit.factor;
            let converted = base_val / target_unit.factor;
            let title = format!("{} {}", format_number(converted), target_unit.plural);
            let subtitle = format!(
                "{} {} = {} {}",
                format_number(amount),
                src_unit.plural,
                format_number(converted),
                target_unit.plural
            );
            let copy_text = format_number(converted);
            Some((title, subtitle, copy_text))
        }
        _ => None,
    }
}

// -------------------------------------------------------------
// World Time & Date Subsystem
// -------------------------------------------------------------

struct WorldLocation {
    keys: &'static [&'static str],
    name: &'static str,
    country: &'static str,
    tz_abbr: &'static str,
    offset_minutes: i32,
}

static WORLD_LOCATIONS: &[WorldLocation] = &[
    WorldLocation { keys: &["tokyo", "japan", "jst", "osaka", "kyoto"], name: "Tokyo", country: "Japan", tz_abbr: "JST", offset_minutes: 540 },
    WorldLocation { keys: &["london", "uk", "britain", "england", "gmt", "bst", "manchester"], name: "London", country: "United Kingdom", tz_abbr: "GMT/BST", offset_minutes: 60 },
    WorldLocation { keys: &["new york", "nyc", "ny", "est", "edt", "boston", "miami"], name: "New York", country: "United States", tz_abbr: "EDT", offset_minutes: -240 },
    WorldLocation { keys: &["los angeles", "la", "san francisco", "sf", "california", "pst", "pdt", "seattle"], name: "Los Angeles", country: "United States", tz_abbr: "PDT", offset_minutes: -420 },
    WorldLocation { keys: &["chicago", "cst", "cdt", "houston", "dallas"], name: "Chicago", country: "United States", tz_abbr: "CDT", offset_minutes: -300 },
    WorldLocation { keys: &["paris", "france", "cet", "cest", "lyon"], name: "Paris", country: "France", tz_abbr: "CEST", offset_minutes: 120 },
    WorldLocation { keys: &["berlin", "germany", "frankfurt", "munich"], name: "Berlin", country: "Germany", tz_abbr: "CEST", offset_minutes: 120 },
    WorldLocation { keys: &["dubai", "uae", "abu dhabi", "gst"], name: "Dubai", country: "United Arab Emirates", tz_abbr: "GST", offset_minutes: 240 },
    WorldLocation { keys: &["delhi", "new delhi", "mumbai", "india", "ist", "bangalore", "kolkata", "chennai", "hyderabad"], name: "New Delhi", country: "India", tz_abbr: "IST", offset_minutes: 330 },
    WorldLocation { keys: &["singapore", "sgt"], name: "Singapore", country: "Singapore", tz_abbr: "SGT", offset_minutes: 480 },
    WorldLocation { keys: &["beijing", "shanghai", "china", "cst", "shenzhen"], name: "Beijing", country: "China", tz_abbr: "CST", offset_minutes: 480 },
    WorldLocation { keys: &["hong kong", "hk", "hkt"], name: "Hong Kong", country: "China", tz_abbr: "HKT", offset_minutes: 480 },
    WorldLocation { keys: &["seoul", "korea", "south korea", "kst"], name: "Seoul", country: "South Korea", tz_abbr: "KST", offset_minutes: 540 },
    WorldLocation { keys: &["sydney", "australia", "melbourne", "aest", "aedt"], name: "Sydney", country: "Australia", tz_abbr: "AEDT", offset_minutes: 660 },
    WorldLocation { keys: &["auckland", "new zealand", "wellington", "nzst", "nzdt"], name: "Auckland", country: "New Zealand", tz_abbr: "NZDT", offset_minutes: 780 },
    WorldLocation { keys: &["bangkok", "thailand", "ict"], name: "Bangkok", country: "Thailand", tz_abbr: "ICT", offset_minutes: 420 },
    WorldLocation { keys: &["moscow", "russia", "msk"], name: "Moscow", country: "Russia", tz_abbr: "MSK", offset_minutes: 180 },
    WorldLocation { keys: &["cairo", "egypt"], name: "Cairo", country: "Egypt", tz_abbr: "EEST", offset_minutes: 180 },
    WorldLocation { keys: &["sao paulo", "brazil", "rio", "brt"], name: "São Paulo", country: "Brazil", tz_abbr: "BRT", offset_minutes: -180 },
    WorldLocation { keys: &["toronto", "canada", "montreal", "vancouver", "ottawa"], name: "Toronto", country: "Canada", tz_abbr: "EDT", offset_minutes: -240 },
    WorldLocation { keys: &["rome", "italy", "milan"], name: "Rome", country: "Italy", tz_abbr: "CEST", offset_minutes: 120 },
    WorldLocation { keys: &["madrid", "spain", "barcelona"], name: "Madrid", country: "Spain", tz_abbr: "CEST", offset_minutes: 120 },
    WorldLocation { keys: &["amsterdam", "netherlands"], name: "Amsterdam", country: "Netherlands", tz_abbr: "CEST", offset_minutes: 120 },
    WorldLocation { keys: &["zurich", "switzerland", "geneva"], name: "Zurich", country: "Switzerland", tz_abbr: "CEST", offset_minutes: 120 },
    WorldLocation { keys: &["istanbul", "turkey"], name: "Istanbul", country: "Turkey", tz_abbr: "TRT", offset_minutes: 180 },
    WorldLocation { keys: &["riyadh", "saudi arabia"], name: "Riyadh", country: "Saudi Arabia", tz_abbr: "AST", offset_minutes: 180 },
    WorldLocation { keys: &["hawaii", "honolulu", "hst"], name: "Honolulu", country: "United States", tz_abbr: "HST", offset_minutes: -600 },
    WorldLocation { keys: &["utc", "gmt", "zulu"], name: "UTC", country: "Coordinated Universal Time", tz_abbr: "UTC", offset_minutes: 0 },
];

fn format_time(hour: u16, min: u16, _sec: u16) -> (String, String) {
    let period = if hour >= 12 { "PM" } else { "AM" };
    let h12 = match hour % 12 {
        0 => 12,
        h => h,
    };
    let time12 = format!("{:02}:{:02} {}", h12, min, period);
    let time24 = format!("{:02}:{:02}", hour, min);
    (time12, time24)
}

fn format_date(year: u16, month: u16, day: u16, dow: u16) -> String {
    let days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
    let months = [
        "", "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];
    let day_name = days.get(dow as usize).copied().unwrap_or("");
    let month_name = months.get(month as usize).copied().unwrap_or("");
    format!("{}, {} {} {}", day_name, day, month_name, year)
}

fn find_world_location(query: &str) -> Option<&'static WorldLocation> {
    let clean = query.trim().to_lowercase();
    for loc in WORLD_LOCATIONS {
        for &k in loc.keys {
            if k == clean || loc.name.to_lowercase() == clean || loc.country.to_lowercase() == clean {
                return Some(loc);
            }
        }
    }
    for loc in WORLD_LOCATIONS {
        for &k in loc.keys {
            if clean.contains(k) || k.contains(&clean) {
                return Some(loc);
            }
        }
    }
    None
}

fn try_time_date_query(query: &str) -> Option<(String, String, String)> {
    let lower = query.to_lowercase();
    let trimmed = lower.trim();

    // 1. Current Time query ("time", "current time", "now", "local time")
    if trimmed == "time" || trimmed == "current time" || trimmed == "now" || trimmed == "local time" {
        let mut local: SYSTEMTIME = unsafe { std::mem::zeroed() };
        unsafe { GetLocalTime(&mut local) };
        let (t12, t24) = format_time(local.wHour, local.wMinute, local.wSecond);
        let date_str = format_date(local.wYear, local.wMonth, local.wDay, local.wDayOfWeek);
        let title = format!("{} ({})", t12, t24);
        let sub = format!("Current Local Time • {}", date_str);
        return Some((title, sub, t12));
    }

    // 2. Current Date query ("date", "today", "current date")
    if trimmed == "date" || trimmed == "today" || trimmed == "current date" {
        let mut local: SYSTEMTIME = unsafe { std::mem::zeroed() };
        unsafe { GetLocalTime(&mut local) };
        let date_str = format_date(local.wYear, local.wMonth, local.wDay, local.wDayOfWeek);
        let (t12, _) = format_time(local.wHour, local.wMinute, local.wSecond);
        let title = date_str.clone();
        let sub = format!("Current Date • Local Time: {}", t12);
        return Some((title, sub, date_str));
    }

    // 3. World Time query ("time in <location>", "time at <location>", "time for <location>", "time <location>")
    let location_query = if let Some(rest) = trimmed.strip_prefix("time in ") {
        Some(rest.trim())
    } else if let Some(rest) = trimmed.strip_prefix("time at ") {
        Some(rest.trim())
    } else if let Some(rest) = trimmed.strip_prefix("time for ") {
        Some(rest.trim())
    } else if trimmed.starts_with("time ") && !trimmed.contains('+') && !trimmed.contains('-') && !trimmed.contains('*') && !trimmed.contains('/') {
        Some(trimmed["time ".len()..].trim())
    } else {
        None
    };

    if let Some(target) = location_query {
        if target.is_empty() {
            return None;
        }

        let loc = find_world_location(target)?;

        let mut utc: SYSTEMTIME = unsafe { std::mem::zeroed() };
        unsafe { GetSystemTime(&mut utc) };

        let mut local: SYSTEMTIME = unsafe { std::mem::zeroed() };
        unsafe { GetLocalTime(&mut local) };

        let utc_min = (utc.wHour as i32) * 60 + (utc.wMinute as i32);
        let target_min_total = utc_min + loc.offset_minutes;

        let normalized_min = target_min_total.rem_euclid(24 * 60) as u16;
        let target_hour = normalized_min / 60;
        let target_minute = normalized_min % 60;

        let (t12, t24) = format_time(target_hour, target_minute, utc.wSecond);

        let local_min_total = (local.wHour as i32) * 60 + (local.wMinute as i32);
        let local_offset_min = (local_min_total - utc_min + 720).rem_euclid(1440) - 720;
        let diff_min = loc.offset_minutes - local_offset_min;

        let diff_str = if diff_min == 0 {
            "Same time as local".to_string()
        } else if diff_min > 0 {
            let h = diff_min / 60;
            let m = diff_min % 60;
            if m == 0 {
                format!("+{}h ahead of local", h)
            } else {
                format!("+{}h {}m ahead of local", h, m)
            }
        } else {
            let abs_diff = -diff_min;
            let h = abs_diff / 60;
            let m = abs_diff % 60;
            if m == 0 {
                format!("-{}h behind local", h)
            } else {
                format!("-{}h {}m behind local", h, m)
            }
        };

        let sign = if loc.offset_minutes >= 0 { "+" } else { "-" };
        let off_h = (loc.offset_minutes.abs()) / 60;
        let off_m = (loc.offset_minutes.abs()) % 60;

        let title = format!("{} ({})", t12, t24);
        let sub = format!(
            "{}, {} ({}, UTC{}{:02}:{:02}) • {}",
            loc.name, loc.country, loc.tz_abbr, sign, off_h, off_m, diff_str
        );
        return Some((title, sub, t12));
    }

    None
}

pub fn process_calculator_query(input: &str) -> Option<(String, String, String, bool)> {
    let raw = input.trim();
    if raw.is_empty() {
        return None;
    }

    // 1. Try Date & World Time Queries
    if let Some((title, sub, copy)) = try_time_date_query(raw) {
        return Some((title, sub, copy, false));
    }

    // 2. Try Currency Conversion
    if let Some((title, sub, copy)) = try_currency_convert(raw) {
        return Some((title, sub, copy, true));
    }

    // 3. Try Physical Unit Conversion
    if let Some((title, sub, copy)) = try_unit_convert(raw) {
        return Some((title, sub, copy, false));
    }

    // 4. Fallback to Math Expression Evaluation
    if let Ok(result) = evaluate(raw) {
        let formatted = format_number(result);
        let copy_val = if result.fract() == 0.0 && result.abs() < 1e14 {
            (result as i64).to_string()
        } else {
            result.to_string()
        };
        let sub = format!("{} = {}", raw, formatted);
        return Some((formatted, sub, copy_val, false));
    }

    None
}

/// Comprehensive native Calculator Query API export
#[no_mangle]
pub unsafe extern "C" fn flow_calc_query(
    query_utf16: *const u16,
    query_len: usize,
    out_result: *mut CalcQueryResult,
) -> i32 {
    if query_utf16.is_null() || query_len == 0 || out_result.is_null() {
        return 0;
    }

    let slice = unsafe { std::slice::from_raw_parts(query_utf16, query_len) };
    let query_str = match String::from_utf16(slice) {
        Ok(s) => s,
        Err(_) => return 0,
    };

    if let Some((title, sub, copy, is_curr)) = process_calculator_query(&query_str) {
        let res = unsafe { &mut *out_result };
        write_utf16_buf(&mut res.title, &mut res.title_len, &title);
        write_utf16_buf(&mut res.subtitle, &mut res.subtitle_len, &sub);
        write_utf16_buf(&mut res.copy_text, &mut res.copy_text_len, &copy);
        res.is_currency = if is_curr { 1 } else { 0 };
        1
    } else {
        0
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_eval() {
        assert_eq!(evaluate("2 + 2 * 3").unwrap(), 8.0);
        assert_eq!(evaluate("(2 + 2) * 3").unwrap(), 12.0);
        assert_eq!(evaluate("2^10").unwrap(), 1024.0);
        assert_eq!(evaluate("10 % 3").unwrap(), 1.0);
        assert_eq!(evaluate("sqrt(16)").unwrap(), 4.0);
        assert_eq!(evaluate("abs(-42)").unwrap(), 42.0);
        assert_eq!(evaluate("2pi").unwrap(), TAU);
        assert_eq!(evaluate("1,000 + 250").unwrap(), 1250.0);
        assert_eq!(evaluate("pow(2,3)").unwrap(), 8.0);
        assert_eq!(evaluate("min(1,-1,-2)").unwrap(), -2.0);
        assert_eq!(evaluate("max(1,-1,-2)").unwrap(), 1.0);
        assert!(evaluate("10/0").is_err());
        assert!(evaluate("sqrt(-1)").is_err());
        assert!(evaluate("log(0)").is_err());
    }

    #[test]
    fn test_currency() {
        let res = try_currency_convert("120 usd to inr").unwrap();
        assert!(res.0.contains("INR"));
        assert!(res.1.contains("83.35"));

        let res2 = try_currency_convert("50 eur in gbp").unwrap();
        assert!(res2.0.contains("GBP"));

        let res3 = try_currency_convert("0.5 btc in usd").unwrap();
        assert!(res3.0.contains("USD"));
    }

    #[test]
    fn test_units() {
        let res = try_unit_convert("100 km to miles").unwrap();
        assert!(res.0.contains("miles"));

        let res2 = try_unit_convert("50 kg in lbs").unwrap();
        assert!(res2.0.contains("lbs"));

        let res3 = try_unit_convert("100 f to c").unwrap();
        assert!(res3.0.contains("37.7778"));

        let res4 = try_unit_convert("16 gb to mb").unwrap();
        assert!(res4.0.contains("16,384"));

        // Kitchen cooking tests
        let res5 = try_unit_convert("1 cup to ml").unwrap();
        assert!(res5.0.contains("milliliters"));
        assert!(res5.0.contains("236.5"));

        let res6 = try_unit_convert("convert 2 tbsp to tsp").unwrap();
        assert!(res6.0.contains("teaspoons"));
        assert!(res6.0.contains("6"));

        let res7 = try_unit_convert("1 stick butter to grams").unwrap();
        assert!(res7.0.contains("113.3"));

        let res8 = try_unit_convert("convert 350 f in c").unwrap();
        assert!(res8.0.contains("176.6"));
    }

    #[test]
    fn test_time_and_date() {
        let (title, sub, copy) = try_time_date_query("time in tokyo").unwrap();
        assert!(!title.is_empty());
        assert!(sub.contains("Tokyo, Japan"));
        assert!(sub.contains("JST"));
        assert!(!copy.is_empty());

        let (title_ny, sub_ny, _) = try_time_date_query("time in new york").unwrap();
        assert!(!title_ny.is_empty());
        assert!(sub_ny.contains("New York"));

        let (title_curr, sub_curr, _) = try_time_date_query("time").unwrap();
        assert!(!title_curr.is_empty());
        assert!(sub_curr.contains("Current Local Time"));

        let (title_date, sub_date, _) = try_time_date_query("date").unwrap();
        assert!(!title_date.is_empty());
        assert!(sub_date.contains("Current Date"));
    }
}

