﻿/****************************************************************************
**
** Copyright (C) 2019 The Qt Company Ltd.
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
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using Microsoft.CSharp;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

using Task = System.Threading.Tasks.Task;

namespace QtVsTest.Macros
{
    /// <summary>
    /// Macros are snippets of C# code provided by a test client at runtime. They are compiled
    /// on-the-fly and may run once after compilation or stored and reused later by other macros.
    /// Macros may also include special statements in comment lines starting with '//#'. These will
    /// be expanded into the corresponding code ahead of C# compilation.
    /// </summary>
    class Macro
    {
        /// <summary>
        /// Global variable, shared between macros
        /// </summary>
        class GlobalVar
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string InitialValueExpr { get; set; }
            public FieldInfo FieldInfo { get; set; }
            public PropertyInfo InitInfo { get; set; }
        }

        /// <summary>
        /// Reference to Visual Studio SDK service
        /// </summary>
        class VSServiceRef
        {
            public string Name { get; set; }
            public string Interface { get; set; }
            public string Type { get; set; }
            public FieldInfo RefVar { get; set; }
            public Type ServiceType { get; set; }
        }

        /// <summary>
        /// Name of reusable macro
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// True if macro compilation was successful
        /// </summary>
        public bool Ok { get; private set; }

        /// <summary>
        /// Result of macro compilation and execution
        /// </summary>
        public string Result { get; private set; }

        /// <summary>
        /// True if macro will run immediately after compilation
        /// </summary>
        public bool AutoRun { get; private set; }

        /// <summary>
        /// True if Visual Studio should be closed after macro execution
        /// </summary>
        public bool QuitWhenDone { get; private set; }

        AsyncPackage Package { get; set; }
        EnvDTE80.DTE2 Dte { get; set; }

        AutomationElement _UiVsRoot;
        AutomationElement UiVsRoot
        {
            get
            {
                if (_UiVsRoot == null)
                    _UiVsRoot = AutomationElement.FromHandle(new IntPtr(Dte.MainWindow.HWnd));
                return _UiVsRoot;
            }
        }

        JoinableTaskFactory JoinableTaskFactory { get; set; }
        CancellationToken ServerLoop { get; set; }

        string Message { get; set; }

        static MacroParser Parser { get; set; }
        MacroLines MacroLines { get; set; }

        List<string> SelectedAssemblies { get { return _SelectedAssemblies; } }
        List<string> _SelectedAssemblies =
            new List<string>(MSBuild.MetaInfo.QtVsTest.Reference)
            {
                "QtVsTest",
                "System.Core",
            };

        IEnumerable<string> RefAssemblies { get; set; }

        List<string> Namespaces { get { return _Namespaces; } }
        List<string> _Namespaces =
            new List<string>
            {
                "System",
                "System.Linq",
                "System.Reflection",
                "Task = System.Threading.Tasks.Task",
                "System.Windows.Automation",
                "EnvDTE",
                "EnvDTE80",
            };

        Dictionary<string, VSServiceRef> ServiceRefs { get { return _ServiceRefs; } }
        Dictionary<string, VSServiceRef> _ServiceRefs =
            new Dictionary<string, VSServiceRef>
            {
                {
                    "Dte", new VSServiceRef
                    { Name = "Dte", Interface = "DTE2", Type = "DTE" }
                },
            };

        Dictionary<string, GlobalVar> GlobalVars { get { return _GlobalVars; } }
        Dictionary<string, GlobalVar> _GlobalVars =
            new Dictionary<string, GlobalVar>
            {
                {
                    "Result", new GlobalVar
                    { Type = "string", Name = "Result", InitialValueExpr = "string.Empty" }
                },
            };

        string CSharpMethodCode { get; set; }
        string CSharpClassCode { get; set; }

        CompilerResults CompilerResults { get; set; }
        Assembly MacroAssembly { get; set; }
        Type MacroClass { get; set; }
        FieldInfo ResultField { get; set; }
        Func<Task> Run { get; set; }

        const BindingFlags PUBLIC_STATIC = BindingFlags.Public | BindingFlags.Static;
        const StringComparison IGNORE_CASE = StringComparison.InvariantCultureIgnoreCase;

        static ConcurrentDictionary<string, Macro> Macros
            = new ConcurrentDictionary<string, Macro>();

        /// <summary>
        /// Macro constructor
        /// </summary>
        /// <param name="package">QtVSTest extension package</param>
        /// <param name="joinableTaskFactory">Task factory, enables joining with UI thread</param>
        /// <param name="serverLoop">Server loop cancellation token</param>
        public Macro(
            AsyncPackage package,
            EnvDTE80.DTE2 dte,
            JoinableTaskFactory joinableTaskFactory,
            CancellationToken serverLoop)
        {
            Package = package;
            JoinableTaskFactory = joinableTaskFactory;
            ServerLoop = serverLoop;
            Dte = dte;
            ErrorMsg("Uninitialized");
        }

        /// <summary>
        /// Compile macro code
        /// </summary>
        /// <param name="msg">Message from client containing macro code</param>
        public async Task<bool> CompileAsync(string msg)
        {
            if (MacroLines != null)
                return Warning("Macro already compiled");

            try {
                Message = msg;

                if (!ParseMessage())
                    return false;

                if (!CompileMacro())
                    return false;

                if (string.IsNullOrEmpty(CSharpMethodCode))
                    return true;

                if (!CompileClass())
                    return false;

                await GetServicesAsync();

                return true;
            } catch (Exception e) {
                return ErrorException(e);
            }
        }

        /// <summary>
        /// Run macro
        /// </summary>
        public async Task RunAsync()
        {
            if (!Ok)
                return;

            if (string.IsNullOrEmpty(CSharpMethodCode))
                return;

            try {
                InitGlobalVars();
                await Run();
                await SwitchToWorkerThreadAsync();
                Result = ResultField.GetValue(null) as string;
            } catch (Exception e) {
                ErrorException(e);
            }
        }

        /// <summary>
        /// Parse message text into sequence of macro statements
        /// </summary>
        /// <returns></returns>
        bool ParseMessage()
        {
            if (Parser == null) {
                var parser = MacroParser.Get();
                if (parser == null)
                    return ErrorMsg("Parser error");
                Parser = parser;
            }

            var macroLines = Parser.Parse(Message);
            if (macroLines == null)
                return ErrorMsg("Parse error");

            MacroLines = macroLines;

            return NoError();
        }

        /// <summary>
        /// Expand macro statements into C# code
        /// </summary>
        /// <returns></returns>
        bool CompileMacro()
        {
            if (UiVsRoot == null)
                return ErrorMsg("UI Automation not available");

            var csharp = new StringBuilder();

            foreach (var line in MacroLines) {
                if (QuitWhenDone)
                    return ErrorMsg("No code allowed after #quit");

                if (line is CodeLine) {
                    var codeLine = line as CodeLine;
                    csharp.Append(codeLine.Code + "\r\n");
                    continue;
                }

                if (!GenerateStatement(line as Statement, csharp))
                    return false;
            }

            if (csharp.Length > 0)
                CSharpMethodCode = csharp.ToString();

            AutoRun = string.IsNullOrEmpty(Name);
            if (AutoRun)
                Name = "Macro_" + Path.GetRandomFileName().Replace(".", "");
            else if (!SaveMacro(Name))
                return ErrorMsg("Macro already defined");

            foreach (var sv in ServiceRefs.Values.Where(x => string.IsNullOrEmpty(x.Type)))
                sv.Type = sv.Interface;

            var selectedAssemblyNames = SelectedAssemblies
                .Select(x => new AssemblyName(x))
                .GroupBy(x => x.FullName)
                .Select(x => x.First());

            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .GroupBy(x => x.GetName().Name)
                .ToDictionary(x => x.Key, x => x.AsEnumerable(),
                    StringComparer.InvariantCultureIgnoreCase);

            var refAssemblies = selectedAssemblyNames
                .GroupBy(x => allAssemblies.ContainsKey(x.Name))
                .SelectMany(x => x.Key
                    ? x.SelectMany(y => allAssemblies[y.Name])
                    : x.Select(y =>
                    {
                        try {
                            return Assembly.Load(y);
                        } catch {
                            return null;
                        }
                    }));

            RefAssemblies = refAssemblies
                .Where(x => x != null)
                .Select(x => x.Location);

            return NoError();
        }

        bool GenerateStatement(Statement s, StringBuilder csharp)
        {
            switch (s.Type) {

                case StatementType.Quit:
                    QuitWhenDone = true;
                    break;

                case StatementType.Macro:
                    if (csharp.Length > 0)
                        return ErrorMsg("#macro must be first statement");
                    if (!string.IsNullOrEmpty(Name))
                        return ErrorMsg("Only one #macro statement allowed");
                    if (s.Args.Count < 1)
                        return ErrorMsg("Missing macro name");
                    Name = s.Args[0];
                    break;

                case StatementType.Thread:
                    if (s.Args.Count < 1)
                        return ErrorMsg("Missing thread id");
                    if (s.Args[0].Equals("ui", IGNORE_CASE)) {

                        csharp.Append(
/** BEGIN generate code **/
@"
            await SwitchToUIThread();"
/** END generate code **/ );

                    } else if (s.Args[0].Equals("default", IGNORE_CASE)) {

                        csharp.Append(
/** BEGIN generate code **/
@"
            await SwitchToWorkerThread();"
/** END generate code **/ );

                    } else {
                        return ErrorMsg("Unknown thread id");
                    }
                    break;

                case StatementType.Reference:
                    if (!s.Args.Any())
                        return ErrorMsg("Missing args for #reference");
                    SelectedAssemblies.Add(s.Args.First());
                    foreach (var ns in s.Args.Skip(1))
                        Namespaces.Add(ns);
                    break;

                case StatementType.Using:
                    if (!s.Args.Any())
                        return ErrorMsg("Missing args for #using");
                    foreach (var ns in s.Args)
                        Namespaces.Add(ns);
                    break;

                case StatementType.Var:
                    if (s.Args.Count < 2)
                        return ErrorMsg("Missing args for #var");
                    var typeName = s.Args[0];
                    var varName = s.Args[1];
                    var initValue = s.Code;
                    if (varName.Where(c => char.IsWhiteSpace(c)).Any())
                        return ErrorMsg("Wrong var name");
                    GlobalVars[varName] = new GlobalVar
                    {
                        Type = typeName,
                        Name = varName,
                        InitialValueExpr = initValue
                    };
                    break;

                case StatementType.Service:
                    if (s.Args.Count <= 1)
                        return ErrorMsg("Missing args for #service");
                    var serviceVarName = s.Args[0];
                    if (serviceVarName.Where(c => char.IsWhiteSpace(c)).Any())
                        return ErrorMsg("Invalid service var name");
                    if (ServiceRefs.ContainsKey(serviceVarName))
                        return ErrorMsg("Duplicate service var name");
                    ServiceRefs.Add(serviceVarName, new VSServiceRef
                    {
                        Name = serviceVarName,
                        Interface = s.Args[1],
                        Type = s.Args.Count > 2 ? s.Args[2] : s.Args[1]
                    });
                    break;

                case StatementType.Call:
                    if (s.Args.Count < 1)
                        return ErrorMsg("Missing args for #call");
                    var calleeName = s.Args[0];
                    var callee = GetMacro(calleeName);
                    if (callee == null)
                        return ErrorMsg("Undefined macro");

                    csharp.AppendFormat(
/** BEGIN generate code **/
@"
            await CallMacro(""{0}"");"
/** END generate code **/ , calleeName);

                    foreach (var globalVar in callee.GlobalVars.Values) {
                        if (GlobalVars.ContainsKey(globalVar.Name))
                            continue;
                        GlobalVars[globalVar.Name] = new GlobalVar
                        {
                            Type = globalVar.Type,
                            Name = globalVar.Name
                        };
                    }
                    break;

                case StatementType.Wait:
                    if (string.IsNullOrEmpty(s.Code))
                        return ErrorMsg("Missing args for #wait");
                    var expr = s.Code;
                    uint timeout = uint.MaxValue;
                    if (s.Args.Count > 0 && !uint.TryParse(s.Args[0], out timeout))
                        return ErrorMsg("Timeout format error in #wait");
                    if (s.Args.Count > 2) {
                        var evalVarType = s.Args[1];
                        var evalVarName = s.Args[2];

                        csharp.AppendFormat(
/** BEGIN generate code **/
@"
            {0} {1} = default({0});
            await WaitExpr({2}, () => {1} = {3});"
/** END generate code **/ , evalVarType,
                            evalVarName,
                            timeout,
                            expr);

                    } else {

                        csharp.AppendFormat(
/** BEGIN generate code **/
@"
            await WaitExpr({0}, () => {1});"
/** END generate code **/ , timeout,
                            expr);

                    }
                    break;

                case StatementType.Ui:
                    if (!GenerateUiStatement(s, csharp))
                        return false;
                    break;
            }
            return true;
        }

        public AutomationElement UiFind(AutomationElement uiContext, string[] path)
        {
            var uiIterator = uiContext;
            foreach (var name in path) {
                uiIterator = uiIterator.FindFirst(TreeScope.Subtree,
                    new PropertyCondition(AutomationElement.NameProperty, name));
                if (uiIterator == null)
                    throw new Exception(
                        string.Format("Could not find UI element \"{0}\"", name));
            }
            return uiIterator;
        }

        static readonly IEnumerable<string> UI_TYPES = new[]
        {
            "Dock", "ExpandCollapse", "GridItem", "Grid", "Invoke", "MultipleView", "RangeValue",
            "Scroll", "ScrollItem", "Selection", "SelectionItem", "SynchronizedInput", "Text",
            "Transform", "Toggle", "Value", "Window", "VirtualizedItem", "ItemContainer"
        };

        bool GenerateUiGlobals(StringBuilder csharp)
        {
            csharp.Append(@"
        public static Func<AutomationElement, string[], AutomationElement> UiFind;
        public static Stack<AutomationElement> UiStack;
        public static Dictionary<string, AutomationElement> UiStash;
        public static AutomationElement UiVsRoot;
        public static AutomationElement UiContext;");
            return false;
        }

        bool InitializeUiGlobals()
        {
            if (MacroClass == null)
                return false;

            MacroClass.GetField("UiFind", PUBLIC_STATIC)
                .SetValue(null, new Func<AutomationElement, string[], AutomationElement>(UiFind));

            MacroClass.GetField("UiStack", PUBLIC_STATIC)
                .SetValue(null, new Stack<AutomationElement>());

            MacroClass.GetField("UiStash", PUBLIC_STATIC)
                .SetValue(null, new Dictionary<string, AutomationElement>());

            MacroClass.GetField("UiVsRoot", PUBLIC_STATIC)
                .SetValue(null, UiVsRoot);

            MacroClass.GetField("UiContext", PUBLIC_STATIC)
                .SetValue(null, UiVsRoot);

            return true;
        }

        bool GenerateUiStatement(Statement s, StringBuilder csharp)
        {
            if (s.Args.Count == 0)
                return ErrorMsg("Invalid #ui statement");

            if (s.Args[0].Equals("context", IGNORE_CASE)) {
                //# ui context [ VS ] => _string_ [, _string_, ... ]
                //# ui context HWND => _int_

                if (s.Args.Count > 2 || string.IsNullOrEmpty(s.Code))
                    return ErrorMsg("Invalid #ui statement");

                string context;
                if (s.Args.Count == 1)
                    context = string.Format("UiFind(UiContext, new[] {{ {0} }})", s.Code);
                else if (s.Args.Count > 1 && s.Args[1] == "VS")
                    context = string.Format("UiFind(UiVsRoot, new[] {{ {0} }})", s.Code);
                else if (s.Args.Count > 1 && s.Args[1] == "HWND")
                    context = string.Format("AutomationElement.FromHandle((IntPtr)({0}))", s.Code);
                else
                    return ErrorMsg("Invalid #ui statement");

                csharp.AppendFormat(@"
                    UiContext = {0};", context);

            } else if (s.Args[0].Equals("pattern", IGNORE_CASE)) {
                //# ui pattern <_TypeName_> <_VarName_> [ => _string_ [, _string_, ... ] ]
                //# ui pattern Invoke [ => _string_ [, _string_, ... ] ]
                //# ui pattern Toggle [ => _string_ [, _string_, ... ] ]

                if (s.Args.Count < 2)
                    return ErrorMsg("Invalid #ui statement");

                string typeName = s.Args[1];
                string varName = (s.Args.Count > 2) ? s.Args[2] : string.Empty;
                if (!UI_TYPES.Contains(typeName))
                    return ErrorMsg("Invalid #ui statement");

                string uiElement;
                if (!string.IsNullOrEmpty(s.Code))
                    uiElement = string.Format("UiFind(UiContext, new[] {{ {0} }})", s.Code);
                else
                    uiElement = "UiContext";

                string patternTypeId = string.Format("{0}PatternIdentifiers.Pattern", typeName);
                string patternType = string.Format("{0}Pattern", typeName);

                if (!string.IsNullOrEmpty(varName)) {

                    csharp.AppendFormat(@"
                            var {0} = {1}.GetCurrentPattern({2}) as {3};",
                        varName,
                        uiElement,
                        patternTypeId,
                        patternType);

                } else if (typeName == "Invoke" || typeName == "Toggle") {

                    csharp.AppendFormat(@"
                            ({0}.GetCurrentPattern({1}) as {2}).{3}();",
                        uiElement,
                        patternTypeId,
                        patternType,
                        typeName);

                } else {
                    return ErrorMsg("Invalid #ui statement");
                }

            } else {
                return ErrorMsg("Invalid #ui statement");
            }

            return true;
        }

        const string SERVICETYPE_PREFIX = "_ServiceType_";
        const string INIT_PREFIX = "_Init_";
        string MethodName { get { return string.Format("_Run_{0}_Async", Name); } }

        bool GenerateClass()
        {
            var csharp = new StringBuilder();
            foreach (var ns in Namespaces) {
                csharp.AppendFormat(
/** BEGIN generate code **/
@"
using {0};"
/** END generate code **/ , ns);
            }

            csharp.AppendFormat(
/** BEGIN generate code **/
@"
namespace QtVsTest.Macros
{{
    public class {0}
    {{"
/** END generate code **/ , Name);

            foreach (var serviceRef in ServiceRefs.Values) {
                csharp.AppendFormat(
/** BEGIN generate code **/
@"
        public static {2} {1};
        public static readonly Type {0}{1} = typeof({3});"
/** END generate code **/ , SERVICETYPE_PREFIX,
                            serviceRef.Name,
                            serviceRef.Interface,
                            serviceRef.Type);
            }

            foreach (var globalVar in GlobalVars.Values) {
                csharp.AppendFormat(
/** BEGIN generate code **/
@"
        public static {1} {2};
        public static {1} {0}{2} {{ get {{ return ({3}); }} }}"
/** END generate code **/ , INIT_PREFIX,
                            globalVar.Type,
                            globalVar.Name,
                            globalVar.InitialValueExpr);
            }

            csharp.Append(
/** BEGIN generate code **/
@"
        public static Func<string, Assembly> GetAssembly;
        public static Func<Task> SwitchToUIThread;
        public static Func<Task> SwitchToWorkerThread;
        public static Func<string, Task> CallMacro;
        public static Func<int, Func<object>, Task> WaitExpr;"
/** END generate code **/ );

            if (!GenerateResultFuncs(csharp))
                return false;

            if (!GenerateUiGlobals(csharp))
                return false;

            csharp.AppendFormat(
/** BEGIN generate code **/
@"
        public static async Task {0}()
        {{
{1}
        }}

    }} /*class*/

}} /*namespace*/"
/** END generate code **/ , MethodName,
                            CSharpMethodCode);

            CSharpClassCode = csharp.ToString();

            return true;
        }

        /// <summary>
        /// Generate and compile C# class for macro
        /// </summary>
        /// <returns></returns>
        bool CompileClass()
        {
            if (!GenerateClass())
                return false;

            var dllUri = new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase);
            var dllPath = Uri.UnescapeDataString(dllUri.AbsolutePath);
            var macroDllPath = Path.Combine(Path.GetDirectoryName(dllPath), Name + ".dll");

            if (File.Exists(macroDllPath))
                File.Delete(macroDllPath);

            var cscParams = new CompilerParameters()
            {
                GenerateInMemory = false,
                OutputAssembly = macroDllPath
            };
            cscParams.ReferencedAssemblies.AddRange(RefAssemblies.ToArray());

            var cSharpProvider = new CSharpCodeProvider();

            CompilerResults = cSharpProvider.CompileAssemblyFromSource(cscParams, CSharpClassCode);
            if (CompilerResults.Errors.Count > 0) {
                if (File.Exists(macroDllPath))
                    File.Delete(macroDllPath);
                return ErrorMsg(string.Join("\r\n",
                    CompilerResults.Errors.Cast<CompilerError>()
                        .Select(x => x.ErrorText)));
            }

            MacroAssembly = AppDomain.CurrentDomain.Load(File.ReadAllBytes(macroDllPath));
            MacroClass = MacroAssembly.GetType(string.Format("QtVsTest.Macros.{0}", Name));
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            if (File.Exists(macroDllPath))
                File.Delete(macroDllPath);

            foreach (var serviceVar in ServiceRefs.Values) {
                serviceVar.RefVar = MacroClass.GetField(serviceVar.Name, PUBLIC_STATIC);
                var serviceType = MacroClass.GetField(SERVICETYPE_PREFIX + serviceVar.Name, PUBLIC_STATIC);
                serviceVar.ServiceType = (Type)serviceType.GetValue(null);
            }

            ResultField = MacroClass.GetField("Result", PUBLIC_STATIC);
            foreach (var globalVar in GlobalVars.Values) {
                globalVar.FieldInfo = MacroClass.GetField(globalVar.Name, PUBLIC_STATIC);
                globalVar.InitInfo = MacroClass.GetProperty(INIT_PREFIX + globalVar.Name, PUBLIC_STATIC);
            }

            Run = (Func<Task>)Delegate.CreateDelegate(typeof(Func<Task>),
                MacroClass.GetMethod(MethodName, PUBLIC_STATIC));

            MacroClass.GetField("GetAssembly", PUBLIC_STATIC)
                .SetValue(null, new Func<string, Assembly>(GetAssembly));

            MacroClass.GetField("SwitchToUIThread", PUBLIC_STATIC)
                .SetValue(null, new Func<Task>(SwitchToUIThreadAsync));

            MacroClass.GetField("SwitchToWorkerThread", PUBLIC_STATIC)
                .SetValue(null, new Func<Task>(SwitchToWorkerThreadAsync));

            MacroClass.GetField("CallMacro", PUBLIC_STATIC)
                .SetValue(null, new Func<string, Task>(CallMacroAsync));

            MacroClass.GetField("WaitExpr", PUBLIC_STATIC)
                .SetValue(null, new Func<int, Func<object>, Task>(WaitExprAsync));

            if (!InitializeUiGlobals())
                return false;

            return NoError();
        }

        Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.RequestingAssembly == null || args.RequestingAssembly != MacroAssembly)
                return null;
            var fullName = new AssemblyName(args.Name);
            var assemblyPath = RefAssemblies
                .Where(x => Path.GetFileNameWithoutExtension(x).Equals(fullName.Name, IGNORE_CASE))
                .FirstOrDefault();
            if (string.IsNullOrEmpty(assemblyPath))
                return null;
            if (!File.Exists(assemblyPath))
                return null;
            return Assembly.LoadFrom(assemblyPath);
        }

        public static Assembly GetAssembly(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => x.GetName().Name == name)
                .FirstOrDefault();
        }

        public async Task SwitchToUIThreadAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(ServerLoop);
        }

        public async Task SwitchToWorkerThreadAsync()
        {
            await TaskScheduler.Default;
        }

        public async Task CallMacroAsync(string macroName)
        {
            var callee = GetMacro(macroName);
            if (callee == null)
                throw new FileNotFoundException("Unknown macro");

            callee.InitGlobalVars();
            callee.CopyGlobalVarsFrom(this);
            await callee.Run();
            CopyGlobalVarsFrom(callee);
        }

        public async Task WaitExprAsync(int timeout, Func<object> expr)
        {
            var tMax = TimeSpan.FromMilliseconds(timeout);
            var tRemaining = tMax;
            var t = Stopwatch.StartNew();
            object value;
            try {
                value = await Task.Run(() => expr()).WithTimeout(tRemaining);
            } catch {
                value = null;
            }
            bool ok = !IsDefaultValue(value);

            while (!ok && (tRemaining = (tMax - t.Elapsed)) > TimeSpan.Zero) {
                await Task.Delay(10);
                try {
                    value = await Task.Run(() => expr()).WithTimeout(tRemaining);
                } catch {
                    value = null;
                }
                ok = !IsDefaultValue(value);
            }

            if (!ok)
                throw new TimeoutException();
        }

        bool IsDefaultValue(object obj)
        {
            if (obj == null)
                return true;
            else if (obj.GetType().IsValueType)
                return obj.Equals(Activator.CreateInstance(obj.GetType()));
            else
                return false;
        }

        void InitGlobalVars()
        {
            var globalVarsInit = GlobalVars.Values
                .Where(x => x.FieldInfo != null && !string.IsNullOrEmpty(x.InitialValueExpr));
            foreach (var globalVar in globalVarsInit)
                globalVar.FieldInfo.SetValue(null, globalVar.InitInfo.GetValue(null));
        }

        void CopyGlobalVarsFrom(Macro src)
        {
            var globalVars = GlobalVars.Values
                .Join(src.GlobalVars.Values,
                    DstVar => DstVar.Name, SrcVar => SrcVar.Name,
                    (DstVar, SrcVar) => new { DstVar, SrcVar })
                .Where(x => x.SrcVar.FieldInfo != null && x.DstVar.FieldInfo != null
                    && x.DstVar.FieldInfo.FieldType
                        .IsAssignableFrom(x.SrcVar.FieldInfo.FieldType));

            foreach (var globalVar in globalVars) {
                globalVar.DstVar.FieldInfo
                    .SetValue(null, globalVar.SrcVar.FieldInfo.GetValue(null));
            }
        }

        async Task<bool> GetServicesAsync()
        {
            foreach (var serviceRef in ServiceRefs.Values.Where(x => x.RefVar != null)) {
                serviceRef.RefVar.SetValue(null,
                    await Package.GetServiceAsync(serviceRef.ServiceType));
            }
            return await Task.FromResult(NoError());
        }

        bool SaveMacro(string name)
        {
            if (Macros.ContainsKey(name))
                return false;
            return Macros.TryAdd(Name = name, this);
        }

        static Macro GetMacro(string name)
        {
            Macro macro;
            if (!Macros.TryGetValue(name, out macro))
                return null;
            return macro;
        }

        bool GenerateResultFuncs(StringBuilder csharp)
        {
            csharp.Append(
/** BEGIN generate code **/
@"
        public static string Ok;
        public static string Error;
        public static Func<string, string> ErrorMsg;"
/** END generate code **/ );
            return true;
        }

        bool InitializeResultFuncs()
        {
            if (MacroClass == null)
                return false;

            MacroClass.GetField("Ok", PUBLIC_STATIC)
                .SetValue(null, MACRO_OK);

            MacroClass.GetField("Error", PUBLIC_STATIC)
                .SetValue(null, MACRO_ERROR);

            MacroClass.GetField("ErrorMsg", PUBLIC_STATIC)
                .SetValue(null, new Func<string, string>(MACRO_ERROR_MSG));

            return true;
        }

        string MACRO_OK { get { return "(ok)"; } }
        string MACRO_ERROR { get { return "(error)"; } }
        string MACRO_WARN { get { return "(warn)"; } }
        string MACRO_ERROR_MSG(string msg) { return string.Format("{0}\r\n{1}", MACRO_ERROR, msg); }
        string MACRO_WARN_MSG(string msg) { return string.Format("{0}\r\n{1}", MACRO_WARN, msg); }

        bool NoError()
        {
            Result = MACRO_OK;
            return (Ok = true);
        }

        bool Error()
        {
            Result = MACRO_ERROR;
            return (Ok = false);
        }

        bool ErrorMsg(string errorMsg)
        {
            Result = MACRO_ERROR_MSG(errorMsg);
            return (Ok = false);
        }

        bool ErrorException(Exception e)
        {
            Result = MACRO_ERROR_MSG(string.Format("{0}\r\n\"{1}\"\r\n{2}",
                e.GetType().Name, e.Message, e.StackTrace));
            return (Ok = false);
        }

        bool Warning(string warnMsg)
        {
            Result = MACRO_WARN_MSG(warnMsg);
            return (Ok = true);
        }
    }
}