using System;
using System.IO;
using Flow.Launcher.Infrastructure.Native;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class NativeMethodsTest
    {
        [Test]
        public void GivenNativeLibrary_WhenLoaded_ThenIsAvailableIsTrue()
        {
            ClassicAssert.IsTrue(FlowNativeMethods.IsAvailable, "FlowNativeMethods should be available");
        }

        [Test]
        public void GivenMathExpression_WhenEvaluated_ThenResultIsCorrect()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            bool ok = FlowNativeMethods.EvaluateMath("10 + 20 * 2", out double result);
            ClassicAssert.IsTrue(ok);
            ClassicAssert.AreEqual(50.0, result, 0.001);
        }

        [Test]
        public void GivenProcessBuffer_WhenGetProcessesCalled_ThenCountIsGreaterThanZero()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            var buffer = new NativeProcessItem[256];
            int count = FlowNativeMethods.GetProcesses(buffer);
            ClassicAssert.Greater(count, 0);

            var first = buffer[0].GetName();
            ClassicAssert.IsFalse(string.IsNullOrEmpty(first));
        }

        [Test]
        public void GivenQueryAndTarget_WhenFuzzyMatchCalled_ThenMatchesCorrectly()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            Span<int> indices = stackalloc int[16];
            bool ok = FlowNativeMethods.FuzzyMatch("flow", "Flow.Launcher", true, indices, out int score, out int matchCount);
            ClassicAssert.IsTrue(ok);
            ClassicAssert.Greater(score, 0);
            ClassicAssert.AreEqual(4, matchCount);
        }

        [Test]
        public void GivenAppPathsBuffer_WhenScanRegistryAppPathsCalled_ThenReturnsApplications()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            var buffer = new NativeRegistryApp[256];
            bool ok = FlowNativeMethods.ScanRegistryAppPaths(buffer, out int count);
            ClassicAssert.IsTrue(ok);
            ClassicAssert.Greater(count, 0);

            var path = buffer[0].GetPath();
            ClassicAssert.IsTrue(File.Exists(path));
        }

        [Test]
        public void GivenDirectoryPath_WhenScanDirectoryCalled_ThenReturnsFiles()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var buffer = new NativeFileItem[64];
            bool ok = FlowNativeMethods.ScanDirectory(baseDir, "dll", false, buffer, out int count);
            ClassicAssert.IsTrue(ok);
            ClassicAssert.Greater(count, 0);
        }

        [Test]
        public void GivenExecutable_WhenExtractPeInfoCalled_ThenReturnsSubsystemAndDescription()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            if (!File.Exists(cmdPath)) Assert.Ignore("cmd.exe not found");

            bool ok = FlowNativeMethods.ExtractPeInfo(cmdPath, out string desc, out uint subsystem, out bool is64Bit);
            ClassicAssert.IsTrue(ok);
            ClassicAssert.AreEqual(3u, subsystem); // 3 = CUI (Console)
        }


        [Test]
        public void GivenProcess_WhenTrimWorkingSetCalled_ThenExecutesSuccessfully()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            bool ok = FlowNativeMethods.TrimWorkingSet();
            ClassicAssert.IsTrue(ok);
        }
    }
}
