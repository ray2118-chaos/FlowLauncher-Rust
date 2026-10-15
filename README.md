# Flow Launcher (Rust Edition) ⚡🦀

A fast, lightweight fork of [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) powered by a high-performance **Rust** native backend, while maintaining 100% compatibility with Flow Launcher's C# and community plugin ecosystem.

---

## 💡 Why This Fork?

Flow Launcher is an incredible productivity tool, but heavy background operations in pure managed C# can cause memory spikes and GC pauses. This fork offloads performance-critical hot paths to a native compiled Rust core (`Flow.Launcher.Native`):

- **⚡ Native Rust Engine**: Heavy computations (SIMD fuzzy matching, zero-allocation `.lnk` parsing, binary PE inspection, Chromium bookmark parsing, process enumeration, xxHash3 hashing, and memory trimming) run directly in compiled native Rust.
- **🔌 Full Plugin Compatibility**: Keeps the familiar WPF user interface and full support for all existing C# and community plugins.
- **🪟 Smart Window Activation**: When triggering an app shortcut, Flow brings the already-open window to the front instead of opening duplicate instances.
- **⌨️ Double-Tap Hotkeys**: Supports assigning repeated modifier keys (like pressing <kbd>Ctrl</kbd> twice, <kbd>Alt</kbd> twice, or <kbd>Shift</kbd> twice) as global shortcuts.
- **🔢 Dynamic Scroll Hotkeys**: <kbd>Alt</kbd> + <kbd>1..5</kbd> dynamically re-anchors to whichever 5 results are currently visible on screen while scrolling.
- **✨ Built-in Modern Plugins**:
  - `timer` & `remind`: Natural language timers, alarms, and daily hydration tracker.
  - `def <word>`: Instant dictionary definitions, synonyms (`syn`), and antonyms (`ant`).
  - `note`: Fast sticky note scratchpad (`note buy milk`, `note del`).
  - `kill <process>`: Native instant process terminator with window priority.
  - `snap <left|right|max|center>`: Keyboard window snapper.

---

## 📥 Download & Install

Download the latest release from the [Releases](https://github.com/ray2118-chaos/FlowLauncher-Rust/releases) page:

- **Installer**: `Flow-Launcher-Setup.exe`
- **Portable**: `Flow-Launcher-Portable.zip` (no installation required)

---

## 🛠️ Building from Source

### Prerequisites
- Windows 10 (19041+) or Windows 11
- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- [Rust Toolchain](https://rustup.rs/) (`cargo` & `rustc`)

### Steps

```powershell
# 1. Clone the repository
git clone https://github.com/ray2118-chaos/FlowLauncher-Rust.git
cd FlowLauncher-Rust

# 2. Build the Rust native library
cargo build --release --manifest-path Flow.Launcher.Native/Cargo.toml
Copy-Item Flow.Launcher.Native\target\release\flow_launcher_native.dll Flow.Launcher.Infrastructure\flow_launcher_native.dll -Force

# 3. Build the solution
dotnet build Flow.Launcher.sln -c Release
```

The compiled application will be in `Output\Release\Flow.Launcher.exe`.

To package the standalone installer and portable `.zip`:
```powershell
.\Scripts\post_build.ps1 -config Release
```

---

## 📄 License & Credits

- Released under the [MIT License](LICENSE).
- Based on the open-source [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) project.
