using Avalonia.Controls;
using Avalonia.Interactivity;
using OmegaPlayer.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.UI.ViewModels;
using System;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;

namespace OmegaPlayer.UI.Controls
{
    public partial class ToastNotifications : UserControl
    {
        private ToastNotificationsViewModel _viewModel;

        public ToastNotifications()
        {
            InitializeComponent();

            Loaded += ToastNotifications_Loaded;
            Unloaded += ToastNotifications_Unloaded;
        }

        private void ToastNotifications_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var notificationService = App.ServiceProvider.GetRequiredService<ToastNotificationService>();
                var errorHandlingService = App.ServiceProvider.GetRequiredService<IErrorHandlingService>();

                _viewModel = new ToastNotificationsViewModel(notificationService, errorHandlingService);
                DataContext = _viewModel;
            }
            catch (Exception ex)
            {
                // Log error but don't show notification to avoid infinite loop
                var errorService = App.ServiceProvider.GetService<IErrorHandlingService>();
                errorService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to initialize notifications",
                    "Error setting up notification service",
                    ex,
                    false);
            }
        }

        private void ToastNotifications_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                // Explicitly dispose our ViewModel since it implements IDisposable
                (_viewModel as IDisposable)?.Dispose();
                _viewModel = null;
            }

            DataContext = null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (e.Source is Button button &&
                button.CommandParameter is ToastNotification notification &&
                _viewModel != null)
            {
                _viewModel.RemoveNotification(notification);
            }
        }
    }
}