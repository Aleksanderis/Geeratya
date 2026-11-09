#!/bin/sh
# Installs or updates Geeratya from a GitHub release (#860).
#
#   curl -fsSL https://raw.githubusercontent.com/Aleksanderis/Geeratya/main/scripts/install.sh | sh
#
# Linux (x64, arm64) and macOS (Apple silicon): downloads the release archive geeratya-<version>-<rid>.tar.gz, checks it
# against the release's SHA256SUMS and puts the binary into ~/.local/bin (no root, no sudo). Running it again updates; it
# does nothing when the installed version is already the wanted one. No telemetry: the only requests go to GitHub.
# Pin a version in the one-liner (the variable belongs to sh, not to curl):
#   curl -fsSL https://raw.githubusercontent.com/Aleksanderis/Geeratya/main/scripts/install.sh | GEERATYA_VERSION=1.2.3 sh
# Windows: use scripts/install.ps1. Uninstall: delete the binary (the script prints the path).
# macOS: the binary is not signed or notarised. Downloads by curl carry no quarantine flag, but if you fetched the archive
# in a browser, run: xattr -d com.apple.quarantine <path to geeratya>
#
# Environment (all optional):
#   GEERATYA_VERSION       install this version (1.2.3 or v1.2.3) instead of the latest
#   GEERATYA_INSTALL_DIR   where the binary goes (default: ~/.local/bin)
#   GEERATYA_FORCE         1 = install even when that version is already installed
#   GEERATYA_RELEASES_URL  release API base (default: the GitHub API's releases of the repository; "latest" and
#                          "tags/v<version>" are appended; a file:// URL works, for tests)
#   GEERATYA_OS, GEERATYA_ARCH  override uname -s and uname -m (for tests)
#
# POSIX sh, no jq. The whole script is one function called on the last line, so a cut-off download runs nothing.
set -eu

REPOSITORY="Aleksanderis/Geeratya"
DEFAULT_RELEASES_URL="https://api.github.com/repos/$REPOSITORY/releases"

say() { printf '%s\n' "$*"; }
fail() { printf 'geeratya install: %s\n' "$*" >&2; exit 1; }
have() { command -v "$1" >/dev/null 2>&1; }

# Downloads $1 to the file $2 (curl or wget). $3 is an optional hint added to the failure. GitHub now and then answers
# 502/503/504 (#872): curl retries those (and 408/429) up to four times with doubling waits, wget retries failed tries.
download() {
  hint="a temporary problem on GitHub's side? run the command again in a minute. ${3:-}"
  if have curl; then curl -fsSL --retry 4 -o "$2" "$1" || fail "download failed: $1 ($hint)"
  elif have wget; then wget -q --tries=4 --waitretry=8 -O "$2" "$1" || fail "download failed: $1 ($hint)"
  else fail "need curl or wget"; fi
}

sha256_of() {
  if have sha256sum; then sha256sum "$1" | cut -d ' ' -f 1
  elif have shasum; then shasum -a 256 "$1" | cut -d ' ' -f 1
  else fail "need sha256sum or shasum to verify the download"; fi
}

# Prints the download URL of the release asset whose name ends with $2 (the release JSON is in the file $1).
asset_url() {
  grep -o '"browser_download_url": *"[^"]*"' "$1" | cut -d '"' -f 4 |
    awk -v suffix="$2" 'length($0) >= length(suffix) && substr($0, length($0) - length(suffix) + 1) == suffix { print; exit }'
}

main() {
  os="${GEERATYA_OS:-$(uname -s)}"
  machine="${GEERATYA_ARCH:-$(uname -m)}"
  case "$machine" in
    x86_64|amd64) arch="x64" ;;
    aarch64|arm64) arch="arm64" ;;
    *) fail "unsupported architecture: $machine (releases exist for x64 and arm64)" ;;
  esac
  case "$os" in
    Linux)
      if have ldd && ldd --version 2>&1 | grep -qi musl; then
        fail "the Linux builds need glibc; this system uses musl (Alpine)"
      fi
      rid="linux-$arch" ;;
    Darwin)
      [ "$arch" = "arm64" ] || fail "macOS builds exist for Apple silicon only"
      rid="osx-arm64" ;;
    *) fail "unsupported system: $os (Windows: use install.ps1)" ;;
  esac
  have tar || fail "need tar"

  base="${GEERATYA_RELEASES_URL:-$DEFAULT_RELEASES_URL}"
  if [ -n "${GEERATYA_VERSION:-}" ]; then
    wanted="${GEERATYA_VERSION#v}"
    release_url="$base/tags/v$wanted"
  else
    release_url="$base/latest"
  fi

  work="$(mktemp -d)"
  trap 'rm -rf "$work"' EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM HUP
  json="$work/release.json"
  download "$release_url" "$json" "(no such release, or a GitHub rate limit: try again later)"
  tag="$(grep -o '"tag_name": *"[^"]*"' "$json" | head -n 1 | cut -d '"' -f 4)"
  [ -n "$tag" ] || fail "could not read the release from $release_url"
  version="${tag#v}"

  target="${GEERATYA_INSTALL_DIR:-$HOME/.local/bin}"
  installed_binary="$target/geeratya"
  if [ -x "$installed_binary" ] && [ "${GEERATYA_FORCE:-0}" != "1" ]; then
    installed="$("$installed_binary" --version 2>/dev/null | awk '{print $2; exit}' || true)"
    if [ "$installed" = "$version" ]; then
      say "Geeratya $version is already installed ($installed_binary)."
      return 0
    fi
    [ -z "$installed" ] || say "Updating Geeratya $installed -> $version"
  fi

  url="$(asset_url "$json" "-$rid.tar.gz")"
  [ -n "$url" ] || fail "release $tag has no $rid archive; see https://github.com/$REPOSITORY/releases"
  sums_url="$(asset_url "$json" "/SHA256SUMS")"
  [ -n "$sums_url" ] || fail "release $tag has no SHA256SUMS; refusing to install an unverified download"

  name="${url##*/}"
  say "Downloading $name"
  download "$url" "$work/$name"
  download "$sums_url" "$work/SHA256SUMS"
  # Lines look like "<hash>  <name>" (or "<hash> *<name>").
  expected="$(awk -v n="$name" '{ f = $2; sub(/^\*/, "", f); if (f == n) { print $1; exit } }' "$work/SHA256SUMS")"
  [ -n "$expected" ] || fail "SHA256SUMS has no entry for $name"
  actual="$(sha256_of "$work/$name")"
  [ -n "$actual" ] || fail "could not compute the checksum of $name"
  [ "$expected" = "$actual" ] || fail "checksum mismatch for $name (expected $expected, got $actual)"

  mkdir -p "$work/unpacked"
  tar -xzf "$work/$name" -C "$work/unpacked" || fail "could not unpack $name"
  binary="$(find "$work/unpacked" -type f -name geeratya | head -n 1)"
  [ -n "$binary" ] || fail "the archive holds no geeratya binary"

  mkdir -p "$target"
  # A temporary name and a rename replace a running binary safely.
  cp "$binary" "$target/geeratya.new"
  chmod 755 "$target/geeratya.new"
  mv -f "$target/geeratya.new" "$installed_binary" || { rm -f "$target/geeratya.new"; fail "could not replace $installed_binary"; }
  say "Installed $installed_binary"
  case ":$PATH:" in
    *":$target:"*) ;;
    *) say "Note: $target is not on your PATH; add it to your shell profile." ;;
  esac
  if [ "$os" = "Darwin" ]; then
    say "macOS: the binary is not signed. If it is blocked, run: xattr -d com.apple.quarantine $installed_binary"
  fi
  say "Geeratya $version installed. Run: geeratya"
  say "To uninstall: rm $installed_binary (your profiles and cache are kept; tokens live in the OS secret store)"
}

main "$@"
