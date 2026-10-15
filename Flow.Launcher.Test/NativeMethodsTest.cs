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

        [Test]
        public void GivenEverythingQuery_WhenBuilt_ThenReturnsFormattedString()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            string query = FlowNativeMethods.BuildEverythingQuery("flow", @"C:\Users", false, "launcher");
            ClassicAssert.AreEqual(@"flow parent:""C:\Users"" content:""launcher""", query);

            Span<NativeHighlightRange> ranges = stackalloc NativeHighlightRange[4];
            int count = FlowNativeMethods.ParseEverythingHighlights("*flow*.exe", ranges);
            ClassicAssert.AreEqual(1, count);
            ClassicAssert.AreEqual(0, ranges[0].Start);
            ClassicAssert.AreEqual(4, ranges[0].Length);
        }

        [Test]
        public void GivenFileMetaAndPreview_WhenRead_ThenReturnsCorrectMeta()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            string cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            if (!File.Exists(cmdPath)) Assert.Ignore("cmd.exe not found");

            bool ok = FlowNativeMethods.GetFileMeta(cmdPath, out NativeFileMeta meta);
            ClassicAssert.IsTrue(ok);
            ClassicAssert.IsTrue(meta.FileSize > 0);
            ClassicAssert.AreEqual(0, meta.IsDirectory);

            string formatted = FlowNativeMethods.FormatFileSize(1024 * 1024 * 2);
            ClassicAssert.AreEqual("2.0 MB", formatted);
        }

        [Test]
        public void GivenScores_WhenRankIndicesCalled_ThenReturnsPermutationSortedDescending()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            int[] scores = { 10, 80, 45, 95 };
            int[] indices = new int[4];
            bool ok = FlowNativeMethods.RankIndices(scores, indices);
            ClassicAssert.IsTrue(ok);
            ClassicAssert.AreEqual(3, indices[0]); // 95
            ClassicAssert.AreEqual(1, indices[1]); // 80
            ClassicAssert.AreEqual(2, indices[2]); // 45
            ClassicAssert.AreEqual(0, indices[3]); // 10
        }

        [Test]
        public void GivenColorPalette_WhenExtracted_ThenReturnsValidThemeLuminance()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            // Dark pixel buffer (RGBA: 30, 30, 30, 255)
            byte[] darkPixels = new byte[16 * 4];
            for (int i = 0; i < 16; i++)
            {
                darkPixels[i * 4] = 30;
                darkPixels[i * 4 + 1] = 30;
                darkPixels[i * 4 + 2] = 30;
                darkPixels[i * 4 + 3] = 255;
            }

            bool ok = FlowNativeMethods.ExtractPalette(darkPixels, 16, out NativeColorPalette palette);
            ClassicAssert.IsTrue(ok);
            ClassicAssert.AreEqual(1, palette.IsDark);
        }

        [Test]
        public void GivenJsonRpcResponse_WhenParsed_ThenPopulatesContiguousNativeItems()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            byte[] json = System.Text.Encoding.UTF8.GetBytes("{\"result\":[{\"title\":\"Test Result\",\"subTitle\":\"Sub Result\",\"icoPath\":\"icon.png\",\"score\":99}]}");
            NativeJsonRpcItem[] items = new NativeJsonRpcItem[2];
            int count = FlowNativeMethods.ParseJsonRpcResponse(json, items);
            ClassicAssert.AreEqual(1, count);
            ClassicAssert.AreEqual("Test Result", items[0].GetTitle());
            ClassicAssert.AreEqual("Sub Result", items[0].GetSubtitle());
            ClassicAssert.AreEqual("icon.png", items[0].GetIconPath());
            ClassicAssert.AreEqual(99, items[0].Score);
        }

        [Test]
        public void GivenUwpManifestXml_WhenParsed_ThenExtractsAppsAndElevate()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
  <Applications>
    <Application Id=""App"" EntryPoint=""Windows.FullTrustApplication"">
      <VisualElements Square44x44Logo=""Assets\Logo.png"" Square150x150Logo=""Assets\Big.png"" />
    </Application>
  </Applications>
</Package>";

            string tempFile = Path.Combine(Path.GetTempPath(), $"TestAppx_{Guid.NewGuid():N}.xml");
            File.WriteAllText(tempFile, xml);
            try
            {
                Span<NativeUwpAppInfo> apps = stackalloc NativeUwpAppInfo[4];
                int count = FlowNativeMethods.ParseUwpManifest(tempFile, apps);
                ClassicAssert.AreEqual(1, count);
                ClassicAssert.AreEqual("App", apps[0].GetAppId());
                ClassicAssert.AreEqual(@"Assets\Logo.png", apps[0].GetLogoUri());
                ClassicAssert.AreEqual(@"Assets\Big.png", apps[0].GetPreviewUri());
                ClassicAssert.AreEqual(1, apps[0].CanElevate);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Test]
        public void GivenBookmarks_WhenRegisteredAndSearched_ThenReturnsSortedMatches()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            FlowNativeMethods.ClearBookmarksCache();
            FlowNativeMethods.RegisterBookmark(0, "GitHub - Flow Launcher", "https://github.com/Flow-Launcher/Flow.Launcher");
            FlowNativeMethods.RegisterBookmark(1, "Rust Language Official", "https://www.rust-lang.org/");
            FlowNativeMethods.RegisterBookmark(2, "Google Search Engine", "https://www.google.com/");

            Span<NativeBookmarkMatch> matches = stackalloc NativeBookmarkMatch[4];
            int count = FlowNativeMethods.SearchBookmarks("rust", matches);
            ClassicAssert.AreEqual(1, count);
            ClassicAssert.AreEqual(1u, matches[0].Id);
            ClassicAssert.IsTrue(matches[0].Score > 0);
        }

        [Test]
        public void GivenTextAndFont_WhenMeasureTextCalled_ThenReturnsAccurateDimensions()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            bool ok = FlowNativeMethods.MeasureText("Flow Launcher", "Segoe UI", 14f, 400, false, out var metrics);
            ClassicAssert.IsTrue(ok);
            ClassicAssert.IsTrue(metrics.Width > 0f);
            ClassicAssert.IsTrue(metrics.Height > 0f);
        }

        [Test]
        public void GivenPayload_WhenCompressedAndDecompressedWithLz4_ThenRoundtripsAccurately()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            byte[] original = System.Text.Encoding.UTF8.GetBytes("Flow Launcher - Instant search with zero-overhead native acceleration. Flow Launcher - Instant search with zero-overhead native acceleration.");
            Span<byte> compressed = stackalloc byte[original.Length * 2 + 32];
            bool compOk = FlowNativeMethods.CompressLz4(original, compressed, out int compLen);
            ClassicAssert.IsTrue(compOk);
            ClassicAssert.IsTrue(compLen > 0);
            ClassicAssert.IsTrue(compLen < original.Length);

            Span<byte> decompressed = stackalloc byte[original.Length + 16];
            bool decompOk = FlowNativeMethods.DecompressLz4(compressed.Slice(0, compLen), decompressed, out int decompLen);
            ClassicAssert.IsTrue(decompOk);
            ClassicAssert.AreEqual(original.Length, decompLen);
            ClassicAssert.IsTrue(original.AsSpan().SequenceEqual(decompressed.Slice(0, decompLen)));
        }

        [Test]
        public void GivenDirectory_WhenKernelWatcherCreated_ThenLifecycleSucceeds()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            var tempDir = Path.GetTempPath();
            var handle = FlowNativeMethods.CreateDirectoryWatcher(tempDir, false);
            ClassicAssert.AreNotEqual(IntPtr.Zero, handle);

            // Non-blocking poll
            _ = FlowNativeMethods.CheckDirectoryWatcher(handle);

            bool closed = FlowNativeMethods.CloseDirectoryWatcher(handle);
            ClassicAssert.IsTrue(closed);
        }

        [Test]
        public void GivenQuery_WhenTokenized_ThenMatchesExpectedOffsetsAndTerms()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            Span<NativeQueryTerm> terms = stackalloc NativeQueryTerm[16];
            bool ok = FlowNativeMethods.TokenizeQuery(
                ">   ping    google.com   -n 20".AsSpan(),
                ">".AsSpan(),
                terms,
                out var parsed);

            ClassicAssert.IsTrue(ok);
            ClassicAssert.AreEqual(0, parsed.IsHomeQuery);
            ClassicAssert.AreEqual(1, parsed.HasActionKeyword);
            ClassicAssert.AreEqual(4, parsed.TermCount);

            string original = ">   ping    google.com   -n 20";
            string firstTerm = original.Substring((int)terms[0].Start, (int)terms[0].Length);
            ClassicAssert.AreEqual("ping", firstTerm);

            string secondTerm = original.Substring((int)terms[1].Start, (int)terms[1].Length);
            ClassicAssert.AreEqual("google.com", secondTerm);
        }

        [Test]
        public void GivenText_WhenHistoryHashed_ThenMatchesManagedAlgorithm()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            string testString = "Windows Terminal - Preview";
            int nativeHash = FlowNativeMethods.ComputeHistoryHash(testString.AsSpan());

            // Compute expected hash using classic loop
            int expected = 23;
            unchecked
            {
                for (int i = 0; i < testString.Length; i++)
                {
                    expected = expected * 31 + testString[i];
                }
            }

            ClassicAssert.AreEqual(expected, nativeHash);
        }

        [Test]
        public void GivenUserSelection_WhenRecordedAndRetrieved_ThenCountsAccurately()
        {
            if (!FlowNativeMethods.IsAvailable) Assert.Ignore("Native library not loaded");

            FlowNativeMethods.ClearUserSelectionHistory();

            int keyWithQuery = 12345;
            int keyWithoutQuery = 67890;

            FlowNativeMethods.RecordUserSelection(keyWithQuery, keyWithoutQuery);
            int score = FlowNativeMethods.GetUserSelectedCount(keyWithQuery, keyWithoutQuery);

            // count_with_query (1) * 5 + count_without_query (1) = 6
            ClassicAssert.AreEqual(6, score);
        }
    }
}
