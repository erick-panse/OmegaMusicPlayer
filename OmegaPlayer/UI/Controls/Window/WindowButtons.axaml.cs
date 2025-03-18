using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace OmegaPlayer.UI.Controls
{
    public partial class WindowButtons : UserControl
    {
        private Window _parentWindow;

        public WindowButtons()
        {
            InitializeComponent();

            // Set initial maximize/restore tooltip
            MaximizeButton.Tag = "Maximize";

            // Subscribe to parent window attachment event
            AttachedToVisualTree += WindowButtons_AttachedToVisualTree;
        }

        private void WindowButtons_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            _parentWindow = this.FindAncestorOfType<Window>();

            if (_parentWindow != null)
            {
                // Update button state when window state changes
                _parentWindow.PropertyChanged += (s, args) =>
                {
                    if (args.Property == Window.WindowStateProperty)
                    {
                        UpdateMaximizeButtonState();
                    }
                };

                // Set initial state
                UpdateMaximizeButtonState();
            }
        }

        private void UpdateMaximizeButtonState()
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
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentWindow != null)
            {
                _parentWindow.WindowState = WindowState.Minimized;
            }
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentWindow != null)
            {
                _parentWindow.WindowState = _parentWindow.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow?.Close();
        }
    }
}