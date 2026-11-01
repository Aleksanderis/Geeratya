# Testing

Strategy and layers. The procedure for writing, recording and running tests is the `testing` skill; budgets and measured numbers are in `docs/performance.md`.

## Layers

| Layer | What it covers | Where |
|---|---|---|
| Pure tests | State, commands, keymap, JQL building, mappers over recorded JSON, layout | `tests/Geeratya.<Project>.Tests`, no terminal, no socket |
| Transport and store tests | Jira calls, paging, refresh, caches against the in-process fake | `FakeJiraHandler` in `tests/Geeratya.Jira.Tests/Fakes` |
| Frame snapshots | Rendering as text at several sizes | Tui tests |
| Fake Jira server | The real binary against recorded data (tmux, screenshots, measurements) | `tools/FakeJira` |
| Live tests | Behaviour the fake might get wrong, on the dev site only | Opt-in, `Live=true` |

xUnit throughout, one test project per source project, same folder layout as the code. `[Trait("Os", "Independent")]` marks pure classes the Windows leg runs (`testing` skill). `tools/FakeJira` has its own test project, `tests/Geeratya.FakeJira.Tests`, which starts the server on a free loopback port and talks to it with a real `HttpClient`, so those tests are not marked OS independent.

## Fixtures

Real responses recorded from the dev site, scrubbed and stored under `tests/Geeratya.Jira.Tests/Fixtures/<area>/<name>.json`. What each one shows, and how Jira behaves, is the survey in `docs/jira-layer.md`. Recording (`scripts/record-fixture.sh`) refuses hosts outside `GEERATYA_LIVE_HOSTS`; scrubbing (`scripts/scrub-fixture.py`) rewrites host, account ids, emails and display names. Never hand-write a response shape you have not recorded. Generated datasets (below) are the one exception, and only for size: their shape is the recorded search shape reduced to the fields the app asks for.

## Fake Jira

Two fakes share the fixture folder, for different consumers:

- `FakeJiraHandler`: an `HttpMessageHandler` for unit and store tests. No socket. Scripted statuses, `Retry-After`, latency and hangs; records every request.
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

Without `--issues`, search serves the recorded three-page fixtures (`search-page1..3-last`). Routes are a fixed table in `FakeJiraRouter` over recorded fixtures: `myself`, `project/search`, `field`, `issuetype`, `filter/my`, `filter/search`, `search/jql` (GET and POST), the agile board list, boards 3 (GTM) and 4 (GCM) with configuration, and board 4 backlog and issues. Anything else is the recorded 404. It ignores credentials and never writes. Add a route when a feature needs one: map the path to a recorded fixture in `FakeJiraRouter`. Point the app at it with a profile whose site URL is the printed address and any token.

`fakejira` is Native AOT compatible (`PublishAot`, `HttpListener`, `Utf8JsonWriter`, no reflection) and follows the same style rules as `src/`.

## Live tests

Only on the owner's dev site, opt-in, never part of the review or CI gate; rules and environment in the `testing` skill.

## Measurement harness

`scripts/measure.sh <binary> <fake url> [runs]` drives the Native AOT binary in a private tmux server with private `XDG_*` directories, so no real profile, token or cache is read. Contract:

- Input: the binary, the base URL of a running FakeJira (exported to the binary as `GEERATYA_FAKE_URL`), the number of sessions (default 20; `KEYS` presses per session, default 5; `TIMEOUT_MS`, default 5000).
- Output: median and p95 in milliseconds for the tmux floor (a trivial command), cold start to first frame and key to frame. Exit 2 for bad arguments, 1 when a frame never appears.
- Needs tmux and GNU `date`: Linux first (macOS needs GNU coreutils; Windows has no equivalent yet). It does not contact the fake URL itself; until the Jira-backed views exist the shell does not either.
- It waits for the frame to appear or change; it never sleeps a fixed time.
- Numbers go in `docs/performance.md` with environment, dataset and latency; an optimisation PR quotes before and after.

For screenshots and manual tmux runs use the same pattern (`testing` skill, "Driving the real binary").

## Screenshot pipeline

`scripts/screenshot.sh render|publish|clean` (skill `screenshots`) captures the real binary against `tools/FakeJira` (generated issues, `--issues 40`; never a real site) in a private tmux server with private `XDG_*` directories. `tools/AnsiToSvg` (tests in `tests/Geeratya.AnsiToSvg.Tests`) turns the `capture-pane -p -e` text (SGR colours: 16, 256 and 24-bit; bold, dim, italic, underline, reverse; wide characters) into an SVG, `rsvg-convert` into a PNG.

- Scenarios are rows of one table in the script (`name:columns:rows:theme:keys`): `default` and `help` 120x36, `narrow` 64x30, `light` (the SVG's default colours are light; the app has no light palette yet, so only unstyled cells change). Add a row per new screen.
- Needs bash, tmux, rsvg-convert, dotnet (and `gh`, `git` for publish and clean); a missing tool exits 3 naming it, an uncloneable screenshots repository exits 4. Linux first.
- It waits until two consecutive captures are equal (never a fixed sleep for the frame); the key delay between scripted key presses is 0.2 s.
