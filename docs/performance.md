# Performance

Budgets and measured numbers. The checklist for writing fast code is the `performance` skill; the harness is `scripts/measure.sh` (see `docs/testing.md`, "Measurement harness"). Measure the Native AOT binary against `tools/FakeJira`, never `dotnet run` and never the dev site.

## Budgets

| Budget | Target |
|---|---|
| Cold start to first frame | Under 100 ms, from local cache or an empty state; never waits on the network |
| Key press to frame | Never waits on a request; a local action (open help, move selection) is drawn in the next frame |
| Scrolling a 10k-issue result list | No dropped frames; work proportional to the visible rows |
| Background tabs | Cheap: no polling faster than the refresh rule, no rendering |
| Memory | Bounded however long the session runs; every cache has a size bound and an owner that drops it |
| Opening a cached view | Cached rows drawn without waiting for the network; the cache read is off the UI thread and after the first frame (met: 82 ms median to first rows from the cache against 162 without, see "M1 measurements (#91)") |
| Refresh tick | An idle `Tick` returns the same state (no redraw unless time-dependent text is visible) and costs a comparison; only the active tab refreshes |

Bounds from the M1 design (`docs/architecture.md`, "Views and tabs", "Cache and refresh"): 10 000 rows per tab, 50 000 across tabs, 32 cached views and 200 cached issue details in memory, 20 MB of cache per profile on disk. The first measurements of these are in "M1 measurements (#91)" below.

## How numbers are taken

`scripts/measure.sh <binary> <fake url> [runs]` starts the binary in a private tmux server (private `XDG_*` directories), polls `capture-pane` until the first non-blank frame, then toggles the help overlay with `?` and times each change of the frame. It first times a trivial command (`printf x`) the same way, the "tmux floor", so the cost of tmux and of the polling loop can be read off. Results are the median and p95 over all runs, printed with the load average before and after (Linux only: `/proc/loadavg`); do not trust a run when the load is near the core count. Start FakeJira beside it with the dataset and latency you want to record (`fakejira --issues 10000 --latency-ms 50`). The script seeds an active fake profile for the binary (`docs/testing.md`).

The harness measures from outside the process, so it includes process start, the first terminal write and tmux's own capture latency (floor below). Treat differences between runs as signal and absolute values as an upper bound.

## Measured

Environment: Linux x86_64, 14 cores, Native AOT `geeratya` published with `dotnet publish src/Geeratya.Tui -c Release`, tmux 120x40, 30 sessions with 5 key presses each, empty shell (the binary does not talk to Jira yet). The harness prints the 1-minute load average before and after a run (a lagging hint, not an exact figure). The machine was never quiet during the re-measurement (load 9 to 13 on 14 cores, other agents building), so these are upper bounds.

| Measurement (ms) | Median | p95 | Budget |
|---|---|---|---|
| tmux floor (first frame of `printf x`) | 9 | 16 | n/a |
| Cold start to first frame, empty shell | 43 | 63 | under 100 |
| Key to frame (`?` help toggle) | 37 | 66 | next frame |

Theme `auto` (#375, default setting, 10 sessions, load 14): cold start to first frame median 39 / p95 50 ms with the OSC 11 query answered by tmux. A terminal that answers neither OSC 11 nor DA1 adds up to 50 ms (worst case, still under the 100 ms budget); one that answers only DA1 adds no wait. The Windows branch (no query, `COLORFGBG` only) has unit tests only, no real console run.

Superseded history (#7, first measurement, load 16): cold start median 124 / p95 173, key to frame 207 / 241; an earlier 10-session run gave cold start 91 / 185 and key to frame 207 / 253. Repeated runs for #37 (five 30-session runs on the same source, load 9 to 17) gave cold start 41 to 58 and key to frame 35 to 41, so the old figures were not reproduced. The cause of the difference is not established: heavier machine load during the first runs is the likely reason, but a quiet machine was not available, so that is not proven, and no code change was made. Re-run on a quiet machine when one is available; if the old figures return, reopen #37.

Reading the key-to-frame figure (ad hoc checks, not committed and not reproducible from the repo): a bare `cat` echoing a typed character, timed with the same loop, takes 10 ms median (p95 20), so about 25 ms of the 37 ms is above the harness floor. A temporary in-process trace (timestamps in the key handler and the `Iteration` event) showed `tmux send-keys` returning 10 to 20 ms before the key reached the process, and the frame visible to `capture-pane` about 15 ms after the key handler ran, mostly the polling loop's own `capture-pane` spawn. Terminal.Gui's main loop iterates about 25 times a second by default (`Application.MaximumIterationsPerSecond`, a static property), but a key press wakes it at once: setting it to 120 did not change the figure (alternating A/B runs, 35 to 41 ms both), so it is left at its default.

## Notes for later rows

Add a row per measured path with binary type, dataset size, injected latency, median and p95. The M1 rows are in "M1 measurements (#91)" at the end; the per-feature tables in between are the numbers taken while each feature was written.

### Local cache (#82)

| Measurement (ms) | Median | p95 | Budget |
|---|---|---|---|
| Cold start to first frame, Native AOT, with the cache wired in (20 sessions, load 11 on 14 cores) | 33 | 44 | under 100 |
| tmux floor | 3 | 14 | n/a |

Nothing is read from the cache before the first frame: the cache is opened lazily when a view is opened, so the start path is unchanged (before #82: 43 / 63 on an equally busy machine). The saving of a cache hit (rows drawn before the network answers) is tested with a gated fake reply (`ViewLoadHandlerCacheTests`), not timed; the end-to-end second-start figures are in "M1 measurements (#91)". Memory: at most 32 views times 100 rows in memory per profile (about 50 KB a view), 20 MB on disk; the RSS delta is not measured.

### List scrolling (#79)

| Path | Binary | Dataset | Median | p95 |
|---|---|---|---|---|
| Scrum backlog (#178): nav key through `AppReducer` plus `FrameBuilder.BuildStyled` at 120x36 (alternating `Ctrl-d` and `j`) | .NET 10 Release JIT, in process | 10 000 rows in 21 sections in one tab, 2500 timed keys (`HotPathMeasurements`) | 0.40 ms | 1.11 ms (load 18, see the note under the Scrum paths table) |
| Nav key through `AppReducer` plus `FrameBuilder.BuildStyled` at 120x36 (alternating `Ctrl-d` and `j`) | .NET 10 Release JIT, in process | 10 000 rows loaded in one tab, 2000 timed keys | 0.20 ms | 1.0 ms |

Method: temporary xUnit test (not committed), 200 warm-up keys. It excludes the terminal draw and tmux. The renderer produces only the viewport rows (a test builds a frame over 10 000 rows and checks it holds the window and nothing else); the work per key is the reducer's list copy of one tab and the visible rows. Real-binary check: `fakejira --issues 10000` with the Native AOT binary under tmux, `:jql`, then `j`, `G` and `Ctrl-d` moved the selection, the next page loaded when `G` came within one screen of the end, and `gg` returned to the top. RSS of a fully loaded 10k tab and AOT key-to-frame are in "M1 measurements (#91)".

### Columns layout (#85)

| Path | Binary | Dataset | Median | p95 |
|---|---|---|---|---|
| Nav key (`j`, `l`, `Ctrl-d`, `h`, `Ctrl-u`) through `AppReducer` plus `FrameBuilder.BuildStyled` at 120x36 | .NET 10 Release JIT, in process | board of 200 cards, 2500 timed keys | 0.31 ms | 0.56 ms |
| same | same | board of 2000 cards, 2500 timed keys | 0.29 ms | 0.59 ms |

Method: temporary xUnit test (not committed), 500 warm-up keys, four columns, assignee on every card; it excludes the terminal draw and tmux. The renderer draws only the cards in the viewport. Each key and each frame still regroup all rows into the columns (`BoardColumns.Visible`, one dictionary lookup per row; `Arrange` adds a pass over the rows looking for parents and, only when some row has one, a key set and a child map, all linear) and a key looks the selected issue up by key once (linear); that is below the noise up to 2000 cards, so the groups are not cached; boards up to the 10 000-row cap are measured in "M1 measurements (#91)" (about 1 ms regrouping with sub-tasks), and the table predates the border rework and the sub-task grouping (linear passes, not re-measured) (the per-frame cost is still bounded by the viewport). Not measured: Native AOT in-process figures.

### JSON mapping of a search result (#66)

| Path | Binary | Dataset | Median | p95 |
|---|---|---|---|---|
| `JiraSearchClient.SearchAsync` (body to string, deserialize, map) | .NET 10 Release JIT, not Native AOT | 10 000 issues, 18.3 MB, page size 5000, fake handler | 80 ms | 121 ms |

Method: a temporary xUnit test (not committed) repeated the 24 issues of `search/search-rows-gcm.json` to 10 000, 5 warm-up and 20 timed runs on a noisy machine. Real pages are tens of issues, so this is far below a millisecond per page. Not measured: the Native AOT figure (no caller is wired into the app yet), the saving of sharing repeated names within a page (a dictionary lookup per string, simple and proportional to data size, kept unmeasured per the performance skill), and reading the body as a stream instead of a string (transport, unchanged). Re-measure on AOT when the first view loads search results.

### ADF parsing (#68)

| Path | Binary | Dataset | Median | p95 |
|---|---|---|---|---|
| `AdfParser.Parse(JsonElement)` | Native AOT, tiny console project referencing `Geeratya.Jira` | `fields.description` of `adf/issue-gtm5-rich-description.json` (longest recorded, 11.1 KB, 13.9 KB allocated) | 11 us | 23 us |
| `AdfParser.Parse(ReadOnlySpan<byte>)` (includes `JsonDocument.Parse`) | same | same | 47 us | 82 us |

Method: temporary console project outside the repo (2000 warm-up, 5000 timed iterations, `Stopwatch`), noisy machine. The parse happens once per opened issue or comment, never per frame.

### ADF rendering (#80)

| Path | Binary | Dataset | Median | p95 |
|---|---|---|---|---|
| `AdfRenderer.Render` at width 80 | Native AOT, tiny console project referencing `Geeratya.Core` | `fields.description` of `adf/issue-gtm5-rich-description.json` (longest recorded, 11.1 KB, about 45 lines, 28 KB allocated) | 10 us | 16 us |
| same at width 20 | same | same | 10 us | 16 us |
| same at width 80 | same | `adf/issue-gcm-media-description.json` (one media node) | under 1 us | 1 us |
| same at width 80 | same | synthetic document: the rich description repeated to 5001 lines (3.5 MB allocated) | 1.2 ms | 2.8 ms |
| same at width 120 | same | same synthetic document | 1.7 ms | 7.5 ms |

Method: temporary console project outside the repo (warm-up of a fifth of the runs, 3000 timed renders for the recorded descriptions, 40 for the synthetic one, `Stopwatch`), machine under load (load average 18 on 14 cores), so p95 is noisy. Rendering is pure and happens once per (document, width), when an issue is opened or the terminal is resized, never per frame; the renderer wraps the whole document (no laziness), which is far below a frame even at 5000 lines. `DisplayWidth.Of` got an ASCII fast path (a scan for printable ASCII before the grapheme enumerator) because table padding measures every line.

## M1 measurements (#91)

Environment: Linux x86_64, 14 cores, load average 2 to 6 during the runs (the harness prints it; never trust a run near 14), tmux 120x40. Native AOT `geeratya` from `dotnet publish src/Geeratya.Tui -c Release` at `b270a46` (the publish printed no warning at all, so no `IL*` warning). Fake: `fakejira --issues 10000 --latency-ms 50` unless a row says 300. Every tmux row is an upper bound that includes tmux (floor below); the JIT in-process rows exclude it. Commands:

- `scripts/measure.sh <binary> <fake url> 20` (cold start, `?` key).
- `SCENARIO=scrum scripts/measure.sh <binary> <fake url> 12` (fake with `--issues 5000`: Scrum backlog and sprint switch with and without cache, `j` and `za` keys, RSS; see "Scrum paths (#187)").
- `SCENARIO=list scripts/measure.sh <binary> <fake url> 8` (open a list, key in a loaded list, 10k rows loaded page by page with `G`, RSS).
- `SCENARIO=views scripts/measure.sh <binary> <fake url> 8` (issue detail, second start from the disk cache, 10 tabs, tab switch, RSS). Each run uses fresh private cache directories; the first session is left 2 s to flush the debounced cache write.
- `GEERATYA_BENCH=1 dotnet test -c Release tests/Geeratya.Tui.Tests --filter HotPathMeasurements --logger "console;verbosity=detailed"` (in-process, JIT; opt-in, skipped otherwise).

| Path | Binary | Dataset, latency | Median (ms) | p95 (ms) | Budget |
|---|---|---|---|---|---|
| tmux floor | n/a | `printf x`, 20 sessions | 9 | 27 | n/a |
| Cold start to first frame, empty shell | AOT | 20 sessions | 35 | 55 | under 100: met |
| Key to frame (`?` help) | AOT | 20 sessions x 5 | 37 | 63 | next frame: met |
| Second start, cold start to first frame, disk cache present | AOT | 8 sessions, 50 ms latency | 33 | 51 | under 100: met |
| Open a list, `:jql` to first rows, no cache | AOT | 10k-issue fake, 50 ms latency, 8 sessions | 162 | 175 | n/a (search and approximate count at 50 ms each, the prompt and tmux) |
| Open a list, `:jql` to first rows, cache present | AOT | same, second start | 82 | 97 | cached rows without the network: met (with 300 ms latency: 84 vs 401 without) |
| `j` key to frame in a loaded list | AOT | 100 rows loaded | 38 | 44 | next frame: met (about the 37 of the `?` key, so the tmux and polling floor) |
| `k` key to frame, 10k rows loaded | AOT | 10 000 rows, 8 sessions | 34 | 61 | no dropped frames: met |
| Enter to issue detail loaded | AOT | fake detail, 50 ms latency (300 ms: 384 / 423) | 153 | 202 | n/a (network bound; the frame with `Loading…` is drawn at once) |
| `Tab` key to frame, 10 tabs open | AOT | 10 tabs of one page | 39 | 63 | next frame: met |
| RSS with a 10 000-row list loaded | AOT | 10 000 rows in one tab | 50 MB | 53 MB | bounded: met (cap 10 000 rows per tab) |
| RSS with 10 tabs | AOT | 10 tabs of 100 rows | 42 MB | 42 MB | bounded: met |
| Nav key + frame, list | JIT, in process | 10 000 rows, 2500 timed keys after 500 warm-up, 120x36 | 0.17 | 0.21 | met |
| Nav key + frame, board | JIT, in process | 10 000 cards, 4 columns | 0.54 | 1.17 | met |
| Nav key + frame, board with sub-tasks under parents | JIT, in process | 10 000 cards, every fifth a child in its parent's column | 2.4 | 3.5 to 4.4 | met (frame is 16 ms at 60 Hz) |
| `BoardColumns.Visible` alone (regrouping) | JIT, in process | 10 000 cards | 0.11 | 0.20 | see below |
| same, with parents (`Arrange` builds the key set and child map) | JIT, in process | 10 000 cards, every fifth a child | 1.0 | 2.4 | see below |

Columns regrouping per key (the open question from #85): at the 10 000-row cap it costs 0.1 ms (no parents) to 1.0 ms (a fifth of the cards nested), and a key plus its frame stays at about 3.5 to 4.5 ms at p95 (loaded machine), so the groups are not cached. A board with a few thousand cards and no sub-tasks is the realistic case and is far cheaper. Caching would add an invalidation rule for no measurable gain; revisit only if a row cap above 10 000 or a per-key cost above a few ms appears.

Not re-measured here: JSON mapping of a 10k-issue search (the #66 row stands: 80 ms median JIT for 10 000 issues in one 18 MB body; the app asks for 100 per page, so one page is about a millisecond) and ADF parse and render (the #68 and #80 rows stand: 10 us for the longest recorded description on AOT, 1.2 ms for 5000 lines). No harness exists for the AOT in-process figures of the JSON mapping; the end-to-end rows above include it.

No budget is missed. One defect found while measuring: cached rows draw a 3-character key column on a cache hit (#236).

Scope note: the acceptance rows for the JSON mapping of a 10k-issue search and the ADF render were not re-taken here; the #66, #68 and #80 rows above stand because no Native AOT in-process harness exists, and the end-to-end rows include both. Add a console harness if an AOT figure is wanted.

## First issue-detail open (#435)

The detail used to await `GET /field` before `GET /issue` on the first open of a session, and again on every open while `/field` kept failing: one sequential round trip in front of the issue. Every connect now adds one low-priority `GET /field` request after the first frame. `SiteFields.Warm()` (once per connection) starts the lookup at lowest priority once the connection exists (after the first frame), concurrent callers share the one request, and a failure is remembered for 30 s. The detail adds no sequential `/field` request once the warm-up has answered or failed; if it is still in flight the open waits for that same request instead of sending its own. Checked by request counts (`FieldClientTests`, `IssueDetailFlowTests`: a failing `/field` is asked once across two opens) and timed end to end (#453) with `SCENARIO=detail scripts/measure.sh` (first Enter of a session to the detail header, pressed as soon as the list shows, fresh cache per session; the saving is one round trip of site latency when the warm-up has already answered).

| First Enter to issue detail loaded (#453), AOT | Median (ms), runs | p95 (ms), runs |
|---|---|---|
| `763bc62^` (`/field` awaited before `/issue`) | 198 | 270 |
| `763bc62` (first `SiteFields.Warm()`, the pair that isolates the warm-up) | 172, 157 | 264, 173 |
| `main` at `84388ae` (with the #435 follow-up fix `2c6db35`), earlier alternating runs | 167, 156 (before: 210, 215) | 295, 170 (before: 346, 275) |

Environment: fake `--issues 200 --latency-ms 50`, 15 sessions per run, Linux x86_64 with load 30 to 36 on 14 cores (other agents building), so absolute values are upper bounds, p95 is nearly the maximum sample and the comparison rests on the medians. `763bc62` also carries the no-open-sprint empty state, which the detail path does not touch; the `main` row differs from `763bc62^` by more than the warm-up. Across the runs the median drops by roughly 40 to 55 ms, consistent with the one `/field` round trip at 50 ms latency. The probe presses Enter once the list header shows (about 160 ms after `:jql`), so the warm-up has already answered and the saving is the whole round trip; a user who opens a detail sooner, while the warm-up is in flight, waits for that same request and saves less. The figure is higher than the "Enter to issue detail loaded" row of the M1 table (153) because that scenario waits 0.5 s before Enter; both rows stay valid for what they time.

## Scrum paths (#187)

Environment: Linux x86_64, 14 cores, load average 13 to 16 (other agents building; every figure is an upper bound), tmux 120x40, Native AOT `geeratya` from `dotnet publish src/Geeratya.Tui -c Release` (no `IL*` warning). Fake: `fakejira --issues 5000 --latency-ms 50`, which serves the generated Scrum board `GEN` (board 7): 5000 issues in 30 sprints (the generator clamps the count to 3..30, so 20 sprints would be 2000 issues), the last sprint future, the one before it active, the backlog view holding 1248 rows (the active and future sprints plus the issues in no sprint; closed sprints are not part of it). Command: `SCENARIO=scrum scripts/measure.sh <binary> <fake url> 12`. Per run two sessions share one private cache: the first has none, the second starts with the cache the first wrote (flushed after 2 s). The switcher step types `28` (a closed sprint of 134 issues) and presses `Enter`.

| Path | Binary | Dataset, latency | Median (ms) | p95 (ms) | Budget |
|---|---|---|---|---|---|
| Cold start to first frame, no cache | AOT | 12 sessions | 43 | 80 | under 100: met (unchanged from M1: 35 to 43) |
| Cold start to first frame, cache present | AOT | 12 sessions | 41 | 98 | under 100: met |
| `:backlog GEN` to first rows, no cache | AOT | 5000 issues, 50 ms | 370 | 408 | network bound (board lookup, sprints, backlog search, approximate count) |
| `:backlog GEN` to first rows, cache present | AOT | same | 204 | 251 | cached rows drawn without the search: met (370 without) |
| `<space>sv` to the switcher drawn | AOT | 30 sprints | 43 | 91 | next frame: met |
| Switcher `Enter` to the rows of a closed sprint, no cache | AOT | 134-issue sprint, 50 ms | 235 | 280 | network bound |
| same, cache present | AOT | same | 74 | 107 | cached rows without the network: met (235 without) |
| `j` key to frame in the loaded backlog | AOT | 1248 rows | 53 | 91 | next frame: met (the `?` key of the same load reads 82, so the tmux and polling floor) |
| `za` (fold a section) to frame | AOT | same | 69 | 85 | next frame: met (two keys) |
| RSS with the backlog and a sprint tab loaded | AOT | 5000-issue site | 47 MB | 47 MB | bounded: met |
| Nav key + frame, Scrum backlog | JIT, in process | 10 000 rows in 21 sections | 0.40 | 1.11 | met (`HotPathMeasurements`, from #178; re-measured in #498 at load 18 with the variants side by side: pre-#493 code 0.38 / 0.97, cursor items materialized per build 0.59 / 3.27, now 0.40 / 1.11; the 0.41 / 0.83 of #178 was a quieter machine) |

Reading it: the first frame is unchanged by the Scrum code (the backlog and the sprint list are only requested after a view is opened). The cold-start and `j` rows were taken at load 13 to 16, so they sit above the quiet-machine M1 figures (35 and 37); a back-to-back `?` key run at the same load read 82 ms median, so the differences are load, not the Scrum paths. A sprint switch with the cache present draws the sprint's rows in 74 ms against 235 without; the sprint list in the switcher is loaded once per open and is not part of the timed rows. Not measured: a 5000-row backlog on the board (the backlog view holds 1248 of the 5000 issues (closed sprints are excluded by its JQL); the 10 000-row in-process row above covers the render size) and the Windows console.

No budget is missed, so no performance issue was registered.

## Startup after the M6 work (#616)

Native AOT `geeratya`, `scripts/measure.sh <binary> <fake url> 20`, fake Jira with 50 ms latency, 14 cores, load average 10 to 14 during the runs (other agents were building). Three alternating 20-session rounds per binary:

| Binary | Cold start to first frame, median / p95 (ms), three rounds |
|---|---|
| `ddb6c86` (PR #553, #542; the 38 ms figure) | 40 / 50, 37 / 47, 57 / 83 |
| `main` at `678c041` (after #615) | 38 / 51, 38 / 45, 45 / 64 |

**There is no startup regression.** The 130 to 140 ms figures (the 137 of #589) were an artifact of the harness: `wait_frame` tested "the frame has text" with `${current//[[:space:]]/}`, and in a UTF-8 locale bash needs about 100 ms for that substitution on a 120x40 frame that contains box-drawing characters (measured: 111 ms against 5 ms for the same frame in ASCII). PR #612 (#537) was the first to draw the start and messages screens in a frame, so from that merge on every check of a frame with a border cost 100 ms, which the harness added to the cold start (and to the key to frame figures: `?` read 180 ms instead of 37). Bisect over the 24 merges after #553: `3af9504` and `bbf545c` read 37, `f6a4800` (#612) and later 133 to 143; a timestamped `tmux capture-pane` trace of the same binary showed the frame at 41 to 48 ms, tmux start included. The check now uses `grep -q '[^[:space:]]'`.

Any figure above taken with the old `measure.sh` on a build that has framed screens (after #612, 2026-11-03) is inflated by up to about 100 ms, in particular the key to frame rows; re-measure before quoting them.

Idle cost (M7 input, #619), start screen and a 100-row list, 20 s windows each, `/proc/<pid>/stat`: 22 and 25 CPU ticks (about 1.1 to 1.3 % of one core, 0.2 s of CPU in 20 s), RSS 47 MB, 10 threads; with the frame on screen and nothing happening the process still uses CPU, registered as #619 (the cause is not investigated).
