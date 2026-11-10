using LastTide.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>Put on an item picture that isn't a button itself: tapping it shows what the item is.</summary>
    public sealed class ItemTipTarget : MonoBehaviour, IPointerClickHandler
    {
        public string Id;
        public void OnPointerClick(PointerEventData e) { if (Hud.Instance != null) Hud.Instance.ShowItemTip(Id, e.position); }
    }

    /// <summary>A small card with an item's name, description and where it comes from, shown for a few seconds above
    /// the picture that was tapped (recipe ingredients, rewards, blueprints).</summary>
    public sealed partial class Hud
    {
        RectTransform _tip;
        CanvasGroup _tipGroup;
        Text _tipName, _tipBody;
        float _tipT;

        public void ShowItemTip(string id, Vector2 screen)
        {
            var d = Content.Item(id);
            if (_tip == null)
            {
                _tip = UIKit.Img("ItemTip", _root, UIKit.Round, new Color(0.07f, 0.08f, 0.08f, 1f)).rectTransform;
                _tip.pivot = new Vector2(0.5f, 0);
                _tip.sizeDelta = new Vector2(620, 230);
                _tipGroup = _tip.gameObject.AddComponent<CanvasGroup>();
                _tipGroup.blocksRaycasts = false; // taps go through to whatever is under it
                var solid = UIKit.Img("Solid", _tip, null, new Color(0.07f, 0.08f, 0.08f, 1f)); // the rounded sprite's middle lets text through
                solid.rectTransform.Fill(10); solid.raycastTarget = false;
                var ring = _tip.gameObject.AddComponent<Outline>();
                ring.effectColor = new Color(0.85f, 0.65f, 0.25f, 0.9f); ring.effectDistance = new Vector2(3, -3);
                _tipName = UIKit.Label("Name", _tip, "", 34, UIKit.Amber, TextAnchor.UpperLeft);
                _tipName.rectTransform.Fill(); _tipName.rectTransform.offsetMin = new Vector2(24, 160); _tipName.rectTransform.offsetMax = new Vector2(-24, -16);
                _tipBody = UIKit.Label("Body", _tip, "", 26, new Color(1, 1, 1, 0.85f), TextAnchor.UpperLeft, false);
                _tipBody.rectTransform.Fill(); _tipBody.rectTransform.offsetMin = new Vector2(24, 14); _tipBody.rectTransform.offsetMax = new Vector2(-24, -66);
                _tipBody.supportRichText = true;
                _tipBody.resizeTextForBestFit = true; _tipBody.resizeTextMinSize = 20; // long blurbs shrink to stay inside the card
                _tipBody.resizeTextMaxSize = Mathf.Max(_tipBody.fontSize, 26);
            }
            _tipName.text = Content.NameOf(id);
            string origin = Content.Origin(id);
            string blurb = d != null && !string.IsNullOrEmpty(d.Blurb) ? d.Blurb : "";
            _tipBody.text = blurb + (origin != null ? (blurb.Length > 0 ? "\n" : "") + "<color=#8FE3A8>" + origin + "</color>" : "");

            // As tall as the text needs (the name takes the top 66), measured at the biggest size best fit may pick,
            // so long origins on a phone (where labels grow) never spill out of the card. Wider on phones.
            float cardW = UIKit.Phone ? 780 : 620;
            var gs = _tipBody.GetGenerationSettings(new Vector2(cardW - 48, 0));
            gs.resizeTextForBestFit = false;
            gs.fontSize = _tipBody.resizeTextMaxSize;
            float bodyH = _tipBody.cachedTextGeneratorForLayout.GetPreferredHeight(_tipBody.text, gs) / _tipBody.pixelsPerUnit;
            _tip.sizeDelta = new Vector2(cardW, Mathf.Clamp(66 + bodyH + 30, 130, 520));
            _tipName.rectTransform.offsetMin = new Vector2(24, 0);

            // Above the finger, kept on screen.
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out local);
            var r = _root.rect;
            float w = _tip.sizeDelta.x, h = _tip.sizeDelta.y;
            local.x = Mathf.Clamp(local.x, r.xMin + w / 2 + 16, r.xMax - w / 2 - 16);
            local.y = Mathf.Clamp(local.y + 60, r.yMin + 16, r.yMax - h - 16);
            _tip.anchorMin = _tip.anchorMax = new Vector2(0.5f, 0.5f);
            _tip.anchoredPosition = local;
            _tip.SetAsLastSibling();
            _tip.gameObject.SetActive(true);
            _tipT = 3f;
            Sfx.Play("click");
        }

        void UpdateItemTip()
        {
            if (_tip == null || !_tip.gameObject.activeSelf) return;
            _tipT -= Time.unscaledDeltaTime;
            _tipGroup.alpha = Mathf.Clamp01(_tipT * 3f);
            if (_tipT <= 0) _tip.gameObject.SetActive(false);
        }
    }
}
