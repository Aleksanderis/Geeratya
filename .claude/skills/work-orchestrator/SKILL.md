---
name: work-orchestrator
description: Orchestrate a Geeratya milestone (or a slice of it) by delegating issues to subagents in bounded batches. Use when asked to work a milestone, run the backlog, or continue the milestone after a previous orchestrator session. Keeps the session small; state lives on GitHub, not in the conversation.
model: sonnet
effort: medium
---

# Work a milestone

Invoke as `/work-orchestrator start working on M2` (any milestone: `M1`..`M7`, or a title; no other prompt is needed). Resolve the argument to the milestone title with `gh api repos/{owner}/{repo}/milestones --jq '.[]|"\(.number) \(.title)"'`. Goals per milestone are in `docs/roadmap.md`. Do not start a milestone while an earlier one still has open, unblocked issues unless the owner says so.

You are the orchestrator, not the implementer. Each issue is delivered by a subagent following the `work-task` skill (read it once; it holds the per-issue procedure and what an agent must do). This skill decides what to run, how many at once, what each agent is told, and when to stop.

## Delegation rules

- At most 3 subagents at a time (token budget); queue the rest and start the next only when one finishes. One subagent per issue, each in its own worktree (`isolation: "worktree"`). Only issues that are independent run together: different files, no shared contracts, not the same keymap rows; issues that touch the same shell files run fewer at once.
- Launched agents: pass `model: sonnet` and `effort: medium` explicitly (`high` only for a clearly hard issue, such as an algorithm or a parser with many edge cases); never `opus`, and never `haiku` for work that needs judgment. The `reviewer` agent sets its own model and effort in its definition.
- Lean prompts, never the orchestrator's context: the issue number, the branch name (`feature/<n>-<slug>`), the few docs sections the agent needs (name sections, not whole files), a two-line summary of the current state of the code it builds on, the paths it may touch, "follow the `work-task` skill and CLAUDE.md", "merge it yourself per work-task when review is clean (unless needs-review)", and the report format below (at most 5 lines).
- Rate limits: a limit error kills running agents and names the reset time. Launch nothing new and never delete the failed agents' worktrees or branches. Resume protocol: after the reset the prompt cache is cold, so `SendMessage` to a failed agent re-reads its whole history at full price (2-3 agents cost 10-15% of a session). Default to a fresh agent per in-flight issue instead: same worktree and branch, and the lean prompt plus "resume issue #<n> from its branch's commits and the latest `Checkpoint` comment on the issue (see `work-task`, Resume checkpoints); do not redo finished steps". Use `SendMessage` only when the agent had no commits or checkpoint yet and its history is short. Relaunch at most 2 at a time, the most advanced issue (PR open, review pending) first. Do not schedule a wake-up (session crons proved unreliable): stop, tell the owner the reset time, and let them start a new session, which rebuilds state from GitHub and relaunches in-flight branches under this protocol.

## 0. Readiness

Look for a readiness report from the `work-readiness` skill for this milestone (the latest comment containing "Verdict:" on the milestone's tracking issue: the open issue labeled `tracking` in that milestone). If none exists from the last few days, tell the owner to run `/work-readiness <milestone>` first (or run it yourself if the owner said to just go) and stop on a `not ready` verdict. Use the report's suggested batches and answered decisions; do not redo its analysis.

## 1. Rebuild state from GitHub (never from memory)

Run `.claude/skills/work-orchestrator/scripts/owner-feedback.sh sweep` (labels the owner's issues and PRs `owner` and lists owner comments without an agent reply; the owner never runs it), then `.claude/skills/work-orchestrator/scripts/orchestrator-state.sh "<milestone>"` and nothing else: it prints one line per open issue (labels, blockers with OPEN/closed), the open PRs, the worktrees and the issues where the owner answered last. Never read issue bodies, comment threads or `gh issue list --json body` output in the session; if you must read one issue (to act on an owner answer, or to write a prompt), read only that issue's latest comment.

- The `Blocked by:` line is the order; an issue with an OPEN blocker is not startable.
- Issues listed as "owner answered last": act on the answer (see "Labels for the owner" in `work-task`: implement, log, or close) and reply with an agent-marked comment. Do not remove the label; do not list an answered issue as "waiting for the owner" in the status comment.
- Skip `spike` and `needs-review` issues as `work-task` says, skip `windows-vm` issues (the owner runs them in separate Windows VM sessions; list them as waiting for the owner in the status comment), and never pick the `tracking` issue as work.

If a worktree or feature branch exists for an issue with no merged PR, that issue is in flight: resume it (`work-task`'s rate-limit rules), do not start it over.

## 2. Run bounded batches

- Pick up to 3 independent unblocked issues (different files, no shared contracts or keymap rows). Prefer finishing in-flight issues first. If issues touch the same shell files, run fewer at once.
- Launch one subagent per issue with the lean prompt above. Do not paste docs or earlier reports into prompts.
- Work in batches of at most 3 issues, never more than 3 agents at once. When a batch is done (merged or stopped for the owner), re-run step 1 and start the next batch; keep going until a stop condition in step 4 holds. The session stays small because agents report in 5 lines, cleanup is one script call and the details live in PR comments.
- Report format every agent must use (put it in the prompt, it is the only thing the orchestrator reads), at most 5 lines:
  1. `PR #<n> merged|open|stopped; closes #<issue>`
  2. `Follow-ups: #a, #b` (issue numbers already registered) or `Follow-ups: none`
  3. `Owner must know: <one line>` or `none`
  4. Optional: one line of anything that changes how the next issues should run.
- As each agent reports: if line 1 says merged, run `.claude/skills/work-orchestrator/scripts/orchestrator-cleanup.sh <pr> <agent-id>` with the agent id from the launch result (verifies the PR is merged, removes the worktree and merged local branches, prunes, pulls `main`; it prints one line). Stop the agent if still listed (`TaskStop`; ignore "not running"). Then start the next queued issue. Do not run the underlying git commands by hand.
- Loose ends: the agent registers them (`work-task`) and lists the issue numbers in line 2; trust the numbers. Check only when a report says a loose end exists but names no issue: register it yourself (`gh issue create`/`gh issue edit`). Never end a session with unregistered loose ends.
- Delegate chores instead of doing them in the session: anything that needs more than two commands or reading more than a few lines of output (resolving a conflict between two finished PRs, investigating a failed CI run, registering several loose ends, the hand-over status comment) goes to a short-lived subagent with a three-line prompt and a one-line report.
- Keep your own messages to the owner to one or two lines per event; no recaps of reports.
- Do not read PR diffs or agent output files yourself; trust the PR comments and CI. Open the PR only if an agent reports a problem.
- CI minutes are scarce (decision 2026-11-01): CI never runs automatically, local build and tests are the gate. Tell agents to verify locally, open the PR ready, push once, skip screenshots without a visual change, and never push empty commits or comment-only nits. Dispatch CI (`gh workflow run ci.yml --ref main`) once before closing a milestone and report the result.

## 3. Rate limits and failures

- Apply the rate-limit rule above.
- If the same failure repeats after three different attempts, comment on the issue and move on (`work-task` step 2).

## 4. Stop and hand over

Stop when any of these holds: nothing is unblocked (every remaining issue is done, blocked or waiting for the owner), or the owner asked you to stop. There is no token limit rule: the session cannot measure its own context, so it stays small by construction (compact state script, 5-line reports, cleanup script, chores delegated).

Before ending:

1. Run `.claude/skills/work-orchestrator/scripts/owner-feedback.sh sweep` and `.claude/skills/work-orchestrator/scripts/orchestrator-state.sh` once more. Owner comments on items touched this session must have an agent reply stating the fate (fixed, registered, or the argument for not doing it); reply or delegate that before ending. Anything still listed as unanswered at session start or end is delegated to one short-lived subagent that replies to each comment individually (fixed, registered, or the argument), without asking the owner. Then: nothing of a merged PR may remain (worktrees, local or remote branches), and unmerged work is listed. Every loose end named in a report must have its issue number.
2. Post a status comment on the milestone's tracking issue (create it as `work-readiness` describes if missing), in the same shape as the readiness report (a short plain-language summary on top, orchestrator details in a collapsed `<details>` block), at most 8 lines of summary and 15 of details. Edit your own previous status comment (`gh api -X PATCH`) instead of adding a new one; only a readiness report with a "Verdict:" line is a new comment. Give the same summary as the final message, with every issue and PR as a full URL (a bare `#<n>` is not clickable in the terminal): issues merged this session with PR links, issues in flight with branch names, issues waiting on the owner (spike, destructive, needs-review), what is unblocked next.
3. Tell the owner: "start a new session with `work-orchestrator` to continue" and nothing else is needed, since GitHub holds the state.

## Rules

- No long summaries of agent work in the conversation; link PRs instead.
- Never implement an issue yourself in the orchestrator session unless it is a trivial fix to something an agent just merged.
- Never decide a milestone is done without checking that every issue in it is closed or waiting for the owner.
