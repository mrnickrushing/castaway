using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>Settings > Name: a small dialog with a text field (the phone's keyboard opens on tap).</summary>
    public sealed partial class Hud
    {
        RectTransform _nameDlg;

        void AskName()
        {
            if (_nameDlg != null) Destroy(_nameDlg.gameObject);
            var s = G.Session;
            _nameDlg = UIKit.Img("Name", _root, null, new Color(0, 0, 0, 0.8f)).rectTransform;
            _nameDlg.Fill();
            _nameDlg.GetComponent<Image>().raycastTarget = true;
            var box = UIKit.Img("Box", _nameDlg, UIKit.Round, UIKit.Panel).rectTransform;
            // High on the screen: the keyboard comes up over the bottom half.
            box.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(860, 400));
            var t = UIKit.Label("Title", box, "YOUR NAME", 48, UIKit.Sand, TextAnchor.UpperCenter);
            t.rectTransform.Fill(); t.rectTransform.offsetMax = new Vector2(0, -30);

            var fieldBack = UIKit.Img("Field", box, UIKit.Round, new Color(1, 1, 1, 0.12f));
            fieldBack.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(700, 96));
            fieldBack.raycastTarget = true;
            var text = UIKit.Label("Text", fieldBack.transform, "", 44, Color.white, TextAnchor.MiddleCenter);
            text.rectTransform.Fill(12);
            text.supportRichText = false;
            text.resizeTextForBestFit = false;
            var hint = UIKit.Label("Hint", fieldBack.transform, "Castaway", 44, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleCenter);
            hint.rectTransform.Fill(12);
            hint.resizeTextForBestFit = false;
            var field = fieldBack.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = Session.NameMax;
            field.lineType = InputField.LineType.SingleLine;
            field.keyboardType = TouchScreenKeyboardType.Default;
            field.text = s.State.name ?? "";

            var why = UIKit.Label("Why", box, "", 28, UIKit.Red, TextAnchor.MiddleCenter, false);
            why.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -216), new Vector2(760, 40));

            System.Action save = () =>
            {
                var err = s.Rename(field.text);
                if (err != null) { why.text = err; Sfx.Play("error"); return; }
                Sfx.Play("click");
                Destroy(_nameDlg.gameObject);
                RefreshHelp();
            };
            var no = UIKit.Button("No", box, "CANCEL", new Color(0.35f, 0.35f, 0.36f), () => Destroy(_nameDlg.gameObject), 32);
            ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-190, 30), new Vector2(330, 84));
            var ok = UIKit.Button("Yes", box, "SAVE", new Color(0.8f, 0.62f, 0.12f), save, 32);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(190, 30), new Vector2(330, 84));
            field.ActivateInputField();
        }
    }
}
