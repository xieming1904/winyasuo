using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
// using Microsoft.Windows.AppLifecycle; // Not strictly needed if using Environment.GetCommandLineArgs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unpack.Core;
using Unpack.Cli; // For CliHandler and CliInvocationContext
using SharpCompress.Compressors.Deflate;
using SharpCompress.Common;

namespace Unpack
{
    public partial class App : Application
    {
        private Window m_window;
        internal static IntPtr WindowHandle { get; set; }
        // public static List<string> QueuedFilePathsForUI { get; private set; } = null; // Replaced by CliInvocationContext

        public App()
        {
            this.InitializeComponent();
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs launchActivatedEventArgs)
        {
            var commandLineArgs = Environment.GetCommandLineArgs();
            // Skip the first argument, which is the path to the executable itself.
            var argsToParse = commandLineArgs.Skip(1).ToArray();

            // Check if there are any arguments to process for CLI mode
            if (argsToParse.Length > 0)
            {
                Debug.WriteLine($"App.OnLaunched: CLI arguments detected: {string.Join(" ", argsToParse)}");
                var cliHandler = new CliHandler();
                var cliContext = new CliInvocationContext();

                int exitCode = await cliHandler.InvokeAsync(argsToParse, cliContext);
                Debug.WriteLine($"App.OnLaunched: CliHandler.InvokeAsync completed with exit code: {exitCode}");

                if (cliContext.ActionResult == CliActionResult.RequiresUIInteraction)
                {
                    Debug.WriteLine("App.OnLaunched: CLI action requires UI. Launching main window and dialog.");
                    // Ensure main window is created and activated, then call the UI method
                    if (m_window == null) m_window = new MainWindow();
                    WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(m_window);
                    m_window.Activate();

                    // Ensure MainWindow is ready before calling (Loaded event is more robust)
                    // For simplicity here, we assume m_window is ready enough or LaunchCreateArchive... handles it.
                    if (m_window is MainWindow mainWindow && cliContext.PathsForUI != null)
                    {
                        // Call on UI thread if not already on it (OnLaunched should be)
                        mainWindow.DispatcherQueue.TryEnqueue(async () => {
                            await mainWindow.LaunchCreateArchiveDialogWithFilePathsAsync(cliContext.PathsForUI);
                        });
                    }
                    return; // UI path will continue, don't exit app.
                }
                else if (cliContext.ActionResult == CliActionResult.HandledSilent ||
                         cliContext.ActionResult == CliActionResult.HandledErrorSilent)
                {
                    Debug.WriteLine("App.OnLaunched: CLI silent operation handled. Exiting application.");
                    Application.Current.Exit(); // Exit after silent task
                    return;
                }
                // If CliActionResult is NoCliCommand, it means System.CommandLine didn't parse any known command.
                // This could be due to invalid CLI args or args intended for normal app activation (e.g. file association launch).
                // In this case, we fall through to normal GUI launch.
                 Debug.WriteLine("App.OnLaunched: No specific CLI command handled by System.CommandLine, or it was an invalid command. Proceeding with normal GUI launch.");
            }

            // Standard window activation if not handled and exited by CLI logic
            Debug.WriteLine("App.OnLaunched: Standard GUI launch sequence.");
            if (m_window == null) m_window = new MainWindow();
            WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(m_window);
            m_window.Activate();
        }

        // Removed old helper methods for CLI name/dir determination as they are now in CliHandler
        // DetermineOutputArchiveName
        // DetermineOutputDirectory
    }
}
