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
| lasttide.captain | Consumable | $19.99 | 3,500 doubloons, cutlass, harpoon, full energy |

To go live:
1. Create the app in App Store Connect (bundle id `com.rushingtech.lasttide`).
2. Create the seven in-app purchases above with the same ids.
3. Create the app in RevenueCat, attach the products, and put its Apple public key (`appl_...`) in
   `RevenueCatStore.ApiKey`.

Grants are idempotent per App Store transaction; a purchase interrupted by a crash is delivered on the next launch
(`Session.ReconcilePurchases`). One-time products come back with Restore Purchases.
