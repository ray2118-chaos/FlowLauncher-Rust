using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Flow.Launcher.Plugin.Sys
{
    public enum HudIconType
    {
        Volume,
        VolumeMuted,
        Brightness,
    }

    public class SysHudOverlay : Window
    {
        private static SysHudOverlay? _currentInstance;
        private static readonly object _lock = new();

        private readonly Border _rootBorder;
        private readonly Border _badgeBorder;
        private readonly TextBlock _textBlock;
        private readonly TextBlock _iconBlock;
        private readonly Ellipse _statusDot;
        private readonly DispatcherTimer _hideTimer;
        private readonly TranslateTransform _translate;

        private SysHudOverlay()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            SizeToContent = SizeToContent.WidthAndHeight;

            _translate = new TranslateTransform(0, 14);

            // Root Capsule container
            _rootBorder = new Border
            {
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 9, 20, 9),
                RenderTransform = _translate,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 18,
                    Opacity = 0.45,
                    ShadowDepth = 3,
                }
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // Badge container
            var badgeGrid = new Grid
            {
                Width = 36,
                Height = 36,
                Margin = new Thickness(0, 0, 14, 0),
            };

            _badgeBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
            };
            badgeGrid.Children.Add(_badgeBorder);

            // Icon glyph (Segoe Fluent Icons / Segoe MDL2 Assets)
            _iconBlock = new TextBlock
            {
                FontFamily = new FontFamily("/Resources/#Segoe Fluent Icons, Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 18,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Text = "\xe767", // Volume default
            };
            badgeGrid.Children.Add(_iconBlock);

            // Status dot in bottom right of badge
            _statusDot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38D39F")),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 1, 1),
            };
            badgeGrid.Children.Add(_statusDot);

            stack.Children.Add(badgeGrid);

            // Text
            _textBlock = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI, Aptos, sans-serif"),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Text = "Volume set to 0%",
            };
            stack.Children.Add(_textBlock);

            _rootBorder.Child = stack;
            Content = _rootBorder;

            ApplyThemeColors();

            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1800),
            };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                FadeOutAndClose();
            };
        }

        private static Brush? ResolveBrush(object? value)
        {
            if (value is Brush b && b != Brushes.Transparent) return b;
            if (value is System.Windows.DynamicResourceExtension dre)
            {
                return Application.Current?.TryFindResource(dre.ResourceKey) as Brush;
            }
            return null;
        }

        private static Color GetBrushColor(Brush? brush, Color fallback)
        {
            if (brush is SolidColorBrush scb) return scb.Color;
            if (brush is LinearGradientBrush lgb && lgb.GradientStops.Count > 0)
                return lgb.GradientStops[0].Color;
            return fallback;
        }

        private static bool TryExtractThemeColorsFromFile(string themeName, out Color bgColor, out Color borderColor, out bool isThemeDark)
        {
            bgColor = Colors.Transparent;
            borderColor = Colors.Transparent;
            isThemeDark = true;

            try
            {
                string[] searchDirs =
                [
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes"),
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowLauncher", "Themes")
                ];

                string? foundPath = null;
                foreach (var dir in searchDirs)
                {
                    var p = System.IO.Path.Combine(dir, $"{themeName}.xaml");
                    if (File.Exists(p)) { foundPath = p; break; }
                }

                if (foundPath == null) return false;

                var content = File.ReadAllText(foundPath);

                if (content.Contains("IsDark: False", StringComparison.OrdinalIgnoreCase))
                    isThemeDark = false;
                else if (content.Contains("IsDark: True", StringComparison.OrdinalIgnoreCase))
                    isThemeDark = true;
                else if (System.Text.RegularExpressions.Regex.IsMatch(content, @"x:Key=""SystemBG"">Light<", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    isThemeDark = false;

                // Extract BorderBrush from WindowBorderStyle
                var borderMatch = System.Text.RegularExpressions.Regex.Match(content, @"x:Key=""WindowBorderStyle""[^>]*>.*?Property=""BorderBrush""\s+Value=""([^""}]+)""", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (borderMatch.Success)
                {
                    var raw = borderMatch.Groups[1].Value.Trim();
                    if (ColorConverter.ConvertFromString(raw) is Color bc)
                        borderColor = bc;
                }

                // Extract Background from WindowBorderStyle
                var bgMatch = System.Text.RegularExpressions.Regex.Match(content, @"x:Key=""WindowBorderStyle""[^>]*>.*?Property=""Background""\s+Value=""([^""}]+)""", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (bgMatch.Success)
                {
                    var raw = bgMatch.Groups[1].Value.Trim();
                    if (ColorConverter.ConvertFromString(raw) is Color bgc && bgc.A > 0)
                        bgColor = bgc;
                }

                // Fallback to LightBG / DarkBG
                if (bgColor.A == 0)
                {
                    var targetKey = isThemeDark ? "DarkBG" : "LightBG";
                    var bgKeyMatch = System.Text.RegularExpressions.Regex.Match(content, $@"x:Key=""{targetKey}"">([^<]+)<");
                    if (bgKeyMatch.Success)
                    {
                        var raw = bgKeyMatch.Groups[1].Value.Trim();
                        if (ColorConverter.ConvertFromString(raw) is Color kbg)
                            bgColor = kbg;
                    }
                }

                return bgColor.A > 0;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyThemeColors()
        {
            try
            {
                Color baseColor = Color.FromRgb(30, 32, 38);
                Color? borderColor = null;
                Brush? accentBrush = null;
                bool isDark = true;
                bool foundFromFile = false;

                // 1. Inquire Flow Launcher Public API for the active theme metadata
                var currentTheme = Main.Context?.API?.GetCurrentTheme();
                if (currentTheme != null)
                {
                    if (currentTheme.IsDark.HasValue)
                    {
                        isDark = currentTheme.IsDark.Value;
                    }

                    // Direct extraction from active theme file (guarantees authentic non-transparent theme values)
                    var themeName = currentTheme.FileNameWithoutExtension ?? currentTheme.Name;
                    if (!string.IsNullOrEmpty(themeName) &&
                        TryExtractThemeColorsFromFile(themeName, out var fileBg, out var fileBorder, out var fileDark))
                    {
                        baseColor = fileBg;
                        if (fileBorder.A > 0) borderColor = fileBorder;
                        if (!currentTheme.IsDark.HasValue) isDark = fileDark;
                        foundFromFile = true;
                    }
                }

                // 2. ResourceDictionary fallbacks if not found from file
                if (!foundFromFile && Application.Current != null)
                {
                    var systemBg = Application.Current.TryFindResource("SystemBG") as string;
                    if (string.Equals(systemBg, "Light", StringComparison.OrdinalIgnoreCase))
                        isDark = false;
                    else if (string.Equals(systemBg, "Dark", StringComparison.OrdinalIgnoreCase))
                        isDark = true;

                    var targetKey = isDark ? "DarkBG" : "LightBG";
                    var rawBg = Application.Current.TryFindResource(targetKey)
                                ?? Application.Current.TryFindResource("LightBG")
                                ?? Application.Current.TryFindResource("DarkBG");

                    if (rawBg is Color c && c.A > 0)
                    {
                        baseColor = c;
                    }
                    else if (rawBg is SolidColorBrush scb && scb.Color.A > 0)
                    {
                        baseColor = scb.Color;
                    }
                    else if (Application.Current.TryFindResource("QueryBoxBgStyle") is Style qbStyle)
                    {
                        var bgSetter = qbStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Border.BackgroundProperty);
                        if (ResolveBrush(bgSetter?.Value) is SolidColorBrush qscb)
                            baseColor = qscb.Color;
                    }

                    if (borderColor == null && Application.Current.TryFindResource("SystemThemeBorder") is SolidColorBrush stb)
                    {
                        borderColor = stb.Color;
                    }
                }

                if (Application.Current != null)
                {
                    accentBrush = Application.Current.TryFindResource("SystemAccentColorLight1Brush") as Brush
                                  ?? Application.Current.TryFindResource("BasicSystemAccentColor") as Brush
                                  ?? Application.Current.TryFindResource("ItemSelectColor") as Brush;
                }

                // If isDark was not explicitly determined, compute ITU-R BT.601 perceived luminance
                if (currentTheme?.IsDark == null && Application.Current?.TryFindResource("SystemBG") == null)
                {
                    var luminance = (baseColor.R * 0.299) + (baseColor.G * 0.587) + (baseColor.B * 0.114);
                    isDark = luminance < 145;
                }

                if (isDark)
                {
                    // Dark theme: Sleek dark acrylic capsule with crisp white text
                    var r = baseColor.R;
                    var g = baseColor.G;
                    var b = baseColor.B;

                    var c1 = Color.FromArgb(245, (byte)Math.Min(255, r + 24), (byte)Math.Min(255, g + 24), (byte)Math.Min(255, b + 24));
                    var c2 = Color.FromArgb(252, (byte)Math.Max(0, r - 6), (byte)Math.Max(0, g - 6), (byte)Math.Max(0, b - 6));
                    _rootBorder.Background = new LinearGradientBrush(c1, c2, 90.0);

                    _rootBorder.BorderBrush = borderColor.HasValue
                        ? new SolidColorBrush(borderColor.Value)
                        : new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));

                    // Pure white text guarantees 100% readability
                    _textBlock.Foreground = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                    _iconBlock.Foreground = new SolidColorBrush(Color.FromRgb(250, 250, 250));

                    if (accentBrush is SolidColorBrush ascb)
                    {
                        var ac = ascb.Color;
                        _badgeBorder.Background = new SolidColorBrush(Color.FromArgb(95, ac.R, ac.G, ac.B));
                    }
                    else
                    {
                        _badgeBorder.Background = new SolidColorBrush(Color.FromArgb(65, 255, 255, 255));
                    }

                    if (_rootBorder.Effect is DropShadowEffect dse)
                    {
                        dse.Color = Colors.Black;
                        dse.Opacity = 0.55;
                    }
                }
                else
                {
                    // Light theme (e.g. Neumorphic Frost, BlurWhite): Soft luminous pill matching theme palette
                    var r = baseColor.R;
                    var g = baseColor.G;
                    var b = baseColor.B;

                    // Specular frosted gradient
                    var c1 = Color.FromArgb(248, (byte)Math.Min(255, r + 6), (byte)Math.Min(255, g + 6), (byte)Math.Min(255, b + 6));
                    var c2 = Color.FromArgb(252, (byte)Math.Max(0, r - 6), (byte)Math.Max(0, g - 6), (byte)Math.Max(0, b - 6));
                    _rootBorder.Background = new LinearGradientBrush(c1, c2, 90.0);

                    _rootBorder.BorderBrush = borderColor.HasValue
                        ? new SolidColorBrush(borderColor.Value)
                        : new SolidColorBrush(Color.FromArgb(180, 144, 196, 248));

                    // High-contrast deep slate text for 100% readability on light background
                    _textBlock.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                    _iconBlock.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));

                    if (accentBrush is SolidColorBrush ascb)
                    {
                        var ac = ascb.Color;
                        _badgeBorder.Background = new SolidColorBrush(Color.FromArgb(50, ac.R, ac.G, ac.B));
                    }
                    else if (borderColor.HasValue)
                    {
                        var tc = borderColor.Value;
                        _badgeBorder.Background = new SolidColorBrush(Color.FromArgb(55, tc.R, tc.G, tc.B));
                    }
                    else
                    {
                        _badgeBorder.Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
                    }

                    if (_rootBorder.Effect is DropShadowEffect dse)
                    {
                        dse.Color = Color.FromRgb(140, 170, 205);
                        dse.Opacity = 0.40;
                        dse.BlurRadius = 18;
                    }
                }
            }
            catch
            {
                // Fallback gracefully to high-contrast dark capsule
                _rootBorder.Background = new SolidColorBrush(Color.FromArgb(245, 30, 32, 38));
                _rootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                _textBlock.Foreground = Brushes.White;
                _iconBlock.Foreground = Brushes.White;
            }
        }

        public static void ShowFeedback(string message, HudIconType iconType, bool isMuted = false)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                lock (_lock)
                {
                    if (_currentInstance == null)
                    {
                        _currentInstance = new SysHudOverlay();
                    }

                    _currentInstance.UpdateAndPresent(message, iconType, isMuted);
                }
            });
        }

        private void UpdateAndPresent(string message, HudIconType iconType, bool isMuted)
        {
            ApplyThemeColors();

            _textBlock.Text = message;

            switch (iconType)
            {
                case HudIconType.VolumeMuted:
                    _iconBlock.Text = "\xe74f"; // Muted icon
                    _statusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E54D42")); // Red dot
                    break;
                case HudIconType.Brightness:
                    _iconBlock.Text = "\xe706"; // Brightness icon
                    _statusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5A623")); // Amber dot
                    break;
                case HudIconType.Volume:
                default:
                    _iconBlock.Text = isMuted ? "\xe74f" : "\xe767"; // Speaker icon
                    _statusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isMuted ? "#E54D42" : "#38D39F"));
                    break;
            }

            // Position at bottom center of primary work area (above taskbar)
            var workArea = SystemParameters.WorkArea;
            _rootBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredWidth = Math.Max(_rootBorder.DesiredSize.Width, 220);
            var desiredHeight = Math.Max(_rootBorder.DesiredSize.Height, 52);

            Left = workArea.Left + (workArea.Width - desiredWidth) / 2.0;
            Top = workArea.Bottom - desiredHeight - 65; // 65px above bottom of work area

            _hideTimer.Stop();

            if (!IsVisible)
            {
                _rootBorder.Opacity = 0;
                _translate.Y = 14;
                Show();

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                };
                var slideUp = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 },
                };

                _rootBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                _translate.BeginAnimation(TranslateTransform.YProperty, slideUp);
            }
            else
            {
                // Subtle pulse to indicate update
                var pulse = new DoubleAnimation(0.7, 1.0, TimeSpan.FromMilliseconds(100));
                _rootBorder.BeginAnimation(UIElement.OpacityProperty, pulse);
            }

            _hideTimer.Start();
        }

        private void FadeOutAndClose()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
            };
            var slideDown = new DoubleAnimation(0, 10, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
            };
            fadeOut.Completed += (s, e) =>
            {
                Hide();
            };
            _rootBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            _translate.BeginAnimation(TranslateTransform.YProperty, slideDown);
        }
    }
}
