using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Infrastructure.Services;
using System;

namespace OmegaPlayer.UI.Markup
{
    public class LocalizeExtension : MarkupExtension
    {
        private IErrorHandlingService _errorHandlingService;
        public string Key { get; set; }

        public LocalizeExtension(string key)
        {
            _errorHandlingService = App.ServiceProvider.GetService<IErrorHandlingService>();

            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            try
            {
                var locService = App.ServiceProvider.GetRequiredService<LocalizationService>();

                // Create a binding to the Translations dictionary with the specified key
                var binding = new Binding
                {
                    Source = locService,
                    Path = $"Translations[{Key}]",
                    Mode = BindingMode.OneWay,
                    // Fallback to the key itself if not found in dictionary
                    FallbackValue = Key
                };

                return binding;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error in LocalizeExtension",
                    ex.Message,
                    ex,
                    false);

                return Key;
            }
        }
    }
}