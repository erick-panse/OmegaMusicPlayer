using Avalonia.Controls;
using OmegaPlayer.Features.Profile.ViewModels;

namespace OmegaPlayer.Features.Profile.Views
{
    public partial class ProfileDialogView : Window
    {
        public ProfileDialogView()
        {
            InitializeComponent();
            DataContext = new ProfileDialogViewModel(this);
        }
    }
}