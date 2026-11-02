# Testing

Strategy and layers. The procedure for writing, recording and running tests is the `testing` skill; budgets and measured numbers are in `docs/performance.md`.

## Layers

| Layer | What it covers | Where |
|---|---|---|
| Pure tests | State, commands, keymap, JQL building, mappers over recorded JSON, layout | `tests/Geeratya.<Project>.Tests`, no terminal, no socket |
| Transport and store tests | Jira calls, paging, refresh, caches against the in-process fake | `FakeJiraHandler` in `tests/Geeratya.Jira.Tests/Fakes` |
| Frame snapshots | Rendering as text at several sizes (help also scrolled, `--help-scroll`) | Tui tests |
| Fake Jira server | The real binary against recorded data (tmux, screenshots, measurements) | `tools/FakeJira` |
| Live tests | Behaviour the fake might get wrong, on the dev site only | Opt-in, `Live=true` |

xUnit throughout, one test project per source project, same folder layout as the code. `[Trait("Os", "Independent")]` marks pure classes the Windows leg runs (`testing` skill). `tools/FakeJira` has its own test project, `tests/Geeratya.FakeJira.Tests`, which starts the server on a free loopback port and talks to it with a real `HttpClient`, so those tests are not marked OS independent.

## Fixtures

Real responses recorded from the dev site, scrubbed and stored under `tests/Geeratya.Jira.Tests/Fixtures/<area>/<name>.json`. What each one shows, and how Jira behaves, is the survey in `docs/jira-layer.md`. Recording (`scripts/record-fixture.sh`) refuses hosts outside `GEERATYA_LIVE_HOSTS`; scrubbing (`scripts/scrub-fixture.py`) rewrites host, account ids, emails and display names. Never hand-write a response shape you have not recorded. Generated datasets (below) are the one exception, and only for size: their shape is the recorded search shape reduced to the fields the app asks for.

## Fake Jira

Two fakes share the fixture folder, for different consumers:

- `FakeJiraHandler`: an `HttpMessageHandler` for unit and store tests. No socket. Scripted statuses, `Retry-After`, latency and hangs; records every request.
- `ScriptedJiraHandler` (`tests/Geeratya.Tui.Tests/Fakes`): the same idea for the Tui tests, which do not reference the Jira test project: a queue of scripted replies (status, body, optional gate, `Retry-After`) and the recorded request URIs. Bodies are small inline JSON; recorded responses belong to the Jira layer's tests.
- `tools/FakeJira` (`fakejira`): a local HTTP server on `127.0.0.1` for running the real binary. Prints its base URL as the first line on standard output and runs until interrupted.

```sh
dotnet run --project tools/FakeJira -- --issues 10000 --latency-ms 50 --rate-limit-every 20
# or the AOT build: dotnet publish tools/FakeJira -c Release -o <dir>; <dir>/fakejira
```

| Option | Meaning |
|---|---|
| `--fixtures <dir>` | Fixture folder; default found by walking up from the binary or the current directory to `tests/Geeratya.Jira.Tests/Fixtures` |
| `--port <n>` | Listen port; default 0 picks a free one (probe-then-bind, retried on a race) |
| `--issues <n>` | Search serves `GEN-1..GEN-n` generated issues, paged with `maxResults` (default 50, cap 5000) and `nextPageToken`; `approximate-count` answers `n` |
| `--latency-ms <n>` | Delay added to every reply |
| `--rate-limit-every <n>` | Every nth request is answered `429` with `Retry-After: 1` |

Without `--issues`, search serves the recorded three-page fixtures (`search-page1..3-last`). Routes are a fixed table in `FakeJiraRouter` over recorded fixtures: `myself`, `project/search`, `field`, `issuetype`, `filter/my`, `filter/search`, `filter/favourite`, `search/jql` (GET and POST), the agile board list, boards 3 (GTM) and 4 (GCM) with configuration and `epic`, board 3 and 4 `issue` and `backlog` (board 3 from the `hierarchy/` fixtures), `issue/GTM-9` and `issue/GCM-8` (epics) whose `parent = KEY` search answers the `hierarchy/search-epic-children-*` fixtures, and `filter/10001` (any other numeric filter id answers the recorded 404 "not available"). Anything else (including a POST to any path except `search/jql` and, with `--issues`, `search/approximate-count`) is the recorded 404. It ignores credentials and never writes. Add a route when a feature needs one: map the path to a recorded fixture in `FakeJiraRouter`. Point the app at it with a profile whose site URL is the printed address and any token.

`fakejira` is Native AOT compatible (`PublishAot`, `HttpListener`, `Utf8JsonWriter`, no reflection) and follows the same style rules as `src/`.

## Live tests

Only on the owner's dev site, opt-in, never part of the review or CI gate; rules and environment in the `testing` skill.

## Measurement harness

`scripts/measure.sh <binary> <fake url> [runs]` drives the Native AOT binary in a private tmux server with private `XDG_*` directories, so no real profile, token or cache is read. Contract:

- Input: the binary, the base URL of a running FakeJira (written as the site of one active `fake` profile into the private `geeratya/profiles.json`; `GEERATYA_TOKEN=fake` is the token, so the binary talks to the fake and must be a loopback address), the number of sessions (default 20; `KEYS` presses per session, default 5; `TIMEOUT_MS`, default 5000).
- Output: median and p95 in milliseconds for the tmux floor (a trivial command), cold start to first frame and key to frame. Exit 2 for bad arguments, 1 when a frame never appears.
- Needs tmux and GNU `date`: Linux first (macOS needs GNU coreutils; Windows has no equivalent yet).
- It waits for the frame to appear or change; it never sleeps a fixed time (two bounded exceptions in `SCENARIO=views`: 0.5 s before Enter on a fresh list, and 2 s for the debounced cache write before the first session is killed).
- `SCENARIO=list` (open a list, `j` and `k` key to frame, 10 000 rows paged in with `G`, RSS; `ROWS`, `JQL`) and `SCENARIO=views` (issue detail, second start from the disk cache, 10 tabs, `Tab` key, RSS) time a loaded view instead of the empty shell. They need a fake started with `--issues 10000` (and `--fixtures`, see above) and print their own table; the `?` figures are not printed then. A wait that times out prints the last frame lines to stderr.
- `tests/Geeratya.Tui.Tests/Performance/HotPathMeasurements.cs` times the in-process paths (list and board keys at 10 000 rows, column regrouping) when `GEERATYA_BENCH=1`; it is a no-op otherwise.
- Numbers go in `docs/performance.md` with environment, dataset and latency; an optimisation PR quotes before and after.

For screenshots and manual tmux runs use the same pattern (`testing` skill, "Driving the real binary").

## Open-in-browser smoke

`scripts/smoke-browser.sh --binary <geeratya>` runs the real binary against the fake Jira with an empty `PATH` directory and no `BROWSER` (no opener can run), presses `<space>o` on a list row and again in the issue detail, and checks the status line. The fake is served over http, which the https-only gate (`BrowserUrlCheck`) refuses before any opener runs, so the checked message is `Cannot open the link: only https links can be opened`; the launcher's own "no browser opener found" message is covered by the Platform tests. Linux only. `TIMEOUT_MS` bounds each wait. Run it by hand after changing the browser path (nothing runs it automatically).

## Screenshot pipeline

`scripts/screenshot.sh render|publish|clean` (skill `screenshots`) captures the real binary against `tools/FakeJira` (generated issues, `--issues 40`; never a real site) in a private tmux server with private `XDG_*` directories. `tools/AnsiToSvg` (tests in `tests/Geeratya.AnsiToSvg.Tests`) turns the `capture-pane -p -e` text (SGR colours: 16, 256 and 24-bit; bold, dim, italic, underline, reverse; wide characters) into an SVG, `rsvg-convert` into a PNG.

- Scenarios are rows of one table in the script (`name;columns;rows;theme;keys;wait-for`, `;` separated because keys hold `:`, and `|` in the keys sends Enter; `wait-for` is text that must be on screen after the keys, because data loads asynchronously and the settle check alone only compares two captures 0.15 s apart): `default` and `help` 120x36, `narrow` 64x30, `list` 120x36 and `list-narrow` 64x24 (a `:jql` view against the generated issues), `detail` 120x36 and `detail-narrow` 64x24 (Enter on `GEN-5`, whose generated detail has a formatted description and 120 comments; the fake serves `GEN-n` issues, comments and an empty remote link list in generated mode), `light` (the SVG's default colours are light; the app has no light palette yet, so only unstyled cells change). Add a row per new screen.
- Needs bash, tmux, rsvg-convert, dotnet (and `gh`, `git` for publish and clean); a missing tool exits 3 naming it, an uncloneable screenshots repository exits 4. Linux only (other systems exit 3).
- The script seeds the private config directory with a `profiles.json` holding one active profile (`fake`, `fake@example.com`, site URL = the fake's loopback address) and exports `GEERATYA_TOKEN=fake` (the single-profile fallback), so the binary reads the fake without a hidden hook; before any key the script waits for the profile line `fake: fake@example.com on ...`, which is also the smoke check that the binary started against the fake profile (`scripts/screenshot.sh render --binary <geeratya> --scenarios default`).
- It waits until two consecutive captures are equal (never a fixed sleep for the frame); the key delay between scripted key presses is 0.2 s.
