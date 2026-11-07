#!/usr/bin/env bash
# Build .deb and .rpm from the linux-x64 release archive (one binary in /usr/bin).
# Usage: scripts/package-linux.sh <archive.tar.gz> <version> <out-dir>
# Needs tar, ar and rpmbuild (Debian/Ubuntu: apt install rpm). The .deb is assembled
# with ar and tar, so dpkg-deb is not needed. A pre-release version (1.2.3-rc.1)
# becomes 1.2.3~rc.1 in both formats so it sorts before 1.2.3.
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "usage: $0 <archive.tar.gz> <version> <out-dir>" >&2
  exit 2
fi
archive=$(cd "$(dirname "$1")" && pwd)/$(basename "$1")
version=$2
mkdir -p "$3"
out=$(cd "$3" && pwd)
pkgver=${version//-/\~}
url=https://github.com/Aleksanderis/Geeratya
summary="Keyboard-first terminal client for Jira Cloud"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
tar -xzf "$archive" -C "$work"
src=$(echo "$work"/geeratya-*)
[ -x "$src/geeratya" ] || { echo "no geeratya binary in $archive" >&2; exit 1; }

# --- .deb ---
root="$work/deb-root"
mkdir -p "$root/usr/bin" "$root/usr/share/doc/geeratya" "$work/deb-ctl"
install -m 0755 "$src/geeratya" "$root/usr/bin/geeratya"
install -m 0644 "$src/LICENSE" "$root/usr/share/doc/geeratya/copyright"
size=$(( ($(stat -c %s "$src/geeratya") + 1023) / 1024 ))
cat > "$work/deb-ctl/control" <<CTL
Package: geeratya
Version: $pkgver
Depends: libc6, libssl3 | libssl3t64, zlib1g
Section: utils
Priority: optional
Architecture: amd64
Installed-Size: $size
Maintainer: Aleksandras T. <noreply@users.noreply.github.com>
Homepage: $url
Description: $summary
 Kanban board, backlog, issues and JQL in the terminal, with vim-style keys.
CTL
printf '2.0\n' > "$work/debian-binary"
tar --owner=0 --group=0 --numeric-owner -C "$work/deb-ctl" -czf "$work/control.tar.gz" ./control
tar --owner=0 --group=0 --numeric-owner -C "$root" -czf "$work/data.tar.gz" ./usr
deb="$out/geeratya_${pkgver}_amd64.deb"
rm -f "$deb"
(cd "$work" && ar rc "$deb" debian-binary control.tar.gz data.tar.gz)
echo "$deb"

# --- .rpm ---
top="$work/rpm"
mkdir -p "$top/SPECS"
cat > "$top/SPECS/geeratya.spec" <<SPEC
Name: geeratya
Version: $pkgver
Release: 1
Summary: $summary
License: MIT
URL: $url
BuildArch: x86_64
AutoReqProv: no
Requires: openssl-libs, zlib

%description
Kanban board, backlog, issues and JQL in the terminal, with vim-style keys.

%install
mkdir -p %{buildroot}/usr/bin
install -m 0755 $src/geeratya %{buildroot}/usr/bin/geeratya
mkdir -p %{buildroot}/usr/share/licenses/geeratya
install -m 0644 $src/LICENSE %{buildroot}/usr/share/licenses/geeratya/LICENSE

%files
%license /usr/share/licenses/geeratya/LICENSE
/usr/bin/geeratya
SPEC
rpmbuild -bb --define "_topdir $top" --define "_build_id_links none" \
  --define "debug_package %{nil}" --define "__strip /bin/true" "$top/SPECS/geeratya.spec" >/dev/null
for f in "$top"/RPMS/*/*.rpm; do cp "$f" "$out/"; echo "$out/$(basename "$f")"; done
