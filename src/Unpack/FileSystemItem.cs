using System;
using SharpCompress.Archives; // For IArchiveEntry

namespace Unpack
{
    public class FileSystemItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; } // For local files: full disk path. For archive entries: Key/Path within archive.
        public string ItemType { get; set; } // "Folder", "File", "Drive", or file extension for archive entries
        public string Icon { get; set; }     // Segoe Fluent Icon glyph
        public DateTimeOffset DateModified { get; set; }
        public long Size { get; set; } // Bytes, for files or uncompressed size for archive entries

        public bool IsArchiveEntry { get; set; } = false;
        public string ArchiveFilePath { get; set; } // Path to the parent archive file, if this is an entry
        public IArchiveEntry ArchiveEntry { get; set; } // The actual SharpCompress entry object

        public string DisplayDateModified => DateModified == DateTimeOffset.MinValue ? "" : DateModified.ToString("g"); // Short date and time
        public string DisplayItemType => ItemType;

        public string DisplaySize
        {
            get
            {
                if (ItemType == "Folder" || ItemType == "Drive") return ""; // No size for folders or drives in this column
                if (Size == -1) return ""; // Size not calculated or error

                string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
                int i = 0;
                double dblSByte = Size;
                if (Size == 0 && ItemType != "Folder" && ItemType != "Drive") return "0 B"; // Show 0 B for empty files
                if (Size == 0) return ""; // Don't show 0 B for folders/drives

                while (i < suffixes.Length - 1 && dblSByte >= 1024)
                {
                    dblSByte /= 1024;
                    i++;
                }
                return String.Format("{0:0.##} {1}", dblSByte, suffixes[i]);
            }
        }

        // Constructor for local file system items
        public FileSystemItem(string name, string fullPath, string itemType, string iconGlyph, DateTimeOffset dateModified, long size = 0)
        {
            Name = name;
            FullPath = fullPath;
            ItemType = itemType;
            Icon = iconGlyph;
            DateModified = dateModified;
            Size = size;
            IsArchiveEntry = false;
            ArchiveEntry = null;
            ArchiveFilePath = null;
        }

        // Constructor for archive entries
        public FileSystemItem(IArchiveEntry entry, string parentArchiveFilePath)
        {
            Name = System.IO.Path.GetFileName(entry.Key.TrimEnd('/', System.IO.Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(Name) && entry.IsDirectory) // Handle root or parent directory references
            {
                // For a directory like "FolderA/", Path.GetFileName might return "FolderA".
                // If entry.Key is "Photos/Holiday/" Name would be "Holiday"
                // If Key is "Photos/", Name would be "Photos"
                // This logic might need refinement based on how entry.Key presents directory names
                var parts = entry.Key.TrimEnd('/', System.IO.Path.AltDirectorySeparatorChar).Split(new[] { '/', System.IO.Path.AltDirectorySeparatorChar });
                Name = parts.LastOrDefault(p => !string.IsNullOrEmpty(p)) ?? "[Unknown Folder]";
            }

            FullPath = entry.Key; // Path within the archive
            ItemType = entry.IsDirectory ? "Folder" : (System.IO.Path.GetExtension(entry.Key)?.TrimStart('.').ToUpperInvariant() + " File" ?? "File");
            Icon = GetIconForArchiveEntry(entry);
            DateModified = entry.LastModifiedTime?.ToLocalTime() ?? DateTimeOffset.MinValue;
            Size = entry.Size; // Uncompressed size
            IsArchiveEntry = true;
            ArchiveEntry = entry;
            ArchiveFilePath = parentArchiveFilePath;
        }

        private static string GetIconForArchiveEntry(IArchiveEntry entry)
        {
            if (entry.IsDirectory) return "\xE8D7"; // Folder icon

            string extension = System.IO.Path.GetExtension(entry.Key)?.ToLowerInvariant();
            switch (extension)
            {
                case ".txt": case ".log": case ".ini": case ".config": case ".md": case ".xml": case ".json": case ".cs": case ".java": case ".py": case ".js":
                    return "\xE7C3"; // Text file icon (generic document)
                case ".jpg": case ".jpeg": case ".png": case ".gif": case ".bmp": case ".tiff":
                    return "\xEB9F"; // Image file icon
                // Add more specific icons based on common types if desired
                default:
                    return "\xE7C3"; // Generic file icon
            }
        }
    }
}
