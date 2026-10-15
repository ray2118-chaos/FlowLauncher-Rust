using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace Flow.Launcher.Infrastructure.Image
{
    public static class ImageLoader
    {
        private static readonly string ClassName = nameof(ImageLoader);

        private static readonly ImageCache ImageCache = new();
        private static Lock storageLock { get; } = new();
        private static BinaryStorage<List<(string, bool)>> _storage;
        private static readonly ConcurrentDictionary<string, string> GuidToKey = new();
        private static ImageHashGenerator _hashGenerator;
        private static readonly bool EnableImageHash = true;
        public static ImageSource MissingImage => ImageCache[Constant.MissingImgIcon, false];
        public static ImageSource LoadingImage => ImageCache[Constant.LoadingImgIcon, false];
        public static ImageSource FolderImage => ImageCache[ResolveIconPackPath(Constant.FolderIcon), false] ?? ImageCache[Constant.FolderIcon, false];
        public const int SmallIconSize = 64;
        public const int FullIconSize = 256;
        public const int FullImageSize = 320;

        public static string ActiveIconPack { get; set; } = "Auto";

        public static void UpdateIconPack(string iconPack)
        {
            ActiveIconPack = string.IsNullOrWhiteSpace(iconPack) ? "Auto" : iconPack;
            ClearCache();
        }

        public static void ClearCache()
        {
            ImageCache.Clear();
            GuidToKey.Clear();
            try
            {
                foreach (var icon in new[] { Constant.DefaultIcon, Constant.MissingImgIcon, Constant.LoadingImgIcon, Constant.FolderIcon })
                {
                    if (File.Exists(icon))
                    {
                        var resolved = ResolveIconPackPath(icon);
                        ImageSource img = new BitmapImage(new Uri(resolved));
                        img.Freeze();
                        ImageCache[icon, false] = img;
                        ImageCache[resolved, false] = img;
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.Exception(ClassName, "Failed to re-populate default icons on cache clear", e);
            }
        }

        public static string GetEffectiveIconPack()
        {
            string pack = null;
            if (!string.IsNullOrEmpty(ActiveIconPack) && !ActiveIconPack.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                pack = ActiveIconPack;
            }
            else
            {
                try
                {
                    if (Application.Current?.Resources != null && Application.Current.Resources.Contains("ThemeIconPack"))
                    {
                        pack = Application.Current.Resources["ThemeIconPack"] as string;
                    }
                }
                catch
                {
                    // Fallback if WPF Application context is not available
                }
            }

            if (string.IsNullOrWhiteSpace(pack))
            {
                return "Default";
            }

            var normalized = pack.Replace(" ", "");
            if (normalized.StartsWith("Monochrome", StringComparison.OrdinalIgnoreCase))
                return "Monochrome";
            if (normalized.StartsWith("Default", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Windows11", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Win11", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Windows10", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Win10", StringComparison.OrdinalIgnoreCase))
                return "Default";

            return normalized;
        }

        public static string ResolveIconPackPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            var pack = GetEffectiveIconPack();
            if (string.IsNullOrEmpty(pack))
                return path;

            string packDir = Path.Combine(Constant.ProgramDirectory, "Images", "IconPacks", pack);
            if (!Directory.Exists(packDir))
            {
                string fallbackDir = Path.Combine(AppContext.BaseDirectory, "Images", "IconPacks", pack);
                if (Directory.Exists(fallbackDir))
                {
                    packDir = fallbackDir;
                }
                else
                {
                    return path;
                }
            }

            string fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
                return path;

            // Direct file match in Icon Pack (e.g. calculator.png, settings.png, folder.png, etc.)
            string candidate = Path.Combine(packDir, fileName);
            if (File.Exists(candidate))
                return candidate;

            string nameWithoutExt = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            string lowerName = fileName.ToLowerInvariant();
            string lowerPath = path.ToLowerInvariant();

            // Direct match by file name without extension + .png
            string directExtCandidate = Path.Combine(packDir, nameWithoutExt + ".png");
            if (File.Exists(directExtCandidate))
                return directExtCandidate;

            // App executable, shortcut (.lnk), UWP asset, and plugin image mapping
            string iconName = null;

            if (lowerName == "camera.exe" || nameWithoutExt == "camera" || lowerPath.Contains("windowscamera") || lowerPath.Contains("camera.lnk") || lowerName.Contains("webcam"))
            {
                iconName = "camera.png";
            }
            else if (lowerName == "calc.exe" || lowerName == "calculatorapp.exe" || nameWithoutExt == "calculator" || nameWithoutExt == "calc" ||
                lowerPath.Contains("windowscalculator") || lowerPath.Contains("calculator.lnk") || lowerPath.Contains("calc.lnk"))
            {
                iconName = "calculator.png";
            }
            else if (lowerName == "chrome.exe" || nameWithoutExt == "chrome" || lowerPath.Contains("google chrome") || lowerPath.Contains("chrome.lnk"))
            {
                iconName = "chrome.png";
            }
            else if (lowerName == "code.exe" || nameWithoutExt == "code" || lowerPath.Contains("visual studio code") || lowerPath.Contains("vscode"))
            {
                iconName = "vscode.png";
            }
            else if (lowerName == "spotify.exe" || nameWithoutExt == "spotify" || lowerPath.Contains("spotify"))
            {
                iconName = "spotify.png";
            }
            else if (lowerName == "systemsettings.exe" || lowerName == "settings.exe" || nameWithoutExt == "settings" ||
                     lowerPath.Contains("windowssettings") || lowerPath.Contains("systemsettings") || lowerPath.Contains("settings.lnk"))
            {
                iconName = "settings.png";
            }
            else if (lowerName == "wt.exe" || lowerPath.Contains("windows terminal"))
            {
                iconName = File.Exists(Path.Combine(packDir, "terminal.png")) ? "terminal.png" : "cmd.png";
            }
            else if (lowerName == "cmd.exe" || lowerName == "powershell.exe" ||
                     lowerPath.Contains("command prompt") || lowerPath.Contains("powershell"))
            {
                iconName = "cmd.png";
            }
            else if (lowerName == "explorer.exe" || lowerPath.Contains("file explorer") || lowerPath.Contains("explorer.lnk"))
            {
                iconName = File.Exists(Path.Combine(packDir, "explorer.png")) ? "explorer.png" : "folder.png";
            }
            else if (lowerName == "photos.exe" || nameWithoutExt == "photos" || lowerPath.Contains("windowsphotos") || lowerPath.Contains("photoviewer") || lowerPath.Contains("gallery"))
            {
                iconName = "photos.png";
            }
            else if (lowerName == "music.exe" || lowerName == "groove.exe" || lowerName == "wmplayer.exe" || nameWithoutExt == "music" || lowerPath.Contains("zunemusic") || lowerPath.Contains("groove"))
            {
                iconName = "music.png";
            }
            else if (lowerName == "video.exe" || lowerName == "movies.exe" || lowerName == "vlc.exe" || nameWithoutExt == "video" || lowerPath.Contains("zunevideo") || lowerPath.Contains("movies"))
            {
                iconName = "video.png";
            }
            else if (lowerName == "mail.exe" || lowerName == "outlook.exe" || lowerName == "thunderbird.exe" || nameWithoutExt == "mail" || lowerPath.Contains("windowsmail") || lowerPath.Contains("outlook"))
            {
                iconName = "mail.png";
            }
            else if (lowerName == "calendar.exe" || nameWithoutExt == "calendar" || lowerPath.Contains("windowscalendar") || lowerPath.Contains("calendar.lnk"))
            {
                iconName = "calendar.png";
            }
            else if (lowerName == "clock.exe" || lowerName == "time.exe" || nameWithoutExt == "clock" || lowerPath.Contains("windowsclock") || lowerPath.Contains("alarms"))
            {
                iconName = "clock.png";
            }
            else if (lowerName == "notepad.exe" || lowerName == "notepad++.exe" || nameWithoutExt == "notepad" || lowerPath.Contains("notepad"))
            {
                iconName = "notepad.png";
            }
            else if (lowerName == "mspaint.exe" || lowerName == "paint.exe" || nameWithoutExt == "paint" || lowerPath.Contains("paint"))
            {
                iconName = "paint.png";
            }
            else if (lowerName == "weather.exe" || nameWithoutExt == "weather" || lowerPath.Contains("bingweather") || lowerPath.Contains("weather.lnk"))
            {
                iconName = "weather.png";
            }
            else if (lowerName == "maps.exe" || nameWithoutExt == "maps" || lowerPath.Contains("windowsmaps") || lowerPath.Contains("maps.lnk"))
            {
                iconName = "maps.png";
            }
            else if (lowerName == "winstore.app.exe" || lowerName == "store.exe" || nameWithoutExt == "store" || lowerPath.Contains("windowsstore") || lowerPath.Contains("store.lnk"))
            {
                iconName = "store.png";
            }
            else if (lowerName.Contains("soundrecorder") || lowerName.Contains("voicerecorder") || nameWithoutExt == "voice" || nameWithoutExt == "mic")
            {
                iconName = "voice.png";
            }
            else if (lowerName == "snippingtool.exe" || lowerName.Contains("screensketch") || lowerPath.Contains("snippingtool") || lowerName == "snip")
            {
                iconName = "snippingtool.png";
            }
            else if (lowerName == "taskmgr.exe" || nameWithoutExt == "taskmgr" || lowerPath.Contains("taskmanager") || lowerPath.Contains("taskmgr"))
            {
                iconName = "taskmanager.png";
            }
            else if (lowerName == "control.exe" || nameWithoutExt == "control" || lowerPath.Contains("control panel") || lowerPath.Contains("controlpanel"))
            {
                iconName = "controlpanel.png";
            }
            else if (lowerName == "winword.exe" || nameWithoutExt == "document" || lowerPath.Contains("documents") || lowerPath.Contains("word.lnk"))
            {
                iconName = "document.png";
            }
            else if (lowerPath.EndsWith("downloads") || lowerPath.Contains("\\downloads\\"))
            {
                iconName = "download.png";
            }
            else if (lowerPath.EndsWith("desktop") || lowerPath.Contains("\\desktop\\"))
            {
                iconName = "desktop.png";
            }
            else if (lowerPath.Contains("recyclebin") || lowerPath.Contains("recycle bin") || lowerName.Contains("recycle"))
            {
                iconName = "recyclebin.png";
            }
            else if (lowerName == "web_search.png" || lowerName == "browser.png" || lowerPath.Contains("web_search") || lowerName.Contains("copylink"))
            {
                iconName = "Browser.png";
            }
            else if (lowerName == "app.png" || lowerName == "app_error.png" || lowerName == "app_missing_img.png")
            {
                iconName = "app.png";
            }
            else if (lowerName.Contains("shutdown"))
            {
                iconName = "shutdown.png";
            }
            else if (lowerName.Contains("restart"))
            {
                iconName = "restart.png";
            }
            else if (lowerName.Contains("sleep") || lowerName.Contains("hibernate"))
            {
                iconName = "sleep.png";
            }
            else if (lowerName.Contains("lock") || lowerName.Contains("logoff"))
            {
                iconName = "lock.png";
            }
            else if (lowerName.Contains("history"))
            {
                iconName = "history.png";
            }
            else if (lowerName.Contains("find") || lowerName.Contains("search"))
            {
                iconName = "find.png";
            }

            if (iconName != null)
            {
                string iconCandidate = Path.Combine(packDir, iconName);
                if (File.Exists(iconCandidate))
                    return iconCandidate;
            }

            return path;
        }

        private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico"];
        private static readonly string SvgExtension = ".svg";
        internal static Func<string, ThumbnailOptions, int, BitmapSource> ShellThumbnailLoader { get; set; } =
            (path, option, size) =>
                WindowsThumbnailProvider.GetThumbnail(
                    path,
                    size,
                    size,
                    option);

        public static async Task InitializeAsync()
        {
            var usage = await Task.Run(() =>
            {
                _storage = new BinaryStorage<List<(string, bool)>>("Image");
                _hashGenerator = new ImageHashGenerator();

                var usage = LoadStorageToConcurrentDictionary();
                _storage.ClearData();

                ImageCache.Initialize(usage);

                foreach (var icon in new[] { Constant.DefaultIcon, Constant.MissingImgIcon, Constant.LoadingImgIcon, Constant.FolderIcon })
                {
                    var resolved = ResolveIconPackPath(icon);
                    ImageSource img = new BitmapImage(new Uri(resolved));
                    img.Freeze();
                    ImageCache[icon, false] = img;
                    if (!string.Equals(icon, resolved, StringComparison.OrdinalIgnoreCase))
                    {
                        ImageCache[resolved, false] = img;
                    }
                }

                return usage;
            });

            _ = Task.Run(async () =>
            {
                await Stopwatch.InfoAsync(ClassName, "Preload images cost", async () =>
                {
                    foreach (var (path, isFullImage) in usage)
                    {
                        await LoadAsync(path, isFullImage);
                    }
                });
                Log.Info(ClassName, $"Number of preload images is <{ImageCache.CacheSize()}>, Images Number: {ImageCache.CacheSize()}, Unique Items {ImageCache.UniqueImagesInCache()}");
            });
        }

        public static void Save()
        {
            lock (storageLock)
            {
                try
                {
                    _storage.Save([.. ImageCache.EnumerateEntries().Select(x => x.Key)]);
                }
                catch (System.Exception e)
                {
                    Log.Exception(ClassName, "Failed to save image cache to file", e);
                }
            }
        }

        private static List<(string, bool)> LoadStorageToConcurrentDictionary()
        {
            lock (storageLock)
            {
                return _storage.TryLoad([]);
            }
        }

        private class ImageResult
        {
            public ImageResult(ImageSource imageSource, ImageType imageType)
            {
                ImageSource = imageSource;
                ImageType = imageType;
            }

            public ImageType ImageType { get; }
            public ImageSource ImageSource { get; }
        }

        private enum ImageType
        {
            File,
            Folder,
            Data,
            ImageFile,
            FullImageFile,
            Error,
            Cache
        }

        private static async ValueTask<ImageResult> LoadInternalAsync(string path, bool loadFullImage = false)
        {
            ImageResult imageResult;

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return new ImageResult(MissingImage, ImageType.Error);
                }

                path = ResolveIconPackPath(path);

                // extra scope for use of same variable name
                {
                    if (ImageCache.TryGetValue(path, loadFullImage, out var imageSource))
                    {
                        return new ImageResult(imageSource, ImageType.Cache);
                    }
                }

                if (Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out var uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    var image = await LoadRemoteImageAsync(loadFullImage, uriResult);
                    ImageCache[path, loadFullImage] = image;
                    return new ImageResult(image, ImageType.ImageFile);
                }

                if (path.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                {
                    var imageSource = new BitmapImage(new Uri(path));
                    imageSource.Freeze();
                    return new ImageResult(imageSource, ImageType.Data);
                }

                imageResult = await Task.Run(() => GetThumbnailResult(path, loadFullImage));
            }
            catch (System.Exception e)
            {
                try
                {
                    // Get thumbnail may fail for certain images on the first try, retry again has proven to work
                    imageResult = GetThumbnailResult(path, loadFullImage);
                }
                catch (System.Exception e2)
                {
                    Log.Warn(ClassName, $"Failed to get thumbnail for {path} on first try: {e.Message}");
                    Log.Warn(ClassName, $"Failed to get thumbnail for {path} on second try: {e2.Message}");

                    ImageSource image = MissingImage;
                    ImageCache[path, false] = image;
                    imageResult = new ImageResult(image, ImageType.Error);
                }
            }

            return imageResult;
        }

        private static async Task<BitmapImage> LoadRemoteImageAsync(bool loadFullImage, Uri uriResult)
        {
            // Download image from url
            await using var resp = await Http.Http.GetStreamAsync(uriResult);
            await using var buffer = new MemoryStream();
            await resp.CopyToAsync(buffer);
            buffer.Seek(0, SeekOrigin.Begin);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (!loadFullImage)
            {
                image.DecodePixelHeight = SmallIconSize;
                image.DecodePixelWidth = SmallIconSize;
            }

            image.StreamSource = buffer;
            image.EndInit();
            image.StreamSource = null;
            image.Freeze();
            return image;
        }

        private static ImageResult GetThumbnailResult(string path, bool loadFullImage = false)
        {
            if (Directory.Exists(path))
                return GetDirectoryThumbnailResult(path, loadFullImage);

            if (!File.Exists(path))
                return GetMissingThumbnailResult();

            var extension = Path.GetExtension(path).ToLower();

            if (ImageExtensions.Contains(extension))
                return GetImageFileThumbnailResult(path, loadFullImage);

            if (extension == SvgExtension)
                return GetSvgFileThumbnailResult(path, loadFullImage);

            return GetFileThumbnailResult(path, loadFullImage);
        }

        private static ImageResult CreateImageResult(ImageSource image, ImageType type)
        {
            if (type != ImageType.Error && !image.IsFrozen)
            {
                image.Freeze();
            }

            return new ImageResult(image, type);
        }

        private static ImageResult GetMissingThumbnailResult()
        {
            return CreateImageResult(MissingImage, ImageType.Error);
        }

        private static ImageResult GetDirectoryThumbnailResult(string path, bool loadFullImage)
        {
            var pack = GetEffectiveIconPack();
            if (pack.Equals("Monochrome", StringComparison.OrdinalIgnoreCase))
            {
                return CreateImageResult(FolderImage, ImageType.Folder);
            }

            var size = loadFullImage ? FullIconSize : SmallIconSize;
            try
            {
                /* Directories can also have thumbnails instead of shell icons.
                 * Generating thumbnails for a bunch of folder results while scrolling
                 * could have a big impact on performance and Flow.Launcher responsibility.
                 * - Solution: just load the icon
                 */
                var image = GetShellThumbnail(path, ThumbnailOptions.IconOnly, size);
                return CreateImageResult(image, ImageType.Folder);
            }
            catch (System.Exception ex)
            {
                Log.Warn(ClassName, $"Failed to get shell thumbnail for folder {path}: {ex.Message}\nUsing default folder image as fallback.");
                return CreateImageResult(FolderImage, ImageType.Folder);
            }
        }

        private static ImageResult GetImageFileThumbnailResult(string path, bool loadFullImage)
        {
            if (loadFullImage)
            {
                try
                {
                    var image = LoadBitmapImageScaleToFitWithin(path, FullImageSize);
                    return CreateImageResult(image, ImageType.FullImageFile);
                }
                catch (NotSupportedException ex)
                {
                    Log.Warn(ClassName, $"Failed to load image file from path {path}: {ex.Message}\nUsing missing icon instead.");
                    return GetMissingThumbnailResult();
                }
            }

            try
            {
                /* Although the documentation for GetImage on MSDN indicates that
                 * if a thumbnail is available it will return one, this has proved to not
                 * be the case in many situations while testing.
                 * - Solution: explicitly pass the ThumbnailOnly flag
                 */
                var image = GetShellThumbnail(path, ThumbnailOptions.ThumbnailOnly);
                return CreateImageResult(image, ImageType.ImageFile);
            }
            catch (System.Exception ex)
            {
                Log.Debug(ClassName, $"Failed to get shell thumbnail for image file {path}: {ex.Message}\nTrying bitmap fallback.");

                try
                {
                    var image = LoadBitmapImageScaleToFitWithin(path, SmallIconSize);
                    return CreateImageResult(image, ImageType.ImageFile);
                }
                catch (System.Exception ex2)
                {
                    Log.Warn(ClassName, $"Failed to load image file from path {path}: {ex2.Message}\nUsing missing icon instead.");
                    return GetMissingThumbnailResult();
                }
            }
        }

        private static ImageResult GetSvgFileThumbnailResult(string path, bool loadFullImage)
        {
            try
            {
                var image = LoadSvgImage(path, loadFullImage);
                return CreateImageResult(image, ImageType.FullImageFile);
            }
            catch (System.Exception ex)
            {
                Log.Warn(ClassName, $"Failed to load SVG image from path {path}: {ex.Message}\nUsing missing icon instead.");
                return GetMissingThumbnailResult();
            }
        }

        private static ImageResult GetFileThumbnailResult(string path, bool loadFullImage)
        {
            var size = loadFullImage ? FullIconSize : SmallIconSize;
            try
            {
                var image = GetShellThumbnail(path, ThumbnailOptions.None, size);
                return CreateImageResult(image, ImageType.File);
            }
            catch (System.Exception ex)
            {
                Log.Debug(ClassName, $"Failed to get shell thumbnail for {path}: {ex.Message}\nTrying ExtractAssociatedIcon fallback.");

                if (TryExtractAssociatedIcon(path, size, out var image))
                {
                    return CreateImageResult(image, ImageType.File);
                }

                Log.Warn(ClassName, $"ExtractAssociatedIcon returned no icon for path: {path}\nUsing missing icon instead.");
                return GetMissingThumbnailResult();
            }
        }

        private static BitmapSource GetShellThumbnail(string path, ThumbnailOptions option = ThumbnailOptions.ThumbnailOnly,
            int size = SmallIconSize)
        {
            return ShellThumbnailLoader(path, option, size);
        }

        private static bool TryExtractAssociatedIcon(string path, int size, out BitmapSource image)
        {
            image = null;

            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon == null)
                {
                    return false;
                }

                image = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(size, size));
                image.Freeze();
                return true;
            }
            catch
            {
                image = null;
                return false;
            }
        }

        public static bool CacheContainImage(string path, bool loadFullImage = false)
        {
            return ImageCache.ContainsKey(path, loadFullImage);
        }

        public static bool TryGetValue(string path, bool loadFullImage, out ImageSource image)
        {
            return ImageCache.TryGetValue(path, loadFullImage, out image);
        }

        public static async ValueTask<ImageSource> LoadAsync(string path, bool loadFullImage = false, bool cacheImage = true)
        {
            string resolvedPath = ResolveIconPackPath(path);
            var imageResult = await LoadInternalAsync(resolvedPath, loadFullImage);

            var img = imageResult.ImageSource;
            if (imageResult.ImageType != ImageType.Error && imageResult.ImageType != ImageType.Cache)
            {
                // we need to get image hash
                string hash = EnableImageHash ? _hashGenerator.GetHashFromImage(img) : null;
                if (hash != null)
                {
                    if (GuidToKey.TryGetValue(hash, out string key))
                    {
                        // image already exists
                        img = ImageCache[key, loadFullImage] ?? img;
                    }
                    else if (cacheImage)
                    {
                        // save guid key
                        GuidToKey[hash] = resolvedPath;
                    }
                }

                if (cacheImage)
                {
                    // update cache
                    ImageCache[path, loadFullImage] = img;
                    if (!string.Equals(path, resolvedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        ImageCache[resolvedPath, loadFullImage] = img;
                    }
                }
            }

            return img;
        }

        private static bool TryGetBitmapImageDimensionsFromMetadata(string path, out int width, out int height)
        {
            width = 0;
            height = 0;

            try
            {
                using var stream = File.OpenRead(path);
                var decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.DelayCreation,
                    BitmapCacheOption.None);

                var frame = decoder.Frames.FirstOrDefault();
                if (frame is null)
                    return false;

                width = frame.PixelWidth;
                height = frame.PixelHeight;
                return width > 0 && height > 0;
            }
            catch
            {
                return false;
            }
        }

        private static BitmapImage LoadBitmapImageScaleToFitWithin(string path, int maxSize)
        {
            BitmapImage decodedImage = null;

            // try to get the image's dimensions from metadata before fully decoding the image
            bool metadataReadSucceeded = TryGetBitmapImageDimensionsFromMetadata(path, out var width, out var height);

            // if we couldn't read the metadata then fully load the image and get dimensions from that
            if (!metadataReadSucceeded)
            {
                decodedImage = LoadBitmapImage(path);
                width = decodedImage.PixelWidth;
                height = decodedImage.PixelHeight;
            }

            // If resizing is unnecessary, return the original image
            // (reusing the already decoded image if available).
            if (width <= maxSize && height <= maxSize)
            {
                return decodedImage ?? LoadBitmapImage(path);
            }

            bool widthIsLarger = width >= height;

            // LoadBitmapImage will maintain aspect ratio so we only need to scale by the largest dimension
            if (widthIsLarger)
            {
                return LoadBitmapImage(path, decodePixelWidth: maxSize);
            }
            else
            {
                return LoadBitmapImage(path, decodePixelHeight: maxSize);
            }
        }

        private static BitmapImage LoadBitmapImage(string path, int? decodePixelWidth = null, int? decodePixelHeight = null)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

            if (decodePixelWidth.HasValue)
            {
                image.DecodePixelWidth = decodePixelWidth.Value;
            }

            if (decodePixelHeight.HasValue)
            {
                image.DecodePixelHeight = decodePixelHeight.Value;
            }

            image.EndInit();
            return image;
        }

        private static RenderTargetBitmap LoadSvgImage(string path, bool loadFullImage = false)
        {
            // Set up drawing settings
            var desiredHeight = loadFullImage ? FullImageSize : SmallIconSize;
            var drawingSettings = new WpfDrawingSettings
            {
                IncludeRuntime = true,
                // Set IgnoreRootViewbox to false to respect the SVG's viewBox
                IgnoreRootViewbox = false
            };

            // Load and render the SVG
            var converter = new FileSvgReader(drawingSettings);
            var drawing = converter.Read(new Uri(path));

            // Calculate scale to achieve desired height
            var drawingBounds = drawing.Bounds;
            if (drawingBounds.Height <= 0)
            {
                throw new InvalidOperationException($"Invalid SVG dimensions: Height must be greater than zero in {path}");
            }
            var scale = desiredHeight / drawingBounds.Height;
            var scaledWidth = drawingBounds.Width * scale;
            var scaledHeight = drawingBounds.Height * scale;

            // Convert the Drawing to a Bitmap
            var drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.PushTransform(new ScaleTransform(scale, scale));
                drawingContext.DrawDrawing(drawing);
            }

            // Create a RenderTargetBitmap to hold the rendered image
            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(scaledWidth),
                (int)Math.Ceiling(scaledHeight),
                96, // DpiX
                96, // DpiY
                PixelFormats.Pbgra32);
            bitmap.Render(drawingVisual);

            return bitmap;
        }
    }
}
