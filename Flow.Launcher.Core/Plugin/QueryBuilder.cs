using System;
using System.Collections.Generic;
using Flow.Launcher.Infrastructure.Native;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    public static class QueryBuilder
    {
        public static Query Build(string originalQuery, string trimmedQuery, Dictionary<string, PluginPair> nonGlobalPlugins)
        {
            // home query
            if (string.IsNullOrEmpty(trimmedQuery))
            {
                return new Query()
                {
                    Search = string.Empty,
                    OriginalQuery = string.Empty,
                    TrimmedQuery = string.Empty,
                    SearchTerms = Array.Empty<string>(),
                    ActionKeyword = string.Empty,
                    IsHomeQuery = true
                };
            }

            ReadOnlySpan<char> span = trimmedQuery.AsSpan().Trim();
            if (span.IsEmpty)
            {
                // nothing was typed
                return null;
            }

            // Find possible action keyword (first space-separated token)
            int firstSpace = span.IndexOf(' ');
            string possibleActionKeyword = firstSpace == -1 ? span.ToString() : span.Slice(0, firstSpace).ToString();

            string actionKeyword = string.Empty;
            if (nonGlobalPlugins != null &&
                nonGlobalPlugins.TryGetValue(possibleActionKeyword, out var pluginPair) &&
                !pluginPair.Metadata.Disabled)
            {
                actionKeyword = possibleActionKeyword;
            }

            Span<NativeQueryTerm> termsSpan = stackalloc NativeQueryTerm[64];
            if (FlowNativeMethods.TokenizeQuery(trimmedQuery.AsSpan(), actionKeyword.AsSpan(), termsSpan, out var parsed))
            {
                if (parsed.IsHomeQuery != 0)
                {
                    return null;
                }

                string search = parsed.SearchLength == 0
                    ? string.Empty
                    : trimmedQuery.Substring((int)parsed.SearchOffset, (int)parsed.SearchLength);

                string[] searchTerms;
                if (parsed.TermCount == 0)
                {
                    searchTerms = Array.Empty<string>();
                }
                else
                {
                    searchTerms = new string[parsed.TermCount];
                    for (int i = 0; i < parsed.TermCount; i++)
                    {
                        searchTerms[i] = trimmedQuery.Substring((int)termsSpan[i].Start, (int)termsSpan[i].Length);
                    }
                }

                return new Query()
                {
                    Search = search,
                    OriginalQuery = originalQuery,
                    TrimmedQuery = trimmedQuery,
                    SearchTerms = searchTerms,
                    ActionKeyword = actionKeyword,
                    IsHomeQuery = false
                };
            }

            // Fallback in case native parsing returned false
            var terms = trimmedQuery.Split(Query.TermSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0)
            {
                return null;
            }

            string fallbackSearch;
            string[] fallbackSearchTerms;

            if (!string.IsNullOrEmpty(actionKeyword))
            {
                fallbackSearch = terms.Length > 1 ? trimmedQuery[(actionKeyword.Length + 1)..].TrimStart() : string.Empty;
                fallbackSearchTerms = terms[1..];
            }
            else
            {
                fallbackSearch = trimmedQuery.TrimStart();
                fallbackSearchTerms = terms;
            }

            return new Query()
            {
                Search = fallbackSearch,
                OriginalQuery = originalQuery,
                TrimmedQuery = trimmedQuery,
                SearchTerms = fallbackSearchTerms,
                ActionKeyword = actionKeyword,
                IsHomeQuery = false
            };
        }
    }
}
