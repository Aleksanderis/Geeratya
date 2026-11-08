# Landing page

The landing page is a separate repository, `Geeratya/geeratya.github.io` (owner decision, #695), served by GitHub Pages at <https://geeratya.github.io/>. Nothing of it lives in this repository, so there is no Pages workflow here. Static files, no build step. Its look is its own (#825): a left rail, monospace headings with a serif body, a slate blue-grey palette on the logo's ink (#1e1e2e), in a dark and a light theme (#839), terminal-window frames, and the GitHub mark (Octicons `mark-github`, unaltered, only on links to the repository, under GitHub's logo usage guidelines; the icon set's MIT licence does not cover the mark) inline. It deliberately does not resemble the Gitunia page.

## Contents of the landing repository

| Path | What |
|---|---|
| `index.html` | One page: what Geeratya is, the demo, Features (a copy of the README list), a Buy me a coffee link, install, quick start, docs links, licence, the non-affiliation line |
| `assets/` | `logo.svg` (an original mark, no Jira or Atlassian imagery; keep it unchanged), `favicon.ico`, `favicon-32/180/192/512.png`, `social-preview.png` (1200x630) |
| `media/` | `demo.gif` and `demo.webm`, the files `scripts/demo.sh` records (`testing.md`, "README demo"); the README links `https://geeratya.github.io/media/demo.gif`. Also the feature screenshots as `.webp` (board, backlog, jql-search, issue-detail, issue-comments, status-picker, status-changed), shown in the page's tour with alt text and lazy loading |
| `robots.txt`, `sitemap.xml` | Crawler files with the `github.io` address |
| `README.md` | How to preview (`python3 -m http.server`) |

## Keeping it in step

- Features in `index.html` copy the list in `README.md`; change both in the same round. "Not included" is not shown on the page (owner, #832); it stays in the README.
- Palette and theme (#839): the default palette is slate (the `:root` block, ink `#1e1e2e` kept). The theme follows `prefers-color-scheme`; a toggle in the rail cycles system / light / dark, stores the choice in `localStorage` (try/catch) and sets `<html data-theme="light|dark">`; an inline script in `<head>` applies it before first paint (no flash), and no attribute means follow the system. The logo green stays as an alternative: `<html lang="en" data-palette="green">` (its own dark and light blocks). Contrast checked (WCAG AA), slate: text 12.8:1 dark and 14.7:1 light, muted text at least 5.9:1, accent at least 6.1:1 on bg and panel, button text on accent at least 7.2:1; green light is lower (accent 5.3:1 on panel, button text 6.4:1) but still AA. The toggle is hidden until its script runs, so it never shows as a dead control without JavaScript. The `.win` frames keep a dark terminal body in both themes with an accent title bar; the demo and screenshots are dark terminals either way. The logo is never changed.
- Support: the footer and the README link to <https://buymeacoffee.com/aleksanderis> (same account as Gitunia), and `.github/FUNDING.yml` sets `buy_me_a_coffee`.
- The Install section says "build from source" until the first release (M8). When release archives and packages exist, replace it with the per-system lines of `docs/user-guide.md`.
- Wording rules (`name-availability.md`): "for Jira Cloud" as a description, never "Jira" in the name or domain, no Atlassian or Jira logos, and the footer line "Geeratya is an independent project and is not affiliated with or endorsed by Atlassian. Jira is a trademark of Atlassian."
- After a visible change to the shown screens, record the demo again and replace the two files and the screenshots in `media/` (convert PNG to WebP, about 80 quality).
- Captions in the tour restate README Features only; do not claim more than the list says.

## Publishing (owner steps)

1. Push the landing repository (its first commit is local until then).
2. In its settings: Pages, deploy from branch `main`, folder `/ (root)`.
3. Make the repository public (a private repository serves Pages only on paid plans). Do this when the main repository goes public (#212), so the README image and the page both load.
4. Optional: a custom domain (costs money, not done by agents), Search Console verification (add the meta tag to `index.html`).

Checks after publishing (the links to this repository resolve only once it is public, #212): `https://geeratya.github.io/`, `.../media/demo.gif`, `.../media/demo.webm`, `.../robots.txt`, `.../sitemap.xml` answer 200, and the repository links on the page open.
