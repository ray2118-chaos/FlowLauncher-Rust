using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ProgramMain = Flow.Launcher.Plugin.Program.Main;
using ProgramSettings = Flow.Launcher.Plugin.Program.Settings;
using ProgramWin32 = Flow.Launcher.Plugin.Program.Programs.Win32;
using UwpApp = Flow.Launcher.Plugin.Program.Programs.UWPApp;

namespace Flow.Launcher.Test.Plugins
{
    // TODO: Program.Main relies on static state (cache, locks, program lists, settings),
    // making it unsafe for parallel test execution and forcing these reflection workarounds. 
    // There's no better option right now,
    // but this should be replaced once dependency injection is added.
    [TestFixture]
    public class ProgramPluginTest
    {
        [TearDown]
        public void TearDown()
        {
            ResetProgramCache();
            SetStaticProperty("_settings", new ProgramSettings());
            SetStaticProperty("_win32s", new List<ProgramWin32>());
            SetStaticProperty("_uwps", new List<UwpApp>());
            SetStaticField("_win32sLock", new SemaphoreSlim(1, 1));
            SetStaticField("_uwpsLock", new SemaphoreSlim(1, 1));
            ProgramMain.UpdateProgramsSnapshot();
            SetStaticProperty("Context", null);
        }

        [Test]
        public async Task QueryAsync_DoesNotCacheCanceledResultsAsync()
        {
            using var cancellation = new CancellationTokenSource();
            var searchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            SetStaticField("_win32sLock", new SemaphoreSlim(1, 1));
            SetStaticField("_uwpsLock", new SemaphoreSlim(1, 1));
            SetStaticProperty("_settings", new ProgramSettings());
            SetStaticProperty("_win32s", new List<ProgramWin32>
            {
                new()
                {
                    Name = "Visual Studio Code",
                    Description = string.Empty,
                    FullPath = @"C:\Users\Test\AppData\Local\Programs\Microsoft VS Code\Code.exe",
                    IcoPath = @"C:\Users\Test\AppData\Local\Programs\Microsoft VS Code\Code.exe",
                    ParentDirectory = @"C:\Users\Test\AppData\Local\Programs\Microsoft VS Code",
                    UniqueIdentifier = @"C:\Users\Test\AppData\Local\Programs\Microsoft VS Code\Code.exe",
                    Valid = true,
                    Enabled = true
                }
            });
            SetStaticProperty("_uwps", new List<UwpApp>());
            SetStaticProperty("Context", new PluginInitContext { API = CreateApi(cancellation, searchStarted).Object });
            ResetProgramCache();

            var plugin = new ProgramMain();
            var query = new Query
            {
                Search = "vsc",
                SearchTerms = ["vsc"],
                TrimmedQuery = "vsc",
                ActionKeyword = string.Empty
            };

            var canceledQuery = plugin.QueryAsync(query, cancellation.Token);
            await searchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await cancellation.CancelAsync();

            var canceledResults = await canceledQuery;
            ClassicAssert.AreEqual(0, canceledResults.Count);

            var results = await plugin.QueryAsync(query, CancellationToken.None);

            ClassicAssert.AreEqual(1, results.Count);
            ClassicAssert.AreEqual("Visual Studio Code", results[0].Title);
        }

        [Test]
        public async Task QueryAsync_BoostsProgramByCustomAliasAsync()
        {
            var settings = new ProgramSettings
            {
                CustomShortcuts = new List<Flow.Launcher.Plugin.Program.AppCustomShortcut>
                {
                    new()
                    {
                        Name = "Brave Browser",
                        UniqueIdentifier = @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
                        Alias = "br",
                        Enabled = true
                    }
                }
            };

            SetStaticField("_win32sLock", new SemaphoreSlim(1, 1));
            SetStaticField("_uwpsLock", new SemaphoreSlim(1, 1));
            SetStaticProperty("_settings", settings);
            SetStaticProperty("_win32s", new List<ProgramWin32>
            {
                new()
                {
                    Name = "Brave Browser",
                    Description = string.Empty,
                    FullPath = @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
                    IcoPath = @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
                    ParentDirectory = @"C:\Program Files\BraveSoftware\Brave-Browser\Application",
                    UniqueIdentifier = @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
                    Valid = true,
                    Enabled = true
                }
            });
            SetStaticProperty("_uwps", new List<UwpApp>());

            var apiMock = new Mock<IPublicAPI>();
            apiMock.Setup(x => x.FuzzySearch(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new MatchResult(true, SearchPrecisionScore.Regular, new List<int>(), 50));

            SetStaticProperty("Context", new PluginInitContext { API = apiMock.Object });
            ProgramMain.UpdateProgramsSnapshot();
            ResetProgramCache();

            var plugin = new ProgramMain();
            var query = new Query
            {
                Search = "br",
                SearchTerms = ["br"],
                TrimmedQuery = "br",
                ActionKeyword = string.Empty
            };

            var results = await plugin.QueryAsync(query, CancellationToken.None);

            ClassicAssert.AreEqual(1, results.Count);
            ClassicAssert.AreEqual("Brave Browser", results[0].Title);
            ClassicAssert.IsTrue(results[0].Score >= 200, "Alias match should be boosted to at least 200");
            ClassicAssert.IsTrue(results[0].SubTitle.Contains("Alias: br"));
        }

        private static Mock<IPublicAPI> CreateApi(CancellationTokenSource cancellation, TaskCompletionSource<bool> searchStarted)
        {
            var callCount = 0;
            var api = new Mock<IPublicAPI>();
            api.Setup(x => x.FuzzySearch("vsc", "Visual Studio Code"))
                .Returns(() =>
                {
                    if (Interlocked.Increment(ref callCount) == 1)
                    {
                        searchStarted.SetResult(true);
                        if (!SpinWait.SpinUntil(() => cancellation.IsCancellationRequested, TimeSpan.FromSeconds(5)))
                            throw new AssertionException("Timed out waiting for query cancellation.");

                        throw new OperationCanceledException(cancellation.Token);
                    }

                    return new MatchResult(true, SearchPrecisionScore.None, [0, 1, 2], 100);
                });
            return api;
        }

        private static void SetStaticProperty(string name, object value)
        {
            var property = typeof(ProgramMain).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Static);
            if (property == null)
                Assert.Fail($"Could not find static property '{name}' on Program plugin Main.");

            property.SetValue(null, value);
        }

        private static void SetStaticField(string name, object value)
        {
            var field = typeof(ProgramMain).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
                Assert.Fail($"Could not find static field '{name}' on Program plugin Main.");

            field.SetValue(null, value);
        }

        private static void ResetProgramCache()
        {
            var method = typeof(ProgramMain).GetMethod("ResetCache", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                Assert.Fail("Could not find static method 'ResetCache' on Program plugin Main.");

            method.Invoke(null, null);
        }
    }
}
