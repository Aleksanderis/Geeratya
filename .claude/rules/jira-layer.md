---
paths:
  - "src/Geeratya.Jira/**"
  - "tests/Geeratya.Jira.Tests/**"
  - "tools/FakeJira/**"
---

# Jira layer rules

The contract, models, endpoints and refresh design are in `docs/internal/jira-layer.md`; read the section you work in. The rules:

- Every call is connection-scoped: a `JiraTransport` is created per `JiraConnection` (site base URL, account) with its token, and clients take only that transport; a tab holds the transport of its site. No static "current site", no base URL in a static field.
- One transport sends every request (`JiraTransport` over one `HttpClient` per connection). It owns auth headers, `Accept`/`Content-Type`, the user agent, timeouts, retries and rate limiting. No `HttpClient` or `HttpRequestMessage` outside it. The site URL must be https (http only for loopback, for the fake Jira), and the transport never follows redirects (a 3xx is an error), so the credential cannot be sent to another host.
- Credentials: Jira Cloud uses Basic auth with the account email and an API token (scoped tokens go through `api.atlassian.com/ex/jira/<cloudId>`). The token comes from the credential store and lives only in the transport. It never reaches logs, exception messages, `ToString()`, snapshots, fixtures or error text shown in the UI; redact the `Authorization` header in any request dump.
- Every call has a `CancellationToken` and a timeout, and maps the status to a typed error: 401 (bad or expired token), 403 (no permission), 404/410 (gone or hidden), 429 (rate limited), 5xx and network failures. No raw `HttpRequestException` crosses the layer boundary.
- Rate limits: honour `Retry-After` on 429 and 503, back off with jitter, retry only idempotent requests (GET, and POST search) automatically, never a write (the one exception: a 429 that names a `Retry-After`, because Jira did not process it). Cap concurrent requests per site. Background refresh yields to user-initiated requests.
- Ask only for what is shown: always send `fields=` (and `expand=` only when needed), never fetch `*all`. Batch instead of N+1 (JQL search with `key in (...)`, bulk fetch, one changelog page per issue only when opened).
- Boards use the agile API (`/rest/agile/1.0/`, offset paged by `startAt`/`total`, no `isLast`: compute it). Never call `/sprint` on a Kanban board (400); the backlog is whatever `/backlog` returns for the board kind, not a column.
- Search with the enhanced JQL search (`/rest/api/3/search/jql`, `nextPageToken` paging); confirm the endpoint and limits in `docs/internal/jira-layer.md` before adding another. Paginate lazily as the user scrolls; never load a whole project eagerly.
- JQL from user input is passed as typed; JQL the app builds quotes and escapes every value through one JQL builder, never string concatenation.
- Custom field ids (`customfield_NNNNN`) differ per site: never hardcode one. Resolve them per connection from the field list (by schema type or name) and cache the mapping with the connection.
- Everything a project or site can configure is read from Jira, never listed in code: issue types and their hierarchy level, fields and their schema, required fields, allowed values, transitions, statuses, priorities, resolutions, link types, board columns, estimation field, sprint and epic fields. The dev projects are samples, not the model: code reads create/edit/transition metadata (`createmeta`, `editmeta`, `transitions?expand=transitions.fields`) and follows it. A field type the app cannot edit is shown read-only with a browser link, never dropped or crashed on; an issue type or status it has never seen renders by name. Tests include a synthetic fixture with a custom issue type, a renamed status and an unknown field type.
- Rich text is Atlassian Document Format (ADF) in API v3. Parse and render it through one ADF model; unknown node and mark types render as plain text with a marker, never throw.
- JSON uses `System.Text.Json` source generation. Mappers never throw on unknown or missing fields (ignore them and surface a warning) and have fixture tests with recorded responses, odd cases included (null fields, empty ADF, unicode and emoji, deleted users, issues without a sprint, next-gen vs classic projects).
- The Jira layer returns typed results and knows nothing of Core or the UI.
- Read paths never write. Every write (transition, edit, comment, assign, sprint move, delete) is its own method with a name that says so, and is only called from a confirmed command.
- Tests use the fake Jira (`testing` skill). Live calls only in opt-in live tests, only to `GEERATYA_LIVE_HOSTS`; the transport itself refuses other hosts when it runs under test.
