/***************************************************************************************************
 Copyright (C) 2023 The Qt Company Ltd.
 SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only
***************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace QtVsTools.Core
{
    public class QMakeImport
    {
        private class QMakeProcess : QMake
        {
            private bool setVcVars;
            private readonly VersionInformation qtVersion;

            public QMakeProcess(VersionInformation qtVersion, EnvDTE.DTE dte)
                : base(qtVersion.qtDir, dte)
            {
                this.qtVersion = qtVersion;
            }

            protected override void InfoStart(Process qmakeProc)
            {
                base.InfoStart(qmakeProc);
                InfoMsg("--- qmake: Working Directory: " + qmakeProc.StartInfo.WorkingDirectory);
                InfoMsg("--- qmake: Arguments: " + qmakeProc.StartInfo.Arguments);
                if (qmakeProc.StartInfo.EnvironmentVariables.ContainsKey("QMAKESPEC")) {
                    var qmakeSpec = qmakeProc.StartInfo.EnvironmentVariables["QMAKESPEC"];
                    if (qmakeSpec != qtVersion.QMakeSpecDirectory) {
                        InfoMsg("--- qmake: Environment "
                          + "variable QMAKESPEC overwriting Qt version QMAKESPEC.");
                        InfoMsg($"--- qmake: Qt version QMAKESPEC: {qtVersion.QMakeSpecDirectory}");
                        InfoMsg($"--- qmake: Environment variable QMAKESPEC: {qmakeSpec}");
                    }
                }
            }

            public int Run(bool setVCVars)
            {
                setVcVars = setVCVars;
                return Run();
            }

            public override int Run()
            {
                int exitCode = -1;
                using (var qmakeProc = CreateProcess()) {
                    try {
                        if (setVcVars) {
                            if (qtVersion is null)
                                OutMsg("Error setting VC vars, Qt version may not be null");
                            if (!HelperFunctions.SetVCVars(qtVersion, qmakeProc.StartInfo))
                                OutMsg("Error setting VC vars");
                        }
                        if (qmakeProc.Start()) {
                            InfoStart(qmakeProc);
                            qmakeProc.BeginOutputReadLine();
                            qmakeProc.BeginErrorReadLine();
                            qmakeProc.WaitForExit();
                            exitCode = qmakeProc.ExitCode;
                            InfoExit(qmakeProc);
                        }
                    } catch (Exception exception) {
                        exception.Log();
                    }
                }
                return exitCode;
            }
        }
        private readonly QMakeProcess qmake;

        public QMakeImport(VersionInformation qtVersion,
            string proFilePath,
            bool recursiveRun = false,
            bool disableWarnings = false,
            EnvDTE.DTE dte = null)
        {
            Debug.Assert(qtVersion != null);

            qmake = new QMakeProcess(qtVersion, dte) {
                ProFile = proFilePath,
                TemplatePrefix = "vc",
                Recursive = recursiveRun,
                DisableWarnings = disableWarnings,
                OutputFile = recursiveRun ? null : Path.ChangeExtension(proFilePath, ".vcxproj"),
                Vars = new Dictionary<string, string> {
                    {"QMAKE_INCDIR_QT", @"$(QTDIR)\include"},
                    {"QMAKE_LIBDIR", @"$(QTDIR)\lib"},
                    {"QMAKE_MOC", @"$(QTDIR)\bin\moc.exe"},
                    {"QMAKE_QMAKE", @"$(QTDIR)\bin\qmake.exe"}
                }
            };
        }

        public int Run(bool setVCVars = false) => qmake.Run(setVCVars);
    }
}
