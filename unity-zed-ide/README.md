# Zed Editor for Unity (`com.unifan.ide.zed`)

<p align="center">
  <strong>English</strong> | <a href="README.zh-CN.md">简体中文</a>
</p>

A specialized Unity Editor integration package (UPM Package) for seamless C# development in **Zed Editor**.

Built upon Unity's official **`com.unity.ide.visualstudio@2.0.29`** architecture, completely refactored and tailored for Zed's Roslyn language server and workspace mechanics.

---

## Key Features

- **Modern SDK-Style Project Generation**:
  - Generates clean, lightweight, and modern SDK-style `.csproj` files with full support for `<Nullable>enable</Nullable>`, C# 11~13 features, and Unity MSBuild pipelines.
- **First-Class Roslyn Language Server Support**:
  - Automatically maintains a single authoritative `.sln` file and purges conflicting `.slnx` files to ensure Zed's C# extension mounts all 50+ project assemblies cleanly.
  - Automatically scans and injects official Unity Roslyn Analyzers (`Microsoft.Unity.Analyzers.dll`) and source generators (`Unity.SourceGenerators.dll`).
- **Cold-Start Launch Timing Protection**:
  - Fixes the issue where Zed 1.26+ CLI calls with both `<folder>` and `<file:line:col>` attach the file as an isolated single-file worktree.
  - Detects cold starts: launches the root workspace folder first, waits 1.5 seconds for Zed's background worktree scanner to index project files, and then navigates to the target file and line.
- **Automated `.zed` Workspace Configurations**:
  - Automatically generates `.zed/settings.json` (excluding `Library/`, `Temp/`, `Logs/`, configuring Unity file associations, and locking the active solution).
  - Automatically generates `.zed/debug.json` with zero-config `Attach to Unity Editor`.
- **Clean Coexistence**:
  - Uses independent namespace `Unifan.Zed.Editor` and unique GUIDs, safely coexisting with Unity's built-in `com.unity.ide.visualstudio`.

---

## Installation

### Option 1: Via Unity Package Manager (Git URL)

1. Open Unity Editor, click **Window > Package Manager**.
2. Click **`+`** in the top-left corner, select **Add package from git URL...**.
3. Enter:
   ```text
   https://github.com/821869798/zed-unity.git?path=/unity-zed-ide
   ```

### Option 2: Via `Packages/manifest.json`

Add the dependency directly into your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unifan.ide.zed": "https://github.com/821869798/zed-unity.git?path=/unity-zed-ide",
    ...
  }
}
```

### Option 3: Local Package Reference

```json
{
  "dependencies": {
    "com.unifan.ide.zed": "file:D:/path/to/zed-unity/unity-zed-ide",
    ...
  }
}
```

---

## Usage

1. Open Unity **Edit > Preferences > External Tools**.
2. Under **External Script Editor**, select **`Zed [Version]`** (auto-detected).
3. Check the desired project generation boxes (**Embedded packages** and **Local packages** recommended).
4. Double-click any C# script in Unity. Zed will launch with complete cross-file navigation and debugging configuration ready to go.
