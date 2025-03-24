using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using OmegaPlayer.Core.Models;
using System.Threading.Tasks;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;

namespace OmegaPlayer.UI.Services
{
    /// <summary>
    /// Service for managing toast notifications in the application
    /// </summary>
    public class ToastNotificationService : IDisposable
    {
        private const int MaxNotifications = 5;

        public ObservableCollection<ToastNotification> Notifications { get; } = new ObservableCollection<ToastNotification>();
        private readonly IMessenger _messenger;
        private readonly IErrorHandlingService _errorHandlingService;

        public ToastNotificationService(IMessenger messenger, IErrorHandlingService errorHandlingService)
        {
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;

            // Subscribe to error messages
            _messenger.Register<ErrorOccurredMessage>(this, (r, m) => ShowNotification(m));
        }

        private void ShowNotification(ErrorOccurredMessage message)
        {
            var notification = new ToastNotification(
                message.Message,
                message.Details,
                message.Severity,
                message.DisplayDuration);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Add new notification
                Notifications.Add(notification);

                // Remove oldest if we have too many
                while (Notifications.Count > MaxNotifications)
                {
                    Notifications.RemoveAt(0);
                }

                // Schedule removal
                ScheduleNotificationRemoval(notification);
            });
        }

        /// <summary>
        /// Shows a notification directly without going through the messaging system
        /// </summary>
        public void ShowToast(string message, string details = null, ErrorSeverity severity = ErrorSeverity.Info, TimeSpan? duration = null)
        {
            var notification = new ToastNotification(message, details, severity, duration);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Notifications.Add(notification);

                while (Notifications.Count > MaxNotifications)
                {
                    Notifications.RemoveAt(0);
                }

                ScheduleNotificationRemoval(notification);
            });
        }

        private async void ScheduleNotificationRemoval(ToastNotification notification)
        {
            try
            {
                // Wait for the specified duration
                await Task.Delay(notification.Duration);

                // Start exit animation
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Trigger exit animation
                    notification.Opacity = 0;
                });

                // Wait for animation to complete before removing
                await Task.Delay(TimeSpan.FromMilliseconds(300));

                // Remove notification
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Notifications.Contains(notification))
                    {
                        Notifications.Remove(notification);
                    }
                });
            }
            catch (Exception ex)
            {
                // Log error but don't show notification to avoid infinite loop
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error removing notification",
                    ex.Message,
                    ex,
                    false);

                // Try to remove anyway
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Notifications.Contains(notification))
                    {
                        Notifications.Remove(notification);
                    }
                });
            }
        }

        public void Dispose()
        {
            // Unregister from messenger
            _messenger.Unregister<ErrorOccurredMessage>(this);
        }
    }
}