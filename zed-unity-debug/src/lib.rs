use std::fs;
use serde_json::{Value, json};
use zed_extension_api::{
    self as zed,
    DebugAdapterBinary, DebugConfig, DebugRequest, DebugScenario,
    DebugTaskDefinition, DownloadedFileType, Result, StartDebuggingRequestArguments,
    StartDebuggingRequestArgumentsRequest, Worktree, process::Command, resolve_tcp_template,
};

const VSTUC_DOWNLOAD_DIR: &str = "vstuc-adapter";
const VSTUC_RELATIVE_DLL: &str = "extension/bin/UnityDebugAdapter.dll";
const VSTUC_MARKETPLACE_URL: &str = "https://VisualStudioToolsForUnity.gallery.vsassets.io/_apis/public/gallery/publisher/VisualStudioToolsForUnity/extension/vstuc/latest/assetbyname/Microsoft.VisualStudio.Services.VSIXPackage";

enum AdapterRuntime {
    /// dotnet <path> — for .NET Core / .NET 5+ managed assemblies (.dll)
    Dotnet,
    /// mono <path> — for Mono / .NET Framework assemblies (.exe)
    Mono,
}

struct UnityDebuggerExtension;

impl UnityDebuggerExtension {
    /// Returns (adapter_path, runtime).
    /// Priority:
    /// 1. `user_provided_path` or `"adapterPath"` in `.zed/debug.json`
    /// 2. Auto-discover `UnityDebugAdapter.dll` from local VS Code / Cursor / Antigravity / Trae / Windsurf extensions
    /// 3. Auto-download official `visualstudiotoolsforunity.vstuc` VSIX into extension work dir
    fn find_adapter(
        &self,
        user_provided_path: Option<String>,
        config: &str,
        worktree: &Worktree,
        is_windows: bool,
    ) -> Result<(String, AdapterRuntime), String> {
        let explicit_path = user_provided_path
            .filter(|p| !p.is_empty())
            .or_else(|| {
                serde_json::from_str::<Value>(config)
                    .ok()
                    .and_then(|cfg| cfg.get("adapterPath")?.as_str().map(str::to_string))
                    .filter(|p| !p.is_empty())
            });

        let path = match explicit_path {
            Some(p) => p,
            None => self.auto_discover_or_download_adapter(worktree, is_windows)?,
        };

        let runtime = if path.to_ascii_lowercase().ends_with(".exe") {
            AdapterRuntime::Mono
        } else {
            AdapterRuntime::Dotnet
        };

        Ok((path, runtime))
    }

    fn auto_discover_or_download_adapter(
        &self,
        worktree: &Worktree,
        is_windows: bool,
    ) -> Result<String, String> {
        // 1. Check if already downloaded in our extension work directory
        let cached_rel = format!("{VSTUC_DOWNLOAD_DIR}/{VSTUC_RELATIVE_DLL}");
        if fs::metadata(&cached_rel).is_ok_and(|stat| stat.is_file()) {
            if let Ok(cwd) = std::env::current_dir() {
                return Ok(cwd.join(&cached_rel).to_string_lossy().into_owned());
            }
        }

        // 2. Search local IDE extension directories (.vscode, .cursor, .antigravity, .trae, .windsurf, etc.)
        if let Some(local_dll) = self.find_local_vstuc_adapter(worktree, is_windows) {
            return Ok(local_dll);
        }

        // 3. Auto-download from Visual Studio Marketplace as a Zip (VSIX is a standard zip archive)
        zed::download_file(VSTUC_MARKETPLACE_URL, VSTUC_DOWNLOAD_DIR, DownloadedFileType::Zip)
            .map_err(|e| {
                format!(
                    "Could not find local Unity Debug Adapter and auto-download failed ({e}). \
                     Install the 'Unity' extension (visualstudiotoolsforunity.vstuc) in VS Code, \
                     or set `\"adapterPath\"` in `.zed/debug.json`."
                )
            })?;

        if fs::metadata(&cached_rel).is_ok_and(|stat| stat.is_file()) {
            let cwd = std::env::current_dir()
                .map_err(|e| format!("Failed to resolve extension working directory: {e}"))?;
            return Ok(cwd.join(&cached_rel).to_string_lossy().into_owned());
        }

        Err(
            "Downloaded VSIX package did not contain expected `extension/bin/UnityDebugAdapter.dll`. \
             Please specify `\"adapterPath\"` in `.zed/debug.json`."
                .to_string(),
        )
    }

    fn find_local_vstuc_adapter(&self, worktree: &Worktree, is_windows: bool) -> Option<String> {
        let env = worktree.shell_env();
        let home = env
            .iter()
            .find(|(k, _)| {
                if is_windows {
                    k.eq_ignore_ascii_case("USERPROFILE")
                } else {
                    k.eq_ignore_ascii_case("HOME")
                }
            })
            .map(|(_, v)| v.as_str())?;

        let ide_dirs = [
            ".vscode",
            ".vscode-insiders",
            ".cursor",
            ".antigravity",
            ".trae",
            ".trae-cn",
            ".windsurf",
            ".qoder",
            ".kiro",
        ];

        if is_windows {
            for ide_dir in ide_dirs {
                let ext_base = format!("{}\\{}\\extensions", home, ide_dir);
                let pattern = format!("{}\\visualstudiotoolsforunity.vstuc*", ext_base);
                let output = Command::new("cmd")
                    .args(["/c", "dir", "/b", "/ad", "/o-n", &pattern])
                    .output()
                    .ok()?;

                let stdout = String::from_utf8_lossy(&output.stdout);
                if let Some(first_dir) = stdout.lines().map(str::trim).find(|l| !l.is_empty()) {
                    let dll_path = format!("{}\\{}\\bin\\UnityDebugAdapter.dll", ext_base, first_dir);
                    let check = Command::new("cmd")
                        .args(["/c", "if", "exist", &dll_path, "echo", "FOUND"])
                        .output()
                        .ok()?;
                    if String::from_utf8_lossy(&check.stdout).contains("FOUND") {
                        return Some(dll_path);
                    }
                }
            }
        } else {
            let script = format!(
                "for d in {}; do \
                    for p in \"{}/$d/extensions/visualstudiotoolsforunity.vstuc\"*; do \
                        if [ -f \"$p/bin/UnityDebugAdapter.dll\" ]; then \
                            echo \"$p/bin/UnityDebugAdapter.dll\"; \
                        fi; \
                    done; \
                 done | sort -r | head -1",
                ide_dirs.join(" "),
                home
            );
            let output = Command::new("/bin/sh").args(["-c", &script]).output().ok()?;
            let found = String::from_utf8_lossy(&output.stdout).trim().to_string();
            if !found.is_empty() {
                return Some(found);
            }
        }

        None
    }

    /// Discovers the Unity Editor Mono Soft Debugger endpoint (`127.0.0.1:<port>`).
    /// Priority:
    /// 1. Reads `{project_root}/Library/EditorInstance.json` to get the exact `process_id`
    ///    of the Unity Editor running THIS project, and calculates `56000 + (process_id % 1000)`.
    ///    This guarantees attaching to the right Unity Editor even when multiple Unity instances are open.
    /// 2. Falls back to scanning listening TCP ports in `56000..=56999` via `lsof` / `netstat`.
    fn discover_unity_port(&self, worktree: &Worktree, is_windows: bool) -> Result<String, String> {
        if let Some(port) = self.discover_port_from_editor_instance(worktree, is_windows) {
            return Ok(format!("127.0.0.1:{}", port));
        }

        let err = "Unity debugger not running. \
                   Make sure Unity Editor is open with this project and Debug code optimization is enabled.";

        if !is_windows && worktree.which("lsof").is_some() {
            // macOS and Linux: list TCP ports in Unity's SDB range that are LISTEN
            let output = Command::new("/bin/sh")
                .args([
                    "-c",
                    "lsof -nP -i TCP:56000-56999 2>/dev/null \
                     | grep LISTEN | awk '{print $9}' | grep -oE '[0-9]+$' | head -1",
                ])
                .output()
                .map_err(|e| format!("Failed to run Unity port discovery: {}", e))?;
            let port = String::from_utf8_lossy(&output.stdout).trim().to_string();
            if !port.is_empty() {
                return Ok(format!("127.0.0.1:{}", port));
            }
        } else {
            // Windows: run netstat and filter in Rust to avoid shell quoting issues
            let output = Command::new("cmd")
                .args(["/c", "netstat -an"])
                .output()
                .map_err(|e| format!("Failed to run Unity port discovery: {}", e))?;
            let stdout = String::from_utf8_lossy(&output.stdout);
            for line in stdout.lines() {
                // Format: "  TCP    127.0.0.1:56716    0.0.0.0:0    LISTENING"
                if !line.contains("LISTENING") {
                    continue;
                }
                if let Some(port) = line
                    .split_whitespace()
                    .nth(1)
                    .and_then(|addr| addr.rsplit_once(':'))
                    .map(|(_, p)| p.trim())
                    .filter(|p| p.len() == 5 && p.starts_with("56"))
                {
                    return Ok(format!("127.0.0.1:{}", port));
                }
            }
        }

        Err(err.to_string())
    }

    fn discover_port_from_editor_instance(
        &self,
        worktree: &Worktree,
        is_windows: bool,
    ) -> Option<u32> {
        let content = worktree
            .read_text_file("Library/EditorInstance.json")
            .ok()
            .or_else(|| {
                let root = worktree.root_path();
                if is_windows {
                    let path = format!("{}\\Library\\EditorInstance.json", root.replace('/', "\\"));
                    let out = Command::new("cmd").args(["/c", "type", &path]).output().ok()?;
                    if out.status == Some(0) {
                        Some(String::from_utf8_lossy(&out.stdout).into_owned())
                    } else {
                        None
                    }
                } else {
                    let path = format!("{}/Library/EditorInstance.json", root.trim_end_matches('/'));
                    let out = Command::new("/bin/sh")
                        .args(["-c", &format!("cat \"{}\" 2>/dev/null", path)])
                        .output()
                        .ok()?;
                    if out.status == Some(0) {
                        Some(String::from_utf8_lossy(&out.stdout).into_owned())
                    } else {
                        None
                    }
                }
            })?;

        let parsed: Value = serde_json::from_str(&content).ok()?;
        let pid = parsed.get("process_id")?.as_u64()? as u32;
        if pid == 0 {
            return None;
        }

        // Unity's standard Mono Soft Debugger port formula: 56000 + (PID % 1000)
        Some(56000 + (pid % 1000))
    }

    fn build_config(
        &self,
        raw_config: &str,
        project_path: &str,
        endpoint: &str,
        is_mono: bool,
    ) -> String {
        let mut cfg: Value = serde_json::from_str(raw_config).unwrap_or_else(|_| json!({}));

        cfg["request"] = json!("attach");

        if is_mono {
            // unity-dap (net472/.exe) expects separate "address" and "port" fields
            if let Some((host, port_str)) = endpoint.rsplit_once(':') {
                cfg["address"] = json!(host);
                if let Ok(port_num) = port_str.parse::<u16>() {
                    cfg["port"] = json!(port_num);
                }
            }
            if let Some(obj) = cfg.as_object_mut() {
                obj.remove("projectPath");
                obj.remove("endPoint");
            }
        } else {
            // VSTU adapter (.dll) expects a combined "endPoint" string
            cfg["endPoint"] = json!(endpoint);
            if cfg.get("projectPath").is_none() || cfg["projectPath"] == json!(null) {
                cfg["projectPath"] = json!(project_path);
            }
        }

        if let Some(obj) = cfg.as_object_mut() {
            obj.remove("adapterPath");
            obj.remove("adapterArgs");
        }

        serde_json::to_string(&cfg).unwrap_or_else(|_| raw_config.to_string())
    }
}

impl zed::Extension for UnityDebuggerExtension {
    fn new() -> Self {
        Self
    }

    fn get_dap_binary(
        &mut self,
        _adapter_name: String,
        config: DebugTaskDefinition,
        user_provided_debug_adapter_path: Option<String>,
        worktree: &Worktree,
    ) -> Result<DebugAdapterBinary, String> {
        // COMSPEC is set on Windows (points to cmd.exe) and not on Unix.
        let is_windows = worktree
            .shell_env()
            .iter()
            .any(|(k, _)| k.eq_ignore_ascii_case("COMSPEC"));

        let (adapter_path, is_mono) = {
            let (path, runtime) = self.find_adapter(
                user_provided_debug_adapter_path,
                &config.config,
                worktree,
                is_windows,
            )?;
            let mono = matches!(runtime, AdapterRuntime::Mono);
            (path, mono)
        };

        // On Windows, net472 .exe files run natively under .NET Framework — no Mono needed.
        // Using Mono on Windows as a subprocess causes pipe/stdio issues.
        let (command, adapter_in_args) = if is_mono && is_windows {
            (adapter_path.clone(), false)
        } else if is_mono {
            let mono = worktree.which("mono").ok_or_else(|| {
                "Mono runtime (`mono`) not found on PATH. \
                 Install Mono from https://www.mono-project.com/download/stable/."
                    .to_string()
            })?;
            (mono, true)
        } else {
            let dotnet = worktree.which("dotnet").ok_or_else(|| {
                ".NET runtime (`dotnet`) not found on PATH. \
                 Install .NET 8 or later from https://dotnet.microsoft.com/download."
                    .to_string()
            })?;
            (dotnet, true)
        };

        let project_path = worktree.root_path();

        let raw: Value = serde_json::from_str(&config.config).unwrap_or_else(|_| json!({}));
        let endpoint = match raw.get("endPoint").and_then(|v| v.as_str()).filter(|s| !s.is_empty()) {
            Some(ep) => ep.to_string(),
            None => self.discover_unity_port(worktree, is_windows)?,
        };

        let adapter_config = self.build_config(&config.config, &project_path, &endpoint, is_mono);

        let connection = match config.tcp_connection {
            Some(template) => Some(resolve_tcp_template(template)?),
            None => None,
        };

        let raw_args: Vec<String> = raw
            .get("adapterArgs")
            .and_then(|v| v.as_array())
            .map(|arr| arr.iter().filter_map(|v| v.as_str().map(str::to_string)).collect())
            .unwrap_or_default();

        let mut arguments = if adapter_in_args { vec![adapter_path] } else { vec![] };
        arguments.extend(raw_args);

        Ok(DebugAdapterBinary {
            command: Some(command),
            arguments,
            envs: vec![],
            cwd: None,
            connection,
            request_args: StartDebuggingRequestArguments {
                configuration: adapter_config,
                request: StartDebuggingRequestArgumentsRequest::Attach,
            },
        })
    }

    fn dap_request_kind(
        &mut self,
        _adapter_name: String,
        _config: Value,
    ) -> Result<StartDebuggingRequestArgumentsRequest, String> {
        Ok(StartDebuggingRequestArgumentsRequest::Attach)
    }

    fn dap_config_to_scenario(&mut self, config: DebugConfig) -> Result<DebugScenario, String> {
        let project_path = match &config.request {
            DebugRequest::Launch(req) => req.cwd.clone().unwrap_or_default(),
            DebugRequest::Attach(_) => String::new(),
        };

        let adapter_config = json!({
            "request": "attach",
            "projectPath": if project_path.is_empty() { json!(null) } else { json!(project_path) },
        });

        Ok(DebugScenario {
            label: config.label,
            adapter: "unity".to_string(),
            config: serde_json::to_string(&adapter_config).map_err(|e| e.to_string())?,
            tcp_connection: None,
            build: None,
        })
    }
}

zed::register_extension!(UnityDebuggerExtension);
