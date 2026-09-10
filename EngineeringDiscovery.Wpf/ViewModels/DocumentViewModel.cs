using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using EngineeringDiscovery.Wpf.Services;

namespace EngineeringDiscovery.Wpf.ViewModels;

public sealed class DocumentViewModel : ObservableObject
{
    private readonly IRepositoryFileService _fileService;
    private string _text = string.Empty;
    private bool _isDirty;
    private bool _isReadOnly;
    private string? _loadError;

    public DocumentViewModel(string filePath, string repositoryPath, IRepositoryFileService fileService)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        RepositoryPath = repositoryPath ?? throw new ArgumentNullException(nameof(repositoryPath));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        RelativePath = Path.GetRelativePath(repositoryPath, filePath).Replace(Path.DirectorySeparatorChar, '/');
        DisplayName = Path.GetFileName(filePath);
        Language = GetLanguage(Path.GetExtension(filePath));
    }

    public string FilePath { get; }
    public string RepositoryPath { get; }
    public string RelativePath { get; }
    public string DisplayName { get; private set; }
    public string Language { get; }
    public bool IsReadOnly
    {
        get => _isReadOnly;
        private set => SetProperty(ref _isReadOnly, value);
    }
    public bool IsLoaded { get; private set; }
    public string? LoadError
    {
        get => _loadError;
        private set => SetProperty(ref _loadError, value);
    }
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (!SetProperty(ref _isDirty, value)) return;
            OnPropertyChanged(nameof(TabHeader));
        }
    }
    public string TabHeader => IsDirty ? $"{DisplayName} *" : DisplayName;
    public string Text
    {
        get => _text;
        private set => SetProperty(ref _text, value);
    }

    public async Task LoadAsync()
    {
        try
        {
            if (RepositoryFileService.IsProbablyBinaryFile(FilePath))
            {
                IsReadOnly = true;
                Text = "This file is not a text document and cannot be edited in the code editor.";
            }
            else
            {
                IsReadOnly = false;
                Text = await _fileService.ReadFileAsync(FilePath).ConfigureAwait(false);
            }
            IsLoaded = true;
            LoadError = null;
            IsDirty = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            IsReadOnly = true;
            LoadError = ex.Message;
            Text = $"Unable to open this file: {ex.Message}";
            IsLoaded = false;
        }
    }

    public void UpdateFromEditor(string text)
    {
        if (IsReadOnly || !IsLoaded) return;
        if (Text == text) return;
        Text = text;
        IsDirty = true;
    }

    public async Task SaveAsync()
    {
        if (IsReadOnly || !IsLoaded) return;
        await _fileService.SaveFileAsync(FilePath, Text).ConfigureAwait(false);
        IsDirty = false;
    }

    private static string GetLanguage(string extension) => extension.ToLowerInvariant() switch
    {
        ".cs" => "C#",
        ".xaml" => "XML",
        ".xml" => "XML",
        ".json" => "JSON",
        ".ps1" => "PowerShell",
        ".md" => "Markdown",
        ".css" => "CSS",
        ".js" or ".ts" => "JavaScript",
        _ => "Text"
    };
}
