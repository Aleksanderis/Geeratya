# Performance

*Internal design note for maintainers and agents. Not needed to use or contribute to Geeratya.*

Budgets and measured numbers. The checklist for writing fast code is the `performance` skill; the harness is `scripts/measure.sh` (see `docs/internal/testing.md`, "Measurement harness"). Measure the Native AOT binary against `tools/FakeJira`, never `dotnet run` and never the dev site.

## Budgets

| Budget | Target |
|---|---|
| Cold start to first frame | Under 100 ms, from local cache or an empty state; never waits on the network |
| Key press to frame | Never waits on a request; a local action (open help, move selection) is drawn in the next frame |
| Scrolling a 10k-issue result list | No dropped frames; work proportional to the visible rows |
| Background tabs | Cheap: no polling faster than the refresh rule, no rendering |
| Memory | Bounded however long the session runs; every cache has a size bound and an owner that drops it |
| Opening a cached view | Cached rows drawn without waiting for the network; the cache read is off the UI thread and after the first frame (met: 82 ms median to first rows from the cache against 162 without, see "M1 measurements (#91)") |
| Idle CPU | Under 2 % of one core with refresh running (met: 0.31 %, see "Release measurements (#211)") |
| Refresh tick | An idle `Tick` returns the same state (no redraw unless time-dependent text is visible) and costs a comparison; only the active tab refreshes |

Bounds from the M1 design (`docs/internal/architecture.md`, "Views and tabs", "Cache and refresh"): 10 000 rows per tab, 50 000 across tabs, 32 cached views and 200 cached issue details in memory, 20 MB of cache per profile on disk. The first measurements of these are in "M1 measurements (#91)" below.

## How numbers are taken

`scripts/measure.sh <binary> <fake url> [runs]` (Windows: `measure.ps1`, default scenario only, untested) starts the binary in a private tmux server (private `XDG_*` directories), polls `capture-pane` until the first non-blank frame, then toggles the help overlay with `?` and times each change of the frame. It first times a trivial command (`printf x`) the same way, the "tmux floor", so the cost of tmux and of the polling loop can be read off. Results are the median and p95 over all runs, printed with the load average before and after (Linux only: `/proc/loadavg`); do not trust a run when the load is near the core count. Start FakeJira beside it with the dataset and latency you want to record (`fakejira --issues 10000 --latency-ms 50`). The script seeds an active fake profile for the binary (`docs/internal/testing.md`).

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

The two rows below were measured before the real-height scroll (#738) and without a view width; see the re-measurement under the method paragraph.

| Path | Binary | Dataset | Median | p95 |
|---|---|---|---|---|
| Nav key (`j`, `l`, `Ctrl-d`, `h`, `Ctrl-u`) through `AppReducer` plus `FrameBuilder.BuildStyled` at 120x36 | .NET 10 Release JIT, in process | board of 200 cards, 2500 timed keys | 0.31 ms | 0.56 ms |
| same | same | board of 2000 cards, 2500 timed keys | 0.29 ms | 0.59 ms |

Method: temporary xUnit test (not committed), 500 warm-up keys, four columns, assignee on every card; it excludes the terminal draw and tmux. The renderer draws only the cards in the viewport. Each key and each frame still regroup all rows into the columns (`BoardColumns.Visible`, one dictionary lookup per row; `Arrange` adds a pass over the rows looking for parents and, only when some row has one, a key set and a child map, all linear) and a key looks the selected issue up by key once (linear); that is below the noise up to 2000 cards, so the groups are not cached; boards up to the 10 000-row cap are measured in "M1 measurements (#91)" (about 1 ms regrouping with sub-tasks), and the table predates the border rework and the sub-task grouping (linear passes, not re-measured) (the per-frame cost is still bounded by the viewport). Not measured: Native AOT in-process figures.

Re-measured after the real-height scroll (#738, #751): `BoardScroll.Reveal` now sums card heights (`BoardCardPlan.Height`) for every column on each `BoardMotion.Resolve`, using the tab's `ViewWidth` (the bench tab now carries `ViewWidth = 120`; before, width 0 made heights a constant). `HotPathMeasurements`, 10 000 cards, 4 columns, 2500 keys, .NET 10 Release JIT, two runs: no sub-tasks median 1.29 and 1.31 ms, p95 2.6 and 3.9 ms (with width 0 for comparison: 1.07 ms, p95 2.0 ms, so the heights cost about 0.2 ms median); every fifth card a child: median 6.7 and 6.9 ms, p95 14 and 19 ms (width 0: 6.6 ms, p95 18.6 ms). Height computation is therefore not the regression and is not cached; the heights are computed only for the few cards near the selection and the end of each column. Against the "M1 measurements" rows (0.54 and 2.4 ms median), the bench is 2 to 3 times slower than when M1 was measured, not because of heights (the cause is not attributed; probably card layout and frame building since then; `BoardColumns.Visible` alone is unchanged, 0.12 ms and 1.15 ms with parents). A 10 000 card board without sub-tasks stays inside a 16 ms frame; with sub-tasks the p95 can exceed it on a loaded machine (follow-up #758).

Fixed in #758: the grouped columns are memoised per tab (`ColumnsMemo`, one entry on `TabState`, keyed by reference on the row list, board and folded-card list and by value on lane, swimlane and Done cut-off). Profile: one nav key plus its frame asked for the columns about six times (`ColumnsOf` from the move, the renderer, the selected-issue text and the hints), each a full `BoardColumns.Visible` (1.07 ms with sub-tasks), which is the whole 6.8 ms; heights and frame building were minor. A key changes only the cursor, so the rows are unchanged and the grouping now happens once per row-list change, not per key. Same bench (`HotPathMeasurements`, 10 000 cards, 4 columns, 2500 keys, JIT, load 15 to 19), before and after: no sub-tasks 1.31 ms median / p95 2.74 to 0.71 / 1.57; every fifth card a child 6.80 / 14.45 to 0.69 / 1.32 (acceptance: under 3 and under 8 ms, met). The "Visible alone" rows are unchanged work (read 1.69 and 0.39 ms in the after run on a busier machine, the cost is now paid once per row change). Memory: the memo holds one grouped copy of the row references per tab plus the row list it was built from, so a tab whose rows were replaced while it was not drawn as columns keeps the old list alive until the columns are next asked for (at most one extra row list per tab, bounded by the 10 000-row cap); the RSS delta is not measured.

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

Re-measured (#765): the `scrum` scenario waited for patterns the sprint view no longer shows (the sprint is a columns tab of cards now, the backlog header is `GEN board ──── 1248 issues ─╮`) and for a start picker that the second session's restored tabs do not have; it now waits for a card (`GEN-n  Task`), the new header, and gives each pass its own state directory (the cache is shared). Same setup (Native AOT at `main` `61b904c`, fake `--issues 5000 --latency-ms 50`, 12 runs, load 12 to 17): cold start 42 / 44 ms (fresh / cache), `:backlog` to first rows 283 ms without the cache (p95 418) and 83 with it (171), `<space>sv` 42, switcher `Enter` to the sprint rows 240 without the cache and 78 with it, `j` 35, `za` 34, RSS 52 MB. Most rows are lower than the table above (cache-present `:backlog` 204 to 83, `j` 53 to 35, `za` 69 to 34); the table predates #612 and #616 and was not replaced. The sprint rows now end when the first card is drawn, a different endpoint from the old row text.

No budget is missed, so no performance issue was registered.

## Startup after the M6 work (#616)

Native AOT `geeratya`, `scripts/measure.sh <binary> <fake url> 20`, fake Jira with 50 ms latency, 14 cores, load average 10 to 14 during the runs (other agents were building). Three alternating 20-session rounds per binary:

| Binary | Cold start to first frame, median / p95 (ms), three rounds |
|---|---|
| `ddb6c86` (PR #553, #542; the 38 ms figure) | 40 / 50, 37 / 47, 57 / 83 |
| `main` at `678c041` (after #615) | 38 / 51, 38 / 45, 45 / 64 |

**There is no startup regression.** The 130 to 140 ms figures (the 137 of #589) were an artifact of the harness: `wait_frame` tested "the frame has text" with `${current//[[:space:]]/}`, and in a UTF-8 locale bash needs about 100 ms for that substitution on a 120x40 frame that contains box-drawing characters (measured: 111 ms against 5 ms for the same frame in ASCII). PR #612 (#537) was the first to draw the start and messages screens in a frame, so from that merge on every check of a frame with a border cost 100 ms, which the harness added to the cold start (and to the key to frame figures: `?` read 180 ms instead of 37). Bisect over the 24 merges after #553: `3af9504` and `bbf545c` read 37, `f6a4800` (#612) and later 133 to 143; a timestamped `tmux capture-pane` trace of the same binary showed the frame at 41 to 48 ms, tmux start included. The check now uses `grep -q '[^[:space:]]'`.

Both rows of the table above were taken with the fixed `measure.sh`. The sections above this one (M1, detail open, Scrum paths) were taken before #612 merged (2026-11-03), on builds without framed screens, so they are not affected. Figures taken with the old `measure.sh` on a build from #612 on (the 137 ms of #589) are inflated by up to about 100 ms, in particular the key to frame rows; re-measure before quoting them. `scripts/screenshot.sh` had the same slow check and was fixed alike (it only made screenshots slower).

Harness note (#759): from the framed start screen (#612) the "Open a view" picker takes typed text as its filter, so `:jql` sent right after the first frame filled the picker instead of opening a list (the same problem as in `idle-cpu.sh`, #749). Every scenario of `measure.sh` now waits for the picker, closes it with Escape and then types its command; the tab-bar, detail-loaded and list-header patterns that no longer matched were updated (the `scrum` scenario followed in #765), and the views scenario starts its second session with a fresh state directory (the first session's saved tabs would be restored, with no picker). The scenarios cannot silently measure the picker: each waits for an issue row (`Generated issue 1`), so a swallowed `:jql` timed out instead of producing a number. The list, views, detail and scrum tables above were taken before #612, when the start screen had no picker, so those rows were not replaced (the scrum scenario was re-measured in #765, see the Scrum paths section). The detail-loaded wait changed from the issue header without "Loading…" to the description text, so "Enter to issue detail" figures before and after this fix are close but not strictly the same endpoint. The spot check below is a few sessions under load, not a replacement for those rows. Spot check after the fix (Native AOT, fake `--issues 10000 --latency-ms 50`, load 9 to 13): views, 8 sessions: first open of the list 196 ms median (p95 227), Enter to detail 168 (187), second start `:jql` to header from the cache 81 (88), Tab key with 10 tabs 35 (54), RSS 49 MB; detail, 10 sessions: 157 ms (164).

## Idle cost (#619)

Native AOT `geeratya` at `main` `d2bbf91`, fake Jira (`--issues 100`), 120x40 tmux, no key presses, a 20 s window after the 100-row list was loaded (see the correction below), per-thread `utime+stime` ticks and `voluntary_ctxt_switches` from `/proc/<pid>/task/*` read twice (`scripts/idle-cpu.sh <binary> <fake url> list`). 10 threads, RSS 47 to 49 MB. Four alternating runs: 6 and 4 CPU ticks per 20 s at the default, 1 and 3 with 10 iterations/s; that is 0.2 to 0.3 % of one core at the default. The 22 to 25 ticks quoted in #616 (and the "about 1 %" of the issue title) were taken over a window that began right after start, when the connection check, the first load and the cache write were still running; a window taken after the screen has settled does not show them. The default runs made 8 664 and 8 926 context switches per 20 s summed over the 10 threads (about 440 a second); one timer expiry costs several of them (timer thread, pool worker, the thread that runs the callback), so the rates below are expiries, not switches.

Correction (#749): until then `idle-cpu.sh list` typed `:jql project = GCM` into the start picker, which answers "Nothing to open", so the "loaded list" figures above and in #616 measured the start picker. The script now opens the first board with Down, Enter (as `idle-frames.sh` does) and warns when no issue key is on screen. Re-measured at `main` `0cd6ba3`, Native AOT, fake Jira `--issues 100`, 120x40 tmux, 20 s window, load average 6 to 17 (other agents building): start screen 3 and 4 ticks (9 651 and 9 074 context switches), kanban board GCM loaded 4 and 5 ticks (9 610 and 9 777). That is 0.15 to 0.25 % of one core; a loaded board costs about one tick per 20 s more than the start screen, within noise. The conclusion above (nothing cheap left to fix on our side) stands. The other figures in this section (the four alternating runs, the 10 iterations/s comparison) were also taken on the start picker and were not re-run.

Idle timer expiries by cause:

| Cause | Rate | Where |
|---|---|---|
| Terminal.Gui input thread polls the terminal every 20 ms with `Task.Delay` | 50/s | library, `InputImpl.Run` |
| Terminal.Gui main loop: one iteration per `MaximumIterationsPerSecond` (default 25), each ending in a delay | 25/s | library |
| Our `Tick` timer (message lifetimes, refresh rule, which-key); the `Application.Invoke` also wakes the main loop | 1/s; an idle tick returns the same state and draws nothing | `ShellHost.PostTick` |
| Which-key timer | none unless a chord is pending | `ShellHost.ScheduleWhichKey` |
| Cache flush timer | one expiry after a cache write (each completed load), none otherwise | `SiteCache` |
| Spinners, Jira polling, file watchers | none exist | |

The library accounts for 75 of the 76 expiries a second, so there is nothing cheap left to fix on our side; the switches per expiry are not split by cause (not measured). Lowering `Application.MaximumIterationsPerSecond` to 10 was tried: 6 805 and 7 086 switches per 20 s against 8 664 and 8 926 (about 20 % fewer, more than the main-loop share alone predicts, because each iteration also runs a size query and a draw check) and 1 and 3 ticks against 6 and 4, which is within the noise of the machine load (other agents were building). It costs a slower first key after a pause, so it was not applied; whether an idle governor pays off is #624. For comparison only: Gitunia measured the same library at 25/s 1.04 % and 10/s 0.91 % on another workload and a 45 s window, so those figures are not comparable with the ones above.

## Idle output (#744)

Owner report: Ghostty at about 380 % CPU and Hyprland at 160 % with geeratya idle (Foot calm). Cause: Terminal.Gui 2.5.0 sets "cursor needs update" on every focus change and never clears it, so `ApplicationNavigation.UpdateCursor` hid the cursor and moved it home in every main-loop iteration (`ESC[1;1H ESC[?25l`, 25 a second), although no cell changed. Not a layout or draw loop: `LayoutAndDraw` found nothing dirty and wrote no cells. Fix: `ShellHost.OnIteration` clears the flag on every iteration (the shell never shows the hardware cursor; the driver hides it at start).

Measured with `scripts/idle-frames.sh <binary> <fake url> [start|list] [seconds]` (Native AOT binary in a 120x36 pty under `script`, fake Jira with the recorded fixtures, window of 10 s after the screen settled; `MOTION=1` adds 10 pointer-move reports a second over the body):

| Case | Before (`main` `90f7625b`) | After |
|---|---|---|
| Start screen, 10 s idle | 2 988 bytes, 248 frames (cursor-home repaints) | 0 bytes, 0 frames |
| Kanban board GCM loaded (`list` mode: Down, Enter on the start picker; the script checks that issue keys are on screen), 10 s idle | 2 976 bytes, 248 frames | 0 bytes, 0 frames |
| Same board, 10 s of pointer moves | not measured | 0 bytes, 0 frames |

Mouse mode: Terminal.Gui enables `?1003h` (any-event) itself; its ANSI driver writes the mode set at start, and no setting for `?1002h` was found (not exhaustively checked). Geeratya uses plain moves for the tab hover (`ShellHost.OnHover`, which returns at once below the tab row when no tab is hovered), so `?1003h` is needed; a pointer move costs no output. Ghostty CPU was not measured here (no Ghostty in the agent environment); the owner's Ghostty and Hyprland CPU check after this change is still open (#744 acceptance).

## Kanban Done cutoff (#674)

A Kanban board no longer loads Done issues whose status category changed more than N days ago (Jira's own "Hide done work items after", else the setting `board.done-cutoff`, default 14, #681; `statusCategory != Done OR statusCategoryChangedDate >= -14d OR (statusCategoryChangedDate is EMPTY AND updated >= -14d)` on the board call). Effect by construction: pages requested = ceil(matching issues / 100), so a board with 2 000 old Done issues and 60 current ones drops from 21 requests (and 2 060 rows against the 10 000 rows per tab cap) to 1 request and 60 rows. Not measured against a live board: the dev site's Done issues are few, and the fake Jira serves recorded pages regardless of `jql`.

Reading Jira's value costs extra requests (#681): the first board load of a connection adds the cloud id (once per connection), the board's view id (once per board) and the cutoff, 3 sequential requests through the transport (about 3 round trips), capped at 3 s by `DoneCutoffReader.LookupTimeout` so a slow or failing gateway delays a load by at most that and then falls back to the setting; the cutoff is reused for 5 minutes (a failure for 1 minute), so refreshes within that cost nothing and later ones one request. Not measured on the live gateway (round-trip time depends on the site); the numbers are the request counts the tests assert.

## Release measurements (#211)

Native AOT `geeratya` from `dotnet publish src/Geeratya.Tui -c Release` at `main` `3bc866cf`, fake Jira (`tools/FakeJira --issues 10000 --latency-ms 50`, recorded fixtures), 120x40 tmux, Linux, 14 cores. Load average during the measurements: 6.8 to 4.5 for the start and list scenarios, 4.4 for views and scrum, 3.8 to 1.9 for the idle runs (a desktop session with Ghostty and Hyprland was running; no other agent). The tmux floor was 13 ms median (p95 18), included in every figure.

| Budget | Measured | Target | Result |
|---|---|---|---|
| Cold start to first frame (15 sessions) | 43 ms median, p95 60 (39 / 51 with a cache present, scrum scenario) | under 100 ms | met |
| Key press to frame (default scenario, `?` toggle) | 37 ms median, p95 52; `j` in a loaded list 35 (41); `Tab` with 10 tabs 36 (63) | no wait on a request | met |
| Open a 10 000-issue list (first page, 5 sessions) | 199 ms median (p95 214), includes the fake's 50 ms | no target | recorded |
| Scroll at the end of the full 10 000 list | `j`/`k` 36 ms median (p95 40) | no dropped frames | met |
| Issue detail from the list | Enter to description 165 ms (p95 167) | no target | recorded |
| Cached view, `:jql` to header | 83 ms (p95 85) against 198 without the cache | cache drawn without the network | met |
| Scrum backlog (5 sessions) | `:backlog` to rows 280 ms without cache (p95 287), 78 with; sprint switch 230 / 81; `j` 35; `za` 36 | no target | recorded |
| Idle CPU, Kanban board GCM loaded from the 10 000-issue fake (first page), no keys, 20 s window (3 runs) | 5, 2 and 4 ticks per 20 s: 0.1 to 0.25 % of one core (start screen: 3 ticks); about 9 600 context switches per 20 s; with refresh running (one run, `WINDOW=130`, two 60 s refresh periods inside, the fake's request count was not checked): 40 ticks per 130 s, 0.31 % of one core | under 2 % of a core | met |
| Memory (RSS) | 69 MB with the 10 000-row list loaded (p95 71); 50 MB with 10 tabs; 55 MB with a backlog and a sprint tab | bounded | met |
| Binary size | 19.98 MB (19.1 MiB) `geeratya`, plus 51.8 MB `geeratya.dbg` (debug symbols, not shipped in the binary) | no budget | recorded |
| `dotnet test` wall time (offline) | 38 s including the incremental build; slowest assembly `Geeratya.Tui.Tests` 19 s; 6 582 tests passed, 15 skipped | no budget | recorded |

Limits: the 20 s idle windows hold no refresh tick (period 60 s); the 130 s run does, and a refresh of a board reloads its pages, so the figure depends on source and size; the difference to the 20 s runs (about 0.1 % of a core) compares runs under different load and is indicative only. The `dotnet test` figure was taken as one run with a warm build. Gitunia's 2.8 % idle CPU found late (the issue's reference) does not show here: the figures above are the CPU of our process only (the terminal emulator side is #744, owner check still open). RSS is 19 MB higher with 10 000 rows than the M1 row (50 MB); investigated in #813, see "RSS with 10 000 rows (#813)".

## RSS with 10 000 rows (#813)

Reproduced at `main` `97b6b6b` (Native AOT, `SCENARIO=list scripts/measure.sh`, fake `--issues 10000`, load about 4): 68 MB median (p95 68 to 69), against 50 MB in "M1 measurements (#91)". Cause found: since #520 the mapper maps the `subtasks` field of every row into `IssueSummary.Subtasks`, whatever was asked for, and the fake always sends sub-task stubs (it ignores `fields`; 1.5 per issue on average, 15 000 stub records with a status object and three strings each). A `:jql` list never asks Jira for `subtasks` (only board sources set `IncludeSubtasks`), so real Jira does not send them there and the 10k figure was inflated by the fake. Every row also held two fresh empty lists (`Sprints`, `Subtasks`).

Fix: `IssueSummaryMapper` maps sub-tasks only when the request asked for them (`IncludeSubtasks`), and returns the shared empty list for rows with no sprints. Same method, same fake, 5 to 8 runs per binary run back to back: 68 MB before, 60 MB after the sub-task gate (p95 64), 59 MB after the empty-list change (p95 61). Key-to-frame and open-list times did not move (35 and 36 ms, first page 119 ms). Test: `GivenSubtaskStubsInTheReplyButNoSubtaskExtra_WhenMapping_ThenRowsHoldNoSubtasks`.

Remaining gap to M1 (about 9 MB, 0.9 KB a row): not attributed to one change. The row record gained fields since M1 (`Sprints`, `Subtasks`, `IsListed`, `Estimate`, parent stubs), the state keeps the rows in the tab list and a memoised grouped copy for the columns layout, and the Terminal.Gui and theme setup grew with the start screen and picker. Decision: accepted. The figure is bounded by the 10 000-row cap per tab, 59 MB is well under any budget the project has set (none for memory), and finding the rest needs a heap profile of a native AOT process, which is not available cheaply here (no `dotnet-gcdump` for AOT). A heap-level profile is worth doing only if a user reports memory pressure.
