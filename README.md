# Zed Unity Suite

一个专为在 **Zed Editor** 中开发与调试 **Unity C#** 项目打造的双端集成套件（Monorepo），包含两个子项目：

1. **`unity-zed-ide`**：Unity 外部代码编辑器集成插件（UPM Package）。负责在 Unity 中自动发现 Zed、双击精准跳转文件行列、自动生成高质量 `.sln` / `.csproj` 解决方案供 Zed C# 语言服务器（Roslyn/OmniSharp）提供代码补全与语法检查。
2. **`zed-unity-debug`**：Zed 端的 Unity 调试器扩展（Rust / WebAssembly 扩展）。负责提供 DAP（Debug Adapter Protocol）支持，对接 Unity 专属 Mono 软调试协议，实现编辑器内的断点、单步执行与变量查看。

---

## 目录结构

```text
zed-unity/
├── unity-zed-ide/         # [子项目 1] Unity 插件包 (UPM package)
│   ├── package.json
│   └── Editor/
│       ├── ZedDiscovery.cs
│       ├── ZedScriptEditor.cs
│       ├── ProjectGeneration/
│       └── com.unifan.ide.zed.asmdef
│
├── zed-unity-debug/       # [子项目 2] Zed 调试器扩展 (Rust / WASM)
│   ├── Cargo.toml
│   ├── extension.toml
│   ├── src/lib.rs
│   └── debug_adapter_schemas/unity.json
│
├── references/            # [参考代码库] 已拉取到本地，加入 .gitignore，方便对照学习
│   ├── com.unity.ide.vscode/   # Unity 官方 VSCode 插件包（首要参考）
│   ├── Unity-Zed-IDE/          # 社区现有 Unity-Zed 集成插件
│   ├── zed-unity-debugger/     # 社区现有 Zed Unity 调试扩展 (tomires)
│   └── vscode-unity-debug/     # Unity 官方 Mono DAP 调试适配器
│
└── .gitignore
```

---

## 本地参考项目对照

| 项目 | 本地路径 | 说明与核心参考点 |
| :--- | :--- | :--- |
| **Unity 官方 VSCode 支持** | `references/com.unity.ide.vscode` | 官方标准的 `IExternalCodeEditor` 与 `ProjectGeneration` 实现逻辑。 |
| **用户现有多 IDE 项目** | `D:\program\my\com.unifan.ides` | 多 AI IDE（Cursor, Antigravity, Trae 等）的路径发现与集成架构。 |
| **社区 Unity-Zed-IDE** | `references/Unity-Zed-IDE` | Zed 专用的命令行调用、偏好设置界面。 |
| **社区 zed-unity-debugger** | `references/zed-unity-debugger` | Zed Rust WASM 调试适配器生命周期、端口嗅探（56000-56999）逻辑。 |
| **官方 Mono DAP 适配器** | `references/vscode-unity-debug` | Unity Mono Soft Debugger 通信协议实现。 |

---

## 开发与构建指南

### 1. Unity 插件 (`unity-zed-ide`)
* 在任何 Unity 工程的 `Packages/manifest.json` 中，通过本地文件路径引用：
  ```json
  "com.unifan.ide.zed": "file:D:/program/my/zed-unity/unity-zed-ide"
  ```
* 或者在 Unity `Package Manager` -> `Add package from disk...` 选择 `unity-zed-ide/package.json`。

### 2. Zed 调试扩展 (`zed-unity-debug`)
* 依赖：Rust 工具链（支持 `wasm32-wasip1` 或 `wasm32-wasi`）。
* 安装开发中的扩展：
  在 Zed 中按 `Ctrl+Shift+P` (macOS `Cmd+Shift+P`) 输入 `zed: install dev extension`，选择 `zed-unity-debug` 目录即可热加载测试。
