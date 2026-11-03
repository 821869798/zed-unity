using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.CodeEditor;

namespace ZedEditor
{
    [InitializeOnLoad]
    public class ZedScriptEditor : IExternalCodeEditor
    {
        const string k_ZedArgumentPref = "zed_arguments";
        const string k_ZedExtensionPref = "zed_userExtensions";
        static readonly GUIContent k_ResetArguments = EditorGUIUtility.TrTextContent("Reset argument");
        string m_Arguments;

        readonly IDiscovery m_Discoverability;
        readonly IGenerator m_ProjectGeneration;

        static readonly string[] k_SupportedFileNames = 
        { 
            "zed.exe", 
            "zed", 
            "zed.app", 
            "zedpreview.app", 
            "dev.zed.zed" 
        };

        static bool IsOSX => Application.platform == RuntimePlatform.OSXEditor;

        static string DefaultApp => EditorPrefs.GetString("kScriptsDefaultApp");

        // Zed CLI syntax: zed "<projectPath>" "<filePath>:<line>:<column>"
        public static string DefaultArgument { get; } = "\"$(ProjectPath)\" \"$(File):$(Line):$(Column)\"";

        string Arguments
        {
            get => m_Arguments ?? (m_Arguments = EditorPrefs.GetString(k_ZedArgumentPref, DefaultArgument));
            set
            {
                m_Arguments = value;
                EditorPrefs.SetString(k_ZedArgumentPref, value);
            }
        }

        static string[] defaultExtensions
        {
            get
            {
                var customExtensions = new[] { "json", "asmdef", "log", "cs" };
                return EditorSettings.projectGenerationBuiltinExtensions
                    .Concat(EditorSettings.projectGenerationUserExtensions)
                    .Concat(customExtensions)
                    .Distinct().ToArray();
            }
        }

        static string[] HandledExtensions
        {
            get
            {
                return HandledExtensionsString
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.TrimStart('.', '*'))
                    .ToArray();
            }
        }

        static string HandledExtensionsString
        {
            get => EditorPrefs.GetString(k_ZedExtensionPref, string.Join(";", defaultExtensions));
            set => EditorPrefs.SetString(k_ZedExtensionPref, value);
        }

        public CodeEditor.Installation[] Installations => m_Discoverability.PathCallback();

        public ZedScriptEditor(IDiscovery discovery, IGenerator projectGeneration)
        {
            m_Discoverability = discovery;
            m_ProjectGeneration = projectGeneration;
        }

        static ZedScriptEditor()
        {
            var projectDirectory = Directory.GetParent(Application.dataPath).FullName;
            var editor = new ZedScriptEditor(new ZedDiscovery(), new ProjectGeneration(projectDirectory));
            CodeEditor.Register(editor);

            if (IsZedInstallation(CodeEditor.CurrentEditorInstallation))
            {
                editor.CreateIfDoesntExist();
            }
        }

        public void Initialize(string editorInstallationPath)
        {
        }

        public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
        {
            var lowerCasePath = editorPath.ToLower();
            var filename = Path.GetFileName(lowerCasePath).Replace(" ", "");
            var installations = Installations;

            if (!IsZedInstallation(editorPath))
            {
                installation = default;
                return false;
            }

            installation = installations.FirstOrDefault(it => it.Path == editorPath);
            if (installation.Equals(default(CodeEditor.Installation)))
            {
                installation = new CodeEditor.Installation
                {
                    Name = "Zed",
                    Path = editorPath
                };
            }
            return true;
        }

        public void OnGUI()
        {
            Arguments = EditorGUILayout.TextField("External Script Editor Args", Arguments);
            if (GUILayout.Button(k_ResetArguments, GUILayout.Width(120)))
            {
                Arguments = DefaultArgument;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generate .csproj files for:");
            EditorGUI.indentLevel++;
            SettingsButton(ProjectGenerationFlag.Embedded, "Embedded packages", "");
            SettingsButton(ProjectGenerationFlag.Local, "Local packages", "");
            SettingsButton(ProjectGenerationFlag.Registry, "Registry packages", "");
            SettingsButton(ProjectGenerationFlag.Git, "Git packages", "");
            SettingsButton(ProjectGenerationFlag.BuiltIn, "Built-in packages", "");
#if UNITY_2019_3_OR_NEWER
            SettingsButton(ProjectGenerationFlag.LocalTarBall, "Local tarball", "");
#endif
            SettingsButton(ProjectGenerationFlag.Unknown, "Packages from unknown sources", "");
            RegenerateProjectFiles();
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            HandledExtensionsString = EditorGUILayout.TextField(new GUIContent("Extensions handled: "), HandledExtensionsString);
        }

        void RegenerateProjectFiles()
        {
            var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
            rect.width = 252;
            if (GUI.Button(rect, "Regenerate project files"))
            {
                m_ProjectGeneration.Sync();
            }
        }

        void SettingsButton(ProjectGenerationFlag preference, string guiMessage, string toolTip)
        {
            var prevValue = m_ProjectGeneration.AssemblyNameProvider.ProjectGenerationFlag.HasFlag(preference);
            var newValue = EditorGUILayout.Toggle(new GUIContent(guiMessage, toolTip), prevValue);
            if (newValue != prevValue)
            {
                m_ProjectGeneration.AssemblyNameProvider.ToggleProjectGeneration(preference);
            }
        }

        public void CreateIfDoesntExist()
        {
            if (!m_ProjectGeneration.SolutionExists())
            {
                m_ProjectGeneration.Sync();
            }
        }

        public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
        {
            (m_ProjectGeneration.AssemblyNameProvider as IPackageInfoCache)?.ResetPackageInfoCache();
            m_ProjectGeneration.SyncIfNeeded(addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles).ToList(), importedFiles);
        }

        public void SyncAll()
        {
            (m_ProjectGeneration.AssemblyNameProvider as IPackageInfoCache)?.ResetPackageInfoCache();
            AssetDatabase.Refresh();
            m_ProjectGeneration.Sync();
        }

        public bool OpenProject(string path, int line, int column)
        {
            if (!string.IsNullOrEmpty(path) && (!SupportsExtension(path) || !File.Exists(path)))
            {
                return false;
            }

            if (line == -1)
                line = 1;
            if (column == -1)
                column = 0;

            var projectDir = Path.GetFullPath(m_ProjectGeneration.ProjectDirectory);
            string arguments;

            if (Arguments != DefaultArgument)
            {
                arguments = CodeEditor.ParseArgument(Arguments, path, line, column);
            }
            else
            {
                // Always pass project folder first so Zed opens it as workspace root
                if (!string.IsNullOrEmpty(path) && path != projectDir)
                {
                    var fullFilePath = Path.GetFullPath(path);
                    arguments = $"\"{projectDir}\" \"{fullFilePath}:{line}:{column}\"";
                }
                else
                {
                    arguments = $"\"{projectDir}\"";
                }
            }

            var app = DefaultApp;
            if (IsOSX)
            {
                return OpenOSX(app, arguments);
            }

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = app,
                    Arguments = arguments,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                };
                Process.Start(processInfo);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ZedScriptEditor] Failed to launch Zed: {ex.Message}");
                return false;
            }
        }

        static bool OpenOSX(string appPath, string arguments)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = $"-n -a \"{appPath}\" --args {arguments}",
                        UseShellExecute = true,
                    }
                };
                process.Start();
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ZedScriptEditor] Failed to launch Zed on macOS: {ex.Message}");
                return false;
            }
        }

        static bool SupportsExtension(string path)
        {
            var extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension))
                return false;
            return HandledExtensions.Contains(extension.TrimStart('.'));
        }

        static bool IsZedInstallation(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var lowerCasePath = path.ToLower().Replace('\\', '/');
            var filename = Path.GetFileName(lowerCasePath).Replace(" ", "");
            return k_SupportedFileNames.Contains(filename) || lowerCasePath.Contains("zed");
        }
    }
}
