using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;
using NAudio.CoreAudioApi.Interfaces;

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
        private bool _isOtherAudioPlaying;
        private bool _isDynamicPauseEnabled;
        private System.Threading.Timer _monitorTimer;
        private bool _wasPausedByMonitor;

        public AudioMonitorService(IMessenger messenger)
        {
            _deviceEnumerator = new MMDeviceEnumerator();
            _messenger = messenger;
            _ownProcessName = Process.GetCurrentProcess().ProcessName;
            _excludedProcesses = new HashSet<string> { _ownProcessName };

            // Initialize timer for periodic monitoring
            _monitorTimer = new System.Threading.Timer(
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
            try
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
                                    Console.WriteLine($"Active audio detected from: {processName}");
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking audio activity: {ex.Message}");
            }
        }

        public void AddExcludedProcess(string processName)
        {
            _excludedProcesses.Add(processName);
        }

        public void Dispose()
        {
            _monitorTimer?.Dispose();
            _deviceEnumerator?.Dispose();
        }
    }
}