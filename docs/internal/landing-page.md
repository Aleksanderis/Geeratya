# Landing page

The landing page is a separate repository, `Geeratya/geeratya.github.io` (owner decision, #695), served by GitHub Pages at <https://geeratya.github.io/>. Nothing of it lives in this repository, so there is no Pages workflow here. Static files, no build step. Its look is its own (#825): a left rail, monospace headings with a serif body, a calm forest-green palette (#844) in a dark and a light theme (#839), terminal-window frames, and the GitHub mark (Octicons `mark-github`, unaltered, only on links to the repository, under GitHub's logo usage guidelines; the icon set's MIT licence does not cover the mark) inline. It deliberately does not resemble the Gitunia page.

## Contents of the landing repository

| Path | What |
|---|---|
| `index.html` | One page: what Geeratya is, the demo, Features (a copy of the README list), a Buy me a coffee button (own inline SVG, themed; the README uses the shields.io badge like Gitunia), install, quick start, docs links, licence, the non-affiliation line |
| `assets/` | `logo.svg` (an original mark, no Jira or Atlassian imagery; keep it unchanged), `favicon.ico`, `favicon-32/180/192/512.png`, `social-preview.png` (1200x630) |
| `media/` | `demo.gif` (under 4 MB, so GitHub's image proxy serves it) and `demo.webm`, the files `scripts/demo.sh` records (`testing.md`, "README demo"); the README links `https://geeratya.github.io/media/demo.gif`. Also the feature screenshots as `.webp` (board, backlog, jql-search, issue-detail, issue-comments, status-picker, status-changed), shown in the page's tour with alt text and lazy loading |
| `robots.txt`, `sitemap.xml` | Crawler files with the `github.io` address |
| `README.md` | How to preview (`python3 -m http.server`) |

## Keeping it in step

- Features in `index.html` copy the list in `README.md`; change both in the same round. "Not included" is not shown on the page (owner, #832); it stays in the README.
- Palette and theme (#839, #844): the default palette is a calm forest green: dark `#0f1511` background with panels `#151d18`, light a warm paper `#f3f1ea`. Text is a neutral warm off-white (`#e9e6de`, light `#1d211e`) and muted warm grey, never green; green (`#6fae84` dark, `#2f6a47` light) is only for accent, links, buttons, frames, numbering and small details. Slate (blue-grey on the logo ink `#1e1e2e`, the Gitunia-like look) is kept only as an alternative behind `<html data-palette="slate">`; it must not be the default (owner, #844), and the page must stay visibly different from the Gitunia landing page (check both renders). The theme follows `prefers-color-scheme`; a toggle in the rail cycles system / light / dark, stores the choice in `localStorage` (try/catch) and sets `<html data-theme="light|dark">`; an inline script in `<head>` applies it before first paint (no flash), and no attribute means follow the system. The toggle is an icon button only (monitor = system, sun = light, moon = dark, inline SVG, icon shows the current mode) with an `aria-label` and a `title` naming the mode and what a click does. Contrast (WCAG AA), green: text 14.8:1 dark and 14.4:1 light, muted at least 7.6:1 dark and 5.6:1 light, accent on bg/panel 7.1/6.6 dark and 5.7/5.1 light, button text on accent 7.3 dark and 6.1 light (slate numbers: see #839). The toggle is hidden until its script runs. The `.win` frames keep a dark terminal body in both themes with an accent title bar. The logo is never changed. Render both themes headless to check: `chromium --headless=new --no-sandbox --blink-settings=preferredColorScheme=0` (dark) or `=1` (light) with `--screenshot`; the plain `--force-prefers-color-scheme` flag is ignored.
- Support: a coffee band above the footer and the README link to <https://buymeacoffee.com/aleksanderis> (same account as Gitunia), and `.github/FUNDING.yml` sets `buy_me_a_coffee`.
- The Install section (#852) only says how to install, never how to build (that stays in CONTRIBUTING.md): brew for macOS and Linux, winget and the .zip for Windows, .deb/.rpm/.tar.gz for Linux, then `geeratya` and the user guide link, with copy buttons (inline script, no external ones). Until the first release `data-pending` on that section shows "available with the first release" (`.soon` spans); remove that one attribute when the release is out. The winget note has its own marker, `data-winget-pending` on the Windows `<li>`: remove it when winget-pkgs accepts #843. The Linux packages entry has no pending note of its own; the section note covers it. Keep README.md Install consistent.
- Wording rules (`name-availability.md`): "for Jira Cloud" as a description, never "Jira" in the name or domain, no Atlassian or Jira logos, and the footer line "Geeratya is an independent project and is not affiliated with or endorsed by Atlassian. Jira is a trademark of Atlassian."
- After a visible change to the shown screens, record the demo again and replace the two files and the screenshots in `media/` (convert PNG to WebP, about 80 quality).
- Captions in the tour restate README Features only; do not claim more than the list says.

## Publishing

1. Agents push the landing repository themselves after each change (owner, 2026-11-09); the first push is done.
2. In its settings: Pages, deploy from branch `main`, folder `/ (root)`.
3. Make the repository public (a private repository serves Pages only on paid plans). Do this when the main repository goes public (#212), so the README image and the page both load.
4. Optional: a custom domain (costs money, not done by agents), Search Console verification (add the meta tag to `index.html`).

Checks after publishing (the links to this repository resolve only once it is public, #212): `https://geeratya.github.io/`, `.../media/demo.gif`, `.../media/demo.webm`, `.../robots.txt`, `.../sitemap.xml` answer 200, and the repository links on the page open.
