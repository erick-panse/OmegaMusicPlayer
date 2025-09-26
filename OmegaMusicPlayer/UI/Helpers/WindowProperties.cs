using Avalonia;
using Avalonia.Controls;

namespace OmegaMusicPlayer.UI.Helpers
{
    public class WindowProperties
    {
        public static readonly AttachedProperty<bool> IsWindowedProperty =
            AvaloniaProperty.RegisterAttached<WindowProperties, Grid, bool>("IsWindowed", true);

        public static bool GetIsWindowed(Grid grid)
        {
            return grid.GetValue(IsWindowedProperty);
        }

        public static void SetIsWindowed(Grid grid, bool value)
        {
            grid.SetValue(IsWindowedProperty, value);
        }
    }
}