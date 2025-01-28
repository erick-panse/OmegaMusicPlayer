using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Library.Models;
using System.Linq;
using System.Diagnostics;
using Avalonia.Media;
using OmegaPlayer.Core;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class PlaylistView : UserControl
    {
        public PlaylistView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}