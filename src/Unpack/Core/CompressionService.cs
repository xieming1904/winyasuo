// NuGet Package: SharpCompress (e.g., version 0.37.2) should be added to the Unpack.csproj project:
// <ItemGroup>
//     <PackageReference Include="SharpCompress" Version="0.37.2" />
// </ItemGroup>

// --- SharpCompress Research Notes ---
// SFX Creation:
// SharpCompress itself does not provide direct functionality to create SFX (Self-Extracting) archives.
// SFX creation typically involves concatenating a pre-existing SFX stub (an .exe module) with the
// actual archive data. This process is external to SharpCompress's core library functions.
// Unpack will need to provide its own SFX stub(s) (e.g., for ZIP, for 7z) and implement the
// concatenation logic.

// Split Archive Creation:
// - ZIP: Standard ZIP format has limited support for true multi-volume splitting in a way that's
//   universally compatible and controllable by specific volume sizes directly through simple library options.
//   SharpCompress's ZipWriter writes to a single stream and does not have built-in options for
//   splitting into fixed-size volumes like .zip, .z01, .z02. This would require manual chunking of
//   output streams, which is complex and not standard for ZipWriter.
// - 7z: The 7z format itself supports multi-volume archives. SharpCompress's SevenZipWriter may
//   or may not expose this directly via SevenZipWriterOptions.
//   *Update:* SharpCompress's `SevenZipWriter` and `ZipWriter` write to a single output `Stream`.
//   They do not have built-in options to automatically split the output into multiple files of a specific volume size.
//   This means true multi-volume splitting would require a custom solution to manage multiple output streams.

using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.SevenZip;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using SharpCompress.Writers.SevenZip;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Readers;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System;

namespace Unpack.Core
{
    public static class CompressionSettingsHelper
    {
        public static CompressionLevel DeflateCompressionLevelFromString(string levelString)
        {
            return levelString?.ToLowerInvariant() switch
            {
                "store (no compression)" => CompressionLevel.None,
                "fastest" => CompressionLevel.BestSpeed,
                "fast" => CompressionLevel.Level2,
                "normal" => CompressionLevel.Default,
                "maximum" => CompressionLevel.Level8,
                "ultra" => CompressionLevel.BestCompression,
                _ => CompressionLevel.Default,
            };
        }

        public static SharpCompress.Common.CompressionType SevenZipCompressionTypeFromString(string typeString)
        {
            return typeString?.ToUpperInvariant() switch
            {
                "LZMA2" => SharpCompress.Common.CompressionType.LZMA2,
                "LZMA" => SharpCompress.Common.CompressionType.LZMA,
                "PPMD" => SharpCompress.Common.CompressionType.PPMd,
                "BZIP2" => SharpCompress.Common.CompressionType.BZip2,
                _ => SharpCompress.Common.CompressionType.LZMA2,
            };
        }

        public static SharpCompress.Common.Zip.ZipEncryptionMethod ZipEncryptionMethodFromString(string methodString)
        {
            return methodString?.ToUpperInvariant() switch
            {
                "AES" => SharpCompress.Common.Zip.ZipEncryptionMethod.Aes,
                "AES-256" => SharpCompress.Common.Zip.ZipEncryptionMethod.Aes,
                "AES128" => SharpCompress.Common.Zip.ZipEncryptionMethod.Aes,
                "AES192" => SharpCompress.Common.Zip.ZipEncryptionMethod.Aes,
                "ZIPCRYPTO (LEGACY)" => SharpCompress.Common.Zip.ZipEncryptionMethod.PkzipWeak,
                "ZIPCRYPTO" => SharpCompress.Common.Zip.ZipEncryptionMethod.PkzipWeak,
                _ => SharpCompress.Common.Zip.ZipEncryptionMethod.None,
            };
        }
    }

    public class CompressionService
    {
        // --- Archive Test & Repair ---
        public async Task<(bool success, string message)> TestArchiveAsync(string archiveFilePath, string password)
        {
            return await Task.Run(() =>
            {
                var readerOptions = new ReaderOptions
                {
                    Password = string.IsNullOrEmpty(password) ? null : password,
                    LookForHeader = true,
                    ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8, Password = Encoding.UTF8 }
                };
                List<string> errors = new List<string>();
                int testedCount = 0;
                long totalSize = 0;
                Stopwatch sw = Stopwatch.StartNew();

                try
                {
                    using (var archive = ArchiveFactory.Open(archiveFilePath, readerOptions))
                    {
                        if (!archive.Entries.Any(e => !e.IsDirectory))
                            return (true, "Archive is empty or contains no file entries to test.");

                        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                        {
                            try
                            {
                                totalSize += entry.Size;
                                using (var stream = entry.OpenEntryStream())
                                {
                                    // Read the entire stream to ensure data integrity
                                    byte[] buffer = new byte[65536]; // 64KB buffer
                                    while (stream.Read(buffer, 0, buffer.Length) > 0) { }
                                }
                                // SharpCompress IArchiveEntry `IsComplete` might be useful if it's reliable after stream read.
                                // Also, entry.Crc could be validated if the archive format stores CRCs per entry and SharpCompress exposes expected vs actual.
                                // For now, successfully reading the stream is our primary test.
                                testedCount++;
                            }
                            catch (Exception entryEx)
                            {
                                Debug.WriteLine($"Error testing entry '{entry.Key}': {entryEx.Message}");
                                errors.Add($"Error in file '{entry.Key}': {entryEx.Message}");
                            }
                        }
                    }
                    sw.Stop();
                    string duration = sw.ElapsedMilliseconds < 1000 ? $"{sw.ElapsedMilliseconds} ms" : $"{sw.Elapsed.TotalSeconds:F2} s";

                    if (errors.Any())
                    {
                        return (false, $"Test completed in {duration} with {errors.Count} error(s) out of {testedCount} file entries tested. Total size: {FormatBytes(totalSize)}.
Errors:
" + string.Join("
", errors.Take(5))); // Show first 5 errors
                    }
                    return (true, $"Test completed successfully in {duration}. {testedCount} file entries verified. Total size: {FormatBytes(totalSize)}.");
                }
                catch (CryptographicException cex) { return (false, $"Test failed: Password incorrect or required. ({cex.Message})"); }
                catch (InvalidFormatException ifex) { return (false, $"Test failed: Invalid or unsupported archive format. ({ifex.Message})"); }
                catch (Exception ex) { return (false, $"Test failed: An unexpected error occurred. ({ex.Message})"); }
            });
        }

        public async Task<(bool success, string message, int recoveredEntries)> RepairZipArchiveAsync(string corruptedZipFilePath, string repairedZipFilePath)
        {
            return await Task.Run(() =>
            {
                int recoveredCount = 0;
                var readerOptions = new ReaderOptions { LookForHeader = true, ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 } };
                // Password is not typically used for repair in this context, as we're salvaging structure and data where possible.
                // If an entry is encrypted and we don't have password, it won't be salvageable.

                try
                {
                    // Ensure output directory for repaired archive exists
                    string repairedDir = Path.GetDirectoryName(repairedZipFilePath);
                    if (!string.IsNullOrEmpty(repairedDir) && !Directory.Exists(repairedDir))
                    {
                        Directory.CreateDirectory(repairedDir);
                    }

                    List<IArchiveEntry> salvageableEntries = new List<IArchiveEntry>();
                    try
                    {
                        // Attempt to open the archive, even if corrupted. SharpCompress might list some entries.
                        using (var sourceArchive = ArchiveFactory.Open(corruptedZipFilePath, readerOptions))
                        {
                            foreach (var entry in sourceArchive.Entries)
                            {
                                if (!entry.IsDirectory && !entry.IsEncrypted) // Only attempt to recover non-encrypted, non-directory entries for simplicity
                                {
                                    try
                                    {
                                        // A simple check: if we can open the stream, consider it potentially salvageable.
                                        // A more robust check might try to read a small part of it.
                                        using (var stream = entry.OpenEntryStream())
                                        {
                                            if (stream.CanRead) // Basic check
                                            {
                                                salvageableEntries.Add(entry);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Skipping unreadable or problematic entry during repair scan: {entry.Key}. Error: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        Debug.WriteLine($"Could not open or fully scan corrupted archive '{corruptedZipFilePath}' for repair. Attempting recovery of what was found. Error: {ex.Message}");
                        // If salvageableEntries is empty here, it means the archive was too corrupt to even list entries.
                    }


                    if (!salvageableEntries.Any())
                    {
                        return (false, "No recoverable file entries found or archive is too severely damaged to open.", 0);
                    }

                    Debug.WriteLine($"Found {salvageableEntries.Count} potentially salvageable entries in '{corruptedZipFilePath}'.");

                    // Create a new archive and write salvageable entries
                    var writerOptions = new ZipWriterOptions(CompressionLevel.Default) // Use default compression for repaired
                    {
                        ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 }
                    };

                    using (var repairedStream = File.Open(repairedZipFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var writer = new ZipWriter(repairedStream, writerOptions))
                    {
                        foreach (var entry in salvageableEntries)
                        {
                            try
                            {
                                Debug.WriteLine($"Attempting to recover entry: {entry.Key}");
                                // Re-open stream from original archive for this entry (important: sourceArchive is disposed)
                                // This requires re-opening the source archive per entry, or a more complex stream copy if sourceArchive is kept open.
                                // For simplicity and to avoid issues with partially open streams from a corrupt archive,
                                // we'll try to extract to a memory stream then add to new archive.
                                using (var sourceArchiveForEntry = ArchiveFactory.Open(corruptedZipFilePath, readerOptions)) // Re-open for each entry
                                {
                                    var specificEntry = sourceArchiveForEntry.Entries.FirstOrDefault(e => e.Key == entry.Key && !e.IsDirectory);
                                    if (specificEntry != null)
                                    {
                                        using (var entryStream = specificEntry.OpenEntryStream())
                                        {
                                            // Add to new archive.
                                            writer.Write(specificEntry.Key, entryStream, specificEntry.LastModifiedTime);
                                        }
                                        recoveredCount++;
                                    }
                                }
                            }
                            catch (Exception entryEx)
                            {
                                Debug.WriteLine($"Failed to recover entry '{entry.Key}' during repair: {entryEx.Message}");
                                // Continue to next entry
                            }
                        }
                    }

                    if (recoveredCount > 0)
                        return (true, $"Repair attempt complete. {recoveredCount} out of {salvageableEntries.Count} potential entries recovered to '{Path.GetFileName(repairedZipFilePath)}'.", recoveredCount);
                    else
                        return (false, "Repair attempt complete, but no entries could be successfully recovered.", 0);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Repair attempt failed for '{corruptedZipFilePath}': {ex.Message}");
                    if (File.Exists(repairedZipFilePath)) { try { File.Delete(repairedZipFilePath); } catch { /* ignored */ } }
                    return (false, $"Repair attempt failed: {ex.Message}", recoveredCount);
                }
            });
        }


        // --- ZIP Methods ---
        public async Task CreateZipArchiveFromSelectionsAsync(
            IEnumerable<string> sourcePaths,
            string zipFilePath,
            CompressionLevel compressionLevel,
            string password,
            string zipEncryptionMethodString,
            bool encryptCentralDirectory,
            long? volumeSizeInBytes,
            bool createSfx)
        {
            if (sourcePaths == null || !sourcePaths.Any()) throw new ArgumentException("No source paths provided.");
            if (string.IsNullOrWhiteSpace(zipFilePath)) throw new ArgumentException("Output ZIP file path is required.");

            await Task.Run(async () =>
            {
                string actualPassword = string.IsNullOrEmpty(password) ? null : password;
                var zipWriterOptions = new ZipWriterOptions(compressionLevel)
                { Password = actualPassword, ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8, Password = Encoding.UTF8 }};

                if (actualPassword != null)
                {
                    var selectedEncryptionMethod = CompressionSettingsHelper.ZipEncryptionMethodFromString(zipEncryptionMethodString);
                    if (selectedEncryptionMethod == ZipEncryptionMethod.Aes) zipWriterOptions.Encryption = ZipEncryptionMethod.Aes;
                    else zipWriterOptions.Encryption = ZipEncryptionMethod.PkzipWeak;
                } else { zipWriterOptions.Encryption = ZipEncryptionMethod.None; }

                Debug.WriteLine($"Starting ZIP creation: {zipFilePath}. SFX: {createSfx}. Split: {volumeSizeInBytes?.ToString() ?? "No"}. Encryption: {zipWriterOptions.Encryption}.");
                string tempArchiveFilePath = (createSfx || volumeSizeInBytes.HasValue) ? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip") : zipFilePath;

                try
                {
                    string archiveFileDirectory = Path.GetDirectoryName(tempArchiveFilePath);
                    if (!string.IsNullOrEmpty(archiveFileDirectory) && !Directory.Exists(archiveFileDirectory)) Directory.CreateDirectory(archiveFileDirectory);

                    if (volumeSizeInBytes.HasValue) Debug.WriteLine("ZIP Splitting: Not directly supported by SharpCompress ZipWriter.");

                    using (var stream = File.Open(tempArchiveFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    using (var writer = new ZipWriter(stream, zipWriterOptions))
                    { AddPathsToWriter(writer, sourcePaths); }

                    if (createSfx)
                    { await ConcatenateSfxAsync(tempArchiveFilePath, zipFilePath, "zipSfxModule.exe"); try { File.Delete(tempArchiveFilePath); } catch {} }
                    else if (tempArchiveFilePath != zipFilePath) { File.Move(tempArchiveFilePath, zipFilePath, true); } // If not SFX but temp was used (e.g. for future splitting)

                    Debug.WriteLine($"Successfully processed ZIP: {zipFilePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating ZIP archive '{zipFilePath}': {ex.ToString()}");
                    if (File.Exists(tempArchiveFilePath)) { try { File.Delete(tempArchiveFilePath); } catch { } }
                    if (createSfx && File.Exists(zipFilePath)) { try { File.Delete(zipFilePath); } catch { } }
                    throw;
                }
            });
        }

        [Obsolete("Use generic ExtractArchiveAsync instead for ZIP files.")]
        public async Task ExtractZipArchiveAsync(string zipFilePath, string destinationDirectoryPath, string password)
        { await ExtractArchiveAsync(zipFilePath, destinationDirectoryPath, password); }

        // --- 7z Methods ---
        public async Task Create7zArchiveFromSelectionsAsync(
            IEnumerable<string> sourcePaths,
            string sevenZipFilePath,
            CompressionLevel compressionLevel,
            bool isSolid,
            SharpCompress.Common.CompressionType sevenZipSpecificCompressionType,
            string password,
            bool encryptHeaders,
            long? volumeSizeInBytes,
            bool createSfx)
        {
            if (sourcePaths == null || !sourcePaths.Any()) throw new ArgumentException("No source paths provided.");
            if (string.IsNullOrWhiteSpace(sevenZipFilePath)) throw new ArgumentException("Output 7z file path is required.");

            await Task.Run(async () =>
            {
                string actualPassword = string.IsNullOrEmpty(password) ? null : password;
                bool actualEncryptHeaders = (actualPassword != null) && encryptHeaders;
                var writerOptions = new SevenZipWriterOptions
                { CompressionType = sevenZipSpecificCompressionType, Level = compressionLevel, IsSolid = isSolid, EncryptHeaders = actualEncryptHeaders, Password = actualPassword, ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 } };
                Debug.WriteLine($"Starting 7z creation: {sevenZipFilePath}. SFX: {createSfx}. Split: {volumeSizeInBytes?.ToString() ?? "No"}. Method: {sevenZipSpecificCompressionType}, Solid: {isSolid}, EncryptHeaders: {actualEncryptHeaders}");
                string tempArchiveFilePath = (createSfx || volumeSizeInBytes.HasValue) ? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".7z") : sevenZipFilePath;
                try
                {
                    string archiveFileDirectory = Path.GetDirectoryName(tempArchiveFilePath);
                    if (!string.IsNullOrEmpty(archiveFileDirectory) && !Directory.Exists(archiveFileDirectory)) Directory.CreateDirectory(archiveFileDirectory);
                    if (volumeSizeInBytes.HasValue) Debug.WriteLine("7z Splitting: Not directly supported by SharpCompress SevenZipWriter options.");
                    using (var stream = File.Open(tempArchiveFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    using (var writer = new SevenZipWriter(stream, writerOptions))
                    { AddPathsToWriter(writer, sourcePaths); }
                    if (createSfx)
                    { await ConcatenateSfxAsync(tempArchiveFilePath, sevenZipFilePath, "7zSfxModule.exe"); try { File.Delete(tempArchiveFilePath); } catch {} }
                    else if (tempArchiveFilePath != sevenZipFilePath) { File.Move(tempArchiveFilePath, sevenZipFilePath, true); }
                    Debug.WriteLine($"Successfully processed 7z: {sevenZipFilePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating 7z archive '{sevenZipFilePath}': {ex.ToString()}");
                    if (File.Exists(tempArchiveFilePath)) { try { File.Delete(tempArchiveFilePath); } catch { } }
                    if (createSfx && File.Exists(sevenZipFilePath)) { try { File.Delete(sevenZipFilePath); } catch { } }
                    throw;
                }
            });
        }

        [Obsolete("Use generic ExtractArchiveAsync instead for 7z files.")]
        public async Task Extract7zArchiveAsync(string sevenZipFilePath, string destinationDirectoryPath, string password)
        { await ExtractArchiveAsync(sevenZipFilePath, destinationDirectoryPath, password); }

        private async Task ConcatenateSfxAsync(string tempArchiveFile, string finalSfxExePath, string sfxModuleName)
        {
            string sfxModulesDir = Path.Combine(AppContext.BaseDirectory, "SFXModules");
            // In a packaged app, AppContext.BaseDirectory might be restricted. Consider Package.Current.InstalledLocation.Path
            // For simplicity in this step, assuming SFXModules is discoverable relative to BaseDirectory.
            // In a real MSIX packaged app, these would be part of your package content.
            if (!Directory.Exists(sfxModulesDir)) Directory.CreateDirectory(sfxModulesDir); // Ensure it exists for placeholder modules

            string sfxModulePath = Path.Combine(sfxModulesDir, sfxModuleName);

            if (!File.Exists(sfxModulePath))
            {
                Debug.WriteLine($"SFX Module not found at {sfxModulePath}. Creating placeholder module for testing.");
                // Create a dummy SFX module for testing if it doesn't exist.
                // In a real app, this module would be a pre-compiled executable.
                try { File.WriteAllText(sfxModulePath, $"#!/bin/sh\necho This is a placeholder SFX module for {sfxModuleName}\necho Archive contents would be below this line."); }
                catch (Exception ex_sfx) { Debug.WriteLine($"Could not create placeholder SFX module: {ex_sfx.Message}"); }
                // Fallback: just copy/rename the temp archive to the final path (without .exe)
                string finalArchivePathWithoutExe = Path.ChangeExtension(finalSfxExePath, Path.GetExtension(tempArchiveFile));
                File.Move(tempArchiveFile, finalArchivePathWithoutExe, true);
                throw new FileNotFoundException($"SFX module '{sfxModuleName}' not found and placeholder could not be reliably used. Archive created without SFX at '{finalArchivePathWithoutExe}'.");
            }

            Debug.WriteLine($"Concatenating SFX module {sfxModulePath} with archive {tempArchiveFile} to create {finalSfxExePath}");
            try
            {
                string finalDir = Path.GetDirectoryName(finalSfxExePath);
                if(!string.IsNullOrEmpty(finalDir) && !Directory.Exists(finalDir)) Directory.CreateDirectory(finalDir);
                using (var sfxStream = File.Create(finalSfxExePath))
                {
                    using (var moduleStream = File.OpenRead(sfxModulePath)) { await moduleStream.CopyToAsync(sfxStream); }
                    using (var archiveStream = File.OpenRead(tempArchiveFile)) { await archiveStream.CopyToAsync(sfxStream); }
                }
                Debug.WriteLine($"SFX file created: {finalSfxExePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during SFX concatenation: {ex.ToString()}");
                string fallbackPath = Path.ChangeExtension(finalSfxExePath, Path.GetExtension(tempArchiveFile));
                if (File.Exists(tempArchiveFile) && !File.Exists(fallbackPath)) File.Copy(tempArchiveFile, fallbackPath, true);
                throw new IOException($"Failed to create SFX file. Archive may be available at '{fallbackPath}'. Error: {ex.Message}", ex);
            }
        }
        private void AddPathsToWriter(IWriter writer, IEnumerable<string> sourcePaths)
        {
            string commonBasePath = DetermineCommonBasePath(sourcePaths.ToList());
            Debug.WriteLine($"AddPathsToWriter - Common base path: {commonBasePath ?? "null (root of archive)"}");
            foreach (var path in sourcePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(path)) { string entryPath = GetRelativeEntryPath(path, commonBasePath, path); writer.Write(entryPath, path, File.GetLastWriteTime(path)); }
                else if (Directory.Exists(path))
                { string dirRootInZip = commonBasePath == null ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : Path.GetRelativePath(commonBasePath, path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                  if (string.IsNullOrEmpty(dirRootInZip) && path.Length > 0 && (path.EndsWith(Path.DirectorySeparatorChar.ToString()) || path.EndsWith(Path.AltDirectorySeparatorChar.ToString())) ) dirRootInZip = new DirectoryInfo(path).Name;
                  else if (string.IsNullOrEmpty(dirRootInZip)) dirRootInZip = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                  AddDirectoryToWriterRecursive(writer, path, path, dirRootInZip); }
            }
        }
        private void AddDirectoryToWriterRecursive(IWriter writer, string directoryPath, string rootForRelativeCalc, string currentPathInArchive)
        {
            var directoryInfo = new DirectoryInfo(directoryPath); if (!directoryInfo.Exists) return;
            foreach (var file in directoryInfo.GetFiles()) { string relPath = Path.GetRelativePath(rootForRelativeCalc, file.FullName); string entryPath = Path.Combine(currentPathInArchive, relPath).Replace(Path.DirectorySeparatorChar, '/'); writer.Write(entryPath, file.FullName, file.LastWriteTime); }
            foreach (var subDir in directoryInfo.GetDirectories()) { string relDirPath = Path.GetRelativePath(rootForRelativeCalc, subDir.FullName); string entryDirPath = Path.Combine(currentPathInArchive, relDirPath).Replace(Path.DirectorySeparatorChar, '/'); AddDirectoryToWriterRecursive(writer, subDir.FullName, rootForRelativeCalc, currentPathInArchive); }
        }
        private string DetermineCommonBasePath(List<string> paths)
        {
            if (paths == null || !paths.Any()) return null;
            var pPaths = paths.Select(p => File.Exists(p) ? Path.GetDirectoryName(p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (!pPaths.Any() || pPaths.Any(string.IsNullOrEmpty)) return null; if (pPaths.Count == 1) { var sP = pPaths.First(); if (sP.Length <= 3 && sP.Contains(":")) return null; return sP; } /* Avoid C:\ as base */
            string shortest = pPaths.OrderBy(p=>p.Length).First(); if(string.IsNullOrEmpty(shortest)) return null;
            while(!string.IsNullOrEmpty(shortest)){ if(pPaths.All(p=>p.StartsWith(shortest,StringComparison.OrdinalIgnoreCase))){ if (shortest.Length <= 3 && shortest.Contains(":")) return null; return shortest; } shortest = Path.GetDirectoryName(shortest); } return null;
        }
        private string GetRelativeEntryPath(string fullPath, string commonBasePath, string originalSourcePath)
        {
            string entryPath;
            if (commonBasePath != null && fullPath.StartsWith(commonBasePath, StringComparison.OrdinalIgnoreCase))
            { string adjBase = commonBasePath.EndsWith(Path.DirectorySeparatorChar.ToString()) || commonBasePath.EndsWith(Path.AltDirectorySeparatorChar.ToString()) ? commonBasePath : commonBasePath + Path.DirectorySeparatorChar; entryPath = Path.GetRelativePath(adjBase, fullPath); }
            else if (Directory.Exists(originalSourcePath) && fullPath.StartsWith(originalSourcePath, StringComparison.OrdinalIgnoreCase))
            { entryPath = Path.GetRelativePath(originalSourcePath, fullPath); if (commonBasePath == null) { entryPath = Path.Combine(Path.GetFileName(originalSourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), entryPath); } }
            else { entryPath = Path.GetFileName(fullPath); }
            return entryPath.Replace(Path.DirectorySeparatorChar, '/');
        }
        private string FormatBytes(long bytes) // Helper for TestArchiveAsync message
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" }; int i = 0; double dblSByte = bytes;
            if (bytes == 0) return "0 B";
            while (i < suffixes.Length - 1 && dblSByte >= 1024) { dblSByte /= 1024; i++; }
            return String.Format("{0:0.##} {1}", dblSByte, suffixes[i]);
        }
    }
}
