#!/usr/bin/env bash
# Tests scripts/gen-manifests.sh with placeholder hashes (no release exists to test against, #821).
# Run: scripts/test-gen-manifests.sh   (also parses the formula with ruby -c when ruby exists)
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }
l=$(printf 'a%.0s' $(seq 64)); w=$(printf 'b%.0s' $(seq 64)); o=$(printf 'c%.0s' $(seq 64))
printf '%s  geeratya-1.2.3-linux-x64.tar.gz\n%s  geeratya-1.2.3-win-x64.zip\n%s  geeratya-1.2.3-osx-arm64.tar.gz\n' "$l" "$w" "$o" > "$tmp/SHA256SUMS"

"$root/scripts/gen-manifests.sh" 1.2.3 "$tmp/SHA256SUMS" "$tmp/out" >/dev/null
f="$tmp/out/homebrew-tap/Formula/geeratya.rb"
[ -f "$f" ] || fail "no tap formula"
cmp -s "$f" "$tmp/out/homebrew/geeratya.rb" || fail "tap formula differs from homebrew/geeratya.rb"
grep -q 'download/v1.2.3/geeratya-1.2.3-osx-arm64.tar.gz"$' "$f" || fail "macOS url"
grep -q "sha256 \"$o\"" "$f" || fail "macOS hash"
grep -q 'download/v1.2.3/geeratya-1.2.3-linux-x64.tar.gz"$' "$f" || fail "linux url"
grep -q "sha256 \"$l\"" "$f" || fail "linux hash"
grep -q 'geeratya --version' "$f" || fail "test block"
wd="$tmp/out/winget/manifests/a/Aleksanderis/Geeratya/1.2.3"
[ "$(cd "$wd" && ls | tr '\n' ' ')" = "Aleksanderis.Geeratya.installer.yaml Aleksanderis.Geeratya.locale.en-US.yaml Aleksanderis.Geeratya.yaml " ] || fail "winget set is not exactly version+installer+locale"
[ "$(find "$tmp/out/winget" -type f | wc -l)" = 3 ] || fail "winget tree has extra files"
for p in "" .installer .locale.en-US; do
  y="$wd/Aleksanderis.Geeratya$p.yaml"
  grep -q '^PackageIdentifier: Aleksanderis.Geeratya$' "$y" || fail "winget identifier in $y"
  grep -q '^PackageVersion: 1.2.3$' "$y" || fail "winget version in $y"
  grep -q '^ManifestVersion: 1.10.0$' "$y" || fail "winget schema version in $y"
done
grep -q '^ManifestType: version$' "$wd/Aleksanderis.Geeratya.yaml" || fail "version manifest type"
grep -q '^DefaultLocale: en-US$' "$wd/Aleksanderis.Geeratya.yaml" || fail "default locale"
i="$wd/Aleksanderis.Geeratya.installer.yaml"
grep -q '^ManifestType: installer$' "$i" || fail "installer manifest type"
grep -q '^InstallerType: zip$' "$i" || fail "installer type"
grep -q '^NestedInstallerType: portable$' "$i" || fail "nested installer type"
grep -qF 'RelativeFilePath: geeratya-1.2.3-win-x64\geeratya.exe' "$i" || fail "nested exe path"
grep -q '^- Architecture: x64$' "$i" || fail "architecture"
grep -q 'InstallerUrl: https://github.com/Aleksanderis/Geeratya/releases/download/v1.2.3/geeratya-1.2.3-win-x64.zip$' "$i" || fail "installer url"
grep -q "InstallerSha256: $w\$" "$i" || fail "installer hash"
grep -q '^ManifestType: defaultLocale$' "$wd/Aleksanderis.Geeratya.locale.en-US.yaml" || fail "locale manifest type"
grep -q '^PackageLocale: en-US$' "$wd/Aleksanderis.Geeratya.locale.en-US.yaml" || fail "package locale"
if command -v yamllint >/dev/null; then yamllint -d relaxed "$wd" || fail "yamllint"; else echo "yamllint not found: yaml not linted"; fi
if grep -rq '@[A-Z0-9]*@' "$tmp/out"; then fail "placeholder left"; fi
[ "$(cd "$tmp/out/homebrew-tap" && find . -type f)" = "./Formula/geeratya.rb" ] || fail "tap tree has extra files"
if command -v ruby >/dev/null; then ruby -c "$f" >/dev/null || fail "ruby syntax"; else echo "ruby not found: syntax not checked"; fi

printf '%s  geeratya-1.2.3-linux-x64.tar.gz\n' "$l" > "$tmp/short"
if "$root/scripts/gen-manifests.sh" 1.2.3 "$tmp/short" "$tmp/o2" 2>/dev/null; then fail "missing checksum accepted"; fi
echo "ok"
