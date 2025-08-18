using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.UI;
using OmegaPlayer.UI.Helpers;
using System;
using System.Threading;

namespace OmegaPlayer.Features.Shell.Views
{
    public partial class MainView : Window
    {
        private IErrorHandlingService _errorHandlingService;

        private Timer _searchDebounceTimer;

        private WindowResizeHandler _windowResizeHandler;

        public MainView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            _errorHandlingService = App.ServiceProvider.GetRequiredService<IErrorHandlingService>();

            PropertyChanged += MainView_PropertyChanged;

            // Mouse navigation support
            PointerPressed += MainView_PointerPressed;

            // Initialize WindowResizeHandler for coordinating resize operations
            _windowResizeHandler = new WindowResizeHandler(this);
        }

        /// <summary>
        /// Gets the WindowResizeHandler instance for this window
        /// </summary>
        public WindowResizeHandler ResizeHandler => _windowResizeHandler;

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

        private void CussstomTitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    if (e.ClickCount == 2)
                    {
                        ToggleWindowState();
                    }
                    else if (e.ClickCount == 1)
                    {
                        BeginMoveDrag(e);
                    }
                },
                "Processing title bar interaction",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnSortDropdownButtonClick(object sender, RoutedEventArgs e)
        {
            _errorHandlingService.SafeExecute(
                () =>
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
                },
                "Opening sort dropdown",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private void OnApplyButtonClick(object sender, RoutedEventArgs e)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    // Close the popup directly from the button click
                    var popup = this.FindControl<Popup>("SortPopup");
                    if (popup != null)
                    {
                        popup.IsOpen = false;
                    }

                    // Let the event bubble to run the command
                    e.Handled = false;
                },
                "Applying sort settings",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Cancel previous timer
                _searchDebounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);

                // Start new timer (300ms delay)
                _searchDebounceTimer = new Timer(async _ =>
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // Only trigger search if there's text
                        if (!string.IsNullOrWhiteSpace(vm.SearchViewModel.SearchQuery))
                        {
                            vm.SearchViewModel.ShowSearchFlyout = true;
                            _ = vm.SearchViewModel.SearchPreviewCommand.ExecuteAsync(null);
                        }
                        else
                        {
                            vm.SearchViewModel.ShowSearchFlyout = false;
                        }
                    });
                }, null, 300, Timeout.Infinite);
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
            _errorHandlingService.SafeExecute(
                () =>
                {
                    if (e.Key == Key.Enter && DataContext is MainViewModel vm)
                    {
                        vm.SearchViewModel.SearchCommand.Execute(null);
                    }
                },
                "Processing search keyboard input",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async void MainView_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            try
            {
                var properties = e.GetCurrentPoint(this).Properties;

                // Check for mouse side buttons
                if (properties.IsXButton1Pressed && DataContext is MainViewModel vm)
                {
                    // Mouse button 4 - Navigate Back
                    if (vm.CanNavigateBack)
                    {
                        await vm.NavigateBackCommand.ExecuteAsync(null);
                        e.Handled = true;
                    }
                }
                else if (properties.IsXButton2Pressed && DataContext is MainViewModel vm2)
                {
                    // Mouse button 5 - Navigate Forward  
                    if (vm2.CanNavigateForward)
                    {
                        await vm2.NavigateForwardCommand.ExecuteAsync(null);
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling mouse navigation",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Cleanup resources when window is closing
        /// </summary>
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            try
            {
                // Dispose WindowResizeHandler to cleanup event handlers
                _windowResizeHandler?.Dispose();
                _searchDebounceTimer?.Dispose();
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error during MainView cleanup",
                    ex.Message,
                    ex,
                    false);
            }

            base.OnClosing(e);
        }
    }
}