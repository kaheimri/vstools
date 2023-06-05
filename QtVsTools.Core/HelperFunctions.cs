/***************************************************************************************************
 Copyright (C) 2023 The Qt Company Ltd.
 SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only
***************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;

using Process = System.Diagnostics.Process;

namespace QtVsTools.Core
{
    using Common;
    using MsBuild;
    using static SyntaxAnalysis.RegExpr;
    using static Utils;

    public static class HelperFunctions
    {
        static LazyFactory StaticLazy { get; } = new();

        static readonly HashSet<string> _sources = new(new[] { ".c", ".cpp", ".cxx" }, CaseIgnorer);
        public static bool IsSourceFile(string fileName)
        {
            return _sources.Contains(Path.GetExtension(fileName));
        }

        static readonly HashSet<string> _headers = new(new[] { ".h", ".hpp", ".hxx" }, CaseIgnorer);
        public static bool IsHeaderFile(string fileName)
        {
            return _headers.Contains(Path.GetExtension(fileName));
        }

        public static bool IsUicFile(string fileName)
        {
            return ".ui".Equals(Path.GetExtension(fileName), IgnoreCase);
        }

        public static bool IsQrcFile(string fileName)
        {
            return ".qrc".Equals(Path.GetExtension(fileName), IgnoreCase);
        }

        public static bool IsWinRCFile(string fileName)
        {
            return ".rc".Equals(Path.GetExtension(fileName), IgnoreCase);
        }

        public static bool IsTranslationFile(string fileName)
        {
            return ".ts".Equals(Path.GetExtension(fileName), IgnoreCase);
        }

        public static bool IsQmlFile(string fileName)
        {
            return ".qml".Equals(Path.GetExtension(fileName), IgnoreCase);
        }

        /// <summary>
        /// Returns the normalized file path of a given file.
        /// </summary>
        /// <param name="name">file name</param>
        public static string NormalizeFilePath(string name)
        {
            var fi = new FileInfo(name);
            return fi.FullName;
        }

        public static string NormalizeRelativeFilePath(string path)
        {
            if (path == null)
                return ".\\";

            path = path.Trim();
            path = ToNativeSeparator(path);

            var tmp = string.Empty;
            while (tmp != path) {
                tmp = path;
                path = path.Replace("\\\\", "\\");
            }

            path = path.Replace("\"", "");

            if (path != "." && !IsAbsoluteFilePath(path)
                && !path.StartsWith(".\\", IgnoreCase)
                && !path.StartsWith("$", IgnoreCase)) {
                path = ".\\" + path;
            }
            if (path.EndsWith("\\", IgnoreCase))
                path = path.Substring(0, path.Length - 1);

            return path;
        }

        public static bool IsAbsoluteFilePath(string path)
        {
            path = path.Trim();
            if (path.Length >= 2 && path[1] == ':')
                return true;
            return path.StartsWith("\\", IgnoreCase)
                || path.StartsWith("/", IgnoreCase);
        }

        /// <summary>
        /// Returns the relative path between a given file and a path.
        /// </summary>
        /// <param name="path">absolute path</param>
        /// <param name="file">absolute file name</param>
        public static string GetRelativePath(string path, string file)
        {
            if (file == null || path == null)
                return "";
            var fi = new FileInfo(file);
            var di = new DirectoryInfo(path);

            var fiArray = fi.FullName.Split(Path.DirectorySeparatorChar);
            var dir = di.FullName;
            while (dir.EndsWith("\\", IgnoreCase))
                dir = dir.Remove(dir.Length - 1, 1);
            var diArray = dir.Split(Path.DirectorySeparatorChar);

            var minLen = fiArray.Length < diArray.Length ? fiArray.Length : diArray.Length;
            int i = 0, j, commonParts = 0;

            while (i < minLen && fiArray[i].ToLower() == diArray[i].ToLower()) {
                commonParts++;
                i++;
            }

            if (commonParts < 1)
                return fi.FullName;

            var result = string.Empty;

            for (j = i; j < fiArray.Length; j++) {
                if (j == i)
                    result = fiArray[j];
                else
                    result += Path.DirectorySeparatorChar + fiArray[j];
            }
            while (i < diArray.Length) {
                result = "..\\" + result;
                i++;
            }
            //MessageBox.Show(path + "\n" + file + "\n" + result);
            if (result.StartsWith("..\\", StringComparison.Ordinal))
                return result;
            return ".\\" + result;
        }

        public static bool HasQObjectDeclaration(VCFile file)
        {
            return CxxStream.ContainsNotCommented(file,
                new[]
                {
                    "Q_OBJECT",
                    "Q_GADGET",
                    "Q_NAMESPACE"
                },
                StringComparison.Ordinal, true);
        }

        /// <summary>
        /// Converts all directory separators of the path to the alternate character
        /// directory separator. For instance, FromNativeSeparators("c:\\winnt\\system32")
        /// returns "c:/winnt/system32".
        /// </summary>
        /// <param name="path">The path to convert.</param>
        /// <returns>Returns path using '/' as file separator.</returns>
        public static string FromNativeSeparators(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Converts all alternate directory separators characters of the path to the native
        /// directory separator. For instance, ToNativeSeparators("c:/winnt/system32")
        /// returns "c:\\winnt\\system32".
        /// </summary>
        /// <param name="path">The path to convert.</param>
        /// <returns>Returns path using '\' as file separator.</returns>
        public static string ToNativeSeparator(string path)
        {
            return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        public static bool IsInFilter(VCFile vcfile, FakeFilter filter)
        {
            var item = (VCProjectItem)vcfile;

            while (item is { Parent: {}, Kind: not "VCProject" }) {
                item = (VCProjectItem)item.Parent;

                if (item.Kind == "VCFilter") {
                    var f = (VCFilter)item;
                    if (f.UniqueIdentifier != null
                        && f.UniqueIdentifier.ToLower() == filter.UniqueIdentifier.ToLower())
                        return true;
                }
            }
            return false;
        }

        // returns true if some exception occurs
        public static bool IsGenerated(VCFile vcfile)
        {
            try {
                return IsInFilter(vcfile, Filters.GeneratedFiles());
            } catch (Exception e) {
                MessageBox.Show(e.ToString());
                return true;
            }
        }

        // returns false if some exception occurs
        public static bool IsResource(VCFile vcfile)
        {
            try {
                return IsInFilter(vcfile, Filters.ResourceFiles());
            } catch (Exception) {
                return false;
            }
        }
        public static List<string> GetProjectFiles(Project pro, FilesToList filter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return GetProjectFiles(pro?.Object as VCProject, filter);
        }

        public static List<string> GetProjectFiles(QtProject qtProject, FilesToList filter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return GetProjectFiles(qtProject?.VcProject, filter);
        }

        public static List<string> GetProjectFiles(VCProject vcPro, FilesToList filter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (vcPro is not { Files: IVCCollection vcFiles })
                return null;

            var configurationName = vcPro.ActiveConfiguration.ConfigurationName;

            var fileList = new List<string>();
            foreach (VCFile vcFile in vcFiles) {
                if (vcFile.ItemName.EndsWith(".vcxproj.filters", StringComparison.Ordinal))
                    continue; // Why are project files also returned?

                var excluded = false;
                var fileConfigurations = (IVCCollection)vcFile.FileConfigurations;
                foreach (VCFileConfiguration config in fileConfigurations) {
                    if (config.ExcludedFromBuild && config.MatchName(configurationName, false)) {
                        excluded = true;
                        break;
                    }
                }

                if (excluded)
                    continue;

                // can be in any filter
                if (IsTranslationFile(vcFile.Name) && filter == FilesToList.FL_Translation)
                    fileList.Add(FromNativeSeparators(vcFile.RelativePath));

                // can also be in any filter
                if (IsWinRCFile(vcFile.Name) && filter == FilesToList.FL_WinResource)
                    fileList.Add(FromNativeSeparators(vcFile.RelativePath));

                if (IsGenerated(vcFile)) {
                    if (filter == FilesToList.FL_Generated)
                        fileList.Add(FromNativeSeparators(vcFile.RelativePath));
                    continue;
                }

                if (IsResource(vcFile)) {
                    if (filter == FilesToList.FL_Resources)
                        fileList.Add(FromNativeSeparators(vcFile.RelativePath));
                    continue;
                }

                switch (filter) {
                case FilesToList.FL_UiFiles: // form files
                    if (IsUicFile(vcFile.Name))
                        fileList.Add(FromNativeSeparators(vcFile.RelativePath));
                    break;
                case FilesToList.FL_HFiles:
                    if (IsHeaderFile(vcFile.Name))
                        fileList.Add(FromNativeSeparators(vcFile.RelativePath));
                    break;
                case FilesToList.FL_CppFiles:
                    if (IsSourceFile(vcFile.Name))
                        fileList.Add(FromNativeSeparators(vcFile.RelativePath));
                    break;
                case FilesToList.FL_QmlFiles:
                    if (IsQmlFile(vcFile.Name))
                        fileList.Add(FromNativeSeparators(vcFile.RelativePath));
                    break;
                }
            }

            return fileList;
        }

        public static Project GetSelectedProject(DTE dteObject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (dteObject == null)
                return null;

            Array prjs = null;
            try {
                prjs = (Array)dteObject.ActiveSolutionProjects;
            } catch {
                // When VS2010 is started from the command line,
                // we may catch a "Unspecified error" here.
            }
            if (prjs is not { Length: >= 1 })
                return null;

            // don't handle multiple selection... use the first one
            if (prjs.GetValue(0) is Project project)
                return project;
            return null;
        }

        /// <summary>
        /// Returns the the current selected Qt Project. If not project is selected
        /// or if the selected project is not a Qt project this function returns null.
        /// </summary>
        public static QtProject GetSelectedQtProject(DTE dteObject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Can happen sometimes shortly after starting VS.
            var projectList = ProjectsInSolution(dteObject);
            if (projectList.Count == 0)
                return null;

            EnvDTE.Project project = null;
            // Grab the first active project.
            if (GetSelectedProject(dteObject) is {} active)
                project = active;

            // Grab the first project out of the list of projects. If there are
            // several projects than there is no way to know which one to select.
            if (projectList.Count == 1 && projectList[0] is {} first)
                project = first;

            // Last try, get the project from an active document.
            if (dteObject?.ActiveDocument?.ProjectItem?.ContainingProject is {} containing)
                project = containing;

            return QtProject.GetOrAdd(project);
        }

        public static VCFile[] GetSelectedFiles(DTE dteObject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (GetSelectedQtProject(dteObject) == null)
                return null;

            if (dteObject.SelectedItems.Count <= 0)
                return null;

            var items = dteObject.SelectedItems;

            var files = new VCFile[items.Count + 1];
            for (var i = 1; i <= items.Count; ++i) {
                var item = items.Item(i);
                if (item.ProjectItem == null)
                    continue;

                VCProjectItem vcitem;
                try {
                    vcitem = (VCProjectItem)item.ProjectItem.Object;
                } catch (Exception) {
                    return null;
                }

                if (vcitem.Kind == "VCFile")
                    files[i - 1] = (VCFile)vcitem;
            }
            files[items.Count] = null;
            return files;
        }

        public static List<Project> ProjectsInSolution(DTE dteObject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (dteObject == null)
                return new List<Project>();

            var projects = new List<Project>();
            var solution = dteObject.Solution;
            if (solution != null) {
                var c = solution.Count;
                for (var i = 1; i <= c; ++i) {
                    try {
                        var prj = solution.Projects.Item(i);
                        if (prj == null)
                            continue;
                        addSubProjects(prj, ref projects);
                    } catch {
                        // Ignore this exception... maybe the next project is ok.
                        // This happens for example for Intel VTune projects.
                    }
                }
            }
            return projects;
        }

        private static void addSubProjects(Project prj, ref List<Project> projects)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // If the actual object of the project is null then the project was probably unloaded.
            if (prj.Object == null)
                return;

            // Is this a Visual C++ project?
            if (prj is { ConfigurationManager: {}, Kind: "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}" })
                projects.Add(prj);
            else // In this case, prj is a solution folder
                addSubProjects(prj.ProjectItems, ref projects);
        }

        private static void addSubProjects(ProjectItems subItems, ref List<Project> projects)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (subItems == null)
                return;

            foreach (ProjectItem item in subItems) {
                Project subprj = null;
                try {
                    subprj = item.SubProject;
                } catch {
                    // The property "SubProject" might not be implemented.
                    // This is the case for Intel Fortran projects. (QTBUG-11567)
                }
                if (subprj != null)
                    addSubProjects(subprj, ref projects);
            }
        }

        /// <summary>
        /// This method copies the specified directory and all its child directories and files to
        /// the specified destination. The destination directory is created if it does not exist.
        /// </summary>
        public static void CopyDirectory(string directory, string targetPath)
        {
            var sourceDir = new DirectoryInfo(directory);
            if (!sourceDir.Exists)
                return;

            try {
                if (!Directory.Exists(targetPath))
                    Directory.CreateDirectory(targetPath);

                var files = sourceDir.GetFiles();
                foreach (var file in files) {
                    try {
                        file.CopyTo(Path.Combine(targetPath, file.Name), true);
                    } catch { }
                }
            } catch { }

            var subDirs = sourceDir.GetDirectories();
            foreach (var subDir in subDirs)
                CopyDirectory(subDir.FullName, Path.Combine(targetPath, subDir.Name));
        }

        static Parser EnvVarParser => StaticLazy.Get(() => EnvVarParser, () =>
        {
            Token tokenName = new Token("name", (~Chars["=\r\n"]).Repeat(atLeast: 1));
            Token tokenValuePart = new Token("value_part", (~Chars[";\r\n"]).Repeat(atLeast: 1));
            Token tokenValue = new Token("value", (tokenValuePart | Chars[';']).Repeat())
            {
                new Rule<List<string>>
                {
                    Capture(_ => new List<string>()),
                    Update("value_part", (List<string> parts, string part) => parts.Add(part))
                }
            };
            Token tokenEnvVar = new Token("env_var", tokenName & "=" & tokenValue & LineBreak)
            {
                new Rule<KeyValuePair<string, List<string>>>
                {
                    Create("name", (string name)
                        => new KeyValuePair<string, List<string>>(name, null)),
                    Transform("value", (KeyValuePair<string, List<string>> prop, List<string> value)
                        => new KeyValuePair<string, List<string>>(prop.Key, value))
                }
            };
            return tokenEnvVar.Render();
        });

        public static string VcPath { get; set; }
        public static bool SetVcVars(VersionInformation versionInfo, ProcessStartInfo startInfo)
        {
            var vm = QtVersionManager.The();
            versionInfo ??= vm.GetVersionInfo(vm.GetDefaultVersion());

            if (string.IsNullOrEmpty(VcPath))
                return false;

            // Select vcvars script according to host and target platforms
            var osIs64Bit = Environment.Is64BitOperatingSystem;
            var vcVarsCmd = "";
            switch (versionInfo.platform()) {
            case Platform.x86:
                vcVarsCmd = Path.Combine(VcPath, osIs64Bit
                        ? @"Auxiliary\Build\vcvarsamd64_x86.bat"
                        : @"Auxiliary\Build\vcvars32.bat");
                break;
            case Platform.x64:
                vcVarsCmd = Path.Combine(VcPath, osIs64Bit
                        ? @"Auxiliary\Build\vcvars64.bat"
                        : @"Auxiliary\Build\vcvarsx86_amd64.bat");
                break;
            case Platform.arm64:
                vcVarsCmd = Path.Combine(VcPath, osIs64Bit
                        ? @"Auxiliary\Build\vcvarsamd64_arm64.bat"
                        : @"Auxiliary\Build\vcvarsx86_arm64.bat");
                if (!File.Exists(vcVarsCmd)) {
                    vcVarsCmd = Path.Combine(VcPath, osIs64Bit
                            ? @"Auxiliary\Build\vcvars64.bat"
                            : @"Auxiliary\Build\vcvarsx86_amd64.bat");
                }
                break;
            }

            if (!File.Exists(vcVarsCmd)) {
                Messages.Print(">>> vcvars: NOT FOUND");
                return false;
            }

            // Run vcvars and print environment variables
            var stdOut = new StringBuilder();
            var command = $"/c \"{vcVarsCmd}\" && set";
            var comspecPath = Environment.GetEnvironmentVariable("COMSPEC");
            var vcVarsStartInfo = new ProcessStartInfo(comspecPath, command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var process = Process.Start(vcVarsStartInfo);
            if (process == null)
                return false;

            var vcVarsProcId = process.Id;
            Messages.Print($"--- vcvars({vcVarsProcId}): {vcVarsCmd}");
            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;
                var data = e.Data.TrimEnd('\r', '\n');
                if (!string.IsNullOrEmpty(data))
                    stdOut.Append($"{data}\r\n");
            };
            process.BeginOutputReadLine();
            process.WaitForExit();
            var ok = process.ExitCode == 0;
            process.Close();
            if (!ok)
                return false;

            // Parse command output: copy environment variables to startInfo
            var envVars = EnvVarParser.Parse(stdOut.ToString())
                .GetValues<KeyValuePair<string, List<string>>>("env_var")
                .ToDictionary(envVar => envVar.Key, envVar => envVar.Value ?? new(), CaseIgnorer);
            foreach (var vcVar in envVars)
                startInfo.Environment[vcVar.Key] = string.Join(";", vcVar.Value);

            // Warn if cl.exe is not in PATH
            var clPath = envVars["PATH"]
                .Select(path => Path.Combine(path, "cl.exe"))
                .FirstOrDefault(File.Exists);
            if (!string.IsNullOrEmpty(clPath))
                Messages.Print($"--- vcvars({vcVarsProcId}): cl path: {clPath}");
            else
                Messages.Print($">>> vcvars({vcVarsProcId}): cl path NOT FOUND");

            return true;
        }

        /// <summary>
        /// Rooted canonical path is the absolute path for the specified path string
        /// (cf. Path.GetFullPath()) without a trailing path separator.
        /// </summary>
        static string RootedCanonicalPath(string path)
        {
            try {
                return Path
                .GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            } catch {
                return "";
            }
        }

        /// <summary>
        /// If the given path is relative and a sub-path of the current directory, returns
        /// a "relative canonical path", containing only the steps beyond the current directory.
        /// Otherwise, returns the absolute ("rooted") canonical path.
        /// </summary>
        public static string CanonicalPath(string path)
        {
            string canonicalPath = RootedCanonicalPath(path);
            if (!Path.IsPathRooted(path)) {
                string currentCanonical = RootedCanonicalPath(".");
                if (canonicalPath.StartsWith(currentCanonical, IgnoreCase)) {
                    return canonicalPath
                    .Substring(currentCanonical.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                return canonicalPath;
            }
            return canonicalPath;
        }

        public static string Unquote(string text)
        {
            text = text.Trim();
            if (string.IsNullOrEmpty(text)
                || text.Length < 3
                || !text.StartsWith("\"")
                || !text.EndsWith("\"")) {
                return text;
            }
            return text.Substring(1, text.Length - 2);
        }

        public static string NewProjectGuid()
        {
            return $"{{{Guid.NewGuid().ToString().ToUpper()}}}";
        }

        public static string SafePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            path = path.Replace("\"", "");
            if (!path.Contains(' '))
                return path;
            if (path.EndsWith("\\"))
                path += Path.DirectorySeparatorChar;
            return $"\"{path}\"";
        }
    }
}
