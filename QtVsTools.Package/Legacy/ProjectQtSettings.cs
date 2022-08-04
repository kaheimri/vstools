/****************************************************************************
**
** Copyright (C) 2022 The Qt Company Ltd.
** Contact: https://www.qt.io/licensing/
**
** This file is part of the Qt VS Tools.
**
** $QT_BEGIN_LICENSE:GPL-EXCEPT$
** Commercial License Usage
** Licensees holding valid commercial Qt licenses may use this file in
** accordance with the commercial license agreement provided with the
** Software or, alternatively, in accordance with the terms contained in
** a written agreement between you and The Qt Company. For licensing terms
** and conditions see https://www.qt.io/terms-conditions. For further
** information use the contact form at https://www.qt.io/contact-us.
**
** GNU General Public License Usage
** Alternatively, this file may be used under the terms of the GNU
** General Public License version 3 as published by the Free Software
** Foundation with exceptions as appearing in the file LICENSE.GPL3-EXCEPT
** included in the packaging of this file. Please review the following
** information to ensure the GNU General Public License requirements will
** be met: https://www.gnu.org/licenses/gpl-3.0.html.
**
** $QT_END_LICENSE$
**
****************************************************************************/

using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace QtVsTools.Legacy
{
    using Core;
    using Legacy = Core.Legacy;

    public class ProjectQtSettings
    {
        public ProjectQtSettings(EnvDTE.Project proj)
        {
            versionManager = QtVersionManager.The();
            project = proj;
            newMocDir = oldMocDir = Legacy.QtVSIPSettings.GetMocDirectory(project);
            newMocOptions = oldMocOptions = Legacy.QtVSIPSettings.GetMocOptions(project);
            newRccDir = oldRccDir = Legacy.QtVSIPSettings.GetRccDirectory(project);
            newUicDir = oldUicDir = Legacy.QtVSIPSettings.GetUicDirectory(project);
            newLUpdateOnBuild = oldLUpdateOnBuild = Legacy.QtVSIPSettings.GetLUpdateOnBuild(project);
            newLUpdateOptions = oldLUpdateOptions = Legacy.QtVSIPSettings.GetLUpdateOptions(project);
            newLReleaseOptions = oldLReleaseOptions = Legacy.QtVSIPSettings.GetLReleaseOptions(project);
            newQtVersion = oldQtVersion = versionManager.GetProjectQtVersion(project);
            QmlDebug = oldQmlDebug = Legacy.QtVSIPSettings.GetQmlDebug(project);
        }

        private readonly QtVersionManager versionManager;
        private EnvDTE.Project project;

        private readonly string oldMocDir;
        private readonly string oldMocOptions;
        private readonly string oldRccDir;
        private readonly string oldUicDir;
        private readonly string oldQtVersion;
        private readonly bool oldLUpdateOnBuild;
        private readonly string oldLUpdateOptions;
        private readonly string oldLReleaseOptions;
        private readonly bool oldQmlDebug;

        private string newMocDir;
        private string newMocOptions;
        private string newRccDir;
        private string newUicDir;
        private string newQtVersion;
        private bool newLUpdateOnBuild;
        private string newLUpdateOptions;
        private string newLReleaseOptions;

        public void SaveSettings()
        {
            var updateMoc = false;
            var qtPro = QtProject.Create(project);

            if (oldMocDir != newMocDir) {
                Legacy.QtVSIPSettings.SaveMocDirectory(project, newMocDir);
                updateMoc = true;
            }
            if (oldMocOptions != newMocOptions) {
                Legacy.QtVSIPSettings.SaveMocOptions(project, newMocOptions);
                updateMoc = true;
            }
            if (updateMoc)
                qtPro.UpdateMocSteps(oldMocDir);

            if (oldUicDir != newUicDir) {
                Legacy.QtVSIPSettings.SaveUicDirectory(project, newUicDir);
                qtPro.UpdateUicSteps(oldUicDir, true);
            }

            if (oldRccDir != newRccDir) {
                Legacy.QtVSIPSettings.SaveRccDirectory(project, newRccDir);
                qtPro.RefreshRccSteps(oldRccDir);
            }

            if (oldLUpdateOnBuild != newLUpdateOnBuild)
                Legacy.QtVSIPSettings.SaveLUpdateOnBuild(project, newLUpdateOnBuild);

            if (oldLUpdateOptions != newLUpdateOptions)
                Legacy.QtVSIPSettings.SaveLUpdateOptions(project, newLUpdateOptions);

            if (oldLReleaseOptions != newLReleaseOptions)
                Legacy.QtVSIPSettings.SaveLReleaseOptions(project, newLReleaseOptions);

            if (oldQmlDebug != QmlDebug)
                Legacy.QtVSIPSettings.SaveQmlDebug(project, QmlDebug);

            if (oldQtVersion != newQtVersion) {
                if (Legacy.QtProject.PromptChangeQtVersion(project, oldQtVersion, newQtVersion)) {
                    var newProjectCreated = false;
                    var versionChanged = qtPro.ChangeQtVersion(
                        oldQtVersion, newQtVersion, ref newProjectCreated);
                    if (versionChanged && newProjectCreated)
                        project = qtPro.Project;
                }
            }
        }

        public string MocDirectory
        {
            get
            {
                return newMocDir;
            }
            set
            {
                var tmp = HelperFunctions.NormalizeRelativeFilePath(value);
                if (tmp.Equals(oldMocDir, StringComparison.OrdinalIgnoreCase))
                    return;

                string badMacros = IncompatibleMacros(tmp);
                if (!string.IsNullOrEmpty(badMacros))
                    Messages.DisplayErrorMessage(SR.GetString("IncompatibleMacros", badMacros));
                else
                    newMocDir = tmp;
            }
        }

        public string MocOptions
        {
            get
            {
                return newMocOptions;
            }

            set
            {
                newMocOptions = value;
            }
        }

        public string UicDirectory
        {
            get
            {
                return newUicDir;
            }
            set
            {
                var tmp = HelperFunctions.NormalizeRelativeFilePath(value);
                if (tmp.Equals(oldUicDir, StringComparison.OrdinalIgnoreCase))
                    return;

                string badMacros = IncompatibleMacros(tmp);
                if (!string.IsNullOrEmpty(badMacros))
                    Messages.DisplayErrorMessage(SR.GetString("IncompatibleMacros", badMacros));
                else
                    newUicDir = tmp;
            }
        }

        public string RccDirectory
        {
            get
            {
                return newRccDir;
            }
            set
            {
                var tmp = HelperFunctions.NormalizeRelativeFilePath(value);
                if (tmp.Equals(oldRccDir, StringComparison.OrdinalIgnoreCase))
                    return;

                string badMacros = IncompatibleMacros(tmp);
                if (!string.IsNullOrEmpty(badMacros))
                    Messages.DisplayErrorMessage(SR.GetString("IncompatibleMacros", badMacros));
                else
                    newRccDir = tmp;
            }
        }

        public bool lupdateOnBuild
        {
            get
            {
                return newLUpdateOnBuild;
            }

            set
            {
                newLUpdateOnBuild = value;
            }
        }

        public string LUpdateOptions
        {
            get
            {
                return newLUpdateOptions;
            }

            set
            {
                newLUpdateOptions = value;
            }
        }

        public string LReleaseOptions
        {
            get
            {
                return newLReleaseOptions;
            }

            set
            {
                newLReleaseOptions = value;
            }
        }

        [DisplayName("QML Debug")]
        [TypeConverter(typeof(QmlDebugConverter))]
        private bool QmlDebug { get; }

        private static string IncompatibleMacros(string stringToExpand)
        {
            string incompatibleMacros = "";
            foreach (Match metaNameMatch in Regex.Matches(stringToExpand, @"\%\(([^\)]+)\)")) {
                string metaName = metaNameMatch.Groups[1].Value;
                if (!incompatibleMacros.Contains(string.Format("%({0})", metaName))) {
                    switch (metaName) {
                    case "RecursiveDir":
                    case "ModifiedTime":
                    case "CreatedTime":
                    case "AccessedTime":
                        if (!string.IsNullOrEmpty(incompatibleMacros))
                            incompatibleMacros += ", ";
                        incompatibleMacros += string.Format("%({0})", metaName);
                        break;
                    }
                }
            }
            return incompatibleMacros;
        }

        internal class QmlDebugConverter : BooleanConverter
        {
            public override object ConvertTo(
                ITypeDescriptorContext context,
                CultureInfo culture,
                object value,
                Type destinationType)
            {
                return (bool)value ? "Enabled" : "Disabled";
            }

            public override object ConvertFrom(
                ITypeDescriptorContext context,
                CultureInfo culture,
                object value)
            {
                return (string)value == "Enabled";
            }
        }

        [TypeConverter(typeof(VersionConverter))]
        public string Version
        {
            get
            {
                return newQtVersion;
            }
            set
            {
                newQtVersion = value;
            }
        }

        internal class VersionConverter : StringConverter
        {
            private readonly QtVersionManager versionManager;

            public VersionConverter()
            {
                versionManager = QtVersionManager.The();
            }

            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                return true;
            }

            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                var versions = versionManager.GetVersions();
                Array.Resize(ref versions, versions.Length + 1);
                versions[versions.Length - 1] = "$(DefaultQtVersion)";
                return new StandardValuesCollection(versions);
            }

            public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
            {
                return true;
            }
        }
    }
}
