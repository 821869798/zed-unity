# Unity Debugger for Zed (`unity-debug`)

<p align="center">
  <strong>English</strong> | <a href="README.zh-CN.md">简体中文</a>
</p>

A dedicated Unity C# debugger extension (WebAssembly / DAP Adapter) for **Zed Editor**.

Built with Rust and the Zed Extension API (`0.7.0`), it seamlessly bridges Zed to Visual Studio Tools for Unity DAP backend (`UnityDebugAdapter.dll`) to deliver a smooth, zero-config debugging experience.

---

## ⚡ Installation & Usage

### Installing as a Dev Extension (Local)

1. Clone or download this repository locally.
2. In Zed, open any window and press **`Ctrl + Shift + P`** (**`Cmd + Shift + P`** on macOS).
3. Search for and select **`zed: install dev extension`**.
4. Select the `zed-unity-debug` directory in the file picker.
5. Zed will build/load the extension and activate `Unity Debugger`.

> **Note**: To manually compile the WebAssembly binary:
> ```bash
> cargo build --target wasm32-wasip1 --release
> cp target/wasm32-wasip1/release/unity_debugger.wasm extension.wasm
> ```

---

## 🐞 Starting a Debug Session

When used with `unity-zed-ide`, your project's `.zed/debug.json` is generated automatically:

```json
[
  {
    "label": "Attach to Unity Editor",
    "adapter": "unity",
    "request": "attach",
    "projectPath": "${ZED_WORKTREE_ROOT}"
  }
]
```

1. Keep your Unity Editor running with your project open.
2. In Zed, press **`F4`** (or click the bug icon in the bottom-left panel and click the play button).
3. Select **`Attach to Unity Editor`**.
4. In Unity, enter Play mode. Hit your breakpoints and start debugging!

---

## 🔍 Key Features

- **Accurate PID & Port Resolution**:
  - Automatically parses Unity's PID from `Library/EditorInstance.json` and computes the exact port using Unity's standard formula `56000 + (process_id % 1000)`. Eliminates port collisions and wrong-process attachments when running multiple Unity instances.
- **Auto-Discovery & Fallback Download**:
  - Reuses existing `visualstudiotoolsforunity.vstuc` installations (from VS Code, Cursor, Windsurf, Trae, Antigravity, etc.).
  - If no local adapter is found, automatically downloads and extracts the official GitHub release directly into the extension's isolated workspace.
- **Full DAP Capabilities**:
  - Breakpoints (`F9`), Continue (`F5`), Step Over (`F10`), Step Into (`F11`), Step Out (`Shift + F11`).
  - **Variables Panel**: Interactive tree of `Locals`, arguments, and `this` object graphs.
  - **Debugger Hover**: Hovering over variables while paused displays real-time runtime values.
  - **Watch Expressions**: Supports adding persistent watch expressions that recalculate on every step.
