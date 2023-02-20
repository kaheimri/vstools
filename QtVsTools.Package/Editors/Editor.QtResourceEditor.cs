/***************************************************************************************************
 Copyright (C) 2023 The Qt Company Ltd.
 SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only
***************************************************************************************************/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace QtVsTools.Editors
{
    [Guid(GuidString)]
    public class QtResourceEditor : Editor
    {
        public const string GuidString = "D0FFB6E6-5829-4DD9-835E-2965449AC6BF";
        public const string Title = "Qt Resource Editor";

        Guid? _Guid;
        public override Guid Guid => (_Guid ?? (_Guid = new Guid(GuidString))).Value;

        public override string ExecutableName => "QrcEditor.exe";

        protected override string GetToolsPath() =>
            QtVsToolsPackage.Instance?.PkgInstallPath;

        public override Func<string, bool> WindowFilter =>
            caption => caption.StartsWith(Title);

        protected override string GetTitle(Process editorProcess) => Title;

        protected override bool Detached => QtVsToolsPackage.Instance.Options.ResourceEditorDetached;
    }
}
