using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.UI;
using System;
using System.Linq;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class FoldersView : UserControl
    {
        private ItemsRepeater _foldersItemsRepeater;
        private IErrorHandlingService _errorHandlingService;
        private bool _isDisposed = false;
        private DispatcherTimer _visibilityCheckTimer;
        private ScrollViewer _scrollViewer;

        public FoldersView()
        {
            _errorHandlingService = App.ServiceProvider.GetService<IErrorHandlingService>();

            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            // Initialize timer for batched visibility checks (performance optimization)
            _visibilityCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Check every 100ms instead of on every scroll event
            };
            _visibilityCheckTimer.Tick += (s, e) =>
            {
                _visibilityCheckTimer.Stop();
                CheckVisibleItems();
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _foldersItemsRepeater = this.FindControl<ItemsRepeater>("FoldersItemsRepeater");
            _scrollViewer = this.FindControl<ScrollViewer>("FoldersScrollViewer");
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isDisposed) return;
            
            // Use timer-based batching to reduce excessive calls during fast scrolling
            _visibilityCheckTimer.Stop();
            _visibilityCheckTimer.Start();
        }

        private async void CheckVisibleItems()
        {
            if (_isDisposed || _scrollViewer == null || _foldersItemsRepeater == null) return;
            if (!(DataContext is FoldersViewModel viewModel)) return;

            try
            {
                var viewportTop = _scrollViewer.Offset.Y;
                var viewportBottom = viewportTop + _scrollViewer.Viewport.Height;
                var buffer = 300;

                var (itemHeight, itemWidth, itemsPerRow) = GetItemDimensions(_scrollViewer.Viewport.Width);

                int index = 0;
                foreach (var folder in viewModel.Folders)
                {
                    if (folder.Cover == null)
                    {
                        var rowIndex = index / itemsPerRow;
                        var estimatedTop = rowIndex * itemHeight;
                        var estimatedBottom = estimatedTop + itemHeight;

                        bool isVisible = estimatedBottom > (viewportTop - buffer) && estimatedTop < (viewportBottom + buffer);

                        if (isVisible)
                        {
                            await viewModel.NotifyFolderVisible(folder, true);
                        }
                    }
                    index++;
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(ErrorSeverity.NonCritical, "Error checking visible folder", ex.Message, ex, false);
            }
        }

        private (double itemHeight, double itemWidth, int itemsPerRow) GetItemDimensions(double viewportWidth)
        {
            var actualDimensions = GetActualItemDimensions(viewportWidth);
            return actualDimensions ?? (190, 152, Math.Max(1, (int)(viewportWidth / 152)));
        }

        private (double itemHeight, double itemWidth, int itemsPerRow)? GetActualItemDimensions(double viewportWidth)
        {
            if (_foldersItemsRepeater == null) return null;

            try
            {
                var containers = _foldersItemsRepeater.GetVisualChildren().OfType<Control>().Where(c => c.DataContext != null).Take(5).ToList();
                if (containers.Count == 0) return null;

                var avgHeight = containers.Average(c => c.Bounds.Height);
                var avgWidth = containers.Average(c => c.Bounds.Width);
                var itemsPerRow = Math.Max(1, (int)(viewportWidth / avgWidth));

                return (avgHeight, avgWidth, itemsPerRow);
            }
            catch { return null; }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isDisposed = true;
            _visibilityCheckTimer?.Stop();
            _visibilityCheckTimer = null;
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isDisposed = true;
            _visibilityCheckTimer?.Stop();
            base.OnDetachedFromVisualTree(e);
        }
    }
}