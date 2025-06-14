using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Unpack
{
    /// <summary>
    /// A simple dialog to get password input from the user.
    /// </summary>
    public sealed partial class PasswordInputDialog : ContentDialog
    {
        public string Password { get; private set; }

        public PasswordInputDialog()
        {
            this.InitializeComponent();
        }

        public void SetInstructionText(string text)
        {
            InstructionTextBlock.Text = text;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Retrieve the password from the PasswordBox
            Password = PasswordInputBox.Password;
            // Dialog will close automatically if args.Cancel is not set to true
        }

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Handle cancel logic if needed, e.g., set Password to null or an empty string
            Password = null;
            // Dialog will close.
        }
    }
}
