# Unpack - Advanced Features Design

## Introduction

This document outlines the design specifications for advanced functionalities within the Unpack application. The goal is to cater to professional users and power users who require more granular control over compression, extraction, and archive management processes. This document builds upon the foundational UI/UX established in the "Unpack - Core Concept and UI Design" document. The features described herein aim to enhance Unpack's capabilities while maintaining its core principles of intuitiveness and adherence to Fluent Design.

## Research Summary: Implementation Approaches for Advanced Features

Research into popular archiving software (e.g., 7-Zip, WinRAR, PeaZip) provided key insights that informed the design of Unpack's advanced features:

*   **Multi-core Compression:** Users expect significant speed improvements. The most common configurable option is the number of CPU threads (with "Auto" being a preferred default). Formats like 7z (LZMA2) are inherently designed for better parallelization than traditional ZIP (Deflate), though modern archivers work around this by compressing multiple files concurrently.
*   **Broad Format Support (Decompression):** Leveraging established libraries like 7-Zip's codebase (via `7z.dll`) or `libarchive` is a common and effective strategy to support a wide array of formats (RAR, TAR, GZ, ISO, WIM, etc.) without reimplementing each one. UnRAR source is available for RAR decompression.
*   **In-Archive Preview:** This is typically a read-only view, often achieved by partial extraction to a temporary location. Support is usually limited to common image and text formats. Performance and handling of encrypted or solid archives are key considerations.
*   **AES-256 & Filename Encryption:** AES-256 is the standard for strong encryption in both ZIP (modern extensions) and 7z. Filename encryption is a crucial privacy feature, particularly well-supported in 7z, but it means the entire file listing is opaque without the password.
*   **Split Archives & SFX Creation:** Users expect predefined common split sizes (floppy, CD, DVD, FAT32 limit) and the option for custom sizes. SFX creation often includes options for a default extraction path and running a program after extraction.
*   **Archive Test & Repair:** Testing provides feedback on errors (CRC, data, header). Repair capabilities are format-dependent; RAR (with recovery records) is more robust than ZIP. Repair usually involves creating a new, salvaged archive.
*   **CLI Design:** A common syntax is `command <archive> <files> -switches`. Key features include password handling (including secure prompt), output path, compression level, type specification, and clear exit codes for scripting. Consistency with established tools like 7-Zip is beneficial.

These takeaways guided the design of the dialogs, interactions, and CLI specifications that follow, aiming for a balance of power, usability, and adherence to user expectations.

## "Create Archive" Dialog Design

The "Create Archive" dialog is central to Unpack's compression capabilities, providing users with comprehensive control over archive parameters through a structured, multi-tab interface. It appears when a user initiates a new compression action that requires detailed configuration (e.g., via a "Compress to..." context menu option or specific UI interaction).

**Overall Dialog Structure:**

*   **Title:** "Create Archive"
*   **Style:**
    *   Modal dialog, blocking interaction with the main application window (if any) or Explorer until dismissed.
    *   Adheres to Fluent Design:
        *   **Background:** Uses the application's theme (Light/Dark). For a modal dialog that needs to stand out, it might have a slightly different shade than the main window or use the standard dialog background provided by WinUI. For simplicity and standard behavior, assume an opaque background that respects the current theme.
        *   **Corners:** 8px rounded corners.
        *   **Font:** Segoe UI Variable used for all text elements.
*   **Layout:** Well-spaced controls, clear visual hierarchy for sections within tabs.
*   **Tabs:** A tabbed interface to organize settings.
    *   **Tab Control:** Standard WinUI TabView, with tabs appearing at the top of the dialog content area.
    *   **Tab Titles:** "General", "Advanced", "Password", "Split/SFX", "Comment".
*   **Action Buttons (Bottom of Dialog):**
    *   **"Create":** Primary action button. Standard Fluent Design button style (e.g., accent color fill for light theme).
    *   **"Cancel":** Secondary action button. Standard Fluent Design button style.
    *   **Horizontal Alignment:** Typically right-aligned ("Create" then "Cancel") or spread ("Cancel" on left, "Create" on right).

---

### Textual Description for Each Tab:

#### 1. General Tab

*   **Focus:** Core settings for archive creation.
*   **Controls:**
    1.  **Archive Name & Path Section:**
        *   **Label:** "Archive file to create:" (Segoe UI Variable, Body)
        *   **File Path Input:** A combination control:
            *   **Text Input Field (Path):** Displays the full path and proposed filename (e.g., `C:\Users\User\Documents\Archive.zip`). Editable. Pre-filled based on context (e.g., current folder, name of selected item if single). (Segoe UI Variable, Body)
            *   **"Browse..." Button:** Standard button style, placed to the right of the text input. Icon: Segoe Fluent Icon for "folder" or "open file". Action: Opens a standard "Save File" system dialog.
    2.  **Archive Format:**
        *   **Label:** "Archive format:" (Segoe UI Variable, Body)
        *   **Dropdown Menu:** Standard WinUI ComboBox style. (Segoe UI Variable, Body)
            *   **Options:** "ZIP (compatible)", "7z (recommended)".
            *   **Default:** "ZIP (compatible)" for broader compatibility or "7z (recommended)" for better compression.
    3.  **Compression Level:**
        *   **Label:** "Compression level:" (Segoe UI Variable, Body)
        *   **Dropdown Menu:** (Segoe UI Variable, Body)
            *   **Options (Dynamic, based on Archive Format):**
                *   **For ZIP:** "Store (no compression)", "Fastest", "Fast", "Normal", "Maximum", "Ultra".
                *   **For 7z:** "Store (no compression)", "Fastest", "Fast", "Normal", "Maximum", "Ultra".
            *   **Default:** "Normal".
    4.  **Update Mode:**
        *   **Label:** "Update mode:" (Segoe UI Variable, Body)
        *   **Dropdown Menu:** (Segoe UI Variable, Body)
            *   **Options:** "Add and replace files", "Add and update files" (add new, update older), "Freshen existing files" (only update older), "Synchronize files" (like update, but also deletes files from archive if not present in source).
            *   **Default:** "Add and replace files".
    5.  **File Paths (Optional Advanced Setting, could be here or Advanced):**
        *   **Label:** "File paths:" (Segoe UI Variable, Body)
        *   **Dropdown Menu:** (Segoe UI Variable, Body)
            *   **Options:** "Relative paths" (default), "Full paths", "Absolute paths (including drive letters)".
            *   **Default:** "Relative paths".

---

#### 2. Advanced Tab

*   **Focus:** Technical compression parameters, often format-specific. UI elements here will dynamically enable/disable or change content based on "Archive format" selected in the General tab.
*   **Controls:**

    *   **Common Advanced Options:**
        1.  **Number of CPU Threads:**
            *   **Label:** "CPU threads:" (Segoe UI Variable, Body)
            *   **Dropdown Menu:** (Segoe UI Variable, Body)
                *   **Options:** "Auto", "1", "2", "4", "8", ... up to the maximum logical cores available on the user's system.
                *   **Default:** "Auto".
                *   **Note:** This option is relevant for formats/methods that support multi-threading (e.g., 7z with LZMA2, modern ZIP methods if compressing multiple files).

    *   **If "Archive format: ZIP" is selected (from General Tab):**
        1.  **Compression Method (ZIP):**
            *   **Label:** "Compression method:" (Segoe UI Variable, Body)
            *   **Dropdown Menu:** (Segoe UI Variable, Body)
                *   **Options:** "Deflate" (standard), "Deflate64", "BZip2", "LZMA (experimental for ZIP)".
                *   **Default:** "Deflate".
        2.  **(Other ZIP-specific options, e.g., for LZMA if selected):**
            *   **LZMA Dictionary Size:** (Label, Dropdown) - e.g., "1MB", "4MB", "16MB", "32MB".
            *   **LZMA Word Size:** (Label, Dropdown) - e.g., "32", "64", "128".

    *   **If "Archive format: 7z" is selected (from General Tab):**
        1.  **Compression Method (7z):**
            *   **Label:** "Compression method:" (Segoe UI Variable, Body)
            *   **Dropdown Menu:** (Segoe UI Variable, Body)
                *   **Options:** "LZMA2" (default), "LZMA", "PPMd", "BZip2".
                *   **Default:** "LZMA2".
        2.  **Dictionary Size (for LZMA/LZMA2):**
            *   **Label:** "Dictionary size:" (Segoe UI Variable, Body)
            *   **Dropdown Menu:** (Segoe UI Variable, Body)
                *   **Options:** "Auto", "256 KB", "1 MB", "4 MB", "8 MB", "16 MB", "32 MB", "64 MB", "128 MB", "256 MB", etc. (relevant to system RAM).
                *   **Default:** "Auto" or a sensible value like "16 MB" or "64 MB".
        3.  **Word Size (for LZMA/LZMA2):**
            *   **Label:** "Word size:" (Segoe UI Variable, Body)
            *   **Dropdown Menu:** (Segoe UI Variable, Body)
                *   **Options:** "Auto", "32", "64", "128", "256".
                *   **Default:** "Auto" or "64".
        4.  **Solid Block Size (7z):**
            *   **Label:** "Solid block size:" (Segoe UI Variable, Body)
            *   **Dropdown Menu:** (Segoe UI Variable, Body)
                *   **Options:** "Off (non-solid)", "1 MB", "4 MB", "16 MB", "64 MB", "Unlimited".
                *   **Default:** "Off (non-solid)" or a moderate size like "4 MB".
                *   **Info:** A small info icon next to the label could explain that solid archives compress better for many small files but make updates/extraction of single files slower.

---

#### 3. Password Tab

*   **Focus:** Setting encryption options for the archive.
*   **Controls:**
    1.  **Enable Encryption Section:**
        *   **Checkbox:** "Encrypt archive" (Segoe UI Variable, Body). When checked, the options below become active. If unchecked, they are disabled.
    2.  **Enter Password:**
        *   **Label:** "Enter password:" (Segoe UI Variable, Body) (Enabled if "Encrypt archive" is checked)
        *   **Password Input Field:** Standard WinUI PasswordBox. (Segoe UI Variable, Body)
    3.  **Re-enter Password:**
        *   **Label:** "Re-enter password:" (Segoe UI Variable, Body) (Enabled if "Encrypt archive" is checked)
        *   **Password Input Field:** Standard WinUI PasswordBox.
    4.  **Show Password:**
        *   **Checkbox:** "Show password" (Segoe UI Variable, Body) (Enabled if "Encrypt archive" is checked). Toggles visibility of characters in the password fields.
    5.  **Password Strength Indicator (Optional):**
        *   A visual bar or textual feedback (e.g., "Weak", "Medium", "Strong") that appears as the user types a password.
    6.  **Encryption Method:**
        *   **Label:** "Encryption method:" (Segoe UI Variable, Body) (Enabled if "Encrypt archive" is checked)
        *   **Dropdown Menu:** (Segoe UI Variable, Body)
            *   **Options (Dynamic, based on Archive Format):**
                *   **For ZIP:** "AES-256 (recommended)", "ZipCrypto (legacy, weak)".
                *   **For 7z:** "AES-256" (likely the only option, shown as read-only or selected by default).
            *   **Default:** "AES-256".
    7.  **Encrypt File Names:**
        *   **Checkbox:** "Encrypt file names" (Segoe UI Variable, Body)
        *   **Availability:** Enabled only if "Encrypt archive" is checked AND the selected "Archive format" (General Tab) and "Encryption method" support it (primarily for 7z with AES-256). Greyed out otherwise.
        *   **Info:** A small info icon could warn that if the password is lost and filenames are encrypted, the content list is also unrecoverable.

---

#### 4. Split/SFX Tab

*   **Focus:** Options for splitting archives into multiple parts (volumes) and creating Self-Extracting Archives (SFX).
*   **Controls:**
    1.  **Split to Volumes Section:**
        *   **Label:** "Split to volumes:" (Segoe UI Variable, Body)
        *   **Dropdown/Input Combo:**
            *   **Dropdown (Preset Sizes):** Standard WinUI ComboBox. Options: "No splitting" (default), "1.44 MB (Floppy)", "100 MB", "700 MB (CD)", "4000 MB (FAT32 Limit)", "4.7 GB (DVD5)", "Custom size...". (Segoe UI Variable, Body)
            *   **Text Input (Custom Size):** Appears or becomes enabled if "Custom size..." is selected. User can enter a number. (Segoe UI Variable, Body)
            *   **Units Dropdown (For Custom Size):** Adjacent to the custom size input. Options: "KB", "MB", "GB". Default: "MB". (Segoe UI Variable, Body)
    2.  **SFX Archive Section:**
        *   **Checkbox:** "Create SFX archive" (Segoe UI Variable, Body)
        *   **Availability:** May depend on selected archive format (e.g., 7z has robust SFX capabilities, ZIP SFX is also common).
        *   **SFX Options (Enabled if checkbox is checked):**
            *   **Label (Optional):** "SFX Module:" (if multiple are offered, e.g., for console, GUI, installer-type for 7z)
            *   **Dropdown (Optional):** Lists available SFX modules.
            *   **Text Input (Optional):** "Default extraction path (optional)"
            *   **Text Input (Optional):** "Run program after extraction (optional)"
            *   **Note:** For a minimalist initial design, just the "Create SFX archive" checkbox might be sufficient, using sensible defaults for the SFX behavior.

---

#### 5. Comment Tab

*   **Focus:** Adding a textual comment to the archive.
*   **Controls:**
    1.  **Archive Comment:**
        *   **Label:** "Archive comment (optional):" (Segoe UI Variable, Body)
        *   **Multi-line Text Input Field:** Standard WinUI TextBox with `AcceptsReturn="True"` and a vertical scrollbar if text exceeds visible area. (Segoe UI Variable, Body)
        *   **Placeholder Text (Optional):** "Enter your archive comment here."

---

**General Dialog Behaviors:**

*   **Dynamic UI Updates:** Changing "Archive format" on the "General" tab should dynamically update available options in "Advanced" (e.g., Compression Method, Dictionary Size specific to 7z/ZIP) and "Password" (e.g., Encryption Method, Encrypt File Names availability).
*   **Error/Validation Feedback:** Inline validation messages for incorrect inputs (e.g., passwords don't match, invalid custom volume size). The "Create" button might be disabled until all required fields are valid.
*   **Defaults:** All options should have sensible, pre-selected defaults to allow a user to quickly create a standard archive if they don't need advanced configuration.
*   **Tooltips/Contextual Help:** While not explicitly detailed in the text, small info icons next to complex settings (like "Solid block size" or "Encrypt file names") would be beneficial, providing a brief explanation on hover.

## File Preview Interaction in Main UI

This section describes how users can preview supported file types directly within the Unpack application's main interface, without needing to extract them first. The design prioritizes a seamless, read-only experience adhering to Fluent Design principles.

### 1. Accessing the Preview

*   **Toggleable Preview Pane Button:**
    *   **Location:** A dedicated button is available in the main application's command bar area (near "Compress", "Decompress" buttons, or in a dedicated "View" section if a toolbar/ribbon is used).
    *   **Icon:** A **Segoe Fluent Icon** clearly representing "preview" (e.g., `View`, `PreviewLink`, or `Page`).
    *   **Tooltip:** "Show/Hide Preview Pane".
    *   **Functionality:** Clicking this button toggles the visibility of the Preview Pane. The state of this button (pressed/unpressed) should persist across sessions.

*   **Location of Preview Pane:**
    *   The Preview Pane appears **to the right of the main file listing area** within the Unpack application window.
    *   It should be resizable using a standard draggable splitter between the file list and the preview pane.
    *   When hidden, the file listing area expands to occupy the full width. When shown, the file listing area contracts.

*   **Behavior on File Selection:**
    *   When the Preview Pane is visible:
        *   If a single file is selected in the archive's file list:
            *   If the file type is supported for preview, its content automatically loads and displays in the Preview Pane.
            *   If the file type is not supported, the Preview Pane updates to show an "unsupported file type" message.
        *   If multiple files are selected, the Preview Pane might show a message like "Multiple files selected. Select a single file to preview." or preview the first selected supported file. (Single file preview is simpler initially).
        *   If the selection is cleared or a folder is selected, the Preview Pane reverts to its empty/instructional state.

### 2. Preview Pane Appearance and Content

*   **General Layout:**
    *   **Background:** The Preview Pane shares the main window's background material (e.g., **Mica Light/Dark**), ensuring a cohesive look.
    *   **Header (Subtle):** At the top of the Preview Pane, a small, non-intrusive header area displays:
        *   **Filename:** The name of the file currently being previewed (e.g., "document.txt" or "image.jpg"). (Segoe UI Variable, BodyStrong or Semibold).
        *   **Close Button (Optional):** A small 'X' icon button (Segoe Fluent Icon: `ChromeClose`) within the Preview Pane's header to close/hide the pane, effectively de-selecting the "Toggle Preview Pane" button in the main command bar. This provides an alternative way to dismiss the pane.
    *   **Content Area:** The area below the header is dedicated to displaying the preview.

*   **Image Preview:**
    *   **Display:** Images are scaled to fit within the available width or height of the Preview Pane, **maintaining their original aspect ratio**. The image should be centered within the content area.
    *   **Information (Displayed below the image or as an overlay on hover - subtle):**
        *   **Filename (if not in header or for clarity):** (Segoe UI Variable, Caption)
        *   **Dimensions:** e.g., "1920 x 1080 pixels". (Segoe UI Variable, Caption)
        *   **File Size (original):** e.g., "1.2 MB". (Segoe UI Variable, Caption)
    *   **Scrollbars:** If an image is zoomed (future enhancement) or if a user explicitly chooses "original size" and it exceeds pane dimensions, standard Fluent scrollbars appear. For initial "fit to pane", scrollbars are not typically needed.

*   **Text File Preview:**
    *   **Display:** Plain text content is rendered in a **scrollable view**.
    *   **Font:**
        *   For common plain text files (.txt, .md, .ini, .conf): **Segoe UI Variable (Body)**.
        *   For source code or log files (.log, .xml, .json, .cs, .py, .js, etc.): A **monospaced font like Cascadia Mono or Consolas** is preferred for better readability and alignment. (Segoe UI Variable, Body, but with the specified monospaced family).
    *   **Text Color:** Standard text color for the current theme (e.g., black on light, white on dark).
    *   **Word Wrap:** Word wrapping is **enabled by default** to fit text within the pane's width. A toggle for word wrap could be a future enhancement, perhaps in a small context menu or settings specific to the text preview.
    *   **Scrollbars:** Vertical scrollbar appears if the text content exceeds the pane's height. A horizontal scrollbar appears if word wrap is off and lines exceed width.

*   **Loading State:**
    *   When a file is selected and its preview is being generated (especially for larger files or initial rendering):
        *   A **subtle loading indicator** (e.g., a standard WinUI ProgressRing of a small to medium size) is displayed in the center of the Preview Pane's content area.
        *   Optionally, a small text message like "Loading preview for [filename]..." can appear below the ring. (Segoe UI Variable, Caption).

*   **Unsupported File Types:**
    *   When a selected file's type is not supported for preview:
        *   The Preview Pane displays a clear message in the center of its content area.
        *   **Message:** "Preview not available for this file type." or "Cannot preview [filename]." (Segoe UI Variable, Body).
        *   **Icon (Optional):** A generic document icon or an icon representing "no preview" (e.g., Segoe Fluent Icon: `Blocked` or `DocumentSearch`).
        *   The header might still show the filename.

*   **Empty State (Pane Open, No File Selected or Archive Empty):**
    *   When the Preview Pane is visible but no file is selected (or the archive is empty):
        *   Instructional text is displayed in the center of the Preview Pane's content area.
        *   **Text:** "Select a file from the list to see a preview." (Segoe UI Variable, Body).
        *   **Icon (Optional):** A large, subtle Segoe Fluent Icon related to viewing or selection (e.g., `View` or `TouchPointer`).

### 3. Interaction Details

*   **Read-Only:**
    *   All previews are strictly **read-only**. The user cannot type in text previews or modify images. The content is for viewing purposes only.
    *   Text selection (for copying) should be possible in text previews.

*   **Context Menu within Preview (Minimal/Optional for Initial Design):**
    *   **Text Preview:** Right-clicking on selected text could show a standard system context menu with "Copy".
    *   **Image Preview:** Right-clicking on an image could offer "Copy image" (copies the image to the clipboard).
    *   *Initial Scope:* For the very first iteration, custom context menus within the preview pane can be omitted, relying on basic text selection copy (Ctrl+C).

*   **Performance Considerations (Implicit in Design):**
    *   Preview generation should be optimized. For large archives or complex files, the preview should be generated asynchronously to avoid freezing the UI. The loading indicator is key here.
    *   Only the selected file's content is read/partially extracted for previewing.

### Fluent Design Considerations Summary:

*   **Seamless Integration:** The Preview Pane should feel like an integral part of the main UI, using the same background materials (Mica) and design language.
*   **Controls & Icons:** Standard WinUI controls (ProgressRing, ScrollBar) and Segoe Fluent Icons are used for a consistent Windows experience.
*   **Typography:** Segoe UI Variable (and appropriate monospaced fonts for code/logs) with clear hierarchy and theme-aware colors.
*   **Spacing:** Adequate padding and spacing within the preview pane for a clean, uncluttered presentation of information and content.
*   **Responsiveness:** The pane should resize smoothly, and content (like scaled images or wrapped text) should adapt gracefully.

## Command-Line Interface (CLI)

The Unpack CLI is designed for automation, scripting, and users who prefer command-line operations. It provides access to core archiving functionalities.

### 1. Command Structure

The basic syntax for the Unpack CLI is as follows:

```bash
unpack.exe <command> <archive_name> [files_to_process...] [options/switches]
```

*   **`unpack.exe`**: The executable name.
*   **`<command>`**: A single letter or short string representing the action to perform (e.g., `a` for add, `x` for extract).
*   **`<archive_name>`**: The path to the archive file.
*   **`[files_to_process...]`**: Optional. One or more paths to files or directories to be added to the archive or specific files to extract from an archive. Wildcards might be supported depending on the shell, but explicit listing is standard.
*   **`[options/switches]`**: Optional. Modifiers that control aspects of the command, such as password, compression level, output path, etc.

---

### 2. Core Commands

#### `a` (Add files to archive)
*   **Description:** Adds specified files or directories to an archive. If the archive does not exist, it will be created. If it exists, files are typically added or replaced based on default behavior or specific update switches.
*   **Syntax:** `unpack.exe a <archive_name> [file1 file2... folder1\...] [options]`
*   **Example:** `unpack.exe a MyArchive.zip Document.txt MyFolder\`
*   **Example (create new):** `unpack.exe a NewArchive.7z Reports\ January.docx`

#### `x` (Extract files with full paths)
*   **Description:** Extracts files from the specified archive, recreating the stored directory structure. Files are extracted to the current directory by default, or to a directory specified by the `-o<path>` switch.
*   **Syntax:** `unpack.exe x <archive_name> [files_to_extract...] [options]`
*   **Example (all files):** `unpack.exe x MyArchive.7z`
*   **Example (specific file):** `unpack.exe x MyArchive.zip "config/settings.ini"`
*   **Example (to specific output):** `unpack.exe x MyArchive.7z -o"C:\Temp\Extracted"`

#### `e` (Extract files to current or specified directory, ignoring paths)
*   **Description:** Extracts files from the archive into a single directory (current by default, or specified by `-o<path>`), without recreating the stored directory structure (flat extraction).
*   **Syntax:** `unpack.exe e <archive_name> [files_to_extract...] [options]`
*   **Example:** `unpack.exe e MyArchive.rar -oExtractedFiles\`
*   **Example (all files to current dir):** `unpack.exe e MyArchive.zip`

#### `l` (List archive contents)
*   **Description:** Lists files and directories within the specified archive. Output can range from simple filenames to detailed listings including size, compressed size, modification date, attributes, and CRC checksums.
*   **Syntax:** `unpack.exe l <archive_name> [options]`
*   **Example:** `unpack.exe l MyArchive.tar.gz`
*   **Example (technical details):** `unpack.exe l MyArchive.7z -slt` (assuming `-slt` for show technical details, like 7-Zip)

#### `t` (Test archive integrity)
*   **Description:** Performs an integrity check on the specified archive files to verify they are not corrupted. Checks CRC values and archive structure.
*   **Syntax:** `unpack.exe t <archive_name> [options]`
*   **Example:** `unpack.exe t ImportantBackup.zip`

---

### 3. Common Switches/Options

Switches modify the behavior of the core commands. They typically start with a hyphen (`-`).

*   **Password:**
    *   `-p<password>`: Sets the password for encryption (when adding files) or decryption (when extracting/listing/testing).
        *   Example: `-pMySecret123`
    *   `-p`: If `<password>` is omitted immediately after `-p`, the CLI should securely prompt the user to enter the password (input will be masked). This is preferred for security over typing the password directly in the command history.
        *   Example: `unpack.exe a Secure.zip Data.txt -p` (prompts for password)

*   **Output Directory (for extraction `x`, `e`):**
    *   `-o<path>`: Specifies the output directory for extracted files. If `<path>` contains spaces, it must be enclosed in quotes. If `<path>` does not exist, the application should attempt to create it.
        *   Example: `unpack.exe x MyArchive.zip -o"C:\Output Here"`
        *   Example (relative path): `unpack.exe e MyArchive.7z -oOutputFolder`

*   **Compression Level (for adding `a`):**
    *   `-mx[level]`: Sets the compression level.
        *   `-mx0`: Store (no compression, fastest)
        *   `-mx1`: Fastest compression
        *   `-mx3`: Fast compression
        *   `-mx5`: Normal compression (default)
        *   `-mx7`: Maximum compression
        *   `-mx9`: Ultra compression (slowest, best compression ratio)
        *   Example: `unpack.exe a MyArchive.7z MyFile.dat -mx9`

*   **Archive Type (for adding `a`, if not inferable from `<archive_name>` extension):**
    *   `-t<type>`: Forces a specific archive type.
        *   `-tzip`: Create a ZIP archive.
        *   `-t7z`: Create a 7z archive.
        *   Example: `unpack.exe a MyArchive -tzip MyFile.txt` (creates `MyArchive.zip` or just `MyArchive` if that's how the tool handles it)

*   **Encryption Method (for adding `a`):**
    *   `-sm<method>`: Sets the encryption method. This switch is primarily for specifying methods beyond defaults or for formats that support multiple (e.g., ZIP).
        *   `-sAES256`: Use AES-256 encryption. For 7z, this is typically the default if a password (`-p`) is provided. For ZIP, this explicitly selects AES-256 over weaker legacy ZipCrypto.
        *   Example: `unpack.exe a Secure.7z SensitiveData\ -pS1perS3cur3 -sAES256`
        *   Example (ZIP AES): `unpack.exe a Secure.zip Data.txt -pS1perS3cur3 -sAES256`

*   **Encrypt File Names / Headers (for adding `a`):**
    *   `-se`: Encrypts the archive's file list/headers. This option is only effective if a password (`-p`) is also provided and the archive format supports it (e.g., 7z, or specific AES modes in ZIP if implemented).
        *   Example (7z): `unpack.exe a Hidden.7z PrivateDocs\ -pP@sswOrd -se`

*   **Create Self-Extracting (SFX) Archive (for adding `a`):**
    *   `-sfx[modulename]`: Creates a Self-Extracting archive (EXE file). Optionally, `[modulename]` can specify a particular SFX module if Unpack supports multiple (e.g., a default GUI module, a console module). If `[modulename]` is omitted, a default GUI module is used.
        *   Example: `unpack.exe a MyInstaller.exe SetupFiles\ -sfx`

*   **Volume Size (Split Archive, for adding `a`):**
    *   `-v<size>[k|m|g]`: Creates a multi-volume archive, splitting it into parts of `<size>`. `k` for kilobytes, `m` for megabytes, `g` for gigabytes. If no unit is specified, bytes might be assumed, but `m` or `k` are more common.
        *   Example: `unpack.exe a BigArchive.7z HugeFile.dat -v100m` (creates `BigArchive.7z.001`, `BigArchive.7z.002`, etc.)
        *   Example: `unpack.exe a DvdBackup.zip LargeVideo.mkv -v4480m` (for DVD-R size)

*   **Recurse Subdirectories (for adding `a`):**
    *   `-r`: Recurse subdirectories when adding files. This is often the default behavior if a folder is specified as input.
    *   `-r0`: Disable subdirectory recursion (process only files in the specified directory).
        *   Example (explicit recurse): `unpack.exe a SourceCode.zip MyProject\ -r`
        *   Example (disable recurse): `unpack.exe a RootFilesOnly.zip MyProject\ -r0`

*   **Yes to All (suppress prompts):**
    *   `-y`: Assumes "Yes" to all prompts automatically, such as file overwrite confirmations during extraction. Use with caution.
        *   Example: `unpack.exe x MyArchive.zip -oExistingFolder\ -y`

---

### 4. Syntax Examples (Illustrative Use Cases)

*   **Create a new 7z archive with ultra compression and AES-256 password protection, including filename encryption:**
    ```bash
    unpack.exe a MySecureDocs.7z "C:\My Documents" -mx9 -pMyStrongPass -sAES256 -se
    ```

*   **Extract all files from a password-protected archive (type inferred) to a specific folder, prompting for password:**
    ```bash
    unpack.exe x Confidential.archive -p -o"D:\Extracted Stuff"
    ```

*   **List contents of a TAR.GZ archive:**
    ```bash
    unpack.exe l backup.tar.gz
    ```
    *(Unpack would need to handle compound extensions like .tar.gz correctly, likely by treating it as a .gz containing a .tar).*

*   **Create a multi-volume ZIP archive, split into approximately 50MB parts, from a large dataset:**
    ```bash
    unpack.exe a LargeData.zip BigDataset\ -tzip -v50m
    ```

*   **Test the integrity of an SFX archive:**
    ```bash
    unpack.exe t MyInstaller.exe
    ```

---

### 5. Output and Error Handling (Conceptual)

*   **Standard Output (stdout):**
    *   Successful completion messages.
    *   File listings for the `l` command.
    *   Progress information (e.g., percentage, current file, speeds) during active operations like add or extract. This output should be formatted clearly, possibly updating a single line or printing new lines for logs.
*   **Standard Error (stderr):**
    *   Warning messages (e.g., "File X not found, skipping.").
    *   Error messages (e.g., "Archive is corrupt.", "Password incorrect.", "Disk full.").
*   **Exit Codes:**
    *   `0`: Success.
    *   `1`: Warning (e.g., non-fatal errors, some files skipped).
    *   `2`: Fatal error (e.g., critical error preventing task completion).
    *   Other specific positive integers can be used to denote different types of errors (e.g., incorrect password, corrupt archive, disk space issues, user interruption). Consistent error codes are crucial for scripting.
*   **Quiet Mode:** A switch like `-q` or `--quiet` could be added to suppress all non-error output for scripting purposes.

## Conclusion

The advanced features specified in this document—including a comprehensive "Create Archive" dialog, an integrated file previewer, and a robust Command-Line Interface—aim to elevate Unpack's utility for users who require more sophisticated control and automation capabilities. These features are designed with user expectations and common archiver functionalities in mind, while adhering to the overall Fluent Design philosophy of the application.

Potential next steps include developing detailed visual mockups for the new UI elements ("Create Archive" dialog, Preview Pane), prototyping these interactions, and planning the implementation phases for both the GUI enhancements and the CLI. User feedback on these advanced features will also be crucial for refinement.
