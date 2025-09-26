using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using NAudio.Wave;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Features.Playback.ViewModels;
using OmegaMusicPlayer.Features.Shell.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;

namespace OmegaMusicPlayer.Core.Services
{
    public class MediaKeyService : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly Timer _globalKeyTimer;
        private bool _isDisposed = false;
        private bool _isProcessingCommand = false;

        // Track key states for global detection
        private bool _wasPlayPausePressed = false;
        private bool _wasNextPressed = false;
        private bool _wasPrevPressed = false;
        private bool _wasStopPressed = false;

        // Virtual key codes
        private const int VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const int VK_MEDIA_STOP = 0xB2;
        private const int VK_MEDIA_PREV_TRACK = 0xB1;
        private const int VK_MEDIA_NEXT_TRACK = 0xB0;

        public MediaKeyService(
            IServiceProvider serviceProvider,
            IErrorHandlingService errorHandlingService)
        {
            _serviceProvider = serviceProvider;
            _errorHandlingService = errorHandlingService;

            // Global key detection timer for when app is not focused
            _globalKeyTimer = new Timer(100);
            _globalKeyTimer.Elapsed += OnGlobalKeyCheck;
        }

        public void StartListening()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _globalKeyTimer.Start();
                SetupFocusedKeyHandling();
            }
        }

        private void SetupFocusedKeyHandling()
        {
            // Hook into main window key events for immediate response when focused
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.KeyDown += OnMainWindowKeyDown;
                }
            }
        }

        private async void OnMainWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (_isProcessingCommand) return;

            try
            {
                // Handle immediate key responses when app is focused
                var handled = false;

                // Arrow keys for volume when focused
                switch (e.Key)
                {
                    case Key.Left:
                        await HandleMediaCommand(MediaAction.VolumeDown);
                        handled = true;
                        break;
                    case Key.Right:
                        await HandleMediaCommand(MediaAction.VolumeUp);
                        handled = true;
                        break;
                    case Key.Space:
                        await HandleMediaCommand(MediaAction.PlayPause);
                        handled = true;
                        break;
                    case Key.Escape:
                        await HandleMediaCommand(MediaAction.CloseImageMode);
                        handled = true;
                        break;
                    // Media keys work even when focused
                    case Key.MediaPlayPause:
                        await HandleMediaCommand(MediaAction.PlayPause);
                        handled = true;
                        break;
                    case Key.MediaNextTrack:
                        await HandleMediaCommand(MediaAction.Next);
                        handled = true;
                        break;
                    case Key.MediaPreviousTrack:
                        await HandleMediaCommand(MediaAction.Previous);
                        handled = true;
                        break;
                    case Key.MediaStop:
                        await HandleMediaCommand(MediaAction.Stop);
                        handled = true;
                        break;
                }

                if (handled)
                {
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling focused key event",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private async void OnGlobalKeyCheck(object sender, ElapsedEventArgs e)
        {
            if (_isProcessingCommand) return;

            try
            {
                // Only check global keys when app is not focused
                if (!IsAppFocused())
                {
                    bool playPausePressed = GetAsyncKeyState(VK_MEDIA_PLAY_PAUSE) < 0;
                    bool nextPressed = GetAsyncKeyState(VK_MEDIA_NEXT_TRACK) < 0;
                    bool prevPressed = GetAsyncKeyState(VK_MEDIA_PREV_TRACK) < 0;
                    bool stopPressed = GetAsyncKeyState(VK_MEDIA_STOP) < 0;

                    // Trigger on key press (not release)
                    if (playPausePressed && !_wasPlayPausePressed)
                    {
                        await HandleMediaCommand(MediaAction.PlayPause);
                    }
                    else if (nextPressed && !_wasNextPressed)
                    {
                        await HandleMediaCommand(MediaAction.Next);
                    }
                    else if (prevPressed && !_wasPrevPressed)
                    {
                        await HandleMediaCommand(MediaAction.Previous);
                    }
                    else if (stopPressed && !_wasStopPressed)
                    {
                        await HandleMediaCommand(MediaAction.Stop);
                    }

                    // Update states
                    _wasPlayPausePressed = playPausePressed;
                    _wasNextPressed = nextPressed;
                    _wasPrevPressed = prevPressed;
                    _wasStopPressed = stopPressed;
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error checking global media keys",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private bool IsAppFocused()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                return mainWindow?.IsActive == true;
            }
            return false;
        }

        private async Task HandleMediaCommand(MediaAction action)
        {
            if (_isProcessingCommand) return;

            _isProcessingCommand = true;

            try
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var trackControlVM = _serviceProvider.GetService<TrackControlViewModel>();
                    if (trackControlVM == null) return;

                    switch (action)
                    {
                        case MediaAction.PlayPause:
                            if (trackControlVM.CurrentlyPlayingTrack != null || trackControlVM.IsPlaying != PlaybackState.Stopped)
                            {
                                await trackControlVM.PlayOrPauseCommand.ExecuteAsync(null);
                            }
                            break;

                        case MediaAction.Next:
                            if (trackControlVM.CurrentlyPlayingTrack != null)
                            {
                                await trackControlVM.PlayNextTrackCommand.ExecuteAsync(null);
                            }
                            break;

                        case MediaAction.Previous:
                            if (trackControlVM.CurrentlyPlayingTrack != null)
                            {
                                await trackControlVM.PlayPreviousTrackCommand.ExecuteAsync(null);
                            }
                            break;

                        case MediaAction.Stop:
                            trackControlVM.StopPlayback();
                            break;

                        case MediaAction.VolumeUp:
                            trackControlVM.TrackVolume = Math.Min(1.0f, trackControlVM.TrackVolume + 0.1f);
                            trackControlVM.SetVolume();
                            break;

                        case MediaAction.VolumeDown:
                            trackControlVM.TrackVolume = Math.Max(0.0f, trackControlVM.TrackVolume - 0.1f);
                            trackControlVM.SetVolume();
                            break;

                        case MediaAction.CloseImageMode:
                            var mainVM = _serviceProvider.GetService<MainViewModel>();
                            if (mainVM == null) return;
                            if (mainVM.IsImageModeActive)
                            {
                                var imgMode = _serviceProvider.GetService<ImageModeViewModel>();
                                if (imgMode == null) return;

                                imgMode.CloseImageMode();
                            }
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    $"Error executing media command {action}",
                    ex.Message,
                    ex,
                    false);
            }
            finally
            {
                _isProcessingCommand = false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _globalKeyTimer?.Stop();
            _globalKeyTimer?.Dispose();

            // Unhook from main window
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.KeyDown -= OnMainWindowKeyDown;
                }
            }

            _isDisposed = true;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }

    public enum MediaAction
    {
        None,
        PlayPause,
        Next,
        Previous,
        Stop,
        VolumeUp,
        VolumeDown,
        CloseImageMode
    }
}