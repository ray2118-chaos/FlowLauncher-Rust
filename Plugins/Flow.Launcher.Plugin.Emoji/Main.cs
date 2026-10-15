using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Emoji
{
    public class Main : IPlugin, IContextMenu
    {
        private PluginInitContext? _context;
        private string _pluginDir = string.Empty;
        private const string DefaultIconPath = "Images\\emoji.png";

        private record EmojiItem(
            string Glyph,
            string Name,
            string Category,
            string CodePoint,
            string IconPath,
            bool HasColorIcon,
            string[] Keywords);

        private readonly List<EmojiItem> _items = new();

        private class JsonEmojiModel
        {
            public string glyph { get; set; } = string.Empty;
            public string name { get; set; } = string.Empty;
            public string category { get; set; } = string.Empty;
            public string codePoint { get; set; } = string.Empty;
            public string filename { get; set; } = string.Empty;
            public string icon { get; set; } = string.Empty;
            public List<string> keywords { get; set; } = new();
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            _pluginDir = context.CurrentPluginMetadata.PluginDirectory;

            LoadDatabase();
        }

        private void LoadDatabase()
        {
            _items.Clear();

            // 1. Load comprehensive Unicode emoji database from emojis.json
            var jsonPath = Path.Combine(_pluginDir, "emojis.json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(jsonPath);
                    var parsed = JsonSerializer.Deserialize<List<JsonEmojiModel>>(jsonContent);
                    if (parsed != null)
                    {
                        foreach (var m in parsed)
                        {
                            var relIcon = m.icon.Replace('/', '\\');
                            var fullIconPath = Path.Combine(_pluginDir, relIcon);
                            var hasIcon = File.Exists(fullIconPath);

                            _items.Add(new EmojiItem(
                                Glyph: m.glyph,
                                Name: m.name,
                                Category: m.category,
                                CodePoint: m.codePoint,
                                IconPath: hasIcon ? relIcon : DefaultIconPath,
                                HasColorIcon: hasIcon,
                                Keywords: m.keywords.ToArray()
                            ));
                        }
                    }
                }
                catch
                {
                    // Fall back to embedded list
                }
            }

            // 2. Add Math & Science Symbols
            AddBuiltinSymbols();
        }

        private void AddBuiltinSymbols()
        {
            var builtins = new (string Glyph, string Name, string Category, string CodePoint, string[] Keywords)[]
            {
                // Math & Science Symbols
                ("±", "Plus-Minus Sign", "Math", "U+00B1", new[] { "pm", "plus minus", "tolerance" }),
                ("≠", "Not Equal To", "Math", "U+2260", new[] { "neq", "not equal", "different" }),
                ("≈", "Almost Equal To", "Math", "U+2248", new[] { "approx", "similar", "roughly" }),
                ("≤", "Less-Than or Equal To", "Math", "U+2264", new[] { "leq", "less equal" }),
                ("≥", "Greater-Than or Equal To", "Math", "U+2265", new[] { "geq", "greater equal" }),
                ("∞", "Infinity", "Math", "U+221E", new[] { "inf", "infinite", "forever", "limit" }),
                ("√", "Square Root", "Math", "U+221A", new[] { "sqrt", "radical", "root" }),
                ("π", "Greek Small Letter Pi", "Math", "U+03C0", new[] { "pi", "3.14", "circle", "greek" }),
                ("µ", "Micro Sign", "Math", "U+00B5", new[] { "micro", "mu", "micron" }),
                ("°", "Degree Sign", "Math", "U+00B0", new[] { "deg", "degree", "temperature", "angle" }),
                ("÷", "Division Sign", "Math", "U+00F7", new[] { "divide", "div", "split" }),
                ("×", "Multiplication Sign", "Math", "U+00D7", new[] { "times", "multiply", "cross" }),
                ("∑", "N-Ary Summation", "Math", "U+2211", new[] { "sum", "sigma", "total" }),
                ("∫", "Integral", "Math", "U+222B", new[] { "integral", "calculus", "integrate" }),
                ("∆", "Increment / Delta", "Math", "U+2206", new[] { "delta", "difference", "triangle" }),
                ("‰", "Per Mille Sign", "Math", "U+2030", new[] { "permille", "per thousand" }),
                ("∅", "Empty Set", "Math", "U+2205", new[] { "null", "empty set", "diameter", "nothing" }),
                ("∈", "Element Of", "Math", "U+2208", new[] { "in", "element", "set member" }),
                ("∉", "Not An Element Of", "Math", "U+2209", new[] { "not in", "not element" }),

                // Typography & Arrows
                ("→", "Rightwards Arrow", "Arrow", "U+2192", new[] { "arrow right", "right", "next", "forward" }),
                ("←", "Leftwards Arrow", "Arrow", "U+2190", new[] { "arrow left", "left", "back", "previous" }),
                ("↑", "Upwards Arrow", "Arrow", "U+2191", new[] { "arrow up", "up", "top" }),
                ("↓", "Downwards Arrow", "Arrow", "U+2193", new[] { "arrow down", "down", "bottom" }),
                ("↔", "Left Right Arrow", "Arrow", "U+2194", new[] { "left right", "horizontal" }),
                ("⇒", "Rightwards Double Arrow", "Arrow", "U+21D2", new[] { "implies", "double right" }),
                ("⇐", "Leftwards Double Arrow", "Arrow", "U+21D0", new[] { "implied by", "double left" }),
                ("⇔", "Left Right Double Arrow", "Arrow", "U+21D4", new[] { "iff", "equivalent" }),
                ("•", "Bullet", "Typography", "U+2022", new[] { "bullet", "dot", "list" }),
                ("–", "En Dash", "Typography", "U+2013", new[] { "endash", "dash", "hyphen" }),
                ("—", "Em Dash", "Typography", "U+2014", new[] { "emdash", "long dash" }),
                ("©", "Copyright Sign", "Typography", "U+00A9", new[] { "copyright", "copr" }),
                ("®", "Registered Sign", "Typography", "U+00AE", new[] { "registered", "trademark" }),
                ("™", "Trade Mark Sign", "Typography", "U+2122", new[] { "trademark", "tm" }),
                ("§", "Section Sign", "Typography", "U+00A7", new[] { "section", "law", "paragraph" }),
                ("✓", "Check Mark", "Symbol", "U+2713", new[] { "check", "tick", "yes" }),
                ("✗", "Ballot X", "Symbol", "U+2717", new[] { "cross", "x", "no", "fail" }),

                // Kaomojis
                ("¯\\_(ツ)_/¯", "Shrug", "Kaomoji", "Kaomoji", new[] { "shrug", "meh", "whatever", "idk" }),
                ("(╯°□°)╯︵ ┻━┻", "Table Flip", "Kaomoji", "Kaomoji", new[] { "tableflip", "rage", "flip", "angry" }),
                ("┬─┬ノ( º _ ºノ)", "Put Table Back", "Kaomoji", "Kaomoji", new[] { "unflip", "calm", "respect tables" }),
                ("( ͡° ͜ʖ ͡°)", "Lenny Face", "Kaomoji", "Kaomoji", new[] { "lenny", "smug", "eyebrows", "creepy" }),
                ("ಠ_ಠ", "Look of Disapproval", "Kaomoji", "Kaomoji", new[] { "disapproval", "stare", "disapprove" }),
                ("(｡♥‿♥｡)", "Heart Eyes", "Kaomoji", "Kaomoji", new[] { "love", "cute", "adore", "sweet", "happy" }),
                ("ʕ•ᴥ•ʔ", "Cute Bear", "Kaomoji", "Kaomoji", new[] { "bear", "animal", "cute" }),
                ("(づ｡◕‿‿◕｡)づ", "Hug", "Kaomoji", "Kaomoji", new[] { "hug", "embrace", "cuddle", "happy" }),
                ("(•_•) ( •_•)>⌐■-■ (⌐■_■)", "Deal With It", "Kaomoji", "Kaomoji", new[] { "dealwithit", "sunglasses", "cool" }),
                ("(ノಠ益ಠ)ノ彡┻━┻", "Furious Table Flip", "Kaomoji", "Kaomoji", new[] { "furious", "mad", "rage" })
            };

            foreach (var b in builtins)
            {
                _items.Add(new EmojiItem(
                    Glyph: b.Glyph,
                    Name: b.Name,
                    Category: b.Category,
                    CodePoint: b.CodePoint,
                    IconPath: DefaultIconPath,
                    HasColorIcon: false,
                    Keywords: b.Keywords
                ));
            }
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            var search = query.Search.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(search))
            {
                // Show popular top emojis by default
                var popularKeywords = new[] { "fire", "rocket", "party", "sparkles", "joy", "heart", "thumbs", "skull", "eyes", "coffee" };
                var featured = _items
                    .Where(x => popularKeywords.Any(pk => x.Keywords.Contains(pk) || x.Name.ToLowerInvariant().Contains(pk)))
                    .DistinctBy(x => x.Glyph)
                    .Take(15);

                foreach (var item in featured)
                {
                    results.Add(CreateResult(item, 80));
                }
                return results;
            }

            foreach (var item in _items)
            {
                var nameMatch = _context?.API.FuzzySearch(search, item.Name);
                var score = nameMatch?.Score ?? 0;

                // Exact glyph query (e.g. paste emoji to search for its name)
                if (item.Glyph == search)
                {
                    score = Math.Max(score, 500);
                }
                else if (item.Name.Equals(search, StringComparison.OrdinalIgnoreCase))
                {
                    score = Math.Max(score, 400);
                }
                else if (item.Name.StartsWith(search, StringComparison.OrdinalIgnoreCase))
                {
                    score = Math.Max(score, 300);
                }
                else if (item.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    score = Math.Max(score, 250);
                }

                foreach (var kw in item.Keywords)
                {
                    if (kw.Equals(search, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 350);
                        break;
                    }
                    else if (kw.StartsWith(search, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 220);
                    }
                    else if (kw.Contains(search, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 160);
                    }
                }

                if (score > 0)
                {
                    var res = CreateResult(item, score);
                    if (nameMatch?.MatchData != null)
                    {
                        res.TitleHighlightData = nameMatch.MatchData;
                    }
                    results.Add(res);
                }
            }

            return results.OrderByDescending(r => r.Score).Take(50).ToList();
        }

        private Result CreateResult(EmojiItem item, int score)
        {
            var isKaomoji = item.Category == "Kaomoji";
            var isSymbol = item.Category == "Math" || item.Category == "Typography" || item.Category == "Arrow" || item.Category == "Symbol";

            Result res;

            if (item.HasColorIcon)
            {
                // Full color PNG icon: renders in vibrant 32-bit color without WPF font clipping!
                res = new Result
                {
                    Title = item.Name,
                    SubTitle = $"{item.Category} • {item.CodePoint} • {string.Join(", ", item.Keywords.Take(4))}",
                    IcoPath = item.IconPath,
                    Score = score,
                    ContextData = item,
                    Action = _ =>
                    {
                        Clipboard.SetText(item.Glyph);

                        // Auto-paste: simulate Ctrl+V into foreground application
                        Task.Run(async () =>
                        {
                            await Task.Delay(100);
                            SimulatePaste();
                        });

                        return true;
                    }
                };
            }
            else
            {
                // Font glyph for text symbols and Kaomojis
                res = new Result
                {
                    Title = isKaomoji ? $"{item.Glyph}   {item.Name}" : item.Name,
                    SubTitle = $"{item.Category} • {item.CodePoint} • {string.Join(", ", item.Keywords.Take(4))}",
                    Glyph = new GlyphInfo(
                        FontFamily: isKaomoji ? "Segoe UI, sans-serif" : "Segoe UI Symbol, Segoe UI, sans-serif",
                        Glyph: isKaomoji ? "ツ" : item.Glyph),
                    IcoPath = DefaultIconPath,
                    Score = score,
                    ContextData = item,
                    Action = _ =>
                    {
                        Clipboard.SetText(item.Glyph);

                        Task.Run(async () =>
                        {
                            await Task.Delay(100);
                            SimulatePaste();
                        });

                        return true;
                    }
                };
            }

            return res;
        }

        private static void SimulatePaste()
        {
            try
            {
                // VK_CONTROL = 0x11, 'V' = 0x56, KEYEVENTF_KEYUP = 0x0002
                keybd_event(0x11, 0, 0, UIntPtr.Zero);
                keybd_event(0x56, 0, 0, UIntPtr.Zero);
                keybd_event(0x56, 0, 0x0002, UIntPtr.Zero);
                keybd_event(0x11, 0, 0x0002, UIntPtr.Zero);
            }
            catch
            {
                // ignored
            }
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var menus = new List<Result>();
            if (selectedResult.ContextData is EmojiItem item)
            {
                var isKaomoji = item.Category == "Kaomoji";

                menus.Add(new Result
                {
                    Title = $"Copy Glyph: {item.Glyph}",
                    SubTitle = "Copy the symbol to clipboard without pasting",
                    IcoPath = item.HasColorIcon ? item.IconPath : DefaultIconPath,
                    Glyph = item.HasColorIcon ? null : new GlyphInfo(
                        FontFamily: isKaomoji ? "Segoe UI, sans-serif" : "Segoe UI Symbol, Segoe UI, sans-serif",
                        Glyph: isKaomoji ? "ツ" : item.Glyph),
                    Action = _ =>
                    {
                        Clipboard.SetText(item.Glyph);
                        return true;
                    }
                });

                menus.Add(new Result
                {
                    Title = $"Copy Codepoint: {item.CodePoint}",
                    SubTitle = "Copy Unicode hex code",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe943"),
                    IcoPath = string.Empty,
                    Action = _ =>
                    {
                        Clipboard.SetText(item.CodePoint);
                        return true;
                    }
                });

                menus.Add(new Result
                {
                    Title = $"Copy Name: {item.Name}",
                    SubTitle = "Copy formal name",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe8c8"),
                    IcoPath = string.Empty,
                    Action = _ =>
                    {
                        Clipboard.SetText(item.Name);
                        return true;
                    }
                });
            }

            return menus;
        }
    }
}
