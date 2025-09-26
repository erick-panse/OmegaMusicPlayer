using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Infrastructure.Services;
using OmegaMusicPlayer.Infrastructure.Services.Database;
using OmegaMusicPlayer.UI;
using System;

namespace OmegaMusicPlayer.Features.Shell.Views
{
    public partial class ErrorWindow : Window
    {
        private TextBlock _titleText;
        private TextBlock _statusText;
        private TextBlock _errorDetailsText;
        private ScrollViewer _errorDetailsScroll;
        private Button _exitButton;

        public event EventHandler ExitRequested;
        private readonly LocalizationService _localizationService;

        public ErrorWindow()
        {
            InitializeComponent();
            FindControls();

            var localizationService = App.ServiceProvider.GetRequiredService<LocalizationService>();
            _localizationService = localizationService;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void FindControls()
        {
            _titleText = this.FindControl<TextBlock>("TitleText");
            _statusText = this.FindControl<TextBlock>("StatusText");
            _errorDetailsText = this.FindControl<TextBlock>("ErrorDetailsText");
            _errorDetailsScroll = this.FindControl<ScrollViewer>("ErrorDetailsScroll");
            _exitButton = this.FindControl<Button>("ExitButton");
        }

        /// <summary>
        /// Show database error in the window
        /// </summary>
        public void ShowDatabaseError(DatabaseErrorHandlingService.DatabaseError error, string phase)
        {
            if (_titleText != null)
            {
                _titleText.Text = error.UserFriendlyTitle;
            }

            if (_statusText != null)
            {
                _statusText.Text = error.UserFriendlyMessage;
            }

            ShowTechnicalDetails(error.TechnicalDetails, error.TroubleshootingSteps, phase, error.Category.ToString());

            if (_exitButton != null)
            {
                _exitButton.IsVisible = true;
            }
        }

        /// <summary>
        /// Show general initialization error
        /// </summary>
        public void ShowInitializationError(string title, string message, string technicalDetails)
        {
            if (_titleText != null)
            {
                _titleText.Text = title;
            }

            if (_statusText != null)
            {
                _statusText.Text = message;
            }

            var troubleshootingSteps = new[]
            {
                _localizationService["Troubleshoot_RunAsAdmin"],
                _localizationService["Troubleshoot_CheckPermissions"],
                _localizationService["Troubleshoot_CheckDiskSpace"],
                _localizationService["Troubleshoot_DisableAntivirusTemporary"]
            };

            ShowTechnicalDetails(technicalDetails, troubleshootingSteps, "Initialization", "System");

            if (_exitButton != null)
            {
                _exitButton.IsVisible = true;
            }
        }

        /// <summary>
        /// Show technical details section
        /// </summary>
        private void ShowTechnicalDetails(string technicalDetails, string[] troubleshootingSteps, string phase, string category)
        {
            if (_errorDetailsText != null && _errorDetailsScroll != null)
            {
                var technicalInfo = $"{_localizationService["ErrorWindow_PhaseLabel"]}: {phase}\n" +
                                   $"{_localizationService["ErrorWindow_CategoryLabel"]}: {category}\n\n" +
                                   $"{_localizationService["ErrorWindow_TechnicalDetailsLabel"]}:\n{technicalDetails}";  

                if (troubleshootingSteps?.Length > 0)
                {
                    technicalInfo += $"\n\n{_localizationService["ErrorWindow_TroubleshootingStepsLabel"]}:\n";
                    for (int i = 0; i < troubleshootingSteps.Length; i++)
                    {
                        technicalInfo += $"{i + 1}. {troubleshootingSteps[i]}\n";
                    }
                }

                _errorDetailsText.Text = technicalInfo;
                _errorDetailsScroll.IsVisible = true;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}