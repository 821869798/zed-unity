# Unity Debugger for Zed (`unity-debugger`)

专为 **Zed Editor** 打造的 Unity C# 调试器扩展（WebAssembly / DAP Adapter）。

基于 Zed Extension API (`0.7.0`) 与 Rust 构建，无缝对接 Visual Studio Tools for Unity DAP 调试后端（`UnityDebugAdapter.dll`），为 Unity 开发者带来流畅、零配置的断点调试体验。

---

## 核心特性

- **多开 Unity 精准进程与端口定位**：
  - 自动读取当前工作区下的 `Library/EditorInstance.json` 解析 Unity 进程 PID，按 Unity 标准算法 `56000 + (process_id % 1000)` 绑定调试端口，彻底解决本地同时打开多个 Unity 项目时 `netstat` 盲扫连错进程的严重缺陷。
- **全自动发现与下载调试器适配器（零配置）**：
  - 优先检测本地已安装的 Unity 调试工具（自动探测 VS Code、Cursor、Windsurf、Trae、Antigravity 等扩展目录下的 `visualstudiotoolsforunity.vstuc`）。
  - 若本机从未安装过该扩展，插件将自动从 GitHub 官方发布源安全下载解压适配器到扩展本地工作区，开发者完全无需手动配置复杂的 `adapterPath`。
- **全方位 DAP 调试功能支持**：
  - 支持行断点（F9）、单步跳过（F10）、单步进入（F11）、单步跳出（Shift+F11）、继续执行（F5）。
  - 支持完整的调用堆栈（Call Stack）、本地与成员变量树（Variables Panel）、监视表达式（Watch）。
  - 支持断点暂停状态下的**悬停变量求值（Debugger Hover）**，鼠标悬停即刻查看实时数值。

---

## 安装与使用

### 安装方式：作为开发者扩展安装

1. 克隆或下载本仓库到本地。
2. 在 Zed 中打开任意窗口，按 **`Ctrl + Shift + P`**（macOS: **`Cmd + Shift + P`**）调出命令面板。
3. 输入并选择 **`zed: install dev extension`**。
4. 在弹出的文件选择器中，选中本项目的 `zed-unity-debug` 目录。
5. Zed 将自动加载（若未预编译则会自动触发编译）并激活 `Unity Debugger` 扩展。

> **提示**：如果想手动编译 WASM 二进制文件，可在 `zed-unity-debug` 目录下执行：
> ```bash
> cargo build --target wasm32-wasip1 --release
> cp target/wasm32-wasip1/release/unity_debugger.wasm extension.wasm
> ```

---

## 调试配置 (`.zed/debug.json`)

配合 `unity-zed-ide` 插件使用时，工程根目录下的 `.zed/debug.json` 会全自动生成：

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

### 开始调试

1. 保持 Unity 编辑器处于打开状态。
2. 在 Zed 中打开项目，按 **`F4`**（或点击左下角调试小虫子面板，点击运行按钮）。
3. 选择 **`Attach to Unity Editor`** 即可附加到 Unity。
4. 在 Unity 中点击 Play 运行游戏，命中断点即可开始调试！
