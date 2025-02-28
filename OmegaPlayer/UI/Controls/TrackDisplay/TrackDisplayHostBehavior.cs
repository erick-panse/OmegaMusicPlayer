using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using OmegaPlayer.UI.Controls.TrackDisplay;

namespace OmegaPlayer.UI.Attached
{
    /// <summary>
    /// Behavior that will automatically initialize TrackDisplayHost attached properties
    /// when added to a control
    /// </summary>
    public class TrackDisplayHostBehavior : Behavior<Control>
    {
        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject != null)
            {
                // Initialize the properties when loaded
                AssociatedObject.Loaded += OnLoaded;

                // If already loaded, initialize immediately
                if (AssociatedObject.IsLoaded)
                {
                    InitializeHost();
                }
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded -= OnLoaded;
            }

            base.OnDetaching();
        }

        private void OnLoaded(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InitializeHost();
        }

        private void InitializeHost()
        {
            TrackDisplayHostProperties.InitializeHost(AssociatedObject);
        }
    }
}