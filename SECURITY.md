# Security policy

## Reporting a vulnerability

Do not open a public issue. Use the repository's **Security** tab, choose **Report a vulnerability**, and include the version (`geeratya --version`) and steps to reproduce. Do not include a real API token or site name; describe them instead.

You can expect an acknowledgement within a week. Geeratya is maintained by one person in spare time, so fixes are best effort. A confirmed issue is fixed in a following release and credited if you wish.

## Scope

Geeratya is a local client that talks to your own Jira Cloud site with an API token. In scope:

- Token handling: the token leaking into logs, error messages, the cache, saved tabs, crash output or a file, or being sent to a host other than your site.
- The secret store: unsafe use of `secret-tool`, Credential Manager or the Keychain, or a token written outside it.
- Fixtures and test data: a token, an account id or a private host shipped in the repository or a release.
- Content from Jira (descriptions, comments, links) that makes the terminal do something it should not, such as escape sequence injection.
- A destructive write that happens without the confirmation the app promises.

Out of scope: problems in Jira itself, in your terminal, or in a tool you configured as your editor.

## Supported versions

The latest release. Until the first release is published, the `main` branch.
