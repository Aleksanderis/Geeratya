---
name: testing
description: Write, run and debug Geeratya tests; record and scrub Jira fixtures; run the fake Jira server; use the free Jira dev site safely for live checks. Use whenever you add or change code that needs tests, need Jira data to run the app or a test against, or are unsure how the real Jira API behaves. Never touch the owner's company Jira.
---

# Testing

This skill is the procedure. Strategy, layers and the measurement harness are in `docs/testing.md`; open it only for the section you need, not up front. Pieces marked "(M0)" do not exist yet and are built in the first milestone; until then, create the smallest version you need and register the rest.

## Mark OS-independent tests

The Windows CI leg runs `--filter "Os!=Independent"`; Linux runs everything. Put `[Trait("Os", "Independent")]` on a test class only when it is pure: in-memory data, the in-process fake Jira handler, no file system, temp dirs, paths, environment, process, sockets, terminal or ANSI capability, line endings or encodings. Unmarked is the default and means "runs on Windows", so when unsure leave it unmarked. A guard test fails when a marked file uses one of those APIs (`tests/Geeratya.Jira.Tests/Guards`).

## Pick the layer

| Changing | Write | Notes |
|---|---|---|
| A JSON mapper or ADF parser | Fixture test: recorded response in, model out | Recorded from the dev site and scrubbed (below); include odd cases |
| A Jira call (read or write) | Transport test on `FakeJiraHandler` | Assert on the request sent (method, path, query, body) and the typed result; cover 401/403/404/429/5xx, timeout and cancel |
| JQL building | Pure test | Quoting and escaping of quotes, backslashes, reserved words, unicode |
| Paging, refresh, caches | Store test on the fake with scripted pages | Last page, empty page, results changing between pages, stale reply after the query changed |
| State, commands, keymap | Pure test, no terminal | Includes the "no binding conflicts" and "generated `docs/keymap.md` is current" tests |
| Rendering | Snapshot of the frame as text | Several terminal sizes, wide chars, long summaries, resize |
| Dependencies or serialization | AOT publish + smoke run | No `IL*` warnings |
| Behaviour the fake might get wrong | Live test on the dev site | Opt-in only (below) |

Name tests `Given<Context>_When<Action>_Then<Outcome>` (PascalCase parts). Tests must fail if the code is wrong; don't write tests that restate the implementation. Every write gets a test for the request it sends and for the "user cancels at the confirm" path (nothing is sent).

## Waiting and fakes

- No sleeps or polling loops to wait for background work. Wait on a signal: a UI pump that runs posted callbacks on the test thread until a condition holds, a `TaskCompletionSource` completed from a store's `Changed` event, or an injected delay or clock. A guard timeout only bounds a hang.
- `FakeJiraHandler` (`tests/Geeratya.Jira.Tests/Fakes`): an in-process `HttpMessageHandler` that serves fixture files by method and path, records every request, and can script per-request status, `Retry-After`, latency and a hang until cancelled. Unit and store tests use it; they never open a socket.
- `tools/FakeJira` (`dotnet run --project tools/FakeJira`; options and routes in `docs/testing.md`): a small local HTTP server over the same fixture set, for running the real binary (tmux, screenshots, measurements). Starts on a free port on `127.0.0.1`, prints its URL, can generate large datasets (`--issues 10000`) and inject latency and 429s. Point the app at it with a profile whose site URL is that address and any token.

## Fixtures

- Fixtures are real responses recorded from the dev site, stored as files next to the tests that use them (`Fixtures/<area>/<name>.json`). Never hand-write a response shape you have not seen; record it.
- Record (`scripts/record-fixture.sh <method> <path> [body]`, live-host guard included) and scrub before committing: site host becomes `example.atlassian.net`, account ids become stable fakes (`fake-account-1`), emails `@example.com`, display names `Test User 1`, avatar and self URLs rewritten, no `Authorization` or cookie data anywhere. Before committing, grep the fixture for `atlassian.net` (only `example.atlassian.net` may remain) and for the token.
- Keep each fixture small: trim to the fields the test needs plus one or two unknown ones (mappers must ignore them).

## Live dev site

The owner's free Jira Cloud dev site is the only live Jira agents may use. The company site is never used, not even read-only.

- Environment: `GEERATYA_LIVE_HOSTS` (comma-separated allowlist, the dev site host only), `GEERATYA_LIVE_EMAIL`, `GEERATYA_LIVE_TOKEN`, `GEERATYA_LIVE_PROJECT` (the sandbox project key). Missing variables mean live tests skip; they never fail the normal run. The token is never echoed: no `set -x`, no `env` dumps, no printing the variable, no `cat` of the env file.
- The variables live in the owner's env file `${GEERATYA_LIVE_ENV:-$HOME/.config/geeratya/dev-site.env}` (outside the repo). A session may have started before they were set (remote sessions cannot restart), so every live command loads the file itself in the same shell call: `. "${GEERATYA_LIVE_ENV:-$HOME/.config/geeratya/dev-site.env}" && dotnet test --filter "Live=true"`. Live scripts in `scripts/` load it themselves when the variables are missing.
- Dev site: `GCM` (company-managed, board 4) and `GTM` (team-managed, board 3), both Kanban with a backlog. `GEERATYA_LIVE_PROJECT` is `GCM`; tests that need the team-managed kind use `GTM`. `GCS` (company-managed Scrum, board 6) and `GTS` (team-managed Scrum, board 5) are the Scrum pair, each with one future sprint and no issues until the seed script covers them (M3). All four are sandboxes under the write rule below.
- Live tests (`[LiveFact]` in `tests/Geeratya.Jira.Tests/Live`: skips without the env, the transport refuses a host outside the allowlist; the site is the first host of `GEERATYA_LIVE_HOSTS`; a shell with the variables exported also runs it on a plain `dotnet test`, it is read only) run with `dotnet test --filter "Live=true"`, only when the issue needs it, and are never part of the review or CI gate.
- Writes on the dev site only inside `GCM`, `GTM`, `GCS` and `GTS`. Every issue a test or agent creates carries the label `geeratya-test-<run id>` and is deleted at the end of the run; never edit or delete data you did not create. No project, user, workflow, scheme or permission changes.
- Seeding: `scripts/seed-dev-site.sh` (`--dry-run` prints the plan, `--project GCM|GTM`) find-or-creates 16 issues per project (epics, stories, tasks, bugs, sub-tasks, backlog items, a rich ADF comment, an attachment, links, a remote link), all labelled `geeratya-seed` plus a unique `geeratya-seed-<project>-<slug>`; running it twice adds nothing (a re-run also moves an issue stuck in To Do into its column; a 429 waits `Retry-After`; a failed request prints Jira's error body). `--reset` lists the labelled issues, `--reset --yes` deletes only those (the dev account may lack delete rights, #19). Linux, macOS and WSL only (bash, curl, jq, python3); the final counts wait for the search index.

## Driving the real binary (tmux)

The agent can't see a terminal, so run the app against `tools/FakeJira` in a detached tmux session and assert on its text:

# seed $XDG_CONFIG_HOME/geeratya/profiles.json with one active profile whose siteUrl is the fake's address, export GEERATYA_TOKEN=fake (as scripts/screenshot.sh does)
tmux -L geer -f /dev/null new-session -d -s gt -x 120 -y 40 'path/to/geeratya'
tmux -L geer -f /dev/null new-session -d -s gt -x 120 -y 40 'path/to/geeratya --profile <fake profile>'
tmux -L geer send-keys -t gt j j Enter
tmux -L geer capture-pane -t gt -p        # rendered frame as text
tmux -L geer kill-server
```

- Use a private server (`-L <name> -f /dev/null`) and `kill-server` in a trap; `set -g remain-on-exit on` keeps the pane after a short-lived binary exits.
- Set `XDG_CONFIG_HOME`, `XDG_STATE_HOME` and `XDG_CACHE_HOME` to a temp dir so the owner's real profiles, tokens and cache are never read.
- Wait for text (`grep -F` on the capture) or for the frame to stop changing, never fixed sleeps. `capture-pane -p -e` keeps escape sequences (to see highlight-only changes). `send-keys -N 200 j` repeats a key; send `g g` as two keys.
- Windows: psmux (`winget install marlocarlo.psmux`) accepts the same tmux syntax; send each key of a chord in its own call. Only for something Windows-specific.

## Running

Report real output, including failures. Benchmarks (BenchmarkDotNet) are not part of the normal run.
