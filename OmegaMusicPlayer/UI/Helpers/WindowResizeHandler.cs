using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.UI.Controls.TrackDisplay;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.UI.Helpers
{
    /// <summary>
    /// Handles window resize operations using SizeChanged events to prevent scroll position drift
    /// Works exactly like maximize/restore behavior by treating resize as single atomic operation
    /// </summary>
    public class WindowResizeHandler
    {
        private readonly Window _window;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly List<TrackDisplayControl> _registeredControls;
        private readonly DispatcherTimer _resizeDebounceTimer;

        private bool _isResizing = false;
        private Size _previousSize;
        private WindowState _previousWindowState;
        private bool _isWindowStateOperation = false;
        private readonly List<dynamic> _registeredViews; // Use dynamic to support different view types

        private const int ResizeDebounceDelayMs = 50; // Wait for resize to stabilize
        private const double MinimumSizeChangeThreshold = 5.0; // Minimum pixel change to trigger resize handling

        public WindowResizeHandler(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _errorHandlingService = App.ServiceProvider?.GetService<IErrorHandlingService>();
            _registeredControls = new List<TrackDisplayControl>();
            _registeredViews = new List<dynamic>();

            // Initialize size tracking
            _previousSize = new Size(_window.Width, _window.Height);
            _previousWindowState = _window.WindowState;

            // Setup debounce timer
            _resizeDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ResizeDebounceDelayMs)
            };
            _resizeDebounceTimer.Tick += OnResizeDebounceTimerTick;

            AttachEventHandlers();
        }

        private void AttachEventHandlers()
        {
            try
            {
                _window.SizeChanged += OnWindowSizeChanged;
                _window.PropertyChanged += OnWindowPropertyChanged;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error attaching window resize handler event handlers",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private void OnWindowPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            try
            {
                if (e.Property == Window.WindowStateProperty)
                {
                    var newState = (WindowState)e.NewValue;

                    // Detect window state operations (maximize/restore/minimize)
                    if (newState != _previousWindowState)
                    {
                        _isWindowStateOperation = true;

                        // Cancel any active resize debouncing since this is a state change
                        if (_isResizing)
                        {
                            _resizeDebounceTimer.Stop();
                            OnResizeCompleted();
                        }

                        // Reset the flag after a brief delay to allow state change to complete
                        Task.Delay(200).ContinueWith(_ =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                _isWindowStateOperation = false;
                                _previousWindowState = newState;
                                _previousSize = new Size(_window.Width, _window.Height);
                            });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling window property change",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                // Skip handling during window state operations (maximize/restore) 
                // These already work correctly and don't need our intervention
                if (_isWindowStateOperation)
                {
                    _previousSize = e.NewSize;
                    return;
                }

                // Calculate size change magnitude
                var widthChange = Math.Abs(e.NewSize.Width - _previousSize.Width);
                var heightChange = Math.Abs(e.NewSize.Height - _previousSize.Height);
                var totalChange = widthChange + heightChange;

                // Only handle significant size changes to avoid noise
                if (totalChange < MinimumSizeChangeThreshold)
                {
                    return;
                }

                // Start or continue resize operation
                if (!_isResizing)
                {
                    OnResizeStarted();
                }

                // Reset the debounce timer - resize is still ongoing
                _resizeDebounceTimer.Stop();
                _resizeDebounceTimer.Start();

                _previousSize = e.NewSize;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling window size change",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private void OnResizeDebounceTimerTick(object sender, EventArgs e)
        {
            try
            {
                _resizeDebounceTimer.Stop();
                OnResizeCompleted();
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error in resize debounce timer",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private void OnResizeStarted()
        {
            try
            {
                if (_isResizing) return;

                _isResizing = true;

                // Notify TrackDisplayControls
                foreach (var control in _registeredControls)
                {
                    try
                    {
                        control.OnResizeStarted();
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error notifying control of resize start", ex.Message, ex, false);
                    }
                }

                // Notify Views
                foreach (var view in _registeredViews)
                {
                    try
                    {
                        view.OnResizeStarted();
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error notifying view of resize start", ex.Message, ex, false);
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error handling resize start", ex.Message, ex, false);
            }
        }

        private void OnResizeCompleted()
        {
            try
            {
                if (!_isResizing) return;

                _isResizing = false;

                // Notify TrackDisplayControls
                foreach (var control in _registeredControls)
                {
                    try
                    {
                        control.OnResizeCompleted();
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error notifying control of resize completion", ex.Message, ex, false);
                    }
                }

                // Notify Views
                foreach (var view in _registeredViews)
                {
                    try
                    {
                        view.OnResizeCompleted();
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error notifying view of resize completion", ex.Message, ex, false);
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error handling resize completion", ex.Message, ex, false);
            }
        }

        // Registration methods for different view types
        public void RegisterTrackDisplayControl(dynamic view) => RegisterView(view);
        public void RegisterGenreView(dynamic view) => RegisterView(view);
        public void RegisterPlaylistView(dynamic view) => RegisterView(view);
        public void RegisterAlbumView(dynamic view) => RegisterView(view);
        public void RegisterArtistView(dynamic view) => RegisterView(view);
        public void RegisterFolderView(dynamic view) => RegisterView(view);
        public void RegisterSearchView(dynamic view) => RegisterView(view);

        public void UnregisterTrackDisplayControl(dynamic view) => UnregisterView(view);
        public void UnregisterGenreView(dynamic view) => UnregisterView(view);
        public void UnregisterPlaylistView(dynamic view) => UnregisterView(view);
        public void UnregisterAlbumView(dynamic view) => UnregisterView(view);
        public void UnregisterArtistView(dynamic view) => UnregisterView(view);
        public void UnregisterFolderView(dynamic view) => UnregisterView(view);
        public void UnregisterSearchView(dynamic view) => UnregisterView(view);

        private void RegisterView(dynamic view)
        {
            if (view != null && !_registeredViews.Contains(view))
            {
                _registeredViews.Add(view);
            }
        }

        private void UnregisterView(dynamic view)
        {
            if (view != null)
            {
                _registeredViews.Remove(view);
            }
        }

        /// <summary>
        /// Cleanup event handlers and resources
        /// </summary>
        public void Dispose()
        {
            try
            {
                _resizeDebounceTimer?.Stop();

                _window.SizeChanged -= OnWindowSizeChanged;
                _window.PropertyChanged -= OnWindowPropertyChanged;

                _registeredControls.Clear();
                _registeredViews.Clear(); // Add this line
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error disposing window resize handler", ex.Message, ex, false);
            }
        }

    }
}