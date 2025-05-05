using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace OmegaPlayer.Infrastructure.Services.Images
{
    /// <summary>
    /// Interface for components that need to respond to memory pressure changes
    /// </summary>
    public interface IMemoryPressureResponder
    {
        /// <summary>
        /// Called when system memory usage is high (typically over 80%)
        /// </summary>
        void OnHighMemoryPressure();

        /// <summary>
        /// Called when system memory usage returns to normal levels
        /// </summary>
        void OnNormalMemoryPressure();
    }

    /// <summary>
    /// Monitors system memory usage and notifies registered components about pressure changes
    /// </summary>
    public class MemoryMonitorService : IDisposable
    {
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly List<WeakReference<IMemoryPressureResponder>> _responders = new List<WeakReference<IMemoryPressureResponder>>();
        private readonly Timer _monitorTimer;
        private bool _isHighPressure = false;
        private bool _isDisposed = false;

        // Windows API for memory status
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;     // Percentage of memory in use
            public ulong ullTotalPhys;    // Total physical memory
            public ulong ullAvailPhys;    // Available physical memory
            public ulong ullTotalPageFile; // Total page file
            public ulong ullAvailPageFile; // Available page file
            public ulong ullTotalVirtual;  // Total virtual memory
            public ulong ullAvailVirtual;  // Available virtual memory
            public ulong ullAvailExtendedVirtual; // Reserved, always 0
        }

        /// <summary>
        /// Threshold percentage at which memory is considered under pressure (0-100)
        /// </summary>
        private const int HighMemoryPressureThreshold = 80;

        /// <summary>
        /// Interval between memory checks in milliseconds
        /// </summary>
        private const int CheckIntervalMs = 5000; // Check every 5 seconds

        public MemoryMonitorService(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;

            // Start monitoring timer
            _monitorTimer = new Timer(CheckMemoryPressure, null, 5000, CheckIntervalMs);

            _errorHandlingService.LogError(
                ErrorSeverity.Info,
                "Memory monitor initialized",
                $"Memory pressure threshold set to {HighMemoryPressureThreshold}%, checking every {CheckIntervalMs / 1000} seconds",
                null,
                false);
        }

        /// <summary>
        /// Registers a component to receive memory pressure notifications
        /// </summary>
        public void RegisterResponder(IMemoryPressureResponder responder)
        {
            if (responder == null) throw new ArgumentNullException(nameof(responder));
            if (_isDisposed) return;

            // Use weak references to avoid memory leaks if responders aren't properly unregistered
            _responders.Add(new WeakReference<IMemoryPressureResponder>(responder));

            _errorHandlingService.LogError(
                ErrorSeverity.Info,
                "Memory pressure responder registered",
                $"Registered {responder.GetType().Name} for memory pressure notifications",
                null,
                false);
        }

        /// <summary>
        /// Timer callback that checks current memory usage
        /// </summary>
        private void CheckMemoryPressure(object state)
        {
            if (_isDisposed) return;

            _errorHandlingService.SafeExecute(() =>
            {
                var memoryInfo = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };

                if (GlobalMemoryStatusEx(ref memoryInfo))
                {
                    bool wasHighPressure = _isHighPressure;

                    // Update current pressure state
                    _isHighPressure = memoryInfo.dwMemoryLoad > HighMemoryPressureThreshold;

                    // Only notify if state changed to avoid constant callbacks
                    if (_isHighPressure != wasHighPressure)
                    {
                        if (_isHighPressure)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.Info,
                                "High memory pressure detected",
                                $"System memory usage at {memoryInfo.dwMemoryLoad}%, above threshold of {HighMemoryPressureThreshold}%",
                                null,
                                false);

                            NotifyHighMemoryPressure();
                        }
                        else
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.Info,
                                "Memory pressure returned to normal",
                                $"System memory usage at {memoryInfo.dwMemoryLoad}%, below threshold of {HighMemoryPressureThreshold}%",
                                null,
                                false);

                            NotifyNormalMemoryPressure();
                        }
                    }
                }
            }, "Checking system memory pressure", ErrorSeverity.NonCritical, false);
        }

        /// <summary>
        /// Notifies all registered responders about high memory pressure
        /// </summary>
        private void NotifyHighMemoryPressure()
        {
            NotifyResponders(true);
        }

        /// <summary>
        /// Notifies all registered responders about normal memory pressure
        /// </summary>
        private void NotifyNormalMemoryPressure()
        {
            NotifyResponders(false);
        }

        /// <summary>
        /// Helper to notify responders and clean up dead references
        /// </summary>
        private void NotifyResponders(bool isHighPressure)
        {
            List<WeakReference<IMemoryPressureResponder>> deadReferences = new List<WeakReference<IMemoryPressureResponder>>();

            foreach (var weakRef in _responders)
            {
                if (weakRef.TryGetTarget(out var responder))
                {
                    try
                    {
                        if (isHighPressure)
                            responder.OnHighMemoryPressure();
                        else
                            responder.OnNormalMemoryPressure();
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Error in memory pressure responder",
                            $"Exception in {responder.GetType().Name} while handling memory pressure change",
                            ex,
                            false);
                    }
                }
                else
                {
                    deadReferences.Add(weakRef);
                }
            }

            // Clean up dead references
            foreach (var deadRef in deadReferences)
            {
                _responders.Remove(deadRef);
            }
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _monitorTimer?.Dispose();
            _responders.Clear();
        }
    }
}