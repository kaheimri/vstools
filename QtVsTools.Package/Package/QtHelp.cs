/***************************************************************************************************
 Copyright (C) 2024 The Qt Company Ltd.
 SPDX-License-Identifier: LicenseRef-Qt-Commercial OR GPL-3.0-only WITH Qt-GPL-exception-1.0
***************************************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace QtVsTools
{
    using Core;
    using Core.Options;
    using VisualStudio;
    using static Core.Options.QtOptionsPage.SourcePreference;

    public class QtHelp
    {
        private static QtHelp Instance
        {
            get;
            set;
        }

        public static void Initialize()
        {
            Instance = new QtHelp();
        }

        private QtHelp()
        {
            var commandService = VsServiceProvider
                .GetService<IMenuCommandService, OleMenuCommandService>();
            commandService?.AddCommand(new MenuCommand(F1QtHelpEventHandler,
                new CommandID(QtMenus.Package.Guid, QtMenus.Package.F1QtHelp)));
        }

        private static bool IsSuperfluousCharacter(string text)
        {
            switch (text) {
            case " ":
            case ";":
            case ".":
            case "<":
            case ">":
            case "{":
            case "}":
            case "(":
            case ")":
            case ":":
            case ",":
            case "/":
            case "\\":
            case "^":
            case "%":
            case "+":
            case "-":
            case "*":
            case "\t":
            case "&":
            case "\"":
            case "!":
            case "[":
            case "]":
            case "|":
            case "'":
            case "~":
            case "#":
            case "=":
                return true; // nothing we are interested in
            }
            return false;
        }

        private static string GetString(DbDataReader reader, int index)
        {
            return reader.IsDBNull(index) ? "" : reader.GetString(index);
        }

        private static void F1QtHelpEventHandler(object sender, EventArgs args)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!ShowEditorContextHelp()) {
                Messages.Print("No help match was found. You can still try to search online at "
                    + "https://doc.qt.io" + ".", false, true);
            }
        }

        public static bool ShowEditorContextHelp()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try {
                var dte = VsServiceProvider.GetService<SDTE, DTE>();
                if (dte?.ActiveDocument?.Object() is not TextDocument objTextDocument)
                    return false;

                string keyword;
                var selection = objTextDocument.Selection;
                if (selection.IsEmpty) { // no selection inside the document
                    var line = selection.ActivePoint.Line; // current line
                    var offset = selection.ActivePoint.LineCharOffset; // current char offset

                    selection.CharLeft(true); // try the character before the cursor
                    if (!selection.IsEmpty) {
                        keyword = selection.Text; // something in front of the cursor
                        selection.CharRight(true); // reset to origin
                        if (!IsSuperfluousCharacter(keyword)) {
                            // move the selection to the start of the word
                            selection.WordLeft(true);
                            selection.MoveToPoint(selection.TopPoint);
                        }
                    }
                    selection.WordRight(true);  // select the word
                    keyword = selection.Text;  // get the selected text
                    selection.MoveToLineAndOffset(line, offset); // reset
                } else {
                    keyword = selection.Text;
                }

                keyword = keyword.Trim();
                if (keyword.Length <= 1 || IsSuperfluousCharacter(keyword))
                    return false; // suppress single character, operators etc...

                var qtVersion = "$(DefaultQtVersion)";
                if (HelperFunctions.GetSelectedQtProject(dte) is {} project)
                    qtVersion = project.QtVersion;

                var info = VersionInformation.GetOrAddByName(qtVersion);
                var docPath = info?.QtInstallDocs;
                if (string.IsNullOrEmpty(docPath) || !Directory.Exists(docPath))
                    return false;

                var qchFiles = Directory.GetFiles(docPath, "*?.qch");
                if (qchFiles.Length == 0)
                    return TryShowGenericSearchResultsOnline(keyword, info.Major);

                var linksForKeyword = "SELECT d.Title, f.Name, e.Name, "
                    + "d.Name, a.Anchor FROM IndexTable a, FileNameTable d, FolderTable e, "
                    + "NamespaceTable f WHERE a.FileId=d.FileId AND d.FolderId=e.Id AND "
                    + $"a.NamespaceId=f.Id AND a.Name='{keyword}'";

                var links = new Dictionary<string, string>();
                var builder = new SQLiteConnectionStringBuilder
                {
                    ReadOnly = true
                };
                foreach (var file in qchFiles) {
                    builder.DataSource = file;
                    using var connection = new SQLiteConnection(builder.ToString());
                    connection.Open();
                    using var command = new SQLiteCommand(linksForKeyword, connection);
                    var reader = QtVsToolsPackage.Instance.JoinableTaskFactory
                        .Run(async () => await command.ExecuteReaderAsync());
                    using (reader) {
                        while (reader.Read()) {
                            var title = GetString(reader, 0);
                            if (string.IsNullOrWhiteSpace(title))
                                title = keyword + ':' + GetString(reader, 3);
                            string path;
                            if (QtOptionsPage.HelpPreference == Offline) {
                                path = "file:///" + Path.Combine(docPath,
                                    GetString(reader, 2), GetString(reader, 3));
                            } else {
                                path = "https://" + Path.Combine("doc.qt.io",
                                    $"qt-{info.Major}", GetString(reader, 3));
                            }
                            if (!string.IsNullOrWhiteSpace(GetString(reader, 4)))
                                path += "#" + GetString(reader, 4);
                            links.Add(title, path);
                        }
                    }
                }

                string uri;
                switch (links.Values.Count) {
                case 0:
                    return TryShowGenericSearchResultsOnline(keyword, info.Major);
                case 1:
                    uri = links.First().Value;
                    break;
                default:
                    var dialog = new QtHelpLinkChooser
                    {
                        Links = links,
                        Keyword = keyword,
                        ShowInTaskbar = false
                    };
                    if (!dialog.ShowModal().GetValueOrDefault())
                        return false;
                    uri = dialog.Link;
                    break;
                }

                uri = HelperFunctions.FromNativeSeparators(uri);
                var helpUri = new Uri(uri);
                if (helpUri.IsFile && !File.Exists(helpUri.LocalPath)) {
                    VsShellUtilities.ShowMessageBox(QtVsToolsPackage.Instance,
                        "Your search - " + keyword + " - did match a document, but it could "
                        + "not be found on disk. To use the online help, select: "
                        + "Tools | Options | Qt | Preferred source | Online",
                        string.Empty, OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                } else {
                    VsShellUtilities.OpenSystemBrowser(uri);
                }
            } catch (Exception exception) {
                exception.Log();
            }
            return true;
        }

        private static bool TryShowGenericSearchResultsOnline(string keyword, uint version)
        {
            if (QtOptionsPage.HelpPreference != Online)
                return false;

            VsShellUtilities.OpenSystemBrowser(HelperFunctions.FromNativeSeparators(
                new UriBuilder($"https://doc.qt.io/qt-{version}/search-results.html")
                {
                    Query = "q=" + keyword
                }.ToString())
            );
            return true;
        }
    }
}
