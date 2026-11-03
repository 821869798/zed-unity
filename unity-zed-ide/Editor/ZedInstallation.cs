/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using UnityEngine;
using SimpleJSON;
using IOPath = System.IO.Path;

namespace Unifan.Zed.Editor
{
	internal class ZedInstallation : VisualStudioInstallation
	{
		private const string MicrosoftUnityExtensionId = "visualstudiotoolsforunity.vstuc";

		private static readonly string[] VstucExtensionHostDirs =
		{
			".vscode",
			".vscode-insiders",
			".cursor",
			".antigravity",
			".trae",
			".trae-cn",
			".windsurf",
			".qoder",
			".kiro"
		};

#if !UNITY_MSBUILD_ENABLED
		private static readonly IGenerator _generator = GeneratorFactory.GetInstance(GeneratorStyle.SDK);
#endif

		public override bool SupportsAnalyzers => true;

#if !UNITY_MSBUILD_ENABLED
		public override Version LatestLanguageVersionSupported => new Version(13, 0);
#endif

		private static string GetExtensionPath()
		{
			// 1. Check Zed's own extension work directory (populated automatically by zed-unity-debug)
			var zedWorkExtensionDirs = new List<string>();
#if UNITY_EDITOR_WIN
			var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			if (!string.IsNullOrEmpty(localAppData))
				zedWorkExtensionDirs.Add(IOPath.Combine(localAppData, "Zed", "extensions", "work", "unity-debugger", "adapter", "extension"));
#elif UNITY_EDITOR_OSX
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (!string.IsNullOrEmpty(home))
				zedWorkExtensionDirs.Add(IOPath.Combine(home, "Library", "Application Support", "Zed", "extensions", "work", "unity-debugger", "adapter", "extension"));
#else
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (!string.IsNullOrEmpty(home))
				zedWorkExtensionDirs.Add(IOPath.Combine(home, ".local", "share", "zed", "extensions", "work", "unity-debugger", "adapter", "extension"));
#endif
			foreach (var zedExtDir in zedWorkExtensionDirs)
			{
				if (Directory.Exists(zedExtDir))
					return zedExtDir;
			}

			// 2. Fallback: Reuse existing local VS Code / Cursor / Trae / Windsurf vstuc installation if present
			var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (string.IsNullOrEmpty(userProfile))
				return null;

			foreach (var hostDir in VstucExtensionHostDirs)
			{
				var extensionsPath = IOPath.Combine(userProfile, hostDir, "extensions");
				if (!Directory.Exists(extensionsPath))
					continue;

				var match = Directory
					.EnumerateDirectories(extensionsPath, $"{MicrosoftUnityExtensionId}*")
					.OrderByDescending(n => n)
					.FirstOrDefault();

				if (!string.IsNullOrEmpty(match))
					return match;
			}

			return null;
		}

		public override string[] GetAnalyzers()
		{
			var vstuPath = GetExtensionPath();
			if (string.IsNullOrEmpty(vstuPath))
				return Array.Empty<string>();

			return GetAnalyzers(vstuPath);
		}

#if !UNITY_MSBUILD_ENABLED
		public override IGenerator ProjectGenerator => _generator;
#endif

		private static bool IsCandidateForDiscovery(string path)
		{
			if (string.IsNullOrEmpty(path))
				return false;

#if UNITY_EDITOR_OSX
			if (Directory.Exists(path) && Regex.IsMatch(path, @".*Zed.*\.app$", RegexOptions.IgnoreCase))
				return true;
			return File.Exists(path) && Regex.IsMatch(IOPath.GetFileName(path), @"^(zed|cli)$", RegexOptions.IgnoreCase);
#elif UNITY_EDITOR_WIN
			return File.Exists(path) && Regex.IsMatch(IOPath.GetFileName(path), @"^zed.*\.exe$", RegexOptions.IgnoreCase);
#else
			return File.Exists(path) && Regex.IsMatch(IOPath.GetFileName(path), @"^(zed|zeditor|dev\.zed\.Zed)$", RegexOptions.IgnoreCase);
#endif
		}

		public static bool TryDiscoverInstallation(string editorPath, out IVisualStudioInstallation installation)
		{
			installation = null;

			if (string.IsNullOrEmpty(editorPath))
				return false;

			if (!IsCandidateForDiscovery(editorPath))
				return false;

			Version version = null;
			var isPrerelease = editorPath.IndexOf("preview", StringComparison.OrdinalIgnoreCase) >= 0;

			try
			{
				var realPath = GetRealPath(editorPath);

#if UNITY_EDITOR_WIN
				version = TryGetWindowsVersion(realPath, out var winPre);
				isPrerelease = isPrerelease || winPre;
#elif UNITY_EDITOR_OSX
				version = TryGetMacVersion(realPath);
#endif
			}
			catch (Exception)
			{
				// do not fail if we are not able to retrieve the exact version number
			}

			installation = new ZedInstallation()
			{
				IsPrerelease = isPrerelease,
				Name = "Zed" + (isPrerelease ? " Preview" : string.Empty) + (version != null ? $" [{version.ToString(3)}]" : string.Empty),
				Path = editorPath,
				Version = version ?? new Version()
			};

			return true;
		}

#if UNITY_EDITOR_WIN
		private static Version TryGetWindowsVersion(string exePath, out bool isPrerelease)
		{
			isPrerelease = false;
			var targetExe = exePath;

			// If pointing to the CLI wrapper (e.g., Zed\bin\Zed.exe whose FileVersion is 0.1.0.0),
			// check the parent directory for the main Zed.exe binary to read the real product version.
			var dir = IOPath.GetDirectoryName(exePath);
			if (!string.IsNullOrEmpty(dir) && string.Equals(IOPath.GetFileName(dir), "bin", StringComparison.OrdinalIgnoreCase))
			{
				var parentDir = IOPath.GetDirectoryName(dir);
				if (!string.IsNullOrEmpty(parentDir))
				{
					var mainExe = IOPath.Combine(parentDir, "Zed.exe");
					if (File.Exists(mainExe))
						targetExe = mainExe;
				}
			}

			var versionInfo = FileVersionInfo.GetVersionInfo(targetExe);
			if (!string.IsNullOrEmpty(versionInfo.ProductVersion) && versionInfo.ProductVersion.IndexOf("preview", StringComparison.OrdinalIgnoreCase) >= 0)
				isPrerelease = true;

			var rawVersion = versionInfo.FileVersion ?? versionInfo.ProductVersion;
			if (string.IsNullOrEmpty(rawVersion))
				return null;

			var cleanVersion = rawVersion.Split('+', '-').FirstOrDefault();
			if (Version.TryParse(cleanVersion, out var parsed) && parsed > new Version(0, 1, 0, 0))
				return parsed;

			return null;
		}
#endif

#if UNITY_EDITOR_OSX
		private static Version TryGetMacVersion(string path)
		{
			var appDir = path;
			if (!Directory.Exists(appDir))
			{
				// e.g., /Applications/Zed.app/Contents/MacOS/cli
				var parent = Directory.GetParent(path);
				if (parent?.Name == "MacOS" && parent.Parent?.Name == "Contents")
					appDir = parent.Parent.Parent?.FullName;
			}

			if (string.IsNullOrEmpty(appDir))
				return null;

			var plistPath = IOPath.Combine(appDir, "Contents", "Info.plist");
			if (!File.Exists(plistPath))
				return null;

			var xPath = new XPathDocument(plistPath);
			var nav = xPath.CreateNavigator().SelectSingleNode("/plist/dict/key[text()='CFBundleShortVersionString']/following-sibling::string[1]/text()");
			if (nav != null && Version.TryParse(nav.Value.Split('-', '+').First(), out var ver))
				return ver;

			return null;
		}
#endif

		public static IEnumerable<IVisualStudioInstallation> GetVisualStudioInstallations()
		{
			var candidates = new List<string>();

#if UNITY_EDITOR_WIN
			var localAppPrograms = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
			var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

			foreach (var basePath in new[] { localAppPrograms, localApp, programFiles })
			{
				if (string.IsNullOrEmpty(basePath))
					continue;

				candidates.Add(IOPath.Combine(basePath, "Zed", "Zed.exe"));
				candidates.Add(IOPath.Combine(basePath, "Zed Preview", "Zed.exe"));
			}
#elif UNITY_EDITOR_OSX
			var appPaths = new[]
			{
				"/Applications",
				IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications")
			};

			foreach (var appPath in appPaths)
			{
				if (!Directory.Exists(appPath))
					continue;

				try
				{
					candidates.AddRange(Directory.EnumerateDirectories(appPath, "Zed*.app"));
				}
				catch
				{
					// ignore permission errors
				}
			}
#elif UNITY_EDITOR_LINUX
			var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (!string.IsNullOrEmpty(userProfile))
				candidates.Add(IOPath.Combine(userProfile, ".local", "bin", "zed"));

			candidates.Add("/usr/bin/zed");
			candidates.Add("/usr/bin/zeditor");
			candidates.Add("/usr/local/bin/zed");
			candidates.Add("/usr/local/bin/zeditor");
			candidates.Add("/var/lib/flatpak/exports/bin/dev.zed.Zed");
			candidates.Add("/var/lib/flatpak/app/dev.zed.Zed/current/active/files/bin/zed");
			candidates.Add("/run/current-system/sw/bin/zeditor");

			candidates.AddRange(GetXdgCandidates());
#endif

			foreach (var candidate in candidates.Distinct())
			{
				if (TryDiscoverInstallation(candidate, out var installation))
					yield return installation;
			}
		}

#if UNITY_EDITOR_LINUX
		private static readonly Regex DesktopFileExecEntry = new Regex(@"Exec=(\S+)", RegexOptions.Singleline | RegexOptions.Compiled);
		private static readonly string[] LinuxDesktopFileNames = { "dev.zed.Zed.desktop", "zed.desktop", "zeditor.desktop" };

		private static IEnumerable<string> GetXdgCandidates()
		{
			var envdirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
			if (string.IsNullOrEmpty(envdirs))
				yield break;

			var dirs = envdirs.Split(':');
			foreach (var dir in dirs)
			{
				foreach (var desktopFileName in LinuxDesktopFileNames)
				{
					Match match = null;
					try
					{
						var desktopFile = IOPath.Combine(dir, "applications", desktopFileName);
						if (!File.Exists(desktopFile))
							continue;

						var content = File.ReadAllText(desktopFile);
						match = DesktopFileExecEntry.Match(content);
					}
					catch
					{
						// do not fail if we cannot read desktop file
					}

					if (match == null || !match.Success)
						continue;

					yield return match.Groups[1].Value;
					yield break;
				}
			}
		}

		[System.Runtime.InteropServices.DllImport("libc")]
		private static extern int readlink(string path, byte[] buffer, int buflen);

		internal static string GetRealPath(string path)
		{
			byte[] buf = new byte[512];
			int ret = readlink(path, buf, buf.Length);
			if (ret == -1) return path;
			char[] cbuf = new char[512];
			int chars = System.Text.Encoding.Default.GetChars(buf, 0, ret, cbuf, 0);
			return new String(cbuf, 0, chars);
		}
#else
		internal static string GetRealPath(string path)
		{
			return path;
		}
#endif

		public override void CreateExtraFiles(string projectDirectory)
		{
			try
			{
				var zedDirectory = IOPath.Combine(projectDirectory.NormalizePathSeparators(), ".zed");
				Directory.CreateDirectory(zedDirectory);

				var enablePatch = !File.Exists(IOPath.Combine(zedDirectory, ".zedpatchdisable"));

				CreateSettingsFile(zedDirectory, enablePatch);
				CreateDebugFile(zedDirectory, enablePatch);
			}
			catch (IOException)
			{
			}
		}

		private const string DefaultDebugFileContent = @"[
    {
        ""label"": ""Attach to Unity Editor"",
        ""adapter"": ""unity"",
        ""request"": ""attach"",
        ""projectPath"": ""${ZED_WORKTREE_ROOT}""
    }
]
";

		private static void CreateDebugFile(string zedDirectory, bool enablePatch)
		{
			var debugFile = IOPath.Combine(zedDirectory, "debug.json");
			if (File.Exists(debugFile))
			{
				if (enablePatch)
					PatchDebugFile(debugFile);

				return;
			}

			File.WriteAllText(debugFile, DefaultDebugFileContent);
		}

		private static void PatchDebugFile(string debugFile)
		{
			try
			{
				const string adapterKey = "adapter";

				var content = File.ReadAllText(debugFile);
				var debugArray = JSONNode.Parse(content) as JSONArray;
				if (debugArray == null)
					return;

				if (debugArray.Linq.Any(entry => entry.Value[adapterKey]?.Value == "unity"))
					return;

				var defaultContent = JSONNode.Parse(DefaultDebugFileContent) as JSONArray;
				if (defaultContent != null && defaultContent.Count > 0)
				{
					debugArray.Add(defaultContent[0]);
					WriteAllTextFromJObject(debugFile, debugArray);
				}
			}
			catch (Exception)
			{
				// do not fail if we cannot patch the debug.json file
			}
		}

		private void CreateSettingsFile(string zedDirectory, bool enablePatch)
		{
			var settingsFile = IOPath.Combine(zedDirectory, "settings.json");
			if (File.Exists(settingsFile))
			{
				if (enablePatch)
					PatchSettingsFile(settingsFile);

				return;
			}

			var solutionFile = DefaultSolutionFileName();
			var content = @"{
    ""file_scan_exclusions"": [
        ""**/.DS_Store"",
        ""**/.git"",
        ""**/.vs"",
        ""**/.vscode"",
        ""**/.gitmodules"",
        ""**/.vsconfig"",
        ""**/*.booproj"",
        ""**/*.pidb"",
        ""**/*.suo"",
        ""**/*.user"",
        ""**/*.userprefs"",
        ""**/*.unityproj"",
        ""**/*.dll"",
        ""**/*.exe"",
        ""**/*.pdf"",
        ""**/*.mid"",
        ""**/*.midi"",
        ""**/*.wav"",
        ""**/*.gif"",
        ""**/*.ico"",
        ""**/*.jpg"",
        ""**/*.jpeg"",
        ""**/*.png"",
        ""**/*.psd"",
        ""**/*.tga"",
        ""**/*.tif"",
        ""**/*.tiff"",
        ""**/*.3ds"",
        ""**/*.3DS"",
        ""**/*.fbx"",
        ""**/*.FBX"",
        ""**/*.lxo"",
        ""**/*.LXO"",
        ""**/*.ma"",
        ""**/*.MA"",
        ""**/*.obj"",
        ""**/*.OBJ"",
        ""**/*.asset"",
        ""**/*.cubemap"",
        ""**/*.flare"",
        ""**/*.mat"",
        ""**/*.meta"",
        ""**/*.prefab"",
        ""**/*.unity"",
        ""*.csproj"",
        ""*.sln"",
        ""*.slnx"",
        ""build/"",
        ""Build/"",
        ""Library/"",
        ""library/"",
        ""obj/"",
        ""Obj/"",
        ""Logs/"",
        ""logs/"",
        ""ProjectSettings/"",
        ""UserSettings/"",
        ""temp/"",
        ""Temp/""
    ],
    ""file_types"": {
        ""YAML"": [
            ""*.asset"",
            ""*.meta"",
            ""*.prefab"",
            ""*.unity"",
            ""*.mat"",
            ""*.anim"",
            ""*.controller"",
            ""*.overrideController"",
            ""*.mask""
        ],
        ""XML"": [
            ""*.uxml""
        ],
        ""CSS"": [
            ""*.uss""
        ],
        ""HLSL"": [
            ""*.shader"",
            ""*.compute"",
            ""*.cginc"",
            ""*.hlsl""
        ]
    },
    ""lsp"": {
        ""roslyn"": {
            ""initialization_options"": {
                ""solutionPath"": """ + solutionFile + @"""
            }
        }
    }
}
";

			File.WriteAllText(settingsFile, content);
		}

		private static void PatchSettingsFile(string settingsFile)
		{
			try
			{
				var content = File.ReadAllText(settingsFile);
				var settings = JSONNode.Parse(content) as JSONObject;
				if (settings == null)
					return;

				var patched = false;
				var solutionFile = DefaultSolutionFileName();

				var lsp = settings["lsp"] as JSONObject;
				if (lsp == null)
				{
					lsp = new JSONObject();
					settings["lsp"] = lsp;
					patched = true;
				}

				var roslyn = lsp["roslyn"] as JSONObject;
				if (roslyn == null)
				{
					roslyn = new JSONObject();
					lsp["roslyn"] = roslyn;
					patched = true;
				}

				var initOptions = roslyn["initialization_options"] as JSONObject;
				if (initOptions == null)
				{
					initOptions = new JSONObject();
					roslyn["initialization_options"] = initOptions;
					patched = true;
				}

				if (initOptions["solutionPath"]?.Value != solutionFile)
				{
					initOptions["solutionPath"] = solutionFile;
					patched = true;
				}

				if (patched)
					WriteAllTextFromJObject(settingsFile, settings);
			}
			catch (Exception)
			{
				// do not fail if we cannot patch the settings.json file
			}
		}

		private static void WriteAllTextFromJObject(string file, JSONNode node)
		{
			using (var fs = File.Open(file, FileMode.Create))
			using (var sw = new StreamWriter(fs))
			{
				sw.Write(node.ToString(aIndent: 4));
			}
		}

		private static string DefaultSolutionFileName()
		{
#if UNITY_MSBUILD_ENABLED
			return MSBuildProjectIntegration.SolutionFileName;
#else
			return IOPath.GetFileName(_generator.SolutionFile());
#endif
		}

		private static string ResolveLaunchExecutable(string application)
		{
#if UNITY_EDITOR_WIN
			// Prefer Zed\bin\Zed.exe CLI launcher if available, as it handles workspace + file:line:col cleanly
			var dir = IOPath.GetDirectoryName(application);
			if (!string.IsNullOrEmpty(dir) && !string.Equals(IOPath.GetFileName(dir), "bin", StringComparison.OrdinalIgnoreCase))
			{
				var cliCandidate = IOPath.Combine(dir, "bin", "Zed.exe");
				if (File.Exists(cliCandidate))
					return cliCandidate;
			}
#elif UNITY_EDITOR_OSX
			// If application is /Applications/Zed.app, prefer Contents/MacOS/cli to reuse existing window cleanly
			if (Directory.Exists(application))
			{
				var cliCandidate = IOPath.Combine(application, "Contents", "MacOS", "cli");
				if (File.Exists(cliCandidate))
					return cliCandidate;

				var zedCandidate = IOPath.Combine(application, "Contents", "MacOS", "zed");
				if (File.Exists(zedCandidate))
					return zedCandidate;
			}
#endif
			return application;
		}

		public override bool Open(string path, int line, int column, string solution)
		{
			var application = ResolveLaunchExecutable(Path);

			line = Math.Max(1, line);
			column = Math.Max(1, column);

			var rawDirectory = IOPath.GetDirectoryName(solution);
			var directory = string.IsNullOrEmpty(rawDirectory)
				? IOPath.GetFullPath(IOPath.Combine(Application.dataPath, "..")).NormalizePathSeparators()
				: IOPath.GetFullPath(rawDirectory).NormalizePathSeparators();

			var normalizedPath = string.Empty;
			if (!string.IsNullOrEmpty(path))
			{
				var fullPath = IOPath.IsPathRooted(path)
					? IOPath.GetFullPath(path)
					: IOPath.GetFullPath(IOPath.Combine(directory, path));
				normalizedPath = fullPath.NormalizePathSeparators();
			}

			// Always pass the absolute project directory first so Zed uses it as the workspace root,
			// followed by the absolute file:line:column target with matching native separators.
			var arguments = string.IsNullOrEmpty(normalizedPath)
				? $"\"{directory}\""
				: $"\"{directory}\" \"{normalizedPath}:{line}:{column}\"";

			ProcessRunner.Start(ProcessStartInfoFor(application, arguments));
			return true;
		}

		private static ProcessStartInfo ProcessStartInfoFor(string application, string arguments)
		{
#if UNITY_EDITOR_OSX
			if (Directory.Exists(application))
			{
				arguments = $"-a \"{application}\" --args {arguments}";
				application = "open";
				return ProcessRunner.ProcessStartInfoFor(application, arguments, redirect: false, shell: true);
			}
#endif
			return ProcessRunner.ProcessStartInfoFor(application, arguments, redirect: false, shell: false);
		}

		public static void Initialize()
		{
		}
	}
}
