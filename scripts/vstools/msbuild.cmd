::**************************************************************************************************
::Copyright (C) 2023 The Qt Company Ltd.
::SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only
::**************************************************************************************************

::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::msbuild.cmd
:: * Solution main build
::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

IF "%BUILD_PLATFORM%" == "" (
    SET BUILD_PLATFORM=%VSCMD_ARG_TGT_ARCH%
)

ECHO.
%##########################%
%##% msbuild: vstools.sln
%##% msbuild: -t:%MSBUILD_TARGETS%
%##% msbuild: -p:Configuration=%BUILD_CONFIGURATION%
%##% msbuild: -p:Platform=%BUILD_PLATFORM%
IF %VERBOSE% (
    %##% msbuild: -p:TransformOutOfDateOnly=%TRANSFORM_INCREMENTAL%
    %##% msbuild: -verbosity:%MSBUILD_VERBOSITY%
    %##% msbuild extras: %MSBUILD_EXTRAS%
)
%##########################%
msbuild ^
    -nologo ^
    -verbosity:%MSBUILD_VERBOSITY% ^
    -maxCpuCount ^
    -p:Configuration=%BUILD_CONFIGURATION% ^
    -p:Platform=%BUILD_PLATFORM% ^
    -p:TransformOutOfDateOnly=%TRANSFORM_INCREMENTAL% ^
    -t:%MSBUILD_TARGETS% ^
    %MSBUILD_EXTRAS% ^
    vstools.sln

IF %ERRORLEVEL% NEQ 0 (
    CALL %SCRIPTLIB%\error.cmd %ERRORLEVEL% "ERROR building solution"
    EXIT /B %ERRORLEVEL%
)

CALL %SCRIPTLIB%\info.cmd "version"
%##% Solution build successful.
%##########################%
