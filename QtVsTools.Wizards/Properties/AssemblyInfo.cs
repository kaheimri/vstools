/***************************************************************************************************
 Copyright (C) 2023 The Qt Company Ltd.
 SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only
****************************************************************************************************
<#@output extension="tt.cs" #>
<#@include file="$(SolutionDir)\version.tt" #>
**              <#=WARNING_GENERATED_FILE#>
****************************************************************************/

using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Qt Project Wizard")]
[assembly: AssemblyDescription("The Qt Visual Studio Tools allow developers to use the standard development environment without having to worry about any Qt-related build steps or tools.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("The Qt Company Ltd.")]
[assembly: AssemblyProduct("Qt Visual Studio Tools")]
[assembly: AssemblyCopyright("Copyright (C) 2016-2017 The Qt Company Ltd.")]
[assembly: AssemblyTrademark("The Qt Company Ltd. Qt and their respective logos are trademarks of The Qt Company Ltd. in Finland and/or other countries worldwide. All other trademarks are property of their respective owners.")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("6ac58c89-d2cc-4b96-9906-e6c66fe2eb54")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("<#=QT_VS_TOOLS_VERSION_ASSEMBLY#>")]
[assembly: AssemblyFileVersion("<#=QT_VS_TOOLS_VERSION_ASSEMBLY_FILE#>")]
