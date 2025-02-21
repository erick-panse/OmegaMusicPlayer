using System;

namespace OmegaPlayer.Core.Messages
{

    public class ReorderModeMessage
    { 
        // Used to display floating buttons and call methods of Library while preventing circular dependency on Main View
        public bool IsInReorderMode { get; }
        public Action SaveAction { get; }
        public Action CancelAction { get; }

        public ReorderModeMessage(bool isInReorderMode, Action saveAction, Action cancelAction)
        {
            IsInReorderMode = isInReorderMode;
            SaveAction = saveAction;
            CancelAction = cancelAction;
        }
    }
}
