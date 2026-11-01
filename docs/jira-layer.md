# Jira layer

Findings about how Jira Cloud behaves, from the survey on the dev site (2026-11-01). Every statement names the fixture it rests on; fixtures live under `tests/Geeratya.Jira.Tests/Fixtures/` (paths below are relative to it) and are recorded with `scripts/record-fixture.sh` and scrubbed by `scripts/scrub-fixture.py`.

This is part 1 of the survey (identity, projects, search, filters, errors). Boards, backlog, ranking, hierarchy, create/edit metadata, transitions, ADF and links are part 2 (#14) and get their own sections. The layer contract, models and refresh design are written when the Jira layer is built.

Dev site facts that shape the fixtures: two sandbox projects, `GCM` (company-managed, `style: classic`) and `GTM` (team-managed, `style: next-gen`). The API token's account is a site admin but has no "delete issues" permission in `GCM` (see Errors).

## Authentication and rate-limit headers

- Basic auth, `email:api-token`, on `https://<site>.atlassian.net/rest/api/3/...`. `Accept: application/json` is enough for reads.
- Every response carries rate-limit headers, also on 200: `ratelimit-policy: "jira-burst-based";q=100;w=1`, `ratelimit: "jira-burst-based";r=<remaining>;t=<reset seconds>`, `x-ratelimit-limit: 350`, `x-ratelimit-remaining`. Source: `myself/myself.meta.txt` (status line and these headers). Reading them to throttle before a 429 happens is deferred (#22).

## Transport (`src/Geeratya.Jira/Transport`)

- `JiraTransport` is created per connection (`JiraConnection`: site URL plus account email) with a `JiraApiToken` and owns one `HttpClient`, the auth header, `Accept`, the user agent, the per-attempt timeout (30 s default) and a per-site concurrency cap (4). Clients such as `JiraUserClient` take the transport; all view state stays in the tab, so one transport serves every tab of the active site.
- Calls return `JiraResult<T>` (value or `JiraError` with `JiraErrorKind`): 400 `BadRequest`, 401 `Unauthorized`, 403 `Forbidden`, 404/410 `NotFound`, 429 `RateLimited`, 5xx `ServerError`, plus `Timeout`, `Network`, `HostRefused`, `InvalidResponse`. Caller cancellation is the one exception that surfaces as `OperationCanceledException`.
- Retries (up to 3) only for idempotent requests (`JiraRequest.Get`, `PostSearch`) on 429, 5xx except 501, and network failure; never on a timeout and never for `JiraRequest.Write`. The wait is `Retry-After` (seconds or HTTP date) or exponential backoff with jitter, capped at 60 s. The 429 path rests on the documented semantics only (#18).
- The token is held only in the transport. `JiraApiToken.ToString()` is `[redacted]`, error messages are scrubbed of the token and the Basic credential, and a network failure message carries only the `HttpRequestError` code.
- `GEERATYA_LIVE_HOSTS` is parsed by `JiraHostAllowList`; a transport given `AllowedHosts` refuses any other host before sending (`HostRefused`).

## Current user: `GET /rest/api/3/myself`

Fixture: `myself/myself.json`.

- Returns `accountId`, `accountType` (`atlassian`), `active`, `displayName`, `emailAddress`, `locale`, `timeZone`, `avatarUrls`, `self`. `expand=groups,applicationRoles` is advertised but the lists come back empty with only a `size`; do not rely on them.
- Use it as the credential check when a profile is added or switched: 200 means email and token are good, 401 means not (see Errors).
- `emailAddress` can be hidden by the account's privacy settings; treat it as optional.

## Projects: `GET /rest/api/3/project/search`

Fixture: `projects/project-search.json`.

- Paged with `startAt`/`maxResults` and `isLast`/`total` (the classic offset paging, unlike search). The requested `maxResults` was echoed (50); the cap was not probed.
- Project kind is told by `style` (`classic` = company-managed, `next-gen` = team-managed) and the boolean `simplified` (`false` / `true`); both are present on every project in the list, no `expand` needed. `projectTypeKey` is `software` for both here; business and service-desk projects use other values.
- `projectCategory` is present only when the project has one (GTM has `Dev`, GCM none): optional.
- `entityId` and `uuid` are the same GUID. `avatarUrls`, `properties` and `description` may be empty.
- Both kinds have the same issue-type names (Epic, Story, Task, Bug, Sub-task/Subtask) but different ids; team-managed issue types additionally carry `entityId` in issue payloads (`search/search-page2.json`, GTM issues; absent on GCM issues).
- Quirk: a team-managed project's statuses have their own ids, and a status name need not match its category (GTM's `To Do` has category `In Progress` in `search/search-page2.json`); never infer the category from the name.

## JQL search: `GET|POST /rest/api/3/search/jql`

Fixtures: `search/search-page1.json`, `search-page2.json`, `search-page3-last.json` (three pages of seven issues across both kinds with `maxResults=3`), `search-post.json`, others as named below.

- Paging is by opaque token. A page has `issues`, `isLast` and, unless last, `nextPageToken`; the last page has no token (`search-page3-last.json`). Pass the token back as `nextPageToken` with the same `jql`, `fields` and `maxResults`. The token is long (about 120 characters) and embeds the query; it expires (a bad or expired one is 400, `errors/search-400-bad-token.json`). There is no `total` and no `startAt`.
- Count: `POST /rest/api/3/search/approximate-count` with `{"jql": ...}` returns `{"count": n}` (`search/approximate-count.json`). Approximate in general; use it only for a "about N" label.
- POST takes the same parameters as a JSON body (`jql`, `maxResults`, `fields` as an array, `nextPageToken`) and pages the same way (`search/search-post.json`). Use it when the JQL is too long for a URL.
- `fields` matters: with none, each issue is `{"id": "..."}` only, not even the key (`search/search-default-fields.json`). With `fields=summary` an issue is `id`, `key`, `self`, `expand` and the field (`search/search-maxresults-5000.json`). `fields=*navigable` returns 76 fields on this site, mostly `customfield_*` ids (`search/search-fields-navigable.json`); never send it, ask for what is shown. Custom field ids are per site (rule in `.claude/rules/jira-layer.md`).
- `expand` on each issue is a string listing what can be expanded, not data; ignore it.
- A requested but empty `assignee` is `null` (`search/search-page1.json`); `parent` is absent, not `null`, when the issue has none (`search/search-page2.json`, `search/search-fields-navigable.json`).
- Without `ORDER BY` the order is not creation order (`search/search-maxresults-5000.json` lists newest first for one project); always send an explicit `ORDER BY`. Ordering across both project kinds works (`ORDER BY created ASC`, `search/search-page1.json`; `DESC` in `search/search-order-desc.json`).
- `key in (A-1, B-2, A-9999)` returns the issues that exist and silently skips the missing key, no error (`search/search-key-in.json`). Good for batch fetches; detect missing keys by diffing.
- JQL must be bounded: an empty `jql` or one with only `ORDER BY` is 400 `Unbounded JQL queries are not allowed here. Please add a search restriction to your query.` (`search/search-unbounded-jql.json`, `search/search-order-only.json`). The app must add a restriction (for example a project) before sending.
- Unknown project or field in valid JQL is not an error here: `200` with no issues, re-checked live with `jql=project = NOPE`, `jql=nosuchfield = 1` and `jql=project = GCM AND nosuchfield = 1` (all with `fields=summary`) (`search/search-unknown-project.json`, `search/search-unknown-field.json`). Only a syntax error is a 400 (below). So "no results" cannot be told from a typo in a project key; the UI should not claim the project exists.
- The old `GET /rest/api/3/search` is gone: 410 with a message pointing to `/search/jql` (`search/search-legacy-removed.json`). Do not use or fall back to it.
- `maxResults`: the survey data (seven issues) cannot show the cap; `5000` was accepted without error (`search/search-maxresults-5000.json`). The documented maximum is not verified; page sizes the app uses (tens) are far below any cap.
- There is no jump to row N: tokens only go forward, so a deep scroll pages through every earlier page.

## Saved filters

Fixtures: `filters/filter-search.json`, `filters/filter-my.json`, `filters/filter-get.json`, `filters/filter-favourite-empty.json`, `filters/filter-get-missing.json`, `search/search-by-filter.json`.

- `GET /rest/api/3/filter/search?expand=jql,description,owner` is offset-paged like projects (`startAt`, `maxResults`, `total`, `isLast`, `values`). `expand=jql` is needed to get the JQL; `owner` to get the owner.
- `GET /rest/api/3/filter/my?expand=jql` returns a bare array of the caller's filters, with `favourite`, `isWritable`, `sharePermissions`, `sharedUsers`, `searchUrl`, `viewUrl`, `approximateLastUsed`. `GET /rest/api/3/filter/favourite` returns a bare array (empty here).
- The two filters on the dev site are the ones Jira created for the boards ("Filter for GCM board" with JQL `project = "GCM" ORDER BY Rank ASC`); a board's filter is an ordinary filter and shows up in these lists.
- Running a filter is plain JQL: `filter = 10001 ORDER BY created ASC` through `/search/jql` (`search/search-by-filter.json`).
- A filter you cannot see or that does not exist is 404 with `The selected filter is not available to you, perhaps it has been deleted or had its permissions changed.` (`filters/filter-get-missing.json`).
- The survey did not create filters (writing filters is outside the sandbox rule); creating, sharing and editing filters is out of scope for the app anyway.

## Errors

Fixtures under `errors/`. Statuses were all recorded live unless marked.

| Case | Status | Body | Fixture |
|---|---|---|---|
| JQL syntax error | 400 | `{"errorMessages": ["Error in the JQL Query: ... (line 1, character 11)"], "errors": {}}` | `errors/search-400-bad-jql.json` |
| Invalid query parameter | 400 | Problem-details shape: `{"detail", "instance", "status", "title", "type"}`, no `errorMessages` | `errors/search-400-bad-maxresults.json` |
| Bad or expired `nextPageToken` | 400 | `errorMessages` | `errors/search-400-bad-token.json` |
| Field validation on write | 400 | `{"errorMessages": [], "errors": {"<field>": "<message>"}}` | `errors/create-400-bad-issuetype.json`, `errors/create-400-unknown-field.json` |
| Missing request body fields | 400 | `errorMessages` ("one of 'fields' or 'update' required") | `errors/create-400-missing-summary.json` |
| Bad token or unknown email | 401 | Plain text `Client must be authenticated to access this resource.` (not JSON); header `www-authenticate: OAuth realm="https%3A%2F%2F<site>"` | `errors/myself-401-bad-token.json`, `errors/myself-401-bad-email.json` |
| No permission | 403 | `errorMessages` ("You do not have permission to delete issues in this project.") | `errors/issue-delete-403-no-permission.json` |
| Missing XSRF header on attachment upload | 404 (not 403) | Plain text `XSRF check failed` | `errors/attachment-xsrf-404.json` |
| Issue missing or hidden | 404 | `errorMessages` ("Issue does not exist or you do not have permission to see it.") | `errors/issue-404.json`, `errors/issue-404-unknown-project.json` |
| Removed endpoint | 410 | `errorMessages` | `search/search-legacy-removed.json` |
| Server failure | 500 | HTML page ("Something went wrong"), not JSON | `errors/500-html-error-page.html` |

Consequences for the mapper:

- Three body shapes exist: `{errorMessages, errors}`, problem-details JSON, and non-JSON text or HTML. The error mapper must try JSON and fall back to a trimmed text snippet (never the raw HTML in the UI). Status decides the typed error, the body only supplies the message.
- 404 and 403 cannot be told apart for hidden things: an issue you may not see is 404, with the same message as a missing one. Say "not found or no access".
- 401 has no JSON body; do not parse it. A wrong email and a wrong token look the same.
- `403` on delete was recorded as a side effect: the dev account cannot delete issues in `GCM` while it can in `GTM` (permission schemes differ between the kinds). Tests that clean up must handle a 403 on delete.
- **429 was not recorded.** The survey sent 120 parallel reads and stayed under the burst limit (all 200); a heavier burst was not run against the dev site on purpose. Per Atlassian's documentation a 429 carries `Retry-After` (seconds) and, for burst limits, `ratelimit-reason`/`x-ratelimit-*` headers; this is unverified here, and `Retry-After` must be parsed as either seconds or an HTTP date. The 429 test must script the response on the fake handler until a real capture is available (#18).

## Fixture inventory and scrubbing

- Scrubbing: host becomes `example.atlassian.net`, account ids `fake-account-N` (also inside URLs, encoded or not), emails `userN@example.com`, display names `Test User N`, `avatarUrls` emptied, time zones `UTC`. Fixtures are recorded with sorted keys and 2-space indentation so diffs stay small.
- Rate-limit headers and the status line are not part of the fixture files; `RECORD_META=<file>` of the script writes them (stored once: `myself/myself.meta.txt`). The status of an error fixture is in the Errors table; the `search/search-*` fixtures with no status in their name are 200 unless the table says otherwise (`search-unbounded-jql` and `search-order-only` 400, `search-legacy-removed` 410).
- Recording guards: writes are refused outside issues of `GCM`/`GTM`; output that still contains another `*.atlassian.net` host, the token or the email is not written.
- Fixtures were recorded without keeping the request; the requests are given in prose above. Keys are sorted by the scrubber, so property order is not Jira's.
- `search/search-fields-navigable.json` is large on purpose (it is the only fixture showing the full field set of one issue, including the per-site `customfield_*` ids); trim it if a test needs less.
- Issues created for the survey carried the label `geeratya-test-survey3`; the three in `GTM` were deleted, the four in `GCM` could not be (403) and remain until the owner removes them or grants the permission (#19).
