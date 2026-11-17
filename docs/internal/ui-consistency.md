# UI consistency audit and shared components

Audit of 2026-11-17, started when Assignee and Reporter behaved differently in the create-sub-task form and in the issue view. It covers forms and text editing; pickers, popups, menus and overlays; lists, panes and value display; colours and text roles; keys; mouse; hints; and the rendered frames (default theme, against the fake Jira). Findings were read from code by parallel audits; the claims this document builds on were re-checked by hand. What was not covered is listed at the end.

**The cause, in one line:** the shared pieces exist (`FormReducer`, `OverlayBox`, `SuggestionState`, `SuggestionRanker`, `PromptText`, `PersonChips`, `Glyphs`, `StatusRoles`, `PaneScroll`, `ListMotion`, `HintBar`), but nothing makes new code use them. So each surface added a copy or a special case (`form.Target is CreateFormTarget`, `field.Id == Assignee`), and the copies drifted.

## Owner decisions

Decided (2026-11-17):

1. **Empty values.** People read "Unassigned"; every other empty value reads "None". The same wording appears on every surface.
2. **Esc and q.** Esc works in two steps: the first leaves field editing for the navigate state, the next closes. In a state where it is not text, `q` does the same, closing one level at a time (popup, field, form, overlay, issue, zoom, Visual). Only on the main view does `q` quit the app.
3. **Mouse wheel.** It depends on the context: the wheel acts on what is under the pointer. Where there is a highlight it moves the highlight, and where there is none it scrolls.
4. **Validation.** It is mixed: formats (number, date) are checked as you type, and required fields and Jira's answer are checked on save.
5. **Reporter** follows Jira. It is shown and editable only when the create or edit metadata offers it. It is prefilled with the default Jira gives, and Jira itself sets the creator when none is sent. This is to be confirmed live on the dev site in a sandbox project.

Recommended defaults, open for the owner to override:

6. **Typed text** uses the normal text colour everywhere. The caret gets its own theme slot, not `Warn`.
7. **Wheel over a form,** outside its popup: it does nothing, so a stray scroll never moves you out of a field. Over a small popup (about 5 rows) a notch moves 1 row; elsewhere it moves 3.
8. **Confirm boxes** have a red frame when Jira cannot undo the action (delete, transition, sprint complete) and a yellow frame when the action can be reversed in Geeratya (unlink, rank across sections).
9. **Key labels** use one role and one colour everywhere: footers, hint bar, Help, which-key, palette shortcuts and the context menu.
10. **A typed filter:** the first Esc clears it and the next one closes. This applies everywhere: picker, Jump, palette, Help and choice fields.
11. **The navigate state** is used by every form with more than one editable field, not only the create form.

## Findings

### Shared today, but bypassed

- **Status colours:** `ListBodyRenderer.StatusRole` duplicates `StatusRoles.Of`, and `SprintHeaderText` hardcodes `"done"` instead of using `StatusCategoryKeys.Done`.
- **Priority glyphs:** `Glyphs.PriorityHigh/Medium/Low` is bypassed by literal `▲ = ▼` in `DetailsPane.cs:23-25` and `CreateFieldBlocks.cs:160-162`. Those copies also use other roles (Warning, ValueBlock, Muted) than cards do (PriorityHigh, Medium, Low).
- **Number format:** `EstimateText` uses `0.##`, but `SprintHeaderText.Number` and `PointsText` use `0.#`.
- **Truncation:** `DisplayText.Cut` and `CardText.Shorten` (with an ellipsis) are bypassed by `TextWidth.Fit`/`Pad` (hard cut) in the list, and by inline cuts in `FrameGrid`, `TabTitles`, `DeleteIssueRequest`, `CommentEditing` and `PromptWindow`.
- **Hint rows:** `HintBar` + `HintChip` is used only by the status bar. Overlay footers parse strings with `OverlayBox.Hints`, and buttons and confirm answers are spans built by hand. `.claude/rules/ui-changes.md` names `KeyedChip`/`ChipKeys`, which do not exist.
- **Scroll markers:** `OverlayBox.ScrollMarker` is not used by the profile manager, Messages, the context menu or the 5-row popups.
- **Scroll types:** `PaneScroll` (offset, chip, scrollbar) is used only by the detail panes and the create pane.
- **Matcher:** `PaletteFuzzy` is used only by the palette. The picker, Jump, Help, the tab list and the sprint list use plain substring matching instead, with 7 copy-pasted `Contains`.
- **Popup list:** `SuggestionState`/`SuggestionPopup` is bypassed by the Settings choice list and by the create form's issue-type chooser.
- **Field-kind helpers:** `FormFieldKinds.IsPicked/IsChoice/IsText` are bypassed by explicit kind lists in `FormOverlay`, `CreateFieldBlocks`, `DetailDropdown` and `FormField.IsChanged`.
- **Confirm:** `ConfirmRequest` is used for the comment discard prompt. Forms have their own inline `DiscardPending`.
- **Validation:** `FormValidation` is not used by the profile form, the comment editor or settings.
- **Field blocks:** `FieldBlock` is used only by the create form. The boxed form and the dropdown draw fields by hand.
- **Issue type:** `IssueTypeRole`/`IssueTypeMarker` are skipped by the detail title and the relation table.

### Duplicated code

| What | Copies |
|---|---|
| Cursor move `Math.Clamp(cursor + delta, 0, count - 1)` | 9 (suggestion popup, context menu, source picker, Jump, palette, profile list, Help, settings, type chooser) |
| "Keep the selected row visible" (`FirstVisible`, `PopupFirst`) | 10 in `Tui/Rendering`, each pinned to the bottom with no scroll margin |
| List scroll with a margin | 4 (`ListMotion`, `BoardScroll`, detail `WithCursor`, `PaneScroll.Reveal`) |
| Half page | 5 definitions (viewport/2, cards that fit, detail stops, 5 rows in Settings, 8 lines in Help) |
| Popup list renderer (5 rows, pointer, Loading/Failed/No match) | 4 (`FormOverlay.Fields`, `CreateDetailsRows`, `DetailDropdown`, `CommentEditorOverlay`) |
| Box scaffolding (width, gaps, backdrop, cover, footer) | 4 (source picker and Jump almost line for line, palette, context menu) |
| Quick rows (`SuggestionState` built by hand) | 3 (`QuickPopup`, `CreateQuickPopup`, `LabelRows`) |
| Text buffers | 2 (`PromptText` and `TextBuffer`; the same editor actions are mapped twice, with maximum lengths of 32767 and 30000) |
| Caret renderers | 4 (`CaretRows`, `PromptSpans`, profile form, literal `_` in `DetailDropdown`) |
| Field value rendering | 3 (`FormOverlay.ValueLines`, `CreateFieldBlocks.Items`, `CreatePaneLayout`) |
| Form value to Jira conversion | 2 (`FieldUpdates.Of` for edit, `NewIssues.ValueOf` for create), which disagree on clearing, dates and number types |
| Discard prompt | 3 (form `DiscardPending`, comment `ConfirmRequest`, pane-edit hint) |
| "Unassigned" / `[--]` constants | 6 (`DetailsPane`, `CreateFieldBlocks`, `RelationTable`, `CardRows`, `ListBodyRenderer`, `Swimlanes`) |
| Date range "d MMM - d MMM" | 2 (`SprintHeaderText`, `BacklogBodyRenderer`) |
| Filter matching | 5 kinds (`PaletteFuzzy`, `SuggestionRanker`, case-sensitive `Contains` in Jump, case-insensitive `Contains` ×7, `NameMatcher`) |

### Behaviour that depends on the form, not the field

There are about 15 places where a target or field id decides behaviour that belongs to the field kind. Among them:

- `FormReducer.Suggestions.cs` uses `is CreateFormTarget`, `is AssignFormTarget` and `is EditFormTarget` to decide the quick rows, the search source and the parent scope.
- `FormReducer.Saving.cs:30` sets `Navigable` only for `CreateFormTarget`.
- `FormReducer.Saving.cs:84`: a dropdown cancels without the discard prompt.
- `FormReducer.Suggestions.cs:58`: a dropdown changes what Enter does.
- `CreatePanes`, `CreateFieldBlocks` and `CreateFormTarget` decide by id (Summary, Description, Parent, Priority).
- `EditRequest` and `NewIssues` decide by id (Summary, Priority, Labels, Parent).
- `FormMetadataHandler` dispatches on a chain of target types.
- `DetailFieldActions` routes per field id.

### Differences a user would notice

**Values**

- An empty assignee reads `-` in the list, `[--]` on a card, and `[--] Unassigned` in Details, the relation table and the create form.
- An empty Reporter is hidden in Details but reads "Unassigned" in the create form. When an issue is opened from a list row, the Reporter row appears late.
- An empty priority reads "None", empty labels read "none", and an empty due date reads "Due none". Empty sprint and estimate rows are hidden.
- An estimate of 0.25 reads `0.25` in the list and Details, but `0.3` on cards and in backlog headers.
- Priority is red on a card but yellow (Warning) in Details and the create form.
- The list summary is cut without an ellipsis; cards and the relation table use one.
- The issue type is coloured in the list and on cards, but not in the detail title or the relation table.
- Status is coloured by category in the backlog, the sprint list and sub-task rows. In the JQL list rendered from the fake data it was not coloured; this may be down to the fake data.

**Assignee, Reporter and other fields**

- Assignee in the issue view (the assign dropdown) offers Me and Unassigned, also when someone is already assigned. The create form offers only Me, and only when the field is empty. Edit and transition forms offer no quick rows.
- Reporter is read-only in the issue view, whatever Jira allows (`DetailFieldActions.cs:36`).
- People search uses `IssueUsers` in the issue view and `ProjectUsers` in the create form. Only this difference is intended, because a new issue does not exist yet.
- Labels: the Details dropdown lists the applied labels so you can remove one. The boxed and create forms do not; there, Backspace on an empty buffer removes one.
- Parent accepts a typed issue key in the link form only. Elsewhere you must pick from the list.
- Editing a field opens differently depending on the route:
  - From Details, Priority, Labels, Assignee and Parent open a dropdown, Status opens the transition picker, Estimate a boxed form, Sprint the source picker, and Summary and Description edit in place.
  - The same field from a list key opens a boxed form.
- The dropdown discards silently on Esc; the boxed form asks first.
- Saving an unchanged form keeps it open with "Nothing changed". An unchanged comment closes with "No change to save."
- Validation: forms check only on save. The profile form checks as you type.
- Date fields: the sprint form shows the format in the label; edit and create forms show it only after an error.
- The estimate form's error is cut at the box edge in the middle of a sentence.

**Keys** (against decision 2)

- `q` quits the app from an open issue, a zoomed pane, Visual mode, rank pickup, and Settings even with a choice list or theme preview open. It is bound once to `app.quit` (`keymap.default.json:22`, `AppReducer.Actions.cs:55`), with no check for a level to close.
- Only the create form has the navigate state. There, `q`, `gg`/`G` and `Ctrl-d`/`Ctrl-u` do nothing. Other forms close on the first Esc, and `j`/`k` type text there.
- The comment `FormState.Navigable` still says "edit form", which is stale (#762).
- Esc closes the picker, Jump, Flash and palette in one step even with text typed. In Help and in choice fields, the first Esc clears the filter.
- `j`/`k` move in Settings, Help, the context menu and create-form navigation. They are text in the picker, Jump and palette. They also move the highlight in the profile manager (#1005).
- The profile manager opened from Settings does not return to Settings on Esc.
- The profile form closes on Esc without asking, even when it has changes.
- After the transition picker, Esc on the transition fields form closes everything instead of going back to the picker.
- A status message at the view root costs one extra Esc.
- The first highlighted row varies: choice and quick-row popups start with no highlight; required single choices, the picker, Jump and the palette start on the first row.

**Mouse** (against decision 3)

- There is no wheel on surfaces with a highlight: form, create-form, dropdown and comment `@` popups, the Settings choice list, the profile list and the create-form field list.
- The wheel over Settings rows moves 1 row, although its map row says 3. The picker, Jump and palette take the wheel only over a row, not over the prompt or the footer.
- Documented but dead: part C of the mouse map (status badge, Assignee value, New issue, Add comment, comment click) is in `DefaultMouseMap` but excluded by `WiredParts` (`MouseResolver.cs:12`).
- `ux-mouse.md` is out of date on several points: which overlays take the wheel, the list of mouse commands, and "part A not wired". It also contradicts itself about the zoom chip.
- Keyboard-only: the in-place edit has no mouse path; Flash has no clicks; Help paging, `[[`, pane focus and new-tab from the picker have no mouse path. Comment add and edit are reachable only through a key or the menu, because part C is dead.

**Colours** (rendered and in code)

- Typed text is yellow (`Editable` maps to theme `Warn`, `PaletteTable.cs:85`) in boxed forms and in the create form's Summary and Description. It is the normal colour in the create form's detail fields, the in-place edit, the picker, Jump, the palette, the profile form and the comment editor.
- The caret has four looks: a yellow block (`EditCaret`, also on `Warn`), `_`, a grey `|` in the palette, and a blue bold `_` in the profile form. The Details dropdown appends a literal `_`.
- Empty placeholders in boxed forms are drawn as typed text (yellow `-`), so an empty field looks filled. Elsewhere, placeholders are grey.
- Key labels are blue bold in footers, yellow bold (`OverlayKey`) in Help and which-key, and grey in the palette. The same `Key` role is also used for issue keys, the `> ` prompt and the profile-form caret.
- Errors are `Error` in forms, `Warning` in the profile form and `Stale` (purple) for comment-editor warnings. The profile "test OK" message borrows `StatusDone`.
- Confirm frames are red for delete, status and sprint complete, and yellow for unlink and rank across sections. "Jira cannot undo this." is yellow in all of them.
- Borrowed theme slots: Accent carries 9+ meanings, including heading, key, field label, link, focus frame, Task type and In Progress. `Warn` carries typed text, the caret, key labels, warnings and Medium priority. ADF red text maps to `Error`, so it looks like an error.
- Which-key dims the background like every other overlay (fixed in phase 1).
- The Details dropdown has no highlighted row until you move.
- In Settings, the category and the first setting are both highlighted, so it is unclear which pane has focus.
- With colour off (`NO_COLOR`), field labels, typed text and read-only values look alike. The focused form field has no marker; the `>` mark was removed, but its doc comment still mentions it. Error, Warning and RateLimited differ only by colour.
- Literal glyphs bypass `Glyphs`: scroll markers, `●`, `○`, `▌`, `›`, `↑` and card corners. The detail scroll markers have no ASCII branch (unsure).

**Hints**

- Esc goes by 7 labels: back, close, cancel, close list, clear filter, stop editing and keep editing.
- Enter goes by about 12: open, switch, move, run, choose, accept, change, keep, nearest, next, save, new line and edit/open.
- The boxed form says "Tab/Shift-Tab field"; elsewhere it is "Tab field".
- Help, Messages, the context menu, the profile manager and which-key got typed footers in #1005 (shown when the box is tall enough). Flash, the Details dropdown and the create form still have none and rely on the status bar, which Minimal mode drops.
- Some footer key labels are hardcoded (Settings `j`/`k`/`Enter`/`Esc`, `Ctrl-f`, `?`). Other footers print an empty string when an action has no key.
- The profile list shows the INSERT badge with no footer hints.

## Shared components

Each component has a pure part in Core (state and behaviour, no terminal) and one renderer in Tui, following `architecture.md`.

1. **`ValueText`** (Core/Text) renders each value type: person, status, priority, issue type, issue key, labels, sprint, estimate, date and empty. Each has full, compact and glyph variants chosen by width. Roles come from `StatusRoles`, `IssueTypeRole` and `Glyphs`. Every surface uses it: list, card, backlog, Details, relation table, forms and status bar. One number format and one empty wording (decision 1).
   - *Replaces:* the 6 "Unassigned" copies, the priority literals, `StatusRole`, the three estimate formats and the date-range copies.
   - *Status (#997):* `ValueText` exists for person, status, priority, estimate, date range, due and empty, and the list, card, backlog, sprint header, Details, relation table and create/boxed form use it; the issue type word in the detail title takes its `IssueTypeRole`. Labels, sprint and issue key keep their own renderers (they already share `LabelChips` and `TextRole.IssueKey`); the status bar shows none of these values.
2. **`FieldKind`** behaviour, one per kind: person, choice, multi-choice, labels, parent, text, paragraph, number and date. Each kind decides its quick rows (person: Me unless already me, plus Unassigned when the field can be cleared and has a value), its search source given a context (issue key or project key), what Enter and Tab do, what Esc does to a filter, its validation timing (decision 4), what an empty value means, and its conversion to Jira. One converter serves both create and edit. Targets only supply the context and the field list.
   - *Replaces:* the per-target and per-id branches, the separate create quick rows, the dropdown-only `LabelRows` (now a labels rule for every form, still in `FormReducer.Suggestions`), `FieldUpdates.Of`, `NewIssues.ValueOf` and the explicit kind lists.
   - *Status (#997):* the person kind is `PersonField` (Core/Forms): quick rows and search source by field and context (`FormTarget.SearchProjectKey`, `IssueKey`) in the create form, assign dropdown, boxed edit and transition fields, with one contract test set (`PersonFieldContractTests`). The Reporter follows Jira's metadata (create: listed and prefilled with the signed-in account, which Jira sets when none is sent; issue view: edit metadata). 
   - *Status (#1000):* the other kinds follow `FormFieldKinds` (`TakesEntry`, `HasPopup`, `HasFormat`, `Labelled`) instead of explicit kind lists; the parent search scope comes from the target (`FormTarget.ParentProjectKey`, `ParentHierarchyLevel`), not from `is EditFormTarget`; a typed full issue key is accepted in every parent field; a labels field lists its applied labels for removal in every form; number and date formats are checked as they are typed (`FormValidation.FormatError`; required and Jira's answer wait for save) and date labels spell out the format. One converter, `FieldConverter` (App), serves edit (`ForEdit`, adds and removes of sets) and create (`ForCreate`, whole values); `NewIssue.Fields` carries `FieldUpdate`s, so both send the same JSON (`CreateFieldValue` is gone). Contract tests: `FieldKindContractTests` (every kind across create, edit, dropdown and transition) and `FieldConverterTests`. What stays by id on purpose: Summary, Description and Parent slots of Jira's create call (`NewIssues`, `CreateFormTarget`), the list-row patch of `EditRequest`, and the Priority role in the create pane, because they name Jira fields that a row or an API slot shows, not a behaviour of a kind. Labels and sprint renderers stay off `ValueText` (they share `LabelChips`; they show no empty wording that differs between surfaces).
3. **`FormShell`**: one form behaviour however the form is drawn (box, create pane, dropdown, in place). It covers the navigate and edit states (decisions 2 and 11), the Esc and `q` steps, one discard prompt through `ConfirmRequest`, "nothing changed", and how errors are shown. The presentation decides only the layout. One field renderer per kind serves all four layouts. Details opens every editable field the same way, Reporter, Status, Sprint and Estimate included, governed by Jira's metadata.
   - *Replaces:* the 3 field renderers, the 3 discard prompts, the dropdown cancel and Enter special cases, and the per-route field opening in `DetailFieldActions`.
4. **`ListCursor`**: selected index, scroll, `Move`, `Page`, `Top`, `Bottom` and `Reveal`, with one scroll margin and one half-page rule. Item heights are an optional input for cards and the detail view.
   - *Replaces:* the 9 clamp copies, the 10 `FirstVisible`/`PopupFirst` copies, and the list scroll in `BoardScroll`, `ListMotion` and the detail view (keeping their height rules).
5. **`PickList`**, the "choose one" control. Its state holds the items, quick rows, a filter run by one `Matcher` (merging `PaletteFuzzy` and `SuggestionRanker`, case-insensitive), a `ListCursor`, a status (waiting, loading, slow, failed, empty) and a defined first highlight. One renderer draws rows, pointer, highlight, scroll marker, count, empty and loading text, and mouse regions with click and wheel (decision 7). Its keys are Up/Down, `Ctrl-n`/`Ctrl-p`, `j`/`k` when there is no text, Enter, Tab, and Esc with the filter-first step (decision 10).
   - *Used by:* source picker (all scopes, tab list, sprint move), Jump, palette, context menu, form, create-pane and dropdown popups, comment `@` popup, the Settings choice list, the type chooser and the profile list.
6. **`Dialog`**: `OverlayBox` plus a footer built from typed `HintChip` values instead of strings, through one label table (one word per action: Esc is "close", or "back" when it returns to a previous screen; Enter is "open", "choose" or "save"). There is one button and chip helper (the `KeyedChip` the rules promise), with click regions. Background dimming and the frame role follow the dialog kind (decision 8). The profile manager, Settings, which-key and Help move onto it.
7. **`TextEditor`**: one buffer (merging `PromptText` and `TextBuffer`, one maximum length), one mapping of editor actions and one caret renderer. Typed text uses the body role and the caret its own role (decision 6). Placeholders are `Muted`.
8. **`NavigationLevels`**: one "close one level" handler, the only path for Esc and for non-text `q`. It closes, in order: popup, filter, field editing, form, overlay, Visual or marks, zoom, the issue stack, and finally the view root, where `q` quits after the running-writes check.
   - *Replaces:* `Quit` as the direct target of `q`, and the per-surface Esc ladders.

### Guardrails

- A "Shared components" section in `architecture.md` lists these, each with its Core type and renderer.
- `.claude/rules/ui-changes.md` says that a new field, list, picker, dialog, value display or text input must use the matching component. The `KeyedChip` line is replaced by the real helper.
- Contract tests per component: one set of scenarios per field kind, run against every form target and every presentation, and one set per `PickList` and `ListCursor` user. A new user of a component is added to the matrix, not given its own tests.
- A rendered-frame check in the screenshot scenarios: typed text, caret, placeholder, key labels and confirm frames have the same SGR on every surface (`docs/internal/testing.md`).
- `ux-mouse.md` and the code are checked against each other by the existing test, extended to fail on map rows that are not wired.

## Phases

| # | Phase | Contents | Needs |
|---|---|---|---|
| 1 | Visible quick fixes | Typed text and caret roles (6), placeholders `Muted`, one key-label role (9), confirm frame rule (8), one estimate format, "Unassigned"/"None" wording, error lines cut with an ellipsis, Settings focus highlight, which-key dimming, `NO_COLOR` focus marker. Docs: `ui-changes.md` (the `KeyedChip` line), stale `FormState.Navigable` comment, `ux-mouse.md` mismatches, and the dead part-C rows hidden from the doc until they are wired | none |
| 2 | Value display and person fields | `ValueText` on every surface. Person `FieldKind`: Assignee and Reporter behave the same in every form. Reporter follows Jira's metadata in create and the issue view (live check on the dev site) | 1 |
| 3 | Esc and `q` | `NavigationLevels`: decision 2 on every surface, including the transition fields going back to the picker, and the profile manager going back to Settings | none |
| 4 | Lists and pickers | `ListCursor` and `PickList` with one `Matcher`, `Dialog` footers from `HintChip`, the wheel per decisions 3 and 7, j/k in the profile manager | 1 |
| 5 | Field kinds | The remaining `FieldKind`s, one create/edit converter, validation timing (decision 4), labels removal everywhere, typed parent key everywhere | 2 |
| 6 | Form shell | The navigate state for every form (11), one discard prompt, one field renderer for box, pane, dropdown and in place, and Details opening every field the same way | 3, 4, 5 |
| 7 | Text editor | One buffer and caret for prompts, forms and the comment editor | 6 |

### Phase 4 status (#999)

Done: `ListCursor` (replaces the clamp copies in the reducers and the six `FirstVisible` copies in the overlay renderers and the two `PopupFirst` copies; `ListMotion` and `BoardScroll` build on it), `Matcher` (shared by `PaletteFuzzy`, `SuggestionRanker`, the picker, tab list, sprint list and Jump filters), `HintLabels`, `DialogFooter` and `ChipButtons` (footers of the picker, Jump, palette, form, comment editor, profile form and Settings; the profile buttons), the wheel by context with `OverlayWheeled` and the contract tests (`OverlayWheelContractTests`), and the rewritten wheel text in `ux-mouse.md`. Not done in #999 and carried to #1005, which finished the footers, the profile-manager `j`/`k` and the dimming and left #1010 and #1011 (see below): a click scenario and the `Ctrl-n`/`Ctrl-p` keys for Jump and the palette in the pick-list contract tests (it runs wheel and Up/Down today), the `MouseMapTests` check that fails on map rows that are not wired (the table test checks only that doc and code agree), the `PickList` state and renderer proper (the four popup renderers and the state types are still separate; the contract tests already run one key and wheel scenario over all of them), `j`/`k` in the profile manager, footers for Help, Messages, the context menu, the profile manager and which-key (they rely on the status bar), and the Dialog dimming and frame role by kind.

### Phase 4 status, #1005

Done: `j`/`k` move the profile list (it has no text field; the profile form keeps them as text), typed footers (`DialogFooter`, new labels `Page`, `Filter`, `Cancel`) on Help, Messages, the context menu, the profile manager and which-key, shown only when the box has room (Help and the context menu from 12 rows, the profile manager from 6 inner rows, which-key when the list leaves two rows and one row of the view above). The Dialog dimming and the frame role by kind were already in place (which-key dims, Confirm picks its frame by `ConfirmOverlay.KindOf`). Not done, registered as #1010 and #1011: the `PickList` state and renderer proper, `j`/`k` in the picker, Jump and the palette when there is no text (owner decision: project keys such as `KAN` or `JRA` start with `j` or `k`, so the first letter could no longer be typed), the typed Confirm answer rows, a click scenario and the `MouseMapTests` wiring check.

### Phase 4 status, #1010

Done: `PickList` (Core/Lists: highlight, `PickStatus`, message, moves and windows through `ListCursor`) is the state of the source picker (`SourcePickerState.Pick`), Jump, the palette (`AppState.PalettePick`), the context menu, the field popups (`SuggestionState.Pick`, which replaces the `SuggestionStatus` enum), the Settings rows and choice list, the type chooser (`FormState.TypePick`) and the profile list. The old `Selected`/`Cursor`/`Status`/`Error` names stay as init-compatible aliases over it (so `with { Selected = 3 }` and the old constructors still work). `PickListRenderer` (Tui) draws their rows: the popup of the form, create pane, Details dropdown and comment `@` popup is one `Popup` (so the comment popup now words a failed lookup as the others do), the box lists take their window, scroll markers and regions from `PickList.WindowAt`, `Walls` and `Mark` (the context menu now shows scroll markers too), and the type chooser uses `Row`. `AnswerRows` (typed `AnswerRow` values, lines and regions from one list) replaces the hand-built Confirm answer rows. Tests: `PickListTests`, `PickListRendererTests`, `AnswerRowsTests`; `OverlayWheelContractTests` gained a click scenario and a region check for every user; `MouseMapTests` (resolver: a wired row must produce a command, the document's "Not wired" text must match the part) and `MouseWiringTests` (a wired overlay target must be drawn by some real overlay and resolve on its frame).

Not merged into one type, on purpose: the item lists. A pick list's rows come from different sources (the picker's board, filter and query lists, Jump's keys and history, the palette's commands, Jira's suggestions), so each user still derives its rows and passes `PickList` their count; the filter itself is `Matcher` everywhere. The overlay boxes keep their own frame, title and prompt line. `j`/`k` in the picker, Jump and the palette stays unbound (#1011).

Phase 6 (`FormShell`) builds on this: its dropdown is `SuggestionState` plus `PickListRenderer.Popup` in every layout, and a new form presentation adds no list code.

## What this audit did not cover

- **Rendered frames not captured with typed text:** Details dropdown typing (Assignee, Labels), the Settings choice list, profile-form field focus, the JQL prompt, and the comment editor with several lines. A few captures timed out and may show another state: detail section, zoomed description edit, epic relations.
- **Frames captured but not decoded:** board lanes and filters, sprint views, rank, backlog sections, light and transparent themes, comments and zoom variants, relations and epic views. Theme scenarios ran in a non-default theme.
- **Not checked:** themes other than the default; 16-colour and ASCII tiers (from code only); terminal widths other than the scenario defaults; Windows and macOS rendering.
- **Not traced end to end:** zoomed-pane `j`/`k` and `gg`; whether `Tab` in the detail view is meant to switch tabs; whether the ASCII tier replaces the detail scroll glyphs; `EditCaret` with colour off.
- **Out of scope:** performance of the renderers and the content of `docs/keymap.md`.
