using Geeratya.Core.Comments;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Sources;
using Geeratya.Core.State;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The one hint list of every overlay and dialog (<c>docs/internal/ui-consistency.md</c>, component 6): the in-box
/// footer and the status line are both drawn from these chips, so they cannot word the same action two ways or list
/// different keys. A new surface adds one builder here and one row to <c>FooterContractTests</c>.
/// </summary>
public static class SurfaceHints
{
    /// <summary>The chips of the surface <paramref name="state"/> shows on top, or null for a plain view.</summary>
    public static SurfaceChips? Of(AppState state, KeymapTable keymap)
    {
        if (state is { SettingsScreen: not null, Mode: InputMode.Normal })
        {
            return SurfaceChips.KeepLast(Settings(keymap, state.SettingsScreen.Choice is not null));
        }

        if (state.Editor is { } editor && state.Mode == InputMode.Insert)
        {
            return SurfaceChips.KeepLast(Editor(keymap, editor));
        }

        if (FormReducer.OpenForm(state) is { } form && state.Mode != InputMode.Confirm)
        {
            var chips = FormFooter.Chips(form, keymap).OfType<HintChip>().ToList();

            return new SurfaceChips(chips, FormFooter.Kept(form, chips.Count));
        }

        if (state.FlashJump is not null)
        {
            return SurfaceChips.KeepLast(Flash(keymap));
        }

        return Of(state, keymap, state.Mode);
    }

    /// <summary>The chips of Jump.</summary>
    public static List<HintChip?> Jump(KeymapTable keymap)
    {
        return [Move(keymap, InputMode.Insert), Enter(keymap, HintLabels.Open), Close(keymap, InputMode.Insert)];
    }

    /// <summary>The chips of the source, tab and sprint pickers.</summary>
    public static List<HintChip?> Picker(KeymapTable keymap, SourcePickerState picker)
    {
        var newTab = DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.PickerOpenNewTab, HintLabels.NewTab);

        return
        [
            Move(keymap, InputMode.Insert),
            Enter(keymap, PickerEnterLabel(picker)),
            picker is { Scope: PickerScope.Sprints, Sprint.MoveKeys: null } ? newTab : null,
            Close(keymap, InputMode.Insert),
        ];
    }

    /// <summary>The chips of the Actions menu.</summary>
    public static List<HintChip?> ContextMenu(KeymapTable keymap)
    {
        return [Move(keymap, InputMode.Insert), Enter(keymap, HintLabels.Run), Close(keymap, InputMode.Insert)];
    }

    /// <summary>The chips of the command palette.</summary>
    public static List<HintChip?> Palette(KeymapTable keymap)
    {
        return
        [
            Move(keymap, InputMode.Command),
            DialogFooter.Chip(keymap, InputMode.Command, ActionIds.PaletteSubmit, HintLabels.Run),
            DialogFooter.Chip(keymap, InputMode.Command, ActionIds.Cancel, HintLabels.Close),
        ];
    }

    /// <summary>
    /// The chips of the Help (pages and filter) or, with <paramref name="withPages"/> false, the Messages.
    /// </summary>
    public static List<HintChip?> Help(KeymapTable keymap, bool withPages, bool filtering)
    {
        if (filtering)
        {
            // While the filter line is typed j, k and / are text, Enter keeps the filter and Esc clears it.
            return
            [
                DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, HintLabels.KeepFilter),
                DialogFooter.Chip(keymap, InputMode.Help, ActionIds.HelpPageNext, HintLabels.Page),
                DialogFooter.Chip(keymap, InputMode.Help, ActionIds.HelpClose, HintLabels.ClearFilter),
            ];
        }

        return
        [
            DialogFooter.Pair(keymap, InputMode.Help, ActionIds.NavigateDown, ActionIds.NavigateUp, HintLabels.Move),
            withPages ? DialogFooter.Chip(keymap, InputMode.Help, ActionIds.HelpPageNext, HintLabels.Page) : null,
            withPages ? DialogFooter.Chip(keymap, InputMode.Help, ActionIds.HelpFilter, HintLabels.Filter) : null,
            DialogFooter.Chip(keymap, InputMode.Help, ActionIds.HelpClose, HintLabels.Close),
        ];
    }

    /// <summary>The chips of the comment editor, or of its mention popup while that has rows.</summary>
    public static List<HintChip?> Editor(KeymapTable keymap, CommentEditor editor)
    {
        if (editor.Suggest is { Items.Count: > 0 })
        {
            var accept = keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormFieldNext);
            var enter = keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormNext);
            var keys = string.Join('/', new[] { accept, enter }.Where(static key => key is { Length: > 0 }));

            return
            [
                Move(keymap, InputMode.Insert),
                keys.Length == 0 ? null : new HintChip(keys, HintLabels.Choose),
                Close(keymap, InputMode.Insert),
            ];
        }

        return
        [
            DialogFooter.Chip(
                keymap,
                InputMode.Insert,
                ActionIds.FormSubmit,
                editor.IsEdit ? HintLabels.Save : HintLabels.Send),
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, HintLabels.NewLine),
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, HintLabels.Cancel),
        ];
    }

    /// <summary>The chips of the profile form on <paramref name="field"/>.</summary>
    public static List<HintChip?> ProfileForm(KeymapTable keymap, ProfileFormField field)
    {
        var last = field == ProfileFormField.Token;

        return
        [
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, last ? HintLabels.Save : HintLabels.Next),
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormFieldNext, HintLabels.Field),
            last ? null : DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormSubmit, HintLabels.Save),
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.ProfileTest, HintLabels.Test),
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, HintLabels.Cancel),
        ];
    }

    /// <summary>The chips of the profile list.</summary>
    public static List<HintChip?> ProfileList(KeymapTable keymap, bool anyProfile)
    {
        return
        [
            Move(keymap, InputMode.Insert),
            anyProfile ? DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, HintLabels.Switch) : null,
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.ProfileAdd, HintLabels.Add),
            anyProfile ? DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.ProfileEdit, HintLabels.Edit) : null,
            anyProfile ? DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.ProfileRemove, HintLabels.Remove) : null,
            Close(keymap, InputMode.Insert),
        ];
    }

    /// <summary>The chips of the Settings screen; <paramref name="listOpen"/> while a choice list is open.</summary>
    public static List<HintChip?> Settings(KeymapTable keymap, bool listOpen)
    {
        return
        [
            DialogFooter.Pair(keymap, InputMode.Normal, ActionIds.NavigateDown, ActionIds.NavigateUp, HintLabels.Move),
            DialogFooter.Chip(
                keymap,
                InputMode.Normal,
                ActionIds.ItemOpen,
                listOpen ? HintLabels.Keep : HintLabels.Change),
            listOpen ? null : DialogFooter.Chip(keymap, InputMode.Normal, ActionIds.SettingsReset, HintLabels.Reset),
            DialogFooter.Chip(keymap, InputMode.Normal, ActionIds.Cancel, HintLabels.Close),
        ];
    }

    /// <summary>The chips of the which-key popup.</summary>
    public static List<HintChip?> WhichKey(KeymapTable keymap)
    {
        return [DialogFooter.Chip(keymap, InputMode.Normal, ActionIds.Cancel, HintLabels.Cancel)];
    }

    /// <summary>The chips of the flash jump prompt.</summary>
    public static List<HintChip?> Flash(KeymapTable keymap)
    {
        return
        [
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, HintLabels.Nearest),
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, HintLabels.Cancel),
        ];
    }

    /// <summary>The chips of an information-only notice (the sprint goal): the answer key closes it.</summary>
    public static List<HintChip?> Notice(KeymapTable keymap)
    {
        var key = keymap.FindKeyLabel(InputMode.Confirm, ActionIds.ConfirmYes);

        return [key is { Length: > 0 } ? new HintChip(key, HintLabels.Close) : null];
    }

    private static SurfaceChips? Of(AppState state, KeymapTable keymap, InputMode mode)
    {
        if (state.Picker is { } picker)
        {
            return SurfaceChips.KeepLast(Picker(keymap, picker));
        }

        if (state.Jump is not null)
        {
            return SurfaceChips.KeepLast(Jump(keymap));
        }

        if (state.ContextMenu is not null)
        {
            return SurfaceChips.KeepLast(ContextMenu(keymap));
        }

        return mode switch
        {
            InputMode.Command => SurfaceChips.KeepLast(Palette(keymap)),
            InputMode.Help => SurfaceChips.KeepLast(Help(keymap, !state.MessagesOpen, state.HelpFiltering)),
            InputMode.Insert when state is { Form: { } profile }
                => SurfaceChips.KeepLast(ProfileForm(keymap, profile.Field)),
            InputMode.Insert when state.ProfileList is not null => SurfaceChips.KeepLast(ProfileList(
                keymap,
                state.Profiles.Profiles.Count > 0)),
            InputMode.Confirm when state.Confirm?.Title is not null => SurfaceChips.KeepLast(Notice(keymap)),
            InputMode.Normal when WhichKeyOverlay.Applies(state) => SurfaceChips.KeepLast(WhichKey(keymap)),
            _ => null,
        };
    }

    private static HintChip? Move(KeymapTable keymap, InputMode mode)
    {
        return DialogFooter.Pair(keymap, mode, ActionIds.PickerPrevious, ActionIds.PickerNext, HintLabels.Move);
    }

    private static HintChip? Enter(KeymapTable keymap, string label)
    {
        return DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, label);
    }

    private static HintChip? Close(KeymapTable keymap, InputMode mode)
    {
        return DialogFooter.Chip(keymap, mode, ActionIds.Cancel, HintLabels.Close);
    }

    private static string PickerEnterLabel(SourcePickerState picker)
    {
        if (picker.Scope == PickerScope.Tabs)
        {
            return HintLabels.Switch;
        }

        if (picker is { Scope: PickerScope.Jql, EditTab: not null })
        {
            return HintLabels.RunInThisTab;
        }

        if (picker.Scope != PickerScope.Sprints)
        {
            return HintLabels.OpenInNewTab;
        }

        return picker.Sprint?.MoveKeys is not null ? HintLabels.MoveIssues : HintLabels.Open;
    }
}
