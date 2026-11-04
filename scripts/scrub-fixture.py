#!/usr/bin/env python3
"""Scrub a Jira JSON response read from stdin; scrubbed JSON goes to stdout.

usage: scrub-fixture.py <site-host>

Host -> example.atlassian.net, account ids -> fake-account-N, emails ->
userN@example.com, display names of users (and ADF mention texts) -> Test User N,
avatar URLs dropped,
cloud ids zeroed, time zones set to UTC, UUIDs inside Atlassian resource ids (ari:...: the cloud id, the
activation id) -> stable fakes (a cloud id that the same document names with a cloudId key keeps the all-zero
value; one that only appears inside an id gets the next fake). Used by record-fixture.sh.
"""
import json
import re
import sys
from urllib.parse import quote

EMAIL = re.compile(r"[A-Za-z0-9._%+-]+@(?!example\.com\b)[A-Za-z0-9.-]+\.[A-Za-z]{2,}")
host = sys.argv[1]
data = json.load(sys.stdin)

accounts = {}
emails = {}
uuids = {}
cloud_ids = set()
UUID = re.compile(r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")
ZERO_UUID = "00000000-0000-0000-0000-000000000000"


def fake_uuid(raw):
    if raw.lower() in cloud_ids:
        return ZERO_UUID
    number = uuids.setdefault(raw.lower(), len(uuids) + 1)
    return "00000000-0000-0000-0000-%012d" % number


def acct(raw):
    return "fake-account-%d" % accounts.setdefault(raw, len(accounts) + 1)


def is_user(node):
    return "accountId" in node or "accountType" in node


# Pass 1: learn every account id so it can also be replaced inside URLs/text.
def collect(node):
    if isinstance(node, dict):
        if isinstance(node.get("accountId"), str):
            acct(node["accountId"])
        if node.get("type") == "mention" and isinstance(node.get("attrs", {}).get("id"), str):
            acct(node["attrs"]["id"])
        for key in ("cloudId", "cloud_id"):
            if isinstance(node.get(key), str):
                cloud_ids.add(node[key].lower())
        for v in node.values():
            collect(v)
    elif isinstance(node, list):
        for v in node:
            collect(v)


collect(data)


def text(s):
    s = s.replace(host, "example.atlassian.net")
    if "ari:" in s:
        s = UUID.sub(lambda m: fake_uuid(m.group(0)), s)
    s = EMAIL.sub(lambda m: "user%d@example.com" % emails.setdefault(m.group(0), len(emails) + 1), s)
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
            elif k in ("cloudId", "cloud_id") and isinstance(v, str):
                out[k] = ZERO_UUID
            else:
                out[k] = walk(v)
        if node.get("type") == "mention" and isinstance(out.get("attrs"), dict) and "text" in out["attrs"]:
            # ADF mention: the visible text is a display name.
            out["attrs"]["text"] = "@Test User %d" % accounts.get(node["attrs"].get("id"), 0)
        return out
    if isinstance(node, list):
        return [walk(v) for v in node]
    if isinstance(node, str):
        return text(node)
    return node


json.dump(walk(data), sys.stdout, indent=2, sort_keys=True, ensure_ascii=False)
sys.stdout.write("\n")
