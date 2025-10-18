using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Features.Playback.Services
{
    /// <summary>
    /// Coordinates queue save operations with cancellation and debouncing to optimize performance.
    /// Implements "latest action wins" pattern to prevent race conditions and excessive database operations.
    /// </summary>
    public class QueueSaveCoordinator : IDisposable
    {
        private readonly IErrorHandlingService _errorHandlingService;

        // Cancellation token sources for different save operations
        private CancellationTokenSource _metadataSaveCts;
        private CancellationTokenSource _fullSaveCts;

        // Debounce timer for metadata saves
        private Timer _metadataDebounceTimer;
        private const int METADATA_DEBOUNCE_MS = 500;

        // Semaphore to ensure thread-safe save operations
        private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

        // Track if we're in the process of shutting down
        private bool _isDisposing = false;
        private readonly object _disposeLock = new object();

        public QueueSaveCoordinator(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
            _metadataSaveCts = new CancellationTokenSource();
            _fullSaveCts = new CancellationTokenSource();
        }

        /// <summary>
        /// Saves only queue metadata (current track index, shuffle state, repeat mode) with debouncing.
        /// Cancels any previously pending metadata save - "latest action wins".
        /// </summary>
        /// <param name="saveAction">The async action that performs the actual save</param>
        public void ScheduleMetadataSave(Func<CancellationToken, Task> saveAction)
        {
            lock (_disposeLock)
            {
                if (_isDisposing) return;

                // Cancel any pending metadata save
                CancelMetadataSave();

                // Create new cancellation token for this operation
                _metadataSaveCts = new CancellationTokenSource();
                var currentToken = _metadataSaveCts.Token;

                // Dispose existing timer and create new one
                _metadataDebounceTimer?.Dispose();

                _metadataDebounceTimer = new Timer(async _ =>
                {
                    // Check if this save was cancelled before executing
                    if (currentToken.IsCancellationRequested)
                    {
                        _errorHandlingService.LogInfo(
                            "Metadata save cancelled (newer operation took priority)",
                            "Debounced save was cancelled before execution");
                        return;
                    }

                    await ExecuteMetadataSave(saveAction, currentToken);

                }, null, METADATA_DEBOUNCE_MS, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Executes the metadata save with proper locking and error handling.
        /// </summary>
        private async Task ExecuteMetadataSave(Func<CancellationToken, Task> saveAction, CancellationToken ct)
        {
            await _saveLock.WaitAsync(ct);
            try
            {
                // Double-check cancellation after acquiring lock
                if (ct.IsCancellationRequested)
                {
                    _errorHandlingService.LogInfo(
                        "Metadata save cancelled after lock acquisition",
                        "Operation was cancelled while waiting for lock");
                    return;
                }

                await saveAction(ct);

                _errorHandlingService.LogInfo(
                    "Metadata save completed successfully",
                    "Debounced save executed");
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled - don't log as error
                _errorHandlingService.LogInfo(
                    "Metadata save operation was cancelled",
                    "Normal cancellation during save");
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Playback,
                    "Failed to save queue metadata",
                    "Error occurred during debounced metadata save",
                    ex,
                    false);
            }
            finally
            {
                _saveLock.Release();
            }
        }

        /// <summary>
        /// Saves the full queue (tracks + metadata) immediately without debouncing.
        /// Cancels any pending metadata saves and any previous full save - "latest action wins".
        /// </summary>
        /// <param name="saveAction">The async action that performs the actual save</param>
        public async Task SaveFullQueueImmediate(Func<CancellationToken, Task> saveAction)
        {
            if (_isDisposing) return;

            // Full save takes priority - cancel any pending metadata saves
            CancelMetadataSave();

            // Cancel any previous full save
            CancelFullSave();

            // Create new cancellation token for this operation
            _fullSaveCts = new CancellationTokenSource();
            var currentToken = _fullSaveCts.Token;

            await _saveLock.WaitAsync(currentToken);
            try
            {
                // Check cancellation after acquiring lock
                if (currentToken.IsCancellationRequested)
                {
                    _errorHandlingService.LogInfo(
                        "Full queue save cancelled",
                        "Newer save operation took priority");
                    return;
                }

                await saveAction(currentToken);

                _errorHandlingService.LogInfo(
                    "Full queue save completed successfully",
                    "Immediate save executed");
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled - don't log as error
                _errorHandlingService.LogInfo(
                    "Full queue save operation was cancelled",
                    "Newer operation took priority");
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Playback,
                    "Failed to save full queue",
                    "Error occurred during immediate full queue save",
                    ex,
                    true);
            }
            finally
            {
                _saveLock.Release();
            }
        }

        /// <summary>
        /// Saves repeat mode immediately (no debouncing needed for this single field).
        /// Ensures no conflict with other save operations.
        /// </summary>
        public async Task SaveRepeatModeImmediate(Func<CancellationToken, Task> saveAction)
        {
            if (_isDisposing) return;

            // Don't cancel other operations - repeat mode is independent
            using var cts = new CancellationTokenSource();
            var currentToken = cts.Token;

            await _saveLock.WaitAsync(currentToken);
            try
            {
                await saveAction(currentToken);

                _errorHandlingService.LogInfo(
                    "Repeat mode save completed successfully",
                    "Immediate repeat mode update executed");
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to save repeat mode",
                    "Error occurred during repeat mode save",
                    ex,
                    false);
            }
            finally
            {
                _saveLock.Release();
            }
        }

        /// <summary>
        /// Cancels any pending metadata save operations.
        /// </summary>
        private void CancelMetadataSave()
        {
            try
            {
                _metadataSaveCts?.Cancel();
                _metadataSaveCts?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }

        /// <summary>
        /// Cancels any pending full queue save operations.
        /// </summary>
        private void CancelFullSave()
        {
            try
            {
                _fullSaveCts?.Cancel();
                _fullSaveCts?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }

        /// <summary>
        /// Cancels all pending save operations.
        /// </summary>
        public void CancelAllPendingSaves()
        {
            lock (_disposeLock)
            {
                CancelMetadataSave();
                CancelFullSave();
                _metadataDebounceTimer?.Dispose();
                _metadataDebounceTimer = null;
            }
        }

        /// <summary>
        /// Attempts to flush any pending debounced saves if there's enough time.
        /// Called during app shutdown to prevent data loss.
        /// </summary>
        /// <param name="maxWaitMs">Maximum time to wait for pending saves (default: 300ms)</param>
        public async Task FlushPendingSavesOnShutdown(int maxWaitMs = 300)
        {
            lock (_disposeLock)
            {
                _isDisposing = true;
            }

            try
            {
                // Check if there's a pending metadata save
                var hasMetadataPending = _metadataDebounceTimer != null;

                if (hasMetadataPending)
                {
                    _errorHandlingService.LogInfo(
                        "Shutdown: Pending metadata save detected",
                        $"Attempting to flush with {maxWaitMs}ms timeout");

                    // Try to acquire lock with timeout
                    var lockAcquired = await _saveLock.WaitAsync(maxWaitMs);

                    if (lockAcquired)
                    {
                        try
                        {
                            // Trigger the debounced save immediately
                            _metadataDebounceTimer?.Change(0, Timeout.Infinite);

                            // Wait a bit for it to complete
                            await Task.Delay(Math.Min(200, maxWaitMs - 50));

                            _errorHandlingService.LogInfo(
                                "Shutdown: Metadata save flushed",
                                "Pending save completed before shutdown");
                        }
                        finally
                        {
                            _saveLock.Release();
                        }
                    }
                    else
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Shutdown: Could not flush pending save",
                            "Lock timeout - recent changes may be lost (prevents corruption)",
                            null,
                            false);
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error during shutdown flush",
                    "Exception while flushing pending saves",
                    ex,
                    false);
            }
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                _isDisposing = true;

                CancelAllPendingSaves();

                _metadataSaveCts?.Dispose();
                _fullSaveCts?.Dispose();
                _metadataDebounceTimer?.Dispose();
                _saveLock?.Dispose();
            }
        }
    }
}