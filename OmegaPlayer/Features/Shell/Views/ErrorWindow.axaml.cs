using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OmegaPlayer.Infrastructure.Services.Database;
using System;

namespace OmegaPlayer.Features.Shell.Views
{
    public partial class ErrorWindow : Window
    {
        private TextBlock _titleText;
        private TextBlock _statusText;
        private TextBlock _errorDetailsText;
        private ScrollViewer _errorDetailsScroll;
        private Button _exitButton;

        public event EventHandler ExitRequested;

        public ErrorWindow()
        {
            InitializeComponent();
            FindControls();
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
                _titleText.Text = "Database Initialization Failed";
            }

            if (_statusText != null)
            {
                _statusText.Text = $"{error.UserFriendlyTitle}\n\n{error.UserFriendlyMessage}";
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
                "Run the application as administrator",
                "Check that the installation folder has write permissions",
                "Ensure sufficient disk space is available",
                "Temporarily disable antivirus software",
                "Contact support if the problem persists"
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
                var technicalInfo = $"Phase: {phase}\n" +
                                   $"Category: {category}\n\n" +
                                   $"Technical Details:\n{technicalDetails}";

                if (troubleshootingSteps?.Length > 0)
                {
                    technicalInfo += "\n\nTroubleshooting Steps:\n";
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