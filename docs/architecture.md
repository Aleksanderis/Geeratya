# Architecture

Solution layout, layering, composition, threading and cross-platform rules. Code style is in `.claude/rules/csharp-style.md`; the Jira contract is in `docs/jira-layer.md`.

## Projects

`Geeratya.slnx` holds four source projects and one test project per source project. Shared settings live in `Directory.Build.props` (net10.0, nullable, warnings as errors, `EnforceCodeStyleInBuild`, trim and AOT analyzers), versions in `Directory.Packages.props` (central package management), the SDK pin in `global.json`, formatting in `.editorconfig`. The product version is `Version` in `Directory.Build.props`; `ApplicationInfo.Version` reads it from the assembly.

| Project | Role | References |
|---|---|---|
| `src/Geeratya.Jira` | Jira Cloud REST client: connection, transport, typed models and errors. Knows nothing of Core or the UI. | none |
| `src/Geeratya.Core` | State layer: views, commands, reducer and effects, the cache. Pure, runs without a terminal. | Jira |
| `src/Geeratya.Platform` | Operating-system abstraction: config/state/cache directories, console capabilities, environment lookup. | none |
| `src/Geeratya.Tui` | The `geeratya` executable: composition root, Terminal.Gui rendering, input. The only project that touches the console. | Core, Jira, Platform |
| `tools/FakeJira` | Local fake Jira server over the recorded fixtures (Native AOT compatible); tests in `tests/Geeratya.FakeJira.Tests`. See `docs/testing.md`. | none |
| `tests/Geeratya.<Name>.Tests` | xUnit, one per source project, same folder layout as the code under test. | its source project |

Layering is `Jira -> Core -> Tui`, with `Platform` beside them: Jira never references Core, Core never references Tui, and only Tui touches Terminal.Gui. Core takes platform facts (paths, capabilities) as plain values from the composition root rather than referencing Platform, so the state layer stays testable with literals.

## View model

A view is a source plus a layout (`docs/decisions.md`, 2026-11-01). `ViewDefinition(ViewSourceKind Source, string SourceValue, ViewLayout Layout)` in `Core/Views`: sources are JQL, saved filter, board and backlog; layouts are list and columns. Issue details and every write work the same in every view. A new board kind is a new source or layout, not new loading code. All view state belongs to a tab; nothing is global.

## Views and tabs

Design for M1 (#64). The code issues build on the names below; a name in a table is the name the code uses. Everything is per tab and per site session; there is no static or global "current" anything.

**State shape.** `AppState.Session` (`SiteSession`) holds `IReadOnlyList<TabState> Tabs` (open order), `TabId? Active` and the site-level `SiteLink Link`. `AppState.Sequence` (a `long`, outside the session; `AppState.ViewportRows` next to it holds the last viewport height so a tab opened later starts with it) is the one counter every `TabId`, `RequestId` and recency stamp is drawn from; it only grows and survives a session swap. The placeholder `SiteSession.Views` is gone (#75); `SiteSession.Cache` (the string map) is removed rather than replaced, because the cache is not view state (see "Cache and refresh"). A profile change still replaces the whole session with `SiteSession.Empty(profile)`, which has no tabs.

`TabState` (record, `Core/Views`):

| Member | Meaning |
|---|---|
| `TabId Id` | Opaque id drawn from `AppState.Sequence`, never reused, also not across a profile switch. |
| `ViewDefinition Definition` | Source kind, source value, layout and a title (`ViewDefinition` gains `Title`; for boards the source value is the board id and the title the board name). |
| `IReadOnlyList<IssueSummary> Rows` | Immutable. A page append builds a new array of `Count + page` references; selection and scroll never copy it. The Jira layer's typed models are used directly: no duplicate row DTO in Core. |
| `PageCursor? Next` | Cursor of the next page, null when the last page is loaded (type from #66). |
| `bool Truncated` | `Rows` hit the cap (10 000 per tab); "load more" stops and the status line says so. |
| `int Selected`, `string? SelectedKey`, `int Scroll` | Selection index plus the issue key it points at (the key survives a refresh that moves rows), first visible row. |
| `int VisibleRows` | Last viewport height reported by the host (`ViewportChanged`), so the reducer can tell when the end is near without touching the UI. |
| layout | Lives in `Definition.Layout` (no second copy); `ViewLayoutChanged` changes it and keeps everything else. |
| `LoadState Load` | `Idle`, `Loading(LoadReason)` or `Failed(JiraErrorKind Kind, string Message)`. |
| `long RequestId`, `long LastActiveSeq` | `RequestId` (drawn from `AppState.Sequence`) is the only request whose reply this tab accepts (0 while idle); `LastActiveSeq` is the `Sequence` value when the tab was last active, for the cold-tab choice. |
| `bool IsStale`, `DateTimeOffset? LoadedAt` | Staleness is separate from loading: cached rows shown while the refresh runs are both stale and loading. `LoadedAt` is when the rows last came from the network (for stale cache rows, the cache entry's save time). |
| `int Failures`, `DateTimeOffset? NextRefreshAt` | Backoff and schedule (see "Cache and refresh"). `Failures` counts failed loads in a row (a failed next page does not count); `NextRefreshAt` is `LoadedAt + 60 s` after a success, the backoff time after a failure, null until the first reply. `Failed` also carries the `LoadReason` it failed under, so the refresh key can retry a failed next page. |
| `BoardConfiguration? Board` | The configuration of a board or backlog tab (null for other sources), set by `BoardConfigurationLoaded` or taken from the cached view; kept with the tab (#78). |
| `IReadOnlyList<DetailState> Detail` | Issue-detail back stack (empty while the list is shown); each `DetailState` has its own `RequestId`, load state and stale flag. Filled by #84; the member is added by #84. |
| `IReadOnlyList<IssueSummary>? PendingRows` | Rows gathered by a refresh that needs several pages (boards); swapped in when the last page arrives, so the screen never shows a half-refreshed board. |
| `bool Cold` | The tab gave its rows up (see below); activation reloads it. |

`LoadState` replaces the `Idle/Loading/Loaded/Failed/Stale` list in the first draft of #75: "loaded" is `Idle` with rows, "stale" is the flag. `SiteLink` is `Online`, `Offline(since)`, `RateLimited(until)` or `Rejected`, driven by commands (`LinkChanged`) and failures, not by tab data.

**Tab rules.** Opening a view appends a tab and activates it. Closing the active tab activates its right neighbour, else its left; closing the last tab leaves `Active` null (start screen). Closing emits `CancelTabEffect(TabId)`. A tab keeps selection, scroll and layout while hidden (all in `TabState`); hidden tabs are not rendered, not polled and do no work on `Tick`.

**Memory bound across tabs.** At most 10 000 rows per tab (about 5 MB of `IssueSummary`, roughly 500 bytes each) and 50 000 across tabs, counting `PendingRows` too (a refresh gathers at most the tab cap again, so a board tab can briefly hold twice its rows). When an append or an activation exceeds the total, the hidden tabs with the lowest `LastActiveSeq` go `Cold` until it fits. Going cold drops `Rows`, `Next` and `PendingRows`, sets `RequestId = 0` and `Load = Idle`, and emits `CancelTabEffect`, so a reply still on its way finds no matching id. Definition, layout, selection key and scroll are kept; activation reloads the first page (`LoadReason.Open`: cache first, then network). A cold tab cannot restore a position deeper than its first page: the key is kept, the scroll falls back to the top. This is the cap #81 left to #64.

**Tabs are not persisted in M1.** A start begins with no tabs; opening a view then hits the disk cache. Restoring last session's tabs is an open question in `docs/roadmap.md`.

**New types by issue** (plain records, `Command` or `Effect` subtypes unless noted):

| Type | Kind | Issue | Notes |
|---|---|---|---|
| `TabId`, `TabState`, `LoadState`, `LoadReason`, `SiteLink`, `ViewReducer` | state | #75 | `ViewReducer` is pure, routed from `AppReducer` with one line, like `ProfileReducer`. `LoadReason`: `Open`, `NextPage`, `Refresh`, `Retry`. |
| `ViewOpenRequested(ViewDefinition)` | command | #75 | Opens and activates a tab, returns `LoadViewPageEffect(Reason: Open)`. |
| `TabActivated(TabId)`, `TabClosed(TabId)`, `ViewLayoutChanged(TabId, ViewLayout)`, `ViewportChanged(int Rows)`, `NextPageRequested(TabId)` | commands (all derive from `ViewCommand`, which `AppReducer` routes to `ViewReducer`) | #75, #81 | Activation may return a refresh load. `ViewportChanged` sets `VisibleRows` on every tab (cheap), so a hidden tab never keeps an old height after a resize. |
| `LoadViewPageEffect(TabId, ViewDefinition, PageCursor? Cursor, long RequestId, LoadReason Reason, bool Background)` | effect | #75 | `Background` selects `AsBackground()`. |
| `ViewPageLoaded(TabId, long RequestId, IssuePage Page, DateTimeOffset At)`, `ViewLoadFailed(TabId, long RequestId, JiraError Error, DateTimeOffset At)` | commands | #75 | Applied only if the tab exists and `RequestId` is its current one. |
| `CancelTabEffect(TabId)` | effect | #75 | The host cancels that tab's in-flight work. |
| `BoardConfigurationLoaded(TabId, long RequestId, BoardConfiguration)`, `BoardColumns.Group` (pure helper) | command | #78 | Board and backlog sources (#78). `BoardConfigurationLoaded` is applied only while its request id is current. `BoardColumns.Group(configuration, rows)` (`Core/Boards`) returns `BoardColumnGroups`: one `BoardColumnGroup(Name, Rows)` per configured column in board order plus `Unmapped` (statuses in no column; a status in two columns goes to the first). |
| `CacheKey`, `CachedView`, `SiteCache`, `SiteCaches`, `FilePermissions`, `CacheMapper`, `CacheJsonContext`, `ViewCacheLoaded(TabId, long RequestId, CachedView)`, `WriteCacheEffect(CacheKey, CachedView)`, `CacheWriteFailed(string)` | cache | #82 | `Core/Cache`. `ViewCacheLoaded` is applied only while that load is current and the tab has no rows. `SiteCaches` is the host-owned registry (one `SiteCache` per profile folder); `FilePermissions` carries the user-only folder and file hooks that `Platform/Files/PrivateFiles` implements. `CacheWriteFailed` puts a one-line notice on the status line. |
| `LoadIssueDetailEffect(TabId, string IssueKey, long RequestId, bool Background)`, `IssueDetailLoaded`, `IssueDetailFailed`, `DetailState` | effect, commands | #84 | Same request-id rule per `DetailState`; detail uses the same cache-then-network flow. |
| `Tick(DateTimeOffset Now)`, `RefreshRequested(TabId)`, `LinkChanged(SiteLink)`, `RefreshSchedule` | commands, static timings | #89 | See "Cache and refresh". `LinkChanged` carries the transport's rate-limit wait (`RateLimited(until)`, or `Online` when it ended; `Online` clears only a rate-limit state). The host (`ShellHost`) owns the one-second timer and maps `SiteConnections.RateLimitChanged` to it. |
| `TabStatus.Of(tab, link, now)` | pure function | #90 | Status text from the link (`LinkChanged` is #89, above) and failures. |
| `JiraServices`, `NetworkRunner`, `ViewLoadHandler`, `ShellHost.Post(Command)` | Tui | #57, #77 | See "Loading". |
| `ViewPaging.PageSize` (100), `LoadErrorText.For(JiraError)` (pure message table) | Core | #75, #77 | The table is in Core so the reducer builds `Failed(kind, message)` itself. |
| `ConnectionChecked` (result of #83) | command | #83 | The reducer maps it to `Link`: success `Online`, 401 `Rejected`. |

**Request ids.** The reducer draws `RequestId` from `AppState.Sequence` and stamps it on the effect and on `TabState.RequestId`. A result is applied only if its tab exists and its id equals `TabState.RequestId`; anything else returns the state unchanged. Starting a new load for a tab (refresh, retry, reload after "page expired") supersedes the old id, so an old reply is ignored even when the host could not cancel it in time.

**Profile switch.** `ProfilesChanged` with another active profile (or removal of the active one) replaces `Session` with `SiteSession.Empty` and emits `DropSiteCacheEffect(oldProfile)`, as today. `AppState.Sequence` is kept, so a tab or request id of the old session never matches a tab of the new one. Replies of the old profile are stopped by two checks that cover each other: the reducer (unknown tab or id, which also catches a reply that was already queued to the UI thread before the switch) and the host, which drops a result whose site generation is not current (`SiteConnections.Generation`, see "Loading") before it is posted. This resolves the remaining parts of #57 and #59.

## Composition root

Dependencies are wired by hand in `Program.cs` (through `ApplicationRunner`) and `JiraServices.Create` once the Jira layer exists. No DI container: it needs reflection or code generation and costs startup. Tests build the same records or swap one part with `with` and a fake. `Program.cs` stays a few lines; the logic sits in `ApplicationRunner` so tests drive it without a process. Interfaces exist only where another layer consumes a class and tests need a fake.

## Threading

- The UI thread renders and handles input. It never waits on the network or the file system for long.
- Every Jira call is async, cancellable, has a timeout and honours rate limits; requests run on background tasks and report back as commands into the state layer, which the UI thread applies.
- The first frame is drawn before any network reply (startup target well under 100 ms).

## Loading

Effects stay data (`Effect` records). The code that talks to Jira lives in `Geeratya.Tui`, next to `ProfileEffectHandler`, not in a new project: Core must not reference the host's connections, and the precedent is set. The Jira clients themselves are in `Geeratya.Jira`.

| Type | Project | Issue | Role |
|---|---|---|---|
| `JiraServices` | Tui | #57 | Bundle of clients (search, board, filter, issue) built from `SiteConnections.Current`; no DI container. Not available while no transport exists. |
| `NetworkRunner` | Tui | #57 | Runs network effects on background tasks with a `CancellationTokenSource` per tab; stamps each run with `SiteConnections.Generation` at start; drops the result when `IsCurrent(generation)` is false or the token is cancelled; posts the rest through `ShellHost.Post`. `CancelScope(tabId)` serves `CancelTabEffect`, `CancelAll()` serves `DropSiteCacheEffect`. |
| `ViewLoadHandler` | Tui | #77, #78 | Turns `LoadViewPageEffect` into `ViewPageLoaded`, `ViewLoadFailed` (and `BoardConfigurationLoaded`), scoped to the tab id on the `NetworkRunner`. List sources (typed JQL, `filter = <id>` for a saved filter) share one search path through `ViewQuery.JqlFor` and `JiraServices.Search`; board and backlog go through `BoardClient`. The network call of a first board page (`Open`/`Retry`, no cursor) fetches the configuration first and reports it as `BoardConfigurationLoaded` through `NetworkRunner.StartReporting` (same currency check as the final command), then asks for the issues. A failing configuration is the load failure (no issue request); later pages and refreshes reuse the configuration in the tab. The board id comes from the source value (a non-number fails the tab). `Background` selects `AsBackground()`; user opens use the user lane. |
| `ShellHost.Post(Command)` | Tui | #57 | Thread-safe entry that marshals to the UI thread, where the reducer runs; the only way a background thread changes state. |

Flow: key or `Tick` -> reducer -> `(state, effects)` -> the host draws the state and hands the effects to the runner -> a task calls Jira -> the result becomes a command -> `Post` -> reducer. The first frame is drawn from the initial state before any task exists, and a reply never touches state directly.

**Paging.** A tab holds only the cursor of the next page (`TabState.Next`): the opaque `nextPageToken` for `/search/jql`, the next `startAt` for the agile API (`PageCursor`; jira-layer.md, "Paging models"). Search has no total and tokens only go forward, so there is no jump to row N and a deep scroll pages through every earlier page.

- The next page is requested automatically when `Next` is not null, the load state is `Idle` (a `Failed` tab waits for an explicit retry, so a held key never hammers a failing Jira) and the selection is within one screen of the end (`Selected >= Rows.Count - VisibleRows`). One page in flight per tab: while `Loading`, scrolling asks for nothing; when a reply arrives the reducer checks again and may request the next page.
- Page size 100 (`ViewPaging.PageSize`; #66 records the real cap). At 10 000 rows `Next` is cleared, `Truncated` is set and the status line says "load more stopped at 10 000 rows".
- Board and backlog sources (agile API, offset paging, last page computed from `total`) load all pages one after another until the last page or the cap, because the columns layout needs every card. List sources (JQL, saved filter) load on demand. The reducer requests the next page of such a tab as soon as the previous one arrived (no selection rule, hidden tabs included); a failed page stops the walk until an explicit retry. The 10 000-row cap ends the walk with the same truncation notice. The Kanban board has no per-column paging. The cached first page holds only 100 rows, so a board opened from the cache shows stale partial columns until the walk ends. A page that repeats an issue already held (offset paging shifts when the board changes mid-walk) is skipped by key.
- `SelectionMoved(TabId, Index)` (sent by the list motion, #79) sets the selection and applies this rule; a page reply applies it too, so a viewport taller than the rows loads on without input. Only the active tab asks for more.
- A load that carried a page token and comes back 400 (token expired, jira-layer.md, "JQL search") is not shown as an error: the Jira layer reports it as `PageExpired`, and the reducer restarts at the first page with `LoadReason.Retry` (the old rows stay on screen) and keeps the selection by issue key.

**Stale replies and cancellation.** Closing a tab cancels its work (`CancelTabEffect`); a profile switch cancels everything (`DropSiteCacheEffect` makes the host call `NetworkRunner.CancelAll()`, `SiteConnections.Drop` and drop the cache). Cancellation is best effort; the guarantee is the request-id rule in the reducer plus the generation rule in the host ("Views and tabs").

**No connection.** When `JiraServices` is not available (no profile, no token, still connecting) the handler answers a load at once with `ViewLoadFailed` carrying a `JiraErrorKind.Unexpected` error whose text is a fixed "Not connected to Jira" line (the precise reason, such as a missing token, is the status-line notice from `ConnectionFailed`), so the tab shows an error instead of loading forever; a manual refresh after the connection is up loads it. `NetworkRunner.Start` takes the `whenUnavailable` command for this case: it is posted off the calling thread when no transport exists at start.

**Failures.** A failed load keeps the existing rows, sets `Load = Failed(kind, message)` and marks a tab with rows stale. The message comes from one pure table in Core keyed by `JiraErrorKind` (`LoadErrorText`, #77), so the reducer builds it and it is shared by refresh and the banners (#90); which kinds mean what is in jira-layer.md, "Errors to UI states".

**Network states** (drawn by #90 with the existing `PaletteRole`s; never colour alone, every state also has a text marker). One status per tab, highest first, from the pure `TabStatus.Of(tab, session.Link, now)`:

| Status | When |
|---|---|
| Rate limited | `Link` is `RateLimited(until)` with `until > now`; shows the wait end |
| Offline | `Link` is `Offline`, or the tab failed with `Network`, `Timeout` or `HostRefused`; last data kept |
| Error | `Failed` with another kind (`Unexpected`, `ServerError`, ...): banner with the message and a hint (401 token rejected, see `:profile`; 403/404 not found or no access; 400 Jira's own text) |
| Loading | `Load` is `Loading` |
| Stale | `IsStale`, with the age from `LoadedAt` |
| Fresh | none of the above |

The roles exist from the start (`docs/ux.md`, "Colour tiers"); #76 adds only what a screen body needs.

**Cold start.** The first frame comes from a state with no tabs. Opening a view issues the load; the handler answers from the cache first (memory, then disk, off the UI thread) with `ViewCacheLoaded`, then asks Jira ("Cache and refresh").

## Cache and refresh

**Cache.** Memory plus disk behind one host-owned object, `SiteCache` (`Core/Cache`; it takes its root directory from the composition root, Core never references Platform). It is not part of `AppState`: LRU order and byte counts are infrastructure, not view state, and keeping them out keeps the reducer pure and cheap. The load handler reads it; the reducer asks for writes with `WriteCacheEffect`. The host holds one `SiteCache` per active profile and `DropSiteCacheEffect` disposes it. (This differs from the first draft of #82, which put the memory part in the session.)

- Memory, per session: views by `CacheKey(ViewSourceKind, SourceValue)` (at most 32, LRU) and issue details by issue key (LRU, default 200). The view entry is the first page (`CachedView`: rows, optional `BoardConfiguration`, `SavedAt`).
- Disk: below the cache directory of `ApplicationDirectories`, one folder per profile named by the first 16 hex characters of SHA-256 of `Profile.CredentialKey` (that key contains site and email, so only the hash is used; the token is never involved), holding `view-<hash of CacheKey>.json` and `issue-<hash of issue key>.json`. Cached (#82): the first page of every opened view (at most `ViewPaging.PageSize` rows, so memory is bounded by 32 views times one page); nothing from `Authorization`. Issue details (200 in memory, `issue-<hash>.json`) arrive with the detail flow (#84), which reuses `SiteCache`'s file layer; the memory bound for them is not implemented yet.
- Files are JSON through a Core source-generated context (`CacheJsonContext`) with its own DTOs (`CachedIssueRow`, ...) and a mapper to and from the Jira models, and `{"schema": n}` in every file. A Jira model change then breaks the mapper at compile time instead of silently changing cached files. A schema mismatch, parse failure or foreign file is ignored and deleted, and the data is refetched. The duplication is at this boundary only; core state uses the Jira models directly.
- Writes are off the UI thread, debounced (2 s after the last change), atomic (temp file in the same folder, then replace); a failed write is dropped with a one-line notice, since a cache is never a reason to fail a read. Cap 20 MB per profile folder, oldest files evicted first. Best-effort flush on exit.
- Removal: `DropSiteCacheEffect` disposes the memory part, discards pending writes and cancels work; the disk folder stays, so a view reopens at once when the profile comes back. Removing a profile also deletes its folder: `RemoveProfileEffect`'s handler calls `SiteCaches.Delete(profile)`, which first disposes that profile's `SiteCache` (no pending write or exit flush can recreate the folder) and then deletes the folder. Other profiles' folders are never touched, and two profiles on one site with different accounts have different credential keys, so different folders. (A switch keeping the disk folder differs from #82 and CLAUDE.md wording "drops that site's views and cache": the views and memory cache are dropped, the disk files are kept; `docs/decisions.md`.)
- Permissions: the cache folders and files are created readable by the user only (mode 0700/0600 on Linux and macOS through a Platform helper; on Windows the default ACL of `%LOCALAPPDATA%` already restricts them, an explicit branch). Cached summaries are plain text of the user's own issues.

**Opening a view.** The reducer opens the tab as `Loading` with no rows and emits `LoadViewPageEffect(Open)`. The handler (for `LoadReason.Open` only, never for refreshes or next pages) reads the cache first on a background task through `NetworkRunner.StartLocal`, which needs no connection, so an offline start still shows the cached rows; on a hit it posts `ViewCacheLoaded` and only then asks Jira, so the cached rows always precede a reply or failure; the reducer shows those rows at once with `IsStale` (only if the tab still has no rows and the request id is current). The network reply then replaces the rows, clears `IsStale`, keeps the selection on `SelectedKey` (if the key is gone the index is clamped) and emits `WriteCacheEffect`. A miss shows the loading state. A failed load keeps the cached rows, stale; with no cache the empty body explains the error.

**Refresh rules.** Only the active tab, ever; hidden tabs neither poll nor render.

- Time is injected: a host timer posts `Tick(now)` once a second (the reducer ignores it without an active tab), and commands carry their own `At`. `AppState` stores no "now". The reducer returns the same state instance when a tick changes nothing, and the host skips the redraw when the state reference is unchanged and nothing on screen shows time. Time-dependent text (the rate-limit countdown, the age of stale data at minute granularity) is drawn by the renderer from the `now` the host passes in, and the host redraws on a tick only while such text is visible. An idle tick costs a comparison and at most the small `ReduceResult` record.
- Schedule: refresh when `now >= NextRefreshAt` (`LoadedAt + 60 s` after a success); the next `Tick` decides, the reducer never reads a clock of its own. `TabActivated` carries no time, so it sets `NextRefreshAt = min(NextRefreshAt, LoadedAt + 30 s)` and the next tick (at most a second later) refreshes when that is already past. The manual refresh key (`RefreshRequested`) refreshes unless a load of that tab is in flight and always uses the user lane. Never more than one load in flight per tab.
- Scope: list sources reload the first page only; board and backlog sources reload every page (gathered in `PendingRows`, swapped in with the last one). Rows are replaced as a whole, selection stays on its issue key, scroll is kept; the board configuration is refetched only by a manual refresh. Automatic and activation refresh are skipped for a list tab that has loaded more than the first page (`Rows.Count > PageSize`), because tokens only go forward and replacing the rows would throw the reader's position away every minute; such a tab is refreshed by the manual key, which resets it to the first page (selection kept by key if it is there, else nearest row).
- Priority: automatic and activation refreshes are `LoadViewPageEffect(Background: true)`, i.e. `AsBackground()`, so they never queue ahead of a key press; open, next page, retry and manual refresh use the user lane.
- Pauses: no automatic refresh while `Link` is `RateLimited` (the transport publishes `JiraTransport.RateLimitedUntil` and `RateLimitWaitChanged` on a non-UI thread; the host maps them to `LinkChanged`) until the end passes, nor while `Link` is `Rejected`. While `Offline` the interval gives way to the backoff probe below, which is how recovery is noticed.
- Backoff: after a failure `Failures` grows and `NextRefreshAt = At + min(300 s, 60 s * 2^(Failures-1))` (60, 120, 240, 300 s); a success resets it and sets `Link` to `Online`. A 401 sets `Link = Rejected` and stops automatic refresh until the connection check (#83) succeeds; a manual refresh is still allowed.
- A failed refresh never destroys rows: they stay, `IsStale` is set and the error is shown.
- A failed next page is not a refresh failure: it does not count towards the backoff, blocks automatic refresh of that tab (which would replace the rows under the reader) and is retried by the refresh key. Pages that shift between requests can repeat an issue; an appended page drops keys that are already loaded.

**Bounds.** Every cache and list has a size bound and an owner that drops it (`docs/performance.md`): 10 000 rows per tab, 50 000 across tabs, 32 cached views, 200 cached details, 20 MB of disk per profile. #91 measures them.

## Cross-platform

Linux is built and tested first, then Windows, macOS last. Rules:

- Platform-specific code lives only in `Geeratya.Platform`; each such feature has a branch for every supported OS or an explicit unsupported path.
- Directories: Linux and macOS follow XDG (`XDG_CONFIG_HOME`, `XDG_STATE_HOME`, `XDG_CACHE_HOME`, falling back to `~/.config`, `~/.local/state`, `~/.cache`; a relative XDG value is ignored; with no absolute XDG value and no absolute home, e.g. `HOME` unset, `ApplicationDirectories` throws `InvalidOperationException` naming `HOME` rather than yield relative paths), each with a `geeratya` folder. Windows uses `%APPDATA%\Geeratya` for configuration and `%LOCALAPPDATA%\Geeratya\State` and `\Cache` for the rest. `ApplicationDirectories` computes them from an `OperatingSystemKind`, an `EnvironmentReader` and `PlatformFolders`, so tests cover all three systems on any host.
- Console capabilities (`ConsoleCapabilities.Detect`): colour tier from `NO_COLOR`, `TERM=dumb` and `COLORTERM`; redirected input or output means non-interactive.
- Environment access goes through `EnvironmentReader` with an injected lookup; no direct `Environment.GetEnvironmentVariable` elsewhere.
- Secrets (`Platform/Secrets`): `ISecretStore` per system, picked by `SecretStores.Create`. Linux runs `secret-tool` (libsecret) with the token on standard input, Windows calls Credential Manager (advapi32 `LibraryImport`), macOS uses `UnsupportedSecretStore`; on Linux without `secret-tool`, `LinuxSecretStore` flips `IsAvailable` to false at first use; `TokenProvider` then falls back to `GEERATYA_TOKEN`. Profiles (site, email, no token) are `profiles.json` in the config directory, owned by `Core/Profiles`; the token is looked up by `Profile.CredentialKey`; `GEERATYA_TOKEN` is one value, used only while exactly one profile exists. Core never sees a token or the store.
- Open in browser (`Platform/Browser`): `IBrowserLauncher` via `BrowserLaunchers.Create`, `ProcessBrowserLauncher` runs `xdg-open` (Linux), `open` (macOS) or `rundll32 url.dll,FileProtocolHandler` (Windows, chosen over `cmd /c start` because cmd parses `&` in URLs) through `IProcessRunner` with the URL as one list argument; a missing opener gives "no browser opener found: copy the URL". Core checks links first (`BrowserUrlCheck`: https only, no control characters, no user info, active site host unless a remote link) and the reducer emits `OpenUrlEffect`; the launcher refuses non-http(s) again. `BrowserEffectHandler` in Tui answers with `UrlOpenFinished`.
- Paths use `Path` APIs only: no hardcoded separators, `/dev/null` or `/tmp`.

## Native AOT

`Geeratya.Tui` sets `PublishAot`. All projects have the trim and AOT analyzers on and warnings as errors, so any `IL*` warning fails the build. Run the publish after adding a dependency or touching anything reflection-adjacent: `dotnet publish src/Geeratya.Tui -c Release -o <dir>`, then `<dir>/geeratya --version`. Reading assembly attributes (`ApplicationInfo`) is the one metadata use and is AOT-safe.

## Terminal.Gui

`Geeratya.Tui` references Terminal.Gui 2.5.0 (stable v2, pinned in `Directory.Packages.props`). The native AOT publish has no `IL*` warnings with it and no suppressions are needed.

Layout in `Geeratya.Tui`: `Input/` (`KeyTranslator`: Terminal.Gui `Key` to the renderer-agnostic spelling of `KeyNotation`), `Rendering/` (pure: `FrameBuilder`, `IBodyRenderer` screens, `StatusBar`, `PaletteTable`, `Palette`, `TextWidth`; no Terminal.Gui), `Views/` (`ShellScreen`, the only code that builds views, and `StyledBodyView`, which draws the frame's styled lines span by span in palette colours and redraws it on resize), `ShellHost` (loop, keys to `AppReducer`, state to `ShellScreen.Render`). Core knows no Terminal.Gui types. `FrameBuilder` composes a header region (one row, always the tab bar, blank without tabs), a body from the first `IBodyRenderer` whose `AppliesTo(state)` is true (screens register without editing each other; start and help are built in) and the status bar; `--print-frame` prints the same lines with styles dropped.

Settings and pitfalls:

- Use the instance API: `Application.Create()`, `application.Init()`, `application.Run(window)`; no static `Application.Init`.
- Keys are read from `application.Keyboard.KeyDown` and marked `Handled`, so no view or default binding sees them. The default app key bindings (`Esc` quits) are cleared at start, otherwise `Esc` would quit; quitting is the keymap's decision.
- Letters arrive as upper-case key codes; without `Shift` they are lower-case characters. `KeyTranslator` handles this (tested). A terminal sends `Esc` immediately followed by a key as `Alt-<key>`, as in any terminal program.
- Non-interactive stdin or stdout (pipes, `TERM=dumb`) never starts Terminal.Gui: the binary prints a one-line explanation and exits 1. `--version` and `--print-keymap-doc` work everywhere.
