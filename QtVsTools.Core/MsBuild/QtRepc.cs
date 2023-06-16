/***************************************************************************************************
 Copyright (C) 2023 The Qt Company Ltd.
 SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only
***************************************************************************************************/

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QtVsTools.Core.MsBuild
{
    using CommandLine;

    public sealed class QtRepc : QtTool
    {
        public const string ItemTypeName = "QtRepc";
        public const string ToolExecName = "repc.exe";

        public enum Property
        {
            ExecutionDescription,
            QTDIR,
            InputFileType,
            InputFile,
            OutputFileType,
            OutputFile,
            IncludePath,
            AlwaysClass,
            PrintDebug
        }

        private readonly Dictionary<Property, Option> options = new();

        public QtRepc()
            : base(defaultInputOutput: false)
        {
            Parser.AddOption(options[Property.InputFileType] =
                new Option("i")
                {
                    ValueName = "<rep|src>",
                    Flags = Option.Flag.ShortOptionStyle
                });

            Parser.AddOption(options[Property.OutputFileType] =
                new Option("o")
                {
                    ValueName = "<source|replica|merged|rep>",
                    Flags = Option.Flag.ShortOptionStyle
                });

            Parser.AddOption(options[Property.IncludePath] =
                new Option("I")
                {
                    ValueName = "dir",
                    Flags = Option.Flag.ShortOptionStyle
                });

            Parser.AddOption(options[Property.AlwaysClass] =
                new Option("c"));

            Parser.AddOption(options[Property.PrintDebug] =
                new Option("d"));
        }

        protected override void ExtractInputOutput(string toolExecName, out string inputPath,
            out string outputPath)
        {
            inputPath = outputPath = "";

            var args = new Queue<string>(Parser.PositionalArguments
                .Where(arg => !arg.EndsWith(toolExecName, Utils.IgnoreCase)));

            if (args.Any())
                inputPath = args.Dequeue();

            if (args.Any())
                outputPath = args.Dequeue();
        }

        public bool ParseCommandLine(string commandLine, IVsMacroExpander macros,
            out Dictionary<Property, string> properties)
        {
            properties = new Dictionary<Property, string>();

            if (!ParseCommandLine(commandLine, macros, ToolExecName, out var qtDir,
                out var inputPath, out var outputPath)) {
                return false;
            }

            if (!string.IsNullOrEmpty(qtDir))
                properties[Property.QTDIR] = qtDir;

            if (Parser.IsSet(options[Property.InputFileType])) {
                properties[Property.InputFileType] =
                    string.Join(";", Parser.Values(options[Property.InputFileType]));
            }

            if (!string.IsNullOrEmpty(inputPath))
                properties[Property.InputFile] = inputPath;

            if (Parser.IsSet(options[Property.OutputFileType])) {
                properties[Property.OutputFileType] =
                    string.Join(";", Parser.Values(options[Property.OutputFileType]));
            }

            if (!string.IsNullOrEmpty(outputPath))
                properties[Property.OutputFile] = outputPath;

            if (Parser.IsSet(options[Property.IncludePath])) {
                properties[Property.IncludePath] =
                    string.Join(";", Parser.Values(options[Property.IncludePath]));
            }

            if (Parser.IsSet(options[Property.AlwaysClass]))
                properties[Property.AlwaysClass] = "true";

            if (Parser.IsSet(options[Property.PrintDebug]))
                properties[Property.PrintDebug] = "true";

            return true;
        }

        public string GenerateCommandLine(QtMsBuildContainer container, object propertyStorage)
        {
            var cmd = new StringBuilder();
            cmd.AppendFormat(@"""{0}\bin\{1}""",
                container.GetPropertyValue(propertyStorage, Property.QTDIR), ToolExecName);

            var inputType = container.GetPropertyValue(propertyStorage, Property.InputFileType);
            if (!string.IsNullOrEmpty(inputType))
                GenerateCommandLineOption(cmd, options[Property.InputFileType], inputType);

            var outputType = container.GetPropertyValue(propertyStorage, Property.OutputFileType);
            if (!string.IsNullOrEmpty(outputType))
                GenerateCommandLineOption(cmd, options[Property.OutputFileType], outputType);

            var value = container.GetPropertyValue(propertyStorage, Property.IncludePath);
            if (!string.IsNullOrEmpty(value))
                GenerateCommandLineOption(cmd, options[Property.IncludePath], value, true);

            if (container.GetPropertyValue(propertyStorage, Property.AlwaysClass) == "true")
                GenerateCommandLineOption(cmd, options[Property.AlwaysClass]);

            if (container.GetPropertyValue(propertyStorage, Property.PrintDebug) == "true")
                GenerateCommandLineOption(cmd, options[Property.PrintDebug]);

            value = container.GetPropertyValue(propertyStorage, Property.InputFile);
            if (!string.IsNullOrEmpty(value))
                cmd.AppendFormat(" \"{0}\"", value);

            value = container.GetPropertyValue(propertyStorage, Property.OutputFile);
            if (!string.IsNullOrEmpty(value))
                cmd.AppendFormat(" \"{0}\"", value);

            return cmd.ToString();
        }
    }
}