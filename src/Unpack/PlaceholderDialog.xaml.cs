using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Unpack
{
    /// <summary>
    /// A simple dialog to show placeholder messages.
    /// </summary>
    public sealed partial class PlaceholderDialog : ContentDialog
    {
        public PlaceholderDialog()
        {
            this.InitializeComponent();
            // Ensure XamlRoot is set for ContentDialog to function correctly when shown from code-behind.
            // This is typically done when ShowAsync is called, by setting dialog.XamlRoot = element.XamlRoot.
            // However, it's good practice to be aware of it.
        }

        public void SetMessage(string message)
        {
            MessageTextBlock.Text = message;
        }

        // Optional: Set title as well
        public void SetTitle(string title)
        {
            this.Title = title;
        }
    }
}
