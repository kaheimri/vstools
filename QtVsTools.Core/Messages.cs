/***************************************************************************************************
 Copyright (C) 2023 The Qt Company Ltd.
 SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only
***************************************************************************************************/

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using static System.Environment;
using Task = System.Threading.Tasks.Task;

namespace QtVsTools.Core
{
    using VisualStudio;

    public static class Messages
    {
        private static OutputWindowPane Pane { get; set; }

        private const string Name = "Qt VS Tools";
        private static readonly Guid PaneGuid = new Guid("8f6a1e44-fa0b-49e5-9934-1c050555350e");

        /// <summary>
        /// Show a message on the output pane.
        /// </summary>
        public static void Print(string text, bool clear = false, bool activate = false)
        {
            msgQueue.Enqueue(new Msg()
            {
                Clear = clear,
                Text = text,
                Activate = activate
            });
            FlushMessages();
        }

        public static void Log(this Exception exception, bool clear = false, bool activate = false)
        {
            msgQueue.Enqueue(new Msg()
            {
                Clear = clear,
                Text = ExceptionToString(exception),
                Activate = activate
            });
            FlushMessages();
        }

        /// <summary>
        /// Activates the message pane of the Qt VS Tools extension.
        /// </summary>
        public static void ActivateMessagePane()
        {
            msgQueue.Enqueue(new Msg()
            {
                Activate = true
            });
            FlushMessages();
        }

        static async Task OutputWindowPane_ActivateAsync()
        {
            await OutputWindowPane_InitAsync();
            await Pane?.ActivateAsync();
        }

        private static string ExceptionToString(System.Exception exception)
        {
            return $"An exception ({exception.GetType().Name}) occurred.\r\n"
                   + $"Message:\r\n   {exception.Message}\r\n"
                   + $"Stack Trace:\r\n   {exception.StackTrace.Trim()}\r\n";
        }

        private const string ErrorString = "The following error occurred:";
        private static readonly string WarningString = "Warning:" + NewLine;

        public static void DisplayCriticalErrorMessage(string msg)
        {
            MessageBox.Show(ErrorString + NewLine + msg,
                Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void DisplayErrorMessage(System.Exception e)
        {
            MessageBox.Show(ExceptionToString(e),
                Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void DisplayErrorMessage(string msg)
        {
            MessageBox.Show(ErrorString + NewLine + msg,
                Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void DisplayWarningMessage(System.Exception e, string solution)
        {
            MessageBox.Show(WarningString
                + ExceptionToString(e)
                + NewLine + NewLine + "To solve this problem:" + NewLine
                + solution,
                Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void DisplayWarningMessage(string msg)
        {
            MessageBox.Show(WarningString +
                msg,
                Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void ClearPane()
        {
            msgQueue.Enqueue(new Msg()
            {
                Clear = true
            });
            FlushMessages();
        }

        static async Task OutputWindowPane_ClearAsync()
        {
            await OutputWindowPane_InitAsync();
            await Pane?.ClearAsync();
        }

        class Msg
        {
            public bool Clear { get; set; } = false;
            public string Text { get; set; } = null;
            public bool Activate { get; set; } = false;
        }

        static readonly ConcurrentQueue<Msg> msgQueue = new ConcurrentQueue<Msg>();

        private static async Task OutputWindowPane_InitAsync()
        {
            try {
                Pane ??= await OutputWindowPane.CreateAsync(Name, PaneGuid);
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        public static JoinableTaskFactory JoinableTaskFactory { get; set; }

        static readonly object staticCriticalSection = new object();
        static Task FlushTask { get; set; }
        static EventWaitHandle MessageReady { get; set; }

        static void FlushMessages()
        {
            lock (staticCriticalSection) {
                if (FlushTask == null) {
                    MessageReady = new EventWaitHandle(false, EventResetMode.AutoReset);
                    FlushTask = Task.Run(async () =>
                    {
                        var package = VsServiceProvider.Instance as Package;
                        while (!package.Zombied) {
                            if (!await MessageReady.ToTask(3000))
                                continue;
                            while (!msgQueue.IsEmpty) {
                                if (!msgQueue.TryDequeue(out Msg msg)) {
                                    await Task.Yield();
                                    continue;
                                }
                                if (msg.Clear)
                                    await OutputWindowPane_ClearAsync();
                                if (msg.Text != null)
                                    await OutputWindowPane_PrintAsync(msg.Text);
                                if (msg.Activate)
                                    await OutputWindowPane_ActivateAsync();
                            }
                        }
                    });
                }
            }
            MessageReady.Set();
        }

        static async Task OutputWindowPane_PrintAsync(string text)
        {
            var active = await OutputWindowPane.GetActiveAsync();

            await OutputWindowPane_InitAsync();
            await Pane.PrintAsync(text);

            (active?.ActivateAsync()).Forget();
        }
    }
}
