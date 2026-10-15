using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Note
{
    public class Main : IPlugin, IContextMenu
    {
        private PluginInitContext? _context;
        private string IconPath => _iconPath ??= Path.Combine(_context?.CurrentPluginMetadata.PluginDirectory ?? AppDomain.CurrentDomain.BaseDirectory, "Images", "icon.png");
        private string? _iconPath;
        private string _storageFile = string.Empty;
        private readonly List<NoteItem> _notes = new();
        private readonly object _lock = new();

        public class NoteItem
        {
            public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public string Content { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; } = DateTime.Now;
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var noteDir = Path.Combine(appData, "FlowLauncher", "Settings", "Plugins", "Flow.Launcher.Plugin.Note");
            Directory.CreateDirectory(noteDir);
            _storageFile = Path.Combine(noteDir, "notes.json");

            LoadNotes();
        }

        private void LoadNotes()
        {
            lock (_lock)
            {
                _notes.Clear();
                if (File.Exists(_storageFile))
                {
                    try
                    {
                        var json = File.ReadAllText(_storageFile);
                        var loaded = JsonSerializer.Deserialize<List<NoteItem>>(json);
                        if (loaded != null)
                        {
                            _notes.AddRange(loaded.OrderByDescending(n => n.CreatedAt));
                        }
                    }
                    catch
                    {
                        // Fallback gracefully
                    }
                }
            }
        }

        private void SaveNotes()
        {
            lock (_lock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_notes, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_storageFile, json);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            var search = query.Search.Trim();
            var lower = search.ToLowerInvariant();

            // 1. Clear command
            if (lower == "clear" || lower == "del all" || lower == "delete all")
            {
                return new List<Result>
                {
                    new Result
                    {
                        Title = "Clear All Notes",
                        SubTitle = $"Delete all {_notes.Count} saved notes",
                        IcoPath = IconPath,
                        Score = 400,
                        Action = _ =>
                        {
                            lock (_lock) { _notes.Clear(); }
                            SaveNotes();
                            _context?.API.ShowMsg("Notes Cleared", "All saved notes have been removed.", IconPath);
                            return true;
                        }
                    }
                };
            }

            // 2. Delete note commands: "note del", "note delete", or "note del 1"
            if (lower == "del" || lower == "delete" || lower == "rm")
            {
                var delResults = new List<Result>();
                lock (_lock)
                {
                    if (_notes.Count == 0)
                    {
                        delResults.Add(new Result
                        {
                            Title = "No notes to delete",
                            SubTitle = "You have not saved any notes yet",
                            IcoPath = IconPath,
                            Score = 400,
                            Action = _ => false
                        });
                        return delResults;
                    }

                    int idx = 1;
                    foreach (var note in _notes)
                    {
                        var target = note;
                        var currentIdx = idx;
                        delResults.Add(new Result
                        {
                            Title = $"Delete Note #{currentIdx}: \"{target.Content}\"",
                            SubTitle = "Press Enter to permanently remove this note",
                            IcoPath = IconPath,
                            Score = 400 - currentIdx,
                            Action = _ =>
                            {
                                lock (_lock) { _notes.Remove(target); }
                                SaveNotes();
                                _context?.API.ShowMsg("Note Deleted", target.Content, IconPath);
                                _context?.API.ReQuery();
                                return true;
                            }
                        });
                        idx++;
                    }
                }
                return delResults;
            }

            if (lower.StartsWith("del ") || lower.StartsWith("delete ") || lower.StartsWith("rm "))
            {
                var numStr = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (int.TryParse(numStr, out var idx) && idx >= 1 && idx <= _notes.Count)
                {
                    var target = _notes[idx - 1];
                    return new List<Result>
                    {
                        new Result
                        {
                            Title = $"Delete Note #{idx}: \"{target.Content}\"",
                            SubTitle = "Press Enter to permanently remove this note",
                            IcoPath = IconPath,
                            Score = 400,
                            Action = _ =>
                            {
                                lock (_lock) { _notes.Remove(target); }
                                SaveNotes();
                                _context?.API.ShowMsg("Note Deleted", target.Content, IconPath);
                                return true;
                            }
                        }
                    };
                }
            }

            // 3. Adding a new note: if search is not empty and not a command, offer saving note at the top!
            if (!string.IsNullOrWhiteSpace(search) && !lower.StartsWith("del") && !lower.StartsWith("rm"))
            {
                var noteToAdd = search.StartsWith("add ", StringComparison.OrdinalIgnoreCase)
                    ? search[4..].Trim()
                    : search;

                if (!string.IsNullOrWhiteSpace(noteToAdd))
                {
                    results.Add(new Result
                    {
                        Title = $"Save Note: \"{noteToAdd}\"",
                        SubTitle = "Press Enter to save to quick notes",
                        IcoPath = IconPath,
                        Score = 350,
                        Action = _ =>
                        {
                            var newNote = new NoteItem
                            {
                                Content = noteToAdd,
                                CreatedAt = DateTime.Now
                            };
                            lock (_lock)
                            {
                                _notes.Insert(0, newNote);
                            }
                            SaveNotes();
                            _context?.API.ShowMsg("Note Saved!", noteToAdd, IconPath);
                            return true;
                        }
                    });
                }
            }

            // 4. List / search existing notes
            lock (_lock)
            {
                if (_notes.Count == 0 && string.IsNullOrWhiteSpace(search))
                {
                    results.Add(new Result
                    {
                        Title = "Quick Notes & Scratchpad",
                        SubTitle = "Type 'note <text>' to quickly capture ideas, links, or to-dos",
                        IcoPath = IconPath,
                        Score = 100,
                        Action = _ => false
                    });
                    return results;
                }

                int index = 1;
                foreach (var note in _notes)
                {
                    if (string.IsNullOrWhiteSpace(search) ||
                        note.Content.Contains(search, StringComparison.OrdinalIgnoreCase))
                    {
                        var timeAgo = FormatTimeAgo(note.CreatedAt);
                        var currentNote = note;
                        var currentIdx = index;

                        results.Add(new Result
                        {
                            Title = $"{index}. {note.Content}",
                            SubTitle = $"{timeAgo} • Enter: Copy • Right-click: Delete • or type 'note del {index}'",
                            IcoPath = IconPath,
                            Score = string.IsNullOrWhiteSpace(search) ? 100 - index : 200,
                            ContextData = currentNote,
                            Action = _ =>
                            {
                                Clipboard.SetText(currentNote.Content);
                                _context?.API.ShowMsg("Copied to Clipboard", currentNote.Content, IconPath);
                                return true;
                            }
                        });
                    }
                    index++;
                }
            }

            return results;
        }

        private static string FormatTimeAgo(DateTime dt)
        {
            var span = DateTime.Now - dt;
            if (span.TotalSeconds < 60) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return dt.ToString("MMM d");
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var menus = new List<Result>();
            if (selectedResult.ContextData is NoteItem note)
            {
                menus.Add(new Result
                {
                    Title = "Copy Note to Clipboard",
                    SubTitle = note.Content,
                    IcoPath = IconPath,
                    Action = _ =>
                    {
                        Clipboard.SetText(note.Content);
                        return true;
                    }
                });

                menus.Add(new Result
                {
                    Title = "Delete Note",
                    SubTitle = "Permanently remove this note",
                    IcoPath = IconPath,
                    Action = _ =>
                    {
                        lock (_lock) { _notes.Remove(note); }
                        SaveNotes();
                        _context?.API.ShowMsg("Note Removed", note.Content, IconPath);
                        return true;
                    }
                });

                menus.Add(new Result
                {
                    Title = "Export All Notes to Desktop",
                    SubTitle = "Save notes.txt to Desktop",
                    IcoPath = IconPath,
                    Action = _ =>
                    {
                        try
                        {
                            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                            var dest = Path.Combine(desktop, "flow_notes.txt");
                            var lines = _notes.Select((n, i) => $"{i + 1}. [{n.CreatedAt:g}] {n.Content}");
                            File.WriteAllLines(dest, lines);
                            _context?.API.ShowMsg("Exported!", $"Saved {dest}", IconPath);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    }
                });
            }

            return menus;
        }
    }
}
