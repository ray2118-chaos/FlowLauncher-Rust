using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Image;
using NUnit.Framework;

namespace Flow.Launcher.Test
{
    [TestFixture]
    [NonParallelizable]
    public class ImageLoaderTests
    {
        private Func<string, ThumbnailOptions, int, BitmapSource> _originalShellThumbnailLoader;

        private Func<string, ThumbnailOptions, int, BitmapSource> _failingShellThumbnailLoader =
            (_, _, _) => throw new InvalidOperationException("Forced shell thumbnail failure");


        [OneTimeSetUp]
        public async Task OneTimeSetUpAsync()
        {
            await ImageLoader.InitializeAsync();

            // Explicitly load defaults by constant keys so fallback tests do not depend on cache preload timing.
            // This should be enough for the current test set, but future tests that depend on cache behavior may need DI/injection.
            _ = await ImageLoader.LoadAsync(Constant.MissingImgIcon, loadFullImage: false, cacheImage: true);
            _ = await ImageLoader.LoadAsync(Constant.FolderIcon, loadFullImage: false, cacheImage: true);

            Assert.That(ImageLoader.MissingImage, Is.Not.Null, "ImageLoader initialization must load default missing image.");
            Assert.That(ImageLoader.FolderImage, Is.Not.Null, "ImageLoader initialization must load default folder image.");
        }

        [SetUp]
        public void SetUp()
        {
            _originalShellThumbnailLoader = ImageLoader.ShellThumbnailLoader;
        }

        [TearDown]
        public void TearDown()
        {
            ImageLoader.ShellThumbnailLoader = _originalShellThumbnailLoader;
        }

        #region Missing Image Cases

        [Test]
        public async Task NonExistentPath_ReturnsMissingImageAsync()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.txt");

            var image = await ImageLoader.LoadAsync(missingPath, loadFullImage: false, cacheImage: false);

            Assert.That(image, Is.SameAs(ImageLoader.MissingImage));
            Assert.That(image.IsFrozen, Is.True);
        }

        [Test]
        public async Task NonExistentFolderPath_ReturnsMissingImageAsync()
        {
            var missingFolderPath = Path.Combine(Path.GetTempPath(), $"missing-folder-{Guid.NewGuid():N}");

            var image = await ImageLoader.LoadAsync(missingFolderPath, loadFullImage: false, cacheImage: false);

            Assert.That(image, Is.SameAs(ImageLoader.MissingImage));
            Assert.That(image.IsFrozen, Is.True);
        }

        [Test]
        public async Task InvalidSvg_ReturnsMissingImageAsync()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"image-loader-{Guid.NewGuid():N}.svg");
            await File.WriteAllTextAsync(tempPath, "not-a-valid-image");

            try
            {
                foreach (var loadFullImage in new[] { false, true })
                {
                    var image = await ImageLoader.LoadAsync(tempPath, loadFullImage, cacheImage: false);

                    Assert.That(image, Is.SameAs(ImageLoader.MissingImage));
                    Assert.That(image.IsFrozen, Is.True);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        #endregion

        #region Shell Thumbnail Failure Fallbacks

        [Test]
        public async Task ShellThumbnailFailure_Directory_ReturnsDefaultFolderImageAsync()
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), $"image-loader-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempFolder);

            try
            {
                ImageLoader.ShellThumbnailLoader = _failingShellThumbnailLoader;

                var image = await ImageLoader.LoadAsync(tempFolder, loadFullImage: false, cacheImage: false);

                Assert.That(image, Is.SameAs(ImageLoader.FolderImage));
                Assert.That(image.IsFrozen, Is.True);
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder);
                }
            }
        }

        [Test]
        public async Task ShellThumbnailFailure_ImageFile_ReturnsNonMissingImageAsync()
        {
            var defaultIconExtension = Path.GetExtension(Constant.DefaultIcon);
            Assert.That(defaultIconExtension, Is.Not.Null.And.Not.Empty, "Default icon must have a file extension.");
            Assert.That(string.Equals(defaultIconExtension, ".svg", StringComparison.OrdinalIgnoreCase), Is.False,
                "This test covers the non-SVG image-file branch.");

            var tempImagePath = Path.Combine(Path.GetTempPath(), $"image-loader-{Guid.NewGuid():N}{defaultIconExtension}");
            File.Copy(Constant.DefaultIcon, tempImagePath);

            try
            {
                ImageLoader.ShellThumbnailLoader = _failingShellThumbnailLoader;

                var image = await ImageLoader.LoadAsync(tempImagePath, loadFullImage: false, cacheImage: false);

                Assert.That(image, Is.Not.Null);
                Assert.That(image, Is.Not.SameAs(ImageLoader.MissingImage));
                Assert.That(image.IsFrozen, Is.True);
            }
            finally
            {
                if (File.Exists(tempImagePath))
                {
                    File.Delete(tempImagePath);
                }
            }
        }

        [Test]
        public async Task ShellThumbnailFailure_Executable_ReturnsNonMissingImageAsync()
        {
            // Use the current process executable as a stable existing generic file input.
            var executablePath = Environment.ProcessPath;
            Assert.That(executablePath, Is.Not.Null.And.Not.Empty, "Current process executable path is unavailable.");
            Assert.That(File.Exists(executablePath), Is.True, $"Current process executable path does not exist: {executablePath}");

            ImageLoader.ShellThumbnailLoader = _failingShellThumbnailLoader;

            var image = await ImageLoader.LoadAsync(executablePath, loadFullImage: false, cacheImage: false);

            Assert.That(image, Is.Not.Null);
            Assert.That(image, Is.Not.SameAs(ImageLoader.MissingImage));
            Assert.That(image.IsFrozen, Is.True);
        }

        [Test]
        public async Task ShellThumbnailFailure_TextFile_ReturnsNonMissingImageAsync()
        {
            var tempTextPath = Path.Combine(Path.GetTempPath(), $"image-loader-{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(tempTextPath, "fallback-test");

            try
            {
                ImageLoader.ShellThumbnailLoader = _failingShellThumbnailLoader;

                var image = await ImageLoader.LoadAsync(tempTextPath, loadFullImage: false, cacheImage: false);

                Assert.That(image, Is.Not.Null);
                Assert.That(image, Is.Not.SameAs(ImageLoader.MissingImage));
                Assert.That(image.IsFrozen, Is.True);
            }
            finally
            {
                if (File.Exists(tempTextPath))
                {
                    File.Delete(tempTextPath);
                }
            }
        }

        #endregion
    }

    [TestFixture]
    [NonParallelizable]
    public class IconPackResolutionTests
    {
        [TearDown]
        public void TearDown()
        {
            ImageLoader.ActiveIconPack = "Auto";
        }

        [Test]
        public void ResolveIconPackPath_MonochromePack_ResolvesCorrectly()
        {
            try
            {
                ImageLoader.ActiveIconPack = "Monochrome";
                var resolvedCalc = ImageLoader.ResolveIconPackPath("Images/calculator.png");
                Assert.That(resolvedCalc, Does.Contain("Monochrome"));
                Assert.That(resolvedCalc, Does.EndWith("calculator.png"));

                var resolvedApp = ImageLoader.ResolveIconPackPath("C:\\Program Files\\VSCode\\Code.exe");
                Assert.That(resolvedApp, Does.Contain("Monochrome"));
                Assert.That(resolvedApp, Does.EndWith("vscode.png"));

                var resolvedLnk = ImageLoader.ResolveIconPackPath("C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\Google Chrome.lnk");
                Assert.That(resolvedLnk, Does.Contain("Monochrome"));
                Assert.That(resolvedLnk, Does.EndWith("chrome.png"));

                var resolvedUwp = ImageLoader.ResolveIconPackPath("C:\\Program Files\\WindowsApps\\Microsoft.WindowsCalculator_8wekyb3d8bbwe\\Assets\\Square44x44Logo.png");
                Assert.That(resolvedUwp, Does.Contain("Monochrome"));
                Assert.That(resolvedUwp, Does.EndWith("calculator.png"));

                var resolvedCamera = ImageLoader.ResolveIconPackPath("C:\\Program Files\\WindowsApps\\Microsoft.WindowsCamera_8wekyb3d8bbwe\\Assets\\Square44x44Logo.png");
                Assert.That(resolvedCamera, Does.Contain("Monochrome"));
                Assert.That(resolvedCamera, Does.EndWith("camera.png"));

                var resolvedFolder = ImageLoader.ResolveIconPackPath("Images/folder.png");
                Assert.That(resolvedFolder, Does.Contain("Monochrome"));
                Assert.That(resolvedFolder, Does.EndWith("folder.png"));
            }
            finally
            {
                ImageLoader.ActiveIconPack = "Auto";
            }
        }

        [Test]
        public void ResolveIconPackPath_Windows10Pack_ResolvesCorrectly()
        {
            try
            {
                ImageLoader.ActiveIconPack = "Windows10";
                var resolvedCalc = ImageLoader.ResolveIconPackPath("calc.exe");
                Assert.That(resolvedCalc, Does.Contain("Windows10"));
                Assert.That(resolvedCalc, Does.EndWith("calculator.png"));

                var resolvedLnk = ImageLoader.ResolveIconPackPath("C:\\Users\\User\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Visual Studio Code\\Visual Studio Code.lnk");
                Assert.That(resolvedLnk, Does.Contain("Windows10"));
                Assert.That(resolvedLnk, Does.EndWith("vscode.png"));

                var resolvedWeb = ImageLoader.ResolveIconPackPath("Plugins\\Flow.Launcher.Plugin.WebSearch\\Images\\web_search.png");
                Assert.That(resolvedWeb, Does.Contain("Windows10"));
                Assert.That(resolvedWeb, Does.EndWith("Browser.png"));
            }
            finally
            {
                ImageLoader.ActiveIconPack = "Auto";
            }
        }

        [Test]
        public void ResolveIconPackPath_Windows11Pack_ResolvesCorrectly()
        {
            try
            {
                ImageLoader.ActiveIconPack = "Windows11";
                var resolvedCalc = ImageLoader.ResolveIconPackPath("calculator.png");
                Assert.That(resolvedCalc, Does.Contain("Windows11"));
                Assert.That(resolvedCalc, Does.EndWith("calculator.png"));
            }
            finally
            {
                ImageLoader.ActiveIconPack = "Auto";
            }
        }
    }
}
