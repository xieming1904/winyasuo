// NuGet Package: System.CommandLine (e.g., version 2.0.0-beta4.22272.1) needs to be added to Unpack.csproj
// <ItemGroup>
//     <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
// </ItemGroup>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing; // For ParseResult
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unpack.Core;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Common;
using SharpCompress.Readers; // For ReaderOptions in list/test
using SharpCompress.Archives; // For ArchiveFactory in list/test

namespace Unpack.Cli
{
    public enum CliActionResult { HandledSilent, HandledErrorSilent, RequiresUIInteraction, NoCliCommand }
    public class CliInvocationContext { public CliActionResult ActionResult { get; set; } = CliActionResult.NoCliCommand; public IEnumerable<string> PathsForUI { get; set; } }

    public class CliHandler
    {
        private RootCommand _rootCommand;
        private CompressionService _compressionService;

        public CliHandler()
        {
            _compressionService = new CompressionService();
            ConfigureCommands();
        }

        private void ConfigureCommands()
        {
            _rootCommand = new RootCommand("Unpack File Archiver: A modern tool for managing your archives.\nUse context menu commands like --extract-here or general commands like 'add', 'extract', 'list', 'test'.");

            // --- Define Reusable Arguments & Options ---
            var pathsArgumentContextMenu = new Argument<string[]>("paths")
                { Description = "Paths to files or folders for context menu operations.", Arity = ArgumentArity.OneOrMore };

            var archivePathArgument = new Argument<FileInfo>("archive-path", "Path to the archive file.")
                { Arity = ArgumentArity.ExactlyOne }; // For list, test, extract single archive

            var filesToProcessArgument = new Argument<string[]>("files-or-folders")
                { Description = "Files or folders to process (add to archive, or extract from archive).", Arity = ArgumentArity.OneOrMore };

            var passwordOption = new Option<string>(new[] { "-p", "--password" }, "Password for encryption/decryption.");
            var outputDirOption = new Option<DirectoryInfo>(new[] { "-o", "--output-dir" }, "Output directory for extraction.");

            var compressionLevelOption = new Option<string>(
                name: "--level",
                description: "Compression level (Store, Fastest, Fast, Normal, Maximum, Ultra). Default: Normal.",
                getDefaultValue: () => "Normal");

            var archiveTypeOption = new Option<string>(
                name: "--type",
                description: "Archive type (zip, 7z). Guessed from archive name if omitted.");

            // 7z specific options
            var solidOption = new Option<bool>("--solid", "Create solid archive (7z only). Default: false.");
            var sevenZipCompressionMethodOption = new Option<string>(
                name: "--7z-method",
                description: "7z compression method (LZMA2, LZMA, PPMd, BZip2). Default: LZMA2.",
                getDefaultValue: () => "LZMA2");
            var encryptHeadersOption = new Option<bool>("--encrypt-headers", "Encrypt archive headers (7z only, requires password). Default: false.");

            // ZIP specific options
            var zipEncryptionMethodOption = new Option<string>(
                name: "--zip-method",
                description: "ZIP encryption method (AES, ZipCrypto). Default: AES if password, else None.",
                getDefaultValue: () => "AES");


            var sfxOption = new Option<bool>("--sfx", "Create Self-Extracting (SFX) archive. Default: false.");
            var volumeSizeOption = new Option<string>("--volume", "Split into volumes (e.g., 100MB, 1.44MB, 2GB). No splitting by default.");

            // --- Context Menu Commands ---
            var extractHereCommand = new Command("--extract-here", "Extracts selected archive(s) in place.") { pathsArgumentContextMenu };
            extractHereCommand.SetHandler(async (string[] paths, InvocationContext invCtx) => {
                invCtx.ExitCode = 0; Debug.WriteLine($"CLI Handler: --extract-here paths: {string.Join(", ", paths)}");
                foreach (var archivePath in paths) {
                    if (!File.Exists(archivePath)) { Console.Error.WriteLine($"Error: Archive '{archivePath}' not found."); invCtx.ExitCode = 1; continue; }
                    string destDir = Path.GetDirectoryName(archivePath) ?? Environment.CurrentDirectory;
                    try { await _compressionService.ExtractArchiveAsync(archivePath, destDir, null); Console.WriteLine($"Extracted '{archivePath}' to '{destDir}'."); }
                    catch (Exception ex) { Console.Error.WriteLine($"Error extracting {archivePath}: {ex.Message}"); invCtx.ExitCode = 1; }
                }
            }, pathsArgumentContextMenu);
            _rootCommand.AddCommand(extractHereCommand);

            var extractToSubdirCommand = new Command("--extract-to-subdir", "Extracts archive to a subfolder named after it.") { pathsArgumentContextMenu };
            extractToSubdirCommand.SetHandler(async (string[] paths, InvocationContext invCtx) => {
                invCtx.ExitCode = 0; if (!paths.Any()) { Console.Error.WriteLine("Error: No archive path provided."); invCtx.ExitCode = 1; return; }
                string archivePath = paths.First(); Debug.WriteLine($"CLI Handler: --extract-to-subdir path: {archivePath}");
                if (!File.Exists(archivePath)) { Console.Error.WriteLine($"Error: Archive '{archivePath}' not found."); invCtx.ExitCode = 1; return; }
                string archiveNameNoExt = Path.GetFileNameWithoutExtension(archivePath);
                string destDir = Path.Combine(Path.GetDirectoryName(archivePath) ?? Environment.CurrentDirectory, archiveNameNoExt);
                try { Directory.CreateDirectory(destDir); await _compressionService.ExtractArchiveAsync(archivePath, destDir, null); Console.WriteLine($"Extracted '{archivePath}' to '{destDir}'."); }
                catch (Exception ex) { Console.Error.WriteLine($"Error extracting {archivePath} to subdir: {ex.Message}"); invCtx.ExitCode = 1; }
            }, pathsArgumentContextMenu);
            _rootCommand.AddCommand(extractToSubdirCommand);

            var compressZipCommand = new Command("--compress-zip", "Compresses selection to a new ZIP archive (default settings).") { pathsArgumentContextMenu };
            compressZipCommand.SetHandler(async (string[] paths, InvocationContext invCtx) => {
                invCtx.ExitCode = 0; if (!paths.Any()) { Console.Error.WriteLine("Error: No files to compress."); invCtx.ExitCode = 1; return; }
                Debug.WriteLine($"CLI Handler: --compress-zip paths: {string.Join(", ", paths)}");
                string outputArchiveName = DetermineOutputArchiveNameForCli(paths, ".zip");
                string outputDir = DetermineOutputDirectoryForCli(paths);
                string outputFullPath = Path.Combine(outputDir, outputArchiveName);
                try { if(!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir); await _compressionService.CreateZipArchiveFromSelectionsAsync(paths, outputFullPath, CompressionLevel.Default, null, "AES", false, null, false); Console.WriteLine($"Compressed to '{outputFullPath}'."); }
                catch (Exception ex) { Console.Error.WriteLine($"Error compressing to ZIP: {ex.Message}"); invCtx.ExitCode = 1; }
            }, pathsArgumentContextMenu);
            _rootCommand.AddCommand(compressZipCommand);

            var compress7zCommand = new Command("--compress-7z", "Compresses selection to a new 7z archive (default settings).") { pathsArgumentContextMenu };
            compress7zCommand.SetHandler(async (string[] paths, InvocationContext invCtx) => {
                invCtx.ExitCode = 0; if (!paths.Any()) { Console.Error.WriteLine("Error: No files to compress."); invCtx.ExitCode = 1; return; }
                Debug.WriteLine($"CLI Handler: --compress-7z paths: {string.Join(", ", paths)}");
                string outputArchiveName = DetermineOutputArchiveNameForCli(paths, ".7z");
                string outputDir = DetermineOutputDirectoryForCli(paths);
                string outputFullPath = Path.Combine(outputDir, outputArchiveName);
                try { if(!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir); await _compressionService.Create7zArchiveFromSelectionsAsync(paths, outputFullPath, CompressionLevel.Default, false, SharpCompress.Common.CompressionType.LZMA2, null, false, null, false); Console.WriteLine($"Compressed to '{outputFullPath}'."); }
                catch (Exception ex) { Console.Error.WriteLine($"Error compressing to 7z: {ex.Message}"); invCtx.ExitCode = 1; }
            }, pathsArgumentContextMenu);
            _rootCommand.AddCommand(compress7zCommand);

            var addToArchiveUICommand = new Command("--add-to-archive-ui", "Open Unpack UI to add selected files to an archive.") { pathsArgumentContextMenu };
            addToArchiveUICommand.SetHandler((InvocationContext context) => {
                var paths = context.ParseResult.GetValueForArgument(pathsArgumentContextMenu);
                Debug.WriteLine($"CLI Handler: --add-to-archive-ui. Queuing paths for UI.");
                var cliContext = context.BindingContext.GetService(typeof(CliInvocationContext)) as CliInvocationContext;
                if (cliContext != null) { cliContext.ActionResult = CliActionResult.RequiresUIInteraction; cliContext.PathsForUI = paths; }
                context.ExitCode = 0;
            });
            _rootCommand.AddCommand(addToArchiveUICommand);

            // --- New General CLI Commands ---
            var addCommand = new Command("add", "Add files/folders to an archive.") { Aliases = { "a" } };
            addCommand.AddArgument(filesToProcessArgument);
            addCommand.AddOption(archiveOption);
            addCommand.AddOption(passwordOption);
            addCommand.AddOption(compressionLevelOption);
            addCommand.AddOption(archiveTypeOption);
            addCommand.AddOption(solidOption); // 7z
            addCommand.AddOption(sevenZipCompressionMethodOption); //7z
            addCommand.AddOption(encryptHeadersOption); // 7z
            addCommand.AddOption(zipEncryptionMethodOption); // ZIP
            // Conceptual: add a zipEncryptHeadersOption if ever supported distinct from 7z one.
            addCommand.AddOption(sfxOption);
            addCommand.AddOption(volumeSizeOption);
            addCommand.SetHandler(async (InvocationContext context) => {
                var files = context.ParseResult.GetValueForArgument(filesToProcessArgument);
                var archiveFile = context.ParseResult.GetValueForOption(archiveOption);
                var password = context.ParseResult.GetValueForOption(passwordOption);
                var levelStr = context.ParseResult.GetValueForOption(compressionLevelOption);
                var typeStr = context.ParseResult.GetValueForOption(archiveTypeOption);
                var isSolid = context.ParseResult.GetValueForOption(solidOption);
                var method7zStr = context.ParseResult.GetValueForOption(sevenZipCompressionMethodOption);
                var encryptHeaders = context.ParseResult.GetValueForOption(encryptHeadersOption);
                var methodZipStr = context.ParseResult.GetValueForOption(zipEncryptionMethodOption);
                var createSfx = context.ParseResult.GetValueForOption(sfxOption);
                var volSizeStr = context.ParseResult.GetValueForOption(volumeSizeOption);

                Console.WriteLine($"CLI: 'add' to '{archiveFile.FullName}'");
                if (!files.Any()) { Console.Error.WriteLine("No files specified to add."); context.ExitCode = 1; return; }

                string archiveType = string.IsNullOrEmpty(typeStr) ? Path.GetExtension(archiveFile.FullName).TrimStart('.').ToLowerInvariant() : typeStr.ToLowerInvariant();
                var deflateLevel = CompressionSettingsHelper.DeflateCompressionLevelFromString(levelStr);
                var dialogTemp = new CreateArchiveDialog(); // Use its parser for now
                long? volSizeInBytes = dialogTemp.ParseVolumeSizeStringToBytes(volSizeStr);

                try
                {
                    if (archiveType == "zip")
                    {
                        await _compressionService.CreateZipArchiveFromSelectionsAsync(files, archiveFile.FullName, deflateLevel, password, methodZipStr, encryptHeaders, volSizeInBytes, createSfx);
                    }
                    else if (archiveType == "7z")
                    {
                        var sevenZipType = CompressionSettingsHelper.SevenZipCompressionTypeFromString(method7zStr);
                        await _compressionService.Create7zArchiveFromSelectionsAsync(files, archiveFile.FullName, deflateLevel, isSolid, sevenZipType, password, encryptHeaders, volSizeInBytes, createSfx);
                    }
                    else { Console.Error.WriteLine($"Unsupported archive type for add: {archiveType}"); context.ExitCode = 1; return; }
                    Console.WriteLine($"Successfully added to archive '{archiveFile.FullName}'.");
                    context.ExitCode = 0;
                }
                catch (Exception ex) { Console.Error.WriteLine($"Error adding to archive: {ex.Message}"); context.ExitCode = 1; }
            });
            _rootCommand.AddCommand(addCommand);

            var extractCommand = new Command("extract", "Extract files/folders from an archive.") { Aliases = { "x", "e" } };
            extractCommand.AddOption(archiveOption); // Archive to extract
            extractCommand.AddArgument(filesToProcessArgument); // Specific files/folders within archive to extract (optional)
            extractCommand.AddOption(outputDirOption);
            extractCommand.AddOption(passwordOption);
            // TODO: Add option for full paths vs flat extraction to differentiate 'x' and 'e' behavior
            extractCommand.SetHandler(async (InvocationContext context) => {
                var archiveFile = context.ParseResult.GetValueForOption(archiveOption);
                var files = context.ParseResult.GetValueForArgument(filesToProcessArgument); // Files to extract from archive
                var outputDir = context.ParseResult.GetValueForOption(outputDirOption);
                var password = context.ParseResult.GetValueForOption(passwordOption);

                string targetDir = outputDir?.FullName ?? Path.GetDirectoryName(archiveFile.FullName) ?? Environment.CurrentDirectory;
                if (files != null && files.Any()) Console.WriteLine($"CLI: 'extract' specific files (not yet implemented) from '{archiveFile.FullName}' to '{targetDir}'.");
                else Console.WriteLine($"CLI: 'extract' all from '{archiveFile.FullName}' to '{targetDir}'.");

                try
                {
                    // Note: ExtractArchiveAsync currently extracts all. Filtering specific files would be an enhancement.
                    if (files != null && files.Any()) { Console.Error.WriteLine("Extracting specific files not yet implemented in this CLI handler, extracting all."); }
                    await _compressionService.ExtractArchiveAsync(archiveFile.FullName, targetDir, password);
                    Console.WriteLine($"Successfully extracted archive '{archiveFile.FullName}' to '{targetDir}'.");
                    context.ExitCode = 0;
                }
                catch (Exception ex) { Console.Error.WriteLine($"Error extracting archive: {ex.Message}"); context.ExitCode = 1; }
            });
            _rootCommand.AddCommand(extractCommand);

            var listCommand = new Command("list", "List contents of an archive.") { Aliases = { "l" } };
            listCommand.AddArgument(archivePathArgument); // Changed from Option to Argument for `unpack list <archive>`
            listCommand.AddOption(passwordOption);
            listCommand.SetHandler(async (InvocationContext context) => {
                var archiveFile = context.ParseResult.GetValueForArgument(archivePathArgument);
                var password = context.ParseResult.GetValueForOption(passwordOption);
                Console.WriteLine($"CLI: 'list' contents of '{archiveFile.FullName}'.");
                try
                {
                    await Task.Run(() => { // Ensure offload for potentially blocking I/O
                        var readerOptions = new ReaderOptions { Password = password, ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 } };
                        using (var archive = ArchiveFactory.Open(archiveFile.FullName, readerOptions))
                        {
                            Console.WriteLine($"{"Mode",-6} {"Last Modified",-20} {"Size",-12} {"Compressed",-12} {"Name"}");
                            Console.WriteLine($"{"------",-6} {"--------------------",-20} {"------------",-12} {"------------",-12} {"----"}");
                            foreach (var entry in archive.Entries.OrderBy(e => e.Key))
                            {
                                string mode = entry.IsDirectory ? "D" : "F";
                                string sizeStr = entry.IsDirectory ? "" : entry.Size.ToString();
                                string compSizeStr = entry.IsDirectory ? "" : entry.CompressedSize.ToString();
                                string dateStr = entry.LastModifiedTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";
                                Console.WriteLine($"{mode,-6} {dateStr,-20} {sizeStr,-12} {compSizeStr,-12} {entry.Key}");
                            }
                        }
                    });
                    context.ExitCode = 0;
                }
                catch (Exception ex) { Console.Error.WriteLine($"Error listing archive: {ex.Message}"); context.ExitCode = 1; }
            });
            _rootCommand.AddCommand(listCommand);

            var testCommand = new Command("test", "Test archive integrity.") { Aliases = { "t" } };
            testCommand.AddArgument(archivePathArgument); // Changed from Option to Argument
            testCommand.AddOption(passwordOption);
            testCommand.SetHandler(async (InvocationContext context) => {
                var archiveFile = context.ParseResult.GetValueForArgument(archivePathArgument);
                var password = context.ParseResult.GetValueForOption(passwordOption);
                Console.WriteLine($"CLI: 'test' integrity of '{archiveFile.FullName}'.");
                try
                {
                    var (success, message) = await _compressionService.TestArchiveAsync(archiveFile.FullName, password);
                    if (success) Console.WriteLine(message);
                    else Console.Error.WriteLine(message);
                    context.ExitCode = success ? 0 : 1;
                }
                catch (Exception ex) { Console.Error.WriteLine($"Error testing archive: {ex.Message}"); context.ExitCode = 1; }
            });
            _rootCommand.AddCommand(testCommand);
        }

        public async Task<int> InvokeAsync(string[] args, CliInvocationContext cliContext)
        {
            // System.CommandLine's BindingContext allows passing services/context to handlers.
            // We'll use a custom service for CliInvocationContext.
            _rootCommand.Handler = CommandHandler.Create(() => { /* Default root handler if no subcommand, can show help */ });

            // A common way to provide context to handlers in System.CommandLine
            // is to use a custom Binder or to have handlers access static/singleton services.
            // For simplicity here, the --add-to-archive-ui handler will update the passed cliContext directly.
            // This requires the handler to have access to cliContext.
            // We can achieve this by making cliContext available in the handler's closure or via a custom service in BindingContext.
            // The InvocationContext passed to handlers has a BindingContext.

            // For the specific case of --add-to-archive-ui needing to communicate back to App.xaml.cs,
            // we ensure its handler has access to the cliContext instance passed to this InvokeAsync method.
            // This is simulated by the handler for --add-to-archive-ui using BindingContext.GetService.

            return await _rootCommand.InvokeAsync(args);
        }

        private string DetermineOutputArchiveNameForCli(IEnumerable<string> paths, string extension)
        {
            if (paths == null || !paths.Any()) return "Archive" + extension;
            string namePart;
            if (paths.Count() == 1) { var firstPath = paths.First(); namePart = Path.GetFileNameWithoutExtension(firstPath); if (Directory.Exists(firstPath)) namePart = new DirectoryInfo(firstPath).Name; }
            else { string commonParent = Path.GetDirectoryName(paths.First()); if (paths.All(p => (Path.GetDirectoryName(p) ?? "").Equals(commonParent, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(commonParent)) namePart = new DirectoryInfo(commonParent).Name; else namePart = "Archive"; }
            return namePart + extension;
        }
        private string DetermineOutputDirectoryForCli(IEnumerable<string> paths)
        {
            if (paths == null || !paths.Any()) return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string firstPathDir = Path.GetDirectoryName(paths.First());
            return string.IsNullOrEmpty(firstPathDir) ? Environment.CurrentDirectory : firstPathDir;
        }
    }
}
