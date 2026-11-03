using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.State;

[Trait("Os", "Independent")]
public class ProfileEditTests
{
    private const string Secret = "tok-s3cret-value";

    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _dev = new("dev", "https://dev.atlassian.net", "me@dev.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenTheProfileList_WhenPressingCtrlE_ThenTheFormOpensFilledWithoutAnyToken()
    {
        var list = Palette(WithProfiles(_work, _dev), "profile list").State;
        var down = Press(list, "Down").State;

        var result = Press(down, "Ctrl-e");

        var form = result.State.Form!;

        Assert.True(form.IsEdit);
        Assert.Equal("dev", form.EditingName);
        Assert.Equal("https://dev.atlassian.net", form.SiteUrl);
        Assert.Equal("me@dev.test", form.Email);
        Assert.True(form.Token.IsEmpty);
        Assert.Null(result.State.ProfileList);
        Assert.Equal(InputMode.Insert, result.State.Mode);
    }

    [Fact]
    public void GivenTheProfileList_WhenPressingCtrlA_ThenAnEmptyAddFormOpens()
    {
        var list = Palette(WithProfiles(_work), "profile list").State;

        var result = Press(list, "Ctrl-a");

        Assert.False(result.State.Form!.IsEdit);
        Assert.Equal(string.Empty, result.State.Form.Name);
        Assert.Null(result.State.ProfileList);
    }

    [Fact]
    public void GivenAnEditedUrlAndNoToken_WhenSaving_ThenTheUpdateKeepsTheStoredToken()
    {
        var form = Palette(WithProfiles(_work, _dev), "profile edit work").State;
        var state = form with { Form = form.Form! with { SiteUrl = "https://new.atlassian.net" } };

        var result = Press(state, "Ctrl-s");

        var effect = Assert.IsType<UpdateProfileEffect>(Assert.Single(result.Effects));

        Assert.Equal(_work, effect.Old);
        Assert.Equal("https://new.atlassian.net", effect.New.SiteUrl);
        Assert.Null(effect.NewToken);
        Assert.Equal(["work", "dev"], effect.Catalog.Profiles.Select(profile => profile.Name));
        Assert.Equal("work", effect.Catalog.ActiveName);
        Assert.False(effect.OldKeyShared);
        Assert.True(result.State.ProfileBusy);
        Assert.Null(result.State.Form);
    }

    [Fact]
    public void GivenATypedToken_WhenSavingAnEdit_ThenTheEffectCarriesItAndNothingPrintsIt()
    {
        var opened = Palette(WithProfiles(_work), "profile edit").State;
        var focused = opened with { Form = opened.Form! with { Field = ProfileFormField.Token } };
        var typed = Type(focused, Secret).State;

        var result = Press(typed, "Enter");

        var effect = Assert.IsType<UpdateProfileEffect>(Assert.Single(result.Effects));

        Assert.Equal(Secret, effect.NewToken!.Reveal());
        Assert.DoesNotContain(Secret, typed.ToString());
        Assert.DoesNotContain(Secret, effect.ToString());
        Assert.DoesNotContain(Secret, result.State.ToString());
    }

    [Fact]
    public void GivenARename_WhenEditing_ThenTheActiveMarkAndPositionFollow()
    {
        var opened = Palette(WithProfiles(_work, _dev), "profile edit work").State;
        var renamed = opened with { Form = opened.Form! with { Name = "office" } };

        var effect = Assert.IsType<UpdateProfileEffect>(Assert.Single(Press(renamed, "Ctrl-s").Effects));

        Assert.Equal(["office", "dev"], effect.Catalog.Profiles.Select(profile => profile.Name));
        Assert.Equal("office", effect.Catalog.ActiveName);
    }

    [Fact]
    public void GivenTheNameOfAnotherProfile_WhenSavingAnEdit_ThenItIsRefusedOnTheNameField()
    {
        var opened = Palette(WithProfiles(_work, _dev), "profile edit work").State;
        var state = opened with { Form = opened.Form! with { Name = "dev", Field = ProfileFormField.Token } };

        var result = Press(state, "Ctrl-s");

        Assert.Empty(result.Effects);
        Assert.Equal(ProfileFormField.Name, result.State.Form!.Field);
        Assert.Contains("already exists", result.State.Form.Error);
    }

    [Fact]
    public void GivenTheOwnName_WhenSavingAnEdit_ThenItIsNotADuplicate()
    {
        var opened = Palette(WithProfiles(_work), "profile edit work").State;

        var result = Press(opened, "Ctrl-s");

        Assert.IsType<UpdateProfileEffect>(Assert.Single(result.Effects));
    }

    [Theory]
    [InlineData("ftp://x.test")]
    [InlineData("")]
    public void GivenABadUrl_WhenSavingAnEdit_ThenTheSiteFieldIsFocusedWithAnError(string url)
    {
        var opened = Palette(WithProfiles(_work), "profile edit work").State;
        var state = opened with { Form = opened.Form! with { SiteUrl = url, Field = ProfileFormField.Token } };

        var result = Press(state, "Ctrl-s");

        Assert.Empty(result.Effects);
        Assert.Equal(ProfileFormField.SiteUrl, result.State.Form!.Field);
        Assert.NotNull(result.State.Form.Error);
    }

    [Fact]
    public void GivenTheAddForm_WhenSavingWithAnEmptyTokenFromAnyField_ThenTheTokenFieldShowsTheError()
    {
        var state = Palette(AppState.Initial, "profile add").State;
        var filled = state with
        {
            Form = state.Form! with { Name = "work", SiteUrl = "https://w.atlassian.net", Email = "me@w.test" },
        };

        var result = Press(filled, "Ctrl-s");

        Assert.Empty(result.Effects);
        Assert.Equal(ProfileFormField.Token, result.State.Form!.Field);
        Assert.Contains("token", result.State.Form.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GivenTheSiteField_WhenTypingABadAddressThenAGoodOne_ThenTheNoteAppearsAndGoesAsYouType()
    {
        var state = Palette(AppState.Initial, "profile add").State;
        var site = state with { Form = state.Form! with { Field = ProfileFormField.SiteUrl } };

        var bad = Type(site, "http://x").State;
        var good = Type(site, "x.atlassian.net").State;

        Assert.NotNull(bad.Form!.Error);
        Assert.Null(good.Form!.Error);
    }

    [Fact]
    public void GivenTheForm_WhenPressingTabAndShiftTab_ThenTheFocusWrapsOverTheFourFields()
    {
        var state = Palette(AppState.Initial, "profile add").State;

        var forward = Press(state, "Tab").State;
        var back = Press(state, "Shift-Tab").State;

        Assert.Equal(ProfileFormField.SiteUrl, forward.Form!.Field);
        Assert.Equal(ProfileFormField.Token, back.Form!.Field);
    }

    [Fact]
    public void GivenAnEditWithoutAToken_WhenTesting_ThenTheEffectReadsTheStoredTokenOfTheOldKey()
    {
        var opened = Palette(WithProfiles(_work, _dev), "profile edit work").State;
        var changed = opened with { Form = opened.Form! with { SiteUrl = "https://other.atlassian.net" } };

        var result = Press(changed, "Alt-t");

        var effect = Assert.IsType<TestProfileEffect>(Assert.Single(result.Effects));

        Assert.Null(effect.Token);
        Assert.Equal(_work.CredentialKey, effect.StoredKey);
        Assert.Equal("https://other.atlassian.net", effect.Profile.SiteUrl);
        Assert.False(effect.AllowEnvironment);
        Assert.True(result.State.Form!.Testing);
    }

    [Fact]
    public void GivenTheAddFormWithoutAToken_WhenTesting_ThenNothingRunsAndTheTokenFieldComplains()
    {
        var state = Palette(AppState.Initial, "profile add").State;
        var filled = state with
        {
            Form = state.Form! with { Name = "w", SiteUrl = "https://w.atlassian.net", Email = "me@w.test" },
        };

        var result = Press(filled, "Alt-t");

        Assert.Empty(result.Effects);
        Assert.Equal(ProfileFormField.Token, result.State.Form!.Field);
    }

    [Fact]
    public void GivenARunningTest_WhenTheAnswerArrives_ThenTheFormShowsItAndAnEditClearsIt()
    {
        var opened = Palette(WithProfiles(_work), "profile edit work").State;
        var testing = Press(opened, "Alt-t").State;
        var tested = new ProfileTested(false, "Failed: Token or email rejected (401).");

        var answered = _reducer.Reduce(testing, tested).State;

        Assert.False(answered.Form!.Testing);
        Assert.True(answered.Form.TestFailed);
        Assert.Contains("401", answered.Form.TestResult);

        var edited = Type(answered, "x").State;

        Assert.Null(edited.Form!.TestResult);
    }

    [Fact]
    public void GivenAClosedForm_WhenATestAnswerArrives_ThenItIsDropped()
    {
        var result = _reducer.Reduce(WithProfiles(_work), new ProfileTested(true, "Connected as Me."));

        Assert.Null(result.State.Form);
    }

    [Fact]
    public void GivenTheActiveProfileEdited_WhenTheSaveIsReported_ThenTheSessionIsReplacedAndTheCacheDropped()
    {
        var state = WithProfiles(_work, _dev);
        var edited = new Profile("work", "https://new.atlassian.net", "me@work.test");
        var catalog = state.Profiles.Replace("work", edited);

        var result = _reducer.Reduce(state, new ProfilesChanged(catalog, "Updated."));

        Assert.Equal(edited, result.State.Profiles.Active);
        Assert.Contains(result.Effects, effect => effect is DropSiteCacheEffect drop && drop.Profile == _work);
        Assert.Contains(result.Effects, effect => effect is CheckConnectionEffect check && check.Profile == edited);
        Assert.Equal("work", result.State.Session.ProfileName);
    }

    [Fact]
    public void GivenAReplacedTokenOfTheActiveProfile_WhenTheSaveIsReported_ThenItReconnects()
    {
        var state = WithProfiles(_work);

        var result = _reducer.Reduce(state, new ProfilesChanged(state.Profiles, "Updated.", Reconnect: true));

        Assert.Contains(result.Effects, effect => effect is DropSiteCacheEffect);
        Assert.Contains(result.Effects, effect => effect is CheckConnectionEffect);
    }

    [Fact]
    public void GivenAnUnchangedActiveProfile_WhenTheSaveIsReported_ThenTheConnectionIsLeftAlone()
    {
        var state = WithProfiles(_work);

        var result = _reducer.Reduce(state, new ProfilesChanged(state.Profiles, "Updated."));

        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenAProfileBusy_WhenOpeningTheEditForm_ThenItIsRefused()
    {
        var state = WithProfiles(_work) with { ProfileBusy = true };

        var result = Palette(state, "profile edit work");

        Assert.Null(result.State.Form);
    }

    [Fact]
    public void GivenAnUnknownName_WhenEditing_ThenTheMessageNamesTheProfiles()
    {
        var result = Palette(WithProfiles(_work), "profile edit nope");

        Assert.Null(result.State.Form);
        Assert.Contains("No profile named nope", result.State.Message);
    }

    [Fact]
    public void GivenTheMouseButtons_WhenClicked_ThenTheyRunTheSameHandlersAsTheKeys()
    {
        var list = Palette(WithProfiles(_work), "profile list").State;

        var add = _reducer.Reduce(list, Click(MouseTarget.ProfileAddButton, ActionIds.ProfileAdd)).State;
        var edit = _reducer.Reduce(list, Click(MouseTarget.ProfileEditButton, ActionIds.ProfileEdit)).State;

        Assert.False(add.Form!.IsEdit);
        Assert.Equal("work", edit.Form!.EditingName);

        var field = _reducer.Reduce(edit, new OverlayRowClicked(MouseTarget.FormField, 3, ActionIds.FormFieldNext));
        var save = _reducer.Reduce(edit, Click(MouseTarget.ProfileSaveButton, ActionIds.FormSubmit));
        var cancel = _reducer.Reduce(edit, Click(MouseTarget.ProfileCancelButton, ActionIds.Cancel));

        Assert.Equal(ProfileFormField.Token, field.State.Form!.Field);
        Assert.IsType<UpdateProfileEffect>(Assert.Single(save.Effects));
        Assert.Null(cancel.State.Form);
    }

    [Fact]
    public void GivenTheCatalog_WhenReplacingTheActiveProfile_ThenTheActiveMarkAndOrderAreKept()
    {
        var catalog = ProfileCatalog.Empty.With(_work).With(_dev).Activate("dev");

        var replaced = catalog.Replace("dev", new Profile("dev2", _dev.SiteUrl, _dev.Email));

        Assert.Equal("dev2", replaced.ActiveName);
        Assert.Equal(["work", "dev2"], replaced.Profiles.Select(profile => profile.Name));
    }

    private static OverlayRowClicked Click(MouseTarget target, string action)
    {
        return new OverlayRowClicked(target, 0, action);
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
}
