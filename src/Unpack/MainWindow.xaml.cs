using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unpack.Core;
using SharpCompress.Compressors.Deflate;
using WinRT.Interop;
using SharpCompress.Common;

namespace Unpack
{
    public sealed partial class MainWindow : Window
    {
        public ObservableCollection<FileSystemItem> CurrentItems { get; set; } = new ObservableCollection<FileSystemItem>();
        private string _currentPath;
        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                _currentPath = value;
                PathTextBox.Text = _currentPath ?? "";
            }
        }

        private CompressionService _compressionService = new CompressionService();

        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Unpack";
            // App.WindowHandle is now set in App.xaml.cs after m_window is created and before Activate.
            // If needed here for some reason before Activate, it might be an issue, but typically dialogs
            // are shown after activation or from user interaction.

            FileListView.ItemsSource = CurrentItems;
            LoadInitialDirectory();

            MainContentGrid.AllowDrop = true;
            MainContentGrid.DragEnter += OnDragEnterMainGrid;
            MainContentGrid.DragOver += OnDragOverMainGrid;
            MainContentGrid.DragLeave += OnDragLeaveMainGrid;
            MainContentGrid.Drop += OnDropMainGrid;

            this.Loaded += MainWindow_Loaded; // Check for CLI args after window is loaded
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (App.QueuedFilePathsForUI != null && App.QueuedFilePathsForUI.Any())
            {
                Debug.WriteLine("MainWindow_Loaded: Detected queued file paths from CLI for 'Add to Archive' UI.");
                await LaunchCreateArchiveDialogWithFilePathsAsync(App.QueuedFilePathsForUI);
                App.QueuedFilePathsForUI = null; // Clear after processing
            }
        }

        public async Task LaunchCreateArchiveDialogWithFilePathsAsync(IEnumerable<string> filePaths)
        {
            if (filePaths == null || !filePaths.Any()) return;

            var fileSystemItems = new List<FileSystemItem>();
            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    fileSystemItems.Add(new FileSystemItem(fileInfo.Name, fileInfo.FullName,
                        (string.IsNullOrEmpty(fileInfo.Extension) ? "File" : fileInfo.Extension.TrimStart('.').ToUpperInvariant() + " File"),
                        "\xE7C3", fileInfo.LastWriteTimeUtc, fileInfo.Length));
                }
                else if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    fileSystemItems.Add(new FileSystemItem(dirInfo.Name, dirInfo.FullName,
                        "Folder", "\xE8D7", dirInfo.LastWriteTimeUtc, 0));
                }
                else
                {
                    Debug.WriteLine($"LaunchCreateArchiveDialogWithFilePathsAsync: Path not found or invalid: {path}");
                    // Optionally skip or add a placeholder error item
                }
            }

            if (!fileSystemItems.Any())
            {
                await ShowMessageDialog("No Valid Items", "Could not process the selected items for archiving.");
                return;
            }

            var dialog = new CreateArchiveDialog();
            dialog.InitializeDialog(fileSystemItems); // Pass items for context
            dialog.XamlRoot = this.Content.XamlRoot;

            ContentDialogResult result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // Use the original string paths for the compression service
                await ProcessArchiveCreation(dialog, filePaths.ToList());
            }
            else
            {
                Debug.WriteLine("CreateArchiveDialog (from CLI path) was cancelled or closed.");
            }
        }


        private async void LoadInitialDirectory()
        {
            try
            {
                StorageFolder documentsFolder = await KnownFolders.GetFolderForUserAsync(null, KnownFolderId.Documents);
                if (documentsFolder != null)
                {
                    LoadDirectory(documentsFolder.Path);
                }
                else
                {
                    LoadDrives();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading initial directory: {ex.Message}");
                LoadDrives();
            }
        }

        private void LoadDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                LoadDrives();
                return;
            }

            string adjustedPath = path;
            if (path.Length == 2 && path.EndsWith(":")) adjustedPath += "\\";

            if (adjustedPath.Length == 3 && adjustedPath.EndsWith(":\\"))
            {
                DriveInfo driveInfo = DriveInfo.GetDrives().FirstOrDefault(d => d.Name.Equals(adjustedPath, StringComparison.OrdinalIgnoreCase));
                if (driveInfo == null || !driveInfo.IsReady)
                {
                    Debug.WriteLine($"Drive not ready or does not exist: {adjustedPath}");
                    ShowMessageDialog("Navigation Error", $"Drive {adjustedPath} is not ready or accessible.");
                    LoadDrives();
                    CurrentPath = adjustedPath;
                    return;
                }
            }
            else if (!Directory.Exists(adjustedPath))
            {
                Debug.WriteLine($"Directory does not exist: {adjustedPath}");
                ShowMessageDialog("Navigation Error", $"The directory '{adjustedPath}' does not exist or is not accessible.");
                LoadDrives();
                CurrentPath = adjustedPath;
                return;
            }

            CurrentPath = adjustedPath;
            CurrentItems.Clear();
            DragDropHintText.Visibility = Visibility.Collapsed;

            try
            {
                var directories = Directory.GetDirectories(adjustedPath);
                foreach (var dirPath in directories)
                {
                    var dirInfo = new DirectoryInfo(dirPath);
                    CurrentItems.Add(new FileSystemItem(
                        dirInfo.Name,
                        dirInfo.FullName,
                        "Folder",
                        "\xE8D7",
                        dirInfo.LastWriteTimeUtc, 0));
                }

                var files = Directory.GetFiles(adjustedPath);
                foreach (var filePath in files)
                {
                    var fileInfo = new FileInfo(filePath);
                    CurrentItems.Add(new FileSystemItem(
                        fileInfo.Name,
                        fileInfo.FullName,
                        (string.IsNullOrEmpty(fileInfo.Extension) ? "File" : fileInfo.Extension.TrimStart('.').ToUpperInvariant() + " File"),
                        "\xE7C3",
                        fileInfo.LastWriteTimeUtc,
                        fileInfo.Length));
                }
                if (!CurrentItems.Any())
                {
                   DragDropHintText.Text = "This folder is empty. Drag files here or navigate elsewhere.";
                   DragDropHintText.Visibility = Visibility.Visible;
                }
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine($"Access denied to directory: {adjustedPath}");
                ShowMessageDialog("Access Denied", $"Access to the directory '{Path.GetFileName(adjustedPath)}' was denied.");
                UpButton_Click(null, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading directory: {adjustedPath} - {ex.Message}");
                ShowMessageDialog("Error Loading Directory", $"Could not load directory '{Path.GetFileName(adjustedPath)}'. Reason: {ex.Message}");
            }
        }

        private void LoadDrives()
        {
            CurrentPath = "";
            PathTextBox.Text = "Computer";
            CurrentItems.Clear();
            DragDropHintText.Visibility = Visibility.Collapsed;
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    if (drive.IsReady)
                    {
                        CurrentItems.Add(new FileSystemItem(
                            string.IsNullOrEmpty(drive.VolumeLabel) ? drive.Name.Replace("\\","") : $"{drive.VolumeLabel} ({drive.Name.Replace("\\","")})",
                            drive.Name,
                            "Drive",
                            "\xE8A7",
                            DateTimeOffset.MinValue,
                            drive.TotalSize
                        ));
                    }
                }
                 if (!CurrentItems.Any())
                {
                   DragDropHintText.Text = "No drives found or accessible.";
                   DragDropHintText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading drives: {ex.Message}");
                ShowMessageDialog("Error Loading Drives", $"Could not retrieve drive information. Reason: {ex.Message}");
            }
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            string pathToNavigate = PathTextBox.Text;
            if (pathToNavigate.Equals("Computer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(CurrentPath))
            {
                return;
            }

            if (!string.IsNullOrEmpty(CurrentPath) && pathToNavigate != CurrentPath)
            {
                try {
                    string potentialParentFromTextBox = Path.GetDirectoryName(pathToNavigate);
                    if (!string.IsNullOrEmpty(potentialParentFromTextBox) && (Directory.Exists(potentialParentFromTextBox) || (potentialParentFromTextBox.Length == 2 && potentialParentFromTextBox.EndsWith(":"))))
                    {
                        LoadDirectory(potentialParentFromTextBox);
                        return;
                    } else if (string.IsNullOrEmpty(potentialParentFromTextBox)) {
                        LoadDrives();
                        return;
                    }
                } catch {}
            }

            if (!string.IsNullOrEmpty(CurrentPath))
            {
                var parentDir = Path.GetDirectoryName(CurrentPath);
                if (parentDir != null)
                {
                    LoadDirectory(parentDir);
                }
                else
                {
                    LoadDrives();
                }
            }
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            string targetPath = PathTextBox.Text;
            if (targetPath.Equals("Computer", StringComparison.OrdinalIgnoreCase) ||
                targetPath.Equals("This PC", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(targetPath))
            {
                LoadDrives();
            }
            else
            {
                LoadDirectory(targetPath);
            }
        }

        private void PathTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                GoButton_Click(sender, e);
            }
        }

        private void FileListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileSystemItem selectedItem)
            {
                if (selectedItem.ItemType == "Folder" || selectedItem.ItemType == "Drive")
                {
                    LoadDirectory(selectedItem.FullPath);
                }
                else if (selectedItem.ItemType != "Error")
                {
                    Debug.WriteLine($"File double-tapped: {selectedItem.FullPath}. Checking if it's an archive.");
                    string fileExtension = Path.GetExtension(selectedItem.FullPath).ToLowerInvariant();
                    if (fileExtension == ".zip" || fileExtension == ".7z")
                    {
                        ShowMessageDialog("Open Archive", $"'{selectedItem.Name}' would be opened in Unpack here. (Not yet implemented)");
                    }
                }
            }
        }

        private void OnDragEnterMainGrid(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                DragDropHintText.Text = "Drop files/folders to add to archive or open";
                DragDropHintText.Visibility = Visibility.Visible;
            } else { e.AcceptedOperation = DataPackageOperation.None; }
        }

        private void OnDragOverMainGrid(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            { e.AcceptedOperation = DataPackageOperation.Copy; } else { e.AcceptedOperation = DataPackageOperation.None; }
        }

        private void OnDragLeaveMainGrid(object sender, DragEventArgs e)
        {
            if(CurrentItems.Any() || PathTextBox.Text == "Computer") DragDropHintText.Visibility = Visibility.Collapsed;
            else DragDropHintText.Text = "This folder is empty. Drag files here or navigate elsewhere.";
        }

        private async void OnDropMainGrid(object sender, DragEventArgs e)
        {
            if(CurrentItems.Any() || PathTextBox.Text == "Computer") DragDropHintText.Visibility = Visibility.Collapsed;
            else DragDropHintText.Text = "This folder is empty. Drag files here or navigate elsewhere.";

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    Debug.WriteLine($"Dropped {items.Count} items onto MainContentGrid. First: {items[0].Path}");
                    var pathsToProcess = items.Select(i => i.Path).ToList();
                    await LaunchCreateArchiveDialogWithFilePathsAsync(pathsToProcess);
                }
            }
        }

        private void FileListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) { } else if (args.Phase == 0) { args.RegisterUpdateCallback(LoadItemContainer); args.Handled = true; }
        }
        private void LoadItemContainer(ListViewBase sender, ContainerContentChangingEventArgs args) { /* For future performance optimization */ }

        private async void CompressButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Compress button clicked");
            var selectedUiItems = FileListView.SelectedItems.Cast<FileSystemItem>().ToList();

            if (!selectedUiItems.Any())
            {
                await ShowMessageDialog("No Files Selected", "Please select files or folders to compress.");
                return;
            }
            await LaunchCreateArchiveDialogWithFilePathsAsync(selectedUiItems.Select(item => item.FullPath));
        }

        private async Task ProcessArchiveCreation(CreateArchiveDialog dialog, List<string> sourcePaths)
        {
            Debug.WriteLine($"Attempting to create archive: {dialog.ArchiveFullName}");
            if (sourcePaths == null || !sourcePaths.Any())
            {
                await ShowMessageDialog("Error", "No source files were provided for archive creation.");
                return;
            }
            try
            {
                string outputArchivePath = dialog.ArchiveFullName;
                CompressionLevel deflateLevel = CompressionSettingsHelper.DeflateCompressionLevelFromString(dialog.SelectedCompressionLevelString);
                string password = dialog.Password;

                if (dialog.SelectedArchiveFormat?.ToUpperInvariant() == "ZIP")
                {
                    await _compressionService.CreateZipArchiveFromSelectionsAsync(sourcePaths, outputArchivePath, deflateLevel, password);
                    await ShowMessageDialog("Success", $"Archive '{Path.GetFileName(outputArchivePath)}' (ZIP) created successfully!");
                }
                else if (dialog.SelectedArchiveFormat?.ToUpperInvariant() == "7Z")
                {
                    bool isSolid = dialog.IsSolidArchive;
                    SharpCompress.Common.CompressionType sevenZipType = CompressionSettingsHelper.SevenZipCompressionTypeFromString(dialog.SelectedSevenZipCompressionMethodString);
                    bool encryptHeaders = dialog.EncryptHeaders;

                    await _compressionService.Create7zArchiveFromSelectionsAsync(sourcePaths, outputArchivePath, deflateLevel, isSolid, sevenZipType, password, encryptHeaders);
                    await ShowMessageDialog("Success", $"Archive '{Path.GetFileName(outputArchivePath)}' (7z) created successfully!");
                }
                else
                {
                    await ShowMessageDialog("Not Implemented", $"Archive format '{dialog.SelectedArchiveFormat}' is not yet supported for creation.");
                    return;
                }

                string targetDir = Path.GetDirectoryName(outputArchivePath);
                if(Directory.Exists(targetDir) && (string.IsNullOrEmpty(CurrentPath) || Path.GetFullPath(targetDir).Equals(Path.GetFullPath(CurrentPath), StringComparison.OrdinalIgnoreCase)))
                {
                    LoadDirectory(CurrentPath);
                }
                else if (Directory.Exists(targetDir))
                {
                    LoadDirectory(targetDir);
                }
            }
            catch (NotImplementedException niex)
            {
                Debug.WriteLine($"Compression feature not fully implemented: {niex.Message}");
                await ShowMessageDialog("Feature Incomplete", $"The compression feature is not fully implemented yet for this format/combination: {niex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during compression: {ex.ToString()}");
                await ShowMessageDialog("Compression Error", $"Could not create archive. Reason: {ex.Message}");
            }
        }

        private async void DecompressButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Decompress button clicked");

            var openPicker = new FileOpenPicker();
            openPicker.FileTypeFilter.Add(".zip");
            openPicker.FileTypeFilter.Add(".7z");
            InitializeWithWindow.Initialize(openPicker, App.WindowHandle);

            StorageFile archiveFile = await openPicker.PickSingleFileAsync();
            if (archiveFile == null)
            {
                Debug.WriteLine("Decompression: User cancelled archive file selection.");
                return;
            }

            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(folderPicker, App.WindowHandle);

            StorageFolder destinationFolder = await folderPicker.PickSingleFolderAsync();
            if (destinationFolder == null)
            {
                Debug.WriteLine("Decompression: User cancelled destination folder selection.");
                return;
            }

            string password = null;
            var passwordDialog = new PasswordInputDialog();
            passwordDialog.XamlRoot = this.Content.XamlRoot;
            passwordDialog.SetInstructionText($"Enter password for '{archiveFile.Name}' (leave empty if none):");

            ContentDialogResult passwordResult = await passwordDialog.ShowAsync();
            if (passwordResult == ContentDialogResult.Primary)
            {
                password = passwordDialog.Password;
            }
            else
            {
                 Debug.WriteLine("Decompression: Password dialog was cancelled or dismissed. Proceeding without password.");
            }

            Debug.WriteLine($"Attempting to extract '{archiveFile.Path}' to '{destinationFolder.Path}'. Password provided: {!string.IsNullOrEmpty(password)}");
            string fileExtension = Path.GetExtension(archiveFile.Name).ToLowerInvariant();

            try
            {
                if (fileExtension == ".zip")
                {
                    await _compressionService.ExtractZipArchiveAsync(archiveFile.Path, destinationFolder.Path, password);
                    await ShowMessageDialog("Extraction Successful", $"Archive '{archiveFile.Name}' (ZIP) extracted successfully to '{destinationFolder.Name}'.");
                }
                else if (fileExtension == ".7z")
                {
                    await _compressionService.Extract7zArchiveAsync(archiveFile.Path, destinationFolder.Path, password);
                    await ShowMessageDialog("Extraction Successful", $"Archive '{archiveFile.Name}' (7z) extracted successfully to '{destinationFolder.Name}'.");
                }
                else
                {
                    await ShowMessageDialog("Unsupported File Type", $"The selected file type ('{fileExtension}') is not supported for decompression by this action.");
                    return;
                }
                LoadDirectory(destinationFolder.Path);
            }
            catch (InvalidOperationException opEx) when (opEx.InnerException is CryptographicException || opEx.Message.ToLower().Contains("password"))
            {
                Debug.WriteLine($"Password error during extraction: {opEx.Message}");
                await ShowMessageDialog("Extraction Failed", "Invalid password or encrypted archive requires a password.");
            }
            catch (InvalidDataException idEx)
            {
                Debug.WriteLine($"Corrupted archive error during extraction: {idEx.Message}");
                await ShowMessageDialog("Extraction Failed", "The archive appears to be corrupted or is not a valid file for this operation.");
            }
             catch (NotImplementedException niex)
            {
                 Debug.WriteLine($"Extraction feature not implemented: {niex.Message}");
                await ShowMessageDialog("Feature Incomplete", $"Extraction for this specific format/case is not fully implemented yet: {niex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Generic error during extraction: {ex.ToString()}");
                await ShowMessageDialog("Extraction Error", $"Could not extract archive. Reason: {ex.Message}");
            }
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Settings button clicked");
            var dialog = new PlaceholderDialog();
            dialog.XamlRoot = this.Content.XamlRoot;
            dialog.SetTitle("Settings");
            dialog.SetMessage("Application settings will be configurable here. This feature is under construction.");
            await dialog.ShowAsync();
        }

        private async Task ShowMessageDialog(string title, string message)
        {
            var dialog = new PlaceholderDialog();
            dialog.XamlRoot = this.Content.XamlRoot;
            dialog.SetTitle(title);
            dialog.SetMessage(message);
            await dialog.ShowAsync();
        }
    }
}
