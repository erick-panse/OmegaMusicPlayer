using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using OmegaPlayer.Core.Enums;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace OmegaPlayer.Core.Models
{
    /// <summary>
    /// Model for toast notifications
    /// </summary>
    public class ToastNotification : INotifyPropertyChanged
    {
        public string Message { get; set; }
        public string Details { get; set; }
        public DateTime CreationTime { get; set; }
        public TimeSpan Duration { get; set; }
        public ErrorSeverity Severity { get; set; }

        private double _opacity = 1.0;
        public double Opacity
        {
            get => _opacity;
            set
            {
                if (_opacity != value)
                {
                    _opacity = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Opacity)));
                }
            }
        }

        private double _translateX = 50;
        public double TranslateX
        {
            get => _translateX;
            set
            {
                if (_translateX != value)
                {
                    _translateX = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TranslateX)));
                }
            }
        }

        public bool IsVisible { get; set; } = true;
        public IBrush Background { get; set; }
        public IBrush Foreground { get; set; }

        // The actual StreamGeometry for the icon
        public StreamGeometry IconGeometry { get; set; }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        public ToastNotification(string message, string details = null, ErrorSeverity severity = ErrorSeverity.Info, TimeSpan? duration = null)
        {
            Message = message;
            Details = details;
            CreationTime = DateTime.Now;
            Severity = severity;
            Duration = duration ?? GetDefaultDuration(severity);

            // Initial animation state
            Opacity = 0;
            TranslateX = 50;

            // Set appearance based on severity
            SetAppearanceBasedOnSeverity();

            // Schedule animation after render
            Task.Delay(16).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    // Trigger entry animation
                    Opacity = 1.0;
                    TranslateX = 0;
                });
            });
        }

        private TimeSpan GetDefaultDuration(ErrorSeverity severity)
        {
            return severity switch
            {
                ErrorSeverity.Critical => TimeSpan.FromSeconds(12),
                ErrorSeverity.Playback => TimeSpan.FromSeconds(8),
                ErrorSeverity.NonCritical => TimeSpan.FromSeconds(5),
                ErrorSeverity.Info => TimeSpan.FromSeconds(3),
                _ => TimeSpan.FromSeconds(5)
            };
        }

        private void SetAppearanceBasedOnSeverity()
        {
            // Check if we're on the UI thread
            if (Dispatcher.UIThread.CheckAccess())
            {
                SetAppearanceOnUIThread();
            }
            else
            {
                // Defer to UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        SetAppearanceOnUIThread();
                    }
                    catch
                    {
                        // Fallback to null brushes to use default theme
                        Background = null;
                        Foreground = null;
                        IconGeometry = null;
                    }
                });
            }
        }

        private void SetAppearanceOnUIThread()
        {
            switch (Severity)
            {
                case ErrorSeverity.Critical:
                    Background = new SolidColorBrush(Color.Parse("#FF5252"));
                    Foreground = Brushes.White;
                    IconGeometry = GetIconGeometry("ErrorIcon");
                    break;
                case ErrorSeverity.Playback:
                    Background = new SolidColorBrush(Color.Parse("#FFC107"));
                    Foreground = Brushes.Black;
                    IconGeometry = GetIconGeometry("WarningIcon");
                    break;
                case ErrorSeverity.NonCritical:
                    Background = new SolidColorBrush(Color.Parse("#757575"));
                    Foreground = Brushes.White;
                    IconGeometry = GetIconGeometry("InfoIcon");
                    break;
                case ErrorSeverity.Info:
                    Background = new SolidColorBrush(Color.Parse("#2196F3"));
                    Foreground = Brushes.White;
                    IconGeometry = GetIconGeometry("InfoIcon");
                    break;
            }
        }

        private StreamGeometry GetIconGeometry(string iconName)
        {
            try
            {
                if (Application.Current?.TryFindResource(iconName, out var resource) == true)
                {
                    if (resource is StreamGeometry geometry)
                    {
                        return geometry;
                    }
                }
            }
            catch
            {
                // Return null if resource access fails
            }

            return null;
        }
    }

}
