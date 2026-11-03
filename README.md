# Zed Unity Suite

<p align="center">
  <strong>English</strong> | <a href="README.zh-CN.md">简体中文</a>
</p>

<p align="center">
  <a href="LICENSE.md"><img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License: MIT"></a>
  <a href="https://unity.com/"><img src="https://img.shields.io/badge/Unity-2021.3%2B-black.svg?logo=unity" alt="Unity: 2021.3+"></a>
  <a href="https://zed.dev/"><img src="https://img.shields.io/badge/Zed-Latest-blueviolet.svg?logo=zed" alt="Zed: Latest"></a>
  <a href="https://github.com/821869798/zed-unity/releases"><img src="https://img.shields.io/github/v/release/821869798/zed-unity?color=green" alt="Release"></a>
</p>

A modern, production-ready dual-component integration suite (monorepo) designed for smooth development and debugging of **Unity C#** projects inside **[Zed Editor](https://zed.dev/)**.

The suite consists of two complementary subprojects:
1. **`unity-zed-ide`** (`com.unifan.ide.zed`): A Unity Editor UPM package based on Unity's official **`com.unity.ide.visualstudio@2.0.29`** architecture, completely refactored and tailored for Zed's Roslyn LSP and workspace mechanics.
2. **`zed-unity-debug`** (`unity-debugger`): A native Zed DAP debugging extension built in Rust / WebAssembly, connecting directly to Visual Studio Tools for Unity DAP services.

---

## Why Zed Unity Suite?

| Pain Point in Traditional Setup | How Zed Unity Suite Solves It |
| :--- | :--- |
| **Broken Cross-file Navigation**<br>Legacy `.csproj` and conflicting `.slnx` files cause Roslyn LSP to only resolve symbols within the active file. | **Clean SDK-style `.csproj` & Single `.sln`**<br>Generates lightweight, modern SDK-style `.csproj` files, guarantees a single authoritative `.sln`, and cleans conflicting `.slnx` so Roslyn mounts all 50+ assemblies cleanly. |
| **Cold-start Broken Workspaces**<br>Double-clicking a script from Unity when Zed is closed causes Zed CLI to mount the file as an isolated single-file root (`IsMiscellaneousFile: true`). | **Two-phase Launch Timing Protection**<br>Detects cold starts, opens the workspace folder first, waits for Zed's initial file tree indexing, and then navigates to target line/column. |
| **Roslyn Diagnostics Missing**<br>Standard Roslyn in Zed lacks Unity-specific warnings, rules, and source generators. | **Automatic Roslyn Analyzer Injection**<br>Scans Zed extension storage, VS Code directory, and Visual Studio installations to inject `Microsoft.Unity.Analyzers.dll` and `Unity.SourceGenerators.dll`. |
| **Multi-instance Debugging Confusion**<br>Blind `netstat` scanning or hardcoded ports connect to the wrong editor when multiple Unity projects are open. | **Exact PID-based Port Determination**<br>Reads `Library/EditorInstance.json` to get the current project's Unity PID and computes the exact port via `56000 + (PID % 1000)`. |
| **Cumbersome Manual Setup**<br>Users had to manually configure `.zed/settings.json`, exclude massive folders, and manually build DAP servers. | **Zero-Config Automation**<br>Automatically generates `.zed/settings.json` (excluding `Library/`, `Temp/`, etc.), `.zed/debug.json`, and downloads `UnityDebugAdapter` automatically if missing. |

---

## Directory Structure

```text
zed-unity/
├── unity-zed-ide/         # [Subproject 1] Unity Package (UPM)
│   ├── package.json
│   ├── Package.Build.props
│   ├── Package.Build.targets
│   ├── README.md          # English UPM Documentation
│   ├── README.zh-CN.md    # Chinese UPM Documentation
│   └── Editor/
│       ├── ZedInstallation.cs          # Zed discovery, Roslyn analyzer injection, two-phase CLI launch
│       ├── Discovery.cs                # Editor discovery registration
│       ├── VisualStudioEditor.cs       # IExternalCodeEditor implementation
│       ├── VisualStudioIntegration.cs  # Unity UDP message protocol
│       ├── MSBuildProjectIntegration.cs# Unity 6+ MSBuild pipeline support
│       ├── ProjectGeneration/          # SDK-style & Legacy csproj / sln generators
│       ├── Messaging/                  # VSTU debugging protocol
│       ├── Testing/                    # Test Framework integration
│       └── com.unifan.ide.zed.asmdef
│
├── zed-unity-debug/       # [Subproject 2] Zed Debugger Extension (Rust / WASM)
│   ├── Cargo.toml
│   ├── extension.toml
│   ├── README.md          # English Extension Documentation
│   ├── README.zh-CN.md    # Chinese Extension Documentation
│   ├── src/lib.rs                      # PID port resolution, adapter locator & auto-downloader
│   └── debug_adapter_schemas/unity.json# DAP schema definitions
│
├── .gitignore
├── LICENSE.md
├── README.md              # English Monorepo Documentation
└── README.zh-CN.md        # Chinese Monorepo Documentation
```

---

## Quick Start

### 1. Install Unity Package (`unity-zed-ide`)

Choose one of the following methods in your Unity project:

#### Option A: Via Unity Package Manager Git URL (Recommended)
1. In Unity, open **Window > Package Manager**.
2. Click the **`+`** button in the top-left and select **Add package from git URL...**.
3. Enter:
   ```text
   https://github.com/821869798/zed-unity.git?path=/unity-zed-ide
   ```

#### Option B: Edit `Packages/manifest.json` directly
```json
{
  "dependencies": {
    "com.unifan.ide.zed": "https://github.com/821869798/zed-unity.git?path=/unity-zed-ide"
  }
}
```

#### Option C: Local Path Reference
```json
{
  "dependencies": {
    "com.unifan.ide.zed": "file:D:/path/to/zed-unity/unity-zed-ide"
  }
}
```

### 2. Configure Unity External Script Editor
1. In Unity, go to **Edit > Preferences > External Tools**.
2. Under **External Script Editor**, select **`Zed [Version]`** (automatically detected).
3. Check the project generation options (recommended: **Embedded packages** and **Local packages**).
4. Double-click any C# script in the Project window. Unity will launch Zed, generating `.sln`, SDK-style `.csproj` files, and `.zed` configurations automatically.

### 3. Install Zed Extensions
1. Install the official **`C#`** extension from Zed's Extension Manager (for Roslyn syntax highlighting, diagnostics, and code navigation).
2. Install the **`unity-debugger`** extension:
   - In Zed, press `Ctrl + Shift + P` (`Cmd + Shift + P` on macOS) to open the command palette.
   - Run **`zed: install dev extension`**.
   - Select the `zed-unity-debug` directory. Zed will build/load the extension automatically.

---

## Features in Depth

### 1. Modern Project Generation (`unity-zed-ide`)
* **Modern SDK-Style Projects**: Generates clean SDK-style `.csproj` files with full support for C# 11–13 features, `<Nullable>enable</Nullable>`, and Unity 6+ MSBuild pipelines.
* **Roslyn Analyzers Injection**: Automatically scans local storage for `Microsoft.Unity.Analyzers.dll` and `Unity.SourceGenerators.dll`, injecting them into generated project files to provide real-time Unity diagnostic warnings and performance suggestions.
* **Cold-Start Race Prevention**: On cold starts (when Zed is not running), `ZedInstallation` opens the project root folder first, allows 1.5 seconds for Zed's background thread to scan the initial file tree, and then opens the target script. This prevents Zed from treating the target file as an isolated single-file workspace root.
* **Automated `.zed` Workspace Config**: Generates `.zed/settings.json` (excluding heavy directories like `Library/`, `Temp/`, `Logs/` and setting Unity file associations) and `.zed/debug.json` (with ready-to-run attach configuration).

### 2. Production-Grade Debugging (`zed-unity-debug`)
* **Accurate Port Resolution**: Multi-instance aware. Reads `Library/EditorInstance.json` to extract the exact Unity Editor PID and binds to `56000 + (PID % 1000)`.
* **Auto-Discovery & Fallback Download**: Reuses existing `visualstudiotoolsforunity.vstuc` installations (from VS Code, Cursor, Antigravity, etc.). If none exist, automatically downloads the official release from GitHub into the extension workspace.
* **Full DAP Capabilities**:
  * Breakpoints (`F9`), Continue (`F5`), Step Over (`F10`), Step Into (`F11`), Step Out (`Shift + F11`).
  * **Variables Panel**: Interactive tree of `Locals`, arguments, and `this` object graphs.
  * **Debugger Hover**: Hovering over variables while paused displays real-time runtime values.
  * **Watch Expressions**: Supports adding persistent watch expressions that recalculate on every step.

---

## Daily Debugging Workflow

1. Keep your Unity project running in the Editor.
2. In Zed, press **`F4`** (or open the Debug panel in the bottom-left corner) and select **`Attach to Unity Editor`**.
3. Set breakpoints in C# scripts (`F9` or click next to the line number).
4. In Unity, enter Play mode. When the breakpoint is hit, Zed pauses execution.
5. Inspect variable values directly on mouse hover or expand object graphs in the **Variables** panel.

---

## FAQ & Troubleshooting

#### Q: Why does cross-file navigation take a few seconds when opening Zed on a cold start?
> **A:** When opening a project with 50+ assemblies on cold start, the Roslyn language server initializes in the background. Check the **`roslyn`** indicator in the bottom status bar. Once initialization finishes (~5-7 seconds), all symbol navigation and completions are instant.

#### Q: Why does hovering over a variable show class docs instead of its runtime value?
> **A:** Zed's hover is context-aware:
> * **When running or editing**: Hovering triggers Roslyn LSP, displaying type declarations and docstrings.
> * **When paused at a breakpoint**: Zed switches to Debugger Hover, sending DAP evaluation requests to display runtime values.

---

## License

This project is licensed under the [MIT License](LICENSE.md).
Third-party notices and underlying Unity Technologies components are acknowledged in `ThirdPartyNotices.md` within each subproject.
