using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ResxBridge.Models;
using ResxBridge.Services;

namespace ResxBridge;

/// <summary>
/// Provides application-wide and project-specific
/// backup configuration.
/// </summary>
public partial class BackupSettingsWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly ProjectDetectionService _projectDetectionService = new();
    private readonly BackupService _backupService = new();

    private readonly ApplicationSettings _workingSettings;
    private readonly string? _resourceDirectory;

    private bool _isInitializing = true;

    /// <summary>
    /// Gets the saved settings after successful confirmation.
    /// </summary>
    public ApplicationSettings? SavedSettings { get; private set; }

    public BackupSettingsWindow(
        ApplicationSettings settings,
        string? resourceDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _workingSettings = CloneSettings(settings);

        _resourceDirectory =
            string.IsNullOrWhiteSpace(resourceDirectory)
                ? null
                : SettingsService.NormalizeDirectory(
                    resourceDirectory);

        InitializeComponent();

        InitializeSettings();

        _isInitializing = false;

        UpdateEffectiveBackupDirectory();
    }

    private void InitializeSettings()
    {
        DefaultBackupDirectoryTextBox.Text =
            _settingsService.ResolveBackupDirectory(
                _workingSettings);

        if (_resourceDirectory is null)
        {
            ProjectSettingsPanel.Visibility =
                Visibility.Collapsed;

            NoProjectPanel.Visibility =
                Visibility.Visible;

            return;
        }

        ProjectSettingsPanel.Visibility =
            Visibility.Visible;

        NoProjectPanel.Visibility =
            Visibility.Collapsed;

        ResourceDirectoryTextBox.Text =
            _resourceDirectory;

        ProjectBackupSettings? existingProject =
            _settingsService.FindProject(
                _workingSettings,
                _resourceDirectory);

        InitializeProjectCandidates(existingProject);

        if (existingProject is not null)
        {
            ProjectNameTextBox.Text =
                existingProject.ProjectName;

            CustomBackupDirectoryTextBox.Text =
                existingProject.BackupDirectoryOverride
                ?? string.Empty;

            UseCustomBackupDirectoryCheckBox.IsChecked =
                !string.IsNullOrWhiteSpace(
                    existingProject.BackupDirectoryOverride);
        }
        else
        {
            var selectedProject =
                ProjectFileComboBox.SelectedItem
                    as ProjectCandidate;

            ProjectNameTextBox.Text =
                selectedProject?.FilePath is not null
                    ? _projectDetectionService.GetProjectName(
                        selectedProject.FilePath)
                    : string.Empty;

            UseCustomBackupDirectoryCheckBox.IsChecked =
                false;
        }

        CustomBackupDirectoryPanel.Visibility =
            UseCustomBackupDirectoryCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void InitializeProjectCandidates(
        ProjectBackupSettings? existingProject)
    {
        var candidates = new List<ProjectCandidate>
        {
            new(
                "No associated project file (manual configuration)",
                null)
        };

        IReadOnlyList<string> detectedFiles =
            Array.Empty<string>();

        try
        {
            detectedFiles =
                _projectDetectionService.FindProjectFiles(
                    _resourceDirectory!);

            foreach (string filePath in detectedFiles)
            {
                candidates.Add(
                    new ProjectCandidate(
                        filePath,
                        filePath));
            }

            ProjectDetectionStatusTextBlock.Text =
                detectedFiles.Count switch
                {
                    0 => "No project file was detected. " +
                         "Enter a project name manually.",

                    1 => "One project file was detected. " +
                         "Confirm the suggested project name.",

                    _ => $"{detectedFiles.Count} project files " +
                         "were detected. Select the associated " +
                         "project or configure it manually."
                };
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            ProjectDetectionStatusTextBlock.Text =
                "Automatic project detection was unavailable. " +
                "Manual configuration is still available.";
        }

        if (!string.IsNullOrWhiteSpace(
                existingProject?.ProjectFilePath))
        {
            bool alreadyIncluded = candidates.Any(
                candidate => string.Equals(
                    candidate.FilePath,
                    existingProject.ProjectFilePath,
                    StringComparison.OrdinalIgnoreCase));

            if (!alreadyIncluded)
            {
                candidates.Add(
                    new ProjectCandidate(
                        existingProject.ProjectFilePath,
                        existingProject.ProjectFilePath));
            }
        }

        ProjectFileComboBox.ItemsSource = candidates;

        ProjectFileComboBox.DisplayMemberPath =
            nameof(ProjectCandidate.DisplayName);

        if (existingProject is not null)
        {
            ProjectFileComboBox.SelectedItem =
                candidates.FirstOrDefault(
                    candidate => string.Equals(
                        candidate.FilePath,
                        existingProject.ProjectFilePath,
                        StringComparison.OrdinalIgnoreCase))
                ?? candidates[0];
        }
        else if (detectedFiles.Count == 1)
        {
            ProjectFileComboBox.SelectedIndex = 1;
        }
        else
        {
            ProjectFileComboBox.SelectedIndex = 0;
        }
    }

    private void SettingsChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        UpdateEffectiveBackupDirectory();
    }

    private void ProjectFileComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        if (ProjectFileComboBox.SelectedItem
                is ProjectCandidate candidate &&
            candidate.FilePath is not null)
        {
            ProjectNameTextBox.Text =
                _projectDetectionService.GetProjectName(
                    candidate.FilePath);
        }

        UpdateEffectiveBackupDirectory();
    }

    private void CustomBackupDirectory_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        bool useCustomDirectory =
            UseCustomBackupDirectoryCheckBox.IsChecked == true;

        CustomBackupDirectoryPanel.Visibility =
            useCustomDirectory
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (useCustomDirectory &&
            string.IsNullOrWhiteSpace(
                CustomBackupDirectoryTextBox.Text))
        {
            CustomBackupDirectoryTextBox.Text =
                DefaultBackupDirectoryTextBox.Text;
        }

        UpdateEffectiveBackupDirectory();
    }

    private void BrowseDefaultBackupDirectory_Click(
        object sender,
        RoutedEventArgs e)
    {
        string? selectedDirectory = BrowseForDirectory(
            DefaultBackupDirectoryTextBox.Text,
            "Select default backup location");

        if (selectedDirectory is not null)
        {
            DefaultBackupDirectoryTextBox.Text =
                selectedDirectory;
        }
    }

    private void BrowseCustomBackupDirectory_Click(
        object sender,
        RoutedEventArgs e)
    {
        string? selectedDirectory = BrowseForDirectory(
            CustomBackupDirectoryTextBox.Text,
            "Select project backup location");

        if (selectedDirectory is not null)
        {
            CustomBackupDirectoryTextBox.Text =
                selectedDirectory;
        }
    }

    private void ResetDefaultBackupDirectory_Click(
        object sender,
        RoutedEventArgs e)
    {
        DefaultBackupDirectoryTextBox.Text =
            _settingsService.DefaultBackupDirectory;
    }

    private string? BrowseForDirectory(
        string currentDirectory,
        string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            string expandedDirectory =
                Environment.ExpandEnvironmentVariables(
                    currentDirectory.Trim());

            if (Directory.Exists(expandedDirectory))
            {
                dialog.InitialDirectory =
                    expandedDirectory;
            }
        }

        return dialog.ShowDialog(this) == true
            ? dialog.FolderName
            : null;
    }

    private void UpdateEffectiveBackupDirectory()
    {
        if (_isInitializing)
        {
            return;
        }

        ValidationMessageTextBlock.Text = string.Empty;

        EffectiveBackupDirectoryTextBox.Text =
            string.Empty;

        OpenBackupFolderButton.IsEnabled = false;

        SaveSettingsButton.IsEnabled = false;

        try
        {
            string defaultBackupDirectory =
                GetDefaultBackupDirectory();

            if (_resourceDirectory is null)
            {
                SaveSettingsButton.IsEnabled = true;
                return;
            }

            ProjectBackupSettings project =
                BuildProjectSettings();

            string backupRoot =
                project.BackupDirectoryOverride
                ?? defaultBackupDirectory;

            string effectiveDirectory =
                _backupService.GetProjectBackupDirectory(
                    backupRoot,
                    project);

            EffectiveBackupDirectoryTextBox.Text =
                effectiveDirectory;

            _backupService.ValidateBackupDirectory(
                backupRoot,
                project);

            OpenBackupFolderButton.IsEnabled =
                Directory.Exists(effectiveDirectory);

            SaveSettingsButton.IsEnabled = true;
        }
        catch (Exception ex) when (
            ex is ArgumentException
                or InvalidOperationException
                or IOException
                or UnauthorizedAccessException
                or NotSupportedException)
        {
            ValidationMessageTextBlock.Text =
                ex.Message;
        }
    }

    private string GetDefaultBackupDirectory()
    {
        string directory =
            DefaultBackupDirectoryTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "Select a default backup location.");
        }

        return SettingsService.NormalizeDirectory(
            directory);
    }

    private ProjectBackupSettings BuildProjectSettings()
    {
        if (_resourceDirectory is null)
        {
            throw new InvalidOperationException(
                "No resource directory is selected.");
        }

        string projectName =
            ProjectNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new InvalidOperationException(
                "Enter a project name.");
        }

        var selectedProject =
            ProjectFileComboBox.SelectedItem
                as ProjectCandidate;

        string? backupDirectoryOverride = null;

        if (UseCustomBackupDirectoryCheckBox.IsChecked == true)
        {
            string customDirectory =
                CustomBackupDirectoryTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(customDirectory))
            {
                throw new InvalidOperationException(
                    "Select a custom backup location.");
            }

            backupDirectoryOverride =
                SettingsService.NormalizeDirectory(
                    customDirectory);
        }

        return new ProjectBackupSettings
        {
            ProjectName = projectName,

            ResourceDirectory = _resourceDirectory,

            ProjectFilePath = selectedProject?.FilePath,

            DirectoryId =
                SettingsService.CreateDirectoryId(
                    _resourceDirectory),

            BackupDirectoryOverride =
                backupDirectoryOverride
        };
    }

    private void OpenBackupFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        string directory =
            EffectiveBackupDirectoryTextBox.Text;

        if (!Directory.Exists(directory))
        {
            ValidationMessageTextBlock.Text =
                "The backup directory does not exist yet.";

            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            ValidationMessageTextBlock.Text =
                $"Unable to open the backup folder: {ex.Message}";
        }
    }

    private void SaveSettings_Click(
        object sender,
        RoutedEventArgs e)
    {
        ValidationMessageTextBlock.Text = string.Empty;

        try
        {
            string defaultBackupDirectory =
                GetDefaultBackupDirectory();

            var candidateSettings =
                CloneSettings(_workingSettings);

            candidateSettings.DefaultBackupDirectory =
                string.Equals(
                    defaultBackupDirectory,
                    _settingsService.DefaultBackupDirectory,
                    StringComparison.OrdinalIgnoreCase)
                    ? null
                    : defaultBackupDirectory;

            if (_resourceDirectory is not null)
            {
                ProjectBackupSettings project =
                    BuildProjectSettings();

                _settingsService.UpsertProject(
                    candidateSettings,
                    project);
            }

            foreach (var project in candidateSettings.Projects)
            {
                string backupRoot =
                    _settingsService.ResolveBackupDirectory(
                        candidateSettings,
                        project);

                _backupService.ValidateBackupDirectory(
                    backupRoot,
                    project);
            }

            ValidateWritableDirectory(
                defaultBackupDirectory);

            foreach (var project in candidateSettings.Projects)
            {
                string backupRoot =
                    _settingsService.ResolveBackupDirectory(
                        candidateSettings,
                        project);

                string projectBackupDirectory =
                    _backupService.GetProjectBackupDirectory(
                        backupRoot,
                        project);

                ValidateWritableDirectory(
                    projectBackupDirectory);
            }

            _settingsService.Save(candidateSettings);

            SavedSettings = candidateSettings;

            DialogResult = true;
        }
        catch (Exception ex)
        {
            ValidationMessageTextBlock.Text =
                $"Unable to save settings: {ex.Message}";
        }
    }

    private static void ValidateWritableDirectory(
        string directory)
    {
        Directory.CreateDirectory(directory);

        string testFilePath = Path.Combine(
            directory,
            $".resxbridge-{Guid.NewGuid():N}.tmp");

        using var stream = new FileStream(
            testFilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            options: FileOptions.DeleteOnClose);

        stream.WriteByte(0);
        stream.Flush(flushToDisk: true);
    }

    private static ApplicationSettings CloneSettings(
        ApplicationSettings settings)
    {
        return new ApplicationSettings
        {
            DefaultBackupDirectory =
                settings.DefaultBackupDirectory,

            LastResourceDirectory =
                settings.LastResourceDirectory,

            Projects = settings.Projects
                .Select(project =>
                    new ProjectBackupSettings
                    {
                        ProjectName =
                            project.ProjectName,

                        ResourceDirectory =
                            project.ResourceDirectory,

                        ProjectFilePath =
                            project.ProjectFilePath,

                        DirectoryId =
                            project.DirectoryId,

                        BackupDirectoryOverride =
                            project.BackupDirectoryOverride
                    })
                .ToList()
        };
    }

    private sealed record ProjectCandidate(
        string DisplayName,
        string? FilePath);
}
