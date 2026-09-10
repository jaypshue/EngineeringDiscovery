using System.Windows;
using EngineeringDiscovery.Wpf.ViewModels;

namespace EngineeringDiscovery.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly EngineeringDiscovery.Core.Services.WorkspaceState _workspaceState;
        private readonly Services.WindowPlacementService _windowPlacementService;
        private Services.WindowBounds _lastKnownNormalBounds;

        public MainWindow(
            MainWindowViewModel vm,
            EngineeringDiscovery.Core.Services.WorkspaceState workspaceState,
            Services.WindowPlacementService windowPlacementService)
        {
            InitializeComponent();
            _viewModel = vm;
            _workspaceState = workspaceState;
            _windowPlacementService = windowPlacementService;
            DataContext = vm;

            ApplyWindowPlacement(_windowPlacementService.Restore());

            _workspaceState.OnChange += WorkspaceState_OnChange;
            _viewModel.RepositoryImported += RepositoryImported;
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;

            // Instrument center Border child for runtime composition investigation (ED-307)
            Loaded += MainWindow_Loaded;
            ContentRendered += MainWindow_ContentRendered;
            Dispatcher.BeginInvoke(new System.Action(() => LogCenterBorderState("ApplicationIdle (scheduled)")), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Loaded += (_, _) => ShowContentForCurrentState();
        }

        private void WorkspaceState_OnChange()
        {
            if (Dispatcher.CheckAccess())
            {
                ShowContentForCurrentState();
                return;
            }

            Dispatcher.BeginInvoke((System.Action)ShowContentForCurrentState);
        }

        private void RepositoryImported() => WorkspaceState_OnChange();

        private void ShowContentForCurrentState()
        {
            if (_viewModel.HasActiveProject)
            {
                ShowEngineeringWorkspace();
            }
            else
            {
                ShowLauncher();
            }
        }

        public void ShowLauncher()
        {
            if (HostContent.Content is Views.WelcomeView) return;
            HostContent.Content = new Views.WelcomeView();
        }

        public void ShowEngineeringWorkspace()
        {
            if (HostContent.Content is Views.EngineeringWorkspace existing && existing.DataContext is EngineeringWorkspaceViewModel)
            {
                return;
            }

            var partner = App.ServiceProvider?.GetService(typeof(EngineeringDiscovery.Core.Services.IEngineeringPartner)) as EngineeringDiscovery.Core.Services.IEngineeringPartner;
            if (partner is null)
            {
                System.Windows.MessageBox.Show("The engineering workspace is unavailable.", "Engineering Workspace");
                return;
            }

            HostContent.Content = new Views.EngineeringWorkspace
            {
                DataContext = new EngineeringWorkspaceViewModel(partner)
            };
        }

        private void ApplyWindowPlacement(Services.WindowPlacement placement)
        {
            Left = placement.Left;
            Top = placement.Top;
            Width = placement.Width;
            Height = placement.Height;
            _lastKnownNormalBounds = new Services.WindowBounds(placement.Left, placement.Top, placement.Width, placement.Height);
            WindowState = placement.State == Services.WindowPlacementState.Maximized
                ? WindowState.Maximized
                : WindowState.Normal;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var normalBounds = GetNormalBounds();
            if (_windowPlacementService.IsMeaningfullyVisible(normalBounds))
            {
                _lastKnownNormalBounds = normalBounds;
            }
            else
            {
                normalBounds = _lastKnownNormalBounds;
            }

            _windowPlacementService.Save(new Services.WindowPlacement
            {
                Left = normalBounds.Left,
                Top = normalBounds.Top,
                Width = normalBounds.Width,
                Height = normalBounds.Height,
                State = WindowState == WindowState.Maximized
                    ? Services.WindowPlacementState.Maximized
                    : Services.WindowPlacementState.Normal
            });
        }

        private Services.WindowBounds GetNormalBounds()
        {
            if (WindowState == WindowState.Normal)
            {
                return new Services.WindowBounds(Left, Top, Width, Height);
            }

            var restoreBounds = RestoreBounds;
            return new Services.WindowBounds(
                restoreBounds.Left,
                restoreBounds.Top,
                restoreBounds.Width,
                restoreBounds.Height);
        }

        private void MainWindow_Closed(object? sender, System.EventArgs e)
        {
            _workspaceState.OnChange -= WorkspaceState_OnChange;
            _viewModel.RepositoryImported -= RepositoryImported;
        }

        private void NavigateToKnowledgeGraph_Click(object sender, RoutedEventArgs e)
        {
            HostContent.Content = new Views.KnowledgeGraphPlaceholder();
        }

        private void NavigateToInspector_Click(object sender, RoutedEventArgs e)
        {
            HostContent.Content = new Views.InspectorPlaceholder();
        }

        private object? _initialChildRef;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LogCenterBorderState("Loaded");
        }

        private void MainWindow_ContentRendered(object? sender, System.EventArgs e)
        {
            LogCenterBorderState("ContentRendered");
        }

        private void LogCenterBorderState(string phase)
        {
            try
            {
                var child = (this.FindName("CenterBorder") as System.Windows.Controls.Border)?.Child;
                var childType = child?.GetType().FullName ?? "(null)";
                var refHash = child is null ? "(null)" : child.GetHashCode().ToString();
                var childCount = 0;
                if (child is System.Windows.Controls.Panel p)
                {
                    childCount = p.Children.Count;
                }

                var changed = false;
                if (_initialChildRef == null && child != null)
                {
                    _initialChildRef = child;
                }
                else if (_initialChildRef != null && !ReferenceEquals(_initialChildRef, child))
                {
                    changed = true;
                }

                System.Diagnostics.Debug.WriteLine($"[ED-307] {phase}: CenterBorder.Child Type={childType}, Hash={refHash}, ChildCount={childCount}, Changed={changed}");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ED-307] LogCenterBorderState error: {ex}");
            }
        }
    }
}
