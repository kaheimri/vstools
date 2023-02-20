/***************************************************************************************************
 Copyright (C) 2023 The Qt Company Ltd.
 SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only
***************************************************************************************************/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QtVsTools.Editors
{
    [Guid(GuidString)]
    public class QtLinguist : Editor
    {
        public const string GuidString = "4A1333DC-5C94-4F14-A7BF-DC3D96092234";
        public const string Title = "Qt Linguist";

        Guid? _Guid;
        public override Guid Guid => (_Guid ?? (_Guid = new Guid(GuidString))).Value;

        public override string ExecutableName => "linguist.exe";

        public override Func<string, bool> WindowFilter =>
            caption => caption.EndsWith(Title);

        protected override string GetTitle(Process editorProcess)
        {
            return Title;
        }

        protected override bool Detached => QtVsToolsPackage.Instance.Options.LinguistDetached;
    }
}
