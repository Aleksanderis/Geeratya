using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Forms;

public static partial class FormReducer
{
    private static ReduceResult Loaded(AppState state, FormMetadataLoaded loaded)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, loaded.Tab);

        if (tab?.Form is not { IsLoading: true } form || form.RequestId != loaded.RequestId)
        {
            return Result(state);
        }

        var target = (loaded.Target ?? form.Target).Loaded(loaded.Fields);
        var top = loaded.Fields.Any(field => field.IsEditable) ? null : target.EmptyText;
        var filled = form with
        {
            Target = target,
            Fields = loaded.Fields,
            IsLoading = false,
            TopError = top,
        };
        var navigable = target is EditFormTarget && loaded.Fields.Count(field => field.IsEditable) > 1;
        var shown = filled with { Focus = Step(filled, 0), Navigable = navigable, Navigating = navigable };

        if (target.Automatic is { } changes)
        {
            return Send(Replace(state, tab, shown), tab, shown, changes, loaded.At);
        }

        return Choosing(Result(Replace(state, tab, shown)), tab.Id);
    }

    private static ReduceResult Failed(AppState state, FormMetadataFailed failed)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, failed.Tab);

        if (tab?.Form is not { IsLoading: true } form || form.RequestId != failed.RequestId)
        {
            return Result(state);
        }

        return Result(Replace(state, tab, form with { IsLoading = false, TopError = failed.Message }));
    }

    /// <summary>While the save runs the form ignores edits; Esc closes it, the result goes to the status line.</summary>
    private static ReduceResult Saving(AppState state, TabState tab, string? action)
    {
        if (action != ActionIds.Cancel)
        {
            return Result(state);
        }

        return Result(Closed(state, tab) with { Message = DetachedText });
    }

    private static ReduceResult Discarding(AppState state, TabState tab, string? answer, string? action)
    {
        if (answer == ActionIds.ConfirmYes)
        {
            return Result(Closed(state, tab) with { Message = "Discarded the changes" });
        }

        if (answer == ActionIds.ConfirmNo || action == ActionIds.Cancel)
        {
            return Result(Replace(state, tab, tab.Form! with { DiscardPending = false }));
        }

        return Result(state);
    }

    private static ReduceResult Cancel(AppState state, TabState tab, FormState form)
    {
        if (form.IsDirty)
        {
            return Result(Replace(state, tab, form with { DiscardPending = true }));
        }

        return Result(Closed(state, tab));
    }

    private static ReduceResult Enter(AppState state, TabState tab, FormState form, DateTimeOffset at)
    {
        if (form.Fields.Count == 0 || form.Focus >= form.Fields.Count)
        {
            return Result(state);
        }

        var edit = FormEditor.Accept(form.Fields[form.Focus]);
        var fields = WithField(form.Fields, form.Focus, edit.Field);
        var edited = form with { Fields = fields };

        if (!edit.Advance)
        {
            return Result(Replace(state, tab, edited));
        }

        if (form.Navigable)
        {
            return Result(Replace(state, tab, edited with { Navigating = true, Suggest = null }), tab.Id);
        }

        var next = NextEditable(edited, form.Focus);

        if (next is null)
        {
            return Submit(Replace(state, tab, edited), tab, edited, at);
        }

        return Result(Replace(state, tab, edited with { Focus = next.Value }));
    }

    private static ReduceResult Submit(AppState state, TabState tab, FormState form, DateTimeOffset at)
    {
        if (form.IsLoading || !form.Fields.Any(field => field.IsEditable))
        {
            return Result(state);
        }

        var committed = form.Fields.Select(FormEditor.Committed).ToArray();
        var checkedFields = FormValidation.Check(committed);
        var firstError = IndexOfError(checkedFields);

        if (firstError >= 0)
        {
            var invalid = form with { Fields = checkedFields, Focus = firstError, TopError = null };

            return Result(Replace(state, tab, invalid));
        }

        var changes = checkedFields
            .Where(field => field.IsChanged)
            .Select(ChangeOf)
            .ToArray();

        if (changes.Length == 0 && !form.Target.AllowsNoChanges)
        {
            return Result(Replace(state, tab, form with { Fields = checkedFields, TopError = NothingChangedText }));
        }

        return Send(state, tab, form with { Fields = checkedFields, TopError = null }, changes, at);
    }

    private static ReduceResult Send(
        AppState state,
        TabState tab,
        FormState form,
        IReadOnlyList<FieldChange> changes,
        DateTimeOffset at)
    {
        if (form.Target.Following(changes) is { } following)
        {
            return Open(state, tab.Id, following);
        }

        var request = form.Target.Request(changes);
        var started = WriteReducer.Reduce(state, new WriteRequested(tab.Id, request, at));

        if (state.Confirm is null && started.State.Confirm is not null)
        {
            // The write asks first: the form is done, the answer decides.
            return started with { State = Closed(started.State, tab) };
        }

        if (started.State.Session.Operations.Count <= state.Session.Operations.Count)
        {
            // Refused (busy, no profile): the form stays as it is, with the reason on its top line.
            var refused = form with { TopError = started.State.Message };

            return Result(Replace(state, tab, refused));
        }

        if (form.Target.ClosesOnSend)
        {
            return started with { State = Closed(started.State, tab) };
        }

        var locked = form with { OperationId = started.State.Sequence };

        return started with { State = Replace(started.State, tab, locked) };
    }

    internal static AppState Closed(AppState state, TabState tab)
    {
        var current = ViewReducer.Find(state.Session.Tabs, tab.Id) ?? tab;
        var closed = ViewReducer.Replace(state, current with { Form = null });
        var active = closed.Session.Active == tab.Id && closed.Mode == InputMode.Insert;

        return active ? closed with { Mode = InputMode.Normal, PendingKeys = string.Empty } : closed;
    }

    private static AppState Unlocked(AppState state, TabState tab, WriteError? error)
    {
        var form = tab.Form!;
        var fields = form.Fields.Select(field => field with { Error = FieldErrorOf(error, field) }).ToArray();
        var focus = IndexOfError(fields);
        var mapped = fields.Count(field => field.Error is not null);
        var allMapped = error is not null && mapped == error.FieldErrors.Count && error.Messages.Count == 0;
        var top = allMapped && mapped > 0 ? null : state.Message;

        var unlocked = form with
        {
            OperationId = null,
            Fields = fields,
            Focus = focus >= 0 ? focus : form.Focus,
            TopError = top,
        };

        return ViewReducer.Replace(state, tab with { Form = unlocked });
    }

    private static string? FieldErrorOf(WriteError? error, FormField field)
    {
        return error?.FieldErrors.FirstOrDefault(candidate => candidate.FieldId == field.Id)?.Message;
    }

    private static int IndexOfError(IReadOnlyList<FormField> fields)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (fields[index].Error is not null)
            {
                return index;
            }
        }

        return -1;
    }
}
