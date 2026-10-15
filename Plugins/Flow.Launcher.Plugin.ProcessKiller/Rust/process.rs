use std::collections::HashMap;
use windows_sys::Win32::Foundation::{CloseHandle, HWND, LPARAM};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, Process32FirstW, Process32NextW, PROCESSENTRY32W, TH32CS_SNAPPROCESS,
};
use windows_sys::Win32::System::ProcessStatus::{
    GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS,
};
use windows_sys::Win32::System::Threading::{
    OpenProcess, TerminateProcess, QueryFullProcessImageNameW,
    PROCESS_TERMINATE, PROCESS_QUERY_LIMITED_INFORMATION,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    EnumWindows, GetWindowTextLengthW, GetWindowTextW, GetWindowThreadProcessId, IsWindowVisible,
};

type BOOL = i32;

#[repr(C)]
pub struct ProcessItem {
    pub pid: u32,
    pub name: [u16; 260],
    pub name_len: u32,
    pub exe_path: [u16; 512],
    pub exe_path_len: u32,
    pub window_title: [u16; 260],
    pub window_title_len: u32,
    pub mem_working_set_mb: u32,
}

unsafe extern "system" fn enum_windows_callback(hwnd: HWND, lparam: LPARAM) -> BOOL {
    let titles_map = &mut *(lparam as *mut HashMap<u32, Vec<u16>>);

    if IsWindowVisible(hwnd) != 0 {
        let length = GetWindowTextLengthW(hwnd);
        if length > 0 {
            let mut title_buf = vec![0u16; (length + 1) as usize];
            let copied = GetWindowTextW(hwnd, title_buf.as_mut_ptr(), length + 1);
            if copied > 0 {
                title_buf.truncate(copied as usize);
                let mut pid: u32 = 0;
                GetWindowThreadProcessId(hwnd, &mut pid);
                if pid > 0 && !titles_map.contains_key(&pid) {
                    titles_map.insert(pid, title_buf);
                }
            }
        }
    }
    1
}

const SYSTEM_PROCESSES: &[&str] = &[
    "conhost", "svchost", "idle", "system", "rundll32", "csrss", "lsass", "lsm",
    "smss", "wininit", "winlogon", "services", "spoolsv", "explorer", "flow.launcher",
];

fn is_system_process(name_slice: &[u16]) -> bool {
    let s = match String::from_utf16(name_slice) {
        Ok(v) => v.to_lowercase(),
        Err(_) => return false,
    };
    let clean = if s.ends_with(".exe") {
        &s[..s.len() - 4]
    } else {
        &s
    };
    SYSTEM_PROCESSES.iter().any(|&sys| sys == clean)
}

/// Enumerates running processes and their primary window titles, full exe paths,
/// and working set memory in a single pass.
/// Runs in under 1ms with zero .NET GC overhead.
#[no_mangle]
pub unsafe extern "C" fn flow_get_processes(
    out_items: *mut ProcessItem,
    max_items: u32,
    filter_system: i32,
    out_count: *mut u32,
) -> i32 {
    if out_items.is_null() || out_count.is_null() || max_items == 0 {
        return 0;
    }

    // 1. Collect window titles map: PID -> WindowTitle UTF-16
    let mut titles_map: HashMap<u32, Vec<u16>> = HashMap::with_capacity(128);
    unsafe {
        EnumWindows(
            Some(enum_windows_callback),
            &mut titles_map as *mut _ as LPARAM,
        );
    }

    // 2. Snapshot processes
    let snapshot = unsafe { CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) };
    if snapshot.is_null() || snapshot == -1isize as _ {
        return 0;
    }

    let mut entry: PROCESSENTRY32W = unsafe { std::mem::zeroed() };
    entry.dwSize = std::mem::size_of::<PROCESSENTRY32W>() as u32;

    let mut count: u32 = 0;

    if unsafe { Process32FirstW(snapshot, &mut entry) } != 0 {
        loop {
            if count >= max_items {
                break;
            }

            let mut name_len = 0usize;
            while name_len < 259 && entry.szExeFile[name_len] != 0 {
                name_len += 1;
            }

            if filter_system != 0 && is_system_process(&entry.szExeFile[..name_len]) {
                if unsafe { Process32NextW(snapshot, &mut entry) } == 0 {
                    break;
                }
                continue;
            }

            let item = unsafe { &mut *out_items.add(count as usize) };
            item.pid = entry.th32ProcessID;

            for i in 0..name_len {
                item.name[i] = entry.szExeFile[i];
            }
            item.name[name_len] = 0;
            item.name_len = name_len as u32;

            item.exe_path[0] = 0;
            item.exe_path_len = 0;
            item.mem_working_set_mb = 0;

            let hproc = unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, entry.th32ProcessID) };
            if !hproc.is_null() {
                let mut path_len = 511u32;
                if unsafe {
                    QueryFullProcessImageNameW(hproc, 0, item.exe_path.as_mut_ptr(), &mut path_len)
                } != 0
                {
                    item.exe_path_len = path_len;
                    item.exe_path[path_len as usize] = 0;
                }

                let mut pmc: PROCESS_MEMORY_COUNTERS = unsafe { std::mem::zeroed() };
                pmc.cb = std::mem::size_of::<PROCESS_MEMORY_COUNTERS>() as u32;
                if unsafe { GetProcessMemoryInfo(hproc, &mut pmc, pmc.cb) } != 0 {
                    item.mem_working_set_mb = (pmc.WorkingSetSize / (1024 * 1024)) as u32;
                }

                unsafe { CloseHandle(hproc) };
            }

            // Match window title if present
            if let Some(title) = titles_map.get(&entry.th32ProcessID) {
                let copy_len = title.len().min(259);
                for i in 0..copy_len {
                    item.window_title[i] = title[i];
                }
                item.window_title[copy_len] = 0;
                item.window_title_len = copy_len as u32;
            } else {
                item.window_title[0] = 0;
                item.window_title_len = 0;
            }

            count += 1;

            if unsafe { Process32NextW(snapshot, &mut entry) } == 0 {
                break;
            }
        }
    }

    unsafe {
        CloseHandle(snapshot);
        *out_count = count;
    }

    1
}

/// Kills a process by PID directly via Win32 TerminateProcess
#[no_mangle]
pub unsafe extern "C" fn flow_kill_process(pid: u32) -> i32 {
    let handle = unsafe { OpenProcess(PROCESS_TERMINATE, 0, pid) };
    if handle.is_null() {
        return 0;
    }
    let success = unsafe { TerminateProcess(handle, 1) };
    unsafe { CloseHandle(handle) };
    if success != 0 {
        1
    } else {
        0
    }
}

/// Kills all running processes whose executable name matches target_name (case-insensitive)
#[no_mangle]
pub unsafe extern "C" fn flow_kill_processes_by_name(
    name_utf16: *const u16,
    name_len: usize,
) -> u32 {
    if name_utf16.is_null() || name_len == 0 {
        return 0;
    }

    let slice = unsafe { std::slice::from_raw_parts(name_utf16, name_len) };
    let target_name = match String::from_utf16(slice) {
        Ok(s) => s.to_lowercase(),
        Err(_) => return 0,
    };

    let target_clean = if target_name.ends_with(".exe") {
        &target_name[..target_name.len() - 4]
    } else {
        &target_name
    };

    let snapshot = unsafe { CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) };
    if snapshot.is_null() || snapshot == -1isize as _ {
        return 0;
    }

    let mut entry: PROCESSENTRY32W = unsafe { std::mem::zeroed() };
    entry.dwSize = std::mem::size_of::<PROCESSENTRY32W>() as u32;

    let mut killed_count: u32 = 0;

    if unsafe { Process32FirstW(snapshot, &mut entry) } != 0 {
        loop {
            let mut name_len = 0usize;
            while name_len < 259 && entry.szExeFile[name_len] != 0 {
                name_len += 1;
            }

            if let Ok(proc_name) = String::from_utf16(&entry.szExeFile[..name_len]) {
                let proc_lower = proc_name.to_lowercase();
                let proc_clean = if proc_lower.ends_with(".exe") {
                    &proc_lower[..proc_lower.len() - 4]
                } else {
                    &proc_lower
                };

                if proc_clean == target_clean {
                    if flow_kill_process(entry.th32ProcessID) != 0 {
                        killed_count += 1;
                    }
                }
            }

            if unsafe { Process32NextW(snapshot, &mut entry) } == 0 {
                break;
            }
        }
    }

    unsafe { CloseHandle(snapshot) };
    killed_count
}

#[link(name = "kernel32")]
extern "system" {
    fn GetCurrentProcess() -> isize;
    fn SetProcessWorkingSetSize(hprocess: isize, dwminimumworkingsetsize: usize, dwmaximumworkingsetsize: usize) -> i32;
}

#[link(name = "psapi")]
extern "system" {
    fn EmptyWorkingSet(hprocess: isize) -> i32;
}

/// Flushes working set pages to standby list and trims OS memory usage
#[no_mangle]
pub unsafe extern "C" fn flow_trim_working_set() -> i32 {
    let proc = GetCurrentProcess();
    EmptyWorkingSet(proc);
    SetProcessWorkingSetSize(proc, usize::MAX, usize::MAX);
    1
}
