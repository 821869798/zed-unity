# Zed Editor for Unity (`com.unifan.ide.zed`)

<p align="center">
  <a href="README.md">English</a> | <strong>简体中文</strong>
</p>

专为在 **Zed Editor** 中流畅开发 Unity C# 打造的 Unity 编辑器集成插件（UPM Package）。

本项目基于 Unity 官方最新的 `com.unity.ide.visualstudio@2.0.29` 架构进行重构，在保持与官方完全同级稳定性与特性的同时，专门针对 Zed 的语言服务器（Roslyn LSP）与架构特点进行了全方位深度优化。

---

## ⚡ 安装方法

### 方式 1：通过 Unity Package Manager (Git URL) 安装（推荐）

1. 打开 Unity 编辑器，点击菜单栏 **Window > Package Manager**。
2. 点击左上角的 **`+`** 按钮，选择 **Add package from git URL...**。
3. 输入以下地址并点击 **Add**：
   ```text
   https://github.com/821869798/zed-unity.git?path=/unity-zed-ide
   ```

### 方式 2：通过 `Packages/manifest.json` 安装

在 Unity 项目目录下的 `Packages/manifest.json` 中直接加入依赖：

```json
{
  "dependencies": {
    "com.unifan.ide.zed": "https://github.com/821869798/zed-unity.git?path=/unity-zed-ide",
    ...
  }
}
```

### 方式 3：本地源码引入 (Local File)

如果你将本仓库克隆到了本地磁盘：
```json
{
  "dependencies": {
    "com.unifan.ide.zed": "file:D:/path/to/zed-unity/unity-zed-ide",
    ...
  }
}
```

---

## 🚀 使用指南

1. 打开 Unity 菜单栏 **Edit > Preferences > External Tools**。
2. 在 **External Script Editor** 下拉列表中选择 **`Zed [版本号]`**（插件会自动探测你安装的 Zed 或 Zed Preview）。
3. 勾选需要生成的项目类型（推荐勾选 **Embedded packages** 和 **Local packages**）。
4. 在 Project 窗口双击任意 C# 脚本，Unity 将自动调用 Zed 并打开脚本，同时生成完整的项目工程文件与配置。

---

## 🔍 核心特性

- **现代 SDK 风格项目生成（SDK-Style `.csproj`）**：
  - 生成干净轻量、符合现代 .NET 标准的 SDK 风格 `.csproj`，支持 `<Nullable>enable</Nullable>`、C# 11~13 语法特性以及 Unity MSBuild Pipeline。
- **完美的 Roslyn 语言服务器支持（跨文件跳转与代码补全）**：
  - 自动维护单一权威的标准 `.sln` 解决方案，自动剔除冲突的 `.slnx` 文件，确保 Zed 的 C# 插件在打开项目时能够准确挂载全工程的 50+ 个程序集。
  - 自动扫描并引入 Unity 官方 Roslyn 分析器（`Microsoft.Unity.Analyzers.dll`）与代码生成器（`Unity.SourceGenerators.dll`），支持自动检测 Zed 插件目录、VS Code 插件目录以及 Visual Studio 安装目录。
- **防止 Zed 多根工作区污染与冷启动等待机制**：
  - 解决 Zed 1.26+ 在单次命令行中同时传入 `<folder>` 与 `<file:line:col>` 时会创建独立的“单文件孤立根节点”并破坏 Roslyn 加载的已知问题。
  - 支持智能冷启动感知：当 Zed 处于关闭状态时，先拉起工程根目录并等待工作区初次文件树扫描完成后，再精准导航至目标代码文件及行号，彻底根除冷启动无法跨文件跳转的痛点。
- **全自动生成 `.zed` 配置文件**：
  - 自动生成 `.zed/settings.json`：包含 Unity 临时文件夹排除规则（`Library`、`Temp`、`Logs` 等）、Unity 文件语法关联（`.shader`、`.uss`、`.uxml`、`.compute`、`.hlsl` 等）、以及锁定绑定当前 `.sln` 的 Roslyn 配置。
  - 自动生成 `.zed/debug.json`：生成开箱即用的 `Attach to Unity Editor` 调试配置。
- **完全解耦与共存**：
  - 独立命名空间 `Unifan.Zed.Editor`、独立程序集定义及全新的 Asset GUID，可与 Unity 内置的 `com.unity.ide.visualstudio` 插件安全并存，互不干扰。
