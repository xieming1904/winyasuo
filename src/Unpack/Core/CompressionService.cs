// NuGet Package: SharpCompress (e.g., version 0.37.2) should be added to the Unpack.csproj project:
// <ItemGroup>
//     <PackageReference Include="SharpCompress" Version="0.37.2" />
// </ItemGroup>

using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
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
    }

    public class CompressionService
    {
        // --- ZIP Methods ---
        public async Task CreateZipArchiveFromSelectionsAsync(
            IEnumerable<string> sourcePaths,
            string zipFilePath,
            CompressionLevel compressionLevel,
            string password)
        {
            if (sourcePaths == null || !sourcePaths.Any())
            {
                Debug.WriteLine("No source paths provided for archiving.");
                throw new ArgumentException("No source paths provided.");
            }
            if (string.IsNullOrWhiteSpace(zipFilePath))
            {
                Debug.WriteLine("Output ZIP file path is not specified.");
                throw new ArgumentException("Output ZIP file path is required.");
            }

            await Task.Run(() =>
            {
                var writerOptions = new ZipWriterOptions(compressionLevel)
                {
                    Password = string.IsNullOrEmpty(password) ? null : password,
                    ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8, Password = Encoding.UTF8 }
                };

                Debug.WriteLine($"Starting ZIP creation: {zipFilePath}. Level: {compressionLevel}. Password: {(string.IsNullOrEmpty(password) ? "No" : "Yes")}");

                try
                {
                    string zipFileDirectory = Path.GetDirectoryName(zipFilePath);
                    if (!string.IsNullOrEmpty(zipFileDirectory) && !Directory.Exists(zipFileDirectory))
                    {
                        Directory.CreateDirectory(zipFileDirectory);
                    }

                    using (var stream = File.Open(zipFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    using (var writer = new ZipWriter(stream, writerOptions))
                    {
                        AddPathsToWriter(writer, sourcePaths);
                    }
                    Debug.WriteLine($"Successfully created ZIP: {zipFilePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating ZIP archive '{zipFilePath}': {ex.ToString()}");
                    if (File.Exists(zipFilePath)) { try { File.Delete(zipFilePath); } catch { /* ignored */ } }
                    throw;
                }
            });
        }

        public async Task ExtractZipArchiveAsync(string zipFilePath,
                                                 string destinationDirectoryPath,
                                                 string password)
        {
            if (string.IsNullOrWhiteSpace(zipFilePath) || !File.Exists(zipFilePath))
            {
                Debug.WriteLine($"Source ZIP file path is invalid or does not exist: {zipFilePath}");
                throw new ArgumentException("Source ZIP file path is invalid or does not exist.", nameof(zipFilePath));
            }
            if (string.IsNullOrWhiteSpace(destinationDirectoryPath))
            {
                Debug.WriteLine($"Destination directory path is not specified: {destinationDirectoryPath}");
                throw new ArgumentException("Destination directory path is required.", nameof(destinationDirectoryPath));
            }

            await Task.Run(() =>
            {
                Debug.WriteLine($"Starting ZIP extraction: {zipFilePath} to {destinationDirectoryPath}. Password: {(string.IsNullOrEmpty(password) ? "No" : "Yes")}");
                try
                {
                    Directory.CreateDirectory(destinationDirectoryPath);

                    var readerOptions = new ReaderOptions
                    {
                        Password = string.IsNullOrEmpty(password) ? null : password,
                        LookForHeader = true,
                        ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8, Password = Encoding.UTF8 }
                    };

                    using (var archive = ZipArchive.Open(zipFilePath, readerOptions))
                    {
                        if (archive.IsEncrypted && string.IsNullOrEmpty(password) && archive.Entries.Any(e => e.IsEncrypted))
                        {
                            Debug.WriteLine($"Archive '{zipFilePath}' is encrypted, but no password was provided.");
                            throw new CryptographicException("Password required but not provided for encrypted entries.");
                        }

                        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                        {
                            Debug.WriteLine($"Extracting ZIP entry: {entry.Key} to {Path.Combine(destinationDirectoryPath, entry.Key)}");
                            entry.WriteToDirectory(destinationDirectoryPath, new ExtractionOptions
                            {
                                ExtractFullPath = true,
                                Overwrite = true
                            });
                        }
                    }
                    Debug.WriteLine($"Successfully extracted ZIP: {zipFilePath} to {destinationDirectoryPath}");
                }
                catch (CryptographicException ex)
                {
                    Debug.WriteLine($"Cryptographic error extracting ZIP '{zipFilePath}' (likely wrong password or unsupported encryption): {ex.Message}");
                    throw new InvalidOperationException("Extraction failed. Invalid password or unsupported encryption method for this ZIP file.", ex);
                }
                catch (InvalidFormatException ex)
                {
                     Debug.WriteLine($"Invalid format error extracting ZIP '{zipFilePath}' (possibly corrupted or not a ZIP): {ex.Message}");
                    throw new InvalidDataException("Extraction failed. The archive is corrupted or not a valid ZIP file.", ex);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error extracting ZIP archive '{zipFilePath}': {ex.ToString()}");
                    throw;
                }
            });
        }

        // --- 7z Methods ---
        public async Task Create7zArchiveFromSelectionsAsync(
            IEnumerable<string> sourcePaths,
            string sevenZipFilePath,
            CompressionLevel compressionLevel,
            bool isSolid,
            SharpCompress.Common.CompressionType sevenZipSpecificCompressionType,
            string password,
            bool encryptHeaders)
        {
            if (sourcePaths == null || !sourcePaths.Any())
            {
                Debug.WriteLine("No source paths provided for 7z archiving.");
                throw new ArgumentException("No source paths provided.");
            }
            if (string.IsNullOrWhiteSpace(sevenZipFilePath))
            {
                Debug.WriteLine("Output 7z file path is not specified.");
                throw new ArgumentException("Output 7z file path is required.");
            }

            await Task.Run(() =>
            {
                string actualPassword = string.IsNullOrEmpty(password) ? null : password;
                bool actualEncryptHeaders = encryptHeaders && (actualPassword != null);

                var writerOptions = new SevenZipWriterOptions
                {
                    CompressionType = sevenZipSpecificCompressionType,
                    Level = compressionLevel,
                    IsSolid = isSolid,
                    EncryptHeaders = actualEncryptHeaders, // Only true if password is set
                    Password = actualPassword,
                    ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 }
                };

                Debug.WriteLine($"Starting 7z creation: {sevenZipFilePath}. Level: {compressionLevel}, Method: {sevenZipSpecificCompressionType}, Solid: {isSolid}, EncryptHeaders: {actualEncryptHeaders}, Password: {(actualPassword == null ? "No" : "Yes")}");

                try
                {
                    string sevenZipFileDirectory = Path.GetDirectoryName(sevenZipFilePath);
                    if (!string.IsNullOrEmpty(sevenZipFileDirectory) && !Directory.Exists(sevenZipFileDirectory))
                    {
                        Directory.CreateDirectory(sevenZipFileDirectory);
                    }

                    using (var stream = File.Open(sevenZipFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    using (var writer = new SevenZipWriter(stream, writerOptions))
                    {
                        AddPathsToWriter(writer, sourcePaths);
                    }
                    Debug.WriteLine($"Successfully created 7z: {sevenZipFilePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating 7z archive '{sevenZipFilePath}': {ex.ToString()}");
                    if (File.Exists(sevenZipFilePath)) { try { File.Delete(sevenZipFilePath); } catch { /* ignored */ } }
                    throw;
                }
            });
        }

        public async Task Extract7zArchiveAsync(
            string sevenZipFilePath,
            string destinationDirectoryPath,
            string password)
        {
            if (string.IsNullOrWhiteSpace(sevenZipFilePath) || !File.Exists(sevenZipFilePath))
            {
                Debug.WriteLine($"Source 7z file path is invalid or does not exist: {sevenZipFilePath}");
                throw new ArgumentException("Source 7z file path is invalid or does not exist.", nameof(sevenZipFilePath));
            }
            if (string.IsNullOrWhiteSpace(destinationDirectoryPath))
            {
                Debug.WriteLine($"Destination directory path is not specified: {destinationDirectoryPath}");
                throw new ArgumentException("Destination directory path is required.", nameof(destinationDirectoryPath));
            }

            await Task.Run(() =>
            {
                Debug.WriteLine($"Starting 7z extraction: {sevenZipFilePath} to {destinationDirectoryPath}. Password: {(string.IsNullOrEmpty(password) ? "No" : "Yes")}");
                try
                {
                    Directory.CreateDirectory(destinationDirectoryPath);

                    var readerOptions = new ReaderOptions
                    {
                        Password = string.IsNullOrEmpty(password) ? null : password,
                        LookForHeader = true, // Useful for some archive types
                        ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8, Password = Encoding.UTF8 } // Default for 7z is usually fine
                    };

                    using (var archive = SevenZipArchive.Open(sevenZipFilePath, readerOptions))
                    {
                        // For 7z, IsEncrypted might be true for the archive, and header encryption is a separate flag.
                        // If headers are encrypted, Open() will likely fail if password is wrong or not provided.
                        // If only content is encrypted, failure might occur on entry access.
                        if (archive.IsEncrypted && string.IsNullOrEmpty(password) && archive.Entries.Any(e => e.IsEncrypted))
                        {
                             Debug.WriteLine($"7z Archive '{sevenZipFilePath}' has encrypted entries, but no password was provided.");
                             throw new CryptographicException("Password required but not provided for encrypted entries.");
                        }
                        // Also check if header encryption was expected but password failed
                        // SharpCompress might throw CryptographicException on Open if headers are encrypted and password is wrong.

                        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                        {
                            Debug.WriteLine($"Extracting 7z entry: {entry.Key} to {Path.Combine(destinationDirectoryPath, entry.Key)}");
                            entry.WriteToDirectory(destinationDirectoryPath, new ExtractionOptions
                            {
                                ExtractFullPath = true,
                                Overwrite = true // Consider making this an option for the user
                            });
                        }
                    }
                    Debug.WriteLine($"Successfully extracted 7z: {sevenZipFilePath} to {destinationDirectoryPath}");
                }
                catch (CryptographicException ex)
                {
                    Debug.WriteLine($"Cryptographic error extracting 7z '{sevenZipFilePath}' (likely wrong password): {ex.Message}");
                    throw new InvalidOperationException("Extraction failed. Invalid password or archive uses unsupported encryption.", ex);
                }
                catch (InvalidFormatException ex)
                {
                     Debug.WriteLine($"Invalid format error extracting 7z '{sevenZipFilePath}' (possibly corrupted or not 7z): {ex.Message}");
                    throw new InvalidDataException("Extraction failed. The archive is corrupted or not a valid 7z file.", ex);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error extracting 7z archive '{sevenZipFilePath}': {ex.ToString()}");
                    throw;
                }
            });
        }

        private void AddPathsToWriter(IWriter writer, IEnumerable<string> sourcePaths)
        {
            string commonBasePath = DetermineCommonBasePath(sourcePaths.ToList());
            Debug.WriteLine($"AddPathsToWriter - Common base path: {commonBasePath ?? "null (root of archive)"}");

            foreach (var path in sourcePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(path))
                {
                    string entryPath = GetRelativeEntryPath(path, commonBasePath, path);
                    Debug.WriteLine($"Adding file: {path} as {entryPath}");
                    writer.Write(entryPath, path, File.GetLastWriteTime(path));
                }
                else if (Directory.Exists(path))
                {
                    Debug.WriteLine($"Adding directory (contents): {path}");
                    string dirRootInZip = commonBasePath == null ? Path.GetFileName(path) : Path.GetRelativePath(commonBasePath, path);
                    if (string.IsNullOrEmpty(dirRootInZip) && path.Length > 0 && path.EndsWith(Path.DirectorySeparatorChar.ToString())) // Handle case like C:\ for a single dir
                    {
                        dirRootInZip = new DirectoryInfo(path).Name;
                    }
                    else if (string.IsNullOrEmpty(dirRootInZip))
                    {
                        dirRootInZip = Path.GetFileName(path); // Fallback
                    }

                    AddDirectoryToWriterRecursive(writer, path, path, dirRootInZip);
                }
            }
        }

        private void AddDirectoryToWriterRecursive(IWriter writer, string directoryPath, string pathToRemoveForRelative, string currentPathInZip)
        {
            var directoryInfo = new DirectoryInfo(directoryPath);
            if (!directoryInfo.Exists) return;

            // Add files in the current directory
            foreach (var file in directoryInfo.GetFiles())
            {
                string relativeFilePath = Path.GetRelativePath(pathToRemoveForRelative, file.FullName);
                string entryPath = Path.Combine(currentPathInZip, relativeFilePath).Replace(Path.DirectorySeparatorChar, '/');
                Debug.WriteLine($"  Adding file from dir: {file.FullName} as {entryPath}");
                writer.Write(entryPath, file.FullName, file.LastWriteTime);
            }

            // Recursively add subdirectories
            foreach (var subDirectory in directoryInfo.GetDirectories())
            {
                string relativeDirPath = Path.GetRelativePath(pathToRemoveForRelative, subDirectory.FullName);
                string entryDirPath = Path.Combine(currentPathInZip, relativeDirPath).Replace(Path.DirectorySeparatorChar, '/');

                // To ensure empty directories are represented, some archive formats/writers might need explicit entries.
                // For SharpCompress IWriter, often paths of files imply directories.
                // If truly empty directories are needed, one might add an entry like "folderName/" with a null stream.
                // For now, this recursive call will add files within subdirectories.
                // writer.Write(entryDirPath + "/", Stream.Null, subDirectory.LastWriteTime); // Example for explicit empty dir

                AddDirectoryToWriterRecursive(writer, subDirectory.FullName, pathToRemoveForRelative, currentPathInZip);
            }
        }

        private string DetermineCommonBasePath(List<string> paths)
        {
            if (paths == null || !paths.Any()) return null;

            // Normalize paths and get directory names if paths are files, or the path itself if it's a directory
            var processedPaths = paths.Select(p =>
                File.Exists(p) ? Path.GetDirectoryName(p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                               : p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            ).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (!processedPaths.Any() || processedPaths.Any(string.IsNullOrEmpty)) return null;

            // If only one unique source directory (or one source file's directory), that's our base.
            if (processedPaths.Count == 1) return processedPaths.First();

            // Find commonality among multiple distinct source directories
            string shortestPath = processedPaths.OrderBy(p => p.Length).First();
            if (string.IsNullOrEmpty(shortestPath)) return null;

            while (!string.IsNullOrEmpty(shortestPath))
            {
                if (processedPaths.All(p => p.StartsWith(shortestPath, StringComparison.OrdinalIgnoreCase)))
                {
                    // Check if it's a drive root like "C:", if so, return null to avoid C:\file.txt becoming \file.txt
                    if (shortestPath.Length == 2 && shortestPath[1] == ':') return null;
                    if (shortestPath.Length == 3 && shortestPath[1] == ':' && (shortestPath[2] == Path.DirectorySeparatorChar || shortestPath[2] == Path.AltDirectorySeparatorChar) ) return null;
                    return shortestPath;
                }
                shortestPath = Path.GetDirectoryName(shortestPath);
            }
            return null;
        }

        private string GetRelativeEntryPath(string fullPath, string commonBasePath, string originalSourcePath)
        {
            string entryPath;
            // If a common base path was found and the current fullPath is within it
            if (commonBasePath != null && fullPath.StartsWith(commonBasePath, StringComparison.OrdinalIgnoreCase))
            {
                string adjustedBasePath = commonBasePath.EndsWith(Path.DirectorySeparatorChar.ToString()) || commonBasePath.EndsWith(Path.AltDirectorySeparatorChar.ToString())
                                          ? commonBasePath
                                          : commonBasePath + Path.DirectorySeparatorChar;
                entryPath = Path.GetRelativePath(adjustedBasePath, fullPath);
            }
            // If originalSourcePath was a directory and fullPath is within it (handles single directory selection)
            else if (Directory.Exists(originalSourcePath) && fullPath.StartsWith(originalSourcePath, StringComparison.OrdinalIgnoreCase))
            {
                 // Contents of the selected directory should appear at the root of the archive
                 entryPath = Path.GetRelativePath(originalSourcePath, fullPath);
            }
            else // Fallback: use the file/directory name as the root entry in the archive
            {
                entryPath = Path.GetFileName(fullPath);
            }
            return entryPath.Replace(Path.DirectorySeparatorChar, '/'); // Use ZIP standard path separators
        }
    }
}
