using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics; // For Debug.WriteLine
using System.IO; // For Path (used in BrowseButton_Click as an example)
using Windows.Storage.Pickers; // For FileSavePicker
using WinRT.Interop; // For InitializeWithWindow

namespace Unpack
{
    public sealed partial class CreateArchiveDialog : ContentDialog
    {
        // General Tab
        public string ArchiveFullName { get; private set; } // Full path including name and extension
        public string SelectedArchiveFormat { get; private set; }
        public string SelectedCompressionLevel { get; private set; }
        public string SelectedUpdateMode { get; private set; }
        public string SelectedFilePathsInArchive { get; private set; }


        // Advanced Tab
        public string SelectedZipCompressionMethod { get; private set; }
        public string SelectedSevenZipCompressionMethod { get; private set; }
        public string SelectedSevenZipDictionarySize { get; private set; }
        public string SelectedSevenZipWordSize { get; private set; }
        public string SelectedSevenZipSolidBlockSize { get; private set; }
        public string SelectedCpuThreads { get; private set; }

        // Password Tab
        public string Password { get; private set; }
        public bool EncryptFileNames { get; private set; }
        public string SelectedEncryptionMethod { get; private set; }

        // Split/SFX Tab
        public string VolumeSize { get; private set; } // Stores custom value or selected preset string
        public bool CreateSfxArchive { get; private set; }

        // Comment Tab
        public string ArchiveComment { get; private set; }

        // Store initial items to be archived (passed from MainWindow)
        // public IReadOnlyList<FileSystemItem> ItemsToArchive { get; private set; }

        public CreateArchiveDialog() // Potentially: public CreateArchiveDialog(IReadOnlyList<FileSystemItem> itemsToArchive)
        {
            this.InitializeComponent();
            // this.ItemsToArchive = itemsToArchive;

            PopulateComboBoxes();
            SetDefaultSelections();
            UpdateAdvancedOptionsVisibility(); // Initial call based on default format
            UpdatePasswordControlsState(); // Initial call

            // Suggest an archive name based on items if provided, or a default
            // if (itemsToArchive != null && itemsToArchive.Count > 0)
            // {
            //     string basePath = Path.GetDirectoryName(itemsToArchive[0].FullPath);
            //     string suggestedName = itemsToArchive.Count == 1 ? Path.GetFileNameWithoutExtension(itemsToArchive[0].Name) : new DirectoryInfo(basePath ?? "").Name;
            //     if (string.IsNullOrEmpty(suggestedName) && itemsToArchive.Count == 1 && itemsToArchive[0].ItemType == "Drive") {
            //          suggestedName = itemsToArchive[0].Name.Replace(":", "").Replace("\\",""); // "C" from "C:"
            //     }
            //     ArchiveNamePathTextBox.Text = Path.Combine(basePath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), suggestedName + ".zip");
            // }
            // else
            // {
                 ArchiveNamePathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Archive.zip");
            // }
        }

        private void PopulateComboBoxes()
        {
            // Compression Levels - will be updated by ArchiveFormatComboBox_SelectionChanged
            // Update Modes already in XAML
            // File Paths in Archive already in XAML
            // ZIP Compression Methods already in XAML
            // 7z Compression Methods, Dictionary, Word, Solid Block already in XAML
            // CPU Threads already in XAML
            // Encryption Methods - will be updated by ArchiveFormatComboBox_SelectionChanged
            // Volume Sizes already in XAML
        }

        private void SetDefaultSelections()
        {
            ArchiveFormatComboBox.SelectedIndex = 0; // ZIP
            UpdateCompressionLevels(); // For ZIP
            CompressionLevelComboBox.SelectedValue = "Normal"; // Default for ZIP

            UpdateModeComboBox.SelectedIndex = 0;
            FilePathsComboBox.SelectedIndex = 0;

            // Advanced defaults (assuming ZIP is default format)
            ZipCompressionMethodComboBox.SelectedValue = "Deflate";
            SevenZipCompressionMethodComboBox.SelectedValue = "LZMA2"; // Default if 7z was chosen
            SevenZipDictionarySizeComboBox.SelectedValue = "16 MB";
            SevenZipWordSizeComboBox.SelectedValue = "64";
            SevenZipSolidBlockSizeComboBox.SelectedValue = "Off (non-solid)";
            CpuThreadsComboBox.SelectedValue = "Auto";

            // Password defaults
            UpdateEncryptionMethods(); // For ZIP
            EncryptionMethodComboBox.SelectedValue = "AES-256"; // Default for ZIP if available

            // Split/SFX defaults
            VolumeSizeComboBox.SelectedIndex = 0; // No splitting
        }

        private void ArchiveFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAdvancedOptionsVisibility();
            UpdateCompressionLevels();
            UpdateEncryptionMethods();
            UpdateSfxAvailability();

            // Update default extension in file path if user hasn't manually changed it much
            if (!string.IsNullOrWhiteSpace(ArchiveNamePathTextBox.Text))
            {
                string currentPath = ArchiveNamePathTextBox.Text;
                string currentExtension = Path.GetExtension(currentPath); // .zip or .7z
                string newExtension = (ArchiveFormatComboBox.SelectedItem as string)?.ToLowerInvariant(); // zip or 7z

                if (!string.IsNullOrEmpty(newExtension) && (currentExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase) || currentExtension.Equals(".7z", StringComparison.OrdinalIgnoreCase)))
                {
                    ArchiveNamePathTextBox.Text = Path.ChangeExtension(currentPath, "." + newExtension);
                }
                else if (!string.IsNullOrEmpty(newExtension) && string.IsNullOrEmpty(currentExtension)) // Has name but no extension
                {
                     ArchiveNamePathTextBox.Text = currentPath + "." + newExtension;
                }
            }
        }

        private void UpdateAdvancedOptionsVisibility()
        {
            if (ArchiveFormatComboBox.SelectedItem as string == "7z")
            {
                AdvancedSettingsTitleTextBlock.Text = "Advanced Settings for 7z";
                ZipAdvancedOptionsPanel.Visibility = Visibility.Collapsed;
                SevenZipAdvancedOptionsPanel.Visibility = Visibility.Visible;
            }
            else // ZIP or others
            {
                AdvancedSettingsTitleTextBlock.Text = "Advanced Settings for ZIP";
                ZipAdvancedOptionsPanel.Visibility = Visibility.Visible;
                SevenZipAdvancedOptionsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateCompressionLevels()
        {
            string selectedFormat = ArchiveFormatComboBox.SelectedItem as string;
            CompressionLevelComboBox.Items.Clear();
            var levels = new List<string> { "Store (no compression)", "Fastest", "Fast", "Normal", "Maximum", "Ultra" };
            foreach (var level in levels) CompressionLevelComboBox.Items.Add(level);

            // Set a sensible default if the previous one isn't available (though they are same for zip/7z here)
            CompressionLevelComboBox.SelectedValue = "Normal";
        }

        private void UpdateEncryptionMethods()
        {
            string selectedFormat = ArchiveFormatComboBox.SelectedItem as string;
            EncryptionMethodComboBox.Items.Clear();
            if (selectedFormat == "7z")
            {
                EncryptionMethodComboBox.Items.Add("AES-256");
                EncryptionMethodComboBox.SelectedIndex = 0;
                EncryptFileNamesCheckBox.IsEnabled = EnableEncryptionCheckBox.IsChecked == true; // 7z supports filename encryption
            }
            else // ZIP
            {
                EncryptionMethodComboBox.Items.Add("AES-256");
                EncryptionMethodComboBox.Items.Add("ZipCrypto (legacy)");
                EncryptionMethodComboBox.SelectedIndex = 0; // Default to AES-256
                EncryptFileNamesCheckBox.IsEnabled = false; // Standard ZIP AES doesn't typically encrypt filenames easily, 7-Zip's does.
                                                            // For simplicity, disable for ZIP. Could be enabled if using a specific ZIP library that supports it.
                EncryptFileNamesCheckBox.IsChecked = false;
            }
        }

        private void UpdateSfxAvailability()
        {
             string selectedFormat = ArchiveFormatComboBox.SelectedItem as string;
             if (selectedFormat == "7z") {
                CreateSfxArchiveCheckBox.IsEnabled = true;
             } else { // ZIP SFX might be possible but often simpler with 7z for robust options
                CreateSfxArchiveCheckBox.IsEnabled = true; // Let's assume basic ZIP SFX is also possible
             }
        }


        private void EnableEncryptionCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePasswordControlsState();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ValidatePasswords();
            // Potentially enable/disable EncryptFileNamesCheckBox based on password presence
            if (EnableEncryptionCheckBox.IsChecked == true) {
                 EncryptFileNamesCheckBox.IsEnabled = (ArchiveFormatComboBox.SelectedItem as string == "7z") &&
                                                   !string.IsNullOrEmpty(EnterPasswordBox.Password);
            }
        }

        private void ValidatePasswords()
        {
            if (EnterPasswordBox.Password != ReEnterPasswordBox.Password)
            {
                PasswordMatchErrorTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                PasswordMatchErrorTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowPasswordCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // This is not directly possible with PasswordBox.
            // A common workaround is to swap PasswordBox with a TextBox.
            // For this version, we'll just note that direct ShowPassword is not simple.
            // Alternative: use a TextBox and toggle font. For simplicity, this checkbox won't change behavior here.
            Debug.WriteLine("ShowPasswordCheckBox interaction - direct reveal not standard for PasswordBox.");
        }

        private void UpdatePasswordControlsState()
        {
            bool enabled = EnableEncryptionCheckBox.IsChecked == true;
            EnterPasswordBox.IsEnabled = enabled;
            ReEnterPasswordBox.IsEnabled = enabled;
            ShowPasswordCheckBox.IsEnabled = enabled;
            EncryptionMethodComboBox.IsEnabled = enabled;
            EncryptFileNamesCheckBox.IsEnabled = enabled &&
                                               (ArchiveFormatComboBox.SelectedItem as string == "7z") &&
                                               !string.IsNullOrEmpty(EnterPasswordBox.Password);

            if (!enabled)
            {
                EnterPasswordBox.Password = "";
                ReEnterPasswordBox.Password = "";
                PasswordMatchErrorTextBlock.Visibility = Visibility.Collapsed;
                EncryptFileNamesCheckBox.IsChecked = false;
            }
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker savePicker = new FileSavePicker();
            InitializeWithWindow.Initialize(savePicker, App.WindowHandle); // Initialize with window handle

            string selectedFormat = ArchiveFormatComboBox.SelectedItem as string ?? "zip";
            if (selectedFormat.Equals("7z", StringComparison.OrdinalIgnoreCase))
            {
                savePicker.FileTypeChoices.Add("7z Archive", new List<string>() { ".7z" });
                savePicker.DefaultFileExtension = ".7z";
            }
            else // Default to ZIP
            {
                savePicker.FileTypeChoices.Add("ZIP Archive", new List<string>() { ".zip" });
                savePicker.DefaultFileExtension = ".zip";
            }

            // Suggest filename based on current textbox or a default
            string currentFullPath = ArchiveNamePathTextBox.Text;
            try
            {
                savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(currentFullPath);
                string initialDir = Path.GetDirectoryName(currentFullPath);
                if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
                {
                    savePicker.SuggestedStartLocation = await StorageFolder.GetFolderFromPathAsync(initialDir);
                }
            }
            catch { /* Use default start location if path parsing fails */ }


            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                ArchiveNamePathTextBox.Text = file.Path;
            }
        }

        private void CustomVolumeSizeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Basic validation for custom volume size could be added here (e.g., numeric, valid units)
        }

        private void VolumeSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VolumeSizeComboBox.SelectedItem is string selectedSize && selectedSize.Equals("Custom...", StringComparison.OrdinalIgnoreCase))
            {
                CustomVolumeSizeTextBox.Visibility = Visibility.Visible;
            }
            else
            {
                CustomVolumeSizeTextBox.Visibility = Visibility.Collapsed;
            }
        }


        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Validate Passwords if encryption is enabled
            if (EnableEncryptionCheckBox.IsChecked == true)
            {
                if (string.IsNullOrEmpty(EnterPasswordBox.Password))
                {
                    args.Cancel = true; // Prevent dialog from closing
                    PasswordMatchErrorTextBlock.Text = "Password cannot be empty.";
                    PasswordMatchErrorTextBlock.Visibility = Visibility.Visible;
                    return;
                }
                if (EnterPasswordBox.Password != ReEnterPasswordBox.Password)
                {
                    args.Cancel = true; // Prevent dialog from closing
                    PasswordMatchErrorTextBlock.Text = "Passwords do not match.";
                    PasswordMatchErrorTextBlock.Visibility = Visibility.Visible;
                    return;
                }
            }
            PasswordMatchErrorTextBlock.Visibility = Visibility.Collapsed;

            // --- Retrieve values from UI controls and assign to public properties ---
            ArchiveFullName = ArchiveNamePathTextBox.Text;
            // OutputFolderPath would typically be Path.GetDirectoryName(ArchiveFullName)
            // Or, if ArchiveNamePathTextBox only holds the name, then another control would hold the path.
            // For this setup, ArchiveNamePathTextBox holds the full path.
            OutputFolderPath = Path.GetDirectoryName(ArchiveFullName);


            SelectedArchiveFormat = ArchiveFormatComboBox.SelectedItem as string;
            SelectedCompressionLevel = CompressionLevelComboBox.SelectedItem as string;
            SelectedUpdateMode = UpdateModeComboBox.SelectedItem as string;
            SelectedFilePathsInArchive = FilePathsComboBox.SelectedItem as string;

            // Advanced
            SelectedCpuThreads = CpuThreadsComboBox.SelectedItem as string;
            if (SelectedArchiveFormat == "ZIP")
            {
                SelectedZipCompressionMethod = ZipCompressionMethodComboBox.SelectedItem as string;
            }
            else if (SelectedArchiveFormat == "7z")
            {
                SelectedSevenZipCompressionMethod = SevenZipCompressionMethodComboBox.SelectedItem as string;
                SelectedSevenZipDictionarySize = SevenZipDictionarySizeComboBox.SelectedItem as string;
                SelectedSevenZipWordSize = SevenZipWordSizeComboBox.SelectedItem as string;
                SelectedSevenZipSolidBlockSize = SevenZipSolidBlockSizeComboBox.SelectedItem as string;
            }

            // Password
            if (EnableEncryptionCheckBox.IsChecked == true)
            {
                Password = EnterPasswordBox.Password; // Store the confirmed password
                SelectedEncryptionMethod = EncryptionMethodComboBox.SelectedItem as string;
                EncryptFileNames = EncryptFileNamesCheckBox.IsChecked == true;
            }
            else
            {
                Password = null;
                SelectedEncryptionMethod = null;
                EncryptFileNames = false;
            }

            // Split/SFX
            if (VolumeSizeComboBox.SelectedItem is string selectedVolume && selectedVolume.Equals("Custom...", StringComparison.OrdinalIgnoreCase))
            {
                VolumeSize = CustomVolumeSizeTextBox.Text; // TODO: Add validation for format like "50MB"
            }
            else if (VolumeSizeComboBox.SelectedItem is string selectedPresetVolume && !selectedPresetVolume.Equals("No splitting", StringComparison.OrdinalIgnoreCase))
            {
                 VolumeSize = selectedPresetVolume; // Store the preset string e.g. "100 MB"
            } else {
                VolumeSize = null; // No splitting
            }
            CreateSfxArchive = CreateSfxArchiveCheckBox.IsChecked == true;

            // Comment
            ArchiveComment = ArchiveCommentTextBox.Text;

            Debug.WriteLine($"CreateArchiveDialog: Primary button clicked. Archive: {ArchiveFullName}, Format: {SelectedArchiveFormat}");
            // The dialog will close automatically if args.Cancel is not set to true.
            // Actual compression logic would be initiated by the caller (MainWindow) using these properties.
        }

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Debug.WriteLine("CreateArchiveDialog: Cancel button clicked.");
            // Dialog will close. No action needed unless specific cleanup is required.
        }
    }

    // Helper class for App.WindowHandle (needed for FileSavePicker)
    public static class App
    {
        public static IntPtr WindowHandle { get; set; }
    }
}
