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
                // Find the primary Grid declared in MainWindow.xaml
                var grid = FindChildOfType<Grid>(main);
                if (grid != null)
                {
                    snapshot.VisualPath.Add(GetElementPath(grid));

                    // Find the primary content Border in column 2 (center)
                    var centerBorder = grid.Children.OfType<Border>().FirstOrDefault(b => Grid.GetColumn(b) == 2 && Grid.GetRow(b) == 0);
                    if (centerBorder != null)
                    {
                        snapshot.VisualPath.Add(GetElementPath(centerBorder));

                        // Find the StackPanel inside it
                        var sp = centerBorder.Child as StackPanel;
                        if (sp != null)
                        {
                            snapshot.VisualPath.Add(GetElementPath(sp));

                            // Collect first-level TextBlock.Text values
                            var tbs = sp.Children.OfType<TextBlock>().ToList();
                            for (int i = 0; i < tbs.Count; i++)
                            {
                                var tb = tbs[i];
                                var key = $"TextBlock[{i}]_{tb.FontSize}_{(tb.FontWeight.ToString())}";
                                snapshot.TextBlockValues[key] = tb.Text;
                            }

                            // Also capture the StackPanel DataContext type name
                            var dc = sp.DataContext;
                            snapshot.StackPanelDataContextType = dc?.GetType().FullName;

                            // If ActivityViewModel, capture a few projection properties via reflection
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
                        }
                    }
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

        private class PresentationSnapshot
        {
            public DateTime TimestampUtc { get; set; }
            public string? MainWindowTitle { get; set; }
            public List<string>? VisualPath { get; set; }
            public Dictionary<string, string?>? TextBlockValues { get; set; }
            public string? StackPanelDataContextType { get; set; }
            public Dictionary<string, string?>? ActivityProjection { get; set; }
        }
    }
}
