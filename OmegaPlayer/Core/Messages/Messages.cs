using OmegaPlayer.Features.Library.ViewModels;

namespace OmegaPlayer.Core.Messages
{

    public class NavigationRequestMessage
    {
        public ContentType ContentType { get; }
        public object Data { get; }

        public NavigationRequestMessage(ContentType contentType, object data)
        {
            ContentType = contentType;
            Data = data;
        }

    }

    /// <summary>
    /// Message for notifying that a profile's configuration has changed
    /// </summary>
    public class ProfileChangedMessage {}

}
