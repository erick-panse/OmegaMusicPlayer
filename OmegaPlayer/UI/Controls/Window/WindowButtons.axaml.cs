using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace OmegaPlayer.UI.Controls
{
    public partial class WindowButtons : UserControl
    {
        private Window _parentWindow;
        private IErrorHandlingService _errorHandlingService;
        private bool _isDisposed = false;

        public WindowButtons()
        {
            // Get error handling service if available
            _errorHandlingService = App.ServiceProvider?.GetService<IErrorHandlingService>();

            InitializeComponent();

            // Set initial maximize/restore tooltip
            MaximizeButton.Tag = "Maximize";

            // Subscribe to parent window attachment event
            AttachedToVisualTree += WindowButtons_AttachedToVisualTree;
        }

        private void WindowButtons_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            if (_isDisposed) return;

            // Find the parent window in the visual tree
            _parentWindow = this.FindAncestorOfType<Window>();

            if (_parentWindow != null)
            {
                // Update button state when window state changes
                _parentWindow.PropertyChanged += (s, args) =>
                {
                    if (_isDisposed) return;

                    if (args.Property == Window.WindowStateProperty)
                    {
                        UpdateMaximizeButtonState();
                    }
                };

                // Set initial state
                UpdateMaximizeButtonState();
            }
            else
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "WindowButtons warning",
                    "No parent window found for WindowButtons control",
                    null,
                    false);
            }
        }

        private void UpdateMaximizeButtonState()
        {
            _errorHandlingService.SafeExecute(() =>
            {
                if (_parentWindow.WindowState == WindowState.Maximized)
                {
                    MaximizeIcon.IsVisible = false;
                    RestoreIcon.IsVisible = true;
                    MaximizeButton.Tag = "Restore";
                }
                else
                {
                    MaximizeIcon.IsVisible = true;
                    RestoreIcon.IsVisible = false;
                    MaximizeButton.Tag = "Maximize";
                }
            },
            "Error updating maximize button state",
            ErrorSeverity.NonCritical,
            false);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDisposed || _parentWindow == null) return;

            _parentWindow.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDisposed || _parentWindow == null) return;

            _parentWindow.WindowState = _parentWindow.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDisposed || _parentWindow == null) return;

            _parentWindow?.Close();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isDisposed = true;

            // Unsubscribe from events
            AttachedToVisualTree -= WindowButtons_AttachedToVisualTree;

            // Clear references
            _parentWindow = null;

            base.OnDetachedFromVisualTree(e);
        }
    }
}