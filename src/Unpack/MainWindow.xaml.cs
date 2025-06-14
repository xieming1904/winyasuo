using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Diagnostics; // For Debug.WriteLine
using Windows.Storage; // For StorageFile, StorageFolder
// using Windows.Storage.FileProperties; // For BasicProperties (not strictly needed for this version)
using System.Threading.Tasks; // For Task

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
                PathTextBox.Text = _currentPath ?? ""; // Ensure PathTextBox is updated
            }
        }

        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Unpack";

            FileListView.ItemsSource = CurrentItems;
            LoadInitialDirectory();

            MainContentGrid.AllowDrop = true;
            MainContentGrid.DragEnter += OnDragEnterMainGrid;
            MainContentGrid.DragOver += OnDragOverMainGrid;
            MainContentGrid.DragLeave += OnDragLeaveMainGrid;
            MainContentGrid.Drop += OnDropMainGrid;

            // Ensure XamlRoot is available for dialogs
            // It's good practice to set this early if not done by the framework automatically
            // For WinUI 3, ContentDialog automatically picks up XamlRoot from the current visual tree when shown.
            // However, if we were creating it very early or from a non-UI thread, we might need to pass it.
            // this.Activated += (sender, args) => { if (Content.XamlRoot != null) { /* Store it if needed */ } };

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

        private async void LoadDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                LoadDrives();
                return;
            }

            // Check if path is a drive root like "C:\"
            if (path.Length == 3 && path.EndsWith(":\\") || path.Length == 2 && path.EndsWith(":"))
            {
                 if(path.Length == 2) path += "\\"; // Ensure C:\
                // For drives, Directory.Exists might be problematic, use DriveInfo
                DriveInfo driveInfo = DriveInfo.GetDrives().FirstOrDefault(d => d.Name.Equals(path, StringComparison.OrdinalIgnoreCase));
                if (driveInfo == null || !driveInfo.IsReady)
                {
                    Debug.WriteLine($"Drive not ready or does not exist: {path}");
                    ShowErrorDialog("Navigation Error", $"Drive {path} is not ready or accessible.");
                    // Optionally fall back to listing all drives or a default path
                    LoadDrives();
                    CurrentPath = path; // Still reflect attempted path
                    return;
                }
            }
            else if (!Directory.Exists(path))
            {
                Debug.WriteLine($"Directory does not exist: {path}");
                ShowErrorDialog("Navigation Error", $"The directory '{path}' does not exist or is not accessible.");
                // Fallback or clear view
                LoadDrives(); // Or a known safe path
                CurrentPath = path; // Reflect attempted path
                return;
            }


            CurrentPath = path;
            CurrentItems.Clear();
            DragDropHintText.Visibility = Visibility.Collapsed;

            try
            {
                var directories = Directory.GetDirectories(path);
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

                var files = Directory.GetFiles(path);
                foreach (var filePath in files)
                {
                    var fileInfo = new FileInfo(filePath);
                    CurrentItems.Add(new FileSystemItem(
                        fileInfo.Name,
                        fileInfo.FullName,
                        (string.IsNullOrEmpty(fileInfo.Extension) ? "File" : fileInfo.Extension.ToUpperInvariant() + " File"),
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
                Debug.WriteLine($"Access denied to directory: {path}");
                ShowErrorDialog("Access Denied", $"Access to the directory '{Path.GetFileName(path)}' was denied.");
                // Optionally, navigate up or to a safe default.
                UpButton_Click(null, null); // Go up one level as a fallback
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading directory: {path} - {ex.Message}");
                ShowErrorDialog("Error Loading Directory", $"Could not load directory '{Path.GetFileName(path)}'. Reason: {ex.Message}");
            }
        }

        private void LoadDrives()
        {
            CurrentPath = "";
            PathTextBox.Text = "Computer"; // Display something like "Computer" or "This PC"
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
                ShowErrorDialog("Error Loading Drives", $"Could not retrieve drive information. Reason: {ex.Message}");
            }
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(CurrentPath) && CurrentPath != PathTextBox.Text && PathTextBox.Text != "Computer" ) // PathTextBox might be "Computer"
            {
                 // If PathTextBox was manually edited and differs from CurrentPath, prioritize PathTextBox
                var potentialParent = Path.GetDirectoryName(PathTextBox.Text);
                 if (!string.IsNullOrEmpty(potentialParent) && Directory.Exists(potentialParent))
                 {
                    LoadDirectory(potentialParent);
                    return;
                 }
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
            } else {
                LoadDrives();
            }
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            string targetPath = PathTextBox.Text;
            if (targetPath.Equals("Computer", StringComparison.OrdinalIgnoreCase) ||
                targetPath.Equals("This PC", StringComparison.OrdinalIgnoreCase))
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
                GoButton_Click(sender, e); // Trigger Go button logic
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
                else if (selectedItem.ItemType != "Error") // It's a file
                {
                    Debug.WriteLine($"File double-tapped: {selectedItem.FullPath} - Placeholder for file open/association.");
                    // This would be where you might try to open the file with default app,
                    // or if it's an archive, open it within Unpack itself.
                    // For now, just a debug message.
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
            if(CurrentItems.Any()) DragDropHintText.Visibility = Visibility.Collapsed;
            else DragDropHintText.Text = "This folder is empty. Drag files here or navigate elsewhere.";
        }

        private async void OnDropMainGrid(object sender, DragEventArgs e)
        {
            if(CurrentItems.Any()) DragDropHintText.Visibility = Visibility.Collapsed;
            else DragDropHintText.Text = "This folder is empty. Drag files here or navigate elsewhere.";

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    Debug.WriteLine($"Dropped {items.Count} items onto MainContentGrid. First: {items[0].Path}");
                    // TODO: Logic to handle dropped files - e.g., add to a list, open CreateArchiveDialog
                    // For now, just list them
                    var dialog = new PlaceholderDialog();
                    dialog.XamlRoot = this.Content.XamlRoot; // Set XamlRoot
                    dialog.SetTitle("Files Dropped");
                    dialog.SetMessage($"You dropped {items.Count} item(s).\nFirst item: {items[0].Name}\n(Implement actual handling for these files)");
                    await dialog.ShowAsync();
                }
            }
        }

        private void FileListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) { } else if (args.Phase == 0) { args.RegisterUpdateCallback(LoadItemContainer); args.Handled = true; }
        }
        private void LoadItemContainer(ListViewBase sender, ContainerContentChangingEventArgs args) { /* For future performance optimization if needed */ }

        // --- Button Click Handlers ---
        private async void CompressButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Compress button clicked");
            var selectedItemsToArchive = FileListView.SelectedItems.Cast<FileSystemItem>().ToList();

            if (!selectedItemsToArchive.Any())
            {
                var infoDialog = new PlaceholderDialog();
                infoDialog.XamlRoot = this.Content.XamlRoot;
                infoDialog.SetTitle("No Files Selected");
                infoDialog.SetMessage("Please select files or folders to compress.");
                await infoDialog.ShowAsync();
                return;
            }

            // For now, we use the placeholder CreateArchiveDialog.
            // Later, this will be replaced by the actual dialog that takes 'selectedItemsToArchive'
            // and allows setting various archive parameters.
            var dialog = new CreateArchiveDialog();
            dialog.XamlRoot = this.Content.XamlRoot; // Important for ContentDialog

            // Pass selected items to the dialog if it's designed to accept them
            // e.g., dialog.InitializeWithItems(selectedItemsToArchive);

            ContentDialogResult result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                Debug.WriteLine("CreateArchiveDialog 'Create' button was clicked.");
                // TODO: Retrieve settings from dialog and start compression
                // For example:
                // string archiveName = dialog.ArchiveName;
                // string format = dialog.SelectedFormat;
                // ... etc.
                // StartCompressionProcess(archiveName, format, selectedItemsToArchive, ...);
            }
            else
            {
                Debug.WriteLine("CreateArchiveDialog was cancelled or closed.");
            }
        }

        private async void DecompressButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Decompress button clicked");
            var dialog = new PlaceholderDialog();
            dialog.XamlRoot = this.Content.XamlRoot; // Set XamlRoot
            dialog.SetTitle("Decompress Action");
            dialog.SetMessage("Decompression options and selection of an archive file will be available here. This feature is under construction.");
            await dialog.ShowAsync();
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Settings button clicked");
            var dialog = new PlaceholderDialog();
            dialog.XamlRoot = this.Content.XamlRoot; // Set XamlRoot
            dialog.SetTitle("Settings");
            dialog.SetMessage("Application settings (e.g., default compression levels, theme preferences, context menu options) will be configurable here. This feature is under construction.");
            await dialog.ShowAsync();
        }

        private async void ShowErrorDialog(string title, string message)
        {
            ContentDialog errorDialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot // Ensure XamlRoot is set for dialogs
            };
            await errorDialog.ShowAsync();
        }
    }
}
