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
| Opening a cached view | Cached rows drawn without waiting for the network; the cache read is off the UI thread and after the first frame (target, measured in #91) |
| Refresh tick | An idle `Tick` returns the same state (no redraw unless time-dependent text is visible) and costs a comparison; only the active tab refreshes |

Bounds from the M1 design (`docs/architecture.md`, "Views and tabs", "Cache and refresh"): 10 000 rows per tab, 50 000 across tabs, 32 cached views and 200 cached issue details in memory, 20 MB of cache per profile on disk. The first measurements of these are #91.

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

Superseded history (#7, first measurement, load 16): cold start median 124 / p95 173, key to frame 207 / 241; an earlier 10-session run gave cold start 91 / 185 and key to frame 207 / 253. Repeated runs for #37 (five 30-session runs on the same source, load 9 to 17) gave cold start 41 to 58 and key to frame 35 to 41, so the old figures were not reproduced. The cause of the difference is not established: heavier machine load during the first runs is the likely reason, but a quiet machine was not available, so that is not proven, and no code change was made. Re-run on a quiet machine when one is available; if the old figures return, reopen #37.

Reading the key-to-frame figure (ad hoc checks, not committed and not reproducible from the repo): a bare `cat` echoing a typed character, timed with the same loop, takes 10 ms median (p95 20), so about 25 ms of the 37 ms is above the harness floor. A temporary in-process trace (timestamps in the key handler and the `Iteration` event) showed `tmux send-keys` returning 10 to 20 ms before the key reached the process, and the frame visible to `capture-pane` about 15 ms after the key handler ran, mostly the polling loop's own `capture-pane` spawn. Terminal.Gui's main loop iterates about 25 times a second by default (`Application.MaximumIterationsPerSecond`, a static property), but a key press wakes it at once: setting it to 120 did not change the figure (alternating A/B runs, 35 to 41 ms both), so it is left at its default.

## Notes for later rows

Add a row per measured path: JSON mapping of a 10k-issue search (ad hoc timed loop, command in the PR), list scrolling, ADF rendering of the longest recorded description, memory (RSS) after opening a 10k list. Record binary type, dataset size, injected latency, median and p95.

### Local cache (#82)

| Measurement (ms) | Median | p95 | Budget |
|---|---|---|---|
| Cold start to first frame, Native AOT, with the cache wired in (20 sessions, load 11 on 14 cores) | 33 | 44 | under 100 |
| tmux floor | 3 | 14 | n/a |

Nothing is read from the cache before the first frame: the cache is opened lazily when a view is opened, so the start path is unchanged (before #82: 43 / 63 on an equally busy machine). The saving of a cache hit (rows drawn before the network answers) is tested with a gated fake reply (`ViewLoadHandlerCacheTests`), not timed; no screen opens a view yet (#79), so the end-to-end "second start" figure is registered for #91. Memory: at most 32 views times 100 rows in memory per profile (about 50 KB a view), 20 MB on disk; the RSS delta is not measured.

### List scrolling (#79)

| Path | Binary | Dataset | Median | p95 |
|---|---|---|---|---|
| Nav key through `AppReducer` plus `FrameBuilder.BuildStyled` at 120x36 (alternating `Ctrl-d` and `j`) | .NET 10 Release JIT, in process | 10 000 rows loaded in one tab, 2000 timed keys | 0.20 ms | 1.0 ms |

Method: temporary xUnit test (not committed), 200 warm-up keys. It excludes the terminal draw and tmux. The renderer produces only the viewport rows (a test builds a frame over 10 000 rows and checks it holds the window and nothing else); the work per key is the reducer's list copy of one tab and the visible rows. Real-binary check: `fakejira --issues 10000` with the Native AOT binary under tmux, `:jql`, then `j`, `G` and `Ctrl-d` moved the selection, the next page loaded when `G` came within one screen of the end, and `gg` returned to the top. Not measured: Native AOT in-process figures and RSS of a fully loaded 10k tab (#91).

### Columns layout (#85)

| Path | Binary | Dataset | Median | p95 |
|---|---|---|---|---|
| Nav key (`j`, `l`, `Ctrl-d`, `h`, `Ctrl-u`) through `AppReducer` plus `FrameBuilder.BuildStyled` at 120x36 | .NET 10 Release JIT, in process | board of 200 cards, 2500 timed keys | 0.31 ms | 0.56 ms |
| same | same | board of 2000 cards, 2500 timed keys | 0.29 ms | 0.59 ms |

Method: temporary xUnit test (not committed), 500 warm-up keys, four columns, assignee on every card; it excludes the terminal draw and tmux. The renderer draws only the cards in the viewport. Each key and each frame still regroup all rows into the columns (`BoardColumns.Visible`, one dictionary lookup per row; `Arrange` adds a pass over the rows looking for parents and, only when some row has one, a key set and a child map, all linear) and a key looks the selected issue up by key once (linear); that is below the noise up to 2000 cards, so the groups are not cached; boards up to the 10 000-row cap are not measured, and the table predates the border rework and the sub-task grouping (linear passes, not re-measured) (the per-frame cost is still bounded by the viewport). Not measured: Native AOT in-process figures.

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
