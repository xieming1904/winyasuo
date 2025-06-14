using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
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
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace Unpack
{
    public sealed partial class MainWindow : Window
    {
        public ObservableCollection<FileSystemItem> CurrentItems { get; set; } = new ObservableCollection<FileSystemItem>();
        private string _currentPath;
        public string CurrentPath
        {
            get => _currentPath;
            set { _currentPath = value; if (!_isBrowsingArchive) PathTextBox.Text = _currentPath ?? "Computer"; }
        }
        private CompressionService _compressionService = new CompressionService();
        private bool _isPreviewPaneVisible = false;
        private bool _isBrowsingArchive = false;
        private IArchive _currentArchive = null;
        private string _localPathBeforeArchive = null;
        private string _currentArchiveFilePath = null;
        private string _currentArchivePathInternal = "";

        public MainWindow()
        {
            this.InitializeComponent(); Title = "Unpack"; App.WindowHandle = WindowNative.GetWindowHandle(this);
            FileListView.ItemsSource = CurrentItems; LoadInitialDirectory();
            MainContentGrid.AllowDrop = true; MainContentGrid.DragEnter += OnDragEnterMainGrid; MainContentGrid.DragOver += OnDragOverMainGrid; MainContentGrid.DragLeave += OnDragLeaveMainGrid; MainContentGrid.Drop += OnDropMainGrid;
            this.Loaded += MainWindow_Loaded; UpdatePreviewPaneVisibility();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (App.QueuedFilePathsForUI != null && App.QueuedFilePathsForUI.Any())
            { await LaunchCreateArchiveDialogWithFilePathsAsync(App.QueuedFilePathsForUI); App.QueuedFilePathsForUI = null; }
        }

        public async Task LaunchCreateArchiveDialogWithFilePathsAsync(IEnumerable<string> filePaths)
        {
            if (filePaths == null || !filePaths.Any()) return;
            var fileSystemItems = new List<FileSystemItem>();
            foreach (var path in filePaths)
            {
                if (File.Exists(path)) { var fi = new FileInfo(path); fileSystemItems.Add(new FileSystemItem(fi.Name, fi.FullName, (string.IsNullOrEmpty(fi.Extension) ? "File" : fi.Extension.TrimStart('.').ToUpperInvariant() + " File"), "\xE7C3", fi.LastWriteTimeUtc, fi.Length)); }
                else if (Directory.Exists(path)) { var di = new DirectoryInfo(path); fileSystemItems.Add(new FileSystemItem(di.Name, di.FullName, "Folder", "\xE8D7", di.LastWriteTimeUtc, 0)); }
            }
            if (!fileSystemItems.Any()) { await ShowMessageDialog("No Valid Items", "Could not process for archiving."); return; }
            var dialog = new CreateArchiveDialog(); dialog.InitializeDialog(fileSystemItems); dialog.XamlRoot = this.Content.XamlRoot;
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary) { await ProcessArchiveCreation(dialog, filePaths.ToList()); }
            else { Debug.WriteLine("CreateArchiveDialog (from CLI/Drop) was cancelled."); }
        }

        private async void LoadInitialDirectory() { try { StorageFolder docs = await KnownFolders.GetFolderForUserAsync(null, KnownFolderId.Documents); if (docs != null) LoadDirectory(docs.Path); else LoadDrives(); } catch (Exception ex) { Debug.WriteLine($"Initial dir error: {ex.Message}"); LoadDrives(); } }
        private void LoadDirectory(string path)
        {
            _isBrowsingArchive = false; _currentArchive?.Dispose(); _currentArchive = null; _currentArchiveFilePath = null; _currentArchivePathInternal = ""; ClearPreview();
            if (string.IsNullOrEmpty(path)) { LoadDrives(); return; } string adjPath = path; if (path.Length == 2 && path.EndsWith(":")) adjPath += "\\";
            if (adjPath.Length == 3 && adjPath.EndsWith(":\\")) { DriveInfo di = DriveInfo.GetDrives().FirstOrDefault(d => d.Name.Equals(adjPath, StringComparison.OrdinalIgnoreCase)); if (di==null || !di.IsReady) { ShowMessageDialog("Nav Error", $"Drive {adjPath} not ready."); LoadDrives(); CurrentPath=adjPath; return; }}
            else if (!Directory.Exists(adjPath)) { ShowMessageDialog("Nav Error", $"Dir '{adjPath}' not found."); LoadDrives(); CurrentPath=adjPath; return; }
            CurrentPath = adjPath; CurrentItems.Clear(); DragDropHintText.Visibility = Visibility.Collapsed;
            try { foreach (var d in Directory.GetDirectories(adjPath)) { var i=new DirectoryInfo(d); CurrentItems.Add(new FileSystemItem(i.Name,i.FullName,"Folder","\xE8D7",i.LastWriteTimeUtc,0));} foreach (var f in Directory.GetFiles(adjPath)) { var i=new FileInfo(f); CurrentItems.Add(new FileSystemItem(i.Name,i.FullName,(string.IsNullOrEmpty(i.Extension)?"File":i.Extension.TrimStart('.').ToUpperInvariant()+" File"),"\xE7C3",i.LastWriteTimeUtc,i.Length));} if(!CurrentItems.Any()){DragDropHintText.Text="Folder empty."; DragDropHintText.Visibility=Visibility.Visible;}}
            catch (UnauthorizedAccessException) { ShowMessageDialog("Access Denied", $"Access to '{Path.GetFileName(adjPath)}' denied."); UpButton_Click(null,null); } catch (Exception ex) { ShowMessageDialog("Load Error", $"Error: {ex.Message}");}
        }
        private void LoadDrives()
        {
            _isBrowsingArchive = false; _currentArchive?.Dispose(); _currentArchive = null; _currentArchiveFilePath = null; _currentArchivePathInternal = ""; ClearPreview();
            CurrentPath = ""; PathTextBox.Text = "Computer"; CurrentItems.Clear(); DragDropHintText.Visibility = Visibility.Collapsed;
            try { foreach (var dr in DriveInfo.GetDrives().Where(d => d.IsReady)) { CurrentItems.Add(new FileSystemItem(string.IsNullOrEmpty(dr.VolumeLabel)?dr.Name.Replace("\\",""):$"{dr.VolumeLabel} ({dr.Name.Replace("\\","")})", dr.Name, "Drive", "\xE8A7", DateTimeOffset.MinValue, dr.TotalSize )); } if(!CurrentItems.Any()){ DragDropHintText.Text="No drives."; DragDropHintText.Visibility=Visibility.Visible;}}
            catch (Exception ex) { ShowMessageDialog("Drive Load Error", $"Error: {ex.Message}");}
        }
        private async Task OpenArchiveAsync(string archivePath)
        {
            Debug.WriteLine($"Opening archive: {archivePath}"); ClearPreview(); string password = null;
            try {
                using (var testArchive = ArchiveFactory.Open(archivePath, new ReaderOptions{LookForHeader=true})) { if (testArchive.IsEncrypted || (testArchive.Entries.Any() && testArchive.Entries.First(entry=>!entry.IsDirectory).IsEncrypted)) { var pwdDlg = new PasswordInputDialog{XamlRoot=this.Content.XamlRoot}; pwdDlg.SetInstructionText($"Password for '{Path.GetFileName(archivePath)}':"); var res = await pwdDlg.ShowAsync(); if (res==ContentDialogResult.Primary) password = pwdDlg.Password; else return; }}
                _currentArchive = ArchiveFactory.Open(archivePath, new ReaderOptions{Password=password, LookForHeader=true});
                _localPathBeforeArchive = CurrentPath; _currentArchiveFilePath = archivePath; _isBrowsingArchive = true; LoadArchiveEntries("");
            } catch (CryptographicException){await ShowMessageDialog("Password Error", "Incorrect password or required.");_currentArchive?.Dispose();_currentArchive=null;} catch (Exception ex){await ShowMessageDialog("Open Archive Error", $"Could not open '{Path.GetFileName(archivePath)}': {ex.Message}");_currentArchive?.Dispose();_currentArchive=null;}
        }
        private void LoadArchiveEntries(string internalPath)
        {
            if (_currentArchive==null || !_isBrowsingArchive) return; CurrentItems.Clear(); _currentArchivePathInternal = internalPath.Replace(Path.AltDirectorySeparatorChar,Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar); PathTextBox.Text = Path.Combine(_currentArchiveFilePath,_currentArchivePathInternal).Replace(Path.AltDirectorySeparatorChar,Path.DirectorySeparatorChar); ClearPreview(); DragDropHintText.Visibility=Visibility.Collapsed;
            try { string normPfx = string.IsNullOrEmpty(_currentArchivePathInternal)?"":_currentArchivePathInternal.Replace(Path.DirectorySeparatorChar,'/')+"/"; foreach (var en in _currentArchive.Entries.Where(e=>{string k=e.Key.Replace(Path.DirectorySeparatorChar,'/'); return k.StartsWith(normPfx,StringComparison.OrdinalIgnoreCase) && !k.Substring(normPfx.Length).TrimEnd('/').Contains('/');}).ToList()) CurrentItems.Add(new FileSystemItem(en,_currentArchiveFilePath)); if(!CurrentItems.Any()){DragDropHintText.Text="Archive folder empty.";DragDropHintText.Visibility=Visibility.Visible;}}
            catch (Exception ex){ShowMessageDialog("Browse Archive Error", $"Error: {ex.Message}");}
        }
        private void CloseArchiveBrowser() { _currentArchive?.Dispose();_currentArchive=null;_isBrowsingArchive=false;_currentArchiveFilePath=null;_currentArchivePathInternal="";ClearPreview(); if(!string.IsNullOrEmpty(_localPathBeforeArchive)&&Directory.Exists(_localPathBeforeArchive))LoadDirectory(_localPathBeforeArchive); else LoadInitialDirectory();}

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            ClearPreview();
            if (_isBrowsingArchive) { if (string.IsNullOrEmpty(_currentArchivePathInternal)) CloseArchiveBrowser(); else { string p = _currentArchivePathInternal.Contains('/') ? _currentArchivePathInternal.Substring(0, _currentArchivePathInternal.TrimEnd('/').LastIndexOf('/')) : ""; LoadArchiveEntries(p); } }
            else { string pathNav = PathTextBox.Text; if (pathNav.Equals("Computer",StringComparison.OrdinalIgnoreCase)||string.IsNullOrEmpty(CurrentPath)&&string.IsNullOrEmpty(pathNav))return; string pTry=null; if(!string.IsNullOrEmpty(CurrentPath)&&Directory.Exists(CurrentPath))pTry=Path.GetDirectoryName(CurrentPath); else if(Directory.Exists(pathNav))pTry=Path.GetDirectoryName(pathNav.TrimEnd(Path.DirectorySeparatorChar)); if(!string.IsNullOrEmpty(pTry))LoadDirectory(pTry);else LoadDrives(); }
        }
        private async void GoButton_Click(object sender, RoutedEventArgs e)
        {
            ClearPreview(); string target = PathTextBox.Text;
            if (_isBrowsingArchive && _currentArchiveFilePath!=null) { if (target.StartsWith(_currentArchiveFilePath,StringComparison.OrdinalIgnoreCase)) { string iPath = target.Substring(_currentArchiveFilePath.Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar); LoadArchiveEntries(iPath);}}
            else { if(target.Equals("Computer",StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(target))LoadDrives(); else if(File.Exists(target)&&IsKnownArchiveType(target))await OpenArchiveAsync(target); else LoadDirectory(target); }
        }
        private void PathTextBox_KeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Enter) GoButton_Click(sender, e); }
        private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e) { /* Suggestions later */ }

        private bool _isPreviewPaneVisible = false;
        private void TogglePreviewPaneButton_Click(object sender, RoutedEventArgs e) { _isPreviewPaneVisible = TogglePreviewPaneButton.IsChecked ?? false; UpdatePreviewPaneVisibility(); if (_isPreviewPaneVisible) UpdatePreview(); else ClearPreview(); }
        private void UpdatePreviewPaneVisibility() { PreviewColumn.Width = _isPreviewPaneVisible ? new GridLength(320, GridUnitType.Pixel) : new GridLength(0); PreviewPane.Visibility = _isPreviewPaneVisible ? Visibility.Visible : Visibility.Collapsed; TogglePreviewPaneButton.IsChecked = _isPreviewPaneVisible; }

        private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPreviewPaneVisible) UpdatePreview(); else ClearPreview();
            UpdateActionButtonsState(); // Update Test/Repair button states
        }
        private void UpdateActionButtonsState()
        {
            bool singleItemSelected = FileListView.SelectedItems.Count == 1;
            FileSystemItem selectedItem = FileListView.SelectedItem as FileSystemItem;

            bool isArchiveSelected = singleItemSelected && selectedItem != null && !selectedItem.IsArchiveEntry && IsKnownArchiveType(selectedItem.FullPath);
            bool isZipSelected = singleItemSelected && selectedItem != null && !selectedItem.IsArchiveEntry && Path.GetExtension(selectedItem.FullPath).Equals(".zip", StringComparison.OrdinalIgnoreCase);

            TestArchiveButton.IsEnabled = isArchiveSelected || (_isBrowsingArchive && _currentArchive != null); // Enable if browsing archive or local archive selected
            RepairArchiveButton.IsEnabled = isZipSelected; // Only for local .zip files for now
        }

        private async void UpdatePreview()
        {
            if (!_isPreviewPaneVisible || FileListView.SelectedItem == null) { if(_isPreviewPaneVisible) PreviewStatusText.Text = "Select an item to preview."; ClearPreviewVisuals(); return; }
            var sel = FileListView.SelectedItem as FileSystemItem; if (sel == null) { ClearPreviewVisuals(); return; }
            PreviewFileNameText.Text = sel.Name; ClearPreviewVisuals(isLoading: true);
            if (!sel.IsArchiveEntry) { PreviewStatusText.Text = "Preview for local files not yet implemented."; PreviewLoadingRing.IsActive=false; PreviewStatusText.Visibility=Visibility.Visible; return; }
            if (sel.ArchiveEntry == null) { PreviewStatusText.Text = "Invalid archive entry."; PreviewLoadingRing.IsActive=false; PreviewStatusText.Visibility=Visibility.Visible; return; }
            var entry = sel.ArchiveEntry; if (entry.IsDirectory) { PreviewStatusText.Text = "Cannot preview folders."; PreviewLoadingRing.IsActive=false; PreviewStatusText.Visibility=Visibility.Visible; return; }
            try { string ext = Path.GetExtension(entry.Key)?.ToLowerInvariant();
                if (IsPreviewableImage(ext)) { using (var s=entry.OpenEntryStream()){var ms=new MemoryStream();await s.CopyToAsync(ms);ms.Position=0;var bmp=new BitmapImage();await bmp.SetSourceAsync(ms.AsRandomAccessStream());PreviewImage.Source=bmp;PreviewImage.Visibility=Visibility.Visible;}}
                else if (IsPreviewableText(ext)) { const long maxPrevSize=1*1024*1024; if(entry.Size > maxPrevSize){PreviewTextContent.Text=$"File too large ({sel.DisplaySize}).";} else { using(var s=entry.OpenEntryStream())using(var r=new StreamReader(s,Encoding.UTF8,true)){char[]buf=new char[8192];int rd=await r.ReadAsync(buf,0,buf.Length);PreviewTextContent.Text=new string(buf,0,rd)+(rd==buf.Length&&!r.EndOfStream?"...":"");}} PreviewTextBorder.Visibility=Visibility.Visible;}
                else { PreviewStatusText.Text = $"No preview for '{sel.Name}'."; }
            } catch (Exception ex) { Debug.WriteLine($"Preview error '{entry.Key}': {ex.Message}"); PreviewStatusText.Text = $"Could not load preview."; }
            finally { PreviewLoadingRing.IsActive=false; PreviewStatusText.Visibility = (PreviewImage.Visibility==Visibility.Collapsed && PreviewTextBorder.Visibility==Visibility.Collapsed)?Visibility.Visible:Visibility.Collapsed;}
        }
        private bool IsPreviewableImage(string ext) => new[]{".png",".jpg",".jpeg",".bmp",".gif"}.Contains(ext);
        private bool IsPreviewableText(string ext) => new[]{".txt",".log",".ini",".config",".md",".xml",".json",".cs",".java",".py",".js",".xaml",".html",".css",".csv",".nfo",".diz"}.Contains(ext);
        private void ClearPreview() { PreviewFileNameText.Text=""; ClearPreviewVisuals(); if (_isPreviewPaneVisible) PreviewStatusText.Text = _isBrowsingArchive ? "Select archive entry." : "Select local file.";}
        private void ClearPreviewVisuals(bool isLoading=false) { PreviewImage.Source=null;PreviewImage.Visibility=Visibility.Collapsed;PreviewTextContent.Text="";PreviewTextBorder.Visibility=Visibility.Collapsed;PreviewStatusText.Visibility=isLoading?Visibility.Collapsed:Visibility.Visible;PreviewLoadingRing.IsActive=isLoading;}

        private void OnDragEnterMainGrid(object sender, DragEventArgs e) { if (e.DataView.Contains(StandardDataFormats.StorageItems)) { e.AcceptedOperation = DataPackageOperation.Copy; DragDropHintText.Text = "Drop files/folders to add to archive or open"; DragDropHintText.Visibility = Visibility.Visible; } else { e.AcceptedOperation = DataPackageOperation.None; } }
        private void OnDragOverMainGrid(object sender, DragEventArgs e) { if (e.DataView.Contains(StandardDataFormats.StorageItems)) e.AcceptedOperation = DataPackageOperation.Copy; else e.AcceptedOperation = DataPackageOperation.None; }
        private void OnDragLeaveMainGrid(object sender, DragEventArgs e) { if(CurrentItems.Any() || PathTextBox.Text == "Computer" || _isBrowsingArchive) DragDropHintText.Visibility = Visibility.Collapsed; else DragDropHintText.Text = "This folder is empty."; }
        private async void OnDropMainGrid(object sender, DragEventArgs e)
        {
            if(CurrentItems.Any() || PathTextBox.Text == "Computer" || _isBrowsingArchive) DragDropHintText.Visibility = Visibility.Collapsed; else DragDropHintText.Text = "This folder is empty.";
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            { var items = await e.DataView.GetStorageItemsAsync(); if (items.Count > 0) { if (items.Count==1 && items[0] is StorageFile file && IsKnownArchiveType(file.Path)) { await OpenArchiveAsync(file.Path);} else {var pathsToProcess = items.Select(i=>i.Path).ToList(); await LaunchCreateArchiveDialogWithFilePathsAsync(pathsToProcess);}}}
        }
        private void FileListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args) { if (args.InRecycleQueue) {} else if (args.Phase == 0) { args.RegisterUpdateCallback(LoadItemContainer); args.Handled = true; } }
        private void LoadItemContainer(ListViewBase sender, ContainerContentChangingEventArgs args) { /* For future performance optimization */ }

        private async void CompressButton_Click(object sender, RoutedEventArgs e)
        {
            var selItems = FileListView.SelectedItems.Cast<FileSystemItem>().ToList();
            if (_isBrowsingArchive) { await ShowMessageDialog("Action Not Supported", "Compressing from an open archive not yet supported. Extract first."); return; }
            if (!selItems.Any()) { await ShowMessageDialog("No Files Selected", "Select files/folders to compress."); return; }
            await LaunchCreateArchiveDialogWithFilePathsAsync(selItems.Select(item => item.FullPath));
        }
        private async Task ProcessArchiveCreation(CreateArchiveDialog dialog, List<string> sourcePaths)
        {
            if (sourcePaths == null || !sourcePaths.Any()) { await ShowMessageDialog("Error", "No source files for archiving."); return; }
            Debug.WriteLine($"Creating archive: {dialog.ArchiveFullName}");
            try { string output = dialog.ArchiveFullName; CompressionLevel compLvl = CompressionSettingsHelper.DeflateCompressionLevelFromString(dialog.SelectedCompressionLevelString); string pwd = dialog.Password; string fmt = dialog.SelectedArchiveFormat?.ToUpperInvariant(); bool sfx = dialog.CreateSfxArchive; long? volSize = dialog.ParseVolumeSizeStringToBytes(dialog.VolumeSizeString);
                if (fmt=="ZIP") await _compressionService.CreateZipArchiveFromSelectionsAsync(sourcePaths,output,compLvl,pwd,dialog.SelectedEncryptionMethod,dialog.EncryptHeaders,volSize,sfx);
                else if (fmt=="7Z") { SharpCompress.Common.CompressionType type7z = CompressionSettingsHelper.SevenZipCompressionTypeFromString(dialog.SelectedSevenZipCompressionMethodString); await _compressionService.Create7zArchiveFromSelectionsAsync(sourcePaths,output,compLvl,dialog.IsSolidArchive,type7z,pwd,dialog.EncryptHeaders,volSize,sfx); }
                else { await ShowMessageDialog("Not Implemented", $"Format '{dialog.SelectedArchiveFormat}' not supported."); return; }
                await ShowMessageDialog("Success", $"Archive '{Path.GetFileName(output)}' ({fmt}{(sfx?", SFX":"")}{(volSize.HasValue?", Split":"")}) created.");
                string targetDir = Path.GetDirectoryName(output); if(Directory.Exists(targetDir) && (string.IsNullOrEmpty(CurrentPath) || Path.GetFullPath(targetDir).Equals(Path.GetFullPath(CurrentPath),StringComparison.OrdinalIgnoreCase))) LoadDirectory(CurrentPath); else if(Directory.Exists(targetDir)) LoadDirectory(targetDir);
            } catch (FileNotFoundException fnfEx) when (fnfEx.Message.Contains("SFX module")) { await ShowMessageDialog("SFX Creation Failed", fnfEx.Message); }
            catch (IOException ioEx) when (ioEx.Message.Contains("SFX file. Archive may be available")) { await ShowMessageDialog("SFX Creation Warning", ioEx.Message); }
            catch (NotImplementedException niex) { await ShowMessageDialog("Feature Incomplete", $"Not fully implemented: {niex.Message}"); }
            catch (Exception ex) { await ShowMessageDialog("Compression Error", $"Error: {ex.ToString()}"); }
        }
        private async void DecompressButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".zip"); picker.FileTypeFilter.Add(".7z"); picker.FileTypeFilter.Add(".rar"); picker.FileTypeFilter.Add(".tar"); picker.FileTypeFilter.Add(".gz"); picker.FileTypeFilter.Add(".tgz"); picker.FileTypeFilter.Add(".bz2"); picker.FileTypeFilter.Add(".xz"); picker.FileTypeFilter.Add(".wim"); picker.FileTypeFilter.Add(".iso");
            InitializeWithWindow.Initialize(picker, App.WindowHandle); StorageFile archiveFile = await picker.PickSingleFileAsync(); if (archiveFile==null) return;
            var folderPicker = new FolderPicker(); folderPicker.SuggestedStartLocation = PickerLocationId.Desktop; folderPicker.FileTypeFilter.Add("*"); InitializeWithWindow.Initialize(folderPicker, App.WindowHandle); StorageFolder destFolder = await folderPicker.PickSingleFolderAsync(); if (destFolder==null) return;
            string pwd=null; var pwdDlg=new PasswordInputDialog{XamlRoot=this.Content.XamlRoot}; pwdDlg.SetInstructionText($"Password for '{archiveFile.Name}' (if any):"); if(await pwdDlg.ShowAsync()==ContentDialogResult.Primary)pwd=pwdDlg.Password;
            try { await _compressionService.ExtractArchiveAsync(archiveFile.Path, destFolder.Path, pwd); await ShowMessageDialog("Extraction Successful", $"Archive '{archiveFile.Name}' extracted to '{destFolder.Name}'."); LoadDirectory(destFolder.Path); }
            catch (InvalidOperationException opEx) when (opEx.Message.ToLower().Contains("password")||(opEx.InnerException is CryptographicException)){await ShowMessageDialog("Extraction Failed","Invalid password or encrypted archive requires a password.");}
            catch (InvalidDataException idEx){await ShowMessageDialog("Extraction Failed",$"Archive '{archiveFile.Name}' corrupted/unsupported. Error: {idEx.Message}");}
            catch (NotImplementedException niex){await ShowMessageDialog("Feature Incomplete",$"Not fully implemented: {niex.Message}");}
            catch (Exception ex){await ShowMessageDialog("Extraction Error",$"Error: {ex.Message}");}
        }
        private async void TestArchiveButton_Click(object sender, RoutedEventArgs e)
        {
            FileSystemItem selectedItem = FileListView.SelectedItem as FileSystemItem;
            string archivePath = null;

            if (_isBrowsingArchive && _currentArchive != null) archivePath = _currentArchiveFilePath;
            else if (selectedItem != null && !selectedItem.IsArchiveEntry && IsKnownArchiveType(selectedItem.FullPath)) archivePath = selectedItem.FullPath;
            else { var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".zip"); picker.FileTypeFilter.Add(".7z"); picker.FileTypeFilter.Add(".rar"); /* Add all known types */ InitializeWithWindow.Initialize(picker, App.WindowHandle); StorageFile file = await picker.PickSingleFileAsync(); if (file == null) return; archivePath = file.Path; }
            if (string.IsNullOrEmpty(archivePath)) { await ShowMessageDialog("Test Archive", "No archive file specified or selected."); return; }

            string password = null; var pwdDlg = new PasswordInputDialog{XamlRoot = this.Content.XamlRoot}; pwdDlg.SetInstructionText($"Password for '{Path.GetFileName(archivePath)}' (if any):");
            if (await pwdDlg.ShowAsync() == ContentDialogResult.Primary) password = pwdDlg.Password;

            var (success, message) = await _compressionService.TestArchiveAsync(archivePath, password);
            await ShowMessageDialog(success ? "Test Successful" : "Test Failed", message);
        }
        private async void RepairArchiveButton_Click(object sender, RoutedEventArgs e)
        {
            FileSystemItem selectedItem = FileListView.SelectedItem as FileSystemItem;
            if (selectedItem == null || selectedItem.IsArchiveEntry || !Path.GetExtension(selectedItem.FullPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            { await ShowMessageDialog("Repair Archive", "Please select a local .zip file to attempt repair."); return; }
            string corruptedZipPath = selectedItem.FullPath;

            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Documents;
            savePicker.FileTypeChoices.Add("Repaired ZIP Archive", new List<string> { ".zip" });
            savePicker.SuggestedFileName = $"{Path.GetFileNameWithoutExtension(corruptedZipPath)}_repaired.zip";
            InitializeWithWindow.Initialize(savePicker, App.WindowHandle);
            StorageFile repairedFile = await savePicker.PickSaveFileAsync();
            if (repairedFile == null) return;

            var (success, message, recoveredCount) = await _compressionService.RepairZipArchiveAsync(corruptedZipPath, repairedFile.Path);
            await ShowMessageDialog(success ? "Repair Attempted" : "Repair Failed", $"{message} ({recoveredCount} entries processed for recovery).");
            if (success) LoadDirectory(Path.GetDirectoryName(repairedFile.Path));
        }
        private async void SettingsButton_Click(object sender, RoutedEventArgs e) { var dialog = new PlaceholderDialog{XamlRoot=this.Content.XamlRoot}; dialog.SetTitle("Settings"); dialog.SetMessage("App settings here. (Not implemented)"); await dialog.ShowAsync(); }
        private async Task ShowMessageDialog(string title, string message) { var dialog = new PlaceholderDialog{XamlRoot=this.Content.XamlRoot}; dialog.SetTitle(title); dialog.SetMessage(message); await dialog.ShowAsync(); }
    }
}
