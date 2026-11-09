# In-app purchases

Products are defined in `Assets/Scripts/Core/Session.Store.cs` (`Catalog.Products`). Purchases go through
`Assets/Scripts/Game/Store/StoreService.cs`: RevenueCat on device, a test store (instant fake purchases) in the
Editor and development builds, nothing in a release build until RevenueCat is configured.

| Product id | Type | Price | Contents |
|---|---|---|---|
| lasttide.doubloons.small | Consumable | $0.99 | 120 doubloons |
| lasttide.doubloons.medium | Consumable | $4.99 | 650 doubloons |
| lasttide.doubloons.large | Consumable | $9.99 | 1,400 doubloons |
| lasttide.starter | Non-consumable | $1.99 | 300 doubloons, spear, food, water, bandages, rope, energy |
| lasttide.bigpack | Non-consumable | $2.99 | +10 backpack slots |
| lasttide.ultra | Non-consumable | $4.99 | Survivor's Path Ultra track (golden rewards on every step) |
| lasttide.season | Consumable | $4.99 | Season pass premium for the current four-week season (premium reward on every level, Nanook and Grey) |
| lasttide.captain | Consumable | $19.99 | 3,500 doubloons, cutlass, harpoon, full energy |

To go live:
1. Done: the app exists in App Store Connect (bundle id `com.rushingtech.lasttide`, Apple ID 6832382482).
2. Done 2026-11-09: all eight in-app purchases exist in App Store Connect with en-US names and descriptions, USA
   base prices and every territory (`Tools/appstore/iap.py`, Codemagic `lasttide-iap`; safe to rerun). Each has its
   review screenshot (the SHOP, `docs/appstore/iap_review.png`) and is READY_TO_SUBMIT: they go to review with the
   first app version (pick them on the version page under In-App Purchases).
3. Done 2026-11-09: RevenueCat project **Last Tide** (`bd280fd2`) with the App Store app `appe3954014ef` for the
   bundle id, the team's in-app purchase key (8V5W657NH4, validates purchases) and App Store Connect API key
   (CBXMPCMX4L, imports products and prices), and all eight products imported. Its public Apple SDK key is in
   `RevenueCatStore.ApiKey`. The game buys and restores by product id, so no entitlements or offerings are needed.
4. Before the first sandbox purchase: the Paid Apps Agreement must be active (App Store Connect → Business), then
   buy in a TestFlight build with a sandbox account; the purchase shows under Customers in RevenueCat.

Grants are idempotent per App Store transaction; a purchase interrupted by a crash is delivered on the next launch
(`Session.ReconcilePurchases`). One-time products come back with Restore Purchases.
