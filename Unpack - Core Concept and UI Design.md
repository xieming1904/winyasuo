# Unpack - Core Concept and UI Design

## Introduction

Unpack is a new compression software designed for Windows, aiming to provide a minimalist and intuitive user experience. The core goals of the Unpack project are to create a tool that is easy to use, visually clean, and deeply integrated with the Windows environment by fully embracing Microsoft's Fluent Design system, including robust support for both light and dark modes. This document outlines the design principles, main user interface, context menu integration, and a core user flow for the Unpack application.

## Fluent Design System - Key Principles Applied

The design of Unpack is guided by Microsoft's Fluent Design System, focusing on creating an experience that feels natural and cohesive within the Windows environment. Key principles such as Light, Depth, Motion, Material, and Scale are interpreted to ensure a modern and accessible interface.

Successfully reviewed Microsoft's Fluent Design documentation, focusing on Windows desktop applications (WinUI). Here are the key takeaways relevant to designing a minimalist and intuitive file compression utility:

**Overall Philosophy:**
*   **Minimalist & Intuitive:** Fluent Design for Windows 11 emphasizes an "effortless, calm, personal, familiar, and complete + coherent" experience. This aligns perfectly with a minimalist and intuitive utility. The design should be clean, decluttered, and allow users to focus on their tasks.

**Core Fluent Principles Applied to the Utility:**

1.  **Light:**
    *   **Themes:** The utility *must* support light and dark themes, respecting user system settings by default. Use theme brushes (e.g., `TextFillColorPrimaryBrush`) for all UI elements (text, backgrounds, icons) to ensure they adapt correctly.
    *   **Accent Color:** Use the system accent color by default to provide visual highlights for interactive elements (buttons, selected items) and states (hover, pressed). Use sparingly. If a brand color is used, ensure high contrast across themes and states. For a minimalist utility, system accent is preferred.
    *   **Hierarchy:** In both themes, darker colors indicate less important background surfaces, while lighter/brighter colors highlight important interactive surfaces.

2.  **Depth (Elevation & Layering):**
    *   **Main Window:** Should have the highest elevation (e.g., 128) with appropriate shadow, establishing it as the primary surface.
    *   **Controls:** Buttons, list views, input fields should have lower elevation (e.g., 2 at rest, 1 when pressed) with subtle shadows/contours to lift them slightly from the base.
    *   **Dialogs/Modals:** (e.g., error messages, password prompts, settings) should use high elevation (128) and Smoke material to dim the content below, indicating a blocking interaction.
    *   **Flyouts/Context Menus:** Should use elevation (e.g., 32) and Acrylic material.
    *   **Layering Structure:** Employ a simple two-layer system:
        *   **Base Layer:** The app's main background/foundation, likely using Mica material.
        *   **Content Layer:** Where primary user interaction occurs (file lists, compression options). Can use "Card" elements (elevation 8) for grouped information if needed.

3.  **Motion:**
    *   **Subtlety is Key:** Use motion sparingly and purposefully to enhance understanding and provide feedback, not distract.
    *   **Standard Controls:** Leverage built-in WinUI control animations for page transitions, connected animations, and animated icons to maintain consistency with the OS.
    *   **Page Transitions:** If the utility has separate views (e.g., main vs. settings), use standard slide transitions.
    *   **Feedback:** Provide clear visual feedback for interactions (hover, press states on buttons). Animated icons can be used subtly for actions (e.g., on a "compress" button click) or for progress indicators.
    *   **Timings:** Adhere to standard Fluent Design timings and easing curves.

4.  **Material:**
    *   **Mica:** Use as the base material for the main application window. It provides a subtle tint from the user's desktop background, adding a personal touch while remaining opaque and focused.
    *   **Acrylic:** Use for transient, light-dismiss surfaces like context menus or flyouts (e.g., advanced options for a compression job).
    *   **Smoke:** Use for modal dialog backgrounds to emphasize the dialog and de-emphasize the content behind it.

5.  **Scale (Geometry, Typography, Iconography):**
    *   **Geometry (Layout & Rounded Corners):**
        *   **Layout:** Keep it clean, uncluttered, with consistent spacing ("gutters") between elements. Focus on clear visual hierarchy.
        *   **Rounded Corners:** Apply consistently: 8px for the main app window and dialogs; 4px for in-page elements like buttons, input fields, and list views. 0px for edges that meet other flat edges or when maximized/snapped.
    *   **Typography:**
        *   **Font:** Use the default **Segoe UI Variable**.
        *   **Type Ramp:** Adhere to the Windows type ramp for clear hierarchy (e.g., "Body" for primary text, "Title" or "Subtitle" for main headers, "Caption" for secondary info).
        *   **Casing:** Use sentence case for all UI text, including titles and button labels.
        *   **Readability:** Aim for 50-60 characters per line for blocks of text (less critical for short labels). Use truncation with ellipses (...) for overflow.
    *   **Iconography:**
        *   **System Icons:** Use **Segoe Fluent Icons** for all in-app iconography (buttons, status indicators). They are monoline, minimal, and theme-aware.
        *   **Clarity:** Choose simple, universally understood metaphors (e.g., "+" for add, gear for settings).
        *   **Sizing:** Use standard icon sizes (e.g., 16x16 epx or 20x20 epx, as appropriate for the control).

**Key Design Principles for the Utility:**
*   **Clarity and Simplicity:** The UI should be immediately understandable. Avoid visual clutter. Every element should have a clear purpose.
*   **Consistency:** Adhere to Windows 11 design patterns for controls, interactions, and visual styling to make the app feel native and predictable.
*   **Efficiency:** The design should support user workflows efficiently. For a compression utility, this means easy access to core functions like adding files, selecting output, and starting the process.

These principles collectively guide the design of Unpack to ensure it is functional, aesthetically pleasing, and integrates seamlessly with the Windows 11 user experience.

## Main Interface Design

The main user interface of Unpack is designed for simplicity and directness, featuring a single-pane layout that focuses the user on the core tasks of compression and decompression.

### Light Mode Mockup Description

**Window Appearance:**

*   **Overall Shape & Corners:** The application window is a standard rectangular shape with **8px rounded corners**, giving it a soft, modern Fluent Design appearance.
*   **Title Bar:** The title bar is seamlessly integrated into the window. It uses the **Mica material** as its background, subtly picking up the desktop wallpaper tint.
    *   The application title "Unpack" is displayed on the left side of the title bar area, using **Segoe UI Variable (Regular weight, Body size - e.g., 14epx)**.
    *   Standard window controls (minimize, maximize, close) are on the right, using system-provided glyphs. They appear to rest on the Mica background.
*   **Material:** The entire main window background is **Mica Light**. This material provides an opaque, subtly textured surface that hints at the desktop wallpaper color. It should appear clean and unobtrusive.

**Layout:**

*   **Single-Pane Focus:** The layout is a single, uncluttered pane designed for simplicity and ease of use.
*   **Vertical Flow:** Key elements are arranged in a clear vertical flow: Action Buttons at the top, followed by the main content/file area.

**Key UI Elements:**

1.  **Main Action Buttons:**
    *   Located prominently near the top of the window, below the title bar area, centered horizontally or slightly offset to the left if a settings icon is on the far right.
    *   Two primary buttons:
        *   **"Compress":** Standard button style.
            *   **Text:** "Compress" using **Segoe UI Variable (Semibold weight, Body or Subtitle size - e.g., 14-16epx)**.
            *   **Icon (Optional but Recommended):** A **Segoe Fluent Icon** representing "compress" or "archive" (e.g., a folder with a zipper, or stacked files with an arrow indicating compression) placed to the left of the text. Icon size should be appropriate for the button height (e.g., 16x16 epx or 20x20 epx).
            *   **Appearance:** Uses standard light theme button styling (e.g., light grey background, darker grey border, black text). **4px rounded corners**. Subtle shadow indicating slight elevation (e.g., 2epx).
            *   **Hover State:** Background becomes slightly darker.
            *   **Pressed State:** Background becomes even darker, shadow might reduce slightly (elevation 1epx).
        *   **"Decompress":** Standard button style, placed next to or below "Compress".
            *   **Text:** "Decompress" using **Segoe UI Variable (Semibold weight, Body or Subtitle size - e.g., 14-16epx)**.
            *   **Icon (Optional but Recommended):** A **Segoe Fluent Icon** representing "decompress" or "extract" (e.g., an open folder, or files expanding outwards) placed to the left of the text.
            *   **Appearance:** Same styling as "Compress" button. **4px rounded corners**.
    *   **Spacing:** Adequate spacing between these buttons and other UI elements to maintain a clean look.

2.  **File/Folder Browsing Area (Central Area):**
    *   Occupies the largest part of the window below the action buttons.
    *   **Empty State:**
        *   A clear instructional message is displayed in the center of this area.
        *   **Text:** "Drag and drop files/folders here to compress or decompress" or "Select files/folders using the buttons above, or drag them here." Use **Segoe UI Variable (Regular weight, Body or BodyLarge size - e.g., 14-18epx)**. Text color should be a medium grey (e.g., `TextFillColorSecondaryBrush`).
        *   **Icon (Optional):** A large, subtle **Segoe Fluent Icon** representing files/folders or drag-and-drop (e.g., a generic file icon, or a downward arrow into a box) can be placed above or below the instructional text. This icon should be a lighter shade of grey.
        *   The background of this area is the main window Mica.
    *   **Populated State (After files are added/selected for compression/decompression):**
        *   The area transforms into a list view.
        *   **List Items:** Each file/folder is a list item.
            *   **Icon:** Appropriate **Segoe Fluent Icon** for file type or folder.
            *   **Text:** File/folder name using **Segoe UI Variable (Regular weight, Body size - e.g., 14epx)**.
            *   **Secondary Text (Optional):** File size or path, using **Segoe UI Variable (Regular weight, Caption size - e.g., 12epx)** and a lighter text color.
            *   **Item Background:** Transparent, resting on the Mica background.
            *   **Selection State:** Selected items have a subtle background highlight (e.g., light blue with system accent color influence).
        *   **Scrollbar (if needed):** Standard Fluent scrollbar (thin, subtle, appears on hover/scroll).
    *   **Border (Optional):** A very subtle, thin border with **4px rounded corners** might delineate this area from the rest of the Mica background, especially in the empty state.

3.  **Drag-and-Drop Indication:**
    *   **Visual Cue:** When files are dragged over any part of the application window (especially the central file area):
        *   The border of the central file/folder area (if present) becomes more prominent (e.g., thicker, or changes color to the system accent color).
        *   Alternatively, the entire central area might display an overlay with a message like "Drop here" and a relevant icon. This overlay could have a semi-transparent background.
        *   The mouse cursor should also provide standard OS drag-and-drop cues.

4.  **Settings Icon:**
    *   A small, unobtrusive settings icon (e.g., a **Segoe Fluent Icon** for "settings" - a gear).
    *   **Location:** Typically placed in the top right corner of the window, either on the title bar itself (if space allows and design is very integrated) or just below it, aligned with the action buttons.
    *   **Appearance:** Icon button style (transparent background, icon color matches text, subtle background highlight on hover/press).

**Fluent Design Application Summary:**

*   **Mica Background:** Main window background, providing depth and personality.
*   **Rounded Corners:** 8px for the window, 4px for buttons and potentially the file area outline.
*   **Segoe UI Variable Font:** Used throughout for all text elements, following the Windows type ramp for sizes and weights.
*   **Segoe Fluent Icons:** Used for action buttons and the settings icon, ensuring visual consistency and modernity.
*   **Layering:** Buttons and active UI elements are subtly elevated with shadows above the Mica base layer.
*   **Motion:** Standard button animations (hover, press). Drag-and-drop visual feedback would involve subtle animations or state changes.

### Dark Mode Mockup Description

**Overall Appearance:**

*   The fundamental layout and structure remain identical to the Light Mode. The changes are primarily in the color palette and material rendering.

**Window Appearance:**

*   **Title Bar:** Still integrated, uses **Mica Dark** as its background. The subtle tint from the desktop wallpaper will be adapted for the dark theme.
    *   Application title "Unpack" and window controls (minimize, maximize, close) use a light text/glyph color (e.g., white or very light grey) for high contrast against the Mica Dark background.
*   **Material:** The entire main window background is **Mica Dark**. It will appear as a dark, subtly textured surface, still hinting at the desktop wallpaper color but in a darker shade.

**Key UI Elements - Color Inversion:**

1.  **Main Action Buttons:**
    *   **"Compress" & "Decompress":**
        *   **Appearance:** Use standard dark theme button styling (e.g., dark grey background, lighter grey border or no border if using a filled look, light text color). **4px rounded corners**. Shadows will be adapted for dark theme, often being more subtle or replaced by glows/highlights if that's the dark theme convention for depth.
        *   **Text & Icon Color:** Light color (e.g., white or very light grey) for high contrast.
        *   **Hover State:** Background becomes slightly lighter.
        *   **Pressed State:** Background becomes even lighter.

2.  **File/Folder Browsing Area (Central Area):**
    *   **Empty State:**
        *   **Instructional Text:** Light color (e.g., a lighter shade of grey, not pure white, like `TextFillColorSecondaryBrush` for dark theme) for readability against the Mica Dark background.
        *   **Optional Icon:** A very light grey or subtly desaturated color.
    *   **Populated State (List View):**
        *   **List Item Text:** Primary text (file/folder name) is a light color. Secondary text is a dimmer light color.
        *   **Icons:** File/folder icons are styled for dark mode (often involving color inversion or using specific dark theme variants if they are bitmap). Segoe Fluent Icons will naturally adapt their color.
        *   **Selection State:** Selected items have a subtle background highlight appropriate for dark mode (e.g., a slightly lighter shade of dark grey, or a desaturated system accent color).
    *   **Border (Optional):** If used, the border color would be a subtle light grey.

3.  **Drag-and-Drop Indication:**
    *   **Visual Cue:**
        *   Border highlight color would be a lighter version of the system accent color or a distinct light grey.
        *   Overlay message ("Drop here") would use light text on a semi-transparent dark overlay.

4.  **Settings Icon:**
    *   **Icon Color:** Light color (e.g., white or very light grey).
    *   **Hover/Press State:** Subtle light background highlight.

**Fluent Design Application Summary (Dark Mode Specifics):**

*   **Mica Dark Background:** Provides the dark, personalized base for the application.
*   **Color Palette:** Inverted from light mode – light text and icons on dark backgrounds. Theme brushes for dark mode are used.
*   **Shadows/Elevation:** Depth cues are maintained, though shadows might appear different or be complemented by other effects (like subtle highlights on elevated surfaces) as per standard dark theme conventions for Fluent Design. The key is that the visual hierarchy of elevation remains clear.
*   **Acrylic Material (If used for Flyouts/Menus):** Would switch to its dark theme variant, maintaining translucency but with darker tones.

## Windows Explorer Context Menu Integration

Unpack aims for seamless integration with Windows Explorer, providing quick access to common compression and decompression tasks. The context menu design prioritizes clarity, efficiency, and adherence to the Windows 11 Fluent Design style.

### Main Level Context Menu Description

**Overall Appearance:**

*   **Invocation:** Appears when a user right-clicks on a single file, a single folder, multiple selected files, or multiple selected folders in Windows Explorer.
*   **Material & Shape:** The context menu background uses **Acrylic material (Light or Dark theme appropriate)**, providing a semi-transparent, frosted glass effect. It has **8px rounded corners**.
*   **Separators:** Thin, subtle lines separate groups of menu items or individual top-level items as per Windows 11 context menu standards.
*   **Typography:** All text uses **Segoe UI Variable (Regular weight, Body size - e.g., 14epx)**. Menu item text color adapts to light/dark themes.
*   **Spacing:** Standard Windows 11 context menu item padding and spacing is applied for a clean, touch-friendly (if applicable) appearance.
*   **Highlighting:** Hovered items have a subtle background highlight. Selected/pressed items also have distinct visual feedback.

**'Unpack' Application Menu Items (Main Level):**

The following items related to 'Unpack' would ideally appear as top-level or near top-level entries in the context menu. Their exact position relative to standard Windows items (Cut, Copy, Paste, Delete, Rename) will depend on Windows' own logic, but they should be grouped logically if possible.

1.  **"Compress to <archive_name>.zip"**
    *   **Visibility:** Appears when right-clicking on one or more files or folders. If a single item is selected, `<archive_name>` defaults to that item's name. If multiple items are selected, it might default to the parent folder's name or "Archive".
    *   **Wording:** "Compress to Documents.zip" (if "Documents" folder is selected) or "Compress to Selection.zip" (if multiple items).
    *   **Icon:** A **Segoe Fluent Icon** representing "compress" or "archive" (e.g., `ZipFolder`). Positioned to the left of the text.
    *   **Action:** Immediately creates a ZIP archive in the current directory with the specified name, containing the selected item(s). (Note: This specific flow implies direct action. The "Core User Flow" section details a version that includes a password prompt via a dialog).

2.  **"Extract Here"**
    *   **Visibility:** Appears only when right-clicking on one or more archive files (e.g., .zip, .rar, .7z).
    *   **Wording:** "Extract Here"
    *   **Icon:** A **Segoe Fluent Icon** representing "extract" or "unarchive directly" (e.g., `FolderOpen`). Positioned to the left of the text.
    *   **Action:** Extracts the contents of the selected archive(s) directly into the current folder.

3.  **"Extract to <folder_name>/"**
    *   **Visibility:** Appears only when right-clicking on one or more archive files. `<folder_name>` is derived from the archive's name (e.g., if "Photos.zip" is selected, this reads "Extract to Photos/"). If multiple archives are selected, it might be disabled or offer to extract each to its respective named folder.
    *   **Wording:** "Extract to Photos/"
    *   **Icon:** A **Segoe Fluent Icon** representing "extract to a folder" (e.g., `FolderArrowRight`). Positioned to the left of the text.
    *   **Action:** Creates a new folder named after the archive in the current directory and extracts the contents into that new folder.

4.  **"Unpack" (Submenu Item)**
    *   **Visibility:** Appears in all scenarios (when right-clicking any file, folder, or selection).
    *   **Wording:** "Unpack"
    *   **Icon:** The 'Unpack' application logo/icon (a simplified version if necessary for menu clarity, otherwise a generic **Segoe Fluent Icon** like `ChevronRightSmall` indicating a submenu, or a custom app icon if allowed by Windows context menu guidelines for app entries). For consistency, a Segoe Fluent Icon like `Toolbox` or `AppFolder` could represent the app's toolset.
    *   **Indicator:** A small right-pointing chevron/arrow (`ChevronRightSmall`) on the far right of the menu item, indicating it opens a submenu.
    *   **Action:** Opens the "Unpack" Submenu.

### "Unpack" Submenu Description

**Overall Appearance:**

*   **Invocation:** Appears when the user hovers over or clicks the "Unpack" main menu item.
*   **Material & Shape:** Same as the main context menu: **Acrylic material (Light or Dark theme appropriate)**, **8px rounded corners**.
*   **Cascading:** Opens adjacent to the main context menu, typically to the right.
*   **Typography & Spacing:** Consistent with the main context menu (Segoe UI Variable, standard padding).
*   **Highlighting:** Consistent hover and selection highlights.

**'Unpack' Submenu Items:**

These items provide more granular control and access to less frequently used features. Visibility of some items might still be conditional based on selection.

1.  **"Compress to..."**
    *   **Visibility:** Appears when files/folders (non-archives) are selected.
    *   **Wording:** "Compress to..." (ellipsis indicates a dialog will follow)
    *   **Icon:** A **Segoe Fluent Icon** like `ZipFolder` or `Archive`. Positioned to the left.
    *   **Action:** Opens the main 'Unpack' application window or a dedicated dialog, pre-populated with the selected files, allowing the user to choose archive format (zip, 7z, tar, etc.), compression level, password, and destination.

2.  **"Add to archive..."**
    *   **Visibility:** Appears when files/folders are selected. If an existing archive is also part of the selection, this could intelligently suggest adding to it.
    *   **Wording:** "Add to archive..."
    *   **Icon:** A **Segoe Fluent Icon** like `Add` combined with an archive symbol, or `ArchiveAdd`. Positioned to the left.
    *   **Action:** Opens a dialog to select an existing archive or create a new one, then adds the selected files/folders to it.

3.  **"Extract to..."**
    *   **Visibility:** Appears when one or more archive files are selected.
    *   **Wording:** "Extract to..." (ellipsis indicates a dialog will follow for destination selection)
    *   **Icon:** A **Segoe Fluent Icon** like `FolderArrowRight` or `Extract`. Positioned to the left.
    *   **Action:** Opens a dialog allowing the user to specify the extraction path and potentially other options (e.g., overwrite behavior).

4.  **"Test archive(s)"**
    *   **Visibility:** Appears when one or more archive files are selected.
    *   **Wording:** "Test archive(s)"
    *   **Icon:** A **Segoe Fluent Icon** like `ArchiveCheck` or `ShieldCheckmark`. Positioned to the left.
    *   **Action:** Performs an integrity check on the selected archive(s) and provides feedback (e.g., a notification or a small status window).

5.  **"Compress and Email"**
    *   **Visibility:** Appears when files/folders (non-archives) are selected.
    *   **Wording:** "Compress and Email..."
    *   **Icon:** A **Segoe Fluent Icon** combining an archive symbol with an email symbol (e.g., `MailSend` or `ArchiveArrowRight`). Positioned to the left.
    *   **Action:** Compresses the selected items (likely to a temporary .zip file), then opens the default email client with the archive attached to a new message.

6.  **"Open Unpack"**
    *   **Visibility:** Always visible in the submenu.
    *   **Wording:** "Open Unpack"
    *   **Icon:** The main 'Unpack' application icon (clearer here than potentially on the main menu level). Positioned to the left.
    *   **Action:** Opens the main 'Unpack' application window.

## Core User Flow: Compression with Password

This section describes a common user flow: compressing selected files into a new ZIP archive with an optional password, initiated from the Windows Explorer context menu.

### Flowchart Description

*   `[START: User intends to compress files with optional password]`
*   `-> <USER_ACTION: Selects 3 files in Windows Explorer>`
*   `-> <USER_ACTION: Right-clicks on the selected files>`
*   `-> <UI_INTERACTION: System displays Windows Explorer context menu (Acrylic background, 8px rounded corners)>`
*   `-> <USER_ACTION: Selects "Compress to .zip" from the context menu (or "Compress to..." from Unpack submenu if more options are desired first)>`
    *   *(Assumption for this flow: "Compress to .zip" directly opens a settings/confirmation dialog)*
*   `-> <UI_INTERACTION: 'Unpack' displays 'Quick Compression Settings' dialog. This is a modal dialog with a Smoke material background, dimming the Explorer window. The dialog has 8px rounded corners and uses Segoe UI Variable font.`
    *   `Dialog Contents:`
        *   `Title: "Compress to ZIP"`
        *   `Field: Editable filename (pre-filled, e.g., "Selection.zip" or based on selected items).`
        *   `Display: Output location (default: current folder, possibly a "Change..." button).`
        *   `Checkbox/Button: "Add Password" (unchecked by default).`
        *   `Button: "Compress" (primary action).`
        *   `Button: "Cancel".`
*   `-> (DECISION: User wants to set a password?)`
    *   `YES -> <USER_ACTION: Clicks the "Add Password" checkbox/button within the 'Quick Compression Settings' dialog.>`
        *   `-> <UI_INTERACTION: The dialog expands or reveals new input fields: "Password" and "Confirm Password". These are standard password input fields. A password strength indicator might appear.> `
        *   `-> <USER_ACTION: User types a password in the "Password" field.>`
        *   `-> <USER_ACTION: User types the same password in the "Confirm Password" field.>`
        *   `-> [SYSTEM_PROCESS: Basic validation (passwords match, minimum complexity if defined). Error message shown in dialog if validation fails, preventing progression until corrected.]`
    *   `NO -> <UI_INTERACTION: User leaves "Add Password" unchecked or does not interact with password options.>`
*   `-> <USER_ACTION: Clicks the "Compress" button in the 'Quick Compression Settings' dialog.>`
*   `-> [SYSTEM_PROCESS: 'Quick Compression Settings' dialog closes. Compression process begins.]`
*   `-> <UI_INTERACTION: A progress indicator appears. This could be a system toast notification with a progress bar, or if the main Unpack app window is configured to open/show, progress is displayed there. The progress indicator shows filename being processed (if multiple), percentage complete, and estimated time remaining. It follows Fluent Design (e.g., Mica/Acrylic background for toast, standard progress bar styling).>`
*   `-> [SYSTEM_PROCESS: Archive file is created. Files are compressed and added. Password protection is applied if specified.]`
*   `-> (DECISION: Compression successful?)`
    *   `YES -> <UI_INTERACTION: System displays a success notification. This is typically a toast notification ("Compression of <archive_name>.zip complete.") with options like "Open containing folder" or "Open archive". The notification uses appropriate Fluent styling for light/dark mode.>`
    *   `NO -> <UI_INTERACTION: System displays an error notification (toast or dialog if critical) explaining the failure (e.g., "Error: Not enough disk space," "Error: File in use").>`
*   `-> [END: Compression task is completed (successfully or with error). User is returned to Windows Explorer, where the new archive file is now visible (if successful).]`

## Conclusion

The design for Unpack, rooted in Fluent Design principles, aims to deliver a compression utility that is both powerful and unobtrusive, feeling like a natural extension of the Windows operating system. The described main interface, context menu integration, and core user flow prioritize ease of use, clarity, and a modern aesthetic.

Next steps would involve translating these textual descriptions into visual mockups and interactive prototypes, followed by user testing to refine the design and user experience further.
