using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;
using NAudio.CoreAudioApi.Interfaces;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Threading;

namespace OmegaPlayer.Features.Playback.Services
{
    public class AudioPauseMessage
    {
        public bool ShouldPause { get; }
        public bool ShouldResume { get; }
        public AudioPauseMessage(bool shouldPause, bool shouldResume)
        {
            ShouldPause = shouldPause;
            ShouldResume = shouldResume;
        }
    }

    public class AudioMonitorService : IDisposable
    {
        private readonly MMDeviceEnumerator _deviceEnumerator;
        private readonly HashSet<string> _excludedProcesses;
        private readonly string _ownProcessName;
        private readonly IMessenger _messenger;
        private readonly IErrorHandlingService _errorHandlingService;
        private bool _isOtherAudioPlaying;
        private bool _isDynamicPauseEnabled;
        private Timer _monitorTimer;
        private bool _wasPausedByMonitor;

        public AudioMonitorService(IMessenger messenger, IErrorHandlingService errorHandlingService)
        {
            _deviceEnumerator = new MMDeviceEnumerator();
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;
            _ownProcessName = Process.GetCurrentProcess().ProcessName;
            _excludedProcesses = new HashSet<string> { _ownProcessName };

            // Initialize timer for periodic monitoring
            _monitorTimer = new Timer(
                CheckAudioActivity,
                null,
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(500)); // Check every 500ms
        }

        public void EnableDynamicPause(bool enable)
        {
            _isDynamicPauseEnabled = enable;
            if (enable)
            {
                // Force an immediate check when enabling
                CheckAudioActivity(null);
            }
        }

        private void CheckAudioActivity(object state)
        {
            _errorHandlingService.SafeExecute(() =>
            {
                var device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var hasOtherAudioPlaying = false;

                var sessionEnumerator = device.AudioSessionManager.Sessions;
                for (int i = 0; i < sessionEnumerator.Count; i++)
                {
                    using (var session = sessionEnumerator[i])
                    {
                        try
                        {
                            var processId = session.GetProcessID;
                            var process = Process.GetProcessById((int)processId);
                            var processName = process.ProcessName;

                            if (!string.IsNullOrEmpty(processName) &&
                                !_excludedProcesses.Contains(processName))
                            {
                                // More thorough check for active audio
                                var isActive = session.State == AudioSessionState.AudioSessionStateActive;
                                var hasVolume = session.SimpleAudioVolume.Volume > 0;
                                var isMuted = session.SimpleAudioVolume.Mute;

                                if (isActive && hasVolume && !isMuted)
                                {
                                    hasOtherAudioPlaying = true;
                                    break;
                                }
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Process might have ended
                            continue;
                        }
                        catch (InvalidOperationException)
                        {
                            // Session might have ended
                            continue;
                        }
                    }
                }

                if (_isOtherAudioPlaying != hasOtherAudioPlaying)
                {
                    _isOtherAudioPlaying = hasOtherAudioPlaying;
                    if (_isDynamicPauseEnabled)
                    {
                        if (hasOtherAudioPlaying)
                        {
                            _wasPausedByMonitor = true;
                            _messenger.Send(new AudioPauseMessage(true, false)); //Sent pause message due to other audio
                        }
                        else if (_wasPausedByMonitor)
                        {
                            // Add a small delay before resuming to avoid rapid pause/resume cycles
                            Task.Delay(200).ContinueWith(_ =>
                            {
                                if (!_isOtherAudioPlaying && _isDynamicPauseEnabled)
                                {
                                    _wasPausedByMonitor = false;
                                    _messenger.Send(new AudioPauseMessage(false, true)); //Sent resume message
                                }
                            });
                        }
                    }
                }
            },
            "Checking audio activity",
            ErrorSeverity.NonCritical,
            false);
        }


        public void AddExcludedProcess(string processName)
        {
            _excludedProcesses.Add(processName);
        }

        public void Dispose()
        {
            try
            {
                _monitorTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _monitorTimer?.Dispose();
                _deviceEnumerator?.Dispose();
            }
            catch (Exception ex)
            {
                // Just log the error, don't throw from Dispose
                if (_errorHandlingService != null)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Error disposing AudioMonitorService",
                        ex.Message,
                        ex,
                        false);
                }
            }
            finally
            {
                _monitorTimer = null;
            }
        }

        /// <summary>
        /// Resets the audio monitoring system to a fresh state.
        /// Used primarily during error recovery.
        /// </summary>
        public void Reset()
        {
            try
            {
                // Store current dynamic pause state
                bool currentState = _isDynamicPauseEnabled;

                // Clear the excluded processes except for the own process
                _excludedProcesses.Clear();
                _excludedProcesses.Add(_ownProcessName);

                // Reset internal state
                _isOtherAudioPlaying = false;
                _wasPausedByMonitor = false;

                // Recreate timer to ensure clean state
                _monitorTimer?.Dispose();
                _monitorTimer = new Timer(
                    CheckAudioActivity,
                    null,
                    TimeSpan.FromMilliseconds(250), // Initial delay
                    TimeSpan.FromMilliseconds(500)); // Regular interval

                // Re-enable with previous state
                EnableDynamicPause(currentState);
            }
            catch (Exception ex)
            {
                // Just log the error, don't throw from Reset
                if (_errorHandlingService != null)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Error resetting audio monitor",
                        ex.Message,
                        ex,
                        false);
                }
            }
        }
    }
}