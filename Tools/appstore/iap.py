#!/usr/bin/env python3
"""Creates Last Tide's in-app purchases in App Store Connect (run by the Codemagic workflow `asc-iap`).

  python3 Tools/appstore/iap.py          # needs APP_STORE_CONNECT_PRIVATE_KEY / _KEY_IDENTIFIER / _ISSUER_ID and PyJWT

For every product in PRODUCTS (the same ids, names and prices as Catalog.Products in Core/Session.Store.cs) it makes
sure there is: the in-app purchase, an en-US display name and description, a price (set in the USA, other storefronts
follow Apple's equivalents), availability in every territory and the App Store review screenshot (the in-game SHOP,
docs/appstore/iap_review.png, read from this repo's main branch on GitHub or from IAP_SCREENSHOT, a path or URL; a
screenshot whose checksum differs from that file is replaced). Anything already there is left alone, so running it
again is safe.
"""
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.request

import jwt

BUNDLE_ID = "com.rushingtech.lasttide"
API = "https://api.appstoreconnect.apple.com"
SCREENSHOT = os.environ.get("IAP_SCREENSHOT") or \
    "https://raw.githubusercontent.com/mrnickrushing/castaway/main/docs/appstore/iap_review.png"

# productId, type, price (USD), display name (<= 30), description (<= 45)
PRODUCTS = [
    ("lasttide.doubloons.small", "CONSUMABLE", "0.99", "Pouch of Doubloons", "120 doubloons"),
    ("lasttide.doubloons.medium", "CONSUMABLE", "4.99", "Purse of Doubloons", "650 doubloons"),
    ("lasttide.doubloons.large", "CONSUMABLE", "9.99", "Chest of Doubloons", "1,400 doubloons"),
    ("lasttide.starter", "NON_CONSUMABLE", "1.99", "Castaway Starter Pack", "Doubloons, food, medicine, rope and a spear"),
    ("lasttide.bigpack", "NON_CONSUMABLE", "2.99", "Sailor's Big Backpack", "10 more backpack slots, for good"),
    ("lasttide.ultra", "NON_CONSUMABLE", "4.99", "Survivor's Path: Ultra", "Golden rewards on every Survivor's Path step"),
    ("lasttide.season", "CONSUMABLE", "4.99", "Season Pass: Premium", "Premium rewards for the current season"),
    ("lasttide.captain", "CONSUMABLE", "19.99", "Captain's Chest", "3,500 doubloons, a cutlass and a harpoon"),
]


def token():
    key = os.environ["APP_STORE_CONNECT_PRIVATE_KEY"].replace("\\n", "\n")
    now = int(time.time())
    return jwt.encode({"iss": os.environ["APP_STORE_CONNECT_ISSUER_ID"], "iat": now, "exp": now + 1100,
                       "aud": "appstoreconnect-v1"}, key, algorithm="ES256",
                      headers={"kid": os.environ["APP_STORE_CONNECT_KEY_IDENTIFIER"], "typ": "JWT"})


TOKEN = token()


def call(method, path, body=None, ok404=False):
    url = path if path.startswith("http") else API + path
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method,
                                 headers={"Authorization": "Bearer " + TOKEN, "Content-Type": "application/json"})
    for attempt in range(5):
        try:
            with urllib.request.urlopen(req) as r:
                raw = r.read()
                return json.loads(raw) if raw else {}
        except urllib.error.HTTPError as e:
            text = e.read().decode(errors="replace")
            if ok404 and e.code == 404:
                return None
            if e.code >= 500 and attempt < 4:
                # Apple's API has the odd server hiccup: wait and try again.
                print("  %s %s -> %d, retrying" % (method, url, e.code))
                time.sleep(5 * (attempt + 1))
                continue
            raise SystemExit("%s %s -> %d\n%s" % (method, url, e.code, text[:2000]))


def pages(path):
    out, url = [], path
    while url:
        r = call("GET", url)
        out += r.get("data", [])
        url = r.get("links", {}).get("next")
    return out


def screenshot_bytes():
    if SCREENSHOT.startswith("http"):
        with urllib.request.urlopen(SCREENSHOT) as r:
            return r.read()
    with open(SCREENSHOT, "rb") as f:
        return f.read()


def upload_screenshot(iap, png):
    """Reserves the review screenshot, sends its parts to Apple's upload URLs and commits it with its checksum."""
    shot = call("POST", "/v1/inAppPurchaseAppStoreReviewScreenshots", {"data": {
        "type": "inAppPurchaseAppStoreReviewScreenshots",
        "attributes": {"fileName": "iap_review.png", "fileSize": len(png)},
        "relationships": {"inAppPurchaseV2": {"data": {"type": "inAppPurchases", "id": iap}}}}})["data"]
    for op in shot["attributes"]["uploadOperations"]:
        part = png[op["offset"]:op["offset"] + op["length"]]
        req = urllib.request.Request(op["url"], data=part, method=op["method"],
                                     headers={h["name"]: h["value"] for h in op.get("requestHeaders", [])})
        with urllib.request.urlopen(req) as r:
            r.read()
    call("PATCH", "/v1/inAppPurchaseAppStoreReviewScreenshots/" + shot["id"], {"data": {
        "type": "inAppPurchaseAppStoreReviewScreenshots", "id": shot["id"],
        "attributes": {"uploaded": True, "sourceFileChecksum": hashlib.md5(png).hexdigest()}}})


def main():
    apps = call("GET", "/v1/apps?filter[bundleId]=%s" % BUNDLE_ID)["data"]
    apps = [a for a in apps if a["attributes"]["bundleId"] == BUNDLE_ID]
    if not apps:
        raise SystemExit("No App Store Connect app with bundle id " + BUNDLE_ID)
    app = apps[0]["id"]
    print("APPLE_ID", app, apps[0]["attributes"]["name"])
    have = {i["attributes"]["productId"]: i for i in pages("/v1/apps/%s/inAppPurchasesV2?limit=200" % app)}
    territories = None
    png = None

    for pid, kind, price, name, desc in PRODUCTS:
        assert len(name) <= 30 and len(desc) <= 45, pid
        if pid in have:
            iap = have[pid]["id"]
            print("exists ", pid, iap, have[pid]["attributes"].get("state"))
        else:
            iap = call("POST", "/v2/inAppPurchases", {"data": {
                "type": "inAppPurchases",
                "attributes": {"name": name, "productId": pid, "inAppPurchaseType": kind,
                               "familySharable": False},
                "relationships": {"app": {"data": {"type": "apps", "id": app}}}}})["data"]["id"]
            print("created", pid, iap)

        locs = pages("/v2/inAppPurchases/%s/inAppPurchaseLocalizations?limit=50" % iap)
        if not any(l["attributes"]["locale"] == "en-US" for l in locs):
            call("POST", "/v1/inAppPurchaseLocalizations", {"data": {
                "type": "inAppPurchaseLocalizations",
                "attributes": {"locale": "en-US", "name": name, "description": desc},
                "relationships": {"inAppPurchaseV2": {"data": {"type": "inAppPurchases", "id": iap}}}}})
            print("  en-US name and description")

        sched = call("GET", "/v2/inAppPurchases/%s/iapPriceSchedule?include=manualPrices" % iap, ok404=True)
        if not (sched and sched.get("included")):
            points = pages("/v2/inAppPurchases/%s/pricePoints?filter[territory]=USA&limit=200" % iap)
            point = next((p for p in points if p["attributes"]["customerPrice"] == price), None)
            if point is None:
                raise SystemExit("No USA price point at $%s for %s" % (price, pid))
            call("POST", "/v1/inAppPurchasePriceSchedules", {
                "data": {"type": "inAppPurchasePriceSchedules",
                         "relationships": {
                             "inAppPurchase": {"data": {"type": "inAppPurchases", "id": iap}},
                             "baseTerritory": {"data": {"type": "territories", "id": "USA"}},
                             "manualPrices": {"data": [{"type": "inAppPurchasePrices", "id": "${p0}"}]}}},
                "included": [{"type": "inAppPurchasePrices", "id": "${p0}",
                              "attributes": {"startDate": None},
                              "relationships": {
                                  "inAppPurchaseV2": {"data": {"type": "inAppPurchases", "id": iap}},
                                  "inAppPurchasePricePoint": {"data": {"type": "inAppPurchasePricePoints",
                                                                       "id": point["id"]}}}}]})
            print("  price $%s (USA base)" % price)

        avail = call("GET", "/v2/inAppPurchases/%s/inAppPurchaseAvailability" % iap, ok404=True)
        if not (avail and avail.get("data")):
            if territories is None:
                territories = [t["id"] for t in pages("/v1/territories?limit=200")]
            call("POST", "/v1/inAppPurchaseAvailabilities", {"data": {
                "type": "inAppPurchaseAvailabilities",
                "attributes": {"availableInNewTerritories": True},
                "relationships": {
                    "inAppPurchase": {"data": {"type": "inAppPurchases", "id": iap}},
                    "availableTerritories": {"data": [{"type": "territories", "id": t} for t in territories]}}}})
            print("  available in %d territories" % len(territories))

        if png is None:
            png = screenshot_bytes()
        shot = call("GET", "/v2/inAppPurchases/%s/appStoreReviewScreenshot" % iap, ok404=True)
        attrs = ((shot or {}).get("data") or {}).get("attributes", {})
        done = (attrs.get("assetDeliveryState") or {}).get("state") in ("UPLOAD_COMPLETE", "COMPLETE")
        if not (done and attrs.get("sourceFileChecksum") == hashlib.md5(png).hexdigest()):
            if shot and shot.get("data"):
                # A different picture, or a reservation that never finished uploading: replace it.
                call("DELETE", "/v1/inAppPurchaseAppStoreReviewScreenshots/" + shot["data"]["id"])
            upload_screenshot(iap, png)
            print("  review screenshot (%d bytes)" % len(png))

    print("IAP done")
    for i in pages("/v1/apps/%s/inAppPurchasesV2?limit=200" % app):
        a = i["attributes"]
        print("IAP", a["productId"], a["inAppPurchaseType"], a.get("state"))


if __name__ == "__main__":
    sys.exit(main())
