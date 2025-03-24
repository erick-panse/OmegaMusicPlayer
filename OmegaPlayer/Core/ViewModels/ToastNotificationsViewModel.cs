using CommunityToolkit.Mvvm.ComponentModel;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.UI.Services;
using System;
using System.Collections.ObjectModel;

namespace OmegaPlayer.UI.ViewModels
{
    public partial class ToastNotificationsViewModel : ViewModelBase, IDisposable
    {
        private readonly ToastNotificationService _notificationService;
        private readonly IErrorHandlingService _errorHandlingService;

        [ObservableProperty]
        private ObservableCollection<ToastNotification> _notifications = new ObservableCollection<ToastNotification>();

        public ToastNotificationsViewModel(ToastNotificationService notificationService, IErrorHandlingService errorHandlingService)
        {
            _notificationService = notificationService;
            _errorHandlingService = errorHandlingService;

            // Subscribe to the notifications collection changes
            _notificationService.Notifications.CollectionChanged += Notifications_CollectionChanged;

            // Initialize with any existing notifications
            SyncNotifications();
        }

        private void Notifications_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Make sure we update the notifications on the UI thread
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    SyncNotifications();
                }
                catch (Exception ex)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Failed to update notifications",
                        "Error syncing notification changes",
                        ex,
                        false); // Don't show notification for errors in notification system
                }
            });
        }

        private void SyncNotifications()
        {
            // Clear and re-add all notifications
            Notifications.Clear();
            foreach (var notification in _notificationService.Notifications)
            {
                Notifications.Add(notification);
            }
        }

        /// <summary>
        /// Removes a notification from the collection
        /// </summary>
        public void RemoveNotification(ToastNotification notification)
        {
            if (_notificationService.Notifications.Contains(notification))
            {
                _notificationService.Notifications.Remove(notification);
            }
        }

        public void Dispose()
        {
            // Unsubscribe from events
            if (_notificationService != null)
            {
                _notificationService.Notifications.CollectionChanged -= Notifications_CollectionChanged;
            }
        }
    }
}