using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Dictionary
{
    public class Main : IPlugin, IContextMenu
    {
        private PluginInitContext? _context;
        private const string IconPath = "Images\\icon.png";
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(4) };
        private string _cacheDir = string.Empty;

        private record OfflineEntry(string Word, string Phonetic, string PartOfSpeech, string Definition, string[] Synonyms, string[] Antonyms, string Example);

        private static readonly List<OfflineEntry> CommonWords = new()
        {
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

            if (isGlobal && !lower.StartsWith("syn ") && !lower.StartsWith("ant ") && !lower.StartsWith("define ") && !lower.StartsWith("dict "))
            {
                return [];
            }

            bool isSynMode = lower.StartsWith("syn ") || query.ActionKeyword.Equals("syn", StringComparison.OrdinalIgnoreCase);
            bool isAntMode = lower.StartsWith("ant ") || query.ActionKeyword.Equals("ant", StringComparison.OrdinalIgnoreCase);

            string word = search;
            if (lower.StartsWith("syn ")) word = search[4..].Trim();
            else if (lower.StartsWith("ant ")) word = search[4..].Trim();
            else if (lower.StartsWith("define ")) word = search[7..].Trim();
            else if (lower.StartsWith("dict ")) word = search[5..].Trim();

            if (string.IsNullOrWhiteSpace(word))
            {
                if (isGlobal) return [];
                if (isSynMode)
                {
                    results.Add(new Result
                    {
                        Title = "Synonyms Finder",
                        SubTitle = "Type a word to find synonyms (e.g. 'syn happy', 'syn clever', 'syn fast')",
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
                        SubTitle = "Type a word to find opposites (e.g. 'ant dark', 'ant brave', 'ant true')",
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
                        SubTitle = "Usage: 'define serendipity', 'syn happy', 'ant brave'",
                        IcoPath = IconPath,
                        Score = 100,
                        Action = _ => false
                    });
                }

                // Show some featured vocabulary
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

            // 1. Try local offline dictionary first
            var local = CommonWords.FirstOrDefault(w => w.Word.Equals(word, StringComparison.OrdinalIgnoreCase));
            if (local != null)
            {
                if (isSynMode)
                {
                    results.Add(new Result
                    {
                        Title = $"Synonyms for '{local.Word}':",
                        SubTitle = string.Join(", ", local.Synonyms),
                        IcoPath = IconPath,
                        Score = 350,
                        Action = _ =>
                        {
                            Clipboard.SetText(string.Join(", ", local.Synonyms));
                            return true;
                        }
                    });
                }
                else if (isAntMode)
                {
                    var ants = local.Antonyms.Length > 0 ? string.Join(", ", local.Antonyms) : "None found";
                    results.Add(new Result
                    {
                        Title = $"Antonyms for '{local.Word}':",
                        SubTitle = ants,
                        IcoPath = IconPath,
                        Score = 350,
                        Action = _ =>
                        {
                            Clipboard.SetText(ants);
                            return true;
                        }
                    });
                }
                else
                {
                    results.Add(new Result
                    {
                        Title = $"{local.Word}  {local.Phonetic}",
                        SubTitle = $"[{local.PartOfSpeech}] {local.Definition}",
                        IcoPath = IconPath,
                        Score = 350,
                        Action = _ =>
                        {
                            Clipboard.SetText($"{local.Word}: {local.Definition}");
                            return true;
                        }
                    });

                    if (!string.IsNullOrEmpty(local.Example))
                    {
                        results.Add(new Result
                        {
                            Title = $"Example: \"{local.Example}\"",
                            SubTitle = "Click to copy example sentence",
                            IcoPath = IconPath,
                            Score = 300,
                            Action = _ =>
                            {
                                Clipboard.SetText(local.Example);
                                return true;
                            }
                        });
                    }

                    if (local.Synonyms.Length > 0)
                    {
                        results.Add(new Result
                        {
                            Title = $"Synonyms: {string.Join(", ", local.Synonyms)}",
                            SubTitle = "Click to copy synonyms list",
                            IcoPath = IconPath,
                            Score = 280,
                            Action = _ =>
                            {
                                Clipboard.SetText(string.Join(", ", local.Synonyms));
                                return true;
                            }
                        });
                    }
                }

                return results;
            }

            // 2. In syn / ant mode, query Datamuse API for instant, comprehensive results
            if (isSynMode || isAntMode)
            {
                var datamuseWords = FetchDatamuse(word, isSynMode);
                if (datamuseWords.Count > 0)
                {
                    results.Add(new Result
                    {
                        Title = isSynMode ? $"Synonyms for '{word}': {string.Join(", ", datamuseWords.Take(8))}"
                                          : $"Antonyms for '{word}': {string.Join(", ", datamuseWords.Take(8))}",
                        SubTitle = "Press Enter to copy all to clipboard",
                        IcoPath = IconPath,
                        Score = 400,
                        Action = _ =>
                        {
                            Clipboard.SetText(string.Join(", ", datamuseWords));
                            return true;
                        }
                    });

                    int rank = 0;
                    foreach (var w in datamuseWords.Take(8))
                    {
                        var copyW = w;
                        results.Add(new Result
                        {
                            Title = copyW,
                            SubTitle = isSynMode ? $"Synonym for '{word}' • Press Enter to copy"
                                                 : $"Antonym for '{word}' • Press Enter to copy",
                            IcoPath = IconPath,
                            Score = 380 - rank,
                            Action = _ =>
                            {
                                Clipboard.SetText(copyW);
                                return true;
                            }
                        });
                        rank++;
                    }

                    return results;
                }
            }

            // 3. Fetch live online from Free Dictionary API or cache
            var apiResults = FetchWord(word, isSynMode, isAntMode);
            if (apiResults != null && apiResults.Count > 0)
            {
                return apiResults;
            }

            // 4. Fallback: offer to search dictionary web page or thesaurus
            if (isSynMode || isAntMode)
            {
                results.Add(new Result
                {
                    Title = isSynMode ? $"Search synonyms for '{word}' on Thesaurus.com" : $"Search antonyms for '{word}' on Thesaurus.com",
                    SubTitle = "Press Enter to look up in online thesaurus",
                    IcoPath = IconPath,
                    Score = 150,
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
            else
            {
                results.Add(new Result
                {
                    Title = $"Look up '{word}' online",
                    SubTitle = "Search definition on Merriam-Webster / Wiktionary",
                    IcoPath = IconPath,
                    Score = 80,
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
            }

            return results;
        }

        private List<string> FetchDatamuse(string word, bool isSyn)
        {
            var rel = isSyn ? "rel_syn" : "rel_ant";
            var url = $"https://api.datamuse.com/words?{rel}={Uri.EscapeDataString(word)}&max=15";
            try
            {
                var resp = _http.GetAsync(url).GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode) return [];
                var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
                var list = new List<string>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("word", out var w))
                    {
                        var val = w.GetString();
                        if (!string.IsNullOrEmpty(val)) list.Add(val);
                    }
                }
                return list;
            }
            catch
            {
                return [];
            }
        }

        private List<Result>? FetchWord(string word, bool synMode, bool antMode)
        {
            try
            {
                var cleanWord = word.ToLowerInvariant().Trim();
                var cacheFile = Path.Combine(_cacheDir, $"{cleanWord}.json");
                string json;

                if (File.Exists(cacheFile))
                {
                    json = File.ReadAllText(cacheFile);
                }
                else
                {
                    var url = $"https://api.dictionaryapi.dev/api/v2/entries/en/{Uri.EscapeDataString(cleanWord)}";
                    var resp = _http.GetAsync(url).GetAwaiter().GetResult();
                    if (!resp.IsSuccessStatusCode) return null;
                    json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    File.WriteAllText(cacheFile, json);
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) return null;

                var entry = root[0];
                var phonetic = "";
                if (entry.TryGetProperty("phonetic", out var ph)) phonetic = ph.GetString() ?? "";

                var list = new List<Result>();
                var allSynonyms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allAntonyms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (entry.TryGetProperty("meanings", out var meanings) && meanings.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in meanings.EnumerateArray())
                    {
                        var partOfSpeech = m.TryGetProperty("partOfSpeech", out var pos) ? pos.GetString() : "definition";

                        if (m.TryGetProperty("synonyms", out var synArray) && synArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var s in synArray.EnumerateArray())
                            {
                                var sv = s.GetString();
                                if (!string.IsNullOrEmpty(sv)) allSynonyms.Add(sv);
                            }
                        }

                        if (m.TryGetProperty("antonyms", out var antArray) && antArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var a in antArray.EnumerateArray())
                            {
                                var av = a.GetString();
                                if (!string.IsNullOrEmpty(av)) allAntonyms.Add(av);
                            }
                        }

                        if (m.TryGetProperty("definitions", out var defs) && defs.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var d in defs.EnumerateArray())
                            {
                                if (d.TryGetProperty("synonyms", out var dSyn) && dSyn.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var s in dSyn.EnumerateArray())
                                    {
                                        var sv = s.GetString();
                                        if (!string.IsNullOrEmpty(sv)) allSynonyms.Add(sv);
                                    }
                                }

                                if (d.TryGetProperty("antonyms", out var dAnt) && dAnt.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var a in dAnt.EnumerateArray())
                                    {
                                        var av = a.GetString();
                                        if (!string.IsNullOrEmpty(av)) allAntonyms.Add(av);
                                    }
                                }

                                var defText = d.TryGetProperty("definition", out var dt) ? dt.GetString() : "";
                                var exText = d.TryGetProperty("example", out var et) ? et.GetString() : "";

                                if (!string.IsNullOrEmpty(defText))
                                {
                                    list.Add(new Result
                                    {
                                        Title = $"{cleanWord}  {phonetic}",
                                        SubTitle = $"[{partOfSpeech}] {defText}",
                                        IcoPath = IconPath,
                                        Score = 320,
                                        Action = _ =>
                                        {
                                            Clipboard.SetText($"{cleanWord} ({partOfSpeech}): {defText}");
                                            return true;
                                        }
                                    });

                                    if (!string.IsNullOrEmpty(exText))
                                    {
                                        list.Add(new Result
                                        {
                                            Title = $"Example: \"{exText}\"",
                                            SubTitle = "Click to copy example",
                                            IcoPath = IconPath,
                                            Score = 290,
                                            Action = _ =>
                                            {
                                                Clipboard.SetText(exText);
                                                return true;
                                            }
                                        });
                                    }
                                }
                            }
                        }
                    }
                }

                if (synMode)
                {
                    if (allSynonyms.Count > 0)
                    {
                        return new List<Result>
                        {
                            new Result
                            {
                                Title = $"Synonyms for '{cleanWord}':",
                                SubTitle = string.Join(", ", allSynonyms.Take(12)),
                                IcoPath = IconPath,
                                Score = 400,
                                Action = _ =>
                                {
                                    Clipboard.SetText(string.Join(", ", allSynonyms));
                                    return true;
                                }
                            }
                        };
                    }

                    return new List<Result>
                    {
                        new Result
                        {
                            Title = $"Search synonyms for '{cleanWord}' on Thesaurus.com",
                            SubTitle = "Press Enter to look up in online thesaurus",
                            IcoPath = IconPath,
                            Score = 350,
                            Action = _ =>
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = $"https://www.thesaurus.com/browse/{Uri.EscapeDataString(cleanWord)}",
                                    UseShellExecute = true
                                });
                                return true;
                            }
                        }
                    };
                }

                if (antMode)
                {
                    if (allAntonyms.Count > 0)
                    {
                        return new List<Result>
                        {
                            new Result
                            {
                                Title = $"Antonyms for '{cleanWord}':",
                                SubTitle = string.Join(", ", allAntonyms.Take(12)),
                                IcoPath = IconPath,
                                Score = 400,
                                Action = _ =>
                                {
                                    Clipboard.SetText(string.Join(", ", allAntonyms));
                                    return true;
                                }
                            }
                        };
                    }

                    return new List<Result>
                    {
                        new Result
                        {
                            Title = $"Search antonyms for '{cleanWord}' on Thesaurus.com",
                            SubTitle = "Press Enter to look up in online thesaurus",
                            IcoPath = IconPath,
                            Score = 350,
                            Action = _ =>
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = $"https://www.thesaurus.com/browse/{Uri.EscapeDataString(cleanWord)}",
                                    UseShellExecute = true
                                });
                                return true;
                            }
                        }
                    };
                }

                if (allSynonyms.Count > 0)
                {
                    list.Add(new Result
                    {
                        Title = $"Synonyms: {string.Join(", ", allSynonyms.Take(8))}",
                        SubTitle = "Click to copy synonyms list",
                        IcoPath = IconPath,
                        Score = 270,
                        Action = _ =>
                        {
                            Clipboard.SetText(string.Join(", ", allSynonyms));
                            return true;
                        }
                    });
                }

                return list.Take(6).ToList();
            }
            catch
            {
                return null;
            }
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return new List<Result>();
        }
    }
}
