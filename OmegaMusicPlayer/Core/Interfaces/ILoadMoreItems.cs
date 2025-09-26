using System.Windows.Input;

namespace OmegaMusicPlayer.Core.Interfaces
{
    public interface ILoadMoreItems
    {
        ICommand LoadMoreItemsCommand { get; }
    }
}
