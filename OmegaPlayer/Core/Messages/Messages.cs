using OmegaPlayer.Features.Library.ViewModels;
using System;

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

}
