using System;
using System.Collections.Generic;
using System.IO;
using EngineeringDiscovery.Wpf.Services;
using Xunit;

namespace EngineeringDiscovery.Wpf.Tests;

public sealed class WindowPlacementTests
{
    private static readonly WindowWorkArea PrimaryWorkArea = new(0, 0, 1920, 1080);

    [Fact]
    public void Visible_Saved_Bounds_Are_Raised_To_The_Usable_Window_Floor()
    {
        var service = CreateService(PrimaryWorkArea);
        var saved = new WindowPlacement
        {
            Left = 240,
            Top = 120,
            Width = 720,
            Height = 480,
            State = WindowPlacementState.Normal
        };

        var restored = service.Restore(saved);

        Assert.Equal(WindowPlacementService.MinimumWindowWidth, restored.Width);
        Assert.Equal(WindowPlacementService.MinimumWindowHeight, restored.Height);
    }

    [Fact]
    public void Valid_Normal_Bounds_Are_Restored_Unchanged()
    {
        var service = CreateService(PrimaryWorkArea);
        var saved = new WindowPlacement
        {
            Left = 240,
            Top = 120,
            Width = 1200,
            Height = 800,
            State = WindowPlacementState.Normal
        };

        var restored = service.Restore(saved);

        Assert.Equal(saved.Left, restored.Left);
        Assert.Equal(saved.Top, restored.Top);
        Assert.Equal(saved.Width, restored.Width);
        Assert.Equal(saved.Height, restored.Height);
        Assert.Equal(WindowPlacementState.Normal, restored.State);
    }

    [Fact]
    public void Maximized_State_Is_Restored_Using_The_Saved_Normal_Bounds()
    {
        var service = CreateService(PrimaryWorkArea);
        var saved = new WindowPlacement
        {
            Left = 240,
            Top = 120,
            Width = 1200,
            Height = 800,
            State = WindowPlacementState.Maximized
        };

        var restored = service.Restore(saved);

        Assert.Equal(saved.Left, restored.Left);
        Assert.Equal(saved.Top, restored.Top);
        Assert.Equal(saved.Width, restored.Width);
        Assert.Equal(saved.Height, restored.Height);
        Assert.Equal(WindowPlacementState.Maximized, restored.State);
    }

    [Fact]
    public void Minimized_State_Is_Never_Restored()
    {
        var service = CreateService(PrimaryWorkArea);
        var saved = new WindowPlacement
        {
            Left = 240,
            Top = 120,
            Width = 1200,
            Height = 800,
            State = WindowPlacementState.Minimized
        };

        var restored = service.Restore(saved);

        Assert.Equal(saved.Left, restored.Left);
        Assert.Equal(saved.Top, restored.Top);
        Assert.Equal(saved.Width, restored.Width);
        Assert.Equal(saved.Height, restored.Height);
        Assert.Equal(WindowPlacementState.Normal, restored.State);
    }

    [Fact]
    public void Off_Screen_Bounds_Fall_Back_To_A_Centered_Normal_Window()
    {
        var service = CreateService(PrimaryWorkArea);
        var saved = new WindowPlacement
        {
            Left = -1100,
            Top = 120,
            Width = 1200,
            Height = 800,
            State = WindowPlacementState.Maximized
        };

        var restored = service.Restore(saved);

        Assert.Equal(360, restored.Left);
        Assert.Equal(140, restored.Top);
        Assert.Equal(WindowPlacementService.DefaultWidth, restored.Width);
        Assert.Equal(WindowPlacementService.DefaultHeight, restored.Height);
        Assert.Equal(WindowPlacementState.Normal, restored.State);
    }

    [Fact]
    public void A_Small_Visible_Sliver_Is_Not_Enough_To_Restore_Bounds()
    {
        var service = CreateService(PrimaryWorkArea);
        var saved = new WindowPlacement
        {
            Left = 1880,
            Top = 1040,
            Width = 1200,
            Height = 800,
            State = WindowPlacementState.Normal
        };

        var restored = service.Restore(saved);

        Assert.Equal(360, restored.Left);
        Assert.Equal(140, restored.Top);
        Assert.Equal(WindowPlacementState.Normal, restored.State);
    }

    [Fact]
    public void Bounds_Spanning_Current_Monitors_Remain_Unchanged()
    {
        var service = CreateService(
            new WindowWorkArea(0, 0, 1920, 1080),
            new WindowWorkArea(1920, 0, 1920, 1080));
        var saved = new WindowPlacement
        {
            Left = 1800,
            Top = 100,
            Width = 2000,
            Height = 800,
            State = WindowPlacementState.Normal
        };

        var restored = service.Restore(saved);

        Assert.Equal(saved.Left, restored.Left);
        Assert.Equal(saved.Top, restored.Top);
        Assert.Equal(saved.Width, restored.Width);
        Assert.Equal(saved.Height, restored.Height);
    }

    [Fact]
    public void Saved_Placement_Round_Trips_Through_The_Wpf_Local_File()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"engineos-window-placement-{Guid.NewGuid():N}.json");
        try
        {
            var service = CreateService(new[] { PrimaryWorkArea }, filePath);
            var saved = new WindowPlacement
            {
                Left = 240,
                Top = 120,
                Width = 1200,
                Height = 800,
                State = WindowPlacementState.Maximized
            };

            service.Save(saved);
            var restored = service.Restore();

            Assert.Equal(saved.Left, restored.Left);
            Assert.Equal(saved.Top, restored.Top);
            Assert.Equal(saved.Width, restored.Width);
            Assert.Equal(saved.Height, restored.Height);
            Assert.Equal(saved.State, restored.State);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static WindowPlacementService CreateService(
        params WindowWorkArea[] workAreas)
    {
        return CreateService(workAreas, Path.Combine(Path.GetTempPath(), $"engineos-window-placement-{Guid.NewGuid():N}.json"));
    }

    private static WindowPlacementService CreateService(
        WindowWorkArea[] workAreas,
        string filePath)
    {
        return new WindowPlacementService(filePath, new FakeWorkAreaProvider(workAreas));
    }

    private sealed class FakeWorkAreaProvider : IWindowWorkAreaProvider
    {
        private readonly IReadOnlyList<WindowWorkArea> _workAreas;

        public FakeWorkAreaProvider(IReadOnlyList<WindowWorkArea> workAreas)
        {
            _workAreas = workAreas;
        }

        public IReadOnlyList<WindowWorkArea> GetWorkAreas() => _workAreas;
    }
}
