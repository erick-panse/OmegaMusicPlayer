using System.Windows.Input;

namespace OmegaPlayer.Core.Interfaces
{
    public interface ILoadMoreItems
    {
        ICommand LoadMoreItemsCommand { get; }
    }
}
