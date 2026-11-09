#!/usr/bin/env bash
# Tests scripts/install.sh against a fake GitHub releases API on disk (file:// URLs, no network, no real GitHub, install
# folder in a temp dir): install, skip when current, update, pinned version, arm64 and macOS, checksum mismatch, missing
# SHA256SUMS or entry, missing asset, unsupported architecture and system (#860), and the download retry flags through a fake
# curl and a fake wget (#880).
# Run: scripts/test-install.sh   (not part of `dotnet test`)
set -euo pipefail
unset GEERATYA_VERSION GEERATYA_FORCE GEERATYA_INSTALL_DIR GEERATYA_RELEASES_URL GEERATYA_OS GEERATYA_ARCH
root="$(cd "$(dirname "$0")/.." && pwd)"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }

# A fake release made with the real packer: <api dir>/tags/v<version> holds the release JSON, SHA256SUMS next to the
# archives (the names release.yml produces), and "latest" is a copy made by set_latest.
make_release() { # <version> <api dir> <rid>...
  local version="$1" api="$2"; shift 2
  local assets="$api/assets-$version"
  mkdir -p "$assets" "$api/tags" "$tmp/publish"
  printf '#!/bin/sh\necho "Geeratya %s"\n' "$version" > "$tmp/publish/geeratya"
  chmod +x "$tmp/publish/geeratya"
  local rid
  for rid in "$@"; do
    "$root/scripts/package-release.sh" "$tmp/publish" "$version" "$rid" "$assets" >/dev/null
  done
  (cd "$assets" && sha256sum geeratya-* > SHA256SUMS)
  {
    echo "{ \"tag_name\": \"v$version\", \"assets\": ["
    local file first=1
    for file in "$assets"/*; do
      [ "$first" = 1 ] || echo ","
      first=0
      printf '  { "name": "%s", "browser_download_url": "file://%s" }' "$(basename "$file")" "$file"
    done
    echo
    echo "] }"
  } > "$api/tags/v$version"
}
set_latest() { cp "$2/tags/v$1" "$2/latest"; } # <version> <api dir>

run_install() { # <api dir> [env assignments...]; prints combined output
  local api="$1"; shift
  env "$@" GEERATYA_RELEASES_URL="file://$api" GEERATYA_INSTALL_DIR="$tmp/bin" \
    PATH="$tmp/bin:$PATH" sh "$root/scripts/install.sh" 2>&1
}
installed_version() { "$tmp/bin/geeratya" --version; }

api="$tmp/api"
make_release 1.2.3 "$api" linux-x64 linux-arm64 osx-arm64
make_release 1.3.0 "$api" linux-x64
set_latest 1.2.3 "$api"

out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64)" || fail "install failed: $out"
[ -x "$tmp/bin/geeratya" ] || fail "binary not installed"
[ "$(installed_version)" = "Geeratya 1.2.3" ] || fail "wrong binary installed"
grep -q "To uninstall: rm $tmp/bin/geeratya" <<<"$out" || fail "no uninstall hint: $out"
[ ! -e "$tmp/bin/geeratya.new" ] || fail "left the temporary file behind"

out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64)" || fail "second run failed: $out"
grep -q "already installed" <<<"$out" || fail "no skip when current: $out"

# Update: a newer latest replaces the installed one.
set_latest 1.3.0 "$api"
out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64)" || fail "update failed: $out"
grep -q "Updating Geeratya 1.2.3 -> 1.3.0" <<<"$out" || fail "no update message: $out"
[ "$(installed_version)" = "Geeratya 1.3.0" ] || fail "not updated"

# Pinned version: goes back to 1.2.3 although 1.3.0 is the latest; "v" prefix accepted; force reinstalls the same version.
out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64 GEERATYA_VERSION=v1.2.3)" || fail "pinned install failed: $out"
[ "$(installed_version)" = "Geeratya 1.2.3" ] || fail "pinned version not installed: $out"
out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64 GEERATYA_VERSION=1.2.3)" || fail "pinned rerun failed: $out"
grep -q "already installed" <<<"$out" || fail "no skip for the pinned current version: $out"
printf '#!/bin/sh\necho "Geeratya broken"\n' > "$tmp/bin/geeratya"
out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64 GEERATYA_VERSION=1.2.3 GEERATYA_FORCE=1)" || fail "forced install failed: $out"
[ "$(installed_version)" = "Geeratya 1.2.3" ] || fail "force did not reinstall"
if out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64 GEERATYA_VERSION=9.9.9)"; then fail "unknown version accepted"; fi
grep -q "download failed" <<<"$out" || fail "no message for an unknown version: $out"

# arm64 and macOS pick their own archives.
rm "$tmp/bin/geeratya"
out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=aarch64 GEERATYA_VERSION=1.2.3)" || fail "arm64 install failed: $out"
[ "$(installed_version)" = "Geeratya 1.2.3" ] || fail "arm64 not installed"
rm "$tmp/bin/geeratya"
out="$(run_install "$api" GEERATYA_OS=Darwin GEERATYA_ARCH=arm64 GEERATYA_VERSION=1.2.3)" || fail "macOS install failed: $out"
grep -q "xattr -d com.apple.quarantine" <<<"$out" || fail "no quarantine note on macOS: $out"
[ -x "$tmp/bin/geeratya" ] || fail "macOS binary not installed"
rm "$tmp/bin/geeratya"

# A release without the wanted archive says which one is missing.
if out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=aarch64)"; then fail "missing arm64 asset accepted"; fi
grep -q "has no linux-arm64 archive" <<<"$out" || fail "no missing-asset message: $out"

# Checksum mismatch, missing entry, missing SHA256SUMS: nothing is installed.
sums="$api/assets-1.2.3/SHA256SUMS"
cp "$sums" "$tmp/SHA256SUMS.good"
printf '%064d  geeratya-1.2.3-linux-x64.tar.gz\n' 0 > "$sums"
set_latest 1.2.3 "$api"
if out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64)"; then fail "checksum mismatch accepted"; fi
grep -q "checksum mismatch" <<<"$out" || fail "no checksum message: $out"
[ ! -e "$tmp/bin/geeratya" ] || fail "binary installed despite a bad checksum"
printf '%064d  geeratya-1.2.3-linux-arm64.tar.gz\n' 0 > "$sums"
if out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64)"; then fail "missing checksum entry accepted"; fi
grep -q "no entry for geeratya-1.2.3-linux-x64.tar.gz" <<<"$out" || fail "no missing-entry message: $out"
cp "$tmp/SHA256SUMS.good" "$sums"
sed 's|, *{ "name": "SHA256SUMS"[^}]*}||; /"name": "SHA256SUMS"/d' "$api/tags/v1.2.3" > "$api/latest"
if out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=x86_64)"; then fail "release without SHA256SUMS accepted"; fi
grep -q "no SHA256SUMS" <<<"$out" || fail "no missing-sums message: $out"
[ ! -e "$tmp/bin/geeratya" ] || fail "binary installed without a checksum file"

# Unsupported platforms.
set_latest 1.2.3 "$api"
if out="$(run_install "$api" GEERATYA_OS=Linux GEERATYA_ARCH=riscv64)"; then fail "unsupported arch accepted"; fi
grep -q "unsupported architecture" <<<"$out" || fail "no arch message: $out"
if out="$(run_install "$api" GEERATYA_OS=FreeBSD GEERATYA_ARCH=x86_64)"; then fail "unsupported system accepted"; fi
grep -q "unsupported system" <<<"$out" || fail "no system message: $out"
if out="$(run_install "$api" GEERATYA_OS=Darwin GEERATYA_ARCH=x86_64)"; then fail "macOS x64 accepted"; fi
grep -q "Apple silicon only" <<<"$out" || fail "no macOS x64 message: $out"
# Download retry (#872, #880): a fake curl (and wget) on PATH logs its arguments and copies the file:// target, so the
# flags install.sh passes can be checked without a network. Real curl does the retrying; the fake only fails when told to.
fakes="$tmp/fakes"; mkdir -p "$fakes"
fake_downloader() { # <name> <output flag>
  cat > "$fakes/$1" <<EOF
#!/bin/sh
printf '%s\\n' "\$*" >> "$tmp/$1.log"
out=""; url=""
while [ \$# -gt 0 ]; do
  case "\$1" in $2) out="\$2"; shift ;; -*) ;; *) url="\$1" ;; esac
  shift
done
[ ! -e "$tmp/$1.fail" ] || exit 22
cp "\${url#file://}" "\$out"
EOF
  chmod +x "$fakes/$1"
}
fake_downloader curl -o
fake_downloader wget -O
set_latest 1.2.3 "$api"
run_fake() { # <PATH>; installs into a clean folder
  rm -rf "$tmp/bin"
  env GEERATYA_OS=Linux GEERATYA_ARCH=x86_64 GEERATYA_RELEASES_URL="file://$api" GEERATYA_INSTALL_DIR="$tmp/bin" PATH="$1" \
    sh "$root/scripts/install.sh" 2>&1
}
rm -f "$tmp/curl.log" "$tmp/curl.fail"
out="$(run_fake "$fakes:$PATH")" || fail "install with the fake curl failed: $out"
[ "$(installed_version)" = "Geeratya 1.2.3" ] || fail "fake curl install did not install"
[ "$(wc -l < "$tmp/curl.log")" = 3 ] || fail "expected 3 curl calls (release, archive, sums): $(cat "$tmp/curl.log")"
[ "$(grep -c -e '-fsSL --retry 4 ' "$tmp/curl.log")" = 3 ] || fail "a curl call lacks -f or --retry 4: $(cat "$tmp/curl.log")"
# A failing curl (after its own retries) fails the install once, with the hint; the script does not loop around curl.
touch "$tmp/curl.fail"; rm -f "$tmp/curl.log"
if out="$(run_fake "$fakes:$PATH")"; then fail "install succeeded although curl failed"; fi
grep -q "download failed" <<<"$out" || fail "no message when curl fails: $out"
[ "$(wc -l < "$tmp/curl.log")" = 1 ] || fail "install.sh called curl again after it failed: $(cat "$tmp/curl.log")"
[ ! -e "$tmp/bin/geeratya" ] || fail "binary installed after a failed download"
rm -f "$tmp/curl.fail"
# wget fallback, with curl hidden: PATH holds only links to the tools install.sh needs, plus the fake wget.
only="$tmp/only-wget"; mkdir -p "$only"
for tool in sh env uname mktemp grep cut head awk tar find cp chmod mv rm mkdir sha256sum cat ldd dirname gzip; do
  found="$(command -v "$tool" 2>/dev/null || true)"
  case "$found" in /*) ln -sf "$found" "$only/$tool" ;; esac
done
cp "$fakes/wget" "$only/wget"
out="$(run_fake "$only")" || fail "install with the fake wget failed: $out"
[ "$(installed_version)" = "Geeratya 1.2.3" ] || fail "fake wget install did not install"
[ "$(grep -c -e '-q --tries=4 --waitretry=8 ' "$tmp/wget.log")" = 3 ] || fail "wget lacks the retry flags: $(cat "$tmp/wget.log")"
echo "ok"
