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

`scripts/measure.sh <binary> <fake url> [runs]` starts the binary in a private tmux server (private `XDG_*` directories), polls `capture-pane` until the first non-blank frame, then toggles the help overlay with `?` and times each change of the frame. It first times a trivial command (`printf x`) the same way, the "tmux floor", so the cost of tmux and of the polling loop can be read off. Results are the median and p95 over all runs. Start FakeJira beside it with the dataset and latency you want to record (`fakejira --issues 10000 --latency-ms 50`); until the Jira-backed views exist (#4 transport is merged, views are not) the binary does not talk to the fake, so these first numbers are of the empty shell.

The harness measures from outside the process, so it includes process start, the first terminal write and tmux's own capture latency (floor below). Treat differences between runs as signal and absolute values as an upper bound.

## Measured

Environment: Linux x86_64, 14 cores, Native AOT `geeratya` published with `dotnet publish src/Geeratya.Tui -c Release`, tmux 120x40, 30 sessions with 5 key presses each, FakeJira with `--issues 10000 --latency-ms 50` running (idle). The machine was busy (load average 16 on 14 cores, other agents building), so the figures are pessimistic.

| Measurement (ms) | Median | p95 | Budget |
|---|---|---|---|
| tmux floor (first frame of `printf x`) | 8 | 17 | n/a |
| Cold start to first frame, empty shell | 124 | 173 | under 100 |
| Key to frame (`?` help toggle) | 207 | 241 | next frame |

An earlier, quieter 10-session run gave a cold-start median of 91 ms (p95 185) and key-to-frame 207 ms (p95 253). Cold start is therefore at the edge of the budget with an empty shell and key-to-frame is well above what a local action should cost; both are registered as follow-up #37. The cause is not investigated here: the numbers only exist to give later changes a baseline.

## Notes for later rows

Add a row per measured path: JSON mapping of a 10k-issue search (ad hoc timed loop, command in the PR), list scrolling, ADF rendering of the longest recorded description, memory (RSS) after opening a 10k list. Record binary type, dataset size, injected latency, median and p95.
