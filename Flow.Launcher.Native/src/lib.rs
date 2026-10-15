pub mod animation;
pub mod bookmarks;
pub mod calc;
pub mod clipboard;
pub mod fs_scanner;
pub mod fuzzy;
pub mod gui;
pub mod hash;
pub mod lnk;
pub mod pe;
pub mod process;
pub mod registry;
pub mod sys;
pub mod telemetry;

#[no_mangle]
pub extern "C" fn flow_native_version() -> u32 {
    100 // Version 1.0.0
}
