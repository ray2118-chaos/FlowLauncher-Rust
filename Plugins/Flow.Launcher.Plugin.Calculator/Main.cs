using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure.Native;
using Flow.Launcher.Plugin.Calculator.ViewModels;
using Flow.Launcher.Plugin.Calculator.Views;

namespace Flow.Launcher.Plugin.Calculator
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider
    {
        private static readonly Regex ThousandGroupRegex = new(@"\B(?=(\d{3})+(?!\d))", RegexOptions.Compiled);
        private const string Comma = ",";
        private const string Dot = ".";
        private const string IcoPath = "Images/calculator.png";
        private static readonly List<Result> EmptyResults = [];

        internal static PluginInitContext Context { get; private set; } = null!;

        private Settings _settings;
        private SettingsViewModel _viewModel;

        public void Init(PluginInitContext context)
        {
            Context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();
            _viewModel = new SettingsViewModel(_settings);
        }

        public List<Result> Query(Query query)
        {
            if (string.IsNullOrWhiteSpace(query.Search))
            {
                return EmptyResults;
            }

            try
            {
                var search = query.Search;
                var decimalSep = GetDecimalSeparator();

                string evalExpr = search;
                if (decimalSep == Comma)
                {
                    evalExpr = search.Replace(Comma, Dot);
                }

                if (FlowNativeMethods.IsAvailable)
                {
                    // 1. First attempt native numeric math evaluation
                    if (FlowNativeMethods.EvaluateMath(evalExpr.AsSpan(), out var doubleResult))
                    {
                        if (!double.IsNaN(doubleResult) && !double.IsInfinity(doubleResult))
                        {
                            decimal roundedResult;
                            try
                            {
                                roundedResult = Math.Round((decimal)doubleResult, _settings.MaxDecimalPlaces, MidpointRounding.AwayFromZero);
                            }
                            catch (OverflowException)
                            {
                                return EmptyResults;
                            }

                            var title = FormatResult(roundedResult);
                            return
                            [
                                new Result
                                {
                                    Title = title,
                                    IcoPath = IcoPath,
                                    Score = 300,
                                    SubTitle = Context == null ? string.Empty : Localize.flowlauncher_plugin_calculator_copy_number_to_clipboard(),
                                    CopyText = title,
                                    Action = c =>
                                    {
                                        try
                                        {
                                            Context.API.CopyToClipboard(title);
                                            return true;
                                        }
                                        catch (ExternalException)
                                        {
                                            Context.API.ShowMsgBox(Localize.flowlauncher_plugin_calculator_failed_to_copy());
                                            return false;
                                        }
                                    }
                                }
                            ];
                        }
                    }

                    // 2. Fallback to advanced queries (currency, unit conversion, time/date)
                    if (FlowNativeMethods.CalculateQuery(evalExpr.AsSpan(), out var nativeTitle, out var nativeSubtitle, out var copyText, out _))
                    {
                        return
                        [
                            new Result
                            {
                                Title = nativeTitle,
                                IcoPath = IcoPath,
                                Score = 300,
                                SubTitle = !string.IsNullOrEmpty(nativeSubtitle) ? nativeSubtitle : (Context == null ? string.Empty : Localize.flowlauncher_plugin_calculator_copy_number_to_clipboard()),
                                CopyText = !string.IsNullOrEmpty(copyText) ? copyText : nativeTitle,
                                Action = c =>
                                {
                                    try
                                    {
                                        Context.API.CopyToClipboard(!string.IsNullOrEmpty(copyText) ? copyText : nativeTitle);
                                        return true;
                                    }
                                    catch (ExternalException)
                                    {
                                        Context.API.ShowMsgBox(Localize.flowlauncher_plugin_calculator_failed_to_copy());
                                        return false;
                                    }
                                }
                            }
                        ];
                    }
                }

                if (!_settings.ShowErrorMessage) return EmptyResults;
                return
                [
                    new Result
                    {
                        Title = Localize.flowlauncher_plugin_calculator_expression_not_complete(),
                        IcoPath = IcoPath
                    }
                ];
            }
            catch (Exception)
            {
                if (!_settings.ShowErrorMessage) return EmptyResults;
                return
                [
                    new Result
                    {
                        Title = Localize.flowlauncher_plugin_calculator_expression_not_complete(),
                        IcoPath = IcoPath
                    }
                ];
            }
        }

        private string FormatResult(decimal roundedResult)
        {
            string decimalSeparator = GetDecimalSeparator();
            string groupSeparator = GetGroupSeparator(decimalSeparator);

            string resultStr = roundedResult.ToString(CultureInfo.InvariantCulture);

            string[] parts = resultStr.Split('.');
            string integerPart = parts[0];
            string fractionalPart = parts.Length > 1 ? parts[1] : string.Empty;

            if (_settings.UseThousandsSeparator && integerPart.Length > 3)
            {
                integerPart = ThousandGroupRegex.Replace(integerPart, groupSeparator);
            }

            if (!string.IsNullOrEmpty(fractionalPart))
            {
                return integerPart + decimalSeparator + fractionalPart;
            }

            return integerPart;
        }

        private string GetGroupSeparator(string decimalSeparator)
        {
            var culture = CultureInfo.CurrentCulture;
            var systemGroupSeparator = culture.NumberFormat.NumberGroupSeparator;

            if (_settings.DecimalSeparator == DecimalSeparator.UseSystemLocale)
            {
                return systemGroupSeparator;
            }

            if (decimalSeparator == systemGroupSeparator)
            {
                return decimalSeparator == Dot ? Comma : Dot;
            }

            return systemGroupSeparator;
        }

        private string GetDecimalSeparator()
        {
            string systemDecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            return _settings.DecimalSeparator switch
            {
                DecimalSeparator.UseSystemLocale => systemDecimalSeparator,
                DecimalSeparator.Dot => Dot,
                DecimalSeparator.Comma => Comma,
                _ => systemDecimalSeparator,
            };
        }

        public string GetTranslatedPluginTitle()
        {
            return Localize.flowlauncher_plugin_calculator_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_calculator_plugin_description();
        }

        public Control CreateSettingPanel()
        {
            return new CalculatorSettings(_settings);
        }

        public void OnCultureInfoChanged(CultureInfo newCulture)
        {
            DecimalSeparatorLocalized.UpdateLabels(_viewModel.AllDecimalSeparator);
        }
    }
}
