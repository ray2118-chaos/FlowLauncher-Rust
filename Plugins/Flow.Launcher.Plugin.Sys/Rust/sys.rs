#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SysCommandType {
    None = 0,
    Volume = 1,
    Brightness = 2,
    Mute = 3,
}

#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SysOpType {
    Status = 0,
    SetAbsolute = 1,
    Increase = 2,
    Decrease = 3,
    Mute = 4,
    Unmute = 5,
    ToggleMute = 6,
}

#[repr(C)]
pub struct NativeSysParsedCommand {
    pub cmd_type: u32,
    pub op_type: u32,
    pub delta_or_target: i32,
    pub title: [u16; 260],
    pub title_len: u32,
    pub subtitle: [u16; 260],
    pub subtitle_len: u32,
}

impl Default for NativeSysParsedCommand {
    fn default() -> Self {
        Self {
            cmd_type: SysCommandType::None as u32,
            op_type: SysOpType::Status as u32,
            delta_or_target: 0,
            title: [0; 260],
            title_len: 0,
            subtitle: [0; 260],
            subtitle_len: 0,
        }
    }
}

fn write_utf16(buf: &mut [u16; 260], len: &mut u32, text: &str) {
    let utf16: Vec<u16> = text.encode_utf16().collect();
    let l = std::cmp::min(utf16.len(), 259);
    buf[..l].copy_from_slice(&utf16[..l]);
    buf[l] = 0;
    *len = l as u32;
}

#[derive(Debug, PartialEq, Eq)]
pub struct SysParsedResult {
    pub cmd_type: SysCommandType,
    pub op_type: SysOpType,
    pub delta_or_target: i32,
    pub title: String,
    pub subtitle: String,
}

pub fn parse_sys_query(input: &str) -> Option<SysParsedResult> {
    let raw = input.trim();
    if raw.is_empty() {
        return None;
    }

    let lower = raw.to_lowercase();
    let cleaned = lower.replace('%', "").trim().to_string();

    // 1. Mute Commands
    if cleaned == "mute" || cleaned == "mute volume" || cleaned == "silence" {
        return Some(SysParsedResult {
            cmd_type: SysCommandType::Mute,
            op_type: SysOpType::Mute,
            delta_or_target: 0,
            title: "Mute Audio".to_string(),
            subtitle: "Silence system master volume".to_string(),
        });
    }
    if cleaned == "unmute" || cleaned == "unmute volume" {
        return Some(SysParsedResult {
            cmd_type: SysCommandType::Mute,
            op_type: SysOpType::Unmute,
            delta_or_target: 0,
            title: "Unmute Audio".to_string(),
            subtitle: "Restore system master volume".to_string(),
        });
    }
    if cleaned == "toggle mute" || cleaned == "togglemute" {
        return Some(SysParsedResult {
            cmd_type: SysCommandType::Mute,
            op_type: SysOpType::ToggleMute,
            delta_or_target: 0,
            title: "Toggle Mute".to_string(),
            subtitle: "Toggle master audio mute state".to_string(),
        });
    }

    // 2. Volume Commands
    if let Some(res) = parse_volume_command(&cleaned) {
        return Some(res);
    }

    // 3. Brightness Commands
    if let Some(res) = parse_brightness_command(&cleaned) {
        return Some(res);
    }

    None
}

fn parse_volume_command(s: &str) -> Option<SysParsedResult> {
    // Shorthand: "vol", "volume"
    if s == "vol" || s == "volume" {
        return Some(SysParsedResult {
            cmd_type: SysCommandType::Volume,
            op_type: SysOpType::Status,
            delta_or_target: 0,
            title: "Volume Status".to_string(),
            subtitle: "Current system master volume".to_string(),
        });
    }

    // Natural Language: "increase vol by 2", "raise volume by 5", "turn up volume by 10", "increase vol 2"
    let increase_prefixes = [
        "increase volume by ", "increase vol by ", "raise volume by ", "raise vol by ",
        "turn up volume by ", "turn up vol by ", "boost volume by ", "boost vol by ",
        "increase volume ", "increase vol ", "raise volume ", "raise vol ",
        "turn up volume ", "turn up vol ",
    ];
    for prefix in increase_prefixes {
        if let Some(rest) = s.strip_prefix(prefix) {
            if let Ok(num) = rest.trim().parse::<i32>() {
                let n = num.clamp(1, 100);
                return Some(SysParsedResult {
                    cmd_type: SysCommandType::Volume,
                    op_type: SysOpType::Increase,
                    delta_or_target: n,
                    title: format!("Increase Volume by {}%", n),
                    subtitle: format!("Raise current master volume by +{}%", n),
                });
            }
        }
    }

    // Natural Language: "decrease vol by 2", "lower volume by 5", "reduce volume by 10", "turn down volume by 2"
    let decrease_prefixes = [
        "decrease volume by ", "decrease vol by ", "lower volume by ", "lower vol by ",
        "reduce volume by ", "reduce vol by ", "turn down volume by ", "turn down vol by ",
        "decrease volume ", "decrease vol ", "lower volume ", "lower vol ",
        "reduce volume ", "reduce vol ", "turn down volume ", "turn down vol ",
    ];
    for prefix in decrease_prefixes {
        if let Some(rest) = s.strip_prefix(prefix) {
            if let Ok(num) = rest.trim().parse::<i32>() {
                let n = num.clamp(1, 100);
                return Some(SysParsedResult {
                    cmd_type: SysCommandType::Volume,
                    op_type: SysOpType::Decrease,
                    delta_or_target: n,
                    title: format!("Decrease Volume by {}%", n),
                    subtitle: format!("Lower current master volume by -{}%", n),
                });
            }
        }
    }

    // "set volume to 50", "set vol to 80", "set volume 50"
    let set_prefixes = ["set volume to ", "set vol to ", "set volume ", "set vol "];
    for prefix in set_prefixes {
        if let Some(rest) = s.strip_prefix(prefix) {
            if let Ok(num) = rest.trim().parse::<i32>() {
                let n = num.clamp(0, 100);
                return Some(SysParsedResult {
                    cmd_type: SysCommandType::Volume,
                    op_type: SysOpType::SetAbsolute,
                    delta_or_target: n,
                    title: format!("Set Volume to {}%", n),
                    subtitle: format!("Adjust master volume to {}%", n),
                });
            }
        }
    }

    // Shorthand: "vol +2", "volume +5", "vol + 2", "volume+2"
    let plus_prefixes = ["vol +", "volume +", "vol+", "volume+"];
    for prefix in plus_prefixes {
        if let Some(rest) = s.strip_prefix(prefix) {
            if let Ok(num) = rest.trim().parse::<i32>() {
                let n = num.clamp(1, 100);
                return Some(SysParsedResult {
                    cmd_type: SysCommandType::Volume,
                    op_type: SysOpType::Increase,
                    delta_or_target: n,
                    title: format!("Increase Volume by {}%", n),
                    subtitle: format!("Raise current master volume by +{}%", n),
                });
            }
        }
    }

    // Shorthand: "vol -2", "volume -5", "vol - 2", "volume-2"
    let minus_prefixes = ["vol -", "volume -", "vol-", "volume-"];
    for prefix in minus_prefixes {
        if let Some(rest) = s.strip_prefix(prefix) {
            if let Ok(num) = rest.trim().parse::<i32>() {
                let n = num.clamp(1, 100);
                return Some(SysParsedResult {
                    cmd_type: SysCommandType::Volume,
                    op_type: SysOpType::Decrease,
                    delta_or_target: n,
                    title: format!("Decrease Volume by {}%", n),
                    subtitle: format!("Lower current master volume by -{}%", n),
                });
            }
        }
    }

    // Direct value: "vol 50", "volume 80"
    let direct_prefixes = ["vol ", "volume "];
    for prefix in direct_prefixes {
        if let Some(rest) = s.strip_prefix(prefix) {
            let t = rest.trim();
            if let Ok(num) = t.parse::<i32>() {
                let n = num.clamp(0, 100);
                return Some(SysParsedResult {
                    cmd_type: SysCommandType::Volume,
                    op_type: SysOpType::SetAbsolute,
                    delta_or_target: n,
                    title: format!("Set Volume to {}%", n),
                    subtitle: format!("Adjust master volume to {}%", n),
                });
            }
        }
    }

    None
}

fn parse_brightness_command(s: &str) -> Option<SysParsedResult> {
    // Status query: "bright", "brightness"
    if s == "bright" || s == "brightness" {
        return Some(SysParsedResult {
            cmd_type: SysCommandType::Brightness,
            op_type: SysOpType::Status,
            delta_or_target: 0,
            title: "Display Brightness".to_string(),
            subtitle: "Current screen brightness level".to_string(),
        });
    }

    // Natural Language: "increase brightness by 10", "increase bright by 5"
    let increase_prefixes = [
        "increase brightness by ", "increase bright by ", "raise brightness by ", "raise bright by ",
        "turn up brightness by ", "turn up bright by ", "boost brightness by ", "boost bright by ",
        "increase brightness ", "increase bright ", "raise brightness ", "raise bright ",
        "turn up brightness ", "turn up bright ",
    ];
    for prefix in increase_prefixes {
        if let Some(rest) = s.strip_prefix(prefix) {
            if let Ok(num) = rest.trim().parse::<i32>() {
                let n = num.clamp(1, 100);
                return Some(SysParsedResult {
                    cmd_type: SysCommandType::Brightness,
                    op_type: SysOpType::Increase,
                    delta_or_target: n,
                    title: format!("Increase Brightness by {}%", n),
                    subtitle: format!("Brighten display by +{}%", n),
                });
            }
        }
    }

    // Natural Language: "decrease brightness by 10", "lower brightness by 5", "reduce brightness by 2"
    let decrease_prefixes = [
        "decrease brightness by ", "decrease bright by ", "lower brightness by ", "lower bright by ",
        "reduce brightness by ", "reduce bright by ", "turn down brightness by ", "turn down bright by ",
        "dim brightness by ", "dim bright by ",
        "decrease brightness ", "decrease bright ", "lower brightness ", "lower bright ",
        "reduce brightness ", "reduce bright ", "turn down brightness ", "turn down bright ",
        "dim brightness ", "dim bright ",
    ];
    for prefix in decrease_prefixes {
        if let Some(rest) = s.strip_prefix(prefix) {
            if let Ok(num) = rest.trim().parse::<i32>() {
                let n = num.clamp(1, 100);
                return Some(SysParsedResult {
                    cmd_type: SysCommandType::Brightness,
                    op_type: SysOpType::Decrease,
                    delta_or_target: n,
                    title: format!("Decrease Brightness by {}%", n),
                    subtitle: format!("Dim display by -{}%", n),
                });
            }
        }
    }

    // "set brightness to 70", "set bright to 50"
    let set_prefixes = [
        "set brightness to ", "set bright to ", "set brightness ", "set bright ",
    ];
    for prefix in set_prefixes {
        if let Some(rest) = s.strip_prefix(prefix) {
            if let Ok(num) = rest.trim().parse::<i32>() {
                let n = num.clamp(0, 100);
                return Some(SysParsedResult {
                    cmd_type: SysCommandType::Brightness,
                    op_type: SysOpType::SetAbsolute,
                    delta_or_target: n,
                    title: format!("Set Brightness to {}%", n),
                    subtitle: format!("Adjust screen brightness to {}%", n),
                });
            }
        }
    }

    // Shorthand: "bright +10", "brightness +5", "bright+10", "brightness+5"
    let plus_prefixes = ["bright +", "brightness +", "bright+", "brightness+"];
    for prefix in plus_prefixes {
        if let Some(rest) = s.strip_prefix(prefix) {
            if let Ok(num) = rest.trim().parse::<i32>() {
                let n = num.clamp(1, 100);
                return Some(SysParsedResult {
                    cmd_type: SysCommandType::Brightness,
                    op_type: SysOpType::Increase,
                    delta_or_target: n,
                    title: format!("Increase Brightness by {}%", n),
                    subtitle: format!("Brighten display by +{}%", n),
                });
            }
        }
    }

    // Shorthand: "bright -10", "brightness -5", "bright-10", "brightness-5"
    let minus_prefixes = ["bright -", "brightness -", "bright-", "brightness-"];
    for prefix in minus_prefixes {
        if let Some(rest) = s.strip_prefix(prefix) {
            if let Ok(num) = rest.trim().parse::<i32>() {
                let n = num.clamp(1, 100);
                return Some(SysParsedResult {
                    cmd_type: SysCommandType::Brightness,
                    op_type: SysOpType::Decrease,
                    delta_or_target: n,
                    title: format!("Decrease Brightness by {}%", n),
                    subtitle: format!("Dim display by -{}%", n),
                });
            }
        }
    }

    // Direct: "bright 70", "brightness 50"
    let direct_prefixes = ["bright ", "brightness "];
    for prefix in direct_prefixes {
        if let Some(rest) = s.strip_prefix(prefix) {
            if let Ok(num) = rest.trim().parse::<i32>() {
                let n = num.clamp(0, 100);
                return Some(SysParsedResult {
                    cmd_type: SysCommandType::Brightness,
                    op_type: SysOpType::SetAbsolute,
                    delta_or_target: n,
                    title: format!("Set Brightness to {}%", n),
                    subtitle: format!("Adjust screen brightness to {}%", n),
                });
            }
        }
    }

    None
}

#[no_mangle]
pub unsafe extern "C" fn flow_sys_parse_command(
    query_ptr: *const u16,
    query_len: usize,
    out_cmd: *mut NativeSysParsedCommand,
) -> i32 {
    if query_ptr.is_null() || query_len == 0 || out_cmd.is_null() {
        return 0;
    }

    let slice = unsafe { std::slice::from_raw_parts(query_ptr, query_len) };
    let query_str = match String::from_utf16(slice) {
        Ok(s) => s,
        Err(_) => return 0,
    };

    if let Some(parsed) = parse_sys_query(&query_str) {
        let dest = unsafe { &mut *out_cmd };
        dest.cmd_type = parsed.cmd_type as u32;
        dest.op_type = parsed.op_type as u32;
        dest.delta_or_target = parsed.delta_or_target;
        write_utf16(&mut dest.title, &mut dest.title_len, &parsed.title);
        write_utf16(&mut dest.subtitle, &mut dest.subtitle_len, &parsed.subtitle);
        1
    } else {
        0
    }
}

pub const SYS_ACTION_SHUTDOWN: u32 = 1;
pub const SYS_ACTION_RESTART: u32 = 2;
pub const SYS_ACTION_RESTART_ADVANCED: u32 = 3;
pub const SYS_ACTION_LOGOFF: u32 = 4;
pub const SYS_ACTION_LOCK: u32 = 5;
pub const SYS_ACTION_SLEEP: u32 = 6;
pub const SYS_ACTION_HIBERNATE: u32 = 7;
pub const SYS_ACTION_EMPTY_RECYCLE_BIN: u32 = 8;

#[repr(C)]
#[derive(Clone, Copy)]
struct LUID {
    low_part: u32,
    high_part: i32,
}

#[repr(C)]
#[derive(Clone, Copy)]
struct LUID_AND_ATTRIBUTES {
    luid: LUID,
    attributes: u32,
}

#[repr(C)]
struct TOKEN_PRIVILEGES {
    privilege_count: u32,
    privileges: [LUID_AND_ATTRIBUTES; 1],
}

const TOKEN_ADJUST_PRIVILEGES: u32 = 0x0020;
const TOKEN_QUERY: u32 = 0x0008;
const SE_PRIVILEGE_ENABLED: u32 = 0x00000002;

const EWX_LOGOFF: u32 = 0x00000000;
const EWX_SHUTDOWN: u32 = 0x00000001;
const EWX_REBOOT: u32 = 0x00000002;
const EWX_POWEROFF: u32 = 0x00000008;
const EWX_BOOTOPTIONS: u32 = 0x01000000;
const SHTDN_REASON_FLAG_PLANNED: u32 = 0x40000000;
const SHTDN_REASON_MAJOR_OTHER: u32 = 0x00000000;
const SHUTDOWN_REASON: u32 = SHTDN_REASON_MAJOR_OTHER | SHTDN_REASON_FLAG_PLANNED;

#[link(name = "advapi32")]
extern "system" {
    fn OpenProcessToken(ProcessHandle: isize, DesiredAccess: u32, TokenHandle: *mut isize) -> i32;
    fn LookupPrivilegeValueW(lpSystemName: *const u16, lpName: *const u16, lpLuid: *mut LUID) -> i32;
    fn AdjustTokenPrivileges(
        TokenHandle: isize,
        DisableAllPrivileges: i32,
        NewState: *const TOKEN_PRIVILEGES,
        BufferLength: u32,
        PreviousState: *mut TOKEN_PRIVILEGES,
        ReturnLength: *mut u32,
    ) -> i32;
}

#[link(name = "user32")]
extern "system" {
    fn ExitWindowsEx(uFlags: u32, dwReason: u32) -> i32;
    fn LockWorkStation() -> i32;
}

#[link(name = "powrprof")]
extern "system" {
    fn SetSuspendState(bHibernate: u8, bForce: u8, bWakeupEventsDisabled: u8) -> u8;
}

#[link(name = "shell32")]
extern "system" {
    fn SHEmptyRecycleBinW(hwnd: isize, pszRootPath: *const u16, dwFlags: u32) -> i32;
}

#[link(name = "kernel32")]
extern "system" {
    fn GetCurrentProcess() -> isize;
}

unsafe fn enable_shutdown_privilege() -> bool {
    let mut token: isize = 0;
    if OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &mut token) == 0 {
        return false;
    }

    let priv_name: Vec<u16> = "SeShutdownPrivilege\0".encode_utf16().collect();
    let mut luid = LUID { low_part: 0, high_part: 0 };
    if LookupPrivilegeValueW(std::ptr::null(), priv_name.as_ptr(), &mut luid) == 0 {
        return false;
    }

    let tp = TOKEN_PRIVILEGES {
        privilege_count: 1,
        privileges: [LUID_AND_ATTRIBUTES {
            luid,
            attributes: SE_PRIVILEGE_ENABLED,
        }],
    };

    AdjustTokenPrivileges(token, 0, &tp, 0, std::ptr::null_mut(), std::ptr::null_mut()) != 0
}

#[no_mangle]
pub unsafe extern "C" fn flow_sys_execute_command(action_id: u32) -> i32 {
    match action_id {
        SYS_ACTION_SHUTDOWN => {
            let _ = enable_shutdown_privilege();
            if ExitWindowsEx(EWX_SHUTDOWN | EWX_POWEROFF, SHUTDOWN_REASON) == 0 {
                let _ = std::process::Command::new("shutdown").args(["/s", "/t", "0"]).spawn();
            }
            1
        }
        SYS_ACTION_RESTART => {
            let _ = enable_shutdown_privilege();
            if ExitWindowsEx(EWX_REBOOT, SHUTDOWN_REASON) == 0 {
                let _ = std::process::Command::new("shutdown").args(["/r", "/t", "0"]).spawn();
            }
            1
        }
        SYS_ACTION_RESTART_ADVANCED => {
            let _ = enable_shutdown_privilege();
            if ExitWindowsEx(EWX_REBOOT | EWX_BOOTOPTIONS, SHUTDOWN_REASON) == 0 {
                let _ = std::process::Command::new("shutdown").args(["/r", "/o", "/t", "0"]).spawn();
            }
            1
        }
        SYS_ACTION_LOGOFF => {
            ExitWindowsEx(EWX_LOGOFF, SHUTDOWN_REASON)
        }
        SYS_ACTION_LOCK => {
            LockWorkStation()
        }
        SYS_ACTION_SLEEP => {
            SetSuspendState(0, 0, 0) as i32
        }
        SYS_ACTION_HIBERNATE => {
            SetSuspendState(1, 0, 0) as i32
        }
        SYS_ACTION_EMPTY_RECYCLE_BIN => {
            SHEmptyRecycleBinW(0, std::ptr::null(), 0)
        }
        _ => 0,
    }
}


#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_volume_shorthand() {
        let r1 = parse_sys_query("vol").unwrap();
        assert_eq!(r1.cmd_type, SysCommandType::Volume);
        assert_eq!(r1.op_type, SysOpType::Status);

        let r2 = parse_sys_query("vol +2").unwrap();
        assert_eq!(r2.cmd_type, SysCommandType::Volume);
        assert_eq!(r2.op_type, SysOpType::Increase);
        assert_eq!(r2.delta_or_target, 2);

        let r3 = parse_sys_query("volume -5").unwrap();
        assert_eq!(r3.cmd_type, SysCommandType::Volume);
        assert_eq!(r3.op_type, SysOpType::Decrease);
        assert_eq!(r3.delta_or_target, 5);

        let r4 = parse_sys_query("vol 80").unwrap();
        assert_eq!(r4.cmd_type, SysCommandType::Volume);
        assert_eq!(r4.op_type, SysOpType::SetAbsolute);
        assert_eq!(r4.delta_or_target, 80);
    }

    #[test]
    fn test_volume_natural_language() {
        let r1 = parse_sys_query("increase vol by 2").unwrap();
        assert_eq!(r1.cmd_type, SysCommandType::Volume);
        assert_eq!(r1.op_type, SysOpType::Increase);
        assert_eq!(r1.delta_or_target, 2);

        let r2 = parse_sys_query("decrease volume by 10").unwrap();
        assert_eq!(r2.cmd_type, SysCommandType::Volume);
        assert_eq!(r2.op_type, SysOpType::Decrease);
        assert_eq!(r2.delta_or_target, 10);

        let r3 = parse_sys_query("lower volume by 5").unwrap();
        assert_eq!(r3.cmd_type, SysCommandType::Volume);
        assert_eq!(r3.op_type, SysOpType::Decrease);
        assert_eq!(r3.delta_or_target, 5);

        let r4 = parse_sys_query("turn up volume by 15").unwrap();
        assert_eq!(r4.cmd_type, SysCommandType::Volume);
        assert_eq!(r4.op_type, SysOpType::Increase);
        assert_eq!(r4.delta_or_target, 15);

        let r5 = parse_sys_query("mute").unwrap();
        assert_eq!(r5.cmd_type, SysCommandType::Mute);
        assert_eq!(r5.op_type, SysOpType::Mute);

        let r6 = parse_sys_query("unmute").unwrap();
        assert_eq!(r6.cmd_type, SysCommandType::Mute);
        assert_eq!(r6.op_type, SysOpType::Unmute);
    }

    #[test]
    fn test_brightness_commands() {
        let r1 = parse_sys_query("bright +10").unwrap();
        assert_eq!(r1.cmd_type, SysCommandType::Brightness);
        assert_eq!(r1.op_type, SysOpType::Increase);
        assert_eq!(r1.delta_or_target, 10);

        let r2 = parse_sys_query("brightness -5").unwrap();
        assert_eq!(r2.cmd_type, SysCommandType::Brightness);
        assert_eq!(r2.op_type, SysOpType::Decrease);
        assert_eq!(r2.delta_or_target, 5);

        let r3 = parse_sys_query("decrease brightness by 10").unwrap();
        assert_eq!(r3.cmd_type, SysCommandType::Brightness);
        assert_eq!(r3.op_type, SysOpType::Decrease);
        assert_eq!(r3.delta_or_target, 10);

        let r4 = parse_sys_query("increase brightness by 10").unwrap();
        assert_eq!(r4.cmd_type, SysCommandType::Brightness);
        assert_eq!(r4.op_type, SysOpType::Increase);
        assert_eq!(r4.delta_or_target, 10);

        let r5 = parse_sys_query("brightness 70").unwrap();
        assert_eq!(r5.cmd_type, SysCommandType::Brightness);
        assert_eq!(r5.op_type, SysOpType::SetAbsolute);
        assert_eq!(r5.delta_or_target, 70);
    }
}
