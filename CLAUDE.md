# Geeratya

Keyboard-first terminal client for Jira Cloud (Kanban board, backlog, issues, JQL, vim-style keys). See README.md for goal and features.

Design docs are in `docs/` (architecture, jira-layer, ux, testing, roadmap, decisions). Work is tracked as GitHub issues grouped by milestone (goals in `docs/roadmap.md`).

## Stack

- C# on .NET 10 (LTS), `net10.0`, SDK pinned in `global.json`. Code in `src/`, tests in `tests/` (layout in `docs/architecture.md`). Published as a single Native AOT binary; it must start in well under 100 ms (first frame before any network reply).
- Jira access: Jira Cloud REST API v3 over `HttpClient`, JSON via `System.Text.Json` source generation. No third-party Jira SDK without a logged decision.
- UI: Terminal.Gui v2 (see `docs/architecture.md` for settings and pitfalls). Tests: xUnit against a fake Jira (recorded JSON fixtures and an in-process HTTP handler); live tests only against the dev site (`testing` skill).

## Rules

- One Jira site is active at a time, chosen from saved profiles (site plus account); switching profiles replaces the connection and drops that site's views and cache. Several views (tabs) over the active site. Still, never assume a global site: every Jira call takes an explicit connection (site URL plus credentials), and all view state is scoped to a tab. No static or global "current site".
- Both project kinds are first class: company-managed and team-managed projects differ in epics, parent links, board settings and fields, and every feature works with both (fixtures for each).
- A view is a source (JQL, saved filter, board, backlog) plus a layout (list, columns). Issue details and every write work the same in every view; a new board kind is a new source or layout, not new loading code.
- Attachments, images and other files are never rendered in the terminal: they show as name, type and size with a link that opens in the browser.
- Never touch a production Jira from development or tests. Automated and agent-run live calls go only to the hosts in `GEERATYA_LIVE_HOSTS` (the owner's free dev site); anything else is refused. Agents never use the owner's company site, not even read-only.
- Credentials: API tokens are secrets. Never log, print, commit, snapshot or put them in fixtures, screenshots, issue or PR text. Read them from the OS secret store or an env var; recorded fixtures are scrubbed (`testing` skill, "Fixtures").
- Cross-platform (Linux, Windows, macOS; macOS lowest priority, Linux is built and tested first). No platform-specific APIs outside the platform abstraction; anything platform-specific gets a branch for every supported OS or an explicit "unsupported" path. Paths via `Path` APIs, no hardcoded separators, `/dev/null` or `/tmp`. Details: `docs/architecture.md`.
- Keep to the Features and "Not included" lists in README.md. Don't add features beyond them without asking.
- Native AOT from day one: no reflection or runtime codegen, every dependency AOT/trimming-compatible; run the AOT publish after adding one (rules in `.claude/rules/csharp-style.md`).
- The Jira layer returns typed results and never touches the UI. The state layer is pure (`(State, Command) -> State` plus effects) and runs without a terminal. Only the rendering layer touches the console. Layering details: `docs/architecture.md`.
- Never block the UI thread on the network. Every Jira call is async, cancellable, has a timeout and honours rate limits (`429` with `Retry-After`).
- Be conservative with writes. Read paths never write. Destructive or hard-to-undo actions (delete an issue or comment, bulk edits, transitions that fire workflow side effects, changing an assignee or sprint for many issues) are confirmed in the UI and say what will change; Jira has no undo, so say so too.
- Code style, naming, folders, static vs instance and AOT rules: `.claude/rules/csharp-style.md`, loaded when you touch `.cs` files. Jira layer rules: `.claude/rules/jira-layer.md`. UI, keys and colour rules: `.claude/rules/ui-changes.md`. Read them before writing code there.
- Scripts (bash and PowerShell) live in `scripts/`; `tools/` holds small C# helper programs (for example the fake Jira server).
- Don't copy code from other projects verbatim (Gitunia included, it is the same owner but a separate codebase: reuse ideas, rewrite the code). Note the source in `docs/decisions.md` only if it shaped a non-obvious decision.

## Git conventions

- Branches: `feature/<issue>-<slug>` for new work, `fix/<issue>-<slug>` for bug fixes. Branch from `main`; when working autonomously, no direct commits to it: changes arrive through merged PRs. Reference the issue in the PR body (`Closes #<n>`).
- Ad-hoc requests from the owner: simple changes (e.g. a `.gitignore` entry, a doc tweak) go straight to `main`, no branch or PR. If it isn't obvious whether a change is simple enough, ask.
- Pushing: never push to any remote without the owner's confirmation. Ad-hoc changes (to `main`, a branch or docs alike) are committed locally; end the task with "committed locally, not pushed" and a one-line summary, and push everything pending in one push once the owner says so (this keeps CI runs down). The autonomous PR flow (agents pushing their `feature/`/`fix/` branches, opening and merging PRs, and `scripts/screenshot.sh publish`/`clean` pushing to the `<repo>-screenshots` repository, plain pushes only) is unaffected. GitHub issue and comment edits are not pushes and need no confirmation.
- Syncing unpushed local commits with `origin/main`: `git fetch` then `git rebase origin/main`. Never `git merge origin/main` or a bare `git pull` (merge commits are for PR merges only); if a merge slipped in, redo it as a rebase.
- Changes that address PR review comments (code, docs, skills, CLAUDE.md alike) all go through a PR branch, never straight to `main`. The straight-to-`main` exception above applies only to standalone requests from the owner.
- After a PR is merged, nothing of it remains: remove its worktree and delete the branch locally and on the remote (spike branches are the exception and stay).
- Agent identity: every commit, PR body and GitHub comment says which agent (orchestrator, implementer, reviewer) produced it: header, signature and hidden marker on GitHub comments; author name or `Agent:` trailer on commits is a soft convention. Details in the `work-task` skill, "Agent identity". Never add the "🤖 Generated with Claude Code" line to PR bodies or comments.
- Commits: Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`, `ci:`), optional scope, imperative subject.

## Which doc to read

Read the doc before working in its area; don't rely on memory of it. Docs that don't exist yet are written in planning or M0.

| Working on | Read |
|---|---|
| Solution layout, layering, rendering, cross-platform | `docs/architecture.md` |
| Performance targets and measured numbers | `docs/performance.md` (and the `performance` skill) |
| REST calls, auth, pagination, JQL, ADF, caching, refresh | `docs/jira-layer.md` |
| Screens, modes, tabs, key bindings, workflows, mouse | `docs/ux.md` (index; links the parts) |
| Any test, fixtures, fake Jira, live dev-site tests | `docs/testing.md` and the `testing` skill |
| Milestone goals and open questions | `docs/roadmap.md` |
| Anything you are about to ask the owner | `docs/decisions.md` first |
| Why a feature is in or out | `README.md` |

## Build, test and lint

Run from the repo root:

- Build (analyzers on, warnings are errors, also the lint step): `dotnet build`
- Test: `dotnet test` (offline; live dev-site tests are opt-in, see the `testing` skill)
- AOT publish (fails on any `IL*` warning): `dotnet publish src/Geeratya.Tui -c Release -o <outdir>`; smoke run: `<outdir>/geeratya`
- Formatting is enforced by `.editorconfig` during build (`EnforceCodeStyleInBuild`).
- Quality inspections the build cannot see (captured disposed or modified variables, unused members, redundant arguments): `scripts/inspect.sh` (JetBrains inspectcode, the same engine as Rider). Run `scripts/inspect.sh --changed` (~10 s) after finishing a change that touches lambdas, events or `using`, and the full run once before opening a PR (the reviewer does the same). The tool is pinned in `dotnet-tools.json`.

## Working autonomously

Follow the `work-task` skill (`.claude/skills/work-task/`) for the procedure. To work a whole milestone or the backlog, follow the `work-orchestrator` skill (`.claude/skills/work-orchestrator/`); `work-readiness` prevalidates a milestone first. They hold the issue order, review, PR, merge, loose-end and owner-feedback procedure. Invariants that hold regardless:

- `spike` and `needs-review` issues are never merged by an agent: spikes stay on unmerged `spike/` branches and close with findings; `needs-review` stops at an open PR for the owner.
- Merge commits only, never squash or rebase, never force-push.
- Every PR carries the reviewer's findings as a PR comment; every owner comment gets an agent-marked reply; loose ends are registered as issues, not left in notes.
- Don't block on questions: decide from the docs or take the recommended default, log it, and say so in the PR.
- Models: use Sonnet for every role, not Opus. Cost is tuned with reasoning effort, set in the skill and agent frontmatter, not repeated in prose.
- Never: spend money or create paid resources/accounts, delete branches you didn't create, touch anything outside this repo and the scratchpad, write to any Jira other than the dev site, or create, edit or delete dev-site data outside the test project the `testing` skill names.
