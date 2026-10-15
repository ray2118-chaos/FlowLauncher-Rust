#nullable enable
using System;
using System.IO;

namespace Flow.Launcher.Infrastructure
{
    public static class InternetShortcutHelper
    {
        /// <summary>
        /// Reads an Internet Shortcut (.url) file without allocating parser ASTs or regexes.
        /// </summary>
        public static bool TryRead(string filePath, out string? url, out string? iconFile)
        {
            url = null;
            iconFile = null;

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                using var reader = new StreamReader(filePath);
                string? line;
                bool inInternetShortcut = false;

                while ((line = reader.ReadLine()) != null)
                {
                    var trimmed = line.AsSpan().Trim();
                    if (trimmed.IsEmpty)
                        continue;

                    if (trimmed[0] == '[' && trimmed[^1] == ']')
                    {
                        var sectionName = trimmed[1..^1].Trim();
                        inInternetShortcut = sectionName.Equals("InternetShortcut".AsSpan(), StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    // Many .url files have [InternetShortcut] section, but some might not specify section header
                    if (inInternetShortcut || trimmed.StartsWith("URL=".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        if (url == null && trimmed.StartsWith("URL=".AsSpan(), StringComparison.OrdinalIgnoreCase))
                        {
                            url = trimmed[4..].Trim().ToString();
                        }
                        else if (iconFile == null && trimmed.StartsWith("IconFile=".AsSpan(), StringComparison.OrdinalIgnoreCase))
                        {
                            iconFile = trimmed[9..].Trim().ToString();
                        }

                        if (url != null && iconFile != null)
                            break;
                    }
                }

                return !string.IsNullOrEmpty(url);
            }
            catch
            {
                return false;
            }
        }
    }
}
