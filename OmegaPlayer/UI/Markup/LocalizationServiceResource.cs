using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Infrastructure.Services;
using System;

namespace OmegaPlayer.UI.Markup
{
    /// <summary>
    /// A resource that provides access to the LocalizationService for XAML.
    /// </summary>
    public class LocalizationServiceResource
    {
        private static LocalizationService _service;

        public static LocalizationService Instance =>
            _service ??= App.ServiceProvider.GetRequiredService<LocalizationService>();

        // This property allows direct binding to the translations
        public System.Collections.Generic.Dictionary<string, string> Translations => Instance.Translations;

        // Allow using this resource as if it was the service itself
        public static implicit operator LocalizationService(LocalizationServiceResource resource) => Instance;
    }
}