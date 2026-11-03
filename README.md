# Zed Unity Suite

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)
[![Unity: 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-black.svg?logo=unity)](https://unity.com/)
[![Zed: 0.140+](https://img.shields.io/badge/Zed-Latest-blueviolet.svg?logo=zed)](https://zed.dev/)

一个专为在 **[Zed Editor](https://zed.dev/)** 中流畅开发与调试 **Unity C#** 项目打造的现代化双端生产级套件（Monorepo）。

本套件由两个协同子项目组成：
1. **`unity-zed-ide`**（`com.unifan.ide.zed`）：Unity Editor 外部代码编辑器集成插件（UPM Package）。基于 Unity 官方最新的 **`com.unity.ide.visualstudio@2.0.29`** 架构深度重构。
2. **`zed-unity-debug`**（`unity-debugger`）：Zed 原生调试扩展（Rust / WebAssembly DAP Adapter）。无缝对接 Visual Studio Tools for Unity 调试服务。

---

## 为什么选择 Zed Unity Suite？

在以往使用 Zed 开发 Unity 时，开发者往往会遇到以下典型痛点：
* ❌ **无法跨文件跳转与代码补全**：传统 Unity 插件生成的旧版 `.csproj` 与多 `.sln/.slnx` 导致 Zed 的 Roslyn 语言服务器无法正确识别解决方案，多程序集工程只能在当前文件内跳转。
* ❌ **冷启动打开脚本破坏工作区**：通过命令行同时打开目录与文件时，Zed 早期或特定版本会将文件作为独立的“单文件孤立根节点”挂载，导致 Roslyn 降级为单文件杂项模式（`IsMiscellaneousFile: true`）。
* ❌ **多开 Unity 调试乱串**：传统的调试器扫描固定端口或盲查 `netstat`，本地同时开两个 Unity 工程时经常连错进程。
* ❌ **配置繁琐**：需要手动编写 `.zed/settings.json`、排除大体积文件夹、手动下载 DAP 适配器。

**Zed Unity Suite 彻底解决了上述所有问题，提供与 VS Code / Rider 同级顺滑的开箱即用体验。**

---

## 目录结构

```text
zed-unity/
├── unity-zed-ide/         # [子项目 1] Unity 编辑器插件 (UPM Package)
│   ├── package.json
│   ├── Package.Build.props
│   ├── Package.Build.targets
│   ├── README.md
│   └── Editor/
│       ├── ZedInstallation.cs          # Zed 安装路径探测、Roslyn 分析器注入、.zed 配置生成、智能双阶段 CLI 启动
│       ├── Discovery.cs                # 注册 Zed 编辑器实例
│       ├── VisualStudioEditor.cs       # IExternalCodeEditor 核心实现
│       ├── VisualStudioIntegration.cs  # Unity Editor UDP 消息服务
│       ├── MSBuildProjectIntegration.cs# Unity 6+ MSBuild 管线支持
│       ├── ProjectGeneration/          # 现代 SDK-style 与 Legacy .csproj / .sln 生成器
│       ├── Messaging/                  # VSTU 调试与编辑器通信协议
│       ├── Testing/                    # Test Framework 集成
│       └── com.unifan.ide.zed.asmdef
│
├── zed-unity-debug/       # [子项目 2] Zed 调试器扩展 (Rust / WASM)
│   ├── Cargo.toml
│   ├── extension.toml
│   ├── README.md
│   ├── src/lib.rs                      # 精准 PID 端口计算、适配器智能定位与自动下载
│   └── debug_adapter_schemas/unity.json# DAP 配置参数约束
│
├── .gitignore
└── README.md
```

---

## 快速上手（Quick Start）

### 第 1 步：安装 Unity 插件 (`unity-zed-ide`)

选择以下任一方式将插件添加到你的 Unity 项目中：

* **方式 A：通过 Unity Package Manager 安装（推荐）**
  1. 打开 Unity 编辑器，点击菜单栏 **Window > Package Manager**。
  2. 点击左上角 **`+`** 号，选择 **Add package from git URL...**。
  3. 输入以下地址并点击 **Add**：
     ```text
     https://github.com/821869798/zed-unity.git?path=/unity-zed-ide
     ```

* **方式 B：直接编辑 `Packages/manifest.json`**
  ```json
  {
    "dependencies": {
      "com.unifan.ide.zed": "https://github.com/821869798/zed-unity.git?path=/unity-zed-ide"
    }
  }
  ```

* **方式 C：本地路径引入**
  ```json
  {
    "dependencies": {
      "com.unifan.ide.zed": "file:D:/path/to/zed-unity/unity-zed-ide"
    }
  }
  ```

### 第 2 步：配置 Unity 外部编辑器

1. 打开 Unity 菜单 **Edit > Preferences > External Tools**。
2. 在 **External Script Editor** 下拉列表中选择 **`Zed [版本号]`**。
3. 勾选需要生成的项目类型（推荐勾选 **Embedded packages** 和 **Local packages**）。
4. 插件会自动生成 SDK-style `.csproj`、统一的 `.sln` 以及自动配置好的 `.zed/settings.json` 与 `.zed/debug.json`。

### 第 3 步：安装 Zed 扩展

1. 在 Zed 中安装官方 **`C#`** 扩展（用于 Roslyn 语法高亮、代码补全与跨文件跳转）。
2. 在 Zed 中安装 **`unity-debugger`** 扩展：
   - 按快捷键 `Ctrl + Shift + P`（macOS: `Cmd + Shift + P`）打开命令面板。
   - 输入并运行 **`zed: install dev extension`**。
   - 选择本仓库中的 `zed-unity-debug` 目录，Zed 将自动加载并启用调试器。

---

## 核心特性详解

### 1. 现代化项目生成与语言服务体验 (`unity-zed-ide`)

* **现代 SDK 风格项目生成**：
  生成纯净标准、结构紧凑的 SDK 风格 `.csproj`，全面支持 C# 11~13 语法特性、`<Nullable>enable</Nullable>` 以及 Unity 6+ 的 MSBuild 管线。
* **Roslyn Analyzers 自动绑定**：
  自动扫描本地环境（Zed 插件缓存、VS Code 扩展目录、Visual Studio 目录）中的 `Microsoft.Unity.Analyzers.dll` 与 `Unity.SourceGenerators.dll`，在项目生成时直接注入到 `.csproj`，提供官方级的 Unity 专属代码诊断与性能优化建议。
* **冷启动目录扫描时序防护**：
  深度适配 Zed 1.26+ 的命令行行为。在 Zed 关闭状态下双击脚本时，插件先在后台拉起工程根目录，并留出必要的初次文件树扫描缓冲期，再精准跳转目标代码与行号，彻底杜绝孤立单文件根节点导致的 Roslyn 崩溃。
* **全自动化环境维护**：
  自动剔除冲突的 `.slnx` 文件，并在 `.zed/settings.json` 中自动锁定主 `.sln`，屏蔽 `Library`、`Temp`、`Logs` 等巨型临时目录的监听与检索，大幅提升 Zed 运行性能。

### 2. 生产级调试体验 (`zed-unity-debug`)

* **多实例精准端口绑定**：
  多开 Unity 项目时，扩展优先读取项目根目录下 `Library/EditorInstance.json` 中当前编辑器实例的精确 `process_id`，严格按 Unity 标准公式 `56000 + (process_id % 1000)` 计算目标端口，连接绝不串台。
* **自动探知与静默下载适配器**：
  无需在 `.zed/debug.json` 中手动硬编码调试器路径。扩展会自动复用本地已有的 `visualstudiotoolsforunity.vstuc` 适配器（如已安装过 VS Code 或 Cursor 扩展）；若未检测到，会自动从 GitHub Release 官方发布源静默下载解压至扩展隔离区。
* **全功能断点与变量检查**：
  - 断点命中、单步跳过（F10）、单步进入（F11）、单步跳出（Shift+F11）。
  - **Variables 面板**：层级展示 `Locals`（局部变量）、参数、`this` 以及复杂的嵌套对象图。
  - **Debugger Hover 悬停取值**：断点暂停时，鼠标悬停在变量上即可直接弹出实时数值卡片。
  - **Watch 监视**：在调试面板中支持添加自定义监视表达式。

---

## 日常调试流程

1. **进入调试**：保持 Unity 项目运行，在 Zed 中按 **`F4`**（或点击左下角调试小虫子面板中的运行按钮），选择 **`Attach to Unity Editor`**。
2. **设置断点**：在 C# 代码中按 **`F9`** 或点击行号左侧红点。
3. **命中断点**：切回 Unity 点击 Play 运行，触发代码即刻在 Zed 中暂停。
4. **查看变量**：
   - 鼠标悬停在变量上直接查看运行时值。
   - 在底部的 **Variables 面板** 查看所有作用域变量。
   - 在 **Watch** 中输入需要持续观察的复杂属性表达式。

---

## 常见问题与排查（FAQ）

#### Q: 首次从 Unity 双击脚本打开 Zed 时，为什么跨文件跳转需要等几秒？
> **A:** 无论是 VS Code 还是 Zed，首次冷启动打开一个包含 50+ 个程序集的大型 Unity 工程时，Roslyn 语言服务器都需要加载解决方案并解析全部元数据。请观察 Zed 状态栏右下角的 **`roslyn`** 状态，大约 5~7 秒初始化完毕后，跨文件跳转与语义分析即达到毫秒级响应。

#### Q: 为什么鼠标悬停有时候看得到值，有时候只看到类信息？
> **A:** Zed 的悬停是**上下文敏感的**：
> * **未命中断点（运行中或编写代码时）**：展示的是 Roslyn 语言服务器的静态类型签名与 XML 文档注释；
> * **命中断点且处于暂停状态时**：Zed 自动切换至调试模式，向 Unity 调试器发送求值请求并展示运行时数值。

---

## 开源协议

本项目采用 [MIT License](LICENSE.md) 开源协议。
内部引用及衍生的 Unity 官方组件均保留其原作者与 Unity Technologies 版权声明，详见各子目录下的 `ThirdPartyNotices.md`。
