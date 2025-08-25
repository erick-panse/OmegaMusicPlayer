using Avalonia.Data.Converters;
using OmegaPlayer.Features.Shell.ViewModels;
using System;
using System.Globalization;

namespace OmegaPlayer.UI.Converters
{
    /// <summary>
    /// Converter to check if current setup step matches a parameter
    /// </summary>
    public class SetupStepConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SetupViewModel.SetupStep step && parameter is string paramStr)
            {
                return paramStr switch
                {
                    "Language" => step == SetupViewModel.SetupStep.Language,
                    "Theme" => step == SetupViewModel.SetupStep.Theme,
                    "ProfileName" => step == SetupViewModel.SetupStep.ProfileName,
                    "LibraryFolder" => step == SetupViewModel.SetupStep.LibraryFolder,
                    "Welcome" => step == SetupViewModel.SetupStep.Welcome,
                    "NotWelcome" => step != SetupViewModel.SetupStep.Welcome, // Show "Next" button for all steps except Welcome
                    _ => false
                };
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to check if count is 0 for visibility (shows when count is 0)
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count == 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}