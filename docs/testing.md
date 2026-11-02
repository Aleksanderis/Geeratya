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

To regenerate snapshots after adding key rows or palette commands, run `GEERATYA_UPDATE_SNAPSHOTS=1 dotnet test tests/Geeratya.Tui.Tests`. The switch is project-wide for the families that honour it: `help-scroll*`, `popup-*`, `network-*` and the issue detail snapshots. Those tests then write the current output into `tests/Geeratya.Tui.Tests/Snapshots/` instead of asserting; review the diff and commit it. Unset, they assert as usual.

## Fixtures

Real responses recorded from the dev site, scrubbed and stored under `tests/Geeratya.Jira.Tests/Fixtures/<area>/<name>.json`. What each one shows, and how Jira behaves, is the survey in `docs/jira-layer.md`. Recording (`scripts/record-fixture.sh`) refuses hosts outside `GEERATYA_LIVE_HOSTS`; scrubbing (`scripts/scrub-fixture.py`) rewrites host, account ids, emails and display names. Never hand-write a response shape you have not recorded. Generated datasets (below) are the one exception, and only for size: their shape is the recorded search shape reduced to the fields the app asks for.

## Fake Jira

Two fakes share the fixture folder, for different consumers:

- `FakeJiraHandler`: an `HttpMessageHandler` for unit and store tests. No socket. Scripted statuses, `Retry-After`, latency and hangs; records every request.
- `ScriptedJiraHandler` (`tests/Geeratya.App.Tests/Fakes`): the same idea for the App tests (handlers, runner, connections), which do not reference the Jira test project; `Geeratya.Tui.Tests` links every file of that `Fakes` folder (so a fake added there is compiled into both test assemblies) to build board states through the real loading path: a queue of scripted replies (status, body, optional gate, `Retry-After`) and the recorded request URIs. Bodies are small inline JSON; recorded responses belong to the Jira layer's tests.
- `tools/FakeJira` (`fakejira`): a local HTTP server on `127.0.0.1` for running the real binary. Prints its base URL as the first line on standard output and runs until interrupted. It serves `GET issue/{key}/editmeta` from the recorded edit metadata of the key's project kind (GTM team-managed, otherwise GCM) for any key, and edits only the recorded issues (a generated `GEN-n` answers 404 on a write).

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
| `--drop-write-every <n>` | Every nth write (POST, PUT, DELETE) is applied, then the connection is cut after the headers, so the client fails reading the reply and cannot tell whether the write happened (the "unknown outcome" path) |

Without `--issues`, search serves the recorded three-page fixtures (`search-page1..3-last`). Routes are a fixed table in `FakeJiraRouter` over recorded fixtures: `myself`, `project/search`, `field`, `issuetype`, `filter/my`, `filter/search`, `filter/favourite`, `search/jql` (GET and POST), the agile board list, boards 3 (GTM) and 4 (GCM) with configuration and `epic`, board 3 and 4 `issue` and `backlog` (board 3 from the `hierarchy/` fixtures), `issue/GTM-9` and `issue/GCM-8` (epics) whose `parent = KEY` search answers the `hierarchy/search-epic-children-*` fixtures, a `key = KEY` (or `issuekey`) search of any routed issue, which answers one page holding that recorded issue (also with `--issues`; there `parent = KEY` of an epic likewise answers the recorded children), and `filter/10001` (any other numeric filter id answers the recorded 404 "not available"). Anything else (including a POST to any path except `search/jql`, with `--issues` `search/approximate-count`, and the write routes below) is the recorded 404. It ignores credentials. Add a route when a feature needs one: map the path to a recorded fixture in `FakeJiraRouter`. Point the app at it with a profile whose site URL is the printed address and any token.

Write routes (`FakeJiraWriteStore`) keep their state in memory for the life of the process and never touch Jira: `POST /issue` (create in `GCM` or `GTM`; keys start at `-100`; no summary, an unknown project or no issue type answer the recorded 400s), `PUT /issue/{key}` (edit `summary`, `labels`, `description`, `duedate`; any other field is the recorded unknown-field 400), `DELETE /issue/{key}` (a created issue is removed, a recorded one answers the recorded 403, as `GCM` does), `GET` and `POST /issue/{key}/transitions` (the recorded project transition lists; a valid id changes the status the issue reads with, a bad id is the recorded 400), `POST`, `PUT` and `DELETE` on `/issue/{key}/comment[/{id}]` (the comments list follows), and `PUT /issue/{key}/assignee` (an account id assigns, the recorded account shows its display name, null or `-1` unassigns; #154), and `PUT /rest/agile/1.0/issue/rank` (204 when every named issue exists or is served by a board or backlog list, else the recorded 400; the moves are laid over the `issues` of the board and backlog replies, so a reload shows the new order, within a page for the paged GCM board; #169). Reads of an edited or created issue (`GET issue/{key}`, the `key = KEY` search, comments) show the changes; a restart forgets them. Only `fields` edits are modelled (an `update` body answers the unknown-field 400), and a recorded comment can be edited or deleted too (the change is a copy kept in memory; an unknown id answers 404). A created issue is readable by key but never appears in `search/jql` (apart from `key = KEY`), board, backlog or epic-children replies. Not modelled: the rank field, create/edit metadata changes, links, attachments and the assignable-user search (#213).

Scrum routes (`FakeScrumStore`, #173) serve the survey fixtures of #172 (`Fixtures/scrum/`) for board 5 (`GTS`, team-managed, says `simple`) and board 6 (`GCS`, company-managed, says `scrum`), with state in memory like the write routes. Reads: `GET board` (all four boards, `projectKeyOrId` and `type` narrow it), `board/{5,6}` with `configuration` and `epic`, `board/{5,6}/sprint` (closed, active, future order; `state` filter, an unknown value is the recorded 400; offset paging with `isLast`, cap 50), `board/{5,6}/issue` (every board issue; the JQL forms `sprint in openSprints()` and `sprint = N` narrow it, any other JQL is ignored), `board/{5,6}/backlog` (issues in no open sprint that are not done, which matches both recorded backlogs, so unfinished issues of a completed sprint come back to it), `sprint/{id}` and `sprint/{id}/issue` (offset paged, no `isLast`). Sprint membership is seeded from the `Sprint` field of the board issues, except an issue the recorded backlog lists (`GTS-35`), which stays in the backlog as on the real site; the `Sprint` field of every issue in a reply is rebuilt from the current membership (closed sprints first, `null` when none). `GET board/{3,4}/sprint` is the recorded 400 `The board does not support sprints`, an unknown board the recorded 404. Writes: `POST sprint` (create, state `future`, boards 5 and 6 only, ids from 1000), `PUT sprint/{id}` (replace: an omitted `goal` is dropped) and `POST sprint/{id}` (partial), with the recorded rules: start needs a start and an end date, future to closed and closed to active are 400, completing a closed sprint again is 200, and `moveIssuesTo` is 400 `Invalid request payload`, so completing leaves unfinished issues in the closed sprint (and in the backlog) and a client moves them with a second call; `DELETE sprint/{id}` (204, any state); `POST sprint/{id}/issue` (an issue in another open sprint moves, a closed target or an unknown key is 400 and nothing moves); `POST backlog/issue` (leaves every open sprint). Not modelled: issue detail (`GET issue/GCS-n`) and the `key = GCS-n` search of `GCS` and `GTS` issues, which answer 404, so a card opens to the not-found panel (#323); `--issues` generated sprints (#320), the `fields` parameter (replies carry the recorded fields), transitions and edits of `GCS` and `GTS` issues, sub-tasks following their parent into a sprint, ranking across sprints, quick filters. The app opens both boards with `:board GCS` and `:board GTS` (the Kanban layout; the Scrum layout is M4).

`fakejira` is Native AOT compatible (`PublishAot`, `HttpListener`, `Utf8JsonWriter`, no reflection) and follows the same style rules as `src/`.

## Write fixtures

`Fixtures/writes/` holds the replies of the write calls that were not recorded elsewhere, recorded from `GTM` with `scripts/record-fixture.sh` (which now also allows `PUT`/`DELETE` of one comment by id and `PUT` of `/assignee`). Every issue the recording made carried `geeratya-test-156` and was deleted at the end (`GTM` only; `GCM` rejects deletes, issue #19). A `.meta.txt` file holds the status line and rate-limit headers of an empty reply (204 has no body to store):

| File | What it shows |
|---|---|
| `create-gtm-task-reply.json` | 201 on create: `id`, `key`, `self` |
| `create-400-no-summary.json` | 400 for a create without `summary`: the message in both `errorMessages` and `errors.summary` |
| `../create/synthetic-issuetypes-custom.json`, `../create/synthetic-fields-custom.json`, `../create/synthetic-create-400-required-custom.json`, `../create/synthetic-create-400-invalid-parent.json` | **Synthetic, not recorded** (folder `create/`, #160). A custom level-2 issue type, a renamed sub-task type, an entry without id, a required custom select with odd allowed values, an unknown field type, and the 400 bodies for a missing required custom field and an invalid parent (message invented, shape follows the recorded field-error body). |
| `comment-post-gtm-reply.json`, `comment-put-gtm-reply.json` | 201 and 200 with the whole comment (ADF body, author, timestamps) |
| `comment-delete-gtm-reply.meta.txt`, `issue-put-gtm-reply.meta.txt`, `transition-post-gtm-reply.meta.txt`, `rank-put-gtm-reply.meta.txt`, `issue-delete-gtm-reply.meta.txt`, `issue-delete-subtasks-gtm-reply.meta.txt` | 204, no body |
| `assignee-404-unknown-user.json` | 404 for `PUT /assignee` with an account id that does not exist (not 400). The 400 and 403 variants stay synthetic inline bodies (#268) |
| `synthetic-transitions-required-field.json`, `synthetic-transition-400-required-field.json` | **Synthetic, not recorded.** A required `resolution` with other choices, and a 400 keyed by field id, which the real `Done` screen does not give (its 400 is `transitions/transition-400-resolution-required.json`, message only). Kept for cases the dev site cannot show; the shapes follow the recorded create-metadata field shape |
| `transitions/transitions-gcm-5-resolution-components.json`, `transitions/transition-done-with-fields-reply.meta.txt` | Recorded (#331). The `GCM` workflow is no longer all to all: `Done` (id `41`) has a screen with a required `resolution` and a `components` multi-select (one value, `Sandbox core`); the other transitions and all of `GTM` have no screen. The 204 reply to `Done` with both fields. `LiveTransitionFieldsTests` runs it live; `GCM` refuses deletes (#19) and is the only project with this screen, so each run leaves one `geeratya-test-<run id>` issue there, an exception to the delete-at-end rule |

The other write replies sit with their area: transitions (`transitions/`), create metadata and edit metadata (`metadata/`), create replies and delete errors (`hierarchy/`, `errors/`), rank (`boards/`), comment posts with ADF (`adf/`), edit 400s (`adf/edit-400-*`). The 403 for a missing permission is `errors/issue-delete-403-no-permission.json`; a missing-permission edit could not be reproduced with the sandbox account, which owns the projects.

## Live tests

Only on the owner's dev site, opt-in, never part of the review or CI gate; rules and environment in the `testing` skill. `LiveWriteRoundTripTests` creates a `GTM` task labelled `geeratya-test-<run id>`, edits it, adds, edits and deletes a comment, transitions it, reads the result back and deletes the issue in a `finally`; it skips without the environment.

## Seeded dev-site data

`scripts/seed-dev-site.sh` (details in the `testing` skill) fills the four sandbox projects; every issue carries `geeratya-seed` plus `geeratya-seed-<project>-<slug>`, and a second run adds nothing.

- `GCM`, `GTM` (Kanban): 16 issues each (epics, stories, tasks, bugs, sub-tasks and backlog items; `--dry-run` lists them), a rich comment, an attachment, links and a remote link.
- `GCS` (company-managed, board 6) and `GTS` (team-managed, board 5): 21 issues each. 2 epics, 7 stories, 6 tasks, 4 bugs and 2 sub-tasks, 4 of them left in the backlog, spread over three sprints found by name: `Seed Sprint 1` (closed, 7 issues: 5 done, `story-search` and `task-tests` not finished and carried over), `Seed Sprint 2` (active, 7 issues: the 2 carried over plus 5 more), `Seed Sprint 3` (future, 3 issues). The pre-existing `GCS Sprint 1`/`GTS Sprint 1` stay untouched. Statuses end as 7 Done, 4 In Progress, 10 To Do.
- Story points come from the board's estimation field (`customfield_10047` on GCS, `customfield_10016` on GTS), read from the board configuration. On `GCS` that field is on the screens of Stories only (Jira drops the value for tasks, bugs and sub-tasks), so only the 7 stories carry points there; on `GTS` every issue in the spec with an estimate has one.
- `--reset --yes` deletes the labelled issues and the `Seed Sprint ...` sprints (sprint writes are allowed only on boards 5 and 6).

## Measurement harness

`scripts/measure.sh <binary> <fake url> [runs]` drives the Native AOT binary in a private tmux server with private `XDG_*` directories, so no real profile, token or cache is read. Contract:

- Input: the binary, the base URL of a running FakeJira (written as the site of one active `fake` profile into the private `geeratya/profiles.json`; `GEERATYA_TOKEN=fake` is the token, so the binary talks to the fake and must be a loopback address), the number of sessions (default 20; `KEYS` presses per session, default 5; `TIMEOUT_MS`, default 5000).
- Output: median and p95 in milliseconds for the tmux floor (a trivial command), cold start to first frame and key to frame. Exit 2 for bad arguments, 1 when a frame never appears.
- Needs tmux and GNU `date`: Linux first (macOS needs GNU coreutils; Windows has no equivalent yet).
- It waits for the frame to appear or change; it never sleeps a fixed time (bounded exceptions in the scenarios: 0.5 s before Enter on a fresh list, 2 s for the debounced cache write before the first session is killed, 1 s before the RSS read, and up to 2 s of settling after paging in `SCENARIO=list`).
- `SCENARIO=list` (open a list, `j` and `k` key to frame, 10 000 rows paged in with `G`, RSS; `ROWS`, `JQL`) and `SCENARIO=views` (issue detail, second start from the disk cache, 10 tabs, `Tab` key, RSS) time a loaded view instead of the empty shell. They need a fake started with `--issues 10000` (and `--fixtures`, see above) and print their own table; the `?` figures are not printed then. A wait that times out prints the last frame lines to stderr.
- `tests/Geeratya.Tui.Tests/Performance/HotPathMeasurements.cs` times the in-process paths (list and board keys at 10 000 rows, column regrouping) when `GEERATYA_BENCH=1`; it is skipped otherwise.
- Numbers go in `docs/performance.md` with environment, dataset and latency; an optimisation PR quotes before and after.

For screenshots and manual tmux runs use the same pattern (`testing` skill, "Driving the real binary").

## Open-in-browser smoke

`scripts/smoke-browser.sh --binary <geeratya>` runs the real binary against the fake Jira with an empty `PATH` directory and no `BROWSER` (no opener can run), presses `<space>o` on a list row and again in the issue detail, and checks the status line. The fake is served over http, which the https-only gate (`BrowserUrlCheck`) refuses before any opener runs, so the first checked message is `Cannot open the link: only https links can be opened`. Then it opens `GEN-2`, whose remote link (the fake serves one https link for that generated issue) passes the gate for any host, and presses `Enter` and `<space>o` on it: the real launcher finds no `xdg-open` on the empty `PATH` and the status line says `Cannot open the link: no browser opener found: copy the URL`. Linux only. `TIMEOUT_MS` bounds each wait. Run it by hand after changing the browser path (nothing runs it automatically).

`scripts/smoke-mouse-overlays.sh --binary <geeratya>` sends SGR mouse press and release sequences (`send-keys -l`) to the real binary against the fake Jira: source picker, create form, jump prompt, field form suggestion, comment editor mention popup and `:profile list` (click and right-click). Linux only; `TIMEOUT_MS` bounds each wait, `DUMP=1` prints the frames. Run it by hand after changing the overlay mouse paths.

## Screenshot pipeline

`scripts/screenshot.sh render|publish|clean` (skill `screenshots`) captures the real binary against `tools/FakeJira` (generated issues, `--issues 40`; never a real site). The `comment-*` scenarios use the generated one (editor, delete question) or, as `comment-recorded-*`, the recorded one (a saved comment on GTM-11, the edit warning on its rich comment): the fake keeps added comments in memory, so edit and delete work on added comments and on recorded ones (kept as an in-memory copy); `~` in a scenario's keys sends `Ctrl-s`. The `detail-relations-*` scenarios use a second fake on the recorded fixtures instead and open one issue with `:jql key = KEY` (GTM-11 and GCM-10 for sub-tasks, links, remote links and attachments; the epics GTM-9 and GCM-8 for epic children, since no recorded issue has all five) in a private tmux server with private `XDG_*` directories. `tools/AnsiToSvg` (tests in `tests/Geeratya.AnsiToSvg.Tests`) turns the `capture-pane -p -e` text (SGR colours: 16, 256 and 24-bit; bold, dim, italic, underline, reverse; wide characters) into an SVG, `rsvg-convert` into a PNG.

- Scenarios are rows of one table in the script (`name;columns;rows;theme;keys;wait-for`, `;` separated because keys hold `:`, and `|` in the keys sends Enter; `wait-for` is text that must be on screen after the keys, because data loads asynchronously and the settle check alone only compares two captures 0.15 s apart): `default` and `help` 120x36, `narrow` 64x30, `list` 120x36 and `list-narrow` 64x24 (a `:jql` view against the generated issues), `detail` 120x36 and `detail-narrow` 64x24 (Enter on `GEN-5`, whose generated detail has a formatted description and 120 comments; the fake serves `GEN-n` issues, comments and an empty remote link list in generated mode), `light` (the SVG's default colours are light; the app has no light palette yet, so only unstyled cells change). Add a row per new screen.
- Needs bash, tmux, rsvg-convert, dotnet (and `gh`, `git` for publish and clean); a missing tool exits 3 naming it, an uncloneable screenshots repository exits 4. Linux only (other systems exit 3).
- The script seeds the private config directory with a `profiles.json` holding one active profile (`fake`, `fake@example.com`, site URL = the fake's loopback address) and exports `GEERATYA_TOKEN=fake` (the single-profile fallback), so the binary reads the fake without a hidden hook; before any key the script waits for the profile line `fake: fake@example.com on ...`, which is also the smoke check that the binary started against the fake profile (`scripts/screenshot.sh render --binary <geeratya> --scenarios default`).
- It waits until two consecutive captures are equal (never a fixed sleep for the frame); the key delay between scripted key presses is 0.2 s.
