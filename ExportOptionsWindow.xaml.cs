using System.ComponentModel;
using System.Windows;
using ResxBridge.Models;
using ResxBridge.Utilities;

namespace ResxBridge;

/// <summary>
/// Allows the developer to select the target languages
/// to include in an exported translation workbook.
/// </summary>
public partial class ExportOptionsWindow : Window
{
    private readonly List<LanguageOption> _languages;
    private readonly bool _hasNeutralResources;

    /// <summary>
    /// Initializes the export options using the available resource files.
    /// </summary>
    public ExportOptionsWindow(
        IReadOnlyList<ResourceFileData> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        _hasNeutralResources = resources.Any(
            resource => resource.File.IsNeutral);

        _languages = resources
            .Where(resource => !resource.File.IsNeutral)
            .Select(resource => resource.File.Culture)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(culture => new LanguageOption(
                culture,
                CultureHelper.GetLanguageName(culture)))
            .OrderBy(
                language => language.Language,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                language => language.Culture,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        InitializeComponent();

        foreach (var language in _languages)
        {
            language.PropertyChanged += Language_PropertyChanged;
        }

        LanguagesItemsControl.ItemsSource = _languages;

        UpdateSelectionSummary();
    }

    /// <summary>
    /// Gets the target cultures confirmed by the developer.
    /// An empty collection represents a default-only export.
    /// </summary>
    public IReadOnlyList<string> SelectedCultures { get; private set; } =
        Array.Empty<string>();

    private void SelectAll_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetAllLanguages(true);
    }

    private void ClearAll_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetAllLanguages(false);
    }

    private void SetAllLanguages(bool isSelected)
    {
        foreach (var language in _languages)
        {
            language.IsSelected = isSelected;
        }

        UpdateSelectionSummary();
    }

    private void LanguageSelection_Changed(
        object sender,
        RoutedEventArgs e)
    {
        UpdateSelectionSummary();
    }

    private void Language_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LanguageOption.IsSelected))
        {
            UpdateSelectionSummary();
        }
    }

    private void UpdateSelectionSummary()
    {
        if (SelectionSummaryTextBlock is null ||
            ExportButton is null)
        {
            return;
        }

        int selectedCount = _languages.Count(
            language => language.IsSelected);

        int totalCount = _languages.Count;

        if (selectedCount == 0)
        {
            SelectionSummaryTextBlock.Text = _hasNeutralResources
                ? "Default resources only will be exported."
                : "Select at least one target language.";
        }
        else
        {
            string languageLabel =
                totalCount == 1 ? "language" : "languages";

            SelectionSummaryTextBlock.Text =
                $"{selectedCount} of {totalCount} " +
                $"target {languageLabel} selected";
        }

        ExportButton.IsEnabled =
            selectedCount > 0 || _hasNeutralResources;
    }

    private void ExportButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var selectedCultures = _languages
            .Where(language => language.IsSelected)
            .Select(language => language.Culture)
            .ToArray();

        if (selectedCultures.Length == 0 &&
            !_hasNeutralResources)
        {
            return;
        }

        SelectedCultures = Array.AsReadOnly(selectedCultures);

        DialogResult = true;
    }

    /// <summary>
    /// Represents a selectable target culture in the export dialog.
    /// </summary>
    private sealed class LanguageOption : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public LanguageOption(
            string culture,
            string language)
        {
            Culture = culture;
            Language = language;
        }

        public string Culture { get; }

        public string Language { get; }

        public bool IsSelected
        {
            get => _isSelected;

            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;

                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(
                        nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
