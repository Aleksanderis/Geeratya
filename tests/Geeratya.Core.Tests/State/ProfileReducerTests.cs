using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.Startup;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Tests.State;

[Trait("Os", "Independent")]
public class ProfileReducerTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _dev = new("dev", "https://dev.atlassian.net", "me@dev.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenPaletteProfileAdd_WhenSubmitting_ThenInsertModeOpensAtTheNameField()
    {
        var result = Palette(AppState.Initial, "profile add");

        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.Equal(ProfileFormField.Name, result.State.Form!.Field);
    }

    [Fact]
    public void GivenTheForm_WhenFillingAllFields_ThenAddEffectCarriesProfileTokenAndCatalog()
    {
        var state = Palette(AppState.Initial, "profile add").State;

        state = Fill(state, "work").State;
        state = Fill(state, "work.atlassian.net/").State;
        state = Fill(state, "me@work.test").State;
        var result = Fill(state, "s3cret:Token q");

        var effect = Assert.IsType<AddProfileEffect>(Assert.Single(result.Effects));

        Assert.Equal(_work, effect.Profile);
        Assert.Equal("s3cret:Token q", effect.Token.Reveal());
        Assert.Equal("work", effect.Catalog.ActiveName);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Null(result.State.Form);
    }

    [Fact]
    public void GivenTokenTyped_WhenPrintingTheStateAndEffect_ThenTheTokenNeverAppears()
    {
        var state = Type(FillToToken(), "topsecret").State;

        var result = Press(state, "Enter");

        Assert.DoesNotContain("topsecret", state.ToString());
        Assert.DoesNotContain("topsecret", result.Effects[0].ToString());
        Assert.DoesNotContain("topsecret", result.State.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenEmptyName_WhenSubmitting_ThenStaysOnTheFieldWithAnError(string name)
    {
        var state = Palette(AppState.Initial, "profile add").State;

        var result = Fill(state, name);

        Assert.Equal(ProfileFormField.Name, result.State.Form!.Field);
        Assert.NotNull(result.State.Form.Error);
    }

    [Fact]
    public void GivenExistingName_WhenSubmittingTheName_ThenRefused()
    {
        var state = Palette(WithProfiles(_work), "profile add").State;

        var result = Fill(state, "work");

        Assert.Contains("already exists", result.State.Form!.Error);
    }

    [Theory]
    [InlineData("http://insecure.test")]
    [InlineData("not a url")]
    [InlineData("")]
    public void GivenBadSite_WhenSubmitting_ThenRefused(string site)
    {
        var state = Fill(Palette(AppState.Initial, "profile add").State, "work").State;

        var result = Fill(state, site);

        Assert.Equal(ProfileFormField.SiteUrl, result.State.Form!.Field);
        Assert.NotNull(result.State.Form.Error);
    }

    [Fact]
    public void GivenBadEmailOrEmptyToken_WhenSubmitting_ThenRefused()
    {
        var state = Fill(Palette(AppState.Initial, "profile add").State, "work").State;

        state = Fill(state, "work.atlassian.net").State;

        var badEmail = Fill(state, "nobody");
        var emptyToken = Press(Fill(state, "me@work.test").State, "Enter");

        Assert.Equal(ProfileFormField.Email, badEmail.State.Form!.Field);
        Assert.Equal(ProfileFormField.Token, emptyToken.State.Form!.Field);
        Assert.NotNull(emptyToken.State.Form.Error);
        Assert.Empty(emptyToken.Effects);
    }

    [Fact]
    public void GivenInsertMode_WhenTypingQAndColon_ThenTheyAreText()
    {
        var state = Palette(AppState.Initial, "profile add").State;

        state = Type(state, "q:").State;

        Assert.Equal("q:", state.Form!.Name);
        Assert.Equal(InputMode.Insert, state.Mode);
    }

    [Fact]
    public void GivenInsertMode_WhenBackspacingAndEscaping_ThenTextShrinksAndFormIsDropped()
    {
        var state = Type(Palette(AppState.Initial, "profile add").State, "ab").State;

        state = Press(state, "Backspace").State;

        Assert.Equal("a", state.Form!.Name);

        var cancelled = Press(state, "Esc");

        Assert.Equal(InputMode.Normal, cancelled.State.Mode);
        Assert.Null(cancelled.State.Form);
        Assert.Empty(cancelled.Effects);
    }

    [Fact]
    public void GivenInsertMode_WhenPressingCtrlC_ThenFormCancelsAndAppKeepsRunning()
    {
        var result = Press(Palette(AppState.Initial, "profile add").State, "Ctrl-c");

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenTwoProfiles_WhenSwitching_ThenSaveEffectActivatesTheNamedOne()
    {
        var result = Palette(WithProfiles(_work, _dev), "profile switch dev");

        var effect = Assert.IsType<SaveProfilesEffect>(Assert.Single(result.Effects));

        Assert.Equal("dev", effect.Catalog.ActiveName);
        Assert.Equal("work", result.State.Profiles.ActiveName);
    }

    [Fact]
    public void GivenSwitchToUnknownOrActive_WhenSubmitting_ThenNoEffectAndAMessage()
    {
        var state = WithProfiles(_work, _dev);

        var unknown = Palette(state, "profile switch nope");
        var active = Palette(state, "profile switch work");

        Assert.Empty(unknown.Effects);
        Assert.Contains("No profile named nope", unknown.State.Message);
        Assert.Empty(active.Effects);
        Assert.Contains("already", active.State.Message);
    }

    [Fact]
    public void GivenProfile_WhenAskingToRemove_ThenConfirmNamesWhatWillChange()
    {
        var result = Palette(WithProfiles(_work, _dev), "profile remove dev");

        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.Empty(result.Effects);
        Assert.Contains("dev", result.State.Confirm!.Message);
        Assert.Contains("https://dev.atlassian.net", result.State.Confirm.Message);
        Assert.Contains("token", result.State.Confirm.Message);
        Assert.Contains("cannot be undone", result.State.Confirm.Message);
    }

    [Fact]
    public void GivenRemoveConfirmed_WhenPressingY_ThenRemoveEffectDeletesTokenAndDropsTheProfile()
    {
        var asked = Palette(WithProfiles(_work, _dev), "profile remove dev").State;

        var result = Press(asked, "y");

        var effect = Assert.IsType<RemoveProfileEffect>(Assert.Single(result.Effects));

        Assert.Equal(_dev, effect.Profile);
        Assert.True(effect.DeleteToken);
        Assert.DoesNotContain(effect.Catalog.Profiles, profile => profile.Name == "dev");
    }

    [Fact]
    public void GivenRemoveAsked_WhenDeclining_ThenNothingHappens()
    {
        var asked = Palette(WithProfiles(_work, _dev), "profile remove dev").State;

        var result = Press(asked, "n");

        Assert.Empty(result.Effects);
        Assert.Equal(2, result.State.Profiles.Profiles.Count);
    }

    [Fact]
    public void GivenTwoProfilesSharingSiteAndAccount_WhenRemovingOne_ThenTokenIsKept()
    {
        var twin = _work with { Name = "twin" };
        var asked = Palette(WithProfiles(_work, twin), "profile remove twin").State;

        var effect = Assert.IsType<RemoveProfileEffect>(Assert.Single(Press(asked, "y").Effects));

        Assert.False(effect.DeleteToken);
    }

    [Fact]
    public void GivenUnknownProfileToRemove_WhenSubmitting_ThenNoConfirmOpens()
    {
        var result = Palette(WithProfiles(_work), "profile remove nope");

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Contains("No profile named nope", result.State.Message);
    }

    [Fact]
    public void GivenProfilesAndNone_WhenListing_ThenTheListOpensOnTheActiveOneOrTheAddFormOpens()
    {
        var both = WithProfiles(_work, _dev);
        var some = Palette(both with { Profiles = both.Profiles.Activate("dev") }, "profile");
        var none = Palette(AppState.Initial, "profile list");

        Assert.Equal(InputMode.Insert, some.State.Mode);
        Assert.Equal(1, some.State.ProfileList!.Selected);
        Assert.NotNull(none.State.ProfileList);
        Assert.Null(none.State.Form);
        Assert.Empty(none.Effects);
        Assert.IsType<LoadTokenSourcesEffect>(Assert.Single(some.Effects));
    }

    [Fact]
    public void GivenBareProfileCommand_WhenSubmittingFromTheCommandLine_ThenTheManagerOpensAtOnce()
    {
        var typed = Type(WithProfiles(_work, _dev) with { Mode = InputMode.Command }, "profile").State;

        var result = Press(typed, "Enter");

        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.NotNull(result.State.ProfileList);
        Assert.Null(result.State.Form);
    }

    [Fact]
    public void GivenNoProfile_WhenStarting_ThenTheManagerIsTheFirstScreenAndAProfileKeepsTheBoardStart()
    {
        var first = ProfileReducer.FirstRun(AppState.Initial with { Message = "A notice." });
        var withProfile = ProfileReducer.FirstRun(WithProfiles(_work));

        Assert.NotNull(first.ProfileList);
        Assert.Equal(InputMode.Insert, first.Mode);
        Assert.Equal("A notice.", first.Message);
        Assert.Null(withProfile.ProfileList);
        Assert.Equal(InputMode.Normal, withProfile.Mode);
    }

    [Fact]
    public void GivenTheManager_WhenAddingThenCancelling_ThenTheManagerIsStillOpen()
    {
        var opened = Palette(WithProfiles(_work, _dev), "profile").State;

        var form = Press(opened, "Ctrl-a").State;
        var up = Press(form, "Up").State;
        var back = Press(form, "Esc").State;

        Assert.NotNull(form.Form);
        Assert.NotNull(form.ProfileList);
        Assert.Equal(0, up.ProfileList!.Selected);
        Assert.Null(back.Form);
        Assert.NotNull(back.ProfileList);
        Assert.Equal(InputMode.Insert, back.Mode);
    }

    [Fact]
    public void GivenTheManager_WhenDecliningARemoval_ThenTheManagerIsStillOpen()
    {
        var opened = Palette(WithProfiles(_work, _dev), "profile").State;

        var asked = Press(opened, "Ctrl-x").State;
        var declined = Press(asked, "n").State;

        Assert.Equal(InputMode.Confirm, asked.Mode);
        Assert.NotNull(asked.ProfileList);
        Assert.Equal(InputMode.Insert, declined.Mode);
        Assert.NotNull(declined.ProfileList);
    }

    [Fact]
    public void GivenTheManager_WhenPressingEnterOnTheActiveProfile_ThenItClosesWithoutSwitching()
    {
        var opened = Palette(WithProfiles(_work, _dev), "profile").State;

        var result = Press(opened, "Enter");

        Assert.Null(result.State.ProfileList);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenTheManager_WhenProfilesChange_ThenTheTokenSourcesAreLookedUpAgain()
    {
        var opened = Palette(WithProfiles(_work), "profile").State;
        var catalog = opened.Profiles.With(_dev);

        var result = _reducer.Reduce(opened, new ProfilesChanged(catalog, "Added."));
        var loaded = _reducer.Reduce(
            result.State,
            new TokenSourcesLoaded(new Dictionary<string, TokenSourceKind> { [_dev.CredentialKey] = TokenSourceKind.None }));

        var lookup = result.Effects.OfType<LoadTokenSourcesEffect>().Single();

        Assert.Equal(2, lookup.Profiles.Count);
        Assert.NotNull(result.State.ProfileList);
        Assert.Equal(TokenSourceKind.None, loaded.State.TokenSources[_dev.CredentialKey]);
    }

    [Fact]
    public void GivenTheOpenList_WhenMovingAndPressingEnter_ThenTheHighlightedProfileIsSwitchedTo()
    {
        var opened = Palette(WithProfiles(_work, _dev), "profile list").State;

        var moved = Press(opened, "Down").State;
        var again = Press(moved, "Down").State;
        var result = Press(moved, "Enter");
        var effect = Assert.IsType<SaveProfilesEffect>(Assert.Single(result.Effects));

        Assert.Equal(1, moved.ProfileList!.Selected);
        Assert.Equal(1, again.ProfileList!.Selected);
        Assert.Equal("dev", effect.Catalog.ActiveName);
        Assert.Null(result.State.ProfileList);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenTheOpenList_WhenPressingCtrlX_ThenTheHighlightedProfileIsAskedToBeRemovedAndNothingRuns()
    {
        var opened = Palette(WithProfiles(_work, _dev), "profile list").State;

        var result = Press(Press(opened, "Down").State, "Ctrl-x");

        Assert.Empty(result.Effects);
        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.Equal("dev", result.State.Confirm!.Target);
        Assert.Contains("cannot be undone", result.State.Confirm.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOpenList_WhenPressingEsc_ThenItClosesWithoutChangingProfiles()
    {
        var opened = Palette(WithProfiles(_work, _dev), "profile list").State;

        var result = Press(opened, "Esc");

        Assert.Null(result.State.ProfileList);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenSavePending_WhenEnterOrCtrlXOrAClickInTheOpenList_ThenItIsRefusedAndNothingRuns()
    {
        var opened = Palette(WithProfiles(_work, _dev), "profile list").State with { ProfileBusy = true };
        var click = new OverlayRowClicked(MouseTarget.ProfileRow, 1, ActionIds.FormNext);
        var clickRemove = new OverlayRowClicked(MouseTarget.ProfileRow, 1, ActionIds.ProfileRemove);

        ReduceResult[] results =
        [
            Press(opened, "Enter"),
            Press(opened, "Ctrl-x"),
            _reducer.Reduce(opened, click),
            _reducer.Reduce(opened, clickRemove),
        ];

        Assert.All(results, result => Assert.Empty(result.Effects));
        Assert.All(results, result => Assert.Contains("Still saving", result.State.Message));
        Assert.All(results, result => Assert.NotEqual(InputMode.Confirm, result.State.Mode));
    }

    [Fact]
    public void GivenTheOpenList_WhenAClickNamesARowThatIsGone_ThenItIsClampedToTheLastProfile()
    {
        var opened = Palette(WithProfiles(_work, _dev), "profile list").State;
        var click = new OverlayRowClicked(MouseTarget.ProfileRow, 7, ActionIds.FormNext);

        var result = _reducer.Reduce(opened, click);

        Assert.Equal("dev", Assert.IsType<SaveProfilesEffect>(Assert.Single(result.Effects)).Catalog.ActiveName);
    }

    [Fact]
    public void GivenHostAnswers_WhenReducing_ThenCatalogAndMessageUpdateOrStay()
    {
        var catalog = ProfileCatalog.Empty.With(_work);

        var changed = _reducer.Reduce(AppState.Initial, new ProfilesChanged(catalog, "Done.")).State;
        var failed = _reducer.Reduce(changed, new ProfileOperationFailed("Nope.")).State;

        Assert.Equal("work", changed.Profiles.ActiveName);
        Assert.Equal("Done.", changed.Message);
        Assert.Equal("Nope.", failed.Message);
        Assert.Equal("work", failed.Profiles.ActiveName);
    }

    [Fact]
    public void GivenUnknownProfileVerb_WhenSubmitting_ThenUsageIsShown()
    {
        Assert.Contains("Usage", Palette(AppState.Initial, "profile frob").State.Message);
    }

    [Fact]
    public void GivenSavePending_WhenIssuingAnotherProfileCommand_ThenItIsRefusedUntilTheHostAnswers()
    {
        var state = WithProfiles(_work, _dev);
        var switching = Palette(state, "profile switch dev").State;

        var refusedSwitch = Palette(switching, "profile switch work");
        var refusedAdd = Palette(switching, "profile add");
        var refusedRemove = Palette(switching, "profile remove work");
        var listing = Palette(switching, "profile list");
        var done = _reducer.Reduce(switching, new ProfilesChanged(state.Profiles.Activate("dev"), "Done.")).State;

        Assert.True(switching.ProfileBusy);
        Assert.Empty(refusedSwitch.Effects);
        Assert.Contains("Still saving", refusedSwitch.State.Message);
        Assert.Equal(InputMode.Normal, refusedAdd.State.Mode);
        Assert.Equal(InputMode.Normal, refusedRemove.State.Mode);
        Assert.Equal(InputMode.Insert, listing.State.Mode);
        Assert.NotNull(listing.State.ProfileList);
        Assert.False(done.ProfileBusy);
        Assert.Single(Palette(done, "profile switch work").Effects);
    }

    [Fact]
    public void GivenSavePending_WhenConfirmingARemoval_ThenNothingRuns()
    {
        var asked = Palette(WithProfiles(_work, _dev), "profile remove dev").State with { ProfileBusy = true };

        var result = Press(asked, "y");

        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenActiveProfile_WhenAskingToRemoveIt_ThenConfirmNamesTheNewActiveProfile()
    {
        var result = Palette(WithProfiles(_work, _dev), "profile remove work");

        Assert.Contains("dev becomes the active profile", result.State.Confirm!.Message);
        Assert.True(Assert.IsType<RemoveProfileEffect>(Assert.Single(Press(result.State, "y").Effects)).WasActive);
    }

    [Fact]
    public void GivenOnlyProfile_WhenAskingToRemoveIt_ThenConfirmSaysNoneWillBeActive()
    {
        var result = Palette(WithProfiles(_work), "profile remove work");

        Assert.Contains("No profile will be active", result.State.Confirm!.Message);
    }

    [Theory]
    [InlineData("profile switch")]
    [InlineData("profile remove  ")]
    public void GivenMissingName_WhenSwitchingOrRemoving_ThenUsageIsShown(string command)
    {
        var result = Palette(WithProfiles(_work), command);

        Assert.Contains("Usage", result.State.Message);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenSiteWithUserInfo_WhenSubmitting_ThenRefused()
    {
        var state = Fill(Palette(AppState.Initial, "profile add").State, "work").State;

        var result = Fill(state, "https://user:secret@x.atlassian.net");

        Assert.Equal(ProfileFormField.SiteUrl, result.State.Form!.Field);
        Assert.NotNull(result.State.Form.Error);
    }

    [Fact]
    public void GivenTabsOfWork_WhenSwitchingToDev_ThenTabsAreGoneAndOldCacheIsDropped()
    {
        var state = WithOpenSession();
        var switching = Palette(state, "profile switch dev").State;

        var result = _reducer.Reduce(switching, new ProfilesChanged(state.Profiles.Activate("dev"), "Done."));

        Assert.Empty(result.State.Session.Tabs);
        Assert.Equal("dev", result.State.Session.ProfileName);
        Assert.Equal(_dev.SiteUrl, result.State.Session.SiteUrl);
        Assert.Equal(_work, Assert.Single(result.Effects.OfType<DropSiteCacheEffect>()).Profile);
    }

    [Fact]
    public void GivenFailureOfOldProfile_WhenReducing_ThenItIsIgnoredAndActiveProfileFailureIsShown()
    {
        var state = WithProfiles(_work, _dev);
        var active = state.Profiles.Active!;
        var other = state.Profiles.Profiles.First(profile => profile != active);

        var stale = _reducer.Reduce(state, new ConnectionFailed(other, "Old failure.")).State;
        var current = _reducer.Reduce(state, new ConnectionFailed(active, "Current failure.")).State;

        Assert.Null(stale.Message);
        Assert.Equal("Current failure.", current.Message);
    }

    [Fact]
    public void GivenTabs_WhenTheActiveProfileStays_ThenTabsAreKept()
    {
        var state = WithOpenSession();

        var result = _reducer.Reduce(state, new ProfilesChanged(state.Profiles.With(_dev), "Added."));

        Assert.Single(result.State.Session.Tabs);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenTabs_WhenTheActiveProfileIsRemoved_ThenTheSessionIsReplacedByTheNextProfile()
    {
        var state = WithOpenSession();

        var result = _reducer.Reduce(state, new ProfilesChanged(state.Profiles.Without("work"), "Removed."));

        Assert.Empty(result.State.Session.Tabs);
        Assert.Equal("dev", result.State.Session.ProfileName);
        Assert.Single(result.Effects.OfType<DropSiteCacheEffect>());
    }

    [Fact]
    public void GivenFirstProfileAdded_WhenNoSessionExisted_ThenTheSessionStartsEmptyWithoutDropping()
    {
        var result = _reducer.Reduce(AppState.Initial, new ProfilesChanged(ProfileCatalog.Empty.With(_work), "Added."));

        Assert.Equal("work", result.State.Session.ProfileName);
        Assert.Empty(result.Effects.OfType<DropSiteCacheEffect>());
    }

    [Fact]
    public void GivenStartupStateWithoutSession_WhenSwitchingOnce_ThenTheOldProfileCacheIsDropped()
    {
        var state = WithProfiles(_work, _dev);

        var result = _reducer.Reduce(state, new ProfilesChanged(state.Profiles.Activate("dev"), "Done."));

        Assert.Equal(_work, Assert.Single(result.Effects.OfType<DropSiteCacheEffect>()).Profile);
    }

    [Fact]
    public void GivenTwoProfilesOnOneSite_WhenSwitchingBetweenThem_ThenTheSessionIsStillReplaced()
    {
        var sibling = new Profile("work2", _work.SiteUrl, "other@work.test");
        var state = WithProfiles(_work, sibling);

        var result = _reducer.Reduce(state, new ProfilesChanged(state.Profiles.Activate("work2"), "Done."));

        Assert.Equal("work2", result.State.Session.ProfileName);
        Assert.Single(result.Effects.OfType<DropSiteCacheEffect>());
    }

    private static AppState WithOpenSession()
    {
        var state = WithProfiles(_work, _dev);
        var reducer = new AppReducer(DefaultKeymap.Load());
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = X", ViewLayout.List, "X");
        var session = state with { Session = SiteSession.Empty(_work) };

        return reducer.Reduce(session, new ViewOpenRequested(definition)).State;
    }

    [Fact]
    public void GivenAConnectedStartWithNoTab_WhenClosingTheManager_ThenTheBoardPickerOpensOnce()
    {
        var state = WithProfiles(_work);
        var started = state with
        {
            Session = SiteSession.Empty(_work) with { Startup = StartupPhase.Done, Connection = new Connected("Me") },
        };
        var opened = Palette(started, "profile").State;

        var closed = Press(opened, "Esc");
        var again = Press(closed.State, "Esc");

        Assert.NotNull(closed.State.Picker);
        Assert.Null(closed.State.ProfileList);
        Assert.Equal(InputMode.Insert, closed.State.Mode);
        Assert.Null(again.State.Picker);
    }

    [Fact]
    public void GivenAnUnfinishedStartOrASwitch_WhenClosingTheManager_ThenNoPickerOpens()
    {
        var checking = WithProfiles(_work) with { Session = SiteSession.Empty(_work) };
        var connected = checking with
        {
            Session = checking.Session with { Startup = StartupPhase.Done, Connection = new Connected("Me") },
        };
        var other = WithProfiles(_work, _dev) with { Session = connected.Session };
        var switching = Press(Palette(other, "profile").State, "Down");

        var early = Press(Palette(checking, "profile").State, "Esc");
        var switched = Press(switching.State, "Enter");

        Assert.Null(early.State.Picker);
        Assert.Null(switched.State.Picker);
        Assert.Single(switched.Effects);
    }

    private static AppState WithProfiles(params Profile[] profiles)
    {
        var catalog = profiles.Aggregate(ProfileCatalog.Empty, (current, profile) => current.With(profile));

        return AppState.Initial with { Profiles = catalog };
    }

    [Fact]
    public void GivenEveryFormStep_WhenPasting_ThenTheTextFillsTheActiveField()
    {
        var state = Palette(AppState.Initial, "profile add").State;

        state = Paste(state, "work");
        Assert.Equal("work", state.Form!.Name);
        state = Press(state, "Enter").State;

        state = Paste(state, "work.atlassian.net");
        Assert.Equal("work.atlassian.net", state.Form!.SiteUrl);
        state = Press(state, "Enter").State;

        state = Paste(state, "me@work.test");
        Assert.Equal("me@work.test", state.Form!.Email);
        state = Press(state, "Enter").State;

        state = Paste(state, "tok:en q");
        Assert.Equal("tok:en q", state.Form!.Token.Reveal());
    }

    [Fact]
    public void GivenTokenStep_WhenPastingMultiLineText_ThenOnlyTheFirstLineIsTakenAndNothingSubmits()
    {
        var state = Paste(FillToToken(), "secret-one\nsecret-two\n");

        Assert.Equal("secret-one", state.Form!.Token.Reveal());
        Assert.Equal(ProfileFormField.Token, state.Form.Field);
        Assert.Equal(InputMode.Insert, state.Mode);
    }

    [Fact]
    public void GivenTokenStep_WhenPastingPaddedText_ThenSurroundingSpacesAreTrimmed()
    {
        var state = Paste(FillToToken(), "  tok en  ");

        Assert.Equal("tok en", state.Form!.Token.Reveal());
    }

    [Fact]
    public void GivenPastedToken_WhenPrintingTheState_ThenTheTokenNeverAppears()
    {
        var state = Paste(FillToToken(), "pastedsecret");

        Assert.DoesNotContain("pastedsecret", state.ToString());
        Assert.Equal(12, state.Form!.Token.Length);
    }

    [Fact]
    public void GivenNormalMode_WhenPasting_ThenNothingChanges()
    {
        var state = Paste(AppState.Initial, "profile add");

        Assert.Equal(AppState.Initial, state);
    }

    [Fact]
    public void GivenPaletteOpen_WhenPasting_ThenTheTextIsAddedToThePalette()
    {
        var state = Paste(Press(AppState.Initial, ":").State, "profile list\n");

        Assert.Equal("profile list", state.PaletteText);
    }

    private AppState Paste(AppState state, string text)
    {
        return _reducer.Reduce(state, new TextPasted(text)).State;
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }

    private ReduceResult Type(AppState state, string text)
    {
        var result = new ReduceResult(state, []);

        foreach (var character in text)
        {
            var key = character == ' ' ? KeyNotation.Space : character.ToString();

            result = Press(result.State, key);
        }

        return result;
    }

    private ReduceResult Palette(AppState state, string command)
    {
        var opened = Press(state, ":");
        var typed = Type(opened.State, command);

        return Press(typed.State, "Enter");
    }

    private ReduceResult Fill(AppState state, string text)
    {
        var typed = Type(state, text);

        return Press(typed.State, "Enter");
    }

    private AppState FillToToken()
    {
        var state = Palette(AppState.Initial, "profile add").State;

        state = Fill(state, "work").State;
        state = Fill(state, "work.atlassian.net").State;

        return Fill(state, "me@work.test").State;
    }
}
