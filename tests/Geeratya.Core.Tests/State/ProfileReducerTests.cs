using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;

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
    public void GivenProfilesAndNone_WhenListing_ThenMessageMarksTheActiveOne()
    {
        var some = Palette(WithProfiles(_work, _dev), "profile");
        var none = Palette(AppState.Initial, "profile list");

        Assert.Equal("Profiles: work *, dev (* active)", some.State.Message);
        Assert.Contains("profile add", none.State.Message);
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

    private static AppState WithProfiles(params Profile[] profiles)
    {
        var catalog = profiles.Aggregate(ProfileCatalog.Empty, (current, profile) => current.With(profile));

        return AppState.Initial with { Profiles = catalog };
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
