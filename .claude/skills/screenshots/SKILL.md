---
name: screenshots
description: Make, publish and clean real screenshots of the Geeratya UI for pull requests (tmux capture of the live binary against the fake Jira, converted to PNG, hosted in the separate screenshots repository). Use when a PR changes what is rendered, or when asked to take or tidy screenshots.
model: sonnet
---

# Screenshots

UI pull requests with a visual change carry one capture (2-3 images: colours, glyphs, layout; none for key labels, hints only, docs or logic; one `render` run per PR, not per push) next to the `--print-frame` text. `scripts/screenshot.sh` (M0; Linux only; needs bash, tmux, rsvg-convert, dotnet, and `gh` for publish/clean; a missing tool prints which one and exits 3) does everything locally; CI never runs it.

- `scripts/screenshot.sh render --binary <geeratya> [--out <dir>] [--scenarios <list>]`: starts `tools/FakeJira` on a deterministic dataset (never the dev site, never a real site: screenshots must hold no real names, keys or text), runs the real binary in a private tmux server (truecolor, private `XDG_*`), captures `capture-pane -p -e`, converts it to SVG with `tools/AnsiToSvg` and then to PNG with rsvg-convert into `<dir>/<scenario>.png`. Scenarios are defined in the script; currently `default` and `help` 120x36, `narrow` 64x30, `list` 120x36 and `list-narrow` 64x24 (a `:jql` view; `|` in a scenario's keys sends Enter) and `light` (light default colours of the image only, the app has no light palette yet); add a row to the script table per new screen, with the text to wait for (`docs/testing.md`, "Screenshot pipeline"); the script seeds an active fake profile and `GEERATYA_TOKEN=fake`, so the binary reads the fake.
- `scripts/screenshot.sh publish --pr <n>`: pushes the PNGs to `pr-<n>/<short sha>/` on `main` of the separate private `<owner>/<repo>-screenshots` repository (override with `GEERATYA_SCREENSHOTS_REPO=owner/name`; plain push, never forced, only that repository) and prints markdown for a PR comment. Links are `https://github.com/<owner>/<repo>-screenshots/raw/<screenshots commit>/pr-<n>/<sha>/<scenario>.png`, pinned to a commit so later pushes never break them. Exits 4 when the repository cannot be cloned; creating that repository is the owner's call.
- `scripts/screenshot.sh clean`: removes `pr-<n>` directories whose PR is merged or closed and pushes the removal. Run it after merging. The code repository never grows from screenshots. `publish`/`clean` commit as `Claude (implementer)` (override with `GEERATYA_AGENT_ROLE`) with an `Agent:` trailer and retry a rejected push once after `pull --rebase`.
- Screenshots are made on Linux only.
- Agents attach the images, they never read them back (token cost). Look at one yourself only when debugging the pipeline.
- Known limits: the font is whatever the machine has, so glyph widths and Nerd Font icons differ from the owner's terminal: those stay a manual check. A frame is captured once after it settles; animations, spinners and the mouse are not covered.
