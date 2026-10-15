using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Remind
{
    public class Main : IPlugin, IContextMenu
    {
        private PluginInitContext? _context;
        private const string IconPath = "Images\\icon.png";
        private static readonly List<ActiveReminder> _activeReminders = new();
        private static readonly object _lock = new();
        private static System.Threading.Timer? _checkTimer;
        private static int _waterMlToday = 0;
        private static DateTime _waterDate = DateTime.Today;

        public class ActiveReminder
        {
            public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public string Title { get; set; } = string.Empty;
            public DateTime TargetTime { get; set; }
            public bool IsTimer { get; set; }
            public TimeSpan OriginalDuration { get; set; }
        }

        public void Init(PluginInitContext context)
        {
            _context = context;

            lock (_lock)
            {
                if (_checkTimer == null)
                {
                    _checkTimer = new System.Threading.Timer(OnTick, null, 1000, 1000);
                }
            }
        }

        private void OnTick(object? state)
        {
            List<ActiveReminder> due = new();
            lock (_lock)
            {
                var now = DateTime.Now;
                due = _activeReminders.Where(r => r.TargetTime <= now).ToList();
                foreach (var d in due)
                {
                    _activeReminders.Remove(d);
                }
            }

            foreach (var reminder in due)
            {
                try
                {
                    SystemSounds.Exclamation.Play();
                }
                catch
                {
                    // ignored
                }

                _context?.API.ShowMsg(
                    reminder.IsTimer ? "⏱️ Timer Complete!" : "⏰ Reminder Alert!",
                    reminder.Title,
                    IconPath);
            }
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            var search = query.Search.Trim();
            var lower = search.ToLowerInvariant();
            bool isGlobal = query.ActionKeyword == "*";
            bool isTimerKeyword = query.ActionKeyword.Equals("timer", StringComparison.OrdinalIgnoreCase);

            // 1. Water Tracker query ("water", "water 250", "water log 500")
            if (query.ActionKeyword.Equals("water", StringComparison.OrdinalIgnoreCase) || lower.StartsWith("water"))
            {
                return QueryWater(search);
            }

            // 2. Querying with empty search
            if (string.IsNullOrWhiteSpace(search))
            {
                if (isGlobal)
                {
                    // Do not show empty query results in global wildcard mode
                    return results;
                }

                lock (_lock)
                {
                    if (_activeReminders.Count > 0)
                    {
                        foreach (var rem in _activeReminders.OrderBy(r => r.TargetTime))
                        {
                            var remaining = rem.TargetTime - DateTime.Now;
                            var remStr = remaining.TotalSeconds > 0
                                ? $"{FormatTimeSpan(remaining)} remaining"
                                : "due now";

                            results.Add(new Result
                            {
                                Title = rem.Title,
                                SubTitle = $"{remStr} • Due at {rem.TargetTime:t} • Press Enter to Cancel",
                                IcoPath = IconPath,
                                Score = 200,
                                Action = _ =>
                                {
                                    lock (_lock) { _activeReminders.Remove(rem); }
                                    _context?.API.ShowMsg("Reminder Cancelled", rem.Title, IconPath);
                                    return true;
                                }
                            });
                        }
                    }
                }

                results.Add(new Result
                {
                    Title = "Set 5 Minute Timer",
                    SubTitle = "Usage: timer 25min, set 5 min timer, set timer for 5 min, remind 15m stretch",
                    IcoPath = IconPath,
                    Score = 90,
                    Action = _ =>
                    {
                        AddTimer(TimeSpan.FromMinutes(5), "5m Timer");
                        return true;
                    }
                });

                results.Add(new Result
                {
                    Title = "Set 25 Minute Focus Session (Pomodoro)",
                    SubTitle = "Quick focus sprint timer",
                    IcoPath = IconPath,
                    Score = 80,
                    Action = _ =>
                    {
                        AddTimer(TimeSpan.FromMinutes(25), "25m Focus Session");
                        return true;
                    }
                });

                if (isTimerKeyword)
                {
                    results.Add(new Result
                    {
                        Title = "Set 10 Minute Timer",
                        SubTitle = "Quick 10m countdown",
                        IcoPath = IconPath,
                        Score = 75,
                        Action = _ =>
                        {
                            AddTimer(TimeSpan.FromMinutes(10), "10m Timer");
                            return true;
                        }
                    });

                    results.Add(new Result
                    {
                        Title = "Set 1 Hour Timer",
                        SubTitle = "60-minute countdown",
                        IcoPath = IconPath,
                        Score = 70,
                        Action = _ =>
                        {
                            AddTimer(TimeSpan.FromHours(1), "1h Timer");
                            return true;
                        }
                    });
                }
                else
                {
                    results.Add(new Result
                    {
                        Title = $"Today's Water: {_waterMlToday} ml / 2000 ml",
                        SubTitle = "Type 'water 250' to quickly log a cup of water",
                        IcoPath = IconPath,
                        Score = 70,
                        Action = _ =>
                        {
                            LogWater(250);
                            return true;
                        }
                    });
                }

                return results;
            }

            // 3. Parse Natural Language Reminders & Timers
            var effectiveInput = search;
            if (isTimerKeyword && !lower.StartsWith("timer ") && !lower.StartsWith("set ") && !lower.Contains("timer"))
            {
                effectiveInput = "timer " + search;
            }

            var parseResult = TryParseReminder(effectiveInput);
            if (parseResult != null)
            {
                var (targetTime, message, isTimer, duration) = parseResult.Value;
                var remaining = targetTime - DateTime.Now;

                results.Add(new Result
                {
                    Title = isTimer
                        ? (message.Equals($"{FormatTimeSpan(duration)} Timer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(message)
                            ? $"Start {FormatTimeSpan(duration)} Timer"
                            : $"Start {FormatTimeSpan(duration)} Timer: {message}")
                        : $"Set Reminder for {targetTime:t} ({FormatTimeSpan(remaining)}): {message}",
                    SubTitle = $"Fires at {targetTime:g} • Press Enter to confirm",
                    IcoPath = IconPath,
                    Score = 350,
                    Action = _ =>
                    {
                        lock (_lock)
                        {
                            _activeReminders.Add(new ActiveReminder
                            {
                                Title = message,
                                TargetTime = targetTime,
                                IsTimer = isTimer,
                                OriginalDuration = duration
                            });
                        }

                        _context?.API.ShowMsg(
                            isTimer ? "Timer Started" : "Reminder Set",
                            $"{message} (at {targetTime:t})",
                            IconPath);

                        return true;
                    }
                });
            }
            else if (!isGlobal)
            {
                results.Add(new Result
                {
                    Title = $"Set Reminder: '{search}'",
                    SubTitle = "Examples: 'timer 25min', 'set 5 min timer', 'set timer for 5 min', 'in 45m meeting', 'at 4pm call mom'",
                    IcoPath = IconPath,
                    Score = 50,
                    Action = _ => false
                });
            }

            return results;
        }

        private static (DateTime targetTime, string message, bool isTimer, TimeSpan duration)? TryParseReminder(string input)
        {
            var text = input.Trim();
            bool isTimer = false;

            // Pattern: "set [a] timer for <duration> [message]" e.g. "set timer for 5 min", "set timer for 25 minutes"
            var setTimerForMatch = Regex.Match(text, @"^(?:set\s+(?:a\s+)?)?timer(?:\s+for)?\s+(.+)$", RegexOptions.IgnoreCase);
            if (setTimerForMatch.Success)
            {
                isTimer = true;
                text = setTimerForMatch.Groups[1].Value.Trim();
            }

            // Pattern: "set <duration> timer [message]" e.g. "set 5 min timer", "set 25m timer focus sprint"
            var setDurTimerMatch = Regex.Match(text, @"^set\s+(.+?)\s+timer(?:\s+(.+))?$", RegexOptions.IgnoreCase);
            if (setDurTimerMatch.Success)
            {
                isTimer = true;
                var durPart = setDurTimerMatch.Groups[1].Value.Trim();
                var msgPart = setDurTimerMatch.Groups[2].Success ? setDurTimerMatch.Groups[2].Value.Trim() : "";
                text = $"{durPart} {msgPart}".Trim();
            }

            // Pattern: "<duration> timer [message]" e.g. "5 min timer", "25m timer"
            var durTimerMatch = Regex.Match(text, @"^(.+?)\s+timer(?:\s+(.+))?$", RegexOptions.IgnoreCase);
            if (durTimerMatch.Success)
            {
                isTimer = true;
                var durPart = durTimerMatch.Groups[1].Value.Trim();
                var msgPart = durTimerMatch.Groups[2].Success ? durTimerMatch.Groups[2].Value.Trim() : "";
                text = $"{durPart} {msgPart}".Trim();
            }

            // Pattern: "remind me [to <msg>] [in|at] <time>"
            var remindMeToMatch = Regex.Match(text, @"^remind(?:\s+me)?\s+to\s+(.+?)\s+(?:in|at)\s+(.+)$", RegexOptions.IgnoreCase);
            if (remindMeToMatch.Success)
            {
                var msgPart = remindMeToMatch.Groups[1].Value.Trim();
                var timePart = remindMeToMatch.Groups[2].Value.Trim();
                text = $"in {timePart} {msgPart}";
            }

            // Strip leading "remind me to " or "remind to " or "remind "
            text = Regex.Replace(text, @"^remind(?:\s+me)?(?:\s+to)?\s+", "", RegexOptions.IgnoreCase).Trim();

            if (text.StartsWith("in ", StringComparison.OrdinalIgnoreCase))
            {
                text = text[3..].Trim();
                isTimer = true;
            }

            // Pattern 1: Relative duration e.g. "25min", "25 min", "15m", "1h 30m", "45s", "1 hour", "2 hours lunch"
            var durRegex = new Regex(
                @"^(?:(\d+)\s*(?:h(?:ours?|rs?)?))?\s*" +
                @"(?:(\d+)\s*(?:m(?:in(?:utes?|s)?)?))?\s*" +
                @"(?:(\d+)\s*(?:s(?:ec(?:onds?|s)?)?))?" +
                @"(?:\s+(.*))?$",
                RegexOptions.IgnoreCase);

            var match = durRegex.Match(text);
            if (match.Success && (match.Groups[1].Success || match.Groups[2].Success || match.Groups[3].Success))
            {
                int hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
                int mins = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                int secs = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

                var duration = new TimeSpan(hours, mins, secs);
                if (duration.TotalSeconds > 0)
                {
                    var msg = match.Groups[4].Success ? match.Groups[4].Value.Trim() : "";
                    if (string.IsNullOrEmpty(msg))
                    {
                        msg = isTimer ? $"{FormatTimeSpan(duration)} Timer" : $"{FormatTimeSpan(duration)} Reminder";
                    }

                    return (DateTime.Now + duration, msg, isTimer, duration);
                }
            }

            // Pattern 2: Absolute time e.g. "at 4:30pm standup", "at 16:00 review", "4pm call mom"
            var atRegex = new Regex(@"^(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)\s*(.*)$", RegexOptions.IgnoreCase);
            var atMatch = atRegex.Match(text);
            if (atMatch.Success)
            {
                int hour = int.Parse(atMatch.Groups[1].Value);
                int min = atMatch.Groups[2].Success ? int.Parse(atMatch.Groups[2].Value) : 0;
                string ampm = atMatch.Groups[3].Value.ToLowerInvariant();
                string msg = atMatch.Groups[4].Success ? atMatch.Groups[4].Value.Trim() : "";

                if (ampm == "pm" && hour < 12) hour += 12;
                if (ampm == "am" && hour == 12) hour = 0;

                var now = DateTime.Now;
                var target = new DateTime(now.Year, now.Month, now.Day, hour, min, 0);
                if (target <= now)
                {
                    target = target.AddDays(1); // Set for tomorrow if time already passed today
                }

                if (string.IsNullOrEmpty(msg))
                {
                    msg = "Scheduled Reminder";
                }

                return (target, msg, false, target - now);
            }

            return null;
        }

        private List<Result> QueryWater(string search)
        {
            if (_waterDate != DateTime.Today)
            {
                _waterDate = DateTime.Today;
                _waterMlToday = 0;
            }

            var clean = search.Replace("water", "", StringComparison.OrdinalIgnoreCase).Replace("log", "", StringComparison.OrdinalIgnoreCase).Replace("ml", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (int.TryParse(clean, out var ml) && ml > 0)
            {
                return new List<Result>
                {
                    new Result
                    {
                        Title = $"Log {ml} ml Water",
                        SubTitle = $"Current: {_waterMlToday} ml • New Total: {_waterMlToday + ml} ml / 2000 ml",
                        IcoPath = IconPath,
                        Score = 300,
                        Action = _ =>
                        {
                            LogWater(ml);
                            return true;
                        }
                    }
                };
            }

            var percent = Math.Min(100, (int)(_waterMlToday / 2000.0 * 100));
            return new List<Result>
            {
                new Result
                {
                    Title = $"Today's Hydration: {_waterMlToday} ml / 2,000 ml ({percent}%)",
                    SubTitle = "Click to log standard glass (+250 ml), or type 'water 500'",
                    IcoPath = IconPath,
                    Score = 300,
                    Action = _ =>
                    {
                        LogWater(250);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Log Bottle (+500 ml)",
                    SubTitle = $"New total: {_waterMlToday + 500} ml",
                    IcoPath = IconPath,
                    Score = 200,
                    Action = _ =>
                    {
                        LogWater(500);
                        return true;
                    }
                }
            };
        }

        private void LogWater(int ml)
        {
            _waterMlToday += ml;
            _context?.API.ShowMsg(
                "💧 Water Logged!",
                $"+{ml} ml added. Today: {_waterMlToday} ml / 2000 ml",
                IconPath);
        }

        private void AddTimer(TimeSpan dur, string title)
        {
            lock (_lock)
            {
                _activeReminders.Add(new ActiveReminder
                {
                    Title = title,
                    TargetTime = DateTime.Now + dur,
                    IsTimer = true,
                    OriginalDuration = dur
                });
            }
            _context?.API.ShowMsg("⏱️ Timer Started", $"{title} ({FormatTimeSpan(dur)})", IconPath);
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}m {ts.Seconds}s";
            return $"{Math.Max(1, (int)ts.TotalSeconds)}s";
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return new List<Result>();
        }
    }
}
