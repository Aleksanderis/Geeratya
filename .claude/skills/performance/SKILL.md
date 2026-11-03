---
name: performance
description: Performance and memory checklist for Geeratya hot paths (JSON mapping of search results, ADF rendering, list rendering, request scheduling, caches, startup). Use when you write or review code that maps Jira responses, renders rich text or long lists, schedules requests, caches data, or runs at startup, or when a change touches the numbers in docs/performance.md.
---

# Performance and memory

Targets and verified numbers are in `docs/performance.md`; read it first, do not repeat it here. Budgets: cold start to first frame under 100 ms (from local cache or an empty state, never waiting on the network), a key press never waits on a request, scrolling a 10k-issue result list without dropped frames, background tabs cheap, memory bounded however long the session runs.

## Measure before optimising

- Write the straightforward version first. Optimise only a path you measured. End-to-end against `tools/FakeJira` with fixed injected latency (`scripts/measure.sh <binary> <fake url>`; Windows: `measure.ps1`, default scenario only); for a pure mapper or renderer, an ad hoc timed loop over a large recorded or generated response, with the command in the PR.
- Record before and after numbers (median plus p95, binary type, dataset size, injected latency) in the PR. An optimisation without a number is not accepted, except a simple, readable change on a path whose cost is clearly proportional to data size (per issue, per field, per ADF node, per frame); the PR must then say it was not measured. A change that makes code harder to read for an unmeasured gain is never accepted.
- Measure the Native AOT binary, not `dotnet run`: JIT and AOT numbers differ. Never measure against the dev site: its latency is noise.

## Network is the real cost

- Fewer, smaller requests beat faster parsing: `fields=` limited to what is shown, batch instead of N+1, one page at a time as the user scrolls, prefetch only the next page after a pause and cancel it when the query changes.
- Show what you have: render from cache immediately, refresh in the background, mark stale data as stale. Never blank a view while a refresh runs.
- Coalesce: the same request in flight twice is sent once; a newer query cancels the older one's requests.
- Respect rate limits (`.claude/rules/jira-layer.md`); a 429 storm is a performance bug.

## CPU and allocations

- Deserialize with source-generated `System.Text.Json` straight from the response stream (`DeserializeAsync` or `Utf8JsonReader`); never read a whole response into a string first.
- Map once per response into compact view models; do not re-walk raw JSON or ADF per frame.
- Work proportional to what is visible: virtualize lists, render and wrap only visible rows, render ADF lazily for the opened issue.
- Avoid LINQ, capturing closures and `foreach` over interfaces in per-row and per-frame loops; indexed loops over arrays or lists.
- Hoist invariants: static readonly option objects and field lists, precomputed lookup tables, `static` lambdas in hot code.
- String interpolation stays the house style. Replace it with buffer writes or a cached string only in a per-frame or per-row loop where measurement shows it matters, and say so in the PR.
- Intern or share long-lived repeated strings (status names, user display names, project keys).
- Do not recompute display width per frame for unchanged text; cache it per row or string.

## Caching and laziness

- Lazy by default: load an issue's details, comments or changelog only when shown.
- Every cache has a bound (entry count and a size budget), an eviction rule (LRU) and an owner that drops it (tab closed, profile switched). No unbounded dictionaries keyed by issue key.
- State what invalidates it (the issue's `updated` timestamp, a write the app made, a refresh, a profile switch) and test that stale data is never shown as fresh.
- A disk cache never stores tokens, and is per site and account.
- A cache must save measured time; record its memory cost (RSS delta) next to the gain.

## Rendering

- Frame diffing and the single flush are specified in `docs/architecture.md`; add no per-cell allocations and no work for unchanged rows.
- Invalidate narrowly (the changed pane or row) on selection or data changes, not the whole screen.

## AOT and startup

- No reflection, runtime codegen or `dynamic`; source generators only.
- Startup path: nothing that is not needed for the first frame (no network before the first frame, no field-list fetch, no regex construction, no large static initialisers). Load profiles and cached views, draw, then start requests.
- After adding a dependency or touching anything reflection-adjacent, run the AOT publish (command in CLAUDE.md) and re-measure cold start against `docs/performance.md`.

## Reporting

In the PR body: path touched, how measured (command, dataset, latency, binary), before and after, memory (RSS) delta. If the budget is at risk and unmeasured, register a follow-up issue rather than guessing.
