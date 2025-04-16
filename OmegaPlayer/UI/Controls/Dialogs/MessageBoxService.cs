using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;
using OmegaPlayer.UI.Controls;

namespace OmegaPlayer.UI.Services
{
    /// <summary>
    /// Service to show consistent message boxes across the application
    /// </summary>
    public static class MessageBoxService
    {
        /// <summary>
        /// Shows a confirmation dialog with Yes/No buttons
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The dialog message</param>
        /// <returns>True if Yes was selected, False otherwise</returns>
        public static async Task<bool> ShowConfirmationDialog(string title, string message)
        {
            var mainWindow = GetMainWindow();
            if (mainWindow == null) return false;

            var result = await CustomMessageBox.Show(
                mainWindow,
                title,
                message,
                CustomMessageBox.MessageBoxButtons.YesNo);

            return result == CustomMessageBox.MessageBoxResult.Yes;
        }

        /// <summary>
        /// Shows an information message with an OK button
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The dialog message</param>
        public static async Task ShowMessageDialog(string title, string message)
        {
            var mainWindow = GetMainWindow();
            if (mainWindow == null) return;

            await CustomMessageBox.Show(
                mainWindow,
                title,
                message,
                CustomMessageBox.MessageBoxButtons.OK);
        }

        /// <summary>
        /// Gets the current main window from the application
        /// </summary>
        private static Window GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Ensure the window is available and visible
                if (desktop.MainWindow != null && desktop.MainWindow.IsVisible)
                {
                    return desktop.MainWindow;
                }

                // If main window isn't visible, try to find any visible window
                foreach (var window in desktop.Windows)
                {
                    if (window.IsVisible)
                    {
                        return window;
                    }
                }
            }
            return null;
        }
    }
}