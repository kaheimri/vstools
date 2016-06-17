/****************************************************************************
**
** Copyright (C) 2016 The Qt Company Ltd.
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

using Microsoft.Win32;

namespace QtVsTools
{
    // Base class to support writing default editor values to registry
    public class DefaultEditorsBase
    {
        private const string templatesDir = "TemplatesDir";
        private const string registryBasePath = @"SOFTWARE\Microsoft\VisualStudio\{0}";
        private const string newProjectTemplates = @"\NewProjectTemplates\TemplateDirs\{0}\/1";

        private const string linguist = @"\Default Editors\ts\Qt Linguist";
        private const string designer = @"\Default Editors\ui\Qt Designer";
        private const string qrcEditor = @"\Default Editors\qrc\Qt Resource Editor";

        protected static string addinGuid = null;
        protected static string appWrapper = null;
        protected static string qrcEditorName = null;

        /// <summary>
        /// Write default editor values to registry for VS 2013 if add-in is installed. Applies
        /// both to Qt4 and Qt5 version of the add-in. TODO: Remove if we drop Visual Studio 2013.
        /// </summary>
        public void WriteAddinRegistryValues()
        {
            var basePath = string.Format(registryBasePath, @"12.0");
            var projectTemplates = basePath + string.Format(newProjectTemplates, addinGuid);

            var addinInstallPath = GetAddinInstallPath(GetCUKey(projectTemplates, false));
            if (string.IsNullOrEmpty(addinInstallPath))
                addinInstallPath = GetAddinInstallPath(GetLMKey(projectTemplates, false));
            WriteRegistryValues(basePath, addinInstallPath);
        }

        /// <summary>
        /// Write default editor values to registry for Visual Studio 2013 and above. Uses the VSIX
        /// install path.
        /// </summary>
        public void WriteVsixRegistryValues()
        {
            if (Vsix.Instance.Dte != null) {
                var basePath = string.Format(registryBasePath, Vsix.Instance.Dte.Version)
#if DEBUG
                    + @"Exp"
#endif
                ;
                WriteRegistryValues(basePath, Vsix.Instance.PkgInstallPath);
            }
        }

        // Get add-in installation path using a registry key
        private string GetAddinInstallPath(RegistryKey key)
        {
            if (key == null)
                return null;

            var templatesDirPath = key.GetValue(templatesDir) as string;
            if (string.IsNullOrEmpty(templatesDirPath))
                return null;

            return templatesDirPath.Substring(0, templatesDirPath.IndexOf(@"\projects\"));
        }

        // Get/create registry key under HKCU
        private RegistryKey GetCUKey(string key_path, bool writable)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(key_path, writable);
            if (key == null && writable)
                key = Registry.CurrentUser.CreateSubKey(key_path);
            return key;
        }

        // Get/create registry key under HKLM
        private RegistryKey GetLMKey(string key_path, bool writable)
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(key_path, writable);
            if (key == null && writable)
                key = Registry.LocalMachine.CreateSubKey(key_path);
            return key;
        }

        private void WriteRegistryValues(string basePath, string installPath)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(installPath))
                return;

            var key = GetCUKey(basePath + linguist, true);
            key.SetValue(@"", installPath + @"\" + appWrapper);

            key = GetCUKey(basePath + designer, true);
            key.SetValue(@"", installPath + @"\" + appWrapper);

            key = GetCUKey(basePath + qrcEditor, true);
            key.SetValue(@"", installPath + @"\" + qrcEditorName);
        }
    }
    // Default editor handling for Qt4 add-in
    public class Qt4DefaultEditors : DefaultEditorsBase
    {
        public Qt4DefaultEditors()
        {
            // Set add-in specific values
            addinGuid = @"{6A7385B4-1D62-46e0-A4E3-AED4475371F0}";
            appWrapper = @"qtappwrapper.exe";
            qrcEditorName = @"qrceditor.exe";
        }
    }

    // Default editor handling for Qt5 add-in
    public class Qt5DefaultEditors : DefaultEditorsBase
    {
        public Qt5DefaultEditors()
        {
            // Set add-in specific values
            addinGuid = @"{C80C78C8-F64B-43df-9A53-96F7C44A1EB6}";
            appWrapper = @"qt5appwrapper.exe";
            qrcEditorName = @"q5rceditor.exe";
        }
    }

    // Default editor handling for Qt5 add-in
    public class QtVsToolsDefaultEditors : DefaultEditorsBase
    {
        public QtVsToolsDefaultEditors()
        {
            // Set add-in specific values
            addinGuid = @"{15021976-2F08-4C44-BFF4-73CCDCB50473}";
            appWrapper = @"QtAppWrapper.exe";
            qrcEditorName = @"QrcEditor.exe";
        }
    }
}