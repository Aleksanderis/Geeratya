# Release checklist

Short procedure for cutting a release. The mechanics of each workflow are in `docs/internal/architecture.md` ("Releases"); this is the order of steps and the catch-up commands. Agents do not run a release or dispatch `homebrew.yml` / `winget.yml` on their own (they publish to external repositories): they register it as a `needs-owner` issue.

## Steps

1. Main is green (`ci.yml` on the commit). Create and push the tag `v<version>` on that commit (`v1.2.3` or `v1.2.3-rc.1`).
2. Optional dry run (no release, archives only as workflow artifacts): `gh workflow run release.yml --ref main -f tag=v0.0.1-dryrun`.
3. Real release, dispatched on the tag: `gh workflow run release.yml --ref v1.2.3 -f tag=v1.2.3 -f dry_run=false`. The guard checks for a full green `ci.yml` run on the commit (ubuntu and windows) and dispatches one when missing.
4. Wait for the run. Five legs build (linux-x64, linux-arm64, win-x64, win-arm64, osx-arm64); the linux legs also build and install-test the `.deb`, `.rpm` and Arch package.
   - A red optional leg (arm64, macOS) does not block the x64 assets: the draft is still made and the run ends red with the names of the missing archives. Fix the cause and use "Re-run failed jobs" on the run: only the red legs rebuild, and the release job replaces the draft's assets (`gh release upload --clobber`) and generates the package manifests once all five archives exist.
   - A red linux-x64 or win-x64 leg means no release is made.
   - Re-running never touches a published release: the job fails instead.
5. Review the draft on GitHub (assets: five archives, packages, `SHA256SUMS`; generated notes). Publish only after a green run with all five archives present (the manifests and the Homebrew and winget routes need every one); a red run is the signal not to. Edit the notes, then publish. Mark a release candidate as pre-release.
6. Publishing triggers, for a final (not pre-release) release only: `homebrew.yml` (formula push to the tap; green with a notice when `HOMEBREW_TAP_TOKEN` is missing) and `winget.yml` (only when the repository variable `WINGET_SUBMIT` is `true`; skips with a notice when an earlier winget-pkgs PR is still open).
7. Check that the install routes work: `geeratya update --check` from the previous version, the Homebrew formula, the packages from the release page.

## Catch-up dispatches

- Homebrew did not run or the tap was unreachable: `gh workflow run homebrew.yml -f version=1.2.3` (the release must be published).
- winget did not run, was skipped by the guard, or the earlier PR has since been merged: `gh workflow run winget.yml -f tag=v1.2.3` (add `-f force=true` to submit despite an open PR).
- AUR and the first winget submission are manual (owner accounts); the generated files are in the `package-manifests` artifact of the release run.
