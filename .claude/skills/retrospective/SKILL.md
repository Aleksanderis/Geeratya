---
name: retrospective
description: Owner-run review of how the agents work. Mines PR review comments for repeated findings, audits CLAUDE.md, rules, skills and agents for stale, duplicate or contradicting text, and proposes changes the owner approves one by one. Use on /retrospective or when asked to tune, clean up or extend the instructions.
model: sonnet
effort: high
---

# Retrospective

Improve the instructions from evidence. Never runs unattended; nothing changes until the owner approves it by id.

**Fenced, never propose changes (only report a problem you see):** safety and destructive-operation rules, pushing and merge rules, agent identity, model choice, the autonomy invariants in `CLAUDE.md`. Only the owner edits these.

## 1. Collect

- Range: after the last entry in `docs/retrospectives.md`; the first run takes the last 40 merged PRs.
- `.claude/skills/retrospective/scripts/collect.sh <scratchpad>/retro.md [since-pr] [limit]` writes every PR's comments tagged OWNER, REVIEWER or IMPLEMENTER (plus a [MISPOSTED] mark when the signature names another PR) and prints counts. Read the file in parts, never raw `gh` output. Over ~30 PRs, read in a forked subagent and bring back grouped findings only.
- Weight: an OWNER comment outweighs a reviewer finding; a finding the implementer fixed is a real miss; a finding the implementer skipped ("Not done", "accepted as a nit") in 3+ PRs is reviewer noise, so propose dropping or demoting that check in `reviewer.md` instead of a new rule.

## 2. Find what repeats

- Group findings by cause, not wording; count distinct PRs. Threshold: 3 for a prose rule, 2 for something a script, `.editorconfig` severity or test can enforce. One-offs and ideas the owner declined before (see the log) stay out.
- For each group take the weakest fix that works:
  1. **Enforce** in `.editorconfig`, an analyzer or a test.
  2. **Make an existing rule findable:** shorter, in the path-loaded file, or a `work-task` checklist item.
  3. **New rule:** one line, in the narrowest place loaded when it matters (`.claude/rules/` before `CLAUDE.md`).
- A manual fix agents repeat by hand is a candidate for a script in `scripts/`.

## 3. Audit what exists

Read `CLAUDE.md`, `.claude/rules/*`, `.claude/agents/*`, every `SKILL.md` and the memory feedback files (`MEMORY.md` in the project memory directory). Report with file:line:
- **Duplicates** (keep the narrowest, point to it from the other; a memory restating a repo rule is one, ask before deleting it) and **contradictions** between files or with `docs/decisions.md`.
- **Stale references:** files, scripts, commands, labels, issue numbers, doc sections that no longer exist (verify with ls, grep, gh).
- **Now enforceable:** prose a script, `.editorconfig` or test could replace.
- **Size:** line counts against the last run. Silence is not "unused": a rule agents follow yields no findings, so trim only duplicates, stale text and enforced prose, and any growth names what it drops.

## 4. Present

One terminal report, no edits: a table of proposals (id, evidence PRs, action enforce/fix/add/merge/delete, exact wording or diff, target file, what it replaces), the audit list, and anything fenced as "for the owner". Ask which ids to apply.

## 5. Apply

- Only approved ids, as worded; anything new becomes a new proposal.
- Owner-requested instruction changes: local `chore:` commit on `main` with an `Agent: orchestrator` trailer, not pushed (say so). Changes answering PR review comments go through a PR branch.
- After a script or `.editorconfig` change run `dotnet build`; run a new check once over recent history to confirm it flags nothing clean.
- Append to `docs/retrospectives.md` (create with a one-line header): date, PR range, counts per recurring group, proposals applied or declined with the owner's reason, instruction file line counts.
- Never weaken a rule or gate to make agents faster; if that seems right, tell the owner.
