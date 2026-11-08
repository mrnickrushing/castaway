using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>The trading post: packs for doubloons, one card at a time with arrows between them.</summary>
    public sealed partial class Hud
    {
        RectTransform _offers, _offerCard, _offerInfo;
        bool _offersOpen;
        int _offerIndex;

        void BuildOffers()
        {
            _offers = UIKit.Img("Offers", _root, null, new Color(0.03f, 0.04f, 0.03f, 0.94f)).rectTransform;
            _offers.Fill();
            _offers.GetComponent<Image>().raycastTarget = true;
            var x = UIKit.Button("Close", _offers, "X", new Color(0, 0, 0, 0), () => { _offersOpen = false; _dirty = true; }, 80);
            ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -40), new Vector2(120, 120));
            foreach (int dir in new[] { -1, 1 })
            {
                var a = UIKit.Button("Arrow", _offers, dir < 0 ? "<" : ">", new Color(0, 0, 0, 0), () => { _offerIndex = (_offerIndex + dir + World.Offers.Count) % World.Offers.Count; RefreshOffers(); }, 120);
                ((RectTransform)a.transform).Place(new Vector2(dir < 0 ? 0 : 1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(dir * -150, 0), new Vector2(140, 200));
                a.GetComponentInChildren<Text>().color = new Color(0.95f, 0.75f, 0.2f);
            }
            _offerCard = UIKit.Rect("Card", _offers).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-480, 0), new Vector2(430, 590));
            _offerInfo = UIKit.Img("Info", _offers, null, new Color(0.3f, 0.33f, 0.3f, 0.97f)).rectTransform;
            _offerInfo.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-250, 0), new Vector2(800, 540));
            _offers.gameObject.SetActive(false);
        }

        public void OpenOffers()
        {
            _offersOpen = true;
            Sfx.Play("click");
            RefreshOffers();
        }

        void RefreshOffers()
        {
            _offers.gameObject.SetActive(_offersOpen);
            if (!_offersOpen) return;
            var s = G.Session;
            var o = World.Offers[_offerIndex];
            Clear(_offerCard); Clear(_offerInfo);
            // The card, tilted like a pack ticket.
            var card = UIKit.Img("Card", _offerCard, UIKit.Round, new Color(0.62f, 0.67f, 0.58f)).rectTransform;
            card.Fill();
            card.localRotation = Quaternion.Euler(0, 0, 4);
            var art = UIKit.Place(o.Art);
            if (art != null) { var im = UIKit.Img("Art", card, art, Color.white); im.preserveAspect = true; im.rectTransform.Fill(30); }
            else
            {
                var grid = UIKit.Rect("Items", card).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 300));
                for (int i = 0; i < o.Items.Length && i < 4; i++)
                {
                    var c = UIKit.Rect("Item", grid).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2((i % 2) * 150, -(i / 2) * 150), new Vector2(150, 150));
                    UIKit.ItemIcon(c, o.Items[i].id, 130);
                }
            }
            var timer = UIKit.Label("Timer", card, "◷ " + DayClock(s.OffersLeft), 36, new Color(0.15f, 0.15f, 0.15f), TextAnchor.UpperLeft);
            timer.rectTransform.Fill(); timer.rectTransform.offsetMin = new Vector2(26, 0); timer.rectTransform.offsetMax = new Vector2(0, -22);
            var limit = UIKit.Label("Limit", card, "Limit: " + s.Bought(o) + "/" + o.Limit, 34, new Color(0.15f, 0.15f, 0.15f), TextAnchor.LowerCenter, false);
            limit.rectTransform.Fill(); limit.rectTransform.offsetMin = new Vector2(0, 22);

            var name = UIKit.Label("Name", _offerInfo, o.Name.ToUpper(), 52, new Color(0.92f, 0.92f, 0.88f), TextAnchor.UpperCenter);
            name.rectTransform.Fill(); name.rectTransform.offsetMax = new Vector2(0, -40);
            var blurb = UIKit.Label("Blurb", _offerInfo, o.Blurb, 32, new Color(1, 1, 1, 0.85f), TextAnchor.UpperCenter, false);
            blurb.rectTransform.Fill(); blurb.rectTransform.offsetMax = new Vector2(-30, -112); blurb.rectTransform.offsetMin = new Vector2(30, 0);
            var rw = UIKit.Label("Rewards", _offerInfo, "Rewards:", 38, new Color(1, 1, 1, 0.85f), TextAnchor.UpperCenter, false);
            rw.rectTransform.Fill(); rw.rectTransform.offsetMax = new Vector2(0, -200);
            int n = o.Items.Length + (o.Energy > 0 ? 1 : 0);
            float w = n * 150 - 14;
            for (int i = 0; i < n; i++)
            {
                var tile = UIKit.Img("Tile", _offerInfo, null, new Color(0.22f, 0.24f, 0.22f)).rectTransform;
                tile.Place(new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(-w / 2 + i * 150, -260), new Vector2(136, 136));
                string id; int count;
                if (i < o.Items.Length) { id = o.Items[i].id; count = o.Items[i].n; UIKit.ItemIcon(tile, id, 110); }
                else { id = "energy"; count = o.Energy; var b = UIKit.Img("Bolt", tile, UIKit.Symbol("bolt"), UIKit.Amber); b.rectTransform.Fill(26); }
                var c = UIKit.Outlined(UIKit.Label("Count", tile, count.ToString(), 32, Color.white, TextAnchor.LowerRight), 1.5f);
                c.rectTransform.Fill(8);
            }
            string why = s.BuyBlocked(o);
            var buy = UIKit.Button("Buy", _offerInfo, why == "Sold out" ? "SOLD OUT" : o.Price + " DOUBLOONS", why == null ? new Color(0.8f, 0.62f, 0.12f) : new Color(0.4f, 0.38f, 0.3f), () =>
            {
                if (s.Buy(o)) { Sfx.Play("buy"); RefreshOffers(); }
                else Toast(s.BuyBlocked(o));
            }, 40);
            ((RectTransform)buy.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(420, 84));
            var have = UIKit.Label("Have", _offerInfo, "You have " + s.State.coins + " doubloons", 26, new Color(1, 1, 1, 0.6f), TextAnchor.LowerCenter, false);
            have.rectTransform.Fill(); have.rectTransform.offsetMin = new Vector2(0, 4);
        }

        static string DayClock(double seconds)
        {
            int s = Mathf.Max(0, (int)seconds);
            int d = s / 86400, h = s % 86400 / 3600, m = s % 3600 / 60;
            return d > 0 ? d + "d " + h + "h" : h > 0 ? h + "h " + m + "m" : m + "m";
        }
    }
}
