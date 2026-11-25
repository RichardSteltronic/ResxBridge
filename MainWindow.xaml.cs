
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ResxBridge.Models;
using ResxBridge.Services;
using ResxBridge.Utilities;

namespace ResxBridge;

/// <summary>
/// Coordinates the resource discovery, translation exchange,
/// import preview, and resource update workflow.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ResxDiscoveryService _discoveryService = new();
    private readonly ResxExportService _exportService = new();
    private readonly ResxImportService _importService = new();
    private readonly ResxApplyService _applyService = new();
    private readonly SettingsService _settingsService = new();

    private ApplicationSettings _settings = new();

    private IReadOnlyList<ResourceFileData> _resources =
        Array.Empty<ResourceFileData>();

    private ImportPreview? _preview;

    private string? _resourceDirectory;

    private bool _isBusy;

    public MainWindow()
    {
        InitializeComponent();

        InitializeVersion();
        InitializeSettings();
        UpdateControls();
    }

    private void InitializeSettings()
    {
        try
        {
            _settings = _settingsService.Load();
        }
        catch (Exception exception)
        {
            SetStatus("Unable to load application settings.");

            MessageBox.Show(
                this,
                "The saved application settings could not be loaded." +
                Environment.NewLine +
                Environment.NewLine +
                exception.Message +
                Environment.NewLine +
                Environment.NewLine +
                "The application will continue with default settings. " +
                "Saving new settings will replace the existing configuration.",
                "Settings load failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void InitializeVersion()
    {
        var version = Assembly
            .GetExecutingAssembly()
            .GetName()
            .Version;

        VersionTextBlock.Text = version is null
            ? "ResX Bridge"
            : $"ResX Bridge v{version.ToString(3)}";
    }

    private async void BrowseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!ConfirmDiscardPreview())
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Select RESX resource directory",
            Multiselect = false
        };

        string? initialDirectory = _resourceDirectory ?? _settings.LastResourceDirectory;

        if (!string.IsNullOrWhiteSpace(initialDirectory) &&
            Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await LoadResourcesAsync(dialog.FolderName);
    }

    private async void RefreshButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_resourceDirectory is null ||
            !ConfirmDiscardPreview())
        {
            return;
        }

        await LoadResourcesAsync(_resourceDirectory);
    }

    private async Task LoadResourcesAsync(string directory)
    {
        await RunOperationAsync(
            "Discovering resource files...",
            async () =>
            {
                var resources = await Task.Run(
                    () => _discoveryService.Discover(directory));

                _resourceDirectory = directory;

                ResourceDirectoryTextBox.Text = directory;

                SetResources(resources);
                ClearPreview();

                SetStatus(resources.Count == 0
                    ? "No RESX files were found."
                    : "Resource discovery completed.");

                RememberResourceDirectory(directory);
            });
    }

    private void SetResources(
        IReadOnlyList<ResourceFileData> resources)
    {
        _resources = resources;

        ResourceFilesDataGrid.ItemsSource = resources
            .Select(resource => resource.File)
            .ToList();

        int familyCount = resources
            .Select(resource => resource.File.ResourceFamily)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        int entryCount = resources.Sum(
            resource => resource.File.EntryCount);

        ResourceSummaryTextBlock.Text =
	        $"{resources.Count:N0} resource " +
	        $"{(resources.Count == 1 ? "file" : "files")}  |  " +
	        $"{familyCount:N0} resource " +
	        $"{(familyCount == 1 ? "family" : "families")}  |  " +
	        $"{entryCount:N0} string " +
	        $"{(entryCount == 1 ? "entry" : "entries")}";
    }

    private void RememberResourceDirectory(string directory)
    {
        try
        {
            _settings.LastResourceDirectory =
                SettingsService.NormalizeDirectory(directory);

            _settingsService.Save(_settings);
        }
        catch (Exception exception)
        {
            SetStatus(
                "Resource discovery completed, but the last " +
                $"directory could not be saved: {exception.Message}");
        }
    }


    private async void ExportButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_isBusy ||
            _resourceDirectory is null ||
            _resources.Count == 0)
        {
            return;
        }

        string directory = _resourceDirectory;

        bool discoveryCompleted = false;

        await RunOperationAsync(
            "Refreshing resource files...",
            async () =>
            {
                var resources = await Task.Run(
                    () => _discoveryService.Discover(directory));

                SetResources(resources);

                discoveryCompleted = true;

                SetStatus(resources.Count == 0
                    ? "No RESX files were found."
                    : "Resource discovery completed.");
            });

        if (!discoveryCompleted || _resources.Count == 0)
        {
            return;
        }

        IReadOnlyList<ResourceFileData> exportResources = _resources;

        bool hasTargetLanguages = _resources.Any(
            resource => !resource.File.IsNeutral);

        if (hasTargetLanguages)
        {
            var optionsWindow = new ExportOptionsWindow(_resources)
            {
                Owner = this
            };

            if (optionsWindow.ShowDialog() != true)
            {
                SetStatus("Export cancelled.");
                return;
            }

            var selectedCultures = new HashSet<string>(
                optionsWindow.SelectedCultures,
                StringComparer.OrdinalIgnoreCase);

            exportResources = _resources
                .Where(resource =>
                    resource.File.IsNeutral ||
                    selectedCultures.Contains(resource.File.Culture))
                .ToArray();
        }

        var availableCultures = _resources
	        .Where(resource => !resource.File.IsNeutral)
	        .Select(resource => resource.File.Culture)
	        .Distinct(StringComparer.OrdinalIgnoreCase)
	        .ToArray();

        var exportedCultures = exportResources
            .Where(resource => !resource.File.IsNeutral)
            .Select(resource => resource.File.Culture)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string? projectName = ResolveExportProjectName(directory);

        string suggestedFileName = WorkbookFileNameHelper.Create(
            projectName,
            exportedCultures,
            availableCultures.Length);

        var dialog = new SaveFileDialog
        {
            Title = "Export translation workbook",
            Filter = "XLSX workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = suggestedFileName,
            InitialDirectory = directory
        };

        if (dialog.ShowDialog(this) != true)
        {
            SetStatus("Export cancelled.");
            return;
        }

        string outputPath = dialog.FileName;

        await RunOperationAsync(
            "Exporting translation workbook...",
            async () =>
            {
                await Task.Run(
                    () => _exportService.Export(
                        exportResources,
                        outputPath));

                SetStatus(
                    $"Export completed: {Path.GetFileName(outputPath)}");

                MessageBox.Show(
                    this,
                    "The translation workbook was exported successfully." +
                    Environment.NewLine +
                    Environment.NewLine +
                    outputPath,
                    "Export completed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
    }

    /// <summary>
    /// Resolves an optional project name for exported workbook filenames.
    /// </summary>
    private string? ResolveExportProjectName(string resourceDirectory)
    {
        var configuredProject = _settingsService.FindProject(
            _settings,
            resourceDirectory);

        if (!string.IsNullOrWhiteSpace(configuredProject?.ProjectName))
        {
            return configuredProject.ProjectName;
        }

        try
        {
            var detectionService = new ProjectDetectionService();

            var projectFiles = detectionService.FindProjectFiles(
                resourceDirectory);

            if (projectFiles.Count == 1)
            {
                return detectionService.GetProjectName(projectFiles[0]);
            }
        }
        catch (IOException)
        {
            // Project detection is optional for workbook exports.
        }
        catch (UnauthorizedAccessException)
        {
            // Project detection is optional for workbook exports.
        }

        return null;
    }


    private async void ImportButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_resourceDirectory is null ||
            !ConfirmDiscardPreview())
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import translation workbook",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = _resourceDirectory
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        string workbookPath = dialog.FileName;
        string directory = _resourceDirectory;

        ClearPreview();

        await RunOperationAsync(
            "Validating translation workbook...",
            async () =>
            {
                var result = await Task.Run(
                    () =>
                    {
                        var resources =
                            _discoveryService.Discover(directory);

                        var preview = _importService.Preview(
                            workbookPath,
                            resources);

                        return (Resources: resources, Preview: preview);
                    });

                SetResources(result.Resources);
                SetPreview(result.Preview);

                SetStatus(result.Preview.HasErrors
                    ? "Import validation failed. Review the issues."
                    : result.Preview.HasChanges
                        ? "Import preview is ready for review."
                        : "No applicable translation changes were found.");
            });
    }

    private void SetPreview(ImportPreview preview)
    {
        _preview = preview;

        ChangesDataGrid.ItemsSource = preview.Changes;
        IssuesDataGrid.ItemsSource = preview.Issues;

        int errorCount = preview.Issues.Count(
            issue => issue.Severity == ImportIssueSeverity.Error);

        int warningCount = preview.Issues.Count(
            issue => issue.Severity == ImportIssueSeverity.Warning);

        ChangesTab.Header =
            $"Proposed changes ({preview.Changes.Count:N0})";

        IssuesTab.Header =
            $"Validation issues ({preview.Issues.Count:N0})";

        PreviewSummaryTextBlock.Text =
            $"{preview.Changes.Count:N0} proposed changes  |  " +
            $"{errorCount:N0} errors  |  " +
            $"{warningCount:N0} warnings";

        PreviewTabControl.SelectedItem = preview.HasErrors
            ? IssuesTab
            : ChangesTab;

        UpdateControls();
    }

    private void ClearPreviewButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!ConfirmDiscardPreview())
        {
            return;
        }

        ClearPreview();

        SetStatus("Import preview cleared.");
    }

    private void ClearPreview()
    {
        _preview = null;

        ChangesDataGrid.ItemsSource = null;
        IssuesDataGrid.ItemsSource = null;

        ChangesTab.Header = "Proposed changes (0)";
        IssuesTab.Header = "Validation issues (0)";

        PreviewSummaryTextBlock.Text =
            "Import a workbook to review proposed changes.";

        PreviewTabControl.SelectedItem = ChangesTab;

        UpdateControls();
    }

    private bool ConfirmDiscardPreview()
    {
        if (_preview is null || !_preview.HasChanges)
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            "The current import preview contains unapplied changes." +
            Environment.NewLine +
            Environment.NewLine +
            "Discard the current preview and continue?",
            "Discard import preview",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private async void ApplyButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (_isBusy ||
            _preview is not { CanApply: true } preview ||
            _resourceDirectory is null)
        {
            return;
        }

        string directory = _resourceDirectory;

        ProjectBackupSettings? project;
        string backupRoot;
        string projectBackupDirectory;

        try
        {
            project = EnsureProjectBackupSettings(directory);

            if (project is null)
            {
                return;
            }

            backupRoot =
                _settingsService.ResolveBackupDirectory(
                    _settings,
                    project);

            var backupService = new BackupService();

            backupService.ValidateBackupDirectory(
                backupRoot,
                project);

            projectBackupDirectory =
                backupService.GetProjectBackupDirectory(
                    backupRoot,
                    project);
        }
        catch (Exception exception)
        {
            SetStatus("Backup configuration validation failed.");

            MessageBox.Show(
                this,
                "The configured backup destination is invalid." +
                Environment.NewLine +
                Environment.NewLine +
                exception.Message +
                Environment.NewLine +
                Environment.NewLine +
                "Review your backup settings before applying changes.",
                "Backup configuration",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        int affectedFiles = preview.Changes
            .Select(change => change.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        string warningText = preview.HasWarnings
            ? Environment.NewLine +
              Environment.NewLine +
              "Validation warnings are present. " +
              "Only applicable changes will be written."
            : string.Empty;

        var confirmation = MessageBox.Show(
            this,
            $"Apply {preview.Changes.Count:N0} translation changes " +
            $"to {affectedFiles:N0} resource files?" +
            Environment.NewLine +
            Environment.NewLine +
            $"Project: {project.ProjectName}" +
            Environment.NewLine +
            Environment.NewLine +
            "Original files will be backed up before modification." +
            Environment.NewLine +
            Environment.NewLine +
            "Backup destination:" +
            Environment.NewLine +
            projectBackupDirectory +
            warningText,
            "Apply translation changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunOperationAsync(
            "Applying translation changes...",
            async () =>
            {
                var result = await Task.Run(
                    () => _applyService.Apply(
                        preview,
                        directory,
                        project,
                        backupRoot));

                ClearPreview();

                // Invalidate the old resource snapshot immediately.
                SetResources(Array.Empty<ResourceFileData>());

                SetStatus("Translation changes applied successfully.");

                MessageBox.Show(
                    this,
                    $"{result.ChangedValues:N0} resource values updated." +
                    Environment.NewLine +
                    $"{result.ChangedFiles:N0} resource files modified." +
                    Environment.NewLine +
                    Environment.NewLine +
                    "Original file backups:" +
                    Environment.NewLine +
                    result.BackupFolder,
                    "Update completed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                try
                {
                    var resources = await Task.Run(
                        () => _discoveryService.Discover(directory));

                    SetResources(resources);

                    SetStatus(
                        $"Successfully updated " +
                        $"{result.ChangedValues:N0} resource values.");
                }
                catch (Exception exception)
                {
                    SetStatus(
                        "Changes applied, but resource refresh failed.");

                    MessageBox.Show(
                        this,
                        "The translation changes were saved successfully, " +
                        "but the resource list could not be refreshed." +
                        Environment.NewLine +
                        Environment.NewLine +
                        exception.Message,
                        "Resource refresh failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            });
    }

    private async Task RunOperationAsync(
        string status,
        Func<Task> operation)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;

        SetStatus(status);
        UpdateControls();

        Mouse.OverrideCursor = Cursors.Wait;

        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            SetStatus("Operation failed.");

            MessageBox.Show(
                this,
                exception.Message,
                "ResX Bridge",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;

            _isBusy = false;

            UpdateControls();
        }
    }

    private void UpdateControls()
    {
        bool hasDirectory =
            !string.IsNullOrWhiteSpace(_resourceDirectory);

        bool hasResources = _resources.Count > 0;

        BrowseButton.IsEnabled = !_isBusy;

        BackupSettingsButton.IsEnabled = !_isBusy;

        RefreshButton.IsEnabled =
            !_isBusy && hasDirectory;

        ExportButton.IsEnabled =
            !_isBusy && hasResources;

        ImportButton.IsEnabled =
            !_isBusy && hasResources;

        ClearPreviewButton.IsEnabled =
            !_isBusy && _preview is not null;

        ApplyButton.IsEnabled =
            !_isBusy && _preview is { CanApply: true };
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.ToolTip = message;
    }

    protected override void OnClosing(
        System.ComponentModel.CancelEventArgs e)
    {
        if (_isBusy)
        {
            e.Cancel = true;
            return;
        }

        if (!ConfirmDiscardPreview())
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    private void BackupSettings_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        ShowBackupSettings();
    }

    private bool ShowBackupSettings()
    {
        try
        {
            var window = new BackupSettingsWindow(
                _settings,
                _resourceDirectory)
            {
                Owner = this
            };

            if (window.ShowDialog() != true ||
                window.SavedSettings is null)
            {
                return false;
            }

            _settings = window.SavedSettings;

            SetStatus("Backup settings saved.");

            return true;
        }
        catch (Exception exception)
        {
            SetStatus("Unable to configure backup settings.");

            MessageBox.Show(
                this,
                exception.Message,
                "Backup settings",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
    }

    private ProjectBackupSettings? EnsureProjectBackupSettings(
        string resourceDirectory)
    {
        ProjectBackupSettings? project =
            _settingsService.FindProject(
                _settings,
                resourceDirectory);

        if (project is not null)
        {
            return project;
        }

        if (!ShowBackupSettings())
        {
            SetStatus("Update cancelled. Backup settings were not confirmed.");

            return null;
        }

        return _settingsService.FindProject(
            _settings,
            resourceDirectory);
    }
}
