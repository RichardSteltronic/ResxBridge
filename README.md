
<p align="center">
  <img src="Resources/Branding/ResxBridgeLogo.png" alt="ResX Bridge" width="650">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-blue" alt="Version">
  <img src="https://img.shields.io/badge/platform-Windows-blue" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-10-512BD4" alt=".NET">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License">
</p>

<p align="center">
  <a href="https://github.com/RichardSteltronic/ResxBridge/releases/latest">
    <img src="https://img.shields.io/badge/Download-ResX%20Bridge-15803D?style=for-the-badge&logo=windows&logoColor=white" alt="Download ResX Bridge">
  </a>
</p>

<p align="center">
  Free &bull; Open Source &bull; Windows x64 &bull; No Installation Required
</p>


## Overview

ResX Bridge is a free, open-source Windows utility designed
to simplify translation collaboration for developers working
with .NET resource (.resx) files.

Export your resource strings to an XLSX workbook, share it
with your translators, and safely import their completed
translations back into your project.

Translators do not need Visual Studio, access to your source
code, or any knowledge of RESX files.

**Export. Share. Import.**

## Why ResX Bridge?

ResX Bridge was created to solve a practical development
problem: exchanging translations with people who are not
necessarily developers.

It is not intended to replace existing resource editors or
localization tools. Instead, it bridges the gap between
developers and translators using a familiar spreadsheet format.

## Features

- Automatic RESX resource discovery
- Support for multiple resource families
- Automatic language and culture identification
- Selective language export
- Default-only resource export for proofreading
- Protected workbook structure and resource identifiers
- Color-coded translation status
- Import preview with before-and-after values
- Formatting placeholder validation
- Stale workbook and translation conflict detection
- Protection against unintended resource modifications
- Automatic project detection
- Configurable, project-specific backups
- Automatic rollback if an update fails
- No Microsoft Excel installation required by ResX Bridge
- No accounts, subscriptions, or cloud services

## How It Works

### 1. Export

Select the directory containing your RESX files and choose
the languages you want to export.

ResX Bridge generates an XLSX workbook containing your
resource keys, default values, and selected translations.

### 2. Share

Send the workbook to your translator.

Resource identifiers and reference values are protected
against accidental modification.

### 3. Import

Import the completed workbook into ResX Bridge.

Review the proposed changes and any validation issues
before modifying your resource files.

### 4. Apply

Apply the validated changes to your RESX files.

ResX Bridge creates a backup snapshot of the original
affected files before applying any modifications.

## Data Integrity

ResX Bridge is designed with resource integrity in mind.

Exported workbooks contain an original resource snapshot
that allows the application to detect conflicting changes
made after export.

Imports are validated before changes are applied. Validation
errors prevent the entire batch from being applied.

Blank translation cells do not erase existing resource
values, and missing resource keys are not automatically
created.

Workbook protection is intended to prevent accidental
modification, not to provide cryptographic security.

## Privacy

ResX Bridge operates locally on your computer.

The application does not require an internet connection,
user account, or external translation service.

## Download and Installation

Download the latest version from the
[GitHub Releases page](https://github.com/RichardSteltronic/ResxBridge/releases/latest).

1. Download the Windows x64 ZIP package.
2. Extract all files to a folder of your choice.
3. Run `ResxBridge.exe`.

The downloadable release is self-contained and does not
require a separate .NET runtime installation.

## Screenshots

### Discover Resource Files

Automatically discover resource families, languages,
cultures, and string entries within a selected directory.

![Resource Discovery](Resources/Screenshots/MainWindow.png)

### Export and Share

Export protected, color-coded XLSX workbooks for
collaboration with translators.

![Translation Workbook](Resources/Screenshots/TranslationWorkbook.png)

### Review and Import

Review proposed translation changes and validation issues
before applying updates to your resource files.

![Import Preview](Resources/Screenshots/ImportPreview.png)

## System Requirements

- Windows
- .NET 10 Desktop Runtime for framework-dependent builds

## Building from Source

- Visual Studio with the .NET desktop development workload
- .NET 10 SDK

Clone the repository, open `ResxBridge.slnx`, restore NuGet
packages, and build the solution.

ResX Bridge uses ClosedXML for XLSX workbook operations.

## Feedback and Contributions

Suggestions, bug reports, and contributions are welcome.

If you encounter a problem or have an idea that would
improve your translation workflow, please open an issue
in the GitHub repository.

## License

ResX Bridge is released under the MIT License.

See [LICENSE](LICENSE.txt) for details.

---

**ResX Bridge**

Built by a developer, for developers.

Export. Share. Import.
