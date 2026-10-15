// Core Flow Launcher Native Subsystems (Launcher Platform Infrastructure)
pub mod animation;
pub mod directwrite;
pub mod fs_watcher;
pub mod fuzzy;
pub mod gui;
pub mod hash;
pub mod lz4;
pub mod telemetry;

// Plugin-Specific Native Subsystems (Linked directly from their respective plugin directories)
#[path = "../../Plugins/Flow.Launcher.Plugin.Calculator/Rust/calc.rs"]
pub mod calc;

#[path = "../../Plugins/Flow.Launcher.Plugin.BrowserBookmark/Rust/bookmarks.rs"]
pub mod bookmarks;

#[path = "../../Plugins/Flow.Launcher.Plugin.ProcessKiller/Rust/process.rs"]
pub mod process;

#[path = "../../Plugins/Flow.Launcher.Plugin.WindowsSettings/Rust/windows_settings.rs"]
pub mod windows_settings;

#[path = "../../Plugins/Flow.Launcher.Plugin.Sys/Rust/sys.rs"]
pub mod sys;

#[path = "../../Plugins/Flow.Launcher.Plugin.Clipboard/Rust/clipboard.rs"]
pub mod clipboard;

#[path = "../../Plugins/Flow.Launcher.Plugin.Program/Rust/program_indexer.rs"]
pub mod program_indexer;

#[path = "../../Plugins/Flow.Launcher.Plugin.Program/Rust/fs_scanner.rs"]
pub mod fs_scanner;

#[path = "../../Plugins/Flow.Launcher.Plugin.Program/Rust/lnk.rs"]
pub mod lnk;

#[path = "../../Plugins/Flow.Launcher.Plugin.Program/Rust/pe.rs"]
pub mod pe;

#[path = "../../Plugins/Flow.Launcher.Plugin.Program/Rust/registry.rs"]
pub mod registry;

#[path = "../../Plugins/Flow.Launcher.Plugin.Program/Rust/uwp.rs"]
pub mod uwp;

#[path = "../../Plugins/Flow.Launcher.Plugin.Emoji/Rust/emoji.rs"]
pub mod emoji;

#[path = "../../Plugins/Flow.Launcher.Plugin.Url/Rust/url.rs"]
pub mod url;

#[path = "../../Plugins/Flow.Launcher.Plugin.Shell/Rust/shell.rs"]
pub mod shell;

#[path = "../../Plugins/Flow.Launcher.Plugin.Explorer/Rust/everything.rs"]
pub mod everything;

#[path = "../../Plugins/Flow.Launcher.Plugin.Explorer/Rust/preview.rs"]
pub mod preview;

#[path = "../../Flow.Launcher.Core/Rust/jsonrpc.rs"]
pub mod jsonrpc;

#[path = "../../Flow.Launcher.Core/Rust/query_parser.rs"]
pub mod query_parser;

#[path = "../../Flow.Launcher.Core/Rust/history.rs"]
pub mod history;

#[no_mangle]
pub extern "C" fn flow_native_version() -> u32 {
    100 // Version 1.0.0
}
