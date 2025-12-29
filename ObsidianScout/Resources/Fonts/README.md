Font files for ObsidianScout
===========================

This folder should contain any custom fonts used by the app. The project expects the Font Awesome Free (Solid) font file named `fa-solid-900.ttf` to be placed here.

How to add Font Awesome (free):

1. Download the free Font Awesome desktop package from https://fontawesome.com (Free download).
2. From the downloaded package, copy `fa-solid-900.ttf` into this `Resources/Fonts` folder.
3. Rebuild the solution. The project registers the font alias `FA` in `MauiProgram.cs` so XAML resources can use it.

Licensing:

- Make sure you comply with the Font Awesome license for the version you download. This repository does not include the font file to avoid licensing issues.

Alternate options:

- Use another open font and update `Resources/Styles/IconFonts.xaml` to reference that font family.
- Replace `FontImageSource` usages with vector `Path` or image assets if you prefer.
