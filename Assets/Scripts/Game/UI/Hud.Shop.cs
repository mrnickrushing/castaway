using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// The shop: doubloon packs and one-time products for real money (App Store, via the store service), a link to the
    /// trading post (packs for doubloons), and Restore Purchases. Also the small confirm dialog used for doubloon spends.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _shop, _shopGrid;
        bool _shopOpen;

        void BuildShop()
        {
            _shop = UIKit.Img("Shop", _root, null, new Color(0.1f, 0.13f, 0.13f, 1f)).rectTransform;
            _shop.Fill();
            _shop.GetComponent<Image>().raycastTarget = true;
            var title = UIKit.Label("Title", _shop, "SHOP", 64, UIKit.Sand, TextAnchor.UpperCenter);
            title.rectTransform.Fill(); title.rectTransform.offsetMax = new Vector2(0, -34);
            var x = UIKit.Button("Close", _shop, "X", new Color(0, 0, 0, 0), () => { _shopOpen = false; _dirty = true; }, 80);
            ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -30), new Vector2(120, 120));
            _shopGrid = UIKit.Rect("Grid", _shop).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1560, 760));
            var trade = UIKit.Button("Trading post", _shop, "TRADING POST (DOUBLOONS)", new Color(0.25f, 0.45f, 0.45f), () => { _shopOpen = false; OpenOffers(); }, 30);
            ((RectTransform)trade.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-260, 30), new Vector2(480, 80));
            var restore = UIKit.Button("Restore", _shop, "RESTORE PURCHASES", new Color(0.3f, 0.3f, 0.32f), RestorePurchases, 30);
            ((RectTransform)restore.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(260, 30), new Vector2(420, 80));
            _shop.gameObject.SetActive(false);
        }

        public void OpenShop()
        {
            _shopOpen = true;
            _offersOpen = false;
            Sfx.Play("click");
            RefreshShop();
        }

        void RefreshShop()
        {
            _shop.gameObject.SetActive(_shopOpen);
            if (!_shopOpen) return;
            Clear(_shopGrid);
            var s = G.Session;
            var items = Catalog.Products;
            // Two rows: three cards a row, four when there are more than six products.
            int cols = items.Count > 6 ? 4 : 3;
            float step = 1560f / cols, cw = step - 20;
            for (int i = 0; i < items.Count; i++)
            {
                var p = items[i];
                int c = i % cols, r = i / cols;
                int inRow = Mathf.Min(cols, items.Count - r * cols);
                float x0 = (cols - inRow) * step / 2; // centre a short last row
                var card = UIKit.Img("Card", _shopGrid, UIKit.Round, new Color(0.22f, 0.26f, 0.25f, 1)).rectTransform;
                card.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(x0 + c * step, -r * 385), new Vector2(cw, 365));
                if (p.Best)
                {
                    var rib = UIKit.Img("Best", card, UIKit.Round, UIKit.Amber);
                    rib.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(170, 44));
                    var t = UIKit.Label("T", rib.transform, "BEST VALUE", 24, new Color(0.2f, 0.12f, 0.05f), TextAnchor.MiddleCenter); t.rectTransform.Fill();
                }
                var art = UIKit.Rect("Art", card).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(170, 150));
                var place = UIKit.Place(p.Art);
                if (place != null) { var im = UIKit.Img("Pic", art, place, Color.white); im.preserveAspect = true; im.rectTransform.Fill(); }
                else if (p.Art == "pack") { var im = UIKit.Img("Pic", art, UIKit.Symbol("pack"), Tropic.Pack); im.preserveAspect = true; im.rectTransform.Fill(10); }
                else
                {
                    // A heap of coins: more coins for a bigger pack.
                    int heap = p.Coins >= 1000 ? 5 : p.Coins >= 500 ? 3 : 1;
                    for (int k = 0; k < heap; k++)
                    {
                        var cn = UIKit.Rect("Coin", art).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((k - (heap - 1) / 2f) * 38, (k % 2) * 16 - 4), new Vector2(110, 110));
                        UIKit.ItemIcon(cn, "doubloon", 110);
                    }
                }
                var name = UIKit.Label("Name", card, p.Name, 36, Color.white, TextAnchor.UpperCenter);
                name.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -176), new Vector2(cw - 24, 46));
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 24; name.resizeTextMaxSize = 36;
                var blurb = UIKit.Label("Blurb", card, p.Blurb, 24, new Color(1, 1, 1, 0.75f), TextAnchor.UpperCenter, false);
                blurb.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -216), new Vector2(cw - 34, 52));
                blurb.resizeTextForBestFit = true; blurb.resizeTextMinSize = 14; blurb.resizeTextMaxSize = 24; blurb.verticalOverflow = VerticalWrapMode.Truncate;
                bool owned = !p.Consumable && s.Owns(p.Id);
                string id = p.Id;
                var buy = UIKit.Button("Buy", card, owned ? "OWNED" : Store.PriceOf(p.Id), owned ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.8f, 0.62f, 0.12f), () =>
                {
                    if (owned) return;
                    Store.Buy(id, (ok, msg) => { if (ok) Sfx.Play("buy"); else if (!string.IsNullOrEmpty(msg)) Toast(msg); _dirty = true; RefreshShop(); });
                }, 36);
                ((RectTransform)buy.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(Mathf.Min(300, cw - 60), 68));
            }
        }

        void RestorePurchases()
        {
            Store.Current.Restore(n =>
            {
                Toast(n > 0 ? "Restored " + n + " purchase" + (n == 1 ? "" : "s") : "Nothing to restore");
                _dirty = true;
                RefreshShop();
            });
        }

        // ------------------------------------------------------------------ confirm

        RectTransform _confirm;

        /// <summary>A small yes/no box ("Refill energy for 40 doubloons?").</summary>
        public void Confirm(string title, string body, string yes, System.Action onYes)
        {
            if (_confirm != null) Destroy(_confirm.gameObject);
            _confirm = UIKit.Img("Confirm", _root, null, new Color(0, 0, 0, 0.8f)).rectTransform;
            _confirm.Fill();
            _confirm.GetComponent<Image>().raycastTarget = true;
            var box = UIKit.Img("Box", _confirm, UIKit.Round, UIKit.Panel).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 420));
            var t = UIKit.Label("Title", box, title, 48, UIKit.Sand, TextAnchor.UpperCenter);
            t.rectTransform.Fill(); t.rectTransform.offsetMax = new Vector2(0, -30);
            var b = UIKit.Label("Body", box, body, 32, Color.white, TextAnchor.UpperCenter, false);
            b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(40, 0); b.rectTransform.offsetMax = new Vector2(-40, -110);
            var no = UIKit.Button("No", box, "NOT NOW", new Color(0.35f, 0.35f, 0.36f), () => Destroy(_confirm.gameObject), 32);
            ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-190, 34), new Vector2(330, 84));
            var ok = UIKit.Button("Yes", box, yes, new Color(0.8f, 0.62f, 0.12f), () => { Destroy(_confirm.gameObject); onYes(); }, 32);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(190, 34), new Vector2(330, 84));
        }
    }
}
