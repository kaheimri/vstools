####################################################################################################
# Copyright (C) 2024 The Qt Company Ltd.
# SPDX-License-Identifier: LicenseRef-Qt-Commercial OR GPL-3.0-only WITH Qt-GPL-exception-1.0
####################################################################################################

# -*- coding: utf-8 -*-

source("../../shared/utils.py")

import builtins
import subprocess

# This script does not actually test anything. It only resets the experimental environment the
# tests are running in so you can start from scratch. After running the script, the environment
# will not contain any user settings. Only nagsreens from first start will already be handled.


def main():
    # Start MSVS to determine its version and instanceID, then close it immediately.
    startApp(True, False)
    vsVersionNr = {"2019":"16.0", "2022":"17.0"}[getMsvsProductLine()]
    vsInstance = "_".join([vsVersionNr, getAppProperty("instanceID")])
    installationPath = getAppProperty("installationPath")
    closeMainWindow()
    # Wait for MSVS to shut down
    waitFor(lambda: not currentApplicationContext().isRunning)
    snooze(2)
    # Reset the experimental environment
    subprocess.check_output('"%s/VSSDK/VisualStudioIntegration/Tools/Bin/'
                            'CreateExpInstance.exe" /Reset /VSInstance=%s /RootSuffix=%s'
                            % (installationPath, vsInstance, rootSuffix))
    # Start MSVS again to click away the nagscreens shown on first start
    startApp(True, False)
    closeMainWindow()
