using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.PluginIndicator
{
    public class Main : IPlugin, IPluginI18n, IHomeQuery
    {
        internal static PluginInitContext Context { get; private set; }

        public void Init(PluginInitContext context)
        {
            Context = context;
        }

        public List<Result> Query(Query query)
        {
            return QueryResults(query);
        }

        private static List<Result> QueryResults(Query query = null)
        {
            var nonGlobalPlugins = GetNonGlobalPlugins();
            var querySearch = query?.Search ?? string.Empty;

            var results =
                from keyword in nonGlobalPlugins.Keys
                let plugin = nonGlobalPlugins[keyword].Metadata
                let keywordSearchResult = Context.API.FuzzySearch(querySearch, keyword)
                let searchResult = keywordSearchResult.IsSearchPrecisionScoreMet() ? keywordSearchResult : Context.API.FuzzySearch(querySearch, plugin.Name)
                let score = searchResult.Score
                where (searchResult.IsSearchPrecisionScoreMet()
                        || string.IsNullOrEmpty(querySearch)) // To list all available action keywords
                    && !plugin.Disabled
                select new Result
                {
                    Title = keyword,
                    SubTitle = Localize.flowlauncher_plugin_pluginindicator_result_subtitle(plugin.Name),
                    Score = score,
                    IcoPath = ResolveKeywordIcon(nonGlobalPlugins[keyword], keyword),
                    AutoCompleteText = $"{keyword}{Plugin.Query.TermSeparator}",
                    Action = c =>
                    {
                        Context.API.ChangeQuery($"{keyword}{Plugin.Query.TermSeparator}");
                        return false;
                    }
                };
            return [.. results];
        }

        private static string ResolveKeywordIcon(PluginPair pair, string keyword)
        {
            var metadata = pair.Metadata;
            try
            {
                var lowerKey = keyword.ToLowerInvariant();

                // 1. Check if plugin is Web Searches
                bool isWebSearch = metadata.ID.Equals("565B73353DBF4806919830B9202EE3BF", System.StringComparison.OrdinalIgnoreCase) ||
                                   metadata.ID.Equals("5725204E98B94E1B862F36104D693DF4", System.StringComparison.OrdinalIgnoreCase) ||
                                   metadata.Name.Contains("Web Search", System.StringComparison.OrdinalIgnoreCase) ||
                                   (metadata.ExecuteFileName != null && metadata.ExecuteFileName.Contains("WebSearch", System.StringComparison.OrdinalIgnoreCase));

                if (isWebSearch)
                {
                    var iconName = lowerKey switch
                    {
                        "maps" => "google_maps.png",
                        "translate" => "google_translate.png",
                        "findicon" => "pictures.png",
                        "sc" => "google.png",
                        "drive" => "google_drive.png",
                        "wiki" => "wiki.png",
                        "lucky" => "google.png",
                        "bd" => "baidu.png",
                        "youtube" => "youtube.png",
                        "youtubemusic" => "youtubemusic.png",
                        "duckduckgo" => "duckduckgo.png",
                        "github" => "github.png",
                        "gist" => "gist.png",
                        "gmail" => "gmail.png",
                        "wolframalpha" => "wolframalpha.png",
                        "stackoverflow" => "stackoverflow.png",
                        "bing" => "bing.png",
                        "yahoo" => "yahoo.png",
                        "netflix" => "netflix.png",
                        _ => $"{lowerKey}.png"
                    };

                    // Check plugin images directory
                    if (!string.IsNullOrEmpty(metadata.PluginDirectory))
                    {
                        var iconPath = System.IO.Path.Combine(metadata.PluginDirectory, "Images", iconName);
                        if (System.IO.File.Exists(iconPath)) return iconPath;

                        var appBasePath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Plugins", System.IO.Path.GetFileName(metadata.PluginDirectory), "Images", iconName);
                        if (System.IO.File.Exists(appBasePath)) return appBasePath;
                    }

                    // Also check custom WebSearch settings if user customized the icon
                    var settingsPath = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                        "FlowLauncher", "Settings", "Plugins", "Flow.Launcher.Plugin.WebSearch", "Settings.json");

                    if (System.IO.File.Exists(settingsPath))
                    {
                        var content = System.IO.File.ReadAllText(settingsPath);
                        var match = System.Text.RegularExpressions.Regex.Match(
                            content,
                            $@"""ActionKeyword""\s*:\s*""{System.Text.RegularExpressions.Regex.Escape(keyword)}""[^}}]*""Icon""\s*:\s*""([^""]+)""",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        if (match.Success)
                        {
                            var customIcon = match.Groups[1].Value;
                            if (System.IO.File.Exists(customIcon)) return customIcon;
                            if (!string.IsNullOrEmpty(metadata.PluginDirectory))
                            {
                                var combined = System.IO.Path.Combine(metadata.PluginDirectory, "Images", customIcon);
                                if (System.IO.File.Exists(combined)) return combined;
                            }
                        }
                    }
                }

                // 2. General plugin keyword-specific image check
                if (!string.IsNullOrEmpty(metadata.PluginDirectory))
                {
                    var specificPath = System.IO.Path.Combine(metadata.PluginDirectory, "Images", $"{lowerKey}.png");
                    if (System.IO.File.Exists(specificPath))
                        return specificPath;
                }
            }
            catch
            {
                // Fallback to plugin default
            }

            return metadata.IcoPath;
        }

        private static Dictionary<string, PluginPair> GetNonGlobalPlugins()
        {
            var nonGlobalPlugins = new Dictionary<string, PluginPair>();
            foreach (var plugin in Context.API.GetAllPlugins())
            {
                foreach (var actionKeyword in plugin.Metadata.ActionKeywords)
                {
                    // Skip global keywords
                    if (actionKeyword == Plugin.Query.GlobalPluginWildcardSign) continue;

                    // Skip dulpicated keywords
                    if (nonGlobalPlugins.ContainsKey(actionKeyword)) continue;

                    nonGlobalPlugins.Add(actionKeyword, plugin);
                }
            }
            return nonGlobalPlugins;
        }

        public string GetTranslatedPluginTitle()
        {
            return Localize.flowlauncher_plugin_pluginindicator_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_pluginindicator_plugin_description();
        }

        public List<Result> HomeQuery()
        {
            return QueryResults();
        }
    }
}
