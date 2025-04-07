using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Features.Profile.ViewModels;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.UI;
using System.Collections.Generic;

namespace OmegaPlayer.Features.Profile.Views
{
    public partial class ProfileDialogView : Window
    {
        private ItemsControl _profilesItemsControl;
        private HashSet<int> _visibleProfileIndexes = new HashSet<int>();

        public ProfileDialogView()
        {
            InitializeComponent();
            var profileService = App.ServiceProvider.GetService<ProfileService>();
            var profileManager = App.ServiceProvider.GetService<ProfileManager>();
            var localizationService = App.ServiceProvider.GetService<LocalizationService>();
            var standardImageService = App.ServiceProvider.GetService<StandardImageService>();
            var messenger = App.ServiceProvider.GetService<IMessenger>();
            DataContext = new ProfileDialogViewModel(this, profileService, profileManager, localizationService, standardImageService, messenger);

            // Hook into the Loaded event to find the ItemsControl
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _profilesItemsControl = this.FindControl<ItemsControl>("ProfilesItemsControl");

            // Check initially visible items
            if (_profilesItemsControl != null)
            {
                // Delay slightly to ensure containers are realized
                Dispatcher.UIThread.Post(() => CheckVisibleItems());
            }
        }

        private async void CheckVisibleItems()
        {
            if (DataContext is not ProfileDialogViewModel viewModel || _profilesItemsControl == null)
                return;

            // Get a reference to the ScrollViewer
            var scrollViewer = this.FindControl<ScrollViewer>("ProfilesScrollViewer");
            if (scrollViewer == null) return;

            // Keep track of which items are currently visible
            var newVisibleIndexes = new HashSet<int>();

            // Get all item containers
            var containers = _profilesItemsControl.GetRealizedContainers();

            foreach (var container in containers)
            {
                // Get the container's position relative to the scroll viewer
                var transform = container.TransformToVisual(scrollViewer);
                if (transform != null)
                {
                    var containerTop = transform.Value.Transform(new Point(0, 0)).Y;
                    var containerHeight = container.Bounds.Height;
                    var containerBottom = containerTop + containerHeight;

                    // Check if the container is in the viewport (fully or partially)
                    bool isVisible = (containerBottom > 0 && containerTop < scrollViewer.Viewport.Height);

                    // Get the container's index
                    int index = _profilesItemsControl.IndexFromContainer(container);

                    if (isVisible)
                    {
                        newVisibleIndexes.Add(index);

                        // If not previously visible, notify it's now visible
                        if (!_visibleProfileIndexes.Contains(index))
                        {
                            // Get the profile from the ViewModel
                            if (index >= 0 && index < viewModel.Profiles.Count)
                            {
                                var profile = viewModel.Profiles[index];
                                await viewModel.NotifyProfilePhotoVisible(profile, true);
                            }
                        }
                    }
                    else if (_visibleProfileIndexes.Contains(index))
                    {
                        // Was visible before but not anymore
                        if (index >= 0 && index < viewModel.Profiles.Count)
                        {
                            var profile = viewModel.Profiles[index];
                            await viewModel.NotifyProfilePhotoVisible(profile, false);
                        }
                    }
                }
            }

            // Update the visible indexes
            _visibleProfileIndexes = newVisibleIndexes;
        }

        // Handle the ScrollChanged event
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender != null)
            {
                CheckVisibleItems();
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // Clean up event handlers
            Loaded -= OnLoaded;

            base.OnDetachedFromVisualTree(e);
        }
    }
}