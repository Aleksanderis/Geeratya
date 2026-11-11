# Workflow profile

Facts and policy for the workflow plugin's skills. `CLAUDE.md` and `.claude/rules/` win over this file on any conflict.

## Docs

- roadmap: `docs/internal/roadmap.md` (milestone goals, "Open questions")
- decisions: `docs/internal/decisions.md`
- testing: `docs/internal/testing.md`
- routing: CLAUDE.md "Which doc to read" table; read the rows for the area an issue touches
- retrospectives: `docs/internal/retrospectives.md`

## Commands

Run from the repo root.

- build (analyzers on, warnings are errors, also the lint step): `dotnet build`
- test (offline): `dotnet test`
- test, one project/filter: `dotnet test tests/<Project> --filter <expr>`
- lint / inspect: `scripts/inspect.sh` (`scripts/inspect.sh --changed` after touching lambdas, events or `using`; full run before a PR)
- extra gate when dependencies or project files change: `dotnet publish src/Geeratya.Tui -c Release -o <outdir>` (Native AOT, fails on any `IL*` warning); smoke: `<outdir>/geeratya`
- start/smoke the real app: `scripts/screenshot.sh`, `scripts/measure.sh` against the fake Jira (`tui-smoke-testing`)

## Policy

- default branch: `main`
- branch names: `feature/<issue>-<slug>`, `fix/<issue>-<slug>`, `spike/<issue>-<slug>`
- merge style: merge commits only, never squash or rebase, never force-push (`gh pr merge --merge --delete-branch`)
- `needs-review` issues: `merge` (merge as usual once the reviewer has no blockers; the owner reviews afterwards)
- `spike` issues: never merged by an agent; they stay on unmerged `spike/` branches and close with findings
- autonomy: `autonomous` (the owner must never be the bottleneck; decide from the docs, log it, report afterwards)
- push policy: never push to a remote without the owner's confirmation; ad-hoc changes are committed locally ("committed locally, not pushed"). The landing repository `../Geeratya-landing` is always pushed by agents straight to its `main` (owner, 2026-11-09). The autonomous PR flow (agents pushing `feature/`/`fix/` branches, opening and merging PRs, `scripts/screenshot.sh publish`/`clean`) is exempt.
- CI: `automatic` (ubuntu on every PR and push to `main`, windows on push to `main` and manual dispatch: `gh workflow run ci.yml --ref <branch> -f os=windows`); CI duration: 10 minutes; CI fix attempts: 3. Orchestrator: after each batch and before handing over, check the latest `main` push run (both jobs); while it is red, fix it first (one agent, `fix/` branch) and do not hand over a red `main` (owner, 2026-11-11: Windows on `main` stayed red unnoticed from #959 to #981)
- labels: `needs-owner` (only the owner can settle it), `owner` (owner wrote or commented), `windows-vm` (needs a Windows VM; the orchestrator never picks it), `blocked-upstream` (waits on an upstream fix or release; never picked), `tracking` (milestone tracking issue, never work), `spike`, `needs-review`
- every issue has a milestone (`gh issue create -m "<milestone>"`); bugs, follow-ups and loose ends go to the rolling "M9 - Ongoing improvements" unless they belong to an open goal milestone; a blocking condition is a label, never a milestone (`docs/internal/roadmap.md`, "After M8")
- commit message format: Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`, `ci:`)
- models: Sonnet for every role, never Opus
- context limit: 150000

## Hot paths

JSON mapping of search results, ADF rendering, list/board rendering, request scheduling, caches, startup (well under 100 ms). Budgets and measured numbers: `docs/internal/performance.md`.

## Agent run-time rules

- Live Jira calls only to hosts in `GEERATYA_LIVE_HOSTS` (the dev site); never the owner's company site, not even read-only.
- Never log, print, commit or snapshot API tokens; fixtures are scrubbed.
- Verify locally (build, tests) first, open the PR ready, push once, then wait for the CI run of the push and fix it if red (never merge a red PR); no empty commits, no comment-only nits; skip screenshots without a visual change.
- Windows CI before merge: a PR that touches file I/O, storage, caches, locks, processes, paths, timing or anything under the platform abstraction also dispatches a Windows run on its branch (`gh workflow run ci.yml --ref <branch> -f os=windows`, one watch) and merges only when that run is green too; PR checks run ubuntu only.
- GitHub API budget (the account has 5000 REST calls an hour, shared by every agent and session and by the Gitunia runs; `gh run watch` polls every 3 s by default and ran the account dry on 2026-11-10): wait for CI with exactly one `gh run watch <id> --exit-status -i 60` per run, never several watchers for one run and no polling loops of `gh run list` / `gh pr checks`; run `gh api rate_limit` (free) before starting a wait and stop with a report instead of retrying when `remaining` is under 200; save a failed log once with `gh run view <id> --log-failed > file` and read the file.
- Never delete branches you did not create; never touch anything outside this repo and the scratchpad (except reading `../Gitunia`).
- Fix in the same PR, don't register a follow-up (owner, 2026-11-11): reviewer findings of any severity, missing or weak tests, docs left stale by the change, edge cases and bugs in the code you touch, small performance wins on the same code path, small refactors the change needs (file splits, naming, dead code). Register a follow-up issue only when the work is big (roughly more than the issue itself), sits in another area or files unrelated to the issue, needs an owner decision, or needs a Windows VM; say in the PR why each follow-up was not fixed in place.

## Self-review additions

See `.claude/rules/csharp-style.md`, `.claude/rules/jira-layer.md` and `.claude/rules/ui-changes.md`; layering Jira layer -> Core reducers -> `Geeratya.App` effects -> `Geeratya.Tui`; no `.cs` file over about 500 lines in `src/`.

## Review checklist

`.claude/review-checklist.md`

## Visual verification

UI PRs attach one capture via the `screenshots` skill (`scripts/screenshot.sh`, hosted in the `<repo>-screenshots` repository); run `scripts/screenshot.sh clean` after merging.
