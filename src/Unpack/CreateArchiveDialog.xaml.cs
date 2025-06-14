using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Linq;
using Unpack.Core;
using System.Text.RegularExpressions; // For ParseVolumeSize

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
        public string SelectedSevenZipCompressionMethodString { get; private set; }
        public string SelectedSevenZipDictionarySize { get; private set; }
        public string SelectedSevenZipWordSize { get; private set; }
        public string SelectedSevenZipSolidBlockSize { get; private set; }
        public bool IsSolidArchive { get; private set; }
        public string SelectedCpuThreads { get; private set; }

        // Password Tab
        public string Password { get; private set; }
        public bool EncryptHeaders { get; private set; }
        public string SelectedEncryptionMethod { get; private set; }

        // Split/SFX Tab
        public string VolumeSizeString { get; private set; } // Raw string from UI e.g. "100 MB", "Custom...", or custom input "50MB"
        public bool CreateSfxArchive { get; private set; }

        // Comment Tab
        public string ArchiveComment { get; private set; }

        public IEnumerable<FileSystemItem> SourceItems { get; private set; }

        public CreateArchiveDialog()
        {
            this.InitializeComponent();
            SetDefaultSelections();
            UpdateDynamicUI();
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
            string currentExtension = GetCurrentTargetExtension();


            if (this.SourceItems != null && this.SourceItems.Any())
            {
                var firstItem = this.SourceItems.First();
                string parentDir = Path.GetDirectoryName(firstItem.FullPath);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                { basePath = parentDir; }
                else if (string.IsNullOrEmpty(parentDir) && firstItem.ItemType == "Drive")
                { basePath = firstItem.FullPath; }

                if (this.SourceItems.Count() == 1)
                {
                    suggestedName = Path.GetFileNameWithoutExtension(firstItem.Name);
                    if (firstItem.ItemType == "Drive")
                    { suggestedName = firstItem.Name.Split(new[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Replace(":", "").Replace("\\", "").Trim() ?? "Drive"; }
                }
                else
                {
                    var commonDirInfo = new DirectoryInfo(basePath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                    suggestedName = commonDirInfo.Name;
                }
            }

            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
            { basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); }
            if (string.IsNullOrEmpty(suggestedName)) suggestedName = "Archive";

            ArchiveNamePathTextBox.Text = Path.Combine(basePath, suggestedName + currentExtension);
        }

        private string GetCurrentTargetExtension()
        {
            string format = ArchiveFormatComboBox.SelectedItem as string ?? "ZIP";
            bool sfx = CreateSfxArchiveCheckBox.IsChecked == true;
            if (sfx) return ".exe";
            return "." + format.ToLowerInvariant();
        }


        private void SetDefaultSelections()
        {
            ArchiveFormatComboBox.SelectedIndex = 0;
            UpdateModeComboBox.SelectedIndex = 0;
            FilePathsComboBox.SelectedIndex = 0;
            CpuThreadsComboBox.SelectedValue = "Auto";
            VolumeSizeComboBox.SelectedIndex = 0;
            CreateSfxArchiveCheckBox.IsChecked = false;
        }

        private void ArchiveFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDynamicUI();
            UpdateArchiveNameExtension();
        }

        private void CreateSfxArchiveCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateArchiveNameExtension();
        }

        private void UpdateArchiveNameExtension()
        {
            if (!string.IsNullOrWhiteSpace(ArchiveNamePathTextBox.Text))
            {
                string currentPath = ArchiveNamePathTextBox.Text;
                string currentNameOnly = Path.GetFileNameWithoutExtension(currentPath);
                // If current extension is .exe, and SFX is unchecked, it might have been an SFX name.
                // Or if it was .zip, and SFX is checked, change to .exe.
                string currentActualExtension = Path.GetExtension(currentPath)?.ToLowerInvariant();

                string currentDir = Path.GetDirectoryName(currentPath);
                string newExtension = GetCurrentTargetExtension(); // .zip, .7z, or .exe

                if (string.IsNullOrEmpty(currentNameOnly) && !string.IsNullOrEmpty(currentDir))
                {
                    // If path was "C:\folder\.zip", make it "C:\folder\folder.newExt"
                    currentNameOnly = new DirectoryInfo(currentDir).Name;
                }
                else if (string.IsNullOrEmpty(currentNameOnly))
                {
                    currentNameOnly = "Archive"; // Fallback
                }


                // Avoid changing extension if it's not related to archive type (e.g. user typed "MyBackup.part1.exe")
                // This logic is simple: if it was an archive type or .exe, and target changes, update it.
                List<string> knownArchiveExtensions = new List<string>{ ".zip", ".7z", ".exe" };
                if (knownArchiveExtensions.Contains(currentActualExtension) || string.IsNullOrEmpty(currentActualExtension))
                {
                     ArchiveNamePathTextBox.Text = Path.Combine(currentDir ?? "", currentNameOnly + newExtension);
                }
            }
        }

        private void UpdateDynamicUI()
        {
            string selectedFormat = ArchiveFormatComboBox.SelectedItem as string;

            if (selectedFormat == "7z")
            {
                AdvancedSettingsTitleTextBlock.Text = "Advanced Settings for 7z";
                ZipAdvancedOptionsPanel.Visibility = Visibility.Collapsed;
                SevenZipAdvancedOptionsPanel.Visibility = Visibility.Visible;
                if (SevenZipCompressionMethodComboBox.SelectedIndex == -1) SevenZipCompressionMethodComboBox.SelectedItem = "LZMA2";
                if (SevenZipDictionarySizeComboBox.SelectedIndex == -1) SevenZipDictionarySizeComboBox.SelectedItem = "16 MB";
                if (SevenZipWordSizeComboBox.SelectedIndex == -1) SevenZipWordSizeComboBox.SelectedItem = "64";
                if (SevenZipSolidBlockSizeComboBox.SelectedIndex == -1) SevenZipSolidBlockSizeComboBox.SelectedItem = "Off (non-solid)";
            }
            else // ZIP
            {
                AdvancedSettingsTitleTextBlock.Text = "Advanced Settings for ZIP";
                ZipAdvancedOptionsPanel.Visibility = Visibility.Visible;
                SevenZipAdvancedOptionsPanel.Visibility = Visibility.Collapsed;
                if (ZipCompressionMethodComboBox.SelectedIndex == -1) ZipCompressionMethodComboBox.SelectedItem = "Deflate";
            }

            var currentCompressionLevel = CompressionLevelComboBox.SelectedItem as string;
            CompressionLevelComboBox.Items.Clear();
            var levels = new List<string> { "Store (no compression)", "Fastest", "Fast", "Normal", "Maximum", "Ultra" };
            foreach (var level in levels) CompressionLevelComboBox.Items.Add(level);
            if (levels.Contains(currentCompressionLevel)) CompressionLevelComboBox.SelectedItem = currentCompressionLevel;
            else CompressionLevelComboBox.SelectedItem = "Normal";

            var currentEncryptionMethod = EncryptionMethodComboBox.SelectedItem as string;
            EncryptionMethodComboBox.Items.Clear();
            if (selectedFormat == "7z")
            {
                EncryptionMethodComboBox.Items.Add("AES-256");
                EncryptFileNamesCheckBox.Content = "Encrypt file names (headers)";
            }
            else // ZIP
            {
                EncryptionMethodComboBox.Items.Add("AES-256");
                EncryptionMethodComboBox.Items.Add("ZipCrypto (legacy)");
                EncryptFileNamesCheckBox.Content = "Encrypt file names (ZIP AES - limited support)";
            }
            if (EncryptionMethodComboBox.Items.Contains(currentEncryptionMethod)) EncryptionMethodComboBox.SelectedItem = currentEncryptionMethod;
            else EncryptionMethodComboBox.SelectedIndex = 0;

            UpdatePasswordControlsState();
            CreateSfxArchiveCheckBox.IsEnabled = true;
        }

        private void EnableEncryptionCheckBox_Changed(object sender, RoutedEventArgs e) { UpdatePasswordControlsState(); }
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) { ValidatePasswords(); UpdatePasswordControlsState(); }

        private void ValidatePasswords()
        {
            if (EnableEncryptionCheckBox.IsChecked == true && EnterPasswordBox.Password != ReEnterPasswordBox.Password)
            { PasswordMatchErrorTextBlock.Visibility = Visibility.Visible; PasswordMatchErrorTextBlock.Text = "Passwords do not match."; }
            else { PasswordMatchErrorTextBlock.Visibility = Visibility.Collapsed; }
        }

        private void ShowPasswordCheckBox_Changed(object sender, RoutedEventArgs e) { Debug.WriteLine($"ShowPasswordCheckBox toggled. (Reveal not implemented)"); }

        private void UpdatePasswordControlsState()
        {
            bool encryptionEnabled = EnableEncryptionCheckBox.IsChecked == true;
            EnterPasswordBox.IsEnabled = encryptionEnabled; ReEnterPasswordBox.IsEnabled = encryptionEnabled;
            ShowPasswordCheckBox.IsEnabled = encryptionEnabled; EncryptionMethodComboBox.IsEnabled = encryptionEnabled;

            bool canEncryptFileNames = false;
            if (encryptionEnabled && !string.IsNullOrEmpty(EnterPasswordBox.Password) && EnterPasswordBox.Password == ReEnterPasswordBox.Password)
            {
                string selectedFormat = ArchiveFormatComboBox.SelectedItem as string;
                if (selectedFormat == "7z") canEncryptFileNames = true;
                else if (selectedFormat == "ZIP")
                {
                    canEncryptFileNames = (EncryptionMethodComboBox.SelectedItem as string)?.ToUpperInvariant().Contains("AES") == true;
                    EncryptFileNamesCheckBox.Content = canEncryptFileNames ? "Encrypt file names (ZIP AES - limited support)" : "Encrypt file names (N/A for ZipCrypto)";
                }
            }
            EncryptFileNamesCheckBox.IsEnabled = canEncryptFileNames;
            if (!encryptionEnabled)
            {
                EnterPasswordBox.Password = ""; ReEnterPasswordBox.Password = "";
                PasswordMatchErrorTextBlock.Visibility = Visibility.Collapsed;
                if (!canEncryptFileNames) EncryptFileNamesCheckBox.IsChecked = false;
            }
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker savePicker = new FileSavePicker();
            IntPtr hwnd = App.WindowHandle != IntPtr.Zero ? App.WindowHandle : WindowNative.GetWindowHandle(this.XamlRoot.Content);
            if (hwnd == IntPtr.Zero) { Debug.WriteLine("Window handle not found for FileSavePicker."); return; }
            InitializeWithWindow.Initialize(savePicker, hwnd);

            string targetExtension = GetCurrentTargetExtension(); // .zip, .7z or .exe
            string formatName = (ArchiveFormatComboBox.SelectedItem as string ?? "ZIP").ToUpper();
            if (CreateSfxArchiveCheckBox.IsChecked == true) formatName += " SFX";

            savePicker.FileTypeChoices.Add($"{formatName} Archive", new List<string>() { targetExtension });
            savePicker.DefaultFileExtension = targetExtension;

            string currentFullPath = ArchiveNamePathTextBox.Text;
            try
            {
                savePicker.SuggestedFileName = string.IsNullOrWhiteSpace(currentFullPath) ? "Archive" : Path.GetFileNameWithoutExtension(currentFullPath);
                string initialDir = string.IsNullOrWhiteSpace(currentFullPath) ? null : Path.GetDirectoryName(currentFullPath);
                if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
                { savePicker.SuggestedStartLocation = await StorageFolder.GetFolderFromPathAsync(initialDir); }
                else { savePicker.SuggestedStartLocation = await KnownFolders.GetFolderForUserAsync(null, KnownFolderId.Documents); }
            }
            catch(Exception ex)
            { Debug.WriteLine($"Error setting up SavePicker: {ex.Message}"); savePicker.SuggestedStartLocation = await KnownFolders.GetFolderForUserAsync(null, KnownFolderId.Documents); savePicker.SuggestedFileName = "Archive"; }

            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null) ArchiveNamePathTextBox.Text = file.Path;
        }

        private void CustomVolumeSizeTextBox_TextChanged(object sender, TextChangedEventArgs e) { /* Validation can be added */ }
        private void VolumeSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VolumeSizeComboBox.SelectedItem is string selectedSize && selectedSize.Equals("Custom...", StringComparison.OrdinalIgnoreCase))
            { CustomVolumeSizeTextBox.Visibility = Visibility.Visible; CustomVolumeSizeTextBox.Focus(FocusState.Programmatic); }
            else { CustomVolumeSizeTextBox.Visibility = Visibility.Collapsed; }
        }

        public long? ParseVolumeSizeStringToBytes(string sizeString) // Made public for MainWindow to use
        {
            if (string.IsNullOrWhiteSpace(sizeString) || sizeString.Equals("No splitting", StringComparison.OrdinalIgnoreCase)) return null;

            var match = Regex.Match(sizeString, @"(\d+\.?\d*)\s*(KB|MB|GB|B)?", RegexOptions.IgnoreCase);
            if (!match.Success) return null; // Or throw FormatException

            if (!double.TryParse(match.Groups[1].Value, out double num)) return null; // Or throw

            string unit = match.Groups[2].Value.ToUpperInvariant();
            switch (unit)
            {
                case "KB": return (long)(num * 1024);
                case "MB": return (long)(num * 1024 * 1024);
                case "GB": return (long)(num * 1024 * 1024 * 1024);
                case "B": return (long)num;
                case "": // Assume bytes if no unit, or it's a preset like "1.44 MB (Floppy)"
                    if (sizeString.Contains("Floppy")) return 1440 * 1024; // Approx
                    if (sizeString.Contains("CD")) return (sizeString.Contains("700") ? 700 : 650) * 1024 * 1024;
                    if (sizeString.Contains("DVD")) return (long)(4480 * 1024 * 1024); // Approx DVD5
                    if (sizeString.Contains("FAT32")) return (long)(4L * 1024 * 1024 * 1024 - 1); // Approx 4GB-1
                    // If it's just a number from custom input without units, treat as bytes
                    if (long.TryParse(sizeString, out long bytesOnly)) return bytesOnly;
                    return null; // Cannot parse
                default: return (long)num; // If only number was parsed, assume bytes
            }
        }


        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Basic Validation
            if (string.IsNullOrWhiteSpace(ArchiveNamePathTextBox.Text))
            { args.Cancel = true; ShowValidationFailedDialog("Archive path cannot be empty."); ArchiveNamePathTextBox.Focus(FocusState.Programmatic); return; }

            if (EnableEncryptionCheckBox.IsChecked == true)
            {
                if (string.IsNullOrEmpty(EnterPasswordBox.Password))
                { args.Cancel = true; PasswordMatchErrorTextBlock.Text = "Password cannot be empty."; PasswordMatchErrorTextBlock.Visibility = Visibility.Visible; MainTabView.SelectedItem = MainTabView.TabItems.Cast<TabViewItem>().FirstOrDefault(t => (t.Header as string) == "Password"); EnterPasswordBox.Focus(FocusState.Programmatic); return; }
                if (EnterPasswordBox.Password != ReEnterPasswordBox.Password)
                { args.Cancel = true; PasswordMatchErrorTextBlock.Text = "Passwords do not match."; PasswordMatchErrorTextBlock.Visibility = Visibility.Visible; MainTabView.SelectedItem = MainTabView.TabItems.Cast<TabViewItem>().FirstOrDefault(t => (t.Header as string) == "Password"); ReEnterPasswordBox.Focus(FocusState.Programmatic); return; }
            }
            PasswordMatchErrorTextBlock.Visibility = Visibility.Collapsed;

            // Populate properties
            ArchiveFullName = ArchiveNamePathTextBox.Text;
            OutputFolderPath = Path.GetDirectoryName(ArchiveFullName);
            SelectedArchiveFormat = ArchiveFormatComboBox.SelectedItem as string;
            SelectedCompressionLevelString = CompressionLevelComboBox.SelectedItem as string;
            SelectedUpdateMode = UpdateModeComboBox.SelectedItem as string;
            SelectedFilePathsInArchive = FilePathsComboBox.SelectedItem as string;
            SelectedCpuThreads = CpuThreadsComboBox.SelectedItem as string;

            if (SelectedArchiveFormat == "ZIP") { SelectedZipCompressionMethod = ZipCompressionMethodComboBox.SelectedItem as string; }
            else if (SelectedArchiveFormat == "7z")
            {
                SelectedSevenZipCompressionMethodString = SevenZipCompressionMethodComboBox.SelectedItem as string;
                SelectedSevenZipDictionarySize = SevenZipDictionarySizeComboBox.SelectedItem as string;
                SelectedSevenZipWordSize = SevenZipWordSizeComboBox.SelectedItem as string;
                SelectedSevenZipSolidBlockSize = SevenZipSolidBlockSizeComboBox.SelectedItem as string;
                IsSolidArchive = SelectedSevenZipSolidBlockSize != "Off (non-solid)";
            }

            if (EnableEncryptionCheckBox.IsChecked == true)
            {
                Password = EnterPasswordBox.Password;
                SelectedEncryptionMethod = EncryptionMethodComboBox.SelectedItem as string;
                EncryptHeaders = EncryptFileNamesCheckBox.IsChecked == true;
            }
            else { Password = null; SelectedEncryptionMethod = null; EncryptHeaders = false; }

            if (VolumeSizeComboBox.SelectedItem is string selectedVolume && selectedVolume.Equals("Custom...", StringComparison.OrdinalIgnoreCase))
            { VolumeSizeString = CustomVolumeSizeTextBox.Text; }
            else if (VolumeSizeComboBox.SelectedItem is string selectedPresetVolume && !selectedPresetVolume.Equals("No splitting", StringComparison.OrdinalIgnoreCase))
            { VolumeSizeString = selectedPresetVolume; }
            else { VolumeSizeString = null; } // Represents "No splitting"

            CreateSfxArchive = CreateSfxArchiveCheckBox.IsChecked == true;
            ArchiveComment = ArchiveCommentTextBox.Text;

            Debug.WriteLine($"CreateArchiveDialog: Primary. Archive: {ArchiveFullName}, Format: {SelectedArchiveFormat}, SFX: {CreateSfxArchive}, Vol: {VolumeSizeString}");
        }

        private async void ShowValidationFailedDialog(string message)
        {
            ContentDialog errorDialog = new ContentDialog { Title = "Validation Error", Content = message, CloseButtonText = "OK", XamlRoot = this.XamlRoot };
            await errorDialog.ShowAsync();
        }
        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) { Debug.WriteLine("CreateArchiveDialog: Cancel clicked."); }
    }
}
