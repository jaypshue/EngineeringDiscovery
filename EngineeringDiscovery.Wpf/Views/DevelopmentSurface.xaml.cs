using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Highlighting;
using EngineeringDiscovery.Wpf.Models;
using EngineeringDiscovery.Wpf.ViewModels;

namespace EngineeringDiscovery.Wpf.Views;

public partial class DevelopmentSurface : System.Windows.Controls.UserControl
{
    private bool _synchronizingEditor;

    public DevelopmentSurface()
    {
        InitializeComponent();
        CodeEditor.TextChanged += CodeEditor_TextChanged;
        DocumentTabs.SelectionChanged += DocumentTabs_SelectionChanged;
    }

    private DevelopmentSurfaceViewModel? ViewModel => DataContext as DevelopmentSurfaceViewModel;

    private void RepositoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is RepositoryFileNode node) _ = ViewModel?.OpenFileAsync(node);
    }

    private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource != DocumentTabs || DocumentTabs.SelectedItem is not DocumentViewModel document) return;
        ViewModel?.SelectDocument(document);
        SynchronizeEditor(document);
    }

    private void CodeEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_synchronizingEditor) return;
        ViewModel?.UpdateActiveDocumentText(CodeEditor.Text);
    }

    private void SynchronizeEditor(DocumentViewModel? document)
    {
        _synchronizingEditor = true;
        try
        {
            CodeEditor.Text = document?.Text ?? "Select a file to begin editing.";
            CodeEditor.IsReadOnly = document is null || document.IsReadOnly;
            CodeEditor.SyntaxHighlighting = document is null
                ? null
                : HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(document.FilePath));
        }
        finally
        {
            _synchronizingEditor = false;
        }
    }
}
