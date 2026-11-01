#!/usr/bin/env python3
"""Scrub a Jira JSON response read from stdin; scrubbed JSON goes to stdout.

usage: scrub-fixture.py <site-host>

Host -> example.atlassian.net, account ids -> fake-account-N, emails ->
userN@example.com, display names of users -> Test User N, avatar URLs dropped,
cloud ids zeroed, time zones set to UTC. Used by record-fixture.sh.
"""
import json
import re
import sys
from urllib.parse import quote

host = sys.argv[1]
data = json.load(sys.stdin)

accounts = {}
emails = {}


def acct(raw):
    return "fake-account-%d" % accounts.setdefault(raw, len(accounts) + 1)


def is_user(node):
    return "accountId" in node or "accountType" in node


# Pass 1: learn every account id so it can also be replaced inside URLs/text.
def collect(node):
    if isinstance(node, dict):
        if isinstance(node.get("accountId"), str):
            acct(node["accountId"])
        for v in node.values():
            collect(v)
    elif isinstance(node, list):
        for v in node:
            collect(v)


collect(data)


def text(s):
    s = s.replace(host, "example.atlassian.net")
    for raw in sorted(accounts, key=lambda r: -len(r)):
        fake = "fake-account-%d" % accounts[raw]
        for variant in (raw, quote(raw, safe=""), quote(raw)):
            s = s.replace(variant, fake)
    return s


def walk(node):
    if isinstance(node, dict):
        out = {}
        user = is_user(node)
        for k, v in node.items():
            if k == "avatarUrls":
                out[k] = {}
            elif k == "emailAddress" and isinstance(v, str):
                out[k] = "user%d@example.com" % emails.setdefault(v, len(emails) + 1)
            elif k == "displayName" and user and isinstance(v, str):
                out[k] = "Test User %d" % accounts[node["accountId"]] if "accountId" in node else "Test User"
            elif k == "timeZone" and isinstance(v, str):
                out[k] = "UTC"
            elif k == "cloudId" and isinstance(v, str):
                out[k] = "00000000-0000-0000-0000-000000000000"
            else:
                out[k] = walk(v)
        return out
    if isinstance(node, list):
        return [walk(v) for v in node]
    if isinstance(node, str):
        return text(node)
    return node


json.dump(walk(data), sys.stdout, indent=2, sort_keys=True, ensure_ascii=False)
sys.stdout.write("\n")
