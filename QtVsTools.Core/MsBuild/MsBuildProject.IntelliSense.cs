/***************************************************************************************************
 Copyright (C) 2023 The Qt Company Ltd.
 SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only
***************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.Threading;

using Task = System.Threading.Tasks.Task;
using Thread = System.Threading.Thread;

namespace QtVsTools.Core.MsBuild
{
    using Core;
    using Options;

    public partial class MsBuildProject
    {
        public void Refresh(
            string configurationName = null,
            IEnumerable<string> selectedFiles = null)
        {
            _ = Task.Run(() => RefreshAsync(configurationName, selectedFiles));
        }

        public async Task RefreshAsync(
            string configurationName = null,
            IEnumerable<string> selectedFiles = null,
            bool refreshQtVars = false)
        {
            if (Options.Get() is not { ProjectTracking: true } options)
                return;
            if (options is { BuildDebugInformation: true }) {
                Messages.Print($"{DateTime.Now:HH:mm:ss.FFF} "
                    + $"QtProjectIntellisense({Thread.CurrentThread.ManagedThreadId}): "
                    + $"Refreshing: [{configurationName ?? "(all configs)"}] {VcProjectPath}");
            }

            await Initialized;

            var properties = new Dictionary<string, string>
            {
                ["QtVSToolsBuild"] = "true"
            };
            if (selectedFiles != null)
                properties["SelectedFiles"] = string.Join(";", selectedFiles);
            var targets = new List<string> { "QtVars" };
            if (Options.Get() is { BuildRunQtTools: true })
                targets.Add("Qt");

            var configurationNames = Enumerable.Empty<string>();
            if (string.IsNullOrEmpty(configurationName)) {
                if (UnconfiguredProject.Services.ProjectConfigurationsService is {} service) {
                    configurationNames = (await service.GetKnownProjectConfigurationsAsync())
                        .Select(configuration => configuration.Name);
                }
            } else {
                configurationNames = new[] { configurationName };
            }

            foreach (var name in configurationNames) {
                if (refreshQtVars)
                    await StartBuildAsync(name, properties, targets, LoggerVerbosity.Quiet);
                else
                    await SetOutdatedAsync(name);
            }
        }
    }
}
