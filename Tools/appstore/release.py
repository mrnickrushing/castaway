#!/usr/bin/env python3
"""Prepares an update of an app that is already on the App Store, and (only when asked) sends it to App Review.
Works for any app the App Store Connect key can see; run by the Codemagic workflow `asc-release` in the train app.

  RELEASE_BUNDLE_ID=com.rushingtech.lasttide RELEASE_VERSION=1.0.1 RELEASE_NOTES="What's new text" \
  [RELEASE_BUILD=5] [RELEASE_SUBMIT=1] python3 Tools/appstore/release.py

Steps, each safe to run again:
  1. the version: uses the one being prepared, or creates RELEASE_VERSION (released automatically once approved);
  2. en-US "What's New": RELEASE_NOTES;
  3. the build: the newest processed build of that version (export compliance: no non-exempt encryption);
  4. with RELEASE_SUBMIT=1: adds the version to a review submission and submits it.
It prints the state it ends in. Without RELEASE_SUBMIT nothing is sent to Apple's reviewers.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iap import call, pages  # noqa: E402  (same key, same API helpers)

BUNDLE = os.environ["RELEASE_BUNDLE_ID"]
WANT = os.environ["RELEASE_VERSION"]
NOTES = os.environ.get("RELEASE_NOTES", "").replace("\\n", "\n").strip()
SUBMIT = os.environ.get("RELEASE_SUBMIT") == "1"
EDITABLE = ("PREPARE_FOR_SUBMISSION", "DEVELOPER_REJECTED", "REJECTED", "METADATA_REJECTED", "INVALID_BINARY")

apps = [a for a in call("GET", "/v1/apps?filter[bundleId]=%s" % BUNDLE)["data"] if a["attributes"]["bundleId"] == BUNDLE]
if not apps:
    raise SystemExit("No App Store Connect app with bundle id " + BUNDLE)
APP = apps[0]["id"]
print("APPLE_ID", APP, apps[0]["attributes"]["name"])

# Where the in-app purchases stand (printed every run: a first version must take its purchases with it).
states = {}
for i in pages("/v1/apps/%s/inAppPurchasesV2?limit=200" % APP):
    states.setdefault(i["attributes"]["state"], []).append(i["attributes"]["productId"])
print("in-app purchases:", "; ".join("%s: %s" % (k, ", ".join(sorted(v))) for k, v in sorted(states.items())) or "none")

def submit_iaps():
    """In-app purchases still waiting to be submitted (RELEASE_IAPS=1). A refusal is reported, not fatal: Apple wants
    an app's very first purchases added on the version's page of the website."""
    for i in pages("/v1/apps/%s/inAppPurchasesV2?limit=200" % APP):
        if i["attributes"]["state"] != "READY_TO_SUBMIT":
            print("  purchase %s: %s" % (i["attributes"]["productId"], i["attributes"]["state"]))
            continue
        try:
            call("POST", "/v1/inAppPurchaseSubmissions", {"data": {"type": "inAppPurchaseSubmissions", "relationships": {
                "inAppPurchaseV2": {"data": {"type": "inAppPurchases", "id": i["id"]}}}}})
            print("  purchase %s: submitted" % i["attributes"]["productId"])
        except SystemExit as e:
            print("  purchase %s: NOT submitted\n    %s" % (i["attributes"]["productId"], str(e)[:1600].replace("\n", "\n    ")))


# 1. The version.
versions = pages("/v1/apps/%s/appStoreVersions?filter[platform]=IOS&limit=20" % APP)
print("versions:", [(v["attributes"]["versionString"], v["attributes"].get("appVersionState")) for v in versions])
# READY_FOR_REVIEW is a version sitting in a draft submission that hasn't been sent yet: it is still ours to finish.
version = next((v for v in versions if v["attributes"].get("appVersionState") in EDITABLE + ("READY_FOR_REVIEW",)), None)
waiting = next((v for v in versions if v["attributes"].get("appVersionState") in ("WAITING_FOR_REVIEW", "IN_REVIEW")), None)
if waiting is not None and version is None:
    print("Version %s is already with App Review (%s)." % (waiting["attributes"]["versionString"], waiting["attributes"]["appVersionState"]))
    if os.environ.get("RELEASE_IAPS") == "1" and SUBMIT:
        submit_iaps()       # a purchase that missed the version's submission can follow it
    sys.exit(0)
if version is None:
    version = call("POST", "/v1/appStoreVersions", {"data": {
        "type": "appStoreVersions", "attributes": {"platform": "IOS", "versionString": WANT, "releaseType": "AFTER_APPROVAL"},
        "relationships": {"app": {"data": {"type": "apps", "id": APP}}}}})["data"]
    print("created version", WANT)
elif version["attributes"]["versionString"] != WANT:
    call("PATCH", "/v1/appStoreVersions/" + version["id"], {"data": {"type": "appStoreVersions", "id": version["id"], "attributes": {"versionString": WANT}}})
    print("renamed the version being prepared to", WANT)
VID = version["id"]

# 2. What's New.
if NOTES:
    assert len(NOTES) <= 4000
    for loc in pages("/v1/appStoreVersions/%s/appStoreVersionLocalizations" % VID):
        call("PATCH", "/v1/appStoreVersionLocalizations/" + loc["id"],
             {"data": {"type": "appStoreVersionLocalizations", "id": loc["id"], "attributes": {"whatsNew": NOTES}}})
        print("what's new set for", loc["attributes"]["locale"])

# 3. The build.
builds = call("GET", "/v1/builds?filter[app]=%s&filter[preReleaseVersion.version]=%s&filter[processingState]=VALID"
                     "&filter[expired]=false&sort=-uploadedDate&limit=1" % (APP, WANT))["data"]
if not builds:
    raise SystemExit("No processed build of version %s yet: upload one (or wait for Apple to finish processing) and run again." % WANT)
b = builds[0]
if b["attributes"].get("usesNonExemptEncryption") is None:
    call("PATCH", "/v1/builds/" + b["id"], {"data": {"type": "builds", "id": b["id"], "attributes": {"usesNonExemptEncryption": False}}})
WANT_BUILD = os.environ.get("RELEASE_BUILD")
if WANT_BUILD and b["attributes"]["version"] != WANT_BUILD:
    raise SystemExit("The newest processed build is %s, not the %s asked for: wait for Apple to finish processing and run again." % (b["attributes"]["version"], WANT_BUILD))
try:
    call("PATCH", "/v1/appStoreVersions/%s/relationships/build" % VID, {"data": {"type": "builds", "id": b["id"]}})
    print("build %s attached (uploaded %s)" % (b["attributes"]["version"], b["attributes"].get("uploadedDate")))
except SystemExit as e:
    cur = call("GET", "/v1/appStoreVersions/%s/build" % VID, ok404=True)
    have = cur and cur.get("data") and cur["data"]["attributes"]["version"]
    if have != b["attributes"]["version"]:
        raise SystemExit("Build %s could not be attached (the version has %s):\n%s" % (b["attributes"]["version"], have, e))
    print("build %s was already attached" % have)

# 3a. Game Center on the version (RELEASE_GAMECENTER=1): a build with the Game Center entitlement can't be reviewed
# without it.
if os.environ.get("RELEASE_GAMECENTER") == "1":
    have = call("GET", "/v1/appStoreVersions/%s/gameCenterAppVersion" % VID, ok404=True)
    if have and have.get("data"):
        gc = have["data"]
        if gc["attributes"].get("enabled") is False:
            call("PATCH", "/v1/gameCenterAppVersions/" + gc["id"], {"data": {"type": "gameCenterAppVersions", "id": gc["id"], "attributes": {"enabled": True}}})
        print("Game Center is on for the version")
    else:
        call("POST", "/v1/gameCenterAppVersions", {"data": {"type": "gameCenterAppVersions", "relationships": {
            "appStoreVersion": {"data": {"type": "appStoreVersions", "id": VID}}}}})
        print("Game Center turned on for the version")

# 3b. The App Review contact, when given (a first version has none yet). Passed in at run time, never stored.
if os.environ.get("RELEASE_REVIEW_EMAIL"):
    attrs = {"contactFirstName": os.environ.get("RELEASE_REVIEW_FIRST"), "contactLastName": os.environ.get("RELEASE_REVIEW_LAST"),
             "contactPhone": os.environ.get("RELEASE_REVIEW_PHONE"), "contactEmail": os.environ["RELEASE_REVIEW_EMAIL"],
             "demoAccountRequired": False}
    if os.environ.get("RELEASE_REVIEW_NOTES"):
        attrs["notes"] = os.environ["RELEASE_REVIEW_NOTES"].replace("\\n", "\n")
    have = call("GET", "/v1/appStoreVersions/%s/appStoreReviewDetail" % VID, ok404=True)
    if have and have.get("data"):
        call("PATCH", "/v1/appStoreReviewDetails/" + have["data"]["id"], {"data": {"type": "appStoreReviewDetails", "id": have["data"]["id"], "attributes": attrs}})
    else:
        call("POST", "/v1/appStoreReviewDetails", {"data": {"type": "appStoreReviewDetails", "attributes": attrs,
             "relationships": {"appStoreVersion": {"data": {"type": "appStoreVersions", "id": VID}}}}})
    print("review contact set")

if os.environ.get("RELEASE_IAPS") == "1" and SUBMIT:
    submit_iaps()

# 4. App Review, only when asked.
if not SUBMIT:
    print("READY: version %s has its build and notes. Not submitted (RELEASE_SUBMIT is not 1)." % WANT)
    sys.exit(0)
subs = call("GET", "/v1/reviewSubmissions?filter[app]=%s&filter[platform]=IOS&filter[state]=READY_FOR_REVIEW,UNRESOLVED_ISSUES" % APP)["data"]
sub = subs[0] if subs else call("POST", "/v1/reviewSubmissions", {"data": {
    "type": "reviewSubmissions", "attributes": {"platform": "IOS"},
    "relationships": {"app": {"data": {"type": "apps", "id": APP}}}}})["data"]
def add_item(rel, kind, rid, what):
    """Adds one thing to the review submission; one that is there already (or can't go in) is reported, not fatal."""
    try:
        call("POST", "/v1/reviewSubmissionItems", {"data": {"type": "reviewSubmissionItems", "relationships": {
            "reviewSubmission": {"data": {"type": "reviewSubmissions", "id": sub["id"]}},
            rel: {"data": {"type": kind, "id": rid}}}}})
        print("  added", what)
        return True
    except SystemExit as e:
        text = str(e)
        print("  not added: %s (%s)" % (what, "already in a submission" if "ALREADY" in text.upper() else text[:700].replace("\n", " ")))
        return False


have_items = pages("/v1/reviewSubmissions/%s/items?include=appStoreVersion" % sub["id"])
if not any((i.get("relationships", {}).get("appStoreVersion", {}) or {}).get("data") for i in have_items):
    try:
        call("POST", "/v1/reviewSubmissionItems", {"data": {"type": "reviewSubmissionItems", "relationships": {
            "reviewSubmission": {"data": {"type": "reviewSubmissions", "id": sub["id"]}},
            "appStoreVersion": {"data": {"type": "appStoreVersions", "id": VID}}}}})
        print("version added to the review submission")
    except SystemExit as e:
        raise SystemExit("The version could not be added to the review submission:\n%s" % e)
# Game Center leaderboards and achievements go to review with the version (RELEASE_GAMECENTER=1).
if os.environ.get("RELEASE_GAMECENTER") == "1":
    detail = call("GET", "/v1/apps/%s/gameCenterDetail" % APP, ok404=True)
    if detail and detail.get("data"):
        for kind in ("Leaderboard", "Achievement"):
            n = 0
            for it in pages("/v1/gameCenterDetails/%s/gameCenter%ssV2?limit=200" % (detail["data"]["id"], kind)):
                versions = pages("/v2/gameCenter%ss/%s/versions?limit=50" % (kind, it["id"]))
                if not versions:
                    continue
                v = max(versions, key=lambda x: x["attributes"].get("version") or 0)
                if (v["attributes"].get("state") or "") in ("LIVE", "WAITING_FOR_REVIEW", "IN_REVIEW"):
                    continue
                n += add_item("gameCenter%sVersion" % kind, "gameCenter%sVersions" % kind, v["id"], "%s %s" % (kind.lower(), it["attributes"]["vendorIdentifier"]))
            print("%ss added: %d" % (kind, n))
print("submission items:", len(pages("/v1/reviewSubmissions/%s/items" % sub["id"])))
call("PATCH", "/v1/reviewSubmissions/" + sub["id"], {"data": {"type": "reviewSubmissions", "id": sub["id"], "attributes": {"submitted": True}}})
state = call("GET", "/v1/appStoreVersions/" + VID)["data"]["attributes"].get("appVersionState")
print("SUBMITTED: version %s is now %s" % (WANT, state))
