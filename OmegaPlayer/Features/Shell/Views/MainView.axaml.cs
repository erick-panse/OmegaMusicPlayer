using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.UI;
using OmegaPlayer.UI.Helpers;
using System;

namespace OmegaPlayer.Features.Shell.Views
{
    public partial class MainView : Window
    {
        // Add this flag to track programmatic changes
        private bool _isUpdatingProgrammatically = false;

        public MainView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            App.ServiceProvider.GetRequiredService<ProfileManager>();

            PropertyChanged += MainView_PropertyChanged;
        }

        private void MainView_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == WindowStateProperty)
            {
                if (this.FindControl<Grid>("CussstomTitleBar") is Grid titleBar)
                {
                    WindowProperties.SetIsWindowed(titleBar, WindowState != WindowState.Maximized);
                }
            }
        }

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender != null)
            {
                var scrollViewer = sender as ScrollViewer;

                // If the user scrolls near the end, trigger the load more command
                if (scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 100)
                {
                    // Get the current view's ViewModel
                    if (DataContext is MainViewModel mainViewModel &&
                        mainViewModel.CurrentPage is ILoadMoreItems loadMoreItems &&
                        loadMoreItems.LoadMoreItemsCommand.CanExecute(null))
                    {
                        loadMoreItems.LoadMoreItemsCommand.Execute(null);
                    }
                }
            }
        }
        private void CussstomTitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
            }
            else if (e.ClickCount == 1)
            {
                BeginMoveDrag(e);
            }
        }
        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }


        // Method to handle the sort dropdown button click
        private void OnSortDropdownButtonClick(object sender, RoutedEventArgs e)
        {
            // Find the popup
            var popup = this.FindControl<Popup>("SortPopup");
            if (popup != null)
            {
                // Toggle the popup visibility
                popup.IsOpen = !popup.IsOpen;

                // Initialize temp values when opening
                if (popup.IsOpen && DataContext is MainViewModel vm)
                {
                    vm.InitializeTempSortSettings();
                }
            }
        }
        private void OnApplyButtonClick(object sender, RoutedEventArgs e)
        {
            // Close the popup directly from the button click
            var popup = this.FindControl<Popup>("SortPopup");
            if (popup != null)
            {
                popup.IsOpen = false;
            }

            // Let the event bubble to run the command
            e.Handled = false;
        }
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Only trigger search if there's text
                if (!string.IsNullOrWhiteSpace(vm.SearchViewModel.SearchQuery))
                {
                    vm.SearchViewModel.ShowSearchFlyout = true;
                    vm.SearchViewModel.SearchPreviewCommand.Execute(null);
                }
                else
                {
                    vm.SearchViewModel.ShowSearchFlyout = false;
                }
            }
        }
        private void OnSearchPopupClosed(object sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Don't reset search query when popup closes
                // Only update the flyout state
                vm.SearchViewModel.ShowSearchFlyout = false;
            }
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is MainViewModel vm)
            {
                vm.SearchViewModel.SearchCommand.Execute(null);
            }
        }
    }
}

