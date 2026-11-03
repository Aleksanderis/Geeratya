using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.SavedViews;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Tests.Sources;

[Trait("Os", "Independent")]
public class SavedViewReducerTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _home = new("home", "https://home.atlassian.net", "me@home.test");
    private static readonly SavedView _bugs = new("Bugs", "Jql", "type = Bug", "List");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAnOpenTab_WhenSavingTheView_ThenTheEffectCarriesSourceAndLayoutUnderTheName()
    {
        var state = Run(Start(), "jql project = A").State;

        var result = Run(state, "view save My project");

        var effect = Assert.Single(result.Effects.OfType<SaveViewEffect>());
        Assert.Equal(_work, effect.Profile);
        Assert.Equal(new SavedView("My project", "Jql", "project = A", "List"), effect.View);
        Assert.Equal("Saved view \"My project\".", result.State.Message);
    }

    [Fact]
    public void GivenNoTab_WhenSavingTheView_ThenNothingIsWrittenAndTheMessageSaysSo()
    {
        var result = Run(Start(), "view save Mine");

        Assert.Empty(result.Effects);
        Assert.Equal("No view to save: open a tab first.", result.State.Message);
    }

    [Fact]
    public void GivenNoName_WhenSaving_ThenTheUsageIsShownAndNothingIsWritten()
    {
        var state = Run(Start(), "jql a = 1").State;

        var result = Run(state, "view save");

        Assert.DoesNotContain(result.Effects, effect => effect is SaveViewEffect);
        Assert.StartsWith("Usage: view", result.State.Message);
    }

    [Fact]
    public void GivenAnExistingName_WhenSavingAgain_ThenTheLoadedCopyIsReplacedAndTheMessageSaysSo()
    {
        var state = WithViews(Run(Start(), "jql a = 1").State, SavedViewList.Empty.Saved(_bugs));

        var result = Run(state, "view save bugs");

        var views = result.State.Session.Sources.SavedViews!;
        Assert.Equal("a = 1", Assert.Single(views.Items).SourceValue);
        Assert.Equal("Replaced saved view \"bugs\".", result.State.Message);
    }

    [Fact]
    public void GivenAQuotedName_WhenRenaming_ThenBothNamesKeepTheirBlanks()
    {
        var state = WithViews(Start(), SavedViewList.Empty.Saved(_bugs with { Name = "Old name" }));

        var result = Run(state, "view rename \"Old name\" \"New name\"");

        var effect = Assert.Single(result.Effects.OfType<RenameSavedViewEffect>());
        Assert.Equal("Old name", effect.OldName);
        Assert.Equal("New name", effect.NewName);
        Assert.Equal("New name", Assert.Single(result.State.Session.Sources.SavedViews!.Items).Name);
    }

    [Fact]
    public void GivenAnotherViewsName_WhenRenaming_ThenNothingChanges()
    {
        var views = SavedViewList.Empty.Saved(_bugs).Saved(_bugs with { Name = "Other" });

        var result = Run(WithViews(Start(), views), "view rename Bugs other");

        Assert.Empty(result.Effects);
        Assert.Equal("A saved view named \"Other\" exists already.", result.State.Message);
    }

    [Fact]
    public void GivenAnUnknownName_WhenDeleting_ThenNothingIsWritten()
    {
        var result = Run(WithViews(Start(), SavedViewList.Empty.Saved(_bugs)), "view delete nope");

        Assert.Empty(result.Effects);
        Assert.Equal("No saved view named \"nope\".", result.State.Message);
    }

    [Fact]
    public void GivenAView_WhenDeleting_ThenOnlyThatViewGoesAndTheMessageSaysItIsLocal()
    {
        var views = SavedViewList.Empty.Saved(_bugs).Saved(_bugs with { Name = "Other" });

        var result = Run(WithViews(Start(), views), "view delete BUGS");

        var effect = Assert.Single(result.Effects.OfType<DeleteSavedViewEffect>());
        Assert.Equal("Bugs", effect.Name);
        Assert.Equal(["Other"], result.State.Session.Sources.SavedViews!.Items.Select(item => item.Name));
        Assert.Contains("local name only", result.State.Message);
    }

    [Fact]
    public void GivenViewsNotLoaded_WhenRenaming_ThenTheyAreRequestedAndNothingChanges()
    {
        var result = Run(Start(), "view rename a b");

        Assert.Single(result.Effects.OfType<LoadSavedViewsEffect>());
        Assert.DoesNotContain(result.Effects, effect => effect is RenameSavedViewEffect);
    }

    [Fact]
    public void GivenSavedViews_WhenTheViewCommandRuns_ThenThePickerFiltersThemAndEnterOpensTheChosenOne()
    {
        var views = SavedViewList.Empty.Saved(_bugs).Saved(new SavedView("Team", "Board", "4", "Columns"));

        var picker = Run(WithViews(Start(), views), "view").State;
        var typed = Press(Press(picker, "T").State, "e").State;
        var result = Press(typed, "Enter");

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Team"), tab.Definition);
    }

    [Fact]
    public void GivenTheViewScope_WhenListingRows_ThenOnlySavedViewsAppear()
    {
        var state = WithViews(Start(), SavedViewList.Empty.Saved(_bugs));
        var picker = new SourcePickerState(string.Empty, 0, PickerScope.Views, false, null);

        var rows = SourceChoices.For(picker, state.Session.Sources);

        Assert.Equal(SourceChoiceKind.SavedView, Assert.Single(rows).Kind);
    }

    [Fact]
    public void GivenAViewWithAnUnknownSource_WhenListingRows_ThenItIsNotOffered()
    {
        var views = new SavedViewList([_bugs with { Source = "Hologram" }]);
        var picker = new SourcePickerState(string.Empty, 0, PickerScope.Views, false, null);

        var rows = SourceChoices.For(picker, Start().Session.Sources with { SavedViews = views });

        Assert.Empty(rows);
    }

    [Fact]
    public void GivenViewsNotLoaded_WhenThePickerOpens_ThenTheyAreRequestedForTheProfile()
    {
        var result = Press(Press(Start(), "Space").State, "t");

        Assert.Equal(_work, Assert.Single(result.Effects.OfType<LoadSavedViewsEffect>()).Profile);
    }

    [Fact]
    public void GivenAReplyForAnotherProfile_WhenApplied_ThenItIsDropped()
    {
        var reply = new SavedViewsLoaded(_home, SavedViewList.Empty.Saved(_bugs), 1);

        var result = _reducer.Reduce(Start(), reply);

        Assert.Null(result.State.Session.Sources.SavedViews);
    }

    [Fact]
    public void GivenAnOlderReply_WhenApplied_ThenTheNewerCopyStays()
    {
        var newer = _reducer.Reduce(Start(), new SavedViewsLoaded(_work, SavedViewList.Empty.Saved(_bugs), 5)).State;

        var result = _reducer.Reduce(newer, new SavedViewsLoaded(_work, SavedViewList.Empty, 4));

        Assert.Single(result.State.Session.Sources.SavedViews!.Items);
    }

    [Fact]
    public void GivenAFailedWrite_WhenApplied_ThenTheStatusShowsTheMessage()
    {
        var result = _reducer.Reduce(Start(), new SavedViewsFailed("Could not write the saved views: disk full"));

        Assert.Equal("Could not write the saved views: disk full", result.State.Message);
        Assert.Null(result.State.Session.Sources.SavedViews);
    }

    [Fact]
    public void GivenTheSaveKey_WhenPressed_ThenTheCommandLineOpensWithTheSaveCommandTyped()
    {
        var chord = Press(Press(Start(), "Space").State, "v");

        var saved = Press(chord.State, "w").State;

        Assert.Equal(InputMode.Command, saved.Mode);
        Assert.Equal("view save ", saved.PaletteText);
    }

    [Fact]
    public void GivenTheOpenKey_WhenPressed_ThenThePickerOpensOnSavedViews()
    {
        var chord = Press(Press(Start(), "Space").State, "v");

        var result = Press(chord.State, "v");

        Assert.Equal(PickerScope.Views, result.State.Picker!.Scope);
    }

    private static AppState Start()
    {
        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = SiteSession.Empty(_work) };
    }

    private static AppState WithViews(AppState state, SavedViewList views)
    {
        var lists = state.Session.Sources with { SavedViews = views, Boards = [], Filters = [] };

        return state with { Session = state.Session with { Sources = lists } };
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }

    private ReduceResult Run(AppState state, string command)
    {
        var result = Press(state, ":");

        foreach (var character in command)
        {
            result = Press(result.State, character == ' ' ? "Space" : character.ToString());
        }

        return Press(result.State, "Enter");
    }
}
