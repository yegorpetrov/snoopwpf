// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;
using System.Windows;
using System.IO;

using System.Reflection;
using System.Text;

namespace Snoop
{
    public class ILSpyIntegration
    {
        private MethodInfo decompileMethodInfo;
        private string directory;

        public ILSpyIntegration(string ilSpyDiretory)
        {
            this.directory = ilSpyDiretory;
        }

        public string DecompileMethodByLoadingAssembly(MethodInfo methodToDecompile)
        {
            if (decompileMethodInfo == null)
            {
                var location = typeof(Snoop.SnoopUI).Assembly.Location;
                var directory = Path.GetDirectoryName(location);
                directory = Path.Combine(directory, "ILSpy");

                var assembly = Assembly.LoadFrom(Path.Combine(directory, "ConsoleApplicationDecompile.exe"));
                var type = assembly.GetType("ConsoleApplicationDecompile.Program");
                decompileMethodInfo = type.GetMethod("GetSourceOfMethod");
            }

            try
            {
                var result = decompileMethodInfo.Invoke(null, new object[] { methodToDecompile });

                return result.ToString();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }


        public string DecompileMethodUsingExternalProcess(MethodInfo methodToDecompile)
        {
            var location = typeof(Snoop.SnoopUI).Assembly.Location;
            var directory = Path.GetDirectoryName(location);
            directory = Path.Combine(directory, "ILSpy");
            var decompileProgramName = Path.Combine(directory, "ConsoleApplicationDecompile.exe");

            Process decompileProcess = new Process();
            decompileProcess.StartInfo.FileName = decompileProgramName;
            decompileProcess.StartInfo.WorkingDirectory = directory;
            //// Set UseShellExecute to false for redirection.
            var parametersStringArray = GetParametersString(methodToDecompile);
            //decompileProcess.StartInfo.Arguments = "\"" + methodToDecompile.DeclaringType.Assembly.Location + "\"" + " " + methodToDecompile.DeclaringType.Name + " " + methodToDecompile.Name + " " + parametersStringArray;
            decompileProcess.StartInfo.Arguments = string.Format("\"{0}\" {1} {2} {3}", methodToDecompile.DeclaringType.Assembly.Location,
                methodToDecompile.DeclaringType.Name,
                methodToDecompile.Name,
                parametersStringArray);


            decompileProcess.StartInfo.UseShellExecute = false;

            //// Redirect the standard output of the sort command.   
            decompileProcess.StartInfo.RedirectStandardOutput = true;
            StringBuilder sourceCode = new StringBuilder();
            decompileProcess.Start();
            sourceCode.Append(decompileProcess.StandardOutput.ReadToEnd());
            decompileProcess.WaitForExit();

            return sourceCode.ToString();
        }

        private static string GetParametersString(MethodInfo methodInfo)
        {
            var parameters = methodInfo.GetParameters();

            if (parameters.Length == 0)
                return string.Empty;

            string[] parametersStringArray = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                parametersStringArray[i] = parameters[i].ParameterType.Name;

            var parametersString = string.Join("|", parametersStringArray);
            return parametersString;
        }
    }

    /// <summary>
    /// Class for ILSpy messaging. We'll move this after code review.
    /// </summary>
    public static class ILSpyInterop
    {
        public const string ILSPY_PREFIX = "ILSpy:\r\n";
        public const string ILSPY_NAVIGATE_TO_TYPE = "/navigateTo:T:";
        public const string ILSPY_NAVIGATE_TO_METHOD = "/navigateTo:M:";
        public const string ILSPY_LINE_BREAK = "\r\n";
        private static readonly string NAVIGATE_TO_TYPE_MESSAGE;//Format of message to be sent to an existing ilspy process via send message.
        private static readonly string NAVIGATE_TO_METHOD_MESSAGE;//Format of message to be sent to an existing ilspy process via send message.
        private static readonly string NAVIGATE_TO_TYPE_ARGUMENT;//Format of command line argument to be sent when starting ILSpy
        private static readonly string NAVIGATE_TO_METHOD_ARGUMENT;//Format of command line argument to be sent when starting ILSpy

        static ILSpyInterop()
        {
            NAVIGATE_TO_TYPE_MESSAGE = ILSPY_PREFIX + ILSPY_LINE_BREAK + "{0}" + ILSPY_LINE_BREAK + ILSPY_NAVIGATE_TO_TYPE + "{1}";
            NAVIGATE_TO_METHOD_MESSAGE = ILSPY_PREFIX + ILSPY_LINE_BREAK + "{0}" + ILSPY_LINE_BREAK + ILSPY_NAVIGATE_TO_METHOD + "{1}";
            //"\"{0}\" /navigateTo:T:{1}"
            NAVIGATE_TO_TYPE_ARGUMENT = "\"{0}\" " + ILSPY_NAVIGATE_TO_TYPE + "{1}";
            NAVIGATE_TO_METHOD_ARGUMENT = "\"{0}\" " + ILSPY_NAVIGATE_TO_METHOD + "{1}";
        }

        public static void OpenTypeInILSpy(string fullAssemblyPath, string fullTypeName, Process ilSpyProcess)
        {
            IntPtr windowHandle = ilSpyProcess.MainWindowHandle;            
            //string args = string.Format("ILSpy:\r\n{0}\r\n/navigateTo:T:{1}", fullAssemblyPath, fullTypeName);
            string args = string.Format(NAVIGATE_TO_TYPE_MESSAGE, fullAssemblyPath, fullTypeName);
            NativeMethods.Send(windowHandle, args);
            NativeMethods.SetForegroundWindow(ilSpyProcess.MainWindowHandle);
        }

        public static void OpenTypeInILSpy(string fullAssemblyPath, string fullTypeName, IntPtr windowHandle)
        {
            //string args = string.Format("ILSpy:\r\n{0}\r\n/navigateTo:T:{1}", fullAssemblyPath, fullTypeName);
            string args = string.Format(NAVIGATE_TO_TYPE_MESSAGE, fullAssemblyPath, fullTypeName);
            NativeMethods.Send(windowHandle, args);
            NativeMethods.SetForegroundWindow(windowHandle);
        }

        public static void OpenMethodInILSpy(string fullAssemblyPath, string fullTypeName, string methodName, IntPtr windowHandle)
        {
            fullTypeName = fullTypeName.Replace('+', '.');
            //string args = string.Format("ILSpy:\r\n{0}\r\n/navigateTo:M:{1}", fullAssemblyPath, fullTypeName + "." + methodName);
            string args = string.Format(NAVIGATE_TO_METHOD_MESSAGE, fullAssemblyPath, fullTypeName + "." + methodName);
            NativeMethods.Send(windowHandle, args);
            NativeMethods.SetForegroundWindow(windowHandle);
        }

        public static Process GetOrCreateILSpyProcess(string fullAssemblyPath, string fullTypeName)
        {
            fullTypeName = fullTypeName.Replace('+', '.');
            //string arguments = string.Format("\"{0}\" /navigateTo:T:{1}", fullAssemblyPath, fullTypeName);
            //string sendToProcessArgs = string.Format("ILSpy:\r\n{0}\r\n/navigateTo:T:{1}", fullAssemblyPath, fullTypeName);
            string arguments = string.Format(NAVIGATE_TO_TYPE_ARGUMENT, fullAssemblyPath, fullTypeName);
            string sendToProcessArgs = string.Format(NAVIGATE_TO_TYPE_MESSAGE, fullAssemblyPath, fullTypeName);
            return CreateILSpyProcessWithArguments(fullAssemblyPath, fullTypeName, arguments, sendToProcessArgs);
        }

        public static Process GetOrCreateILSpyProcess(string fullAssemblyPath, string fullTypeName, string methodName)
        {
            fullTypeName = fullTypeName.Replace('+', '.');
            //string arguments = string.Format("\"{0}\" /navigateTo:M:{1}", fullAssemblyPath, fullTypeName + "." + methodName);
            //string sendToProcessArgs = string.Format("ILSpy:\r\n{0}\r\n/navigateTo:M:{1}", fullAssemblyPath, fullTypeName + "." + methodName);
            string arguments = string.Format(NAVIGATE_TO_METHOD_ARGUMENT, fullAssemblyPath, fullTypeName + "." + methodName);
            string sendToProcessArgs = string.Format(NAVIGATE_TO_METHOD_MESSAGE, fullAssemblyPath, fullTypeName + "." + methodName);

            return CreateILSpyProcessWithArguments(fullAssemblyPath, fullTypeName, arguments, sendToProcessArgs);
        }

        private static Process CreateILSpyProcessWithArguments(string fullAssemblyPath, string fullTypeName, string arguments, string sendToProcessArgs)
        {
            Process ilSpyProcess = null;
            var location = typeof(Snoop.SnoopUI).Assembly.Location;
            var directory = Path.GetDirectoryName(location);
            directory = Path.Combine(directory, "ILSpy");
            var ilSpyProgram = Path.Combine(directory, "ILSpy.exe");

            var processes = Process.GetProcessesByName("ILSpy");
            if (processes.Length > 0)
            {
                ilSpyProcess = processes[0];
                NativeMethods.Send(ilSpyProcess.MainWindowHandle, sendToProcessArgs);
                NativeMethods.SetForegroundWindow(ilSpyProcess.MainWindowHandle);
                return ilSpyProcess;
            }

            ilSpyProcess = new Process();
            ilSpyProcess.StartInfo.FileName = ilSpyProgram; 
            ilSpyProcess.StartInfo.WorkingDirectory = directory;
            ilSpyProcess.StartInfo.Arguments = arguments;
            //ilSpyProcess.EnableRaisingEvents = true;
            ilSpyProcess.Start();
            return ilSpyProcess;
        }
    }

    //ILSPY
    [StructLayout(LayoutKind.Sequential)]
    public struct CopyDataStruct
    {
        public IntPtr Padding;
        public int Size;
        public IntPtr Buffer;

        public CopyDataStruct(IntPtr padding, int size, IntPtr buffer)
        {
            this.Padding = padding;
            this.Size = size;
            this.Buffer = buffer;
        }
    }

    public static class NativeMethods
    {
        //ILSPY
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint msg, IntPtr wParam, ref CopyDataStruct lParam,
            uint flags, uint timeout, out IntPtr result);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public const uint WM_COPYDATA = 0x4a;

        public static IntPtr Send(IntPtr hWnd, string message)
        {
            const uint SMTO_NORMAL = 0;

            CopyDataStruct lParam;
            lParam.Padding = IntPtr.Zero;
            lParam.Size = message.Length * 2;
            lParam.Buffer = Marshal.StringToHGlobalUni(message);

            IntPtr result;
            NativeMethods.SendMessageTimeout(hWnd, NativeMethods.WM_COPYDATA, IntPtr.Zero, ref lParam, SMTO_NORMAL, 3000,
                                             out result);
            return result;
        }

        public static IntPtr[] ToplevelWindows
        {
            get
            {
                List<IntPtr> windowList = new List<IntPtr>();
                GCHandle handle = GCHandle.Alloc(windowList);
                try
                {
                    NativeMethods.EnumWindows(NativeMethods.EnumWindowsCallback, (IntPtr)handle);
                }
                finally
                {
                    handle.Free();
                }

                return windowList.ToArray();
            }
        }
        public static Process GetWindowThreadProcess(IntPtr hwnd)
        {
            int processID;
            NativeMethods.GetWindowThreadProcessId(hwnd, out processID);

            try
            {
                return Process.GetProcessById(processID);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private delegate bool EnumWindowsCallBackDelegate(IntPtr hwnd, IntPtr lParam);
        private static bool EnumWindowsCallback(IntPtr hwnd, IntPtr lParam)
        {
            ((List<IntPtr>)((GCHandle)lParam).Target).Add(hwnd);
            return true;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct MODULEENTRY32
        {
            public uint dwSize;
            public uint th32ModuleID;
            public uint th32ProcessID;
            public uint GlblcntUsage;
            public uint ProccntUsage;
            IntPtr modBaseAddr;
            public uint modBaseSize;
            IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExePath;
        };

        public class ToolHelpHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            private ToolHelpHandle()
                : base(true)
            {
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            override protected bool ReleaseHandle()
            {
                return NativeMethods.CloseHandle(handle);
            }
        }

        [Flags]
        public enum SnapshotFlags : uint
        {
            HeapList = 0x00000001,
            Process = 0x00000002,
            Thread = 0x00000004,
            Module = 0x00000008,
            Module32 = 0x00000010,
            Inherit = 0x80000000,
            All = 0x0000001F
        }

        [DllImport("user32.dll")]
        private static extern int EnumWindows(EnumWindowsCallBackDelegate callback, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hwnd, out int processId);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32")]
        public extern static IntPtr LoadLibrary(string librayName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static public extern ToolHelpHandle CreateToolhelp32Snapshot(SnapshotFlags dwFlags, int th32ProcessID);

        [DllImport("kernel32.dll")]
        static public extern bool Module32First(ToolHelpHandle hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll")]
        static public extern bool Module32Next(ToolHelpHandle hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll", SetLastError = true)]
        static public extern bool CloseHandle(IntPtr hHandle);


        // anvaka's changes below


        public static Point GetCursorPosition()
        {
            var pos = new Point();
            var win32Point = new POINT();
            if (GetCursorPos(ref win32Point))
            {
                pos.X = win32Point.X;
                pos.Y = win32Point.Y;
            }
            return pos;
        }

        public static IntPtr GetWindowUnderMouse()
        {
            POINT pt = new POINT();
            if (GetCursorPos(ref pt))
            {
                return WindowFromPoint(pt);
            }
            return IntPtr.Zero;
        }

        public static Rect GetWindowRect(IntPtr hwnd)
        {
            RECT rect = new RECT();
            GetWindowRect(hwnd, out rect);
            return new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(ref POINT pt);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    }
}
