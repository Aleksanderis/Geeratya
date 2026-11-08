# Name availability: "Geeratya"

Research for issue #208, done 2026-11-08 from public pages and unauthenticated registry APIs only (no accounts, nothing paid). Nothing was renamed or registered.

## Recommendation

**Keep "Geeratya".** No blocker was found: the name is unused on every registry checked, no trademark or product under it turned up, and it does not contain "Jira", so Atlassian's naming rules do not constrain it. Only the descriptive wording ("for Jira") needs care (see Atlassian below).

Before going public, claim what is cheap and still free: the GitHub name `geeratya` (user or organisation) for later transfer or Pages, the crates.io and npm names if ever wanted, and a domain. Availability can change any day; this is a snapshot.

## Findings

| Where | Check | Result |
|---|---|---|
| GitHub repositories | `search/repositories?q=geeratya` | 0 results (also 0 for name, description and README) |
| GitHub user or organisation `geeratya` | `users/geeratya`, `orgs/geeratya`, user search | 404, 0 results: free |
| GitHub Pages `geeratya.github.io` | HTTP request | 404: nothing is served. It is free only while no user or organisation `geeratya` exists, so creating that account claims it. Alternatively `<owner>.github.io/Geeratya` works without a new account |
| crates.io | `api/v1/crates/geeratya` | 404: free |
| npm | registry `geeratya` | 404: free |
| PyPI | `pypi/geeratya/json` | 404: free |
| NuGet | flat container `geeratya` | 404: free |
| AUR | RPC search, `packages/geeratya`, `geeratya-bin` | 0 results, 404: free |
| Homebrew | formula and cask API `geeratya` | 404: free |
| winget | `microsoft/winget-pkgs` code search (needs login, so not run); manifest folder `manifests/g/Geeratya` | 404: free by folder. Final check is the winget validation of the first manifest PR |
| Domains | DNS lookup and `whois` | `geeratya.com`: no match (whois). `.io`, `.org`: not found (whois). `.dev`, `.app`, `.net`: no DNS record and whois gave no answer, so likely free but not confirmed. A registrar search is the authoritative check and was not done |
| General web search | "Geeratya", with "trademark", "brand", "software", "Jira terminal client" | No page, product, company or mark. Only near-miss strings (Geertruida, Geeraart, GEARYTE, GEEBS) |
| Trademark databases | WIPO Global Brand Database, USPTO, EUIPO | These need a browser (script-rendered search forms and blocked automated requests: Justia returned 403), so a real register search was **not** completed. Public aggregator pages found nothing. Treat "no conflict" as probable, not proven |

The name is a coined word with no dictionary meaning in the search results, which is good for distinctiveness and for search ranking. The closest-looking names are unrelated (Dutch given names, other software with "Gee" or "Gea" prefixes), and the spelling and the "-ya" ending differ enough to avoid a likelihood of confusion in classes 9 and 42 (software).

## Atlassian guidance

Sources: the [Atlassian trademark guidelines](https://www.atlassian.com/legal/trademark) and the [Marketplace brand guidelines](https://developer.atlassian.com/platform/marketplace/atlassian-brand-guidelines-for-marketplace-partners/). Geeratya is not a Marketplace app, so these apply by analogy, and they are the only published rules for third-party use of the name. The full trademark page is long and was only read through a summary, so confirm the exact wording before launch.

- Do not put "Jira" or "Atlassian" in the product name, repository name, package names or domain. Geeratya does not.
- Where the product is described, use a referential phrase: "a terminal client **for** Jira Cloud" is fine, "Jira terminal" or a name starting with "Jira" is not. The README already says "terminal client for Jira Cloud".
- Write "Jira" in title case, never "JIRA".
- The product must not suggest it is made or endorsed by Atlassian. Suggested line for the README footer and the site: "Geeratya is an independent project and is not affiliated with or endorsed by Atlassian. Jira is a trademark of Atlassian."
- Avoid Atlassian logos and the Jira icon; use none.
- Uncertain cases: Atlassian points to a support ticket (developer.atlassian.com/support). Not needed unless the owner wants to add "for Jira" to the name.

## Alternatives (only if a blocker appears)

None needed. If the owner still wants a fallback, candidates should be checked with the same list above first; the pattern to keep is a short coined word without "Jira" in it. Not researched further because the default is to keep the name.

## Open items for the owner

- Optional: claim the GitHub user or organisation `geeratya` before the project goes public (needed for `geeratya.github.io`).
- Optional: register a domain after a registrar search.
- Optional: a real register search at WIPO, USPTO and EUIPO in a browser (a few minutes, free) if a trademark filing is ever planned.
- Add the non-affiliation line to the README and the site when the release docs are written.
