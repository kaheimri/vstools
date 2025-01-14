/***************************************************************************************************
 Copyright (C) 2024 The Qt Company Ltd.
 SPDX-License-Identifier: LicenseRef-Qt-Commercial OR GPL-3.0-only WITH Qt-GPL-exception-1.0
***************************************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace QtVsTools.Core
{
    using Common;

    public class QtModules
    {
        public static QtModules Instance { get; } = new();

        private readonly IReadOnlyCollection<QtModule> qt5Modules;
        private readonly IReadOnlyCollection<QtModule> qt6Modules;

        public IEnumerable<QtModule> GetAvailableModules(uint major)
        {
            return major switch
            {
                < 6 => qt5Modules,
                6 => qt6Modules,
                _ => throw new ArgumentException("Unsupported Qt version.")
            };
        }

        private QtModules()
        {
            qt5Modules = FillModules("qtmodules.xml", "5");
            qt6Modules = FillModules("qt6modules.xml", "6");
        }

        private static IReadOnlyCollection<QtModule>FillModules(string modulesFile, string major)
        {
            var list = new List<QtModule>();
            var modulesFilePath = Path.Combine(Utils.PackageInstallPath, modulesFile);
            if (!File.Exists(modulesFilePath))
                return list;

            var xmlText = File.ReadAllText(modulesFilePath, Encoding.UTF8);
            XDocument xml = null;
            try {
                using var reader = XmlReader.Create(new StringReader(xmlText));
                xml = XDocument.Load(reader);
            } catch (Exception exception) {
                exception.Log();
            }

            if (xml == null)
                return list;

            foreach (var xModule in xml.Elements("QtVsTools").Elements("Module")) {
                var module = new QtModule(major)
                {
                    Name = (string)xModule.Element("Name"),
                    Selectable = (string)xModule.Element("Selectable") == "true",
                    LibraryPrefix = (string)xModule.Element("LibraryPrefix"),
                    proVarQT = (string)xModule.Element("proVarQT"),
                    IncludePath = xModule.Elements("IncludePath").Select(x => x.Value).ToList(),
                    Defines = xModule.Elements("Defines").Select(x => x.Value).ToList(),
                    AdditionalLibraries = xModule.Elements("AdditionalLibraries")
                        .Select(x => x.Value).ToList(),
                    AdditionalLibrariesDebug = xModule.Elements("AdditionalLibrariesDebug")
                        .Select(x => x.Value).ToList()
                };
                if (string.IsNullOrEmpty(module.Name) || string.IsNullOrEmpty(module.LibraryPrefix)) {
                    Messages.Print($"\r\nCritical error: incorrect format of {modulesFile}");
                    throw new FormatException($"Critical error: incorrect format of {modulesFile}");
                }
                list.Add(module);
            }

            return list;
        }
    }
}
