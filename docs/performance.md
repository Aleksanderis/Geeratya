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

`scripts/measure.sh <binary> <fake url> [runs]` starts the binary in a private tmux server (private `XDG_*` directories), polls `capture-pane` until the first non-blank frame, then toggles the help overlay with `?` and times each change of the frame. It first times a trivial command (`printf x`) the same way, the "tmux floor", so the cost of tmux and of the polling loop can be read off. Results are the median and p95 over all runs, printed with the load average before and after (Linux only: `/proc/loadavg`); do not trust a run when the load is near the core count. Start FakeJira beside it with the dataset and latency you want to record (`fakejira --issues 10000 --latency-ms 50`); until the Jira-backed views exist (#4 transport is merged, views are not) the binary does not talk to the fake, so these first numbers are of the empty shell.

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
