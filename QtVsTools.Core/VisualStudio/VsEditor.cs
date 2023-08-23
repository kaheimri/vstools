/***************************************************************************************************
Copyright (C) 2023 The Qt Company Ltd.
SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only
***************************************************************************************************/

using Microsoft.VisualStudio.Shell;
using EnvDTE;

namespace QtVsTools.VisualStudio
{
    using static Common.EnumExt;

    public static class VsEditor
    {
        public enum OpenWith
        {
            [String(Constants.vsViewKindPrimary)] DefaultEditor,
            [String(Constants.vsViewKindAny)] LastEditor,
            [String(Constants.vsViewKindCode)] CodeEditor,
            [String(Constants.vsViewKindTextView)] TextEditor,
            [String(Constants.vsViewKindDebugging)] Debugger,
            [String(Constants.vsViewKindDesigner)] FormsDesigner
        }

        public static void Open(string path, OpenWith editor = OpenWith.DefaultEditor)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (VsServiceProvider.GetService<DTE>() is not { } dte)
                return;
            dte.ItemOperations.OpenFile(path, editor.Cast<string>());
        }

        private const string DiffFilesGuid = "{5D4C0442-C0A2-4BE8-9B4D-AB1C28450942}";
        private const int DiffFilesId = 256;

        public static void Diff(string leftPath, string rightPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (VsServiceProvider.GetService<DTE>() is not { } dte)
                return;
            var diffCustomArgs = $"\"{leftPath}\" \"{rightPath}\"" as object;
            dte.Commands.Raise(DiffFilesGuid, DiffFilesId, ref diffCustomArgs, ref diffCustomArgs);
        }
    }
}