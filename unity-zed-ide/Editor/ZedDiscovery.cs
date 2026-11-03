using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.CodeEditor;

namespace ZedEditor
{
    public interface IDiscovery
    {
        CodeEditor.Installation[] PathCallback();
    }

    public class ZedDiscovery : IDiscovery
    {
        List<CodeEditor.Installation> m_Installations;

        public CodeEditor.Installation[] PathCallback()
        {
            if (m_Installations == null)
            {
                m_Installations = new List<CodeEditor.Installation>();
                FindInstallationPaths();
            }

            return m_Installations.ToArray();
        }

        void FindInstallationPaths()
        {
            var possiblePaths = new List<string>();

#if UNITY_EDITOR_OSX
            possiblePaths.Add("/Applications/Zed.app");
            possiblePaths.Add("/Applications/Zed Preview.app");
            possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications/Zed.app"));
#elif UNITY_EDITOR_WIN
            var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(localAppData))
            {
                possiblePaths.Add(Path.Combine(localAppData, "Zed", "zed.exe"));
                possiblePaths.Add(Path.Combine(localAppData, "Programs", "Zed", "zed.exe"));
            }

            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            if (!string.IsNullOrEmpty(programFiles))
            {
                possiblePaths.Add(Path.Combine(programFiles, "Zed", "zed.exe"));
            }
#else
            possiblePaths.Add("/usr/bin/zed");
            possiblePaths.Add("/usr/local/bin/zed");
            possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/bin/zed"));
            possiblePaths.Add("/var/lib/flatpak/exports/bin/dev.zed.Zed");
#endif

            var existingPaths = possiblePaths.Where(ZedExists).Distinct().ToList();
            if (!existingPaths.Any())
            {
                return;
            }

            m_Installations = existingPaths.Select(path => new CodeEditor.Installation
            {
                Name = path.Contains("Preview") ? "Zed Preview" : "Zed",
                Path = path
            }).ToList();
        }

        public static bool ZedExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

#if UNITY_EDITOR_OSX
            return Directory.Exists(path);
#else
            return File.Exists(path);
#endif
        }
    }
}
