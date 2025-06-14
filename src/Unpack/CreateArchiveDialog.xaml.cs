using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Linq;
using Unpack.Core; // For CompressionSettingsHelper

namespace Unpack
{
    public sealed partial class CreateArchiveDialog : ContentDialog
    {
        // General Tab
        public string ArchiveFullName { get; private set; }
        public string SelectedArchiveFormat { get; private set; }
        public string SelectedCompressionLevelString { get; private set; }
        public string SelectedUpdateMode { get; private set; }
        public string SelectedFilePathsInArchive { get; private set; }

        // Advanced Tab
        public string SelectedZipCompressionMethod { get; private set; }
        // 7z Specific
        public string SelectedSevenZipCompressionMethodString { get; private set; } // String from ComboBox
        public string SelectedSevenZipDictionarySize { get; private set; }
        public string SelectedSevenZipWordSize { get; private set; }
        public string SelectedSevenZipSolidBlockSize { get; private set; }
        public bool IsSolidArchive { get; private set; } // Derived from SolidBlockSize or a dedicated CheckBox
        public string SelectedCpuThreads { get; private set; }


        // Password Tab
        public string Password { get; private set; }
        public bool EncryptHeaders { get; private set; } // Renamed from EncryptFileNames for clarity with 7z
        public string SelectedEncryptionMethod { get; private set; }

        // Split/SFX Tab
        public string VolumeSize { get; private set; }
        public bool CreateSfxArchive { get; private set; }

        // Comment Tab
        public string ArchiveComment { get; private set; }

        public IEnumerable<FileSystemItem> SourceItems { get; private set; }


        public CreateArchiveDialog()
        {
            this.InitializeComponent();
            PopulateComboBoxes(); // Initial population if any
            SetDefaultSelections(); // Set initial default values for controls
            UpdateDynamicUI(); // Update UI based on initial defaults (e.g. ZIP)

            SuggestInitialArchiveName();
        }

        public void InitializeDialog(IEnumerable<FileSystemItem> itemsToArchive)
        {
            this.SourceItems = itemsToArchive;
            SuggestInitialArchiveName();
        }

        private void SuggestInitialArchiveName()
        {
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string suggestedName = "Archive";
            string currentExtension = (ArchiveFormatComboBox.SelectedItem as string)?.ToLowerInvariant() ?? "zip";

            if (this.SourceItems != null && this.SourceItems.Any())
            {
                var firstItem = this.SourceItems.First();
                string parentDir = Path.GetDirectoryName(firstItem.FullPath);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    basePath = parentDir;
                }
                else if (string.IsNullOrEmpty(parentDir) && firstItem.ItemType == "Drive") // e.g. C:\
                {
                     basePath = firstItem.FullPath; // The drive itself
                }


                if (this.SourceItems.Count() == 1)
                {
                    suggestedName = Path.GetFileNameWithoutExtension(firstItem.Name);
                    if (firstItem.ItemType == "Drive")
                    {
                        suggestedName = firstItem.Name.Split(new[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Replace(":", "").Replace("\\", "").Trim() ?? "Drive";
                    }
                }
                else
                {
                    var commonDirInfo = new DirectoryInfo(basePath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                    suggestedName = commonDirInfo.Name;
                }
            }

            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
            {
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
             if (string.IsNullOrEmpty(suggestedName)) suggestedName = "Archive";


            ArchiveNamePathTextBox.Text = Path.Combine(basePath, suggestedName + "." + currentExtension);
        }

        private void PopulateComboBoxes()
        {
            // Most ComboBox items are defined directly in XAML.
            // Dynamic ones like CompressionLevel and EncryptionMethod are populated based on format selection in UpdateDynamicUI.
        }

        private void SetDefaultSelections()
        {
            ArchiveFormatComboBox.SelectedIndex = 0; // Default to ZIP
            // UpdateDynamicUI will be called right after this, and it will set the defaults for dependent comboboxes.

            UpdateModeComboBox.SelectedIndex = 0;
            FilePathsComboBox.SelectedIndex = 0;
            CpuThreadsComboBox.SelectedValue = "Auto"; // Common advanced default
            VolumeSizeComboBox.SelectedIndex = 0; // No splitting
        }

        private void ArchiveFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDynamicUI();
            if (!string.IsNullOrWhiteSpace(ArchiveNamePathTextBox.Text))
            {
                string currentPath = ArchiveNamePathTextBox.Text;
                string currentNameOnly = Path.GetFileNameWithoutExtension(currentPath);
                string currentDir = Path.GetDirectoryName(currentPath);
                string newExtension = (ArchiveFormatComboBox.SelectedItem as string)?.ToLowerInvariant();

                if (!string.IsNullOrEmpty(newExtension) && !string.IsNullOrEmpty(currentDir))
                {
                    ArchiveNamePathTextBox.Text = Path.Combine(currentDir, currentNameOnly + "." + newExtension);
                }
                else if (!string.IsNullOrEmpty(newExtension) && string.IsNullOrEmpty(currentDir) && !string.IsNullOrEmpty(currentNameOnly))
                {
                    // Handle case where path might just be a name without directory
                     ArchiveNamePathTextBox.Text = currentNameOnly + "." + newExtension;
                }
            }
        }

        private void UpdateDynamicUI()
        {
            string selectedFormat = ArchiveFormatComboBox.SelectedItem as string;

            // Update Advanced Tab visibility and defaults
            if (selectedFormat == "7z")
            {
                AdvancedSettingsTitleTextBlock.Text = "Advanced Settings for 7z";
                ZipAdvancedOptionsPanel.Visibility = Visibility.Collapsed;
                SevenZipAdvancedOptionsPanel.Visibility = Visibility.Visible;
                // Set 7z defaults if not already set by user interaction
                if (SevenZipCompressionMethodComboBox.SelectedIndex == -1) SevenZipCompressionMethodComboBox.SelectedValue = "LZMA2";
                if (SevenZipDictionarySizeComboBox.SelectedIndex == -1) SevenZipDictionarySizeComboBox.SelectedValue = "16 MB";
                if (SevenZipWordSizeComboBox.SelectedIndex == -1) SevenZipWordSizeComboBox.SelectedValue = "64";
                if (SevenZipSolidBlockSizeComboBox.SelectedIndex == -1) SevenZipSolidBlockSizeComboBox.SelectedValue = "Off (non-solid)";
            }
            else // ZIP
            {
                AdvancedSettingsTitleTextBlock.Text = "Advanced Settings for ZIP";
                ZipAdvancedOptionsPanel.Visibility = Visibility.Visible;
                SevenZipAdvancedOptionsPanel.Visibility = Visibility.Collapsed;
                if (ZipCompressionMethodComboBox.SelectedIndex == -1) ZipCompressionMethodComboBox.SelectedValue = "Deflate";
            }

            // Update Compression Levels ComboBox
            var currentCompressionLevel = CompressionLevelComboBox.SelectedValue as string;
            CompressionLevelComboBox.Items.Clear();
            var levels = new List<string> { "Store (no compression)", "Fastest", "Fast", "Normal", "Maximum", "Ultra" };
            foreach (var level in levels) CompressionLevelComboBox.Items.Add(level);

            if (levels.Contains(currentCompressionLevel)) CompressionLevelComboBox.SelectedValue = currentCompressionLevel;
            else CompressionLevelComboBox.SelectedValue = "Normal";

            // Update Encryption Methods ComboBox and EncryptFileNames CheckBox
            var currentEncryptionMethod = EncryptionMethodComboBox.SelectedItem as string;
            EncryptionMethodComboBox.Items.Clear();
            if (selectedFormat == "7z")
            {
                EncryptionMethodComboBox.Items.Add("AES-256");
                EncryptFileNamesCheckBox.Content = "Encrypt file names";
            }
            else // ZIP
            {
                EncryptionMethodComboBox.Items.Add("AES-256");
                EncryptionMethodComboBox.Items.Add("ZipCrypto (legacy)");
                EncryptFileNamesCheckBox.Content = "Encrypt file names (requires compatible ZIP tool)";
            }
            // Try to restore previous selection or set default
            if (EncryptionMethodComboBox.Items.Contains(currentEncryptionMethod)) EncryptionMethodComboBox.SelectedItem = currentEncryptionMethod;
            else EncryptionMethodComboBox.SelectedIndex = 0; // Default to AES-256

            UpdatePasswordControlsState();
            CreateSfxArchiveCheckBox.IsEnabled = true;
        }

        private void EnableEncryptionCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePasswordControlsState();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ValidatePasswords();
            UpdatePasswordControlsState();
        }

        private void ValidatePasswords()
        {
            if (EnableEncryptionCheckBox.IsChecked == true && EnterPasswordBox.Password != ReEnterPasswordBox.Password)
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
            Debug.WriteLine($"ShowPasswordCheckBox toggled. (Actual reveal not implemented for standard PasswordBox)");
        }

        private void UpdatePasswordControlsState()
        {
            bool encryptionEnabled = EnableEncryptionCheckBox.IsChecked == true;
            EnterPasswordBox.IsEnabled = encryptionEnabled;
            ReEnterPasswordBox.IsEnabled = encryptionEnabled;
            ShowPasswordCheckBox.IsEnabled = encryptionEnabled;
            EncryptionMethodComboBox.IsEnabled = encryptionEnabled;

            bool canEncryptFileNames = encryptionEnabled &&
                                       (ArchiveFormatComboBox.SelectedItem as string == "7z") && // Primarily a 7z feature
                                       !string.IsNullOrEmpty(EnterPasswordBox.Password) &&
                                       EnterPasswordBox.Password == ReEnterPasswordBox.Password;
            EncryptFileNamesCheckBox.IsEnabled = canEncryptFileNames;

            if (!encryptionEnabled)
            {
                EnterPasswordBox.Password = "";
                ReEnterPasswordBox.Password = "";
                PasswordMatchErrorTextBlock.Visibility = Visibility.Collapsed;
                if (!canEncryptFileNames) EncryptFileNamesCheckBox.IsChecked = false; // Only uncheck if it's disabled due to other reasons
            }
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker savePicker = new FileSavePicker();

            if (App.WindowHandle != IntPtr.Zero) {
                 InitializeWithWindow.Initialize(savePicker, App.WindowHandle);
            } else if (this.XamlRoot != null && this.XamlRoot.Content != null) {
                 InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(this.XamlRoot.Content));
            } else {
                Debug.WriteLine("Window handle not found for FileSavePicker.");
                return;
            }

            string selectedFormatExtension = (ArchiveFormatComboBox.SelectedItem as string)?.ToLowerInvariant() ?? "zip";
            savePicker.FileTypeChoices.Add(selectedFormatExtension.ToUpper() + " Archive", new List<string>() { "." + selectedFormatExtension });
            savePicker.DefaultFileExtension = "." + selectedFormatExtension;

            string currentFullPath = ArchiveNamePathTextBox.Text;
            try
            {
                savePicker.SuggestedFileName = string.IsNullOrWhiteSpace(currentFullPath) ? "Archive" : Path.GetFileNameWithoutExtension(currentFullPath);
                string initialDir = string.IsNullOrWhiteSpace(currentFullPath) ? null : Path.GetDirectoryName(currentFullPath);

                if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
                {
                    savePicker.SuggestedStartLocation = await StorageFolder.GetFolderFromPathAsync(initialDir);
                } else {
                    savePicker.SuggestedStartLocation = await KnownFolders.GetFolderForUserAsync(null, KnownFolderId.Documents);
                }
            }
            catch(Exception ex) {
                Debug.WriteLine($"Error setting up SavePicker: {ex.Message}");
                savePicker.SuggestedStartLocation = await KnownFolders.GetFolderForUserAsync(null, KnownFolderId.Documents);
                savePicker.SuggestedFileName = "Archive"; // Fallback name
            }

            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                ArchiveNamePathTextBox.Text = file.Path;
            }
        }

        private void CustomVolumeSizeTextBox_TextChanged(object sender, TextChangedEventArgs e) { /* Validation can be added */ }

        private void VolumeSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VolumeSizeComboBox.SelectedItem is string selectedSize && selectedSize.Equals("Custom...", StringComparison.OrdinalIgnoreCase))
            {
                CustomVolumeSizeTextBox.Visibility = Visibility.Visible;
                CustomVolumeSizeTextBox.Focus(FocusState.Programmatic);
            }
            else
            {
                CustomVolumeSizeTextBox.Visibility = Visibility.Collapsed;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(ArchiveNamePathTextBox.Text))
            {
                args.Cancel = true;
                ShowValidationFailedDialog("Archive path cannot be empty.");
                ArchiveNamePathTextBox.Focus(FocusState.Programmatic);
                return;
            }
            // Further path validation can be added here if needed

            if (EnableEncryptionCheckBox.IsChecked == true)
            {
                if (string.IsNullOrEmpty(EnterPasswordBox.Password))
                {
                    args.Cancel = true;
                    PasswordMatchErrorTextBlock.Text = "Password cannot be empty if encryption is enabled.";
                    PasswordMatchErrorTextBlock.Visibility = Visibility.Visible;
                    MainTabView.SelectedItem = MainTabView.TabItems.Cast<TabViewItem>().FirstOrDefault(t => (t.Header as string) == "Password");
                    EnterPasswordBox.Focus(FocusState.Programmatic);
                    return;
                }
                if (EnterPasswordBox.Password != ReEnterPasswordBox.Password)
                {
                    args.Cancel = true;
                    PasswordMatchErrorTextBlock.Text = "Passwords do not match.";
                    PasswordMatchErrorTextBlock.Visibility = Visibility.Visible;
                    MainTabView.SelectedItem = MainTabView.TabItems.Cast<TabViewItem>().FirstOrDefault(t => (t.Header as string) == "Password");
                    ReEnterPasswordBox.Focus(FocusState.Programmatic);
                    return;
                }
            }
            PasswordMatchErrorTextBlock.Visibility = Visibility.Collapsed;

            ArchiveFullName = ArchiveNamePathTextBox.Text;
            OutputFolderPath = Path.GetDirectoryName(ArchiveFullName);

            SelectedArchiveFormat = ArchiveFormatComboBox.SelectedItem as string;
            SelectedCompressionLevelString = CompressionLevelComboBox.SelectedItem as string;
            SelectedUpdateMode = UpdateModeComboBox.SelectedItem as string;
            SelectedFilePathsInArchive = FilePathsComboBox.SelectedItem as string;

            SelectedCpuThreads = CpuThreadsComboBox.SelectedItem as string;
            if (SelectedArchiveFormat == "ZIP")
            {
                SelectedZipCompressionMethod = ZipCompressionMethodComboBox.SelectedItem as string;
            }
            else if (SelectedArchiveFormat == "7z")
            {
                SelectedSevenZipCompressionMethodString = SevenZipCompressionMethodComboBox.SelectedItem as string; // Store string
                SelectedSevenZipDictionarySize = SevenZipDictionarySizeComboBox.SelectedItem as string;
                SelectedSevenZipWordSize = SevenZipWordSizeComboBox.SelectedItem as string;
                SelectedSevenZipSolidBlockSize = SevenZipSolidBlockSizeComboBox.SelectedItem as string;
                // IsSolidArchive could be a dedicated CheckBox or derived from SolidBlockSize string
                IsSolidArchive = SelectedSevenZipSolidBlockSize != "Off (non-solid)";
            }

            if (EnableEncryptionCheckBox.IsChecked == true)
            {
                Password = EnterPasswordBox.Password;
                SelectedEncryptionMethod = EncryptionMethodComboBox.SelectedItem as string;
                EncryptHeaders = EncryptFileNamesCheckBox.IsChecked == true; // For 7z, this is header encryption
            }
            else
            {
                Password = null;
                SelectedEncryptionMethod = null;
                EncryptHeaders = false;
            }

            if (VolumeSizeComboBox.SelectedItem is string selectedVolume && selectedVolume.Equals("Custom...", StringComparison.OrdinalIgnoreCase))
            {
                VolumeSize = CustomVolumeSizeTextBox.Text;
            }
            else if (VolumeSizeComboBox.SelectedItem is string selectedPresetVolume && !selectedPresetVolume.Equals("No splitting", StringComparison.OrdinalIgnoreCase))
            {
                 VolumeSize = selectedPresetVolume;
            } else {
                VolumeSize = null;
            }
            CreateSfxArchive = CreateSfxArchiveCheckBox.IsChecked == true;
            ArchiveComment = ArchiveCommentTextBox.Text;

            Debug.WriteLine($"CreateArchiveDialog: Primary button clicked. Archive: {ArchiveFullName}, Format: {SelectedArchiveFormat}, Level: {SelectedCompressionLevelString}");
        }

        private async void ShowValidationFailedDialog(string message)
        {
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "Validation Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Debug.WriteLine("CreateArchiveDialog: Cancel button clicked.");
        }
    }
}
