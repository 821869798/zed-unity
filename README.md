# Zed Unity Suite

一个专为在 **Zed Editor** 中开发与调试 **Unity C#** 项目打造的双端集成套件（Monorepo），包含两个子项目：

1. **`unity-zed-ide`**（`com.unifan.ide.zed`）：基于 Unity 官方最新 **`com.unity.ide.visualstudio@2.0.29`** 架构重构的 Unity 外部代码编辑器集成插件（UPM Package）。
   * 支持现代 **SDK-style `.csproj`** 生成（C# 13、Nullable、Unity MSBuild Pipeline 支持）。
   * 自动生成标准 `.sln` 解决方案（并清理冲突的 `.slnx`），确保 Zed 的 Roslyn / OmniSharp / csharp-ls 语言服务器 100% 识别且无多解决方案歧义。
   * 自动扫描并注入本地已安装的 **Unity Roslyn Analyzers**（`Microsoft.Unity.Analyzers.dll`）。
   * 自动发现 Windows / macOS / Linux 上的 Zed 与 Zed Preview 安装路径并读取真实版本号（如 `Zed [1.15.0]`）。
   * 完整的 UDP `VisualStudioIntegration` 消息通道（端口 `56000 + (PID % 1000) + 2`），配合调试器与 Test Runner 通信。
   * 自动生成并维护 `.zed/settings.json`（文件排除规则、Unity YAML/Shader/UXML/USS 文件关联、Roslyn `solutionPath` 绑定）与 `.zed/debug.json`（零配置调试任务）。
   * 使用独立命名空间 `Unifan.Zed.Editor`、独立程序集 `Unifan.Zed.Editor` 与全新生成的 `.meta` GUID，可与项目自带的 `com.unity.ide.visualstudio` 完美共存。

2. **`zed-unity-debug`**：Zed 端的 Unity 调试器扩展（Rust / WebAssembly 扩展）。
   * **零配置自动查找 / 下载调试适配器**：无需手动在 `.zed/debug.json` 填写 `adapterPath`，自动扫描本地 `.vscode` / `.cursor` / `.antigravity` / `.trae` / `.windsurf` 扩展目录下的 `visualstudiotoolsforunity.vstuc` (`UnityDebugAdapter.dll`)，若未安装则自动从官方源下载解压到扩展工作区。
   * **多开 Unity 精准端口绑定**：优先读取当前工程下的 `Library/EditorInstance.json` 获取当前工程所属的 Unity 进程 `process_id`，通过 `56000 + (process_id % 1000)` 精准连接当前项目的 Unity Editor，彻底解决多开 Unity 时 `netstat` 盲扫连错进程的问题。

---

## 目录结构

```text
zed-unity/
├── unity-zed-ide/         # [子项目 1] Unity 插件包 (基于 com.unity.ide.visualstudio@2.0.29)
│   ├── package.json
│   ├── Package.Build.props
│   ├── Package.Build.targets
│   └── Editor/
│       ├── ZedInstallation.cs          # Zed 专属安装探测、Roslyn 分析器注入、.zed 配置生成、CLI 启动
│       ├── Discovery.cs                # 注册 ZedInstallation
│       ├── VisualStudioEditor.cs       # IExternalCodeEditor 核心实现
│       ├── VisualStudioIntegration.cs  # Unity Editor UDP 消息服务
│       ├── MSBuildProjectIntegration.cs# Unity 6+ MSBuild 管线支持
│       ├── ProjectGeneration/          # SDK-style 与 Legacy .csproj + .sln 生成器
│       ├── Messaging/                  # VSTU 调试与编辑器通信协议
│       ├── Testing/                    # Test Framework 集成
│       └── com.unifan.ide.zed.asmdef
│
├── zed-unity-debug/       # [子项目 2] Zed 调试器扩展 (Rust / WASM)
│   ├── Cargo.toml
│   ├── extension.toml
│   ├── src/lib.rs
│   └── debug_adapter_schemas/unity.json
│
├── references/            # [本地参考代码库] (已加入 .gitignore)
│   ├── com.unity.ide.visualstudio@2.0.29/  # ★ Unity 官方最新 IDE 支持包（核心底座参考）
│   ├── zed-csharp/                         # ★ Zed 官方 C# (Roslyn/OmniSharp) 扩展源码
│   ├── Unity-Zed-IDE/                      # 社区现有 Unity-Zed 集成插件
│   ├── zed-unity-debugger/                 # 社区现有 Zed Unity 调试扩展 (tomires)
│   ├── vscode-unity-debug/                 # Unity 旧版 Mono DAP 调试适配器
│   └── com.unity.ide.vscode/               # Unity 旧版 VSCode 插件包
│
└── .gitignore
```

---

## 开发与测试指南

### 1. Unity 插件 (`unity-zed-ide`)
* 在 Unity 工程的 `Packages/manifest.json` 中添加本地引用：
  ```json
  "com.unifan.ide.zed": "file:D:/program/my/zed-unity/unity-zed-ide"
  ```
* 打开 **Edit > Preferences > External Tools**，在 **External Script Editor** 中选择 `Zed [x.y.z]`。
* 插件会自动在项目根目录生成 `.sln`、SDK 风格的 `.csproj` 以及 `.zed/settings.json` 和 `.zed/debug.json`。

### 2. Zed 调试扩展 (`zed-unity-debug`)
* 在 Zed 中按 `Ctrl+Shift+P` (macOS `Cmd+Shift+P`) 输入 `zed: install dev extension`，选择 `D:\program\my\zed-unity\zed-unity-debug` 目录即可编译并加载扩展。
* 在 Zed 中打开 Unity 项目，按 `F4`（或打开 Debug 面板）选择 `Attach to Unity Editor` 即可直接断点调试。
