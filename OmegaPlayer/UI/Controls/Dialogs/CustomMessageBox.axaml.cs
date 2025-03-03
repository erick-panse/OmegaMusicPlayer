using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;

namespace OmegaPlayer.UI.Controls
{
    public partial class CustomMessageBox : Window
    {
        public enum MessageBoxButtons
        {
            OK,
            YesNo
        }

        public enum MessageBoxResult
        {
            None,
            OK,
            Yes,
            No
        }

        private MessageBoxResult _result = MessageBoxResult.None;
        private TextBlock _titleText;
        private TextBlock _messageText;
        private Button _yesButton;
        private Button _noButton;
        private Button _okButton;

        public CustomMessageBox()
        {
            InitializeComponent();

            // Find the controls AFTER initialization
            _titleText = this.FindControl<TextBlock>("TitleText");
            _messageText = this.FindControl<TextBlock>("MessageText");
            _yesButton = this.FindControl<Button>("YesButton");
            _noButton = this.FindControl<Button>("NoButton");
            _okButton = this.FindControl<Button>("OkButton");

            // Connect event handlers
            _yesButton.Click += YesButton_Click;
            _noButton.Click += NoButton_Click;
            _okButton.Click += OkButton_Click;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Shows a message box with customizable buttons
        /// </summary>
        /// <param name="owner">Parent window</param>
        /// <param name="title">Message box title</param>
        /// <param name="message">Message content</param>
        /// <param name="buttons">Button configuration (OK or YesNo)</param>
        /// <returns>Result indicating which button was clicked</returns>
        public static async Task<MessageBoxResult> Show(
            Window owner,
            string title,
            string message,
            MessageBoxButtons buttons = MessageBoxButtons.OK)
        {
            var msgBox = new CustomMessageBox();

            // Set text content
            msgBox._titleText.Text = title;
            msgBox._messageText.Text = message;

            // Configure buttons based on the requested type
            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    msgBox._okButton.IsVisible = true;
                    break;
                case MessageBoxButtons.YesNo:
                    msgBox._yesButton.IsVisible = true;
                    msgBox._noButton.IsVisible = true;
                    break;
            }

            // Show dialog and wait for result
            await msgBox.ShowDialog(owner);
            return msgBox._result;
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Yes;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.No;
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.OK;
            Close();
        }
    }
}