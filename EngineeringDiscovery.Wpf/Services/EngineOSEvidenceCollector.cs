using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EngineeringDiscovery.Wpf.ViewModels;

namespace EngineeringDiscovery.Wpf.Services
{
    /// <summary>
    /// Presentation-layer evidence collector for EngineOS.
    /// Collects a snapshot of the visual tree and selected presentation properties and
    /// writes the snapshot as JSON to local application data.
    /// Non-invasive: reads runtime values only, no behavioral changes.
    /// </summary>
    public static class EngineOSEvidenceCollector
    {
        private const string EvidenceFolderName = "EngineOS-Evidence";
        private const string EvidenceFileName = "presentation_snapshot.json";

        public static async Task<string> CollectAsync(Window main)
        {
            if (main is null) throw new ArgumentNullException(nameof(main));

            var snapshot = new PresentationSnapshot
            {
                TimestampUtc = DateTime.UtcNow,
                MainWindowTitle = main.Title,
                VisualPath = new List<string>(),
                TextBlockValues = new Dictionary<string, string?>()
            };

            try
            {
                // Locate the StackPanel that presents the Activity projection.
                // Prefer explicit DataContext-based discovery over positional assumptions.
                var candidate = FindStackPanelBoundToActivity(main);
                if (candidate != null)
                {
                    snapshot.VisualPath.Add(GetElementPath(candidate));

                    // Collect descendant TextBlock.Text values (preserve order by tree traversal)
                    var tbs = FindAllChildrenOfType<TextBlock>(candidate).ToList();
                    for (int i = 0; i < tbs.Count; i++)
                    {
                        var tb = tbs[i];
                        var key = $"TextBlock[{i}]_{tb.FontSize}_{(tb.FontWeight.ToString())}";
                        snapshot.TextBlockValues[key] = tb.Text;
                    }

                    var dc = candidate.DataContext;
                    snapshot.StackPanelDataContextType = dc?.GetType().FullName;

                    if (dc != null)
                    {
                        var type = dc.GetType();
                        string? GetProp(string name)
                        {
                            try
                            {
                                var p = type.GetProperty(name);
                                if (p is null) return null;
                                var v = p.GetValue(dc);
                                return v?.ToString();
                            }
                            catch { return null; }
                        }

                        snapshot.ActivityProjection = new Dictionary<string, string?>
                        {
                            ["Title"] = GetProp("Title"),
                            ["Type"] = GetProp("Type"),
                            ["Status"] = GetProp("Status"),
                            ["Intent"] = GetProp("Intent"),
                            ["CurrentObservationDescription"] = GetProp("CurrentObservationDescription"),
                            ["CurrentHypothesisDescription"] = GetProp("CurrentHypothesisDescription"),
                            ["CurrentEvidenceRequestTarget"] = GetProp("CurrentEvidenceRequestTarget"),
                            ["CurrentRecoveredUnderstandingStatement"] = GetProp("CurrentRecoveredUnderstandingStatement")
                        };
                    }

                    // Capture visual properties for the center Border (ancestor), the Activity StackPanel, and the first bound TextBlock
                    try
                    {
                        var centerBorder = FindAncestorOfType<Border>(candidate);
                        if (centerBorder != null)
                        {
                            snapshot.ElementProperties ??= new Dictionary<string, Dictionary<string, string?>>();
                            snapshot.ElementProperties["CenterBorder"] = CaptureVisualProperties(centerBorder);
                        }

                        snapshot.ElementProperties ??= new Dictionary<string, Dictionary<string, string?>>();
                        snapshot.ElementProperties["ActivityStackPanel"] = CaptureVisualProperties(candidate);

                        var firstBound = tbs.FirstOrDefault(tb => System.Windows.Data.BindingOperations.GetBindingExpression(tb, TextBlock.TextProperty) != null) ?? tbs.FirstOrDefault();
                        if (firstBound != null)
                        {
                            snapshot.ElementProperties["FirstTextBlock"] = CaptureVisualProperties(firstBound);
                        }
                        // Capture ancestor chain from candidate up to Window (inclusive)
                        var chain = new List<Dictionary<string, string?>>();
                        DependencyObject cur = candidate;
                        while (cur != null)
                        {
                            var entry = new Dictionary<string, string?>();
                            var t = cur.GetType();
                            entry["Type"] = t.Name;
                            if (cur is FrameworkElement fe)
                            {
                                entry["Name"] = string.IsNullOrEmpty(fe.Name) ? null : fe.Name;
                                try
                                {
                                    var col = Grid.GetColumn(fe);
                                    var row = Grid.GetRow(fe);
                                    entry["Grid.Column"] = col.ToString();
                                    entry["Grid.Row"] = row.ToString();
                                }
                                catch { }
                            }
                            var parent = System.Windows.Media.VisualTreeHelper.GetParent(cur);
                            entry["ParentType"] = parent?.GetType().Name;
                            chain.Add(entry);
                            cur = parent;
                        }
                        snapshot.AncestorChain = chain;

                        // Compare chain to expected center Border presence
                        var passes = chain.Any(e => e.TryGetValue("Type", out var v) && v == "Border" && e.TryGetValue("Grid.Column", out var c) && c == "2");
                        snapshot.AncestorDivergence = !passes;
                        if (!passes)
                        {
                            snapshot.AncestorDivergenceMessage = "Activity StackPanel ancestor chain did not include a Border with Grid.Column=2. Collector expected center content Border but found different ancestry.";
                        }
                        // Inspect siblings of the center Border and top-level adorner/popups for potential overlays
                        try
                        {
                            var overlays = new List<Dictionary<string, string?>>();
                            // siblings in the same Grid
                            var centerBorderObj = chain.FirstOrDefault(e => e.TryGetValue("Type", out var t) && t == "Border" && e.TryGetValue("Grid.Column", out var c) && c == "2");
                            // find actual Border instance by walking children of the parent grid
                            var parentGrid = FindAncestorOfType<System.Windows.Controls.Grid>(candidate) as System.Windows.Controls.Grid;
                            if (parentGrid != null)
                            {
                                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parentGrid); i++)
                                {
                                    var child = System.Windows.Media.VisualTreeHelper.GetChild(parentGrid, i) as FrameworkElement;
                                    if (child == null) continue;
                                    var info = new Dictionary<string, string?>();
                                    info["Type"] = child.GetType().Name;
                                    info["Name"] = string.IsNullOrEmpty(child.Name) ? null : child.Name;
                                    try { info["Visibility"] = child.Visibility.ToString(); } catch { info["Visibility"] = null; }
                                    try { info["Opacity"] = child.Opacity.ToString(); } catch { info["Opacity"] = null; }
                                    try { info["ZIndex"] = System.Windows.Controls.Panel.GetZIndex(child).ToString(); } catch { info["ZIndex"] = null; }
                                    try { info["ActualWidth"] = child.ActualWidth.ToString(); info["ActualHeight"] = child.ActualHeight.ToString(); } catch { }
                                    overlays.Add(info);
                                }
                            }

                            // AdornerLayer children
                            var adornerLayer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(main);
                            if (adornerLayer != null)
                            {
                                var adorners = adornerLayer.GetAdorners(main) ?? Array.Empty<System.Windows.Documents.Adorner>();
                                foreach (var a in adorners)
                                {
                                    var info = new Dictionary<string, string?>();
                                    info["Type"] = a.GetType().Name;
                                    info["Visibility"] = (a as UIElement)?.IsVisible.ToString();
                                    overlays.Add(info);
                                }
                            }

                            // Popups (scan visual tree for Popup instances)
                            var popups = FindAllChildrenOfType<System.Windows.Controls.Primitives.Popup>(main).ToList();
                            foreach (var p in popups)
                            {
                                var info = new Dictionary<string, string?>();
                                info["Type"] = p.GetType().Name;
                                info["IsOpen"] = p.IsOpen.ToString();
                                try { info["PlacementTarget"] = p.PlacementTarget?.GetType().Name; } catch { }
                                overlays.Add(info);
                            }

                            snapshot.Overlays = overlays;
                            // Simple divergence: any sibling occupying full size and visible with ZIndex >= center
                            var center = parentGrid?.Children.OfType<FrameworkElement>().FirstOrDefault(ch => System.Windows.Controls.Grid.GetColumn(ch) == 2);
                            if (center != null)
                            {
                                foreach (var ov in overlays)
                                {
                                    if (ov.TryGetValue("Visibility", out var v) && v == "Visible")
                                    {
                                        // if any overlay has no ZIndex or same/higher, treat as potential cover
                                        if (!ov.TryGetValue("ZIndex", out var z) || int.TryParse(z, out var zi) && zi >= (System.Windows.Controls.Panel.GetZIndex(center)))
                                        {
                                            snapshot.OverlayFound = true;
                                            snapshot.OverlayMessage = "Visible sibling or overlay with equal or higher ZIndex found.";
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    catch { }
                }

                // Write snapshot
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), EvidenceFolderName);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, EvidenceFileName);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(snapshot, options);
                await File.WriteAllTextAsync(path, json).ConfigureAwait(false);

                // Also write path to Debug output
                System.Diagnostics.Debug.WriteLine($"EngineOS Evidence written: {path}");

                return path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EngineOS EvidenceCollector failed: {ex}");
                throw;
            }
        }

        private static string GetElementPath(FrameworkElement el)
        {
            if (el is null) return "(null)";
            var parts = new List<string>();
            DependencyObject cur = el;
            while (cur != null)
            {
                if (cur is FrameworkElement fe && !string.IsNullOrEmpty(fe.GetType().Name))
                {
                    var name = fe.Name;
                    parts.Insert(0, string.IsNullOrEmpty(name) ? fe.GetType().Name : $"{fe.GetType().Name}#{name}");
                }
                else
                {
                    parts.Insert(0, cur.GetType().Name);
                }
                cur = System.Windows.Media.VisualTreeHelper.GetParent(cur);
                if (cur is Window) break; // stop at window for brevity
            }
            return string.Join(" -> ", parts);
        }

        private static T? FindChildOfType<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is T t) return t;
                var found = FindChildOfType<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        // Note: FindAncestorOfType and CaptureVisualProperties are defined below and used earlier in the file.

        private static IEnumerable<T> FindAllChildrenOfType<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is T t) yield return t;
                foreach (var desc in FindAllChildrenOfType<T>(child)) yield return desc;
            }
        }

        private static StackPanel? FindStackPanelBoundToActivity(DependencyObject root)
        {
            if (root == null) return null;

            // First, prefer explicitly named StackPanel if present
            var named = FindAllChildrenOfType<StackPanel>(root).OfType<FrameworkElement>().FirstOrDefault(f => !string.IsNullOrEmpty(f.Name)) as StackPanel;
            if (named != null)
            {
                // Heuristic: check DataContext type or child TextBlock content
                if (IsActivityPresentation(named)) return named;
            }

            // Otherwise, search all StackPanels and find one whose DataContext type name contains 'Activity' or whose descendants include text that looks like activity labels
            foreach (var spObj in FindAllChildrenOfType<StackPanel>(root))
            {
                var sp = spObj as StackPanel;
                if (sp == null) continue;
                if (IsActivityPresentation(sp)) return sp;
            }

            return null;
        }

        private static bool IsActivityPresentation(StackPanel sp)
        {
            try
            {
                var dc = sp.DataContext;
                if (dc != null && dc.GetType().Name.Contains("Activity")) return true;

                // Check descendant TextBlocks for known labels like 'Current Activity' or 'Intent'
                var texts = FindAllChildrenOfType<TextBlock>(sp).Select(tb => tb.Text ?? string.Empty).ToList();
                if (texts.Any(t => t.Contains("Current Activity") || t.Contains("Intent") || t.Contains("Observation"))) return true;
            }
            catch { }
            return false;
        }

        private static DependencyObject? FindAncestorOfType<T>(DependencyObject child) where T : DependencyObject
        {
            if (child == null) return null;
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T) return parent;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private static Dictionary<string, string?> CaptureVisualProperties(DependencyObject obj)
        {
            var dict = new Dictionary<string, string?>();
            try
            {
                if (obj is UIElement ue)
                {
                    dict["Visibility"] = (ue is FrameworkElement fe) ? fe.Visibility.ToString() : null;
                    dict["IsVisible"] = ue.IsVisible.ToString();
                    dict["Opacity"] = (ue is FrameworkElement fe2) ? fe2.Opacity.ToString() : null;
                    dict["ActualWidth"] = (ue is FrameworkElement fe3) ? fe3.ActualWidth.ToString() : null;
                    dict["ActualHeight"] = (ue is FrameworkElement fe4) ? fe4.ActualHeight.ToString() : null;
                }

                if (obj is FrameworkElement fobj)
                {
                    // Foreground / Background available on Control/TextBlock/Panel where applicable
                    if (fobj is System.Windows.Controls.Control c)
                    {
                        dict["Foreground"] = c.Foreground?.ToString();
                        dict["Background"] = c.Background?.ToString();
                    }
                    else if (fobj is TextBlock tb)
                    {
                        dict["Foreground"] = tb.Foreground?.ToString();
                        dict["Background"] = tb.Background?.ToString();
                    }
                    else if (fobj is System.Windows.Controls.Panel p)
                    {
                        dict["Background"] = p.Background?.ToString();
                    }

                    if (fobj is UIElement ue2)
                    {
                        var z = System.Windows.Controls.Panel.GetZIndex(ue2);
                        dict["ZIndex"] = z.ToString();
                    }
                }
            }
            catch { }
            return dict;
        }

        private class PresentationSnapshot
        {
            public DateTime TimestampUtc { get; set; }
            public string? MainWindowTitle { get; set; }
            public List<string>? VisualPath { get; set; }
            public Dictionary<string, string?>? TextBlockValues { get; set; }
            public string? StackPanelDataContextType { get; set; }
            public Dictionary<string, string?>? ActivityProjection { get; set; }
            public Dictionary<string, Dictionary<string, string?>>? ElementProperties { get; set; }
            public List<Dictionary<string, string?>>? AncestorChain { get; set; }
            public bool AncestorDivergence { get; set; }
            public string? AncestorDivergenceMessage { get; set; }
            public List<Dictionary<string, string?>>? Overlays { get; set; }
            public bool OverlayFound { get; set; }
            public string? OverlayMessage { get; set; }
        }
    }
}
