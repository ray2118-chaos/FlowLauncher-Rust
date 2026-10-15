# Flow Launcher (Rust Edition) ⚡🦀

<p align="center">
  <strong>Next-Generation Windows Productivity Launcher & Command Hub</strong><br>
  <em>Powered by .NET 9 WPF with a High-Performance Native Rust Core</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?style=flat-square&logo=windows&logoColor=white" alt="Platform: Windows 10/11" />
  <img src="https://img.shields.io/badge/.NET-9.0%20WPF-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 9.0" />
  <img src="https://img.shields.io/badge/Rust-2021%20Edition-DEA584?style=flat-square&logo=rust&logoColor=black" alt="Rust 2021" />
  <img src="https://img.shields.io/badge/License-MIT-green.svg?style=flat-square" alt="License: MIT" />
  <img src="https://img.shields.io/badge/Architecture-x64-blue.svg?style=flat-square" alt="Architecture: x64" />
</p>

---

## 🌟 Overview

**Flow Launcher-Rust** is an ultra-fast, modern productivity launcher for Windows. Designed for power users, developers, and speed enthusiasts, it supercharges the traditional launcher workflow by combining a fluid **Neumorphic / Fluent WPF presentation layer** with a **bare-metal native Rust acceleration library (`Flow.Launcher.Native`)**.

From natural language timers and interactive notes to instant process termination, SIMD fuzzy matching, and real-time thesaurus lookups, every component is built for instantaneous responsiveness, low memory overhead, and aesthetic polish.

---

## 🚀 Key Highlights & New Capabilities

### 🦀 1. Native Rust Engine (`Flow.Launcher.Native`)
Heavy computations, I/O scans, and memory-intensive operations are offloaded from the .NET Garbage Collector to a compiled C-ABI Rust library:
- **Instant Process Snapshots**: Uses Win32 `Toolhelp32Snapshot` + `EnumWindows` directly in native memory in $<0.4\text{ms}$ (avoiding .NET `Process.GetProcesses()` heap spikes).
- **Zero-Allocation `.lnk` Parser**: Binary MS-SHLLINK parser extracting targets, arguments, and metadata without COM overhead.
- **PE Header Inspector**: Extracts embedded icon resources, subsystem flags, and descriptions directly from Win32 executables.
- **High-Performance Chromium Bookmark Parser**: Recursively extracts thousands of browser bookmarks in under $3\text{ms}$.
- **Hardware-Accelerated Hashing**: Integrated `xxHash3-64` for thumbnail validation and fast caching.
- **Working Set Trimmer**: Native API to purge inactive memory pages to keep background RAM usage minimal.

---

### ⏱️ 2. Natural Language Timers, Focus Sprints & Hydration Tracker
Integrated into the **Reminders & Timers** plugin with full natural language parsing:
- **Timers by Duration**:
  - `timer 25min` or `timer 25 min`
  - `timer 10s` / `timer 1h 30m` / `timer 45s`
- **Conversational Phrases**:
  - `set 5 min timer`
  - `set timer for 5 min`
  - `set 25m timer focus sprint`
  - `set timer for 1 hour lunch`
- **Scheduled Alarms**:
  - `at 5:30pm standup meeting`
  - `remind me at 4pm call mom`
  - `remind me in 15m to stretch`
- **Daily Hydration Tracker**:
  - Type `water` to view today's water progress (`ml / 2000 ml`).
  - Type `water 250` or `water 500` to instantly log a glass or bottle of water.

---

### 📝 3. Sticky Notes & Scratchpad
A lightweight, lightning-fast scratchpad plugin (`note`) designed for rapid capture without opening heavy text editors:
- **Capture Ideas Instantly**: Type `note buy milk` or `note Meeting link: https://...` and hit <kbd>Enter</kbd> to save.
- **Copy to Clipboard**: Hit <kbd>Enter</kbd> on any saved note to copy it immediately.
- **Intuitive Deletion**:
  - Type `note del` or `note delete` to view all saved notes with a direct **Press Enter to delete** action.
  - Type `note del 1` (or any index) to immediately remove that specific note.
  - Right-click any note $\rightarrow$ click **Delete Note**.
  - Type `note clear` or `note del all` to remove all notes.
- **Desktop Export**: Context menu option to export all notes to `flow_notes.txt` on your Desktop.

---

### 📖 4. Dictionary, Synonyms & Antonyms
Instant vocabulary lookup with online and offline resilience:
- **Word Definitions**: Type `define serendipity` or `dict ephemeral` to view phonetic pronunciation, part of speech, and comprehensive definitions.
- **Live Synonyms Finder**: Type `syn happy` or `syn clever` to get rich synonyms powered by the **Datamuse API** with one-click copying.
- **Live Antonyms Finder**: Type `ant dark` or `ant brave` to instantly retrieve opposing words.
- **Thesaurus Fallback**: Instant fallback to Thesaurus.com when an esoteric word is queried.
- **Global Search Support**: Type `syn happy` or `ant bright` anywhere in the launcher without prefix switching.

---

### ⚡ 5. Process Killer
High-speed task termination with dedicated squircle branding:
- **Query & Filter**: Type `kill chrome`, `kill node`, or `kill discord` to filter running processes by executable name or active window title.
- **Prioritize Active Windows**: Processes with visible windows appear first for quick accessibility.
- **Batch Kill Instances**: When multiple instances of the same program run (e.g., Chrome or Electron), a top option allows killing all instances at once.
- **Native Termination**: Directly invokes Win32 `TerminateProcess` via Rust without spawning external command prompts.

---

### 🪟 6. Window Snapper
Keyboard-driven window snapping and layout control:
- Type `snap left` $\rightarrow$ Snaps the active foreground window to the left half of the display.
- Type `snap right` $\rightarrow$ Snaps the active window to the right half.
- Type `snap top` / `snap bottom` $\rightarrow$ Snaps to top/bottom half.
- Type `snap max` / `snap min` / `snap center` $\rightarrow$ Maximizes, minimizes, or centers on the current monitor.

---

### 📋 7. Fuzzy Clipboard History
Built-in SQLite-backed clipboard manager:
- Type `cb` followed by any keyword to search recent clipboard clips.
- Previews full text, timestamp, and character count.
- Press <kbd>Enter</kbd> to instantly re-copy to clipboard.
- Context menu option to clear history or delete individual clips.

---

### 🎨 8. Neumorphic & Fluent Glass HUD
- **Dynamic Accent Extraction**: The launcher automatically parses the active XAML theme (Neumorphic, Dark, Light, OLED, Custom) and extracts true theme background, border, and accent colors directly into the HUD overlay capsule.
- **Frosted Glass (Acrylic / Mica)**: Modern Windows 11/10 composition with real-time blur behind the window.
- **Authentic Main Menu Brand Icons**: Web search action keywords (`facebook`, `twitter`, `maps`, `translate`, `findicon`, `github`) automatically resolve and display their authentic high-resolution brand icons instead of generic globe icons.

---

## 🛠️ Architecture & Project Structure

```
Flow_NotDotRun/
├── Flow.Launcher/                   # WPF UI Shell, MainWindow, Themes, Settings UI
├── Flow.Launcher.Core/              # Plugin lifecycle, query routing, settings manager
├── Flow.Launcher.Infrastructure/    # Image loader, hotkey bindings, FFI bridge
├── Flow.Launcher.Native/            # 🦀 Rust cdylib core library (C-ABI FFI)
│   ├── src/
│   │   ├── bookmarks.rs             # Chromium JSON bookmark parser
│   │   ├── calc.rs                  # Math expression engine
│   │   ├── clipboard.rs             # Clipboard storage helpers
│   │   ├── fs_scanner.rs            # Fast directory and file scanner
│   │   ├── fuzzy.rs                 # SIMD fuzzy string scoring
│   │   ├── lnk.rs                   # Windows Shell Link (.lnk) binary parser
│   │   ├── pe.rs                    # Portable Executable (.exe/.dll) resource reader
│   │   ├── process.rs               # Win32 Toolhelp32 process snapshot & kill
│   │   ├── registry.rs              # Registry App Paths scanner
│   │   └── sys.rs                   # System power & memory trim APIs
│   └── Cargo.toml                   # Rust dependencies & crate configuration
├── Flow.Launcher.Plugin/            # Common plugin interfaces & models (IPlugin)
├── Flow.Launcher.Test/              # Unit & integration test suite
└── Plugins/                         # Built-in Plugins
    ├── Flow.Launcher.Plugin.BrowserBookmark/
    ├── Flow.Launcher.Plugin.Calculator/
    ├── Flow.Launcher.Plugin.Clipboard/
    ├── Flow.Launcher.Plugin.Dictionary/
    ├── Flow.Launcher.Plugin.Emoji/
    ├── Flow.Launcher.Plugin.Explorer/
    ├── Flow.Launcher.Plugin.Note/
    ├── Flow.Launcher.Plugin.PluginIndicator/
    ├── Flow.Launcher.Plugin.PluginsManager/
    ├── Flow.Launcher.Plugin.ProcessKiller/
    ├── Flow.Launcher.Plugin.Program/
    ├── Flow.Launcher.Plugin.Remind/
    ├── Flow.Launcher.Plugin.Shell/
    ├── Flow.Launcher.Plugin.Snap/
    ├── Flow.Launcher.Plugin.Sys/
    ├── Flow.Launcher.Plugin.Url/
    ├── Flow.Launcher.Plugin.WebSearch/
    └── Flow.Launcher.Plugin.WindowsSettings/
```

---

## 💻 Building from Source

### Prerequisites
- **Operating System**: Windows 10 (Build 19041+) or Windows 11
- **.NET 9.0 SDK**: [Download .NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- **Rust Toolchain**: `cargo` & `rustc` (1.75+) installed via [rustup.rs](https://rustup.rs/)
- **Build Tools**: Visual Studio 2022 (v17.12+) or JetBrains Rider / VS Code

### Build Steps

1. **Clone the Repository**:
   ```cmd
   git clone https://github.com/ray2118-chaos/FlowLauncher-Rust.git
   cd FlowLauncher-Rust
   ```

2. **Build the Native Rust Library**:
   ```cmd
   cd Flow.Launcher.Native
   cargo build --release
   cd ..
   ```
   *(Alternatively, run `Flow.Launcher.Native\build.cmd` to build and auto-copy `flow_launcher_native.dll`)*

3. **Build the .NET Solution**:
   ```cmd
   dotnet build Flow.Launcher.sln -c Release
   ```

4. **Launch Flow Launcher**:
   The compiled executable will be located at:
   ```cmd
   Output\Release\Flow.Launcher.exe
   ```

---

## ⌨️ Common Default Hotkeys

| Action | Shortcut |
| :--- | :--- |
| **Open / Hide Flow Launcher** | <kbd>Alt</kbd> + <kbd>Space</kbd> |
| **Open Settings Window** | Type `settings` or right-click tray icon |
| **Open Context Menu** | <kbd>Shift</kbd> + <kbd>F10</kbd> or <kbd>→</kbd> (Right Arrow) |
| **Copy Result Path** | <kbd>Ctrl</kbd> + <kbd>C</kbd> |
| **Run as Administrator** | <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Enter</kbd> |
| **Toggle Result Preview** | <kbd>F1</kbd> |

---

## 📄 License & Credits

- This project is released under the **[MIT License](LICENSE)**.
- Built upon and inspired by the open-source [Flow-Launcher](https://github.com/Flow-Launcher/Flow.Launcher) project with custom Rust acceleration, new native plugins, and enhanced UI components.
