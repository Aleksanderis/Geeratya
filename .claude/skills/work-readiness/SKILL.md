---
name: work-readiness
description: Check whether a Geeratya milestone is ready to start (issue quality, blockers, open decisions, missing prerequisites, file overlap, stale docs) and post a readiness report. Use before work-orchestrator starts a milestone, or when asked to prevalidate, groom or refine the next phase. Read-only except for issue edits the owner approves.
model: sonnet
effort: high
---

# Milestone readiness

Invoke as `/work-readiness <milestone>` (for example `M<n>`; `<n>` is a placeholder, not a default). Without an argument, do not guess: list the milestones with their open and closed counts, take the earliest milestone that still has open issues (an unfinished earlier milestone means that one is the target, or the verdict for a later one is `not ready`), and say which one you chose and why in the report. Resolve the milestone title with `gh api repos/{owner}/{repo}/milestones --jq '.[]|"\(.number) \(.title)"'`; goals are in `docs/roadmap.md`). Do the whole job in this one session, cheaply: read GitHub and only the docs and code areas the milestone touches. Check the previous milestone first: if it still has open issues that are not `needs-owner` or `windows-vm`, the next milestone is `not ready`, whatever its own quality. Do not implement anything and do not launch implementation agents.

## 1. Gather

- All issues of the milestone: `gh issue list --milestone "<title>" --state all --json number,title,state,labels,body`, plus the issues they reference in `Blocked by:` lines.
- Open PRs, in-flight worktrees and branches (`gh pr list`, `git worktree list`, `git branch -a`).
- The milestone's section in `docs/roadmap.md` (goal and "Done when") and its open questions; `docs/decisions.md` entries about the area; the workflow rows in `docs/ux-workflows.md` the milestone relies on.
- Every screenshot in an issue body or comment: download and look at it before judging scope or conflicts with decisions (command in `work-task`, step 1). Images are readable; never judge a UX issue from its text alone.
- For each issue, `ls`/`grep` the code it will build on (do not read whole files) to see what exists.

## 2. Check

1. **Issue quality:** each issue has a clear scope and an `Acceptance:` line that can be tested; a `Blocked by:` line that is consistent (every blocker exists, none is a cycle, closed blockers are not listed as open); correct labels (`spike`, `needs-review`); a milestone. Also list open issues of the whole repo that have none (`gh issue list --state open --json number,title,milestone --jq '.[]|select(.milestone==null)'`) and assign each to the milestone it belongs to.
2. **Owner-gated work:** list `needs-review` issues (merged by agents, reviewed by the owner afterwards, never a blocker) and `spike` issues (never merged).
3. **Questions:** answer every open issue labeled `question` in the issue itself (agent-marked comment, see `work-task` step 2), after checking the docs and the code; the report only links the answers.
   **Open decisions:** open issues labeled `needs-owner` (read the comments: if the owner answered, act and remove the label as `work-task` describes), open questions in `docs/roadmap.md` and unsettled choices in `docs/decisions.md` that this milestone depends on. For each, propose a default and say what changes if it is wrong, so agents are not blocked mid-run.
4. **Prerequisites and assumptions:** what each issue assumes about existing code versus what exists; missing pieces; unwired work from earlier milestones (follow-up issues such as session or clipboard wiring) that this milestone needs or that could break it.
5. **Overlap:** which issues touch the same files, shared contracts or keymap rows (check `keymap.default.json`), so the orchestrator knows which can run together (max 3 agents) and in what order.
6. **Docs:** design issues still to be written (flows in `docs/ux-workflows.md`), docs that are stale for this area, keymap rows that will be needed, and `docs/decisions.md` entries longer than 3 lines or holding implementation detail (move the detail to the area doc).
7. **Size and cost:** issues too big for one agent run (propose a split by feature, never by layer, see "Planning"), and which large docs agents will have to load (suggest the sections to name in their prompts).
8. **Loose ends:** known limits, deferred work and manual checks from earlier milestones that exist only in `docs/decisions.md` or `docs/history/decisions.md`, PR comments or code comments (grep `docs/*.md` and `docs/history/*.md` for 'known limit', 'follow-up', 'not wired', 'TODO'); each must be an issue or part of one. Register missing ones (new issue or edit an existing one) and list them in the report.
   **Windows (`windows-vm`) checks:** open one only for behaviour that is platform specific: credential store, Native AOT, publishing, packaging, the browser launcher, paths, console or terminal APIs. Never for UI, rendering, layouts, keys or dialogs (Terminal.Gui draws them the same on every OS); the owner decided this on 2026-11-02 after the M1, M3 and M4 Windows checks turned out to be pure UI. Do not copy the M0 Windows issue as a template for a milestone.
9. **Environment:** the build, tests and AOT publish are green on `main`; local build and tests are green (CI runs on tags, weekly and by dispatch; run it if the last run is stale); nothing from the previous milestone is half-merged (stray worktrees, branches, open PRs awaiting the owner).

## Planning (milestone with a goal but no issues)

When asked to plan a milestone, create its issues from the roadmap goal, then run the checks above. Rules (owner, 2026-11-02, after a layer-per-issue plan was rejected in Gitunia):

- Slice vertically: every issue is a feature a user can see and a release can ship on its own, carrying its own Jira client call, Core state, view, keymap rows, tests and docs. Never one issue per layer (Jira client / Core / TUI / keys / docs) or per kind of work.
- Test each issue: "if only this one merged, would the release have a working feature?" If not, merge it into the slice it belongs to.
- Order slices so the smallest useful one comes first; later slices extend it (options, refinements, extra entry points) and list it in `Blocked by:`.
- Only cross-cutting work gets its own issue: performance on large result sets, Windows/macOS checks, a final docs consistency pass if needed. Docs and keymap rows travel with their feature. Both project kinds (company- and team-managed) are covered inside each slice.
- Changing the scope of a slice or planning by layer needs the owner's approval.
- Create a `tracking` issue and post the plan there; give each issue an `Acceptance:` line and a milestone.

## 3. Report

Post one comment on the milestone's tracking issue (find it with `gh issue list --milestone "<title>" --label tracking --state open`; if missing, create it with `gh issue create --title "<milestone title>: tracking" --label tracking --milestone "<title>"` (create the `tracking` label first if it does not exist) and a one-line body saying it holds readiness reports and orchestrator status), and tell the owner the link: verdict `ready`, `ready with conditions` or `not ready`.

The comment and any issue you create carry the agent identity (header, `<!-- agent: readiness -->` marker, signature; see "Agent identity" in `work-task`). Write the comment for a human first. Open with a plain-language summary of at most 8 lines, no file paths and few issue links: a bold `**Verdict:** ...` line (the orchestrator looks for "Verdict:"), what is left in the milestone in one sentence, what the owner must decide or do (with the proposed default), and the next step. Put everything for the orchestrator inside one collapsed `<details><summary>Details for the orchestrator</summary>` block: environment status, issue edits applied, overlap, suggested batches in order (up to 3 independent issues each, with the reason), risks, cost notes and the doc sections to name in prompts. Format for GitHub: one item per line, never several numbered items in one paragraph; decisions and options are a numbered list with a blank line before it, and a blank line separates every bold heading from its text. Every issue or PR the owner has to look at, decide or check is a full URL (`https://github.com/<owner>/<repo>/issues/<n>`, from `gh repo view --json url`), in the comment and in the message to the owner: a bare `#<n>` is not clickable in the terminal. Keep the details block under about 25 lines and the whole comment under about 50, and link instead of quoting. After the owner answers, edit the comment (`gh api -X PATCH repos/{owner}/{repo}/issues/comments/<id>`) so it shows the current verdict, not the first draft.

## 4. Fix what you may

- Issue-body fixes that are mechanical (a wrong `Blocked by:` line, a missing label) may be applied directly and listed in the report.
- Anything that changes scope, splits an issue or answers a decision waits for the owner's approval. Log accepted defaults in `docs/decisions.md` or `docs/roadmap.md` only when the owner approves.

## 5. Hand over

End with: the verdict, the decisions the owner must make, and "run `/work-orchestrator <milestone>` when ready". The orchestrator will refuse to start on a `not ready` verdict and starts without asking again on `ready` when the report is less than a few days old.
