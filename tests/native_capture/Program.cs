using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

if (!OperatingSystem.IsWindows() || args.Length != 3)
    throw new ArgumentException("Usage: NativeCapture <godot.exe> <project-root> <dump-path>");
var startup = new Native.StartupInfo { Size = Marshal.SizeOf<Native.StartupInfo>(), Flags = 1 };
var command = new StringBuilder($"\"{args[0]}\" --headless --path \"{args[1]}\" --script tests/terrain_iso_shutdown_probe.gd --quit-after 3600");
if (!Native.CreateProcess(args[0], command, IntPtr.Zero, IntPtr.Zero, false, 0x08000002,
        IntPtr.Zero, args[1], ref startup, out var process))
    throw new Win32Exception();
IntPtr debugEvent = Marshal.AllocHGlobal(176);
bool captured = false;
var deadline = DateTime.UtcNow.AddMinutes(3);
try
{
    while (DateTime.UtcNow < deadline)
    {
        if (!Native.WaitForDebugEvent(debugEvent, 1000)) continue;
        int kind = Marshal.ReadInt32(debugEvent);
        uint pid = unchecked((uint)Marshal.ReadInt32(debugEvent, 4));
        uint tid = unchecked((uint)Marshal.ReadInt32(debugEvent, 8));
        uint status = 0x00010002;
        if (kind == 1)
        {
            uint exception = unchecked((uint)Marshal.ReadInt32(debugEvent, 16));
            int firstChance = Marshal.ReadInt32(debugEvent, 168);
            if (exception != 0x80000003) status = 0x80010001;
            if (exception == 0xC0000005 && !captured)
            {
                Console.WriteLine($"Access violation pid={pid} tid={tid} firstChance={firstChance} address={Marshal.ReadIntPtr(debugEvent, 32):X}");
                using var file = new FileStream(args[2], FileMode.CreateNew, FileAccess.Write);
                if (!Native.MiniDumpWriteDump(process.Process, pid, file.SafeFileHandle.DangerousGetHandle(),
                        2, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();
                captured = true;
                Console.WriteLine($"Captured {args[2]}");
            }
        }
        if (kind == 3 || kind == 6)
        {
            IntPtr file = Marshal.ReadIntPtr(debugEvent, 16);
            if (file != IntPtr.Zero && file != new IntPtr(-1)) Native.CloseHandle(file);
        }
        Native.ContinueDebugEvent(pid, tid, status);
        if (kind == 5)
        {
            Console.WriteLine($"Process exit: {Marshal.ReadInt32(debugEvent, 16):X8}; captured={captured}");
            return captured ? 0 : 2;
        }
    }
    throw new TimeoutException("Debug target exceeded three minutes");
}
finally
{
    // This harness owns only the process it created, never another running Godot instance.
    if (Native.GetExitCodeProcess(process.Process, out uint exit) && exit == 259)
        Native.TerminateProcess(process.Process, 1);
    Marshal.FreeHGlobal(debugEvent);
    Native.CloseHandle(process.Thread);
    Native.CloseHandle(process.Process);
}

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved, Desktop, Title;
        public uint X, Y, Width, Height, XChars, YChars, Fill, Flags;
        public ushort ShowWindow, ReservedSize;
        public IntPtr ReservedBytes, StdInput, StdOutput, StdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessInfo
    {
        public IntPtr Process, Thread;
        public uint ProcessId, ThreadId;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateProcessW")]
    internal static extern bool CreateProcess(string app, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string directory,
        ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool WaitForDebugEvent(IntPtr debugEvent, uint timeout);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ContinueDebugEvent(uint processId, uint threadId, uint status);
    [DllImport("kernel32.dll")]
    internal static extern bool GetExitCodeProcess(IntPtr process, out uint exit);
    [DllImport("kernel32.dll")]
    internal static extern bool TerminateProcess(IntPtr process, uint exit);
    [DllImport("kernel32.dll")]
    internal static extern bool CloseHandle(IntPtr handle);
    [DllImport("dbghelp.dll", SetLastError = true)]
    internal static extern bool MiniDumpWriteDump(IntPtr process, uint processId, IntPtr file,
        uint type, IntPtr exception, IntPtr stream, IntPtr callback);
}
