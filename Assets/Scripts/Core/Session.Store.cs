using System;
using System.Collections.Generic;
using System.Linq;

namespace LastTide.Core
{
    /// <summary>A real-money product (App Store in-app purchase).</summary>
    public sealed class ProductDef
    {
        public string Id, Name, Blurb, FallbackPrice, Art;
        public int Coins, Energy;
        public ItemCount[] Items = new ItemCount[0];
        public bool Consumable = true;      // doubloon packs; one-time products are restored on a new device
        public string Perk;                 // a lasting upgrade ("bigpack": +8 backpack slots)
        public bool Best;                   // shown with a "best value" ribbon
    }

    /// <summary>One App Store transaction, as the store reports it.</summary>
    public struct StoreReceipt
    {
        public string ProductId, TransactionId;
        public double PurchasedAt;
        public StoreReceipt(string p, string t, double at) { ProductId = p; TransactionId = t; PurchasedAt = at; }
    }

    [Serializable]
    public sealed class PendingPurchase
    {
        public string productId;
        public double startedAt;
    }

    public static class Catalog
    {
        public static readonly List<ProductDef> Products = new List<ProductDef>
        {
            new ProductDef { Id = "lasttide.doubloons.small", Name = "Pouch of Doubloons", Blurb = "120 doubloons", FallbackPrice = "$0.99", Coins = 120, Art = "doubloon" },
            new ProductDef { Id = "lasttide.doubloons.medium", Name = "Purse of Doubloons", Blurb = "650 doubloons", FallbackPrice = "$4.99", Coins = 650, Art = "doubloon" },
            new ProductDef { Id = "lasttide.doubloons.large", Name = "Chest of Doubloons", Blurb = "1,400 doubloons", FallbackPrice = "$9.99", Coins = 1400, Art = "doubloon", Best = true },
            new ProductDef { Id = "lasttide.starter", Name = "Castaway Starter Pack", Blurb = "300 doubloons, a full meal, medicine, rope and a wooden spear. Once only.", FallbackPrice = "$1.99",
                Coins = 300, Energy = 100, Consumable = false, Art = "offer_starter",
                Items = new[] { Content.I("spear", 1), Content.I("bandage", 5), Content.I("meat", 6), Content.I("water", 6), Content.I("rope", 6) } },
            new ProductDef { Id = "lasttide.bigpack", Name = "Sailor's Big Backpack", Blurb = "10 more backpack slots, for good.", FallbackPrice = "$2.99", Consumable = false, Perk = "bigpack", Art = "offer_bigpack" },
            new ProductDef { Id = "lasttide.ultra", Name = "Survivor's Path: Ultra", Blurb = "Unlocks the golden Ultra rewards on every step of the Survivor's Path, past and future.", FallbackPrice = "$4.99",
                Consumable = false, Perk = "ultra", Art = "keyart" },
            new ProductDef { Id = Season.PremiumProduct, Name = "Season Pass: Premium", Blurb = "Unlocks the premium reward on every level of this season, including the companions.", FallbackPrice = "$4.99",
                Perk = "season", Art = "keyart" },
            new ProductDef { Id = "lasttide.captain", Name = "Captain's Chest", Blurb = "3,500 doubloons, a pirate cutlass and an iron harpoon.", FallbackPrice = "$19.99",
                Coins = 3500, Energy = 200, Art = "offer_builder", Items = new[] { Content.I("cutlass", 1), Content.I("harpoon", 1) } },
        };

        public static ProductDef Product(string id) { return Products.FirstOrDefault(p => p.Id == id); }
    }

    /// <summary>Real-money purchases (granted exactly once per transaction, even across a crash) and doubloon shortcuts.</summary>
    public sealed partial class Session
    {
        public const int RefillEnergyCost = 40, RushCraftCost = 5, RecoverBagCost = 15;
        public const int BigPackSlots = 10;

        public bool Owns(string productId) { return Flag("owned_" + productId); }
        public bool HasPendingPurchases { get { return State.pendingPurchases.Count > 0; } }

        public void BeginPurchase(string productId)
        {
            State.pendingPurchases.RemoveAll(p => p.productId == productId);
            State.pendingPurchases.Add(new PendingPurchase { productId = productId, startedAt = RealNow() });
        }

        public void EndPurchase(string productId) { State.pendingPurchases.RemoveAll(p => p.productId == productId); }

        /// <summary>Gives what a paid product contains. A transaction id already granted gives nothing again.</summary>
        public bool GrantPurchase(string productId, string transactionId)
        {
            var p = Catalog.Product(productId);
            EndPurchase(productId);
            if (p == null) return false;
            if (!string.IsNullOrEmpty(transactionId))
            {
                if (State.grantedTx.Contains(transactionId)) return false;
                State.grantedTx.Add(transactionId);
            }
            if (!p.Consumable)
            {
                if (Owns(productId)) return false;
                SetFlag("owned_" + productId);
            }
            Deliver(p);
            Toast?.Invoke(p.Name + " is yours!");
            Changed?.Invoke();
            return true;
        }

        void Deliver(ProductDef p)
        {
            State.coins += p.Coins;
            if (p.Energy > 0) State.energy = Math.Min(World.EnergyMax, State.energy + p.Energy);
            if (p.Perk == "bigpack") State.Repair(BackpackSize); // the new slots first: the pack's items may need them
            foreach (var i in p.Items) GiveOrHold(i.id, i.n);
            if (p.Perk == "season") GrantSeasonPremium();
        }

        /// <summary>Total backpack slots (more with the big backpack).</summary>
        public int BackpackSize { get { return Content.BackpackSlots + (Owns("lasttide.bigpack") ? BigPackSlots : 0); } }

        /// <summary>Delivers purchases this device started but never rewarded (the app died mid-purchase). Returns their names.</summary>
        public List<string> ReconcilePurchases(IEnumerable<StoreReceipt> receipts)
        {
            var got = new List<string>();
            foreach (var pending in State.pendingPurchases.ToList())
            {
                var hit = receipts.Where(r => r.ProductId == pending.productId && r.PurchasedAt >= pending.startedAt - 120 && !State.grantedTx.Contains(r.TransactionId))
                    .OrderBy(r => r.PurchasedAt).FirstOrDefault();
                if (hit.ProductId == null) continue;
                if (GrantPurchase(pending.productId, hit.TransactionId)) got.Add(Catalog.Product(pending.productId).Name);
            }
            return got;
        }

        /// <summary>One-time products the account owns come back on a new device. Returns how many were restored.</summary>
        public int RestoreNonConsumables(IEnumerable<string> productIds)
        {
            int n = 0;
            foreach (var id in productIds ?? new string[0])
            {
                var p = Catalog.Product(id);
                if (p == null || p.Consumable || Owns(id)) continue;
                SetFlag("owned_" + id);
                // The lasting perk comes back; the starter pack's consumables were used on the old device.
                if (p.Perk != null) Deliver(new ProductDef { Perk = p.Perk });
                n++;
            }
            if (n > 0) Changed?.Invoke();
            return n;
        }

        // ------------------------------------------------------------------ doubloon shortcuts

        public bool Spend(int coins)
        {
            if (State.coins < coins) { Toast?.Invoke("Not enough doubloons"); return false; }
            State.coins -= coins;
            Changed?.Invoke();
            return true;
        }

        public bool RefillEnergy()
        {
            if (State.energy >= World.EnergyMax) { Toast?.Invoke("Energy is already full"); return false; }
            if (!Spend(RefillEnergyCost)) return false;
            State.energy = World.EnergyMax;
            State.energyStamp = RealNow();
            return true;
        }

        /// <summary>Doubloons to arrive at once: one per half minute still to go.</summary>
        public int RushTravelCost { get { var t = State.travel; return t == null ? 0 : Math.Max(2, (int)Math.Ceiling((t.arriveAt - RealNow()) / 30.0)); } }

        public bool RushTravel()
        {
            if (State.travel == null || Arrived) return false;
            if (!Spend(RushTravelCost)) return false;
            State.travel.arriveAt = RealNow();
            return true;
        }

        public bool RushCraft(string station)
        {
            var j = Job(station);
            // Charge what the SKIP button shows; a job that is already done (waiting for room) needs no rush.
            if (j == null || j.doneAt <= Now || !Spend(RushCost(station))) return false;
            j.doneAt = Now;
            return true;
        }

        public bool RecoverBag()
        {
            var bag = NewestBag;
            if (bag == null || !Spend(RecoverBagCost)) return false;
            // Whatever doesn't fit lands at the castaway's feet.
            bag.location = State.location; bag.x = State.px; bag.z = State.pz;
            TakeBag(bag);
            return true;
        }
    }
}
