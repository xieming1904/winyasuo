using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle; // For AppInstance.GetActivatedEventArgs()
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unpack.Core; // For CompressionService & CompressionSettingsHelper
using SharpCompress.Compressors.Deflate; // For CompressionLevel enum
using SharpCompress.Common; // For CompressionType enum

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Unpack
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window m_window;
        // Making WindowHandle internal static so MainWindow can access it if needed,
        // but primarily App sets it. For pickers/dialogs from MainWindow, it can use its own handle.
        // This is more for cases where a picker/dialog might be shown *before* MainWindow is fully active or from another context.
        internal static IntPtr WindowHandle { get; set; }

        public static List<string> QueuedFilePathsForUI { get; private set; } = null;


        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            // TODO: Hook up AppInstance.Activated event for richer activation scenarios if needed
            // var mainInstance = AppInstance.FindOrRegisterForKey("unpack-main-instance-key");
            // if (!mainInstance.IsCurrent) { /* Redirect or handle multiple instances */ }
            // mainInstance.Activated += OnAppActivated;
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="launchActivatedEventArgs">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs launchActivatedEventArgs)
        {
            var commandLineArgs = Environment.GetCommandLineArgs();
            bool silentOperationShouldExit = false; // Flag to indicate if app should exit after CLI task

            // Check if we are launched by CLI for a silent operation
            if (commandLineArgs.Length > 1)
            {
                string command = commandLineArgs[1].ToLowerInvariant();
                List<string> paths = commandLineArgs.Skip(2).Select(p => p.Trim('"')).ToList();

                // Validate paths for silent commands that require them
                if (paths.Any() && paths.Any(p => !File.Exists(p) && !Directory.Exists(p)))
                {
                    // For a real CLI, output this to stderr. For now, Debug.
                    Debug.WriteLine($"CLI Error: One or more provided paths do not exist. Command: {command}");
                    // Decide if to exit or let user know. For silent, usually exit.
                    Application.Current.Exit(); // Exit if paths are invalid for CLI operations
                    return;
                }

                CompressionService compressionService = new CompressionService(); // Instantiate for silent ops
                bool commandRecognizedAndAttempted = true;

                try
                {
                    switch (command)
                    {
                        case "--extract-here":
                        case "--extract-to-subdir": // Note: spec used --extract-to-folder, changed to --extract-to-subdir
                            if (!paths.Any()) { Debug.WriteLine($"CLI Error: No archive paths provided for {command}."); break; }
                            Debug.WriteLine($"CLI: Processing {command} for {paths.Count} archive(s).");
                            foreach (var archivePath in paths)
                            {
                                if (!File.Exists(archivePath)) {
                                    Debug.WriteLine($"CLI Error: Archive not found '{archivePath}' for {command}.");
                                    continue;
                                }
                                string destDir;
                                if (command == "--extract-to-subdir")
                                {
                                    string archiveNameNoExt = Path.GetFileNameWithoutExtension(archivePath);
                                    destDir = Path.Combine(Path.GetDirectoryName(archivePath) ?? Environment.CurrentDirectory, archiveNameNoExt);
                                    Directory.CreateDirectory(destDir);
                                }
                                else { destDir = Path.GetDirectoryName(archivePath) ?? Environment.CurrentDirectory; }

                                string fileExtension = Path.GetExtension(archivePath).ToLowerInvariant();
                                Debug.WriteLine($"CLI: Extracting '{archivePath}' to '{destDir}'");
                                if (fileExtension == ".zip") await compressionService.ExtractZipArchiveAsync(archivePath, destDir, null);
                                else if (fileExtension == ".7z") await compressionService.Extract7zArchiveAsync(archivePath, destDir, null);
                                else Debug.WriteLine($"CLI: Unsupported archive type for silent extraction: {fileExtension}");
                            }
                            silentOperationShouldExit = true;
                            break;

                        case "--compress-zip":
                        case "--compress-7z":
                            if (!paths.Any()) { Debug.WriteLine($"CLI Error: No input paths provided for {command}."); break; }
                            Debug.WriteLine($"CLI: Processing {command} for {paths.Count} item(s).");
                            string outputArchiveName = DetermineOutputArchiveName(paths, command == "--compress-zip" ? ".zip" : ".7z");
                            string outputDir = DetermineOutputDirectory(paths);
                            string outputFullPath = Path.Combine(outputDir, outputArchiveName);
                            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                            Debug.WriteLine($"CLI: Compressing to '{outputFullPath}'");
                            if (command == "--compress-zip")
                            {
                                await compressionService.CreateZipArchiveFromSelectionsAsync(paths, outputFullPath, CompressionLevel.Default, null);
                            }
                            else // --compress-7z
                            {
                                await compressionService.Create7zArchiveFromSelectionsAsync(paths, outputFullPath,
                                    CompressionLevel.Default, false, SharpCompress.Common.CompressionType.LZMA2, null, false);
                            }
                            silentOperationShouldExit = true;
                            break;

                        case "--add-to-archive-ui":
                            QueuedFilePathsForUI = paths; // Store paths for MainWindow
                            Debug.WriteLine($"CLI: Queued {paths.Count} paths for 'Add to Archive' UI.");
                            // silentOperationShouldExit remains false, Main Window will launch.
                            commandRecognizedAndAttempted = true; // It's a recognized UI command
                            break;

                        default:
                            commandRecognizedAndAttempted = false; // Not a CLI command we handle here
                            Debug.WriteLine($"CLI: Unknown command '{command}'. Proceeding with normal GUI launch.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CLI Error: Exception during '{command}' operation: {ex.ToString()}");
                    // For a real CLI, log this error appropriately and set an error exit code.
                    silentOperationShouldExit = true; // Ensure exit after error in CLI mode
                }

                if (silentOperationShouldExit && commandRecognizedAndAttempted)
                {
                    Debug.WriteLine("CLI: Silent operation finished or errored. Exiting application.");
                    Application.Current.Exit();
                    return;
                }
            }

            // If not a silent CLI operation that already exited, launch the main window.
            // This will also handle the --add-to-archive-ui case because silentOperationShouldExit is false.
            if (m_window == null) // Check if window is already created (e.g. from other activation kinds)
            {
                m_window = new MainWindow();
            }
            WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(m_window); // Store handle for dialogs

            // Ensure the MainWindow knows if it needs to process QueuedFilePathsForUI.
            // This might be done via a public method on MainWindow or by MainWindow checking App.QueuedFilePathsForUI.
            // For this example, MainWindow's OnLaunched or constructor will check App.QueuedFilePathsForUI.

            m_window.Activate();
        }


        private string DetermineOutputArchiveName(List<string> paths, string extension)
        {
            if (paths == null || !paths.Any()) return "Archive" + extension;

            string namePart;
            if (paths.Count == 1)
            {
                var firstPath = paths[0];
                namePart = Path.GetFileNameWithoutExtension(firstPath);
                if (Directory.Exists(firstPath)) // If it's a directory, its name is correct
                {
                    namePart = new DirectoryInfo(firstPath).Name;
                }
            }
            else
            {
                // Multiple items, try to use common parent folder name
                string commonParent = Path.GetDirectoryName(paths[0]);
                bool allShareParent = true;
                foreach (var path in paths.Skip(1))
                {
                    if (Path.GetDirectoryName(path) != commonParent)
                    {
                        allShareParent = false;
                        break;
                    }
                }
                if (allShareParent && !string.IsNullOrEmpty(commonParent))
                {
                    namePart = new DirectoryInfo(commonParent).Name;
                }
                else
                {
                    namePart = "Archive"; // Fallback for items from diverse locations
                }
            }
            return namePart + extension;
        }

        private string DetermineOutputDirectory(List<string> paths)
        {
            if (paths == null || !paths.Any()) return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            string firstPathDir = Path.GetDirectoryName(paths[0]);
            if (string.IsNullOrEmpty(firstPathDir))
            {
                // If path is like "file.txt", implies current directory.
                // However, context menu usually provides full paths.
                // If truly no directory part, MyDocuments is a safer default than CurrentDirectory for GUI app.
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            return firstPathDir; // Default to directory of the first item.
        }
    }
}
