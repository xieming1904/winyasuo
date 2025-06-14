using System;

namespace Unpack
{
    public class FileSystemItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string ItemType { get; set; } // "Folder", "File", "Drive"
        public string Icon { get; set; } // Segoe Fluent Icon glyph: Folder = &#xE8D7; (E8D7), File = &#xE7C3; (E7C3), Drive = &#xE8A7; (E8A7)
        public DateTimeOffset DateModified { get; set; }
        public long Size { get; set; } // Bytes, for files

        public string DisplayDateModified => DateModified.ToString("g"); // Short date and time
        public string DisplayItemType => ItemType; // Could be localized or more descriptive later

        public string DisplaySize
        {
            get
            {
                if (ItemType == "File")
                {
                    if (Size == -1) return ""; // Size not calculated or error
                    string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
                    int i = 0;
                    double dblSByte = Size;
                    if (Size == 0) return "0 B";
                    while (i < suffixes.Length - 1 && dblSByte >= 1024)
                    {
                        dblSByte /= 1024;
                        i++;
                    }
                    return String.Format("{0:0.##} {1}", dblSByte, suffixes[i]);
                }
                return ""; // No size for folders or drives in this column
            }
        }

        public FileSystemItem(string name, string fullPath, string itemType, string iconGlyph, DateTimeOffset dateModified, long size = 0)
        {
            Name = name;
            FullPath = fullPath;
            ItemType = itemType;
            Icon = iconGlyph;
            DateModified = dateModified;
            Size = size;
        }
    }
}
