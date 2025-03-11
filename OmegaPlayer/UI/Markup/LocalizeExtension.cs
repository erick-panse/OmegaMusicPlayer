using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.UI;
using System;
using System.Diagnostics;

namespace OmegaPlayer.UI.Markup
{
    public class LocalizeExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocalizeExtension(string key)
        {
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
                Debug.WriteLine($"Error in LocalizeExtension: {ex}");
                return Key;
            }
        }
    }
}