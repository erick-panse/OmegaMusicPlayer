using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using OmegaPlayer.Core.Models;
using System.Threading.Tasks;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace OmegaPlayer.UI.Services
{
    /// <summary>
    /// Service for managing toast notifications in the application with robust error handling.
    /// </summary>
    public class ToastNotificationService : IDisposable
    {
        private const int MAX_NOTIFICATIONS = 5;
        private const int EMERGENCY_MESSAGE_DURATION_MS = 10000; // 10 seconds for emergency fallback
        private const int MAX_PENDING_NOTIFICATIONS = 20;

        public ObservableCollection<ToastNotification> Notifications { get; } = new ObservableCollection<ToastNotification>();

        private readonly IMessenger _messenger;
        private readonly IErrorHandlingService _errorHandlingService;

        // Use a queue to prevent message loss during high-volume error situations
        private readonly Queue<ToastNotification> _pendingNotifications = new Queue<ToastNotification>();

        // Track whether the dispatcher is available for emergency fallback
        private bool _isDispatcherAvailable = true;

        // Cancellation token source for terminating background processing
        private CancellationTokenSource _processingCts;

        // Track if disposal has been initiated
        private bool _disposing = false;

        // Track if message processing is running
        private bool _isProcessingMessages = false;

        // Lock for thread safety
        private readonly object _notificationLock = new object();

        public ToastNotificationService(IMessenger messenger, IErrorHandlingService errorHandlingService)
        {
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;

            // Subscribe to error messages
            _messenger.Register<ErrorOccurredMessage>(this, (r, m) => ShowNotification(m));

            // Start background processing of notifications
            _processingCts = new CancellationTokenSource();
            Task.Run(() => ProcessPendingNotificationsAsync(_processingCts.Token));
        }

        /// <summary>
        /// Shows a notification from an error message with queuing and fallback.
        /// </summary>
        private void ShowNotification(ErrorOccurredMessage message)
        {
            try
            {
                var notification = new ToastNotification(
                    message.Message,
                    message.Details,
                    message.Severity,
                    message.DisplayDuration);

                // Add to queue instead of directly to UI
                EnqueueNotification(notification);
            }
            catch (Exception ex)
            {
                // Use direct error logging as a fallback
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to create notification for error message",
                    "An internal error occurred while trying to display an error notification.",
                    ex,
                    false); // Don't recursively show notification

                // Try emergency fallback message for critical errors
                if (message.Severity == ErrorSeverity.Critical)
                {
                    TryEmergencyNotification(message.Message, message.Details);
                }
            }
        }

        /// <summary>
        /// Shows a notification directly without going through the messaging system.
        /// </summary>
        public void ShowToast(string message, string details = null, ErrorSeverity severity = ErrorSeverity.Info, TimeSpan? duration = null)
        {
            try
            {
                var notification = new ToastNotification(message, details, severity, duration);

                // Add to queue instead of directly to UI
                EnqueueNotification(notification);
            }
            catch (Exception ex)
            {
                // Use direct error logging as a fallback
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to create direct notification",
                    "An internal error occurred while trying to display a notification.",
                    ex,
                    false); // Don't recursively show notification

                // Try emergency fallback for critical errors
                if (severity == ErrorSeverity.Critical)
                {
                    TryEmergencyNotification(message, details);
                }
            }
        }

        /// <summary>
        /// Adds a notification to the processing queue with overflow protection.
        /// </summary>
        private void EnqueueNotification(ToastNotification notification)
        {
            lock (_notificationLock)
            {
                // Don't add more if we're disposing
                if (_disposing) return;

                // Add to queue
                _pendingNotifications.Enqueue(notification);

                // Trim queue if it gets too large (dropping oldest messages)
                while (_pendingNotifications.Count > MAX_PENDING_NOTIFICATIONS)
                {
                    _pendingNotifications.Dequeue();
                }
            }
        }

        /// <summary>
        /// Processes pending notifications in the background.
        /// </summary>
        private async Task ProcessPendingNotificationsAsync(CancellationToken cancellationToken)
        {
            _isProcessingMessages = true;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    ToastNotification notification = null;

                    // Get next notification from queue if available
                    lock (_notificationLock)
                    {
                        if (_pendingNotifications.Count > 0)
                        {
                            notification = _pendingNotifications.Dequeue();
                        }
                    }

                    // Process notification if we got one
                    if (notification != null)
                    {
                        await AddNotificationToUIAsync(notification);
                    }

                    // Short delay to prevent CPU thrashing
                    await Task.Delay(50, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested, exit loop
                    break;
                }
                catch (Exception ex)
                {
                    // Log but continue processing
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Error processing notification queue",
                        "The notification processing task encountered an error but will continue.",
                        ex,
                        false); // Don't show notification to avoid recursive error

                    // Add a delay to prevent rapid failure loops
                    try
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _isProcessingMessages = false;
        }

        /// <summary>
        /// Adds a notification to the UI thread safely.
        /// </summary>
        private async Task AddNotificationToUIAsync(ToastNotification notification)
        {
            try
            {
                // Make sure we add to UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        // Add new notification
                        Notifications.Add(notification);

                        // Remove oldest if we have too many
                        while (Notifications.Count > MAX_NOTIFICATIONS)
                        {
                            Notifications.RemoveAt(0);
                        }

                        // Schedule removal
                        ScheduleNotificationRemoval(notification);

                        // Mark that dispatcher is working
                        _isDispatcherAvailable = true;
                    }
                    catch (Exception ex)
                    {
                        // Mark that dispatcher is not reliable
                        _isDispatcherAvailable = false;

                        // Log error but don't show notification to avoid infinite loop
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to add notification to UI",
                            ex.Message,
                            ex,
                            false);
                    }
                });
            }
            catch (Exception ex)
            {
                // Mark that dispatcher is not reliable
                _isDispatcherAvailable = false;

                // Log error but don't show notification to avoid infinite loop
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to invoke UI thread for notification",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Schedules a notification to be removed after its duration with fallback.
        /// </summary>
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
                    notification.TranslateX = 50;
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
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (Notifications.Contains(notification))
                        {
                            Notifications.Remove(notification);
                        }
                    });
                }
                catch
                {
                    // Nothing else we can do - Just avoid crash
                }
            }
        }

        /// <summary>
        /// Attempts to display a last-resort emergency notification when other methods fail.
        /// </summary>
        private void TryEmergencyNotification(string message, string details)
        {
            try
            {
                // Only attempt if the dispatcher is available
                if (!_isDispatcherAvailable)
                    return;

                // Create a simple emergency notification
                var emergencyNotification = new ToastNotification(
                    $"EMERGENCY: {message}",
                    details,
                    ErrorSeverity.Critical,
                    TimeSpan.FromMilliseconds(EMERGENCY_MESSAGE_DURATION_MS));

                // Try to display it
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        // Remove all existing notifications
                        Notifications.Clear();

                        // Add emergency notification
                        Notifications.Add(emergencyNotification);

                        // Schedule removal
                        Task.Delay(EMERGENCY_MESSAGE_DURATION_MS + 500).ContinueWith(_ =>
                        {
                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                if (Notifications.Contains(emergencyNotification))
                                {
                                    Notifications.Remove(emergencyNotification);
                                }
                            });
                        });
                    }
                    catch
                    {
                        // Nothing else we can do - Just avoid crash
                    }
                });
            }
            catch
            {
                // Nothing else we can do - Just avoid crash
            }
        }

        /// <summary>
        /// Clears all notifications.
        /// </summary>
        public void ClearAllNotifications()
        {
            try
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Notifications.Clear();
                });

                lock (_notificationLock)
                {
                    _pendingNotifications.Clear();
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to clear notifications",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Removes a specific notification.
        /// </summary>
        public void RemoveNotification(ToastNotification notification)
        {
            try
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Notifications.Contains(notification))
                    {
                        Notifications.Remove(notification);
                    }
                });
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to remove notification",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Cleanly disposes of resources.
        /// </summary>
        public void Dispose()
        {
            // Mark as disposing
            _disposing = true;

            try
            {
                // Unregister from messenger
                _messenger.Unregister<ErrorOccurredMessage>(this);

                // Stop background processing
                _processingCts?.Cancel();

                // Wait for processing to complete if it's running
                if (_isProcessingMessages)
                {
                    for (int i = 0; i < 10; i++) // Wait up to 1 second
                    {
                        if (!_isProcessingMessages)
                            break;

                        Thread.Sleep(100);
                    }
                }

                // Clear collections
                lock (_notificationLock)
                {
                    _pendingNotifications.Clear();
                }

                // Try to clear UI notifications
                try
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Notifications.Clear();
                    });
                }
                catch
                {
                    // Ignore errors during shutdown
                }

                // Dispose of cancellation token source
                _processingCts?.Dispose();
                _processingCts = null;
            }
            catch (Exception ex)
            {
                // Log but don't show notification
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error during toast notification service disposal",
                    ex.Message,
                    ex,
                    false);
            }
        }
    }
}