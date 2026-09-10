using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.Models;
using EngineeringDiscovery.Wpf.Services;

namespace EngineeringDiscovery.Wpf.ViewModels;

public sealed class DevelopmentSurfaceViewModel : ObservableObject, IDisposable, IConfirmedEngineeringOperationExecutor
{
    private readonly IEngineeringStateQuery _stateQuery;
    private readonly WorkspaceState _workspaceState;
    private readonly IRepositoryFileService _fileService;
    private readonly IGitChangesService _gitChangesService;
    private readonly IDevelopmentCommandService _commandService;
    private CancellationTokenSource? _operationCts;
    private string _repositoryPath = string.Empty;
    private string _repositoryName = "No repository loaded";
    private string _searchQuery = string.Empty;
    private string _statusText = "Ready";
    private bool _isBusy;
    private DocumentViewModel? _activeDocument;
    private GitChange? _selectedChange;
    private string _diffText = "Select a changed file to view its diff.";
    private FileSearchResult? _selectedSearchResult;
    private bool _disposed;

    public DevelopmentSurfaceViewModel(
        IEngineeringStateQuery stateQuery,
        WorkspaceState workspaceState,
        IRepositoryFileService fileService,
        IGitChangesService gitChangesService,
        IDevelopmentCommandService commandService)
    {
        _stateQuery = stateQuery ?? throw new ArgumentNullException(nameof(stateQuery));
        _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _gitChangesService = gitChangesService ?? throw new ArgumentNullException(nameof(gitChangesService));
        _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
        Steering = new EngineeringSteeringViewModel(_stateQuery, _workspaceState);

        OpenDocuments = new ObservableCollection<DocumentViewModel>();
        RootNodes = new ObservableCollection<RepositoryFileNode>();
        SearchResults = new ObservableCollection<FileSearchResult>();
        TextSearchResults = new ObservableCollection<FileSearchResult>();
        GitChanges = new ObservableCollection<GitChange>();
        Problems = new ObservableCollection<ProblemItem>();
        Output = new ObservableCollection<DevelopmentOutputEntry>();
        Results = new ObservableCollection<DevelopmentResultViewModel>();

        RefreshCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(InitializeAsync, () => !IsBusy);
        SearchFilesCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(SearchFilesAsync, () => !IsBusy);
        SearchTextCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(SearchTextAsync, () => !IsBusy);
        SaveCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(SaveActiveAsync, () => ActiveDocument is { IsDirty: true });
        SaveAllCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(SaveAllAsync, () => OpenDocuments.Any(document => document.IsDirty));
        BuildCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(RunBuildAsync, () => !IsBusy && HasRepository);
        TestCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(RunTestAsync, () => !IsBusy && HasRepository);
        RefreshChangesCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => RefreshChangesAsync(), () => !IsBusy && HasRepository);
        CloseDocumentCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<DocumentViewModel>(document => CloseDocument(document));
        ClearOutputCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ClearOutput);

        _workspaceState.OnChange += WorkspaceState_OnChange;
    }

    public ObservableCollection<RepositoryFileNode> RootNodes { get; }
    public ObservableCollection<DocumentViewModel> OpenDocuments { get; }
    public ObservableCollection<FileSearchResult> SearchResults { get; }
    public ObservableCollection<FileSearchResult> TextSearchResults { get; }
    public ObservableCollection<GitChange> GitChanges { get; }
    public ObservableCollection<ProblemItem> Problems { get; }
    public ObservableCollection<DevelopmentOutputEntry> Output { get; }
    public ObservableCollection<DevelopmentResultViewModel> Results { get; }

    public EngineeringSteeringViewModel Steering { get; }

    public ICommand RefreshCommand { get; }
    public ICommand SearchFilesCommand { get; }
    public ICommand SearchTextCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAllCommand { get; }
    public ICommand BuildCommand { get; }
    public ICommand TestCommand { get; }
    public ICommand RefreshChangesCommand { get; }
    public ICommand CloseDocumentCommand { get; }
    public ICommand ClearOutputCommand { get; }

    public string RepositoryPath
    {
        get => _repositoryPath;
        private set => SetProperty(ref _repositoryPath, value);
    }
    public string RepositoryName
    {
        get => _repositoryName;
        private set => SetProperty(ref _repositoryName, value);
    }
    public bool HasRepository => !string.IsNullOrWhiteSpace(RepositoryPath) && Directory.Exists(RepositoryPath);
    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            RaiseCommandStates();
        }
    }
    public DocumentViewModel? ActiveDocument
    {
        get => _activeDocument;
        set
        {
            if (!SetProperty(ref _activeDocument, value)) return;
            RaiseCommandStates();
        }
    }
    public GitChange? SelectedChange
    {
        get => _selectedChange;
        set
        {
            if (!SetProperty(ref _selectedChange, value) || value is null) return;
            _ = SelectChangeAsync(value);
        }
    }
    public FileSearchResult? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set
        {
            if (!SetProperty(ref _selectedSearchResult, value) || value is null) return;
            _ = OpenSearchResultAsync(value);
        }
    }
    public string DiffText
    {
        get => _diffText;
        private set => SetProperty(ref _diffText, value);
    }

    public async Task InitializeAsync()
    {
        var context = _stateQuery.GetWorkspaceContext();
        var path = context?.RepositoryPath ?? string.Empty;
        if (!string.Equals(path, RepositoryPath, StringComparison.OrdinalIgnoreCase))
        {
            RepositoryPath = path;
            RepositoryName = string.IsNullOrWhiteSpace(path) ? "No repository loaded" : Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            OnPropertyChanged(nameof(HasRepository));
        }

        if (!HasRepository)
        {
            RootNodes.Clear();
            GitChanges.Clear();
            OpenDocuments.Clear();
            ActiveDocument = null;
            StatusText = "Open a repository folder to begin.";
            return;
        }

        IsBusy = true;
        StatusText = "Loading repository files…";
        try
        {
            CancelCurrentOperation();
            _operationCts = new CancellationTokenSource();
            var token = _operationCts.Token;
            var root = await _fileService.GetTreeAsync(RepositoryPath, token).ConfigureAwait(true);
            RootNodes.Clear();
            RootNodes.Add(root);
            await RefreshChangesAsync(token).ConfigureAwait(true);
            StatusText = $"{root.Children.Count} top-level items · Ready";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusText = $"Repository files unavailable: {ex.Message}";
            AddOutput(DevelopmentOutputChannel.File, StatusText, true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task OpenFileAsync(RepositoryFileNode? node)
    {
        if (node is null || node.IsDirectory || !HasRepository) return;
        var existing = OpenDocuments.FirstOrDefault(document => string.Equals(document.FilePath, node.FullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ActiveDocument = existing;
            return;
        }

        var document = new DocumentViewModel(node.FullPath, RepositoryPath, _fileService);
        IsBusy = true;
        try
        {
            await document.LoadAsync().ConfigureAwait(true);
            OpenDocuments.Add(document);
            ActiveDocument = document;
            AddOutput(DevelopmentOutputChannel.File, $"Opened {node.RelativePath}");
            if (document.LoadError is not null) AddOutput(DevelopmentOutputChannel.File, document.LoadError, true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SelectDocument(DocumentViewModel? document) => ActiveDocument = document;

    public void UpdateActiveDocumentText(string text)
    {
        ActiveDocument?.UpdateFromEditor(text);
        RaiseCommandStates();
    }

    public async Task SaveActiveAsync()
    {
        if (ActiveDocument is not { IsDirty: true } document) return;
        await document.SaveAsync().ConfigureAwait(true);
        AddOutput(DevelopmentOutputChannel.File, $"Saved {document.RelativePath}");
        RaiseCommandStates();
    }

    public async Task SaveAllAsync()
    {
        foreach (var document in OpenDocuments.Where(document => document.IsDirty).ToArray())
            await document.SaveAsync().ConfigureAwait(true);
        AddOutput(DevelopmentOutputChannel.File, "Saved all modified files.");
        RaiseCommandStates();
    }

    public void CloseDocument(DocumentViewModel? document)
    {
        if (document is null) return;
        if (document.IsDirty)
        {
            StatusText = $"{document.DisplayName} has unsaved changes. Save it before closing.";
            return;
        }
        var index = OpenDocuments.IndexOf(document);
        OpenDocuments.Remove(document);
        if (ReferenceEquals(ActiveDocument, document))
            ActiveDocument = OpenDocuments.Count == 0 ? null : OpenDocuments[Math.Max(0, Math.Min(index, OpenDocuments.Count - 1))];
    }

    public async Task SearchFilesAsync()
    {
        if (!HasRepository || string.IsNullOrWhiteSpace(SearchQuery)) return;
        await RunSearchAsync(_fileService.SearchFileNamesAsync, SearchResults, "file name").ConfigureAwait(true);
    }

    public async Task SearchTextAsync()
    {
        if (!HasRepository || string.IsNullOrWhiteSpace(SearchQuery)) return;
        await RunSearchAsync(_fileService.SearchTextAsync, TextSearchResults, "source text").ConfigureAwait(true);
    }

    public async Task RefreshChangesAsync() => await RefreshChangesAsync(CancellationToken.None).ConfigureAwait(true);

    public async Task SelectChangeAsync(GitChange change)
    {
        try
        {
            DiffText = await _gitChangesService.GetDiffAsync(RepositoryPath, change).ConfigureAwait(true);
            AddOutput(DevelopmentOutputChannel.Git, $"Loaded diff for {change.RelativePath}");
        }
        catch (Exception ex)
        {
            DiffText = $"Unable to read diff: {ex.Message}";
            AddOutput(DevelopmentOutputChannel.Git, DiffText, true);
        }
    }

    private async Task RefreshChangesAsync(CancellationToken cancellationToken)
    {
        if (!HasRepository) return;
        var changes = await _gitChangesService.GetChangesAsync(RepositoryPath, cancellationToken).ConfigureAwait(true);
        GitChanges.Clear();
        foreach (var change in changes) GitChanges.Add(change);
        AddOutput(DevelopmentOutputChannel.Git, $"Git: {changes.Count} changed file(s).");
    }

    private async Task OpenSearchResultAsync(FileSearchResult result)
    {
        var node = new RepositoryFileNode(Path.GetFileName(result.FullPath), result.FullPath, result.RelativePath, false);
        await OpenFileAsync(node).ConfigureAwait(true);
    }

    private async Task RunSearchAsync(
        Func<string, string, CancellationToken, Task<IReadOnlyList<FileSearchResult>>> search,
        ObservableCollection<FileSearchResult> target,
        string description)
    {
        IsBusy = true;
        try
        {
            CancelCurrentOperation();
            _operationCts = new CancellationTokenSource();
            var results = await search(RepositoryPath, SearchQuery, _operationCts.Token).ConfigureAwait(true);
            target.Clear();
            foreach (var result in results) target.Add(result);
            StatusText = $"{results.Count} {description} result(s).";
            AddOutput(DevelopmentOutputChannel.Search, $"Search {description}: {results.Count} result(s) for '{SearchQuery}'.");
        }
        catch (OperationCanceledException) { }
        finally { IsBusy = false; }
    }

    private async Task RunBuildAsync() => await RunCommandAsync(DevelopmentCommandKind.Build).ConfigureAwait(true);
    private async Task RunTestAsync() => await RunCommandAsync(DevelopmentCommandKind.Test).ConfigureAwait(true);

    public async Task<DevelopmentCommandResult?> ExecuteAsync(
        EngineeringOperationKind operation,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy || !HasRepository) return null;

        return operation switch
        {
            EngineeringOperationKind.Build => await RunCommandAsync(DevelopmentCommandKind.Build, cancellationToken).ConfigureAwait(true),
            EngineeringOperationKind.Test => await RunCommandAsync(DevelopmentCommandKind.Test, cancellationToken).ConfigureAwait(true),
            _ => null
        };
    }

    private async Task<DevelopmentCommandResult?> RunCommandAsync(
        DevelopmentCommandKind kind,
        CancellationToken cancellationToken = default)
    {
        if (!HasRepository) return null;
        IsBusy = true;
        Problems.Clear();
        var channel = kind == DevelopmentCommandKind.Build ? DevelopmentOutputChannel.Build : DevelopmentOutputChannel.Test;
        StatusText = kind == DevelopmentCommandKind.Build ? "Building…" : "Running tests…";
        try
        {
            CancelCurrentOperation();
            _operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var result = await _commandService.RunAsync(kind, RepositoryPath, entry => AddOutput(entry.Channel, entry.Text, entry.IsError), _operationCts.Token).ConfigureAwait(true);
            foreach (var problem in result.Problems) Problems.Add(problem);
            Results.Insert(0, new DevelopmentResultViewModel(
                kind == DevelopmentCommandKind.Build ? "Build" : "Tests",
                result.DisplayStatus,
                $"Exit code {result.ExitCode} · {result.Problems.Count} problem(s) · {result.Duration.TotalSeconds:0.0}s",
                DateTimeOffset.Now,
                result.Problems.Count));
            StatusText = result.Succeeded ? $"{(kind == DevelopmentCommandKind.Build ? "Build" : "Tests")} passed." : $"{(kind == DevelopmentCommandKind.Build ? "Build" : "Tests")} failed.";
            return result;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Development command cancelled.";
            AddOutput(channel, StatusText, true);
            return null;
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            AddOutput(channel, StatusText, true);
            Problems.Add(new ProblemItem(ProblemSeverity.Error, ex.Message, Source: kind == DevelopmentCommandKind.Build ? "dotnet build" : "dotnet test"));
            return null;
        }
        finally { IsBusy = false; }
    }

    private void AddOutput(DevelopmentOutputChannel channel, string text, bool isError = false)
    {
        void Add() => Output.Add(new DevelopmentOutputEntry(DateTimeOffset.Now, channel, text, isError));
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Add();
        else dispatcher.BeginInvoke((Action)Add);
    }

    private void ClearOutput() => Output.Clear();

    private void WorkspaceState_OnChange()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) _ = InitializeAsync();
        else dispatcher.BeginInvoke(new Action(() => _ = InitializeAsync()));
    }

    private void CancelCurrentOperation()
    {
        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = null;
    }

    private void RaiseCommandStates()
    {
        (RefreshCommand as CommunityToolkit.Mvvm.Input.AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (SearchFilesCommand as CommunityToolkit.Mvvm.Input.AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (SearchTextCommand as CommunityToolkit.Mvvm.Input.AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (SaveCommand as CommunityToolkit.Mvvm.Input.AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (SaveAllCommand as CommunityToolkit.Mvvm.Input.AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (BuildCommand as CommunityToolkit.Mvvm.Input.AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (TestCommand as CommunityToolkit.Mvvm.Input.AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (RefreshChangesCommand as CommunityToolkit.Mvvm.Input.AsyncRelayCommand)?.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _workspaceState.OnChange -= WorkspaceState_OnChange;
        CancelCurrentOperation();
        Steering.Dispose();
        _disposed = true;
    }
}
