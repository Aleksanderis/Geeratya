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

## How numbers are taken

`scripts/measure.sh <binary> <fake url> [runs]` starts the binary in a private tmux server (private `XDG_*` directories), polls `capture-pane` until the first non-blank frame, then toggles the help overlay with `?` and times each change of the frame. It first times a trivial command (`printf x`) the same way, the "tmux floor", so the cost of tmux and of the polling loop can be read off. Results are the median and p95 over all runs, printed with the load average before and after (Linux only: `/proc/loadavg`); do not trust a run when the load is near the core count. Start FakeJira beside it with the dataset and latency you want to record (`fakejira --issues 10000 --latency-ms 50`); until the Jira-backed views exist (#4 transport is merged, views are not) the binary does not talk to the fake, so these first numbers are of the empty shell.

The harness measures from outside the process, so it includes process start, the first terminal write and tmux's own capture latency (floor below). Treat differences between runs as signal and absolute values as an upper bound.

## Measured

Environment: Linux x86_64, 14 cores, Native AOT `geeratya` published with `dotnet publish src/Geeratya.Tui -c Release`, tmux 120x40, 30 sessions with 5 key presses each, empty shell (the binary does not talk to Jira yet). The harness prints the load average before and after a run; keep it in the table.

| Measurement (ms) | Load (1 min, 14 cores) | Median | p95 | Budget |
|---|---|---|---|---|
| tmux floor (first frame of `printf x`) | 16 | 8 | 17 | n/a |
| Cold start to first frame | 16 (first measurement, #7) | 124 | 173 | under 100 |
| Key to frame (`?` help toggle) | 16 (first measurement, #7) | 207 | 241 | next frame |
| tmux floor | 9 to 12 | 3 to 12 | 16 | n/a |
| Cold start to first frame (re-measured, #37) | 9 to 13 | 41 to 45 | 56 to 65 | under 100 |
| Key to frame (re-measured, #37) | 9 to 13 | 35 to 37 | 45 to 70 | next frame |

The re-measurement ran several times on the same binary with the machine still busy (load 9 to 13, never quiet), and it is far inside the budget: the first figures (124 ms and 207 ms) were an artefact of the load of 16 at the time and are not reproducible. No code change was needed (#37).

Reading the key-to-frame figure: a bare `cat` echoing a typed character, timed with the same loop, takes 10 ms median (p95 20), so 35 to 37 ms is about 25 ms above the harness floor. An in-process trace (temporary timestamps in the key handler and `Iteration` event, not committed) showed the pieces: `tmux send-keys` returns 10 to 20 ms before the key reaches the process (tmux and the pty hand-off), the key handler runs and the next frame is visible to `capture-pane` about 15 ms later, which is mostly the polling loop's own `capture-pane` process spawn. Terminal.Gui's main loop is capped at 25 iterations per second by default (`Application.MaximumIterationsPerSecond`), but a key press wakes the loop at once, so the cap does not delay it: raising the cap to 120 changed neither figure (alternating A/B runs, 30 sessions each, 35 to 41 ms both). The cap is left at its default.

## Notes for later rows

Add a row per measured path: JSON mapping of a 10k-issue search (ad hoc timed loop, command in the PR), list scrolling, ADF rendering of the longest recorded description, memory (RSS) after opening a 10k list. Record binary type, dataset size, injected latency, median and p95.
