using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Infrastructure.Services.Cache;

namespace OmegaPlayer.Infrastructure.Services.Images
{
    /// <summary>
    /// Provides threaded image loading to prevent UI blocking when loading many images
    /// </summary>
    public class ImageLoadingService : IDisposable
    {
        private class ImageLoadRequest
        {
            public string ImagePath { get; }
            public int Width { get; }
            public int Height { get; }
            public bool IsHighQuality { get; }
            public int Priority { get; } // Lower number = higher priority
            public TaskCompletionSource<Bitmap> CompletionSource { get; }

            public ImageLoadRequest(string path, int width, int height, bool highQuality, int priority)
            {
                ImagePath = path;
                Width = width;
                Height = height;
                IsHighQuality = highQuality;
                Priority = priority;
                CompletionSource = new TaskCompletionSource<Bitmap>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        private readonly IErrorHandlingService _errorHandlingService;
        private readonly ImageCacheService _imageCacheService;

        // Queue of pending image loads - using a simple List and sorting since C# doesn't have a built-in priority queue
        private readonly List<ImageLoadRequest> _loadQueue = new List<ImageLoadRequest>();
        private readonly SemaphoreSlim _queueLock = new SemaphoreSlim(1, 1);
        private readonly AutoResetEvent _queueSignal = new AutoResetEvent(false);

        // Worker threads and cancellation
        private readonly List<Thread> _workerThreads = new List<Thread>();
        private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();
        private bool _disposed = false;

        // Constants
        private const int MAX_WORKER_THREADS = 2;

        // Track visible items to prioritize
        private readonly HashSet<string> _visibleItems = new HashSet<string>();
        private readonly SemaphoreSlim _visibilityLock = new SemaphoreSlim(1, 1);

        public ImageLoadingService(IErrorHandlingService errorHandlingService, ImageCacheService imageCacheService)
        {
            _errorHandlingService = errorHandlingService;
            _imageCacheService = imageCacheService;

            // Start worker threads
            StartWorkers();

            _errorHandlingService.LogError(
                ErrorSeverity.Info,
                "Image loading service initialized",
                $"Started {MAX_WORKER_THREADS} worker threads for background image loading",
                null,
                false);
        }

        private void StartWorkers()
        {
            for (int i = 0; i < MAX_WORKER_THREADS; i++)
            {
                var thread = new Thread(ProcessImageLoadQueue)
                {
                    Name = $"ImageLoader-{i}",
                    IsBackground = true, // Allow application to exit even if thread is running
                    Priority = ThreadPriority.BelowNormal // Don't compete with UI thread
                };

                _workerThreads.Add(thread);
                thread.Start();
            }
        }

        private void ProcessImageLoadQueue()
        {
            while (!_disposed && !_cancellationSource.IsCancellationRequested)
            {
                ImageLoadRequest request = null;

                // Wait for signal that queue has items
                _queueSignal.WaitOne(100); // Wait with timeout to check cancellation

                if (_disposed || _cancellationSource.IsCancellationRequested)
                    break;

                // Try to get a request from the queue
                _queueLock.Wait();
                try
                {
                    if (_loadQueue.Count > 0)
                    {
                        // Sort by priority
                        _loadQueue.Sort((a, b) => a.Priority.CompareTo(b.Priority));

                        // Take highest priority item
                        request = _loadQueue[0];
                        _loadQueue.RemoveAt(0);
                    }
                }
                finally
                {
                    _queueLock.Release();
                }

                // Process the request if we got one
                if (request != null)
                {
                    try
                    {
                        Bitmap bitmap;
                        if (request.IsHighQuality)
                        {
                            bitmap = _imageCacheService.LoadHighQualityImageAsync(
                                request.ImagePath, request.Width, request.Height).Result;
                        }
                        else
                        {
                            bitmap = _imageCacheService.LoadThumbnailAsync(
                                request.ImagePath, request.Width, request.Height).Result;
                        }

                        request.CompletionSource.SetResult(bitmap);
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Image loading failed",
                            $"Failed to load image: {request.ImagePath}",
                            ex,
                            false);

                        request.CompletionSource.SetException(ex);
                    }
                }
            }
        }

        /// <summary>
        /// Loads an image asynchronously on a background thread
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="width">Target width</param>
        /// <param name="height">Target height</param>
        /// <param name="highQuality">Whether to use high quality loading</param>
        /// <param name="isVisible">Whether the image is currently visible in the UI</param>
        /// <param name="isTopPriority">Whether the image should load immediatly</param>
        /// <returns>Task that completes with the loaded bitmap</returns>
        public Task<Bitmap> LoadImageAsync(string imagePath, int width, int height, bool highQuality, bool isVisible, bool isTopPriority = false)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ImageLoadingService));

            // Fast fail for invalid paths
            if (string.IsNullOrEmpty(imagePath))
            {
                return Task.FromResult<Bitmap>(null);
            }

            // Create request with appropriate priority
            int priority = isTopPriority ? 0 : (isVisible ? 1 : 10);
            var request = new ImageLoadRequest(imagePath, width, height, highQuality, priority);

            // Check if this path is marked as visible
            _visibilityLock.Wait();
            try
            {
                if (_visibleItems.Contains(imagePath) && priority > 1) // Only upgrade if current priority is lower than visible priority
                {
                    request = new ImageLoadRequest(imagePath, width, height, highQuality, 1);
                }
            }
            finally
            {
                _visibilityLock.Release();
            }

            // Enqueue request
            _queueLock.Wait();
            try
            {
                _loadQueue.Add(request);
                _queueSignal.Set(); // Signal that queue has items
            }
            finally
            {
                _queueLock.Release();
            }

            return request.CompletionSource.Task;
        }

        /// <summary>
        /// Notifies the service that an image is now visible or invisible in the UI
        /// </summary>
        /// <param name="imagePath">Path to the image</param>
        /// <param name="isVisible">Whether the image is visible</param>
        public async Task SetItemVisibility(string imagePath, bool isVisible)
        {
            if (_disposed)
                return;

            if (string.IsNullOrEmpty(imagePath))
                return;

            await _visibilityLock.WaitAsync();
            try
            {
                if (isVisible)
                {
                    _visibleItems.Add(imagePath);
                }
                else
                {
                    _visibleItems.Remove(imagePath);
                }

                // Update priorities of queued items
                await _queueLock.WaitAsync();
                try
                {
                    // Find matching requests and update their priority
                    bool needsSort = false;
                    foreach (var request in _loadQueue)
                    {
                        if (request.ImagePath == imagePath)
                        {
                            // Can't modify Priority directly since it's readonly, but we'll sort the queue
                            needsSort = true;
                            break;
                        }
                    }

                    // If we changed priorities, resort the queue
                    if (needsSort)
                    {
                        _loadQueue.Sort((a, b) =>
                        {
                            // Sort by priority FIRST (0 = top priority, 1 = visible, 10 = not visible)
                            int priorityComparison = a.Priority.CompareTo(b.Priority);
                            if (priorityComparison != 0)
                                return priorityComparison;

                            // If same priority, sort by visibility as secondary criteria
                            bool aVisible = _visibleItems.Contains(a.ImagePath);
                            bool bVisible = _visibleItems.Contains(b.ImagePath);

                            if (aVisible != bVisible)
                                return aVisible ? -1 : 1;

                            // If same priority and visibility, maintain insertion order
                            return 0;
                        });
                    }
                }
                finally
                {
                    _queueLock.Release();
                }
            }
            finally
            {
                _visibilityLock.Release();
            }
        }

        /// <summary>
        /// Cancels all pending image loads, typically used when navigating away from a view
        /// </summary>
        public async Task CancelPendingLoads()
        {
            if (_disposed)
                return;

            await _queueLock.WaitAsync();
            try
            {
                foreach (var request in _loadQueue)
                {
                    // Set canceled result so tasks don't hang
                    request.CompletionSource.TrySetCanceled();
                }

                _loadQueue.Clear();
            }
            finally
            {
                _queueLock.Release();
            }
        }

        /// <summary>
        /// Dispose resources used by the image loading service
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Signal cancellation to all threads
            _cancellationSource.Cancel();

            // Release waiting threads
            _queueSignal.Set();

            // Give threads time to shut down gracefully
            foreach (var thread in _workerThreads)
            {
                thread.Join(100); // Wait up to 100ms per thread
            }

            // Clean up resources
            _queueSignal.Dispose();
            _queueLock.Dispose();
            _visibilityLock.Dispose();
            _cancellationSource.Dispose();

            // Clear collections
            _loadQueue.Clear();
            _visibleItems.Clear();
            _workerThreads.Clear();
        }
    }
}