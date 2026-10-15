using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Dictionary
{
    public class Main : IPlugin, IContextMenu
    {
        private PluginInitContext? _context;
        private const string IconPath = "Images\\icon.png";
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
        private string _cacheDir = string.Empty;

        private record OfflineEntry(string Word, string Phonetic, string PartOfSpeech, string Definition, string[] Synonyms, string[] Antonyms, string Example);

        private static readonly List<OfflineEntry> CommonWords = new()
        {
            new("large", "/lɑːrdʒ/", "adjective", "Of considerable or relatively great size, extent, or capacity.",
                new[] { "big", "huge", "great", "massive", "vast", "gigantic", "immense" }, new[] { "small", "little", "tiny", "minute", "miniature" }, "A large house with a beautiful garden."),
            new("serendipity", "/ˌser.ənˈdɪp.ə.ti/", "noun", "The occurrence and development of events by chance in a happy or beneficial way.",
                new[] { "chance", "fluke", "happy accident", "fortune", "luck" }, new[] { "misfortune", "bad luck" }, "A fortunate stroke of serendipity."),
            new("ephemeral", "/ɪˈfem.ər.əl/", "adjective", "Lasting for a very short time; transitory; fleeting.",
                new[] { "fleeting", "transitory", "short-lived", "momentary", "brief" }, new[] { "permanent", "eternal", "lasting" }, "Fashion trends are notoriously ephemeral."),
            new("resilient", "/rɪˈzɪl.jənt/", "adjective", "Able to withstand or recover quickly from difficult conditions.",
                new[] { "tough", "hardy", "adaptable", "buoyant", "strong" }, new[] { "fragile", "vulnerable", "weak" }, "Babies are remarkably resilient."),
            new("ubiquitous", "/juːˈbɪk.wə.təs/", "adjective", "Present, appearing, or found everywhere.",
                new[] { "omnipresent", "everywhere", "pervasive", "universal" }, new[] { "rare", "scarce", "uncommon" }, "Smartphones have become ubiquitous in daily life."),
            new("happy", "/ˈhæp.i/", "adjective", "Feeling or showing pleasure or contentment.",
                new[] { "joyful", "cheerful", "glad", "delighted", "contented", "ecstatic" }, new[] { "sad", "miserable", "depressed", "unhappy" }, "Melissa came home with a happy smile."),
            new("sad", "/sæd/", "adjective", "Feeling or showing sorrow; unhappy.",
                new[] { "unhappy", "sorrowful", "depressed", "downcast", "gloomy", "mournful" }, new[] { "happy", "cheerful", "joyful" }, "I was sad when the summer vacation ended."),
            new("eloquent", "/ˈel.ə.kwənt/", "adjective", "Fluent or persuasive in speaking or writing.",
                new[] { "articulate", "fluent", "expressive", "persuasive", "silver-tongued" }, new[] { "inarticulate", "hesitant" }, "An eloquent speech that moved the audience."),
            new("pragmatic", "/præɡˈmæt.ɪk/", "adjective", "Dealing with things sensibly and realistically in a practical way.",
                new[] { "practical", "realistic", "sensible", "down-to-earth", "rational" }, new[] { "idealistic", "impractical", "unrealistic" }, "A pragmatic approach to problem solving."),
            new("candid", "/ˈkæn.dɪd/", "adjective", "Truthful and straightforward; frank.",
                new[] { "frank", "honest", "blunt", "sincere", "forthright" }, new[] { "dishonest", "guarded", "insincere" }, "His candid responses surprised the interviewer."),
            new("nostalgia", "/nɑːˈstæl.dʒə/", "noun", "A sentimental longing or wistful affection for the past.",
                new[] { "reminiscence", "homesickness", "yearning", "wistfulness" }, Array.Empty<string>(), "I was overcome with nostalgia seeing my old high school."),
            new("meticulous", "/məˈtɪk.jə.ləs/", "adjective", "Showing great attention to detail; very careful and precise.",
                new[] { "thorough", "diligent", "painstaking", "scrupulous", "exacting" }, new[] { "careless", "sloppy", "slapdash" }, "He had kept meticulous accounts."),
            new("lucid", "/ˈluː.sɪd/", "adjective", "Expressed clearly; easy to understand; bright or luminous.",
                new[] { "clear", "intelligible", "comprehensible", "articulate", "transparent" }, new[] { "confusing", "ambiguous", "vague" }, "A remarkably lucid explanation of quantum physics.")
        };

        private static readonly ConcurrentDictionary<string, List<string>> _synCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, List<string>> _antCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, List<DefinitionItem>> _defCache = new(StringComparer.OrdinalIgnoreCase);

        public void Init(PluginInitContext context)
        {
            _context = context;
            _cacheDir = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "Cache");
            Directory.CreateDirectory(_cacheDir);
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            var search = query.Search.Trim();
            var lower = search.ToLowerInvariant();
            bool isGlobal = query.ActionKeyword == "*";

            if (isGlobal && !lower.StartsWith("syn ") && !lower.StartsWith("ant ") && !lower.StartsWith("define ") && !lower.StartsWith("dict ") && !lower.StartsWith("def "))
            {
                return [];
            }

            bool isSynMode = lower.StartsWith("syn ") || query.ActionKeyword.Equals("syn", StringComparison.OrdinalIgnoreCase);
            bool isAntMode = lower.StartsWith("ant ") || query.ActionKeyword.Equals("ant", StringComparison.OrdinalIgnoreCase);
            bool isDefineMode = lower.StartsWith("define ") || lower.StartsWith("dict ") || lower.StartsWith("def ")
                || query.ActionKeyword.Equals("def", StringComparison.OrdinalIgnoreCase)
                || query.ActionKeyword.Equals("define", StringComparison.OrdinalIgnoreCase)
                || query.ActionKeyword.Equals("dict", StringComparison.OrdinalIgnoreCase);

            string word = search;
            if (lower.StartsWith("syn ")) word = search[4..].Trim();
            else if (lower.StartsWith("ant ")) word = search[4..].Trim();
            else if (lower.StartsWith("def ")) word = search[4..].Trim();
            else if (lower.StartsWith("dict ")) word = search[5..].Trim();
            else if (lower.StartsWith("define ")) word = search[7..].Trim();

            if (string.IsNullOrWhiteSpace(word))
            {
                if (isGlobal) return [];
                if (isSynMode)
                {
                    results.Add(new Result
                    {
                        Title = "Synonyms Finder",
                        SubTitle = "Type a word to find synonyms (e.g. 'syn large', 'syn happy', 'syn clever')",
                        IcoPath = IconPath,
                        Score = 100,
                        Action = _ => false
                    });
                }
                else if (isAntMode)
                {
                    results.Add(new Result
                    {
                        Title = "Antonyms Finder",
                        SubTitle = "Type a word to find opposites (e.g. 'ant large', 'ant dark', 'ant brave')",
                        IcoPath = IconPath,
                        Score = 100,
                        Action = _ => false
                    });
                }
                else
                {
                    results.Add(new Result
                    {
                        Title = "Dictionary & Synonyms",
                        SubTitle = "Usage: 'def large', 'define large', 'syn happy', 'ant brave'",
                        IcoPath = IconPath,
                        Score = 100,
                        Action = _ => false
                    });
                }

                foreach (var ex in CommonWords.Take(3))
                {
                    results.Add(new Result
                    {
                        Title = isSynMode ? $"Synonyms for '{ex.Word}': {string.Join(", ", ex.Synonyms.Take(4))}"
                              : isAntMode ? $"Antonyms for '{ex.Word}': {string.Join(", ", ex.Antonyms.Take(3))}"
                              : $"Word of the day: {ex.Word} {ex.Phonetic}",
                        SubTitle = $"[{ex.PartOfSpeech}] {ex.Definition}",
                        IcoPath = IconPath,
                        Score = 60,
                        Action = _ =>
                        {
                            Clipboard.SetText(isSynMode ? string.Join(", ", ex.Synonyms)
                                            : isAntMode ? string.Join(", ", ex.Antonyms)
                                            : $"{ex.Word} ({ex.PartOfSpeech}): {ex.Definition}");
                            return true;
                        }
                    });
                }

                return results;
            }

            // 1. Synonyms Mode (syn <word>)
            if (isSynMode)
            {
                var syns = GetSynonyms(word);
                if (syns.Count > 0)
                {
                    var headerStr = string.Join(", ", syns.Take(7));
                    results.Add(new Result
                    {
                        Title = $"Synonyms for '{word}': {headerStr}...",
                        SubTitle = "Press Enter to copy all synonyms to clipboard",
                        IcoPath = IconPath,
                        Score = 500,
                        Action = _ =>
                        {
                            Clipboard.SetText(string.Join(", ", syns));
                            return true;
                        }
                    });

                    int rank = 0;
                    foreach (var s in syns.Take(8))
                    {
                        var copyWord = s;
                        results.Add(new Result
                        {
                            Title = copyWord,
                            SubTitle = $"Synonym for '{word}' • Press Enter to copy",
                            IcoPath = IconPath,
                            Score = 480 - rank,
                            Action = _ =>
                            {
                                Clipboard.SetText(copyWord);
                                return true;
                            }
                        });
                        rank++;
                    }
                }
                else
                {
                    results.Add(new Result
                    {
                        Title = $"Search synonyms for '{word}' on Thesaurus.com",
                        SubTitle = "Press Enter to look up in online thesaurus",
                        IcoPath = IconPath,
                        Score = 200,
                        Action = _ =>
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = $"https://www.thesaurus.com/browse/{Uri.EscapeDataString(word)}",
                                UseShellExecute = true
                            });
                            return true;
                        }
                    });
                }
                return results;
            }

            // 2. Antonyms Mode (ant <word>)
            if (isAntMode)
            {
                var ants = GetAntonyms(word);
                if (ants.Count > 0)
                {
                    var headerStr = string.Join(", ", ants.Take(6));
                    results.Add(new Result
                    {
                        Title = $"Antonyms for '{word}': {headerStr}...",
                        SubTitle = "Press Enter to copy all antonyms to clipboard",
                        IcoPath = IconPath,
                        Score = 500,
                        Action = _ =>
                        {
                            Clipboard.SetText(string.Join(", ", ants));
                            return true;
                        }
                    });

                    int rank = 0;
                    foreach (var a in ants.Take(8))
                    {
                        var copyWord = a;
                        results.Add(new Result
                        {
                            Title = copyWord,
                            SubTitle = $"Antonym for '{word}' • Press Enter to copy",
                            IcoPath = IconPath,
                            Score = 480 - rank,
                            Action = _ =>
                            {
                                Clipboard.SetText(copyWord);
                                return true;
                            }
                        });
                        rank++;
                    }
                }
                else
                {
                    results.Add(new Result
                    {
                        Title = $"Search antonyms for '{word}' on Thesaurus.com",
                        SubTitle = "Press Enter to look up in online thesaurus",
                        IcoPath = IconPath,
                        Score = 200,
                        Action = _ =>
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = $"https://www.thesaurus.com/browse/{Uri.EscapeDataString(word)}",
                                UseShellExecute = true
                            });
                            return true;
                        }
                    });
                }
                return results;
            }

            // 3. Definitions Mode (define <word>, dict <word>, or word query)
            var defs = GetDefinitions(word);
            if (defs.Count > 0)
            {
                int defIndex = 0;
                foreach (var d in defs.Take(3))
                {
                    var copyDef = $"{word} [{d.PartOfSpeech}]: {d.Text}";
                    results.Add(new Result
                    {
                        Title = $"{word}  •  [{d.PartOfSpeech}]",
                        SubTitle = d.Text,
                        IcoPath = IconPath,
                        Score = 500 - (defIndex * 15),
                        Action = _ =>
                        {
                            Clipboard.SetText(copyDef);
                            return true;
                        }
                    });
                    defIndex++;
                }

                // Show related Synonyms Card
                var quickSyns = GetSynonyms(word);
                if (quickSyns.Count > 0)
                {
                    var synList = string.Join(", ", quickSyns.Take(6));
                    results.Add(new Result
                    {
                        Title = $"Synonyms: {synList}",
                        SubTitle = $"Press Enter to copy, or type 'syn {word}' for complete list",
                        IcoPath = IconPath,
                        Score = 350,
                        Action = _ =>
                        {
                            Clipboard.SetText(synList);
                            return true;
                        }
                    });
                }

                // Show related Antonyms Card
                var quickAnts = GetAntonyms(word);
                if (quickAnts.Count > 0)
                {
                    var antList = string.Join(", ", quickAnts.Take(5));
                    results.Add(new Result
                    {
                        Title = $"Antonyms: {antList}",
                        SubTitle = $"Press Enter to copy, or type 'ant {word}' for complete list",
                        IcoPath = IconPath,
                        Score = 340,
                        Action = _ =>
                        {
                            Clipboard.SetText(antList);
                            return true;
                        }
                    });
                }

                // Web lookup option
                results.Add(new Result
                {
                    Title = $"More definitions for '{word}' on Merriam-Webster",
                    SubTitle = "Press Enter to open full dictionary in browser",
                    IcoPath = IconPath,
                    Score = 100,
                    Action = _ =>
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = $"https://www.merriam-webster.com/dictionary/{Uri.EscapeDataString(word)}",
                            UseShellExecute = true
                        });
                        return true;
                    }
                });

                return results;
            }

            // Fallback web search
            results.Add(new Result
            {
                Title = $"Look up '{word}' online",
                SubTitle = "Search definition on Merriam-Webster / Wiktionary",
                IcoPath = IconPath,
                Score = 100,
                Action = _ =>
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = $"https://www.merriam-webster.com/dictionary/{Uri.EscapeDataString(word)}",
                        UseShellExecute = true
                    });
                    return true;
                }
            });

            return results;
        }

        private class Candidate
        {
            public string Word { get; set; } = string.Empty;
            public double Freq { get; set; }
            public int Score { get; set; }
            public bool HasSynTag { get; set; }
            public bool IsDirect { get; set; }
        }

        private List<string> GetSynonyms(string word)
        {
            if (_synCache.TryGetValue(word, out var cached)) return cached;

            var local = CommonWords.FirstOrDefault(w => w.Word.Equals(word, StringComparison.OrdinalIgnoreCase));
            if (local != null && local.Synonyms.Length > 0)
            {
                var list = local.Synonyms.ToList();
                _synCache[word] = list;
                return list;
            }

            var map = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);

            // 1. Fetch semantic matches (ml=word)
            try
            {
                var url1 = $"https://api.datamuse.com/words?ml={Uri.EscapeDataString(word)}&md=f&max=35";
                var resp1 = _http.GetAsync(url1).GetAwaiter().GetResult();
                if (resp1.IsSuccessStatusCode)
                {
                    var json1 = resp1.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    using var doc = JsonDocument.Parse(json1);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            var w = el.TryGetProperty("word", out var wp) ? wp.GetString() ?? "" : "";
                            if (string.IsNullOrWhiteSpace(w) || w.Equals(word, StringComparison.OrdinalIgnoreCase)) continue;

                            var sc = el.TryGetProperty("score", out var sp) ? sp.GetInt32() : 0;
                            double f = 0.0;
                            bool hasSyn = false;
                            if (el.TryGetProperty("tags", out var tp) && tp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var t in tp.EnumerateArray())
                                {
                                    var ts = t.GetString() ?? "";
                                    if (ts.Equals("syn", StringComparison.OrdinalIgnoreCase)) hasSyn = true;
                                    if (ts.StartsWith("f:")) double.TryParse(ts[2..], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out f);
                                }
                            }

                            if (!map.TryGetValue(w, out var cand))
                            {
                                cand = new Candidate { Word = w };
                                map[w] = cand;
                            }
                            cand.Score = Math.Max(cand.Score, sc);
                            cand.Freq = Math.Max(cand.Freq, f);
                            cand.HasSynTag |= hasSyn;
                        }
                    }
                }
            }
            catch { }

            // 2. Fetch direct relational synonyms (rel_syn=word)
            try
            {
                var url2 = $"https://api.datamuse.com/words?rel_syn={Uri.EscapeDataString(word)}&md=f&max=30";
                var resp2 = _http.GetAsync(url2).GetAwaiter().GetResult();
                if (resp2.IsSuccessStatusCode)
                {
                    var json2 = resp2.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    using var doc = JsonDocument.Parse(json2);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            var w = el.TryGetProperty("word", out var wp) ? wp.GetString() ?? "" : "";
                            if (string.IsNullOrWhiteSpace(w) || w.Equals(word, StringComparison.OrdinalIgnoreCase)) continue;

                            double f = 0.0;
                            if (el.TryGetProperty("tags", out var tp) && tp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var t in tp.EnumerateArray())
                                {
                                    var ts = t.GetString() ?? "";
                                    if (ts.StartsWith("f:")) double.TryParse(ts[2..], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out f);
                                }
                            }

                            if (!map.TryGetValue(w, out var cand))
                            {
                                cand = new Candidate { Word = w };
                                map[w] = cand;
                            }
                            cand.IsDirect = true;
                            cand.Freq = Math.Max(cand.Freq, f);
                        }
                    }
                }
            }
            catch { }

            var sorted = map.Values
                .Where(c => c.HasSynTag || c.IsDirect)
                .OrderByDescending(c => c.Freq > 0 ? (c.HasSynTag ? c.Freq * 10 : c.Freq) : (c.Score / 1000.0))
                .Select(c => c.Word)
                .Take(15)
                .ToList();

            if (sorted.Count == 0 && map.Count > 0)
            {
                sorted = map.Values.OrderByDescending(c => c.Freq).Select(c => c.Word).Take(15).ToList();
            }

            _synCache[word] = sorted;
            return sorted;
        }

        private List<string> GetAntonyms(string word)
        {
            if (_antCache.TryGetValue(word, out var cached)) return cached;

            var local = CommonWords.FirstOrDefault(w => w.Word.Equals(word, StringComparison.OrdinalIgnoreCase));
            if (local != null && local.Antonyms.Length > 0)
            {
                var list = local.Antonyms.ToList();
                _antCache[word] = list;
                return list;
            }

            var map = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);

            // 1. Direct antonyms (rel_ant=word)
            try
            {
                var url1 = $"https://api.datamuse.com/words?rel_ant={Uri.EscapeDataString(word)}&md=f&max=20";
                var resp1 = _http.GetAsync(url1).GetAwaiter().GetResult();
                if (resp1.IsSuccessStatusCode)
                {
                    var json1 = resp1.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    using var doc = JsonDocument.Parse(json1);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            var w = el.TryGetProperty("word", out var wp) ? wp.GetString() ?? "" : "";
                            if (string.IsNullOrWhiteSpace(w) || w.Equals(word, StringComparison.OrdinalIgnoreCase)) continue;

                            double f = 0.0;
                            if (el.TryGetProperty("tags", out var tp) && tp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var t in tp.EnumerateArray())
                                {
                                    var ts = t.GetString() ?? "";
                                    if (ts.StartsWith("f:")) double.TryParse(ts[2..], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out f);
                                }
                            }

                            if (!map.TryGetValue(w, out var cand))
                            {
                                cand = new Candidate { Word = w };
                                map[w] = cand;
                            }
                            cand.IsDirect = true;
                            cand.Freq = Math.Max(cand.Freq, f);
                        }
                    }
                }
            }
            catch { }

            // 2. Semantic opposite (ml=opposite+of+word)
            try
            {
                var url2 = $"https://api.datamuse.com/words?ml=opposite+of+{Uri.EscapeDataString(word)}&md=f&max=20";
                var resp2 = _http.GetAsync(url2).GetAwaiter().GetResult();
                if (resp2.IsSuccessStatusCode)
                {
                    var json2 = resp2.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    using var doc = JsonDocument.Parse(json2);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            var w = el.TryGetProperty("word", out var wp) ? wp.GetString() ?? "" : "";
                            if (string.IsNullOrWhiteSpace(w) || w.Equals(word, StringComparison.OrdinalIgnoreCase)) continue;
                            if (w.Equals("opposite", StringComparison.OrdinalIgnoreCase) || w.Equals("of", StringComparison.OrdinalIgnoreCase) || w.Equals("time", StringComparison.OrdinalIgnoreCase)) continue;

                            double f = 0.0;
                            if (el.TryGetProperty("tags", out var tp) && tp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var t in tp.EnumerateArray())
                                {
                                    var ts = t.GetString() ?? "";
                                    if (ts.StartsWith("f:")) double.TryParse(ts[2..], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out f);
                                }
                            }

                            if (!map.TryGetValue(w, out var cand))
                            {
                                cand = new Candidate { Word = w };
                                map[w] = cand;
                            }
                            cand.Freq = Math.Max(cand.Freq, f);
                        }
                    }
                }
            }
            catch { }

            var sorted = map.Values
                .OrderByDescending(c => c.IsDirect ? (c.Freq * 10 + 1000) : c.Freq)
                .Select(c => c.Word)
                .Take(12)
                .ToList();

            _antCache[word] = sorted;
            return sorted;
        }

        public class DefinitionItem
        {
            public string PartOfSpeech { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }

        private List<DefinitionItem> GetDefinitions(string word)
        {
            if (_defCache.TryGetValue(word, out var cached)) return cached;

            var local = CommonWords.FirstOrDefault(w => w.Word.Equals(word, StringComparison.OrdinalIgnoreCase));
            if (local != null)
            {
                var res = new List<DefinitionItem>
                {
                    new DefinitionItem { PartOfSpeech = char.ToUpperInvariant(local.PartOfSpeech[0]) + local.PartOfSpeech[1..], Text = local.Definition }
                };
                _defCache[word] = res;
                return res;
            }

            var list = new List<DefinitionItem>();
            try
            {
                var url = $"https://api.datamuse.com/words?sp={Uri.EscapeDataString(word)}&md=dpfs&max=1";
                var resp = _http.GetAsync(url).GetAwaiter().GetResult();
                if (resp.IsSuccessStatusCode)
                {
                    var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                    {
                        var entry = doc.RootElement[0];
                        if (entry.TryGetProperty("defs", out var defsArray) && defsArray.ValueKind == JsonValueKind.Array)
                        {
                            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var d in defsArray.EnumerateArray())
                            {
                                var raw = d.GetString() ?? "";
                                if (string.IsNullOrWhiteSpace(raw)) continue;

                                string posName = "General";
                                string text = raw;

                                int tabIdx = raw.IndexOf('\t');
                                if (tabIdx > 0)
                                {
                                    var prefix = raw[..tabIdx].Trim().ToLowerInvariant();
                                    text = raw[(tabIdx + 1)..].Trim();
                                    posName = prefix switch
                                    {
                                        "adj" => "Adjective",
                                        "n" => "Noun",
                                        "v" => "Verb",
                                        "adv" => "Adverb",
                                        "prep" => "Preposition",
                                        "conj" => "Conjunction",
                                        "pron" => "Pronoun",
                                        "interj" => "Interjection",
                                        _ => char.ToUpperInvariant(prefix[0]) + prefix[1..]
                                    };
                                }

                                if (seen.Add(text))
                                {
                                    list.Add(new DefinitionItem { PartOfSpeech = posName, Text = text });
                                    if (list.Count >= 4) break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            _defCache[word] = list;
            return list;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "Copy result",
                    SubTitle = "Copy title to clipboard",
                    IcoPath = IconPath,
                    Action = _ =>
                    {
                        Clipboard.SetText(selectedResult.Title);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Copy details",
                    SubTitle = "Copy subtitle details to clipboard",
                    IcoPath = IconPath,
                    Action = _ =>
                    {
                        Clipboard.SetText(selectedResult.SubTitle);
                        return true;
                    }
                }
            };
        }
    }
}
