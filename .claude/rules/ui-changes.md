---
paths:
  - "src/Geeratya.Tui/**"
  - "src/Geeratya.Core/**"
  - "tests/Geeratya.Tui.Tests/**"
  - "tests/Geeratya.Core.Tests/**"
---

# UI, keys and look

Specs start at `docs/internal/ux.md`, which links the parts; the key reference is generated in `docs/keymap.md` (design rules in `docs/internal/ux-keymap.md`) and the mouse table in `docs/internal/ux-mouse.md`. Checklist for a change that adds or alters what the user sees or presses:

## A new action, key or mouse path

- The key row goes in `src/Geeratya.Core/Keymap/keymap.default.json` in the same change, then run `scripts/gen-keymap-doc.sh` to refresh `docs/keymap.md`; no conflicts within a mode and view (tests check the file and the generated reference). A mouse path gets its row in the mouse table (a test checks it against `DefaultMouseMap`).
- Vim-clash rule (owner, carried over from Gitunia): standard vim/neovim keys keep their vim meaning; a Geeratya-specific action never binds a bare vim normal-mode command (`s` `u` `c` `a` `x` `r` `D` `M` `E` ...) but a `<space>` leader chord or a `Ctrl-`/`Alt-` combination. Check a new key against vim before binding it (list in `docs/internal/ux-keymap.md`, "Key rule").
- Everything doable by keyboard is doable by mouse and the reverse; nothing is keyboard-only or mouse-only. A mouse gesture maps to an action id and runs the same handler as the key.
- The action has a title in `ActionCatalog` (a test fails otherwise). An action bound ahead of its feature goes in `ActionCatalog.Unimplemented`; modal-only ones in `IsModalOnly`.
- Status bar hints are short, not taken from the catalog titles; each key names its `ActionId` and a test checks the default keymap binds it.
- Help, palette and which-key text is generated from the keymap and catalog, never hand-written.
- Key notation is spelled out everywhere (`Ctrl-d`, `Alt-1`, `Shift-Tab`, `<space>o`), never `^d` or `c-d`.
- `Esc` and `q` follow the precedence in `docs/internal/ux.md` ("Focus model and key style"). In Insert mode printable keys are text: only `Esc`, `Enter`, `Ctrl-` and `Alt-` combinations act.
- Destructive actions go through Confirm mode (`y`/`n`/`Esc` only, never `Enter`) and say what they do and how to undo it, or that Jira cannot undo it.

## Rendering and colour

- Terminal.Gui types stay in `Geeratya.Tui` (`Views`, `Input`); `Rendering` and Core know only styled text frames and renderer-agnostic key events.
- Key hints and buttons: a row of hints is drawn by `HintBar` (footers by `OverlayBox.Hints`), a clickable chip by `KeyedChip` with its key from `ChipKeys`; new hint rows and buttons do not hand-build key and label segments or bracket buttons (`docs/internal/architecture.md`, "Shared key hints and chips").
- Screens produce `StyledLine`/`StyledSpan` with a Core `TextRole` (via an `IBodyRenderer`), never colours; `PaletteTable.For` maps roles to palette roles.
- Colours are decided in one place: `PaletteTable` and `Palette`. Never hard-code a colour in a view. Contrast is tested (4.5, dividers 3.0).
- A chip that starts a bar line (mode badge, banner title) starts at column 0. Network states (loading, offline, rate limited, error) have their own palette roles and a visible indicator; a stale view says it is stale.
- Every colour tier has a path: 24-bit, 16-colour, `NO_COLOR`/`TERM=dumb`, and the ASCII glyph set. Focus and selection never rely on colour alone.
- Layout is data (`PaneLayout`, `GraphColumnLayout`, `FrameGrid`) and is unit- and snapshot-tested at several terminal sizes, including narrow and tiny ones; nothing may crash on resize.
- Wide characters are never split; text width goes through `TextWidth` (backed by `DisplayWidth` in Core).
- UI PRs with a visual change attach one capture (`screenshots` skill); none for key labels, hints only, docs or logic.
- New action checklist (Gitunia alignment, 2026-11-03): every new user action has an action id, a title, a level (everyday or advanced), an applies-to tag, a palette entry, a keymap row (`keymap.default.json`, then `scripts/gen-keymap-doc.sh`), a mouse path or an explicit reason (`docs/internal/ux-mouse.md`), a hint only if everyday, and reports its failures as a status message with a severity. Before designing a key, mode or screen, read the matching Gitunia doc (read-only) and say in the PR where Geeratya differs and why; a difference without a reason becomes a follow-up issue, not code.
