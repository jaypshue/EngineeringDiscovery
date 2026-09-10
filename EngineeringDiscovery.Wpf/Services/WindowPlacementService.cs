using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace EngineeringDiscovery.Wpf.Services;

public enum WindowPlacementState
{
    Normal,
    Maximized,
    Minimized
}

public sealed class WindowPlacement
{
    public double Left { get; init; }
    public double Top { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public WindowPlacementState State { get; init; }
}

public readonly record struct WindowBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

public readonly record struct WindowWorkArea(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

public interface IWindowWorkAreaProvider
{
    IReadOnlyList<WindowWorkArea> GetWorkAreas();
}

public sealed class ScreenWorkAreaProvider : IWindowWorkAreaProvider
{
    public IReadOnlyList<WindowWorkArea> GetWorkAreas()
    {
        var screens = Screen.AllScreens;
        var orderedScreens = screens
            .OrderByDescending(screen => screen.Primary)
            .ThenBy(screen => screen.Bounds.Left)
            .ThenBy(screen => screen.Bounds.Top);

        return orderedScreens
            .Select(screen => new WindowWorkArea(
                screen.WorkingArea.Left,
                screen.WorkingArea.Top,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height))
            .ToArray();
    }
}

public sealed class WindowPlacementService
{
    public const double DefaultWidth = 1200;
    public const double DefaultHeight = 800;
    public const double MinimumWindowWidth = 900;
    public const double MinimumWindowHeight = 600;
    public const double MinimumVisibleWidth = 120;
    public const double MinimumVisibleHeight = 80;
    public const double MinimumVisibleAreaRatio = 0.10;

    private const double FallbackMargin = 24;
    private const double MaximumRestorableDimension = 100_000;

    private readonly string _filePath;
    private readonly IWindowWorkAreaProvider _workAreaProvider;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public WindowPlacementService(string filePath, IWindowWorkAreaProvider workAreaProvider)
    {
        _filePath = filePath;
        _workAreaProvider = workAreaProvider;
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public WindowPlacement Restore()
    {
        return Restore(Load());
    }

    public WindowPlacement Restore(WindowPlacement? saved)
    {
        var workAreas = _workAreaProvider.GetWorkAreas();
        if (saved is not null)
        {
            var savedBounds = ToBounds(saved);
            if (IsMeaningfullyVisible(savedBounds, workAreas))
            {
                return new WindowPlacement
                {
                    Left = saved.Left,
                    Top = saved.Top,
                    Width = Math.Max(saved.Width, MinimumWindowWidth),
                    Height = Math.Max(saved.Height, MinimumWindowHeight),
                    State = saved.State == WindowPlacementState.Maximized
                        ? WindowPlacementState.Maximized
                        : WindowPlacementState.Normal
                };
            }
        }

        return CreateFallback(workAreas.FirstOrDefault());
    }

    public WindowPlacement? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(_filePath), _jsonOptions);
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(WindowPlacement placement)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(placement, _jsonOptions));
        }
        catch (IOException)
        {
            // Window placement is best-effort and must never prevent shutdown.
        }
        catch (UnauthorizedAccessException)
        {
            // Window placement is best-effort and must never prevent shutdown.
        }
    }

    public bool IsMeaningfullyVisible(WindowBounds bounds)
    {
        return IsMeaningfullyVisible(bounds, _workAreaProvider.GetWorkAreas());
    }

    public static bool IsMeaningfullyVisible(
        WindowBounds bounds,
        IEnumerable<WindowWorkArea> workAreas)
    {
        if (!IsSensible(bounds))
        {
            return false;
        }

        var savedArea = bounds.Width * bounds.Height;
        return workAreas.Any(workArea =>
        {
            if (!IsSensible(workArea))
            {
                return false;
            }

            var visibleWidth = Math.Min(bounds.Right, workArea.Right) - Math.Max(bounds.Left, workArea.Left);
            var visibleHeight = Math.Min(bounds.Bottom, workArea.Bottom) - Math.Max(bounds.Top, workArea.Top);
            if (visibleWidth < MinimumVisibleWidth || visibleHeight < MinimumVisibleHeight)
            {
                return false;
            }

            return visibleWidth * visibleHeight >= savedArea * MinimumVisibleAreaRatio;
        });
    }

    private static WindowPlacement CreateFallback(WindowWorkArea workArea)
    {
        if (!IsSensible(workArea))
        {
            workArea = new WindowWorkArea(0, 0, DefaultWidth, DefaultHeight);
        }

        var availableWidth = Math.Max(1, workArea.Width - (FallbackMargin * 2));
        var availableHeight = Math.Max(1, workArea.Height - (FallbackMargin * 2));
        var width = Math.Min(DefaultWidth, availableWidth);
        var height = Math.Min(DefaultHeight, availableHeight);
        if (availableWidth >= MinimumWindowWidth)
        {
            width = Math.Max(width, MinimumWindowWidth);
        }

        if (availableHeight >= MinimumWindowHeight)
        {
            height = Math.Max(height, MinimumWindowHeight);
        }

        return new WindowPlacement
        {
            Left = workArea.Left + ((workArea.Width - width) / 2),
            Top = workArea.Top + ((workArea.Height - height) / 2),
            Width = width,
            Height = height,
            State = WindowPlacementState.Normal
        };
    }

    private static WindowBounds ToBounds(WindowPlacement placement)
    {
        return new WindowBounds(placement.Left, placement.Top, placement.Width, placement.Height);
    }

    private static bool IsSensible(WindowBounds bounds)
    {
        return IsFinite(bounds.Left)
            && IsFinite(bounds.Top)
            && IsFinite(bounds.Width)
            && IsFinite(bounds.Height)
            && bounds.Width > 0
            && bounds.Height > 0
            && bounds.Width <= MaximumRestorableDimension
            && bounds.Height <= MaximumRestorableDimension
            && IsFinite(bounds.Right)
            && IsFinite(bounds.Bottom);
    }

    private static bool IsSensible(WindowWorkArea workArea)
    {
        return IsFinite(workArea.Left)
            && IsFinite(workArea.Top)
            && IsFinite(workArea.Width)
            && IsFinite(workArea.Height)
            && workArea.Width > 0
            && workArea.Height > 0
            && IsFinite(workArea.Right)
            && IsFinite(workArea.Bottom);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
