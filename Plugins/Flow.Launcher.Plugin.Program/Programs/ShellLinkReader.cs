using System;
using Flow.Launcher.Infrastructure.Native;

namespace Flow.Launcher.Plugin.Program.Programs
{
    public static class ShellLinkReader
    {
        // Retrieves the target path, arguments, and description from a shell link via native MS-SHLLINK binary parser
        public static ShellLinkReadResult Read(string path)
        {
            if (FlowNativeMethods.IsAvailable &&
                FlowNativeMethods.ReadLnk(path, out var nativeTarget, out var nativeDesc, out var nativeArgs))
            {
                return new ShellLinkReadResult(nativeTarget ?? string.Empty, nativeDesc ?? string.Empty, nativeArgs ?? string.Empty);
            }

            return new ShellLinkReadResult(string.Empty, string.Empty, string.Empty);
        }
    }
}
