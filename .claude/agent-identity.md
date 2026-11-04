# Agent identity

All agents act through the owner's GitHub login, so every PR body and GitHub comment must say which agent made it (commits: see below). Roles: `orchestrator` (the main session: picks issues, opens PRs, merges), `implementer` (a per-issue subagent: code, tests, fix commits) and `reviewer` (the review subagent) and `readiness` (milestone readiness reports and issue edits).

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
