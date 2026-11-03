# Zed Unity Suite

<p align="center">
  <a href="README.md">English</a> | <strong>简体中文</strong>
</p>

<p align="center">
  <a href="LICENSE.md"><img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License: MIT"></a>
  <a href="https://unity.com/"><img src="https://img.shields.io/badge/Unity-2021.3%2B-black.svg?logo=unity" alt="Unity: 2021.3+"></a>
  <a href="https://zed.dev/"><img src="https://img.shields.io/badge/Zed-Latest-blueviolet.svg?logo=zed" alt="Zed: Latest"></a>
  <a href="https://github.com/821869798/zed-unity/releases"><img src="https://img.shields.io/github/v/release/821869798/zed-unity?color=green" alt="Release"></a>
</p>

专为在 **[Zed Editor](https://zed.dev/)** 中流畅开发与调试 **Unity C#** 项目打造的现代化双端生产级套件（Monorepo）。

本套件由两个协同子项目组成：
1. **`unity-zed-ide`**（`com.unifan.ide.zed`）：Unity Editor 外部代码编辑器集成插件（UPM Package）。基于 Unity 官方最新的 **`com.unity.ide.visualstudio@2.0.29`** 架构深度重构，专为 Zed 的 Roslyn LSP 与工作区机制深度定制。
2. **`zed-unity-debug`**（`unity-debugger`）：Zed 原生调试扩展（Rust / WebAssembly DAP Adapter）。无缝对接 Visual Studio Tools for Unity 调试服务。

---

## 为什么选择 Zed Unity Suite？

| 传统集成方案的痛点 | Zed Unity Suite 的解决方案 |
| :--- | :--- |
| **跨文件跳转与补全失效**<br>旧版 `.csproj` 与冲突的 `.slnx` 导致 Roslyn 语言服务器无法正确识别项目，多程序集工程只能在单文件内跳转。 | **现代 SDK-style `.csproj` 与单一 `.sln`**<br>生成纯净紧凑的 SDK 风格 `.csproj`，自动维护唯一的 `.sln` 并清理冲突的 `.slnx`，确保 Roslyn 挂载全工程的 50+ 个程序集。 |
| **冷启动打开脚本破坏工作区**<br>Zed 处于关闭状态时从 Unity 双击脚本，CLI 会将该脚本挂载为独立的“单文件孤立根节点”（`IsMiscellaneousFile: true`）。 | **双阶段时序智能防护**<br>冷启动时先拉起工程根目录，等待后台文件索引扫描就绪后，再精准跳转至目标代码行与列，彻底解决冷启动跨文件失效。 |
| **缺少 Unity 专属代码分析**<br>原生 Roslyn 在 Zed 中缺乏 Unity 语法规则、优化警告和源代码生成器。 | **自动注入 Roslyn Analyzers**<br>自动扫描 Zed 插件缓存、VS Code 扩展目录与 Visual Studio，自动将 `Microsoft.Unity.Analyzers.dll` 与 `Unity.SourceGenerators.dll` 注入到项目文件中。 |
| **多开 Unity 调试乱串**<br>盲扫端口或写死端口在本地同时打开多个 Unity 工程时经常连错进程。 | **基于精确 PID 计算目标端口**<br>读取 `Library/EditorInstance.json` 解析当前工程的 Unity PID，通过 `56000 + (PID % 1000)` 绑定唯一端口，绝不串台。 |
| **配置繁琐**<br>需要手动编写 `.zed/settings.json`、排除大体积文件夹、手动下载 DAP 适配器。 | **全自动零配置体验**<br>自动生成 `.zed/settings.json`（排除 `Library/`、`Temp/` 等）、自动生成 `.zed/debug.json`，且缺少调试组件时自动下载。 |

---

## 目录结构

```text
zed-unity/
├── unity-zed-ide/         # [子项目 1] Unity 编辑器插件 (UPM Package)
│   ├── package.json
│   ├── Package.Build.props
│   ├── Package.Build.targets
│   ├── README.md          # 英文 UPM 文档
│   ├── README.zh-CN.md    # 中文 UPM 文档
│   └── Editor/
│       ├── ZedInstallation.cs          # Zed 安装探测、Roslyn 分析器注入、双阶段 CLI 启动
│       ├── Discovery.cs                # 编辑器探测注册
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
│   ├── README.md          # 英文扩展文档
│   ├── README.zh-CN.md    # 中文扩展文档
│   ├── src/lib.rs                      # 精准 PID 端口计算、适配器智能定位与自动下载
│   └── debug_adapter_schemas/unity.json# DAP 配置参数约束
│
├── .gitignore
├── LICENSE.md
├── README.md              # 英文主文档
└── README.zh-CN.md        # 中文主文档
```

---

## 快速上手（Quick Start）

### 1. 安装 Unity 插件 (`unity-zed-ide`)

选择以下任一方式将插件添加到你的 Unity 项目中：

#### 方式 A：通过 Unity Package Manager Git URL 安装（推荐）
1. 在 Unity 中打开 **Window > Package Manager**。
2. 点击左上角的 **`+`** 按钮，选择 **Add package from git URL...**。
3. 输入以下地址并点击 **Add**：
   ```text
   https://github.com/821869798/zed-unity.git?path=/unity-zed-ide
   ```

#### 方式 B：直接编辑 `Packages/manifest.json`
```json
{
  "dependencies": {
    "com.unifan.ide.zed": "https://github.com/821869798/zed-unity.git?path=/unity-zed-ide"
  }
}
```

#### 方式 C：本地路径引入 (Local Package)
```json
{
  "dependencies": {
    "com.unifan.ide.zed": "file:D:/path/to/zed-unity/unity-zed-ide"
  }
}
```

### 2. 配置 Unity 外部编辑器
1. 打开 Unity 菜单 **Edit > Preferences > External Tools**。
2. 在 **External Script Editor** 下拉列表中选择 **`Zed [版本号]`**（插件会自动探测你安装的 Zed 或 Zed Preview）。
3. 勾选需要生成的项目类型（推荐勾选 **Embedded packages** 和 **Local packages**）。
4. 在 Project 窗口双击任意 C# 脚本，Unity 将自动调用 Zed 并打开脚本，同时生成完整的项目工程文件与配置。

### 3. 安装 Zed 扩展
1. 在 Zed 中安装官方 **`C#`** 扩展（用于 Roslyn 语法高亮、代码补全与跨文件跳转）。
2. 在 Zed 中安装 **`unity-debugger`** 扩展：
   - 按快捷键 `Ctrl + Shift + P`（macOS: `Cmd + Shift + P`）打开命令面板。
   - 输入并运行 **`zed: install dev extension`**。
   - 选择本仓库中的 `zed-unity-debug` 目录，Zed 将自动加载并启用调试器。

---

## 核心特性详解

### 1. 现代化项目生成与语言服务体验 (`unity-zed-ide`)
* **现代 SDK 风格项目生成**：生成纯净标准、结构紧凑的 SDK 风格 `.csproj`，全面支持 C# 11~13 语法特性、`<Nullable>enable</Nullable>` 以及 Unity 6+ 的 MSBuild 管线。
* **Roslyn Analyzers 自动绑定**：自动扫描本地环境（Zed 插件缓存、VS Code 扩展目录、Visual Studio 目录）中的 `Microsoft.Unity.Analyzers.dll` 与 `Unity.SourceGenerators.dll`，在项目生成时直接注入到 `.csproj`，提供官方级的 Unity 专属代码诊断与性能优化建议。
* **冷启动时序智能防护**：深度适配 Zed 1.26+ 的命令行行为。在 Zed 关闭状态下双击脚本时，插件先在后台拉起工程根目录，并等待初次文件树扫描完成后，再精准跳转目标代码与行号，彻底杜绝孤立单文件根节点导致的 Roslyn 崩溃。
* **全自动化环境维护**：自动剔除冲突的 `.slnx` 文件，并在 `.zed/settings.json` 中自动锁定主 `.sln`，屏蔽 `Library`、`Temp`、`Logs` 等巨型临时目录的监听与检索，大幅提升 Zed 运行性能。

### 2. 生产级调试体验 (`zed-unity-debug`)
* **多实例精准端口绑定**：多开 Unity 项目时，扩展优先读取项目根目录下 `Library/EditorInstance.json` 中当前编辑器实例的精确 `process_id`，严格按 Unity 标准公式 `56000 + (process_id % 1000)` 计算目标端口，连接绝不串台。
* **自动探知与静默下载适配器**：无需在 `.zed/debug.json` 中手动硬编码调试器路径。扩展会自动复用本地已有的 `visualstudiotoolsforunity.vstuc` 适配器（如已安装过 VS Code 或 Cursor 扩展）；若未检测到，会自动从 GitHub Release 官方发布源静默下载解压至扩展隔离区。
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
