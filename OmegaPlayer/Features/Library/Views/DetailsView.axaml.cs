using Avalonia;
using Avalonia.Controls;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.ViewModels;
using System;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class DetailsView : UserControl
    {
        public DetailsView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            HeaderGrid.PropertyChanged += MainGrid_PropertyChanged;
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender != null)
            {
                var scrollViewer = sender as ScrollViewer;

                // If the user scrolls near the end, trigger the load more command
                if (scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 100)
                {
                    // Get the current view's ViewModel
                    if (DataContext is DetailsViewModel detailsViewModel &&
                        detailsViewModel is ILoadMoreItems loadMoreItems &&
                        loadMoreItems.LoadMoreItemsCommand.CanExecute(null))
                    {
                        loadMoreItems.LoadMoreItemsCommand.Execute(null);
                    }
                }
            }
        }
        private void OnLoaded(object? sender, EventArgs e)
        {
            UpdateTitleWidth();
        }

        private void MainGrid_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Grid.BoundsProperty)
            {
                UpdateTitleWidth();
            }
        }

        /// <summary>
        /// set title width for proper width and animation on TitleBlock
        /// </summary>
        private void UpdateTitleWidth()
        {
            if (TitleBlock == null) return;

            // view's usercontrol width
            double minWidth = this.MinWidth;
            double currentWidth = this.Bounds.Width;
            double percentage = 0.3; // standard percentage
            double padding = 90;

            if (currentWidth > minWidth)
            {
                for (int i = (int)minWidth; i < currentWidth; i += 100)
                {
                    percentage += 0.01; // increases percentage per every 100 width bigger than 850 (Minimum resolution)
                }
            }

            double widthPercentage = currentWidth * percentage; // get width available
            var textMaxWidth = widthPercentage - padding;

            // Avoid inconsistent AnimationWidth on large width
            if (textMaxWidth > 237)
            {
                TitleBlock.AnimationWidth = textMaxWidth;
            }

            TitleBlock.Width = textMaxWidth > 0 ? textMaxWidth : 270;
        }

        private void OnUnloaded(object sender, EventArgs e)
        {
            HeaderGrid.PropertyChanged -= MainGrid_PropertyChanged;
            this.Loaded -= OnLoaded;
            this.Unloaded -= OnUnloaded;
        }
    }
}