---
name: work-task
description: Deliver one Geeratya GitHub issue end to end (pick, branch, implement, test, review, PR). Use when asked to work on the next issue or a specific issue number (the work-orchestrator skill runs it through the backlog).
model: sonnet
effort: medium
---

# Work an issue

## 1. Pick

- If given an issue number, use it. Otherwise take the lowest-numbered open issue in the earliest open milestone that is unblocked: `gh issue list --state open --json number,title,milestone,body`.
- Unblocked means every issue in the body's `Blocked by:` line is closed (`gh issue view <n> --json state`). That line is the single source of truth for order. If it is missing or forms a cycle, fix the issue body and say so in the PR.
- Issues awaiting the owner's merge don't block you: continue with any other unblocked issue. Stop and summarize only when nothing is unblocked.
- Read the issue, including every screenshot in it: download each `https://github.com/user-attachments/assets/<id>` with `curl -sSL -H "Authorization: Bearer $(gh auth token)" -o <scratchpad>/img/<name>.png <url>` and open it with the Read tool (the owner's screenshots are often annotated and carry the real intent; images can be read, never say otherwise). Then the docs in `CLAUDE.md`'s routing table for the area it touches. If a doc contradicts the issue's Acceptance line, the issue wins; fix the doc in the same PR.

## 2. Unblock without asking

- Settle questions from the docs, `docs/decisions.md`, issue comments or a sensible default; note the assumption in the PR.
- If a real owner decision is needed, add it to "Open questions" in `docs/roadmap.md` (or open an issue labeled `needs-owner`) with your recommended default and what changes if it is wrong, then continue with the default or move to another issue. Don't wait.
- Issues labeled `question` are answered in the issue itself, as an agent-marked comment (the answer, with the reason and the doc or code it rests on), not only in a report; if the answer implies work, the question issue itself becomes the task: retitle it to the work, replace `question` with `enhancement`, `bug` or `spike`, and add an `Acceptance:` line in the comment. Keep the issue open until that work is merged or the owner closes it.
- Owner feedback is never dropped. Every owner comment (issue, PR, inline review comment, review) gets an agent-marked reply that states its fate: `Fixed in #<PR>` (what changed), `Registered as #<issue>` (when it is deferred, with why), or `Not done: <reason>` (an argument, not silence). Reply before you close the issue or merge the PR, and quote the owner's point in one line so the reply reads alone. Check with `.claude/skills/work-orchestrator/scripts/owner-feedback.sh unanswered`: nothing you touched may stay listed. A marked reply after the owner's comment is what settles it; there is no other state, and a newer owner comment reopens it. The PR body lists the owner comments it addresses under "Owner feedback". The issues and PRs the owner wrote or commented on carry the `owner` label (set by the orchestrator), so they can be found with `label:owner`.
- Label for the owner: `needs-owner` marks anything only the owner can settle: a decision (now or for a later milestone) or a manual check only the owner can do (a real terminal, clipboard). State in the issue what is asked, the recommended default and whether work is blocked. The owner answers in a comment and does not touch the label; the label is not removed automatically either. An issue counts as answered when its latest comment has no `<!-- agent:` marker (agents and the owner post from the same GitHub account, so the marker is the only way to tell them apart). The next agent that sees such an issue acts on the comment (implement, log it in `docs/decisions.md`, or close the issue) and replies with an agent-marked comment saying what it did; that reply makes the issue "unanswered" again, so it is not picked up twice. A later owner comment re-triggers it. Close the issue when the answer settles it ("good enough", "leave as is").
- If the same failure persists after three different attempts, comment on the issue with what you tried and move on.

## 3. Implement

- Branch from `main` as `feature/<n>-<slug>` or `fix/<n>-<slug>`; one issue per branch. Spikes use `spike/<n>-<slug>` and are never merged.
- Waiting for CI or any long build costs tokens only if you poll. Estimate its duration (a CI run takes about 4-6 minutes), start the wait as one background command (`gh run watch <id> --exit-status` or `gh pr checks <n> --watch` with `run_in_background: true`), do nothing until it exits, and if it is still not done wait again for about the same time; never loop on status calls or foreground `sleep`. Details: `work-orchestrator`, "CI minutes are scarce".
- When you run as a subagent of the `work-orchestrator` skill (parallel worktree agent): commit and push work-in-progress to your feature branch regularly so an interruption never loses work; stay inside the paths you were given; do not leave background processes (CI watches, servers) running when you finish; you cannot remove your own worktree, so report `merged, cleanup pending` with your worktree path and branch names; keep the final report to at most 5 lines in the format the orchestrator gave you (PR number and state, follow-up issue numbers, what the owner must know), since details belong in the PR comments. If a missing prerequisite appears, open a new issue instead of widening the PR.
- Resume checkpoints (subagents): an interruption by a usage limit must be cheap to recover from, because the replacement agent starts from GitHub, not from your history. At the end of each phase (branch created and plan clear; implementation compiles; tests green; PR opened; review fixed) commit and push, and post or edit one agent-marked `Checkpoint` comment on the issue: phase reached, what is done, the exact next step, files touched, and build/test state, in at most 8 lines. Edit the same comment instead of adding new ones. A replacement agent reads only that comment, `git log main..HEAD` and `git status`; it does not re-read docs it no longer needs, re-run finished steps or re-explore the code. While working, keep build and test output out of the conversation (pipe to a file, read the tail or failures only) so the history stays small.
- Code on a hot path (JSON mapping of search results, ADF rendering, list rendering, caches, startup) follows the `performance` skill: load it before implementing and put measured numbers in the PR.
- Tests follow the `testing` skill; layering and cross-platform rules are in `CLAUDE.md`; style, Git-layer and UI rules are in `.claude/rules/` (loaded when you touch matching files).
- Jira behaviour you are unsure of (a field's shape, an endpoint's paging, an error code) is checked against the dev site and recorded as a scrubbed fixture (`testing` skill, "Live dev site"), not guessed. Never the owner's company site.
- New key bindings: add the row to `keymap.default.json` and run `scripts/gen-keymap-doc.sh` in the same PR.
- Run the AOT publish if you added a dependency or touched anything reflection-adjacent.
- Loose ends must not be lost. Anything you notice and do not fix in this PR (a known limit, a deferred case, an unwired piece, a manual check you could not run, a decision for the owner, a missing prerequisite for a later issue) gets registered before you finish, in exactly one of these ways: add it to the Acceptance or body of the existing issue that will own it (`gh issue edit` or a comment), or open a new issue (always with a milestone: `gh issue create -m "<milestone>"`, the milestone of the feature it follows, the next open one if that is closed; area label, `Blocked by:` line if it depends on something; label `needs-owner` for an owner decision or a manual check). A note in `docs/decisions.md`, a PR comment or your report is not registration. List every registered item with its issue number in the PR body under "Follow-ups". If a later issue's prerequisite is missing, add the new issue to that later issue's `Blocked by:` line.
- Update stale docs in the same PR. Decisions: add a `docs/decisions.md` entry only when the choice is not already evident from `docs/ux.md` and its parts (`ux-*.md`), `docs/architecture.md` or the code, or when it overrides an earlier decision (a behaviour change documented in `ux.md` needs none); format in the header of `docs/decisions.md`.

## 4. Deliver

- Build, test and (if relevant) AOT-publish; report real output, including failures.
- Before the reviewer runs, walk the self-review list below. These are the findings reviewers raised again and again; every one you fix here is one round of review saved.
- Self-review list (each item answered before review, not after):
  - **`docs/decisions.md`.** Entry only under the rule in section 3 (non-obvious choice or an override; mark the superseded entry).
  - **Sweep after a rename, key move or behaviour change.** Grep the old term, key and wording across `src/`, `tests/`, `docs/` (`ux*.md`, `git-layer.md`, `architecture.md`), status hints, `ActionCatalog` titles, XML docs, test names and the bodies of related open issues. Stale text in docs, hints and comments was the most common should-fix.
  - **Tests prove the Acceptance line.** Through the real path (controller or action plus the fake Jira serving recorded responses), not only the helper; fail-if-wrong, not just green. Cover the edge cases the code branches on (empty, zero or tiny size, last page, 401/403/429 replies, wide or combining characters, wrapped rows, several hunks, cancel and race, other tab or session active while an overlay is open). New caches, list sources, views and event wiring each get a test. A test name says what it asserts.
  - **Destructive or ambiguous targets.** The confirm dialog names exactly what will be changed, and a fallback never widens the scope beyond it (one issue to the whole filter, a selection to every result, one sprint to the board). Texts that count things are pluralised and say what was not done on failure.
  - **Claims are true.** "One request", "pure function", "no allocation", "stays within N ms" in code comments, docs or the PR body are checked against the code or a measurement.
  - **UI PRs.** Attach the capture or say in the PR body why none (hints, key labels, logic only). A UI or shell-wiring change also gets a tmux smoke run of the real binary (`scripts/screenshot.sh` or `scripts/measure.sh` against the fake Jira): `StartupSmokeTests` catches constructor-order crashes only.
- Run the `reviewer` subagent on the branch with the issue number. Fix every `blocker` and `should-fix`, or justify why not in the PR.
- Make the review visible on GitHub: after opening the PR, fix the findings first, then post one PR comment (use the PR number from `gh pr create`, never an issue number) with `gh pr comment <n> --body-file <file>`: the verdict, one line saying how many findings were fixed (with the commit), and in full only the findings skipped or still open with the reason. Fixed findings are not repeated (the commits show them) and there is no second fix-up comment. The PR body carries only the one-line verdict. Reviewing the work-task's own PR with no reviewer run is not allowed, including for style or docs-only changes.
- Everything you write on GitHub or in git carries the agent identity (see "Agent identity" below).
- Open a PR titled like the issue, body `Closes #<n>`, what changed, assumptions, and the reviewer verdict. Verify locally first, because CI never runs automatically (decision 2026-11-01): `dotnet build`, `dotnet test`, the AOT publish when dependencies or project files changed, and `scripts/inspect.sh --changed` (Windows: `scripts/inspect.ps1 --changed`). Then open the PR ready (`gh pr create`, no draft toggling) after the reviewer has no open blockers, and push once. Push nothing for comment-only nits and push again only to fix a blocker. If another PR merged since your last push, also run `dotnet build && dotnet test` on updated `main` afterwards. PRs whose base is a `review/**` branch (owner review-only PRs) are never merged.
- UI PRs (anything that changes what is rendered; none for key labels, hints only, docs or logic) attach one capture (at most 2-3 images) as a PR comment and run `scripts/screenshot.sh clean` after merging: follow the `screenshots` skill (agent header and signature on the comment; never read the images back; if a tool is missing, say so in the PR).
- Never rebase a pushed branch: merge `origin/main` into it, never force-push. `screenshot.sh publish` pushes every PNG in the output dir, so clean the dir first.
- `windows-vm`: needs a Windows VM; the orchestrator never picks it. In a Windows session the owner names the issue; work it as usual and record what was run on Windows in the PR. Create such an issue only for platform-specific behaviour (credential store, AOT, publishing, packaging, launcher, paths, console APIs), never for UI or rendering.
- `spike`: don't merge or open a PR; post measured findings as an issue comment, push the `spike/` branch and close the issue.
- `needs-review`: work and merge it like any other issue (reviewer clean, local build and tests green); the label only marks what the owner reviews afterwards. Never stop at an open PR waiting for the owner, and say in the PR body what deserves a closer look.
- Otherwise, when local build and tests are green and the reviewer has no open blockers: `gh pr merge <n> --merge --delete-branch` then update local `main`, remove the issue's worktree (`git worktree remove <path>`) and delete the local branch. Once a PR is merged, nothing of it may remain: no worktree, no local branch, no remote branch. A subagent cannot remove the worktree it runs in (and the harness locks it), so a parallel agent only merges and reports "merged, cleanup pending" with its worktree path and branch names; the caller (orchestrating session) then removes each finished agent's worktree (`git worktree remove -f -f <path>`) and deletes its local branches, including the harness's `worktree-agent-*` branch, after checking they are merged into `main` (`git branch --merged main`). Run `git fetch -p` and confirm no stale worktrees or branches remain before ending the session. Spike branches are the exception: they stay (remote) by design.
- Then continue with the next issue if running autonomously.

## Agent identity

All agents act through the owner's GitHub login, so every PR body and GitHub comment must say which agent made it (commits: see below). Roles: `orchestrator` (the main session: picks issues, opens PRs, merges), `implementer` (a per-issue subagent: code, tests, fix commits) and `reviewer` (the `reviewer` subagent) and `readiness` (the `work-readiness` skill: reports and issue edits).

- **Commits (soft convention, not enforced):** nice to have: author name is the role, email stays the owner's from git config (`GIT_AUTHOR_NAME="Claude (implementer)" git commit ...`), and/or an `Agent: <role>` trailer above the `Co-Authored-By` line.
- **PR comments, reviews, issue comments:** start with a header line, end with a signature line, and include a hidden marker so they can be filtered:

  ```
  ### 🔍 Reviewer agent: findings
  <!-- agent: reviewer -->

  ...body...

  ---
  <sub>🔍 Reviewer agent · Claude · PR #<n> @ <short sha></sub>
  ```

  Icons: orchestrator 🧭, implementer 🛠, reviewer 🔍, readiness 📋. Issues and issue comments created by an agent follow the same header and signature rules (a signature on a new issue body; edits to an existing issue body say who added the text). Header titles: `findings`, `spike findings`, `blocked`, etc. The signature names the role, "Claude", and the PR or issue plus commit it refers to.
- **PR body:** the last line reads `Opened by 🧭 orchestrator agent` (or `🛠 implementer agent` when a subagent opens it). No "Generated with Claude Code" footer anywhere.
- **Reviewer output:** the `reviewer` subagent is read-only, so it returns the complete comment (header, marker, body, signature) to whoever runs it, who fixes the findings and then posts the trimmed version described under "Make the review visible" (keep the header, marker and signature).
