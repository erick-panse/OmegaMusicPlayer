using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OmegaMusicPlayer.UI.Controls
{
    public partial class GradientColorPicker : UserControl
    {
        public GradientColorPicker()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}