using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

public class RowMenuMarkerTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Theory]
    [InlineData(120, 36, GlyphSet.Unicode)]
    [InlineData(80, 24, GlyphSet.Unicode)]
    [InlineData(40, 10, GlyphSet.Unicode)]
    [InlineData(40, 10, GlyphSet.Ascii)]
    public void GivenAList_WhenBuildingTheFrame_ThenOnlyTheSelectedRowEndsInTheMarkerAndTheFrameReportsItsCells(
        int width,
        int height,
        GlyphSet glyphs)
    {
        var frame = FrameBuilder.BuildStyled(Sample(), width, height, _keymap, null, null, glyphs)!;
        var marked = frame.Body.Where(line => line.PlainText.Contains(RowMenuMarker.Text)).ToList();
        var line = Assert.Single(marked);
        var marker = Assert.IsType<OverlayRegion>(frame.RowMenuMarker);

        Assert.Contains("GCM-14", line.PlainText);
        Assert.Equal(MouseTarget.RowMenuButton, marker.Target);
        Assert.Equal(RowMenuMarker.Text.Length, marker.Width);
        Assert.Equal(FrameBuilder.HeaderRows + frame.Body.ToList().IndexOf(line), marker.Top);
        Assert.Equal(RowMenuMarker.Text, line.PlainText.Substring(marker.Left, marker.Width));
    }

    [Fact]
    public void GivenARowNarrowerThanTheMinimum_WhenBuildingTheFrame_ThenNoMarkerIsDrawn()
    {
        var frame = FrameBuilder.BuildStyled(Sample(), 24, 10, _keymap)!;

        Assert.Null(frame.RowMenuMarker);
        Assert.DoesNotContain(frame.Body, line => line.PlainText.Contains(RowMenuMarker.Text));
    }

    [Fact]
    public void GivenAnOverlayMode_WhenBuildingTheFrame_ThenNoMarkerIsDrawn()
    {
        var palette = Sample() with { Mode = InputMode.Command };
        var help = Sample() with { Mode = InputMode.Help };

        Assert.Null(FrameBuilder.BuildStyled(palette, 80, 24, _keymap)!.RowMenuMarker);
        Assert.Null(FrameBuilder.BuildStyled(help, 80, 24, _keymap)!.RowMenuMarker);
    }

    [Fact]
    public void GivenAFocusedSprintHeader_WhenBuildingTheFrame_ThenNoMarkerIsDrawn()
    {
        var state = Sample();
        var tab = state.Session.Tabs[0] with { FocusedSprintId = 7 };
        var focused = state with { Session = state.Session with { Tabs = [tab] } };

        Assert.Null(FrameBuilder.BuildStyled(focused, 80, 24, _keymap)!.RowMenuMarker);
    }

    [Fact]
    public void GivenTheDrawnMarker_WhenClickingIt_ThenTheMenuOpensForTheSelectedRowWithoutMovingTheCursor()
    {
        var state = Sample();
        var marker = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!.RowMenuMarker!;
        var context = Context(state, marker);

        var commands = MouseResolver.Resolve(context, marker.Left + 1, marker.Top, MouseGesture.Click, default);
        var menu = new AppReducer(_keymap).Reduce(state, Assert.Single(commands)).State;

        Assert.Equal(ActionIds.ContextMenuOpen, Assert.IsType<ActionRequested>(commands[0]).Action);
        Assert.NotNull(menu.ContextMenu);
        Assert.Equal(1, menu.Session.Tabs[0].Selected);
    }

    [Fact]
    public void GivenTheDrawnMarker_WhenOtherGesturesHitIt_ThenTheyActOnTheRowAsBefore()
    {
        var state = Sample();
        var marker = FrameBuilder.BuildStyled(state, 80, 24, _keymap)!.RowMenuMarker!;
        var context = Context(state, marker);

        var right = MouseResolver.Resolve(context, marker.Left, marker.Top, MouseGesture.RightClick, default);
        var plain = MouseResolver.Resolve(context, marker.Left - 5, marker.Top, MouseGesture.Click, default);

        Assert.IsType<PointerPlaced>(Assert.Single(right));
        Assert.IsType<PointerPlaced>(Assert.Single(plain));
    }

    private static MouseContext Context(AppState state, OverlayRegion marker)
    {
        return MouseContext.From(state, 80, 24, FrameBuilder.HeaderRows, default) with { RowMenuMarker = marker };
    }

    private static AppState Sample()
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var definition = new ViewDefinition(ViewSourceKind.Jql, "assignee = currentUser()", ViewLayout.List, "Mine");
        var rows = new[]
        {
            Row("GCM-12", "Fix login redirect after token refresh"),
            Row("GCM-14", "Crash when the board has no columns"),
            Row("GCM-15", "As a user I can filter by label"),
        };
        var tab = TabState.Opened(new TabId(1), definition, 1, 1, 10) with
        {
            Rows = rows,
            Selected = 1,
            SelectedKey = "GCM-14",
            KeyWidth = 6,
        };
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static IssueSummary Row(string key, string summary)
    {
        return new IssueSummary(
            key,
            key,
            summary,
            new IssueStatus("To Do", "To Do", "new"),
            new IssueTypeSummary("Task", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "GCM");
    }
}
