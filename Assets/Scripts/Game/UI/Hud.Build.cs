using System.Linq;
using LastTide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastTide.Game
{
    /// <summary>
    /// Build mode at camp: a grid laid over the clearing, the pieces by group along the bottom (each with its cost),
    /// and a ghost of the chosen piece to drag into place, turn and confirm. Tap a built piece to take it down.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _buildBar, _buildTiles, _ghostButtons;
        bool _building;
        int _buildTab;
        string _ghostId;
        GameObject _ghost, _gridLines;
        Material _ghostMat;
        int _gi, _gj, _grot;
        Vector2 _buildPress;
        bool _buildDrag;

        public bool Building { get { return _building; } }

        void BuildBuildMode()
        {
            _buildBar = UIKit.Img("Build bar", _root, null, new Color(0.13f, 0.15f, 0.16f, 0.96f)).rectTransform;
            _buildBar.anchorMin = new Vector2(0, 0); _buildBar.anchorMax = new Vector2(1, 0); _buildBar.pivot = new Vector2(0.5f, 0);
            _buildBar.offsetMin = new Vector2(0, 0); _buildBar.offsetMax = new Vector2(0, 330);
            _buildBar.GetComponent<Image>().raycastTarget = true;
            _buildTiles = UIKit.Rect("Tiles", _buildBar).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 16), new Vector2(2200, 220));
            var close = UIKit.Button("Close", _buildBar, "CLOSE  X", new Color(0, 0, 0, 0), ExitBuild, 40);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -10), new Vector2(260, 80));
            _ghostButtons = UIKit.Rect("Ghost buttons", _root).Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, 120));
            GhostButton("x", new Color(0.6f, 0.15f, 0.12f), new Vector2(-130, 0), () => { DropGhost(); });
            GhostButton("rotate", new Color(0.12f, 0.42f, 0.42f), new Vector2(0, 40), () => { _grot = (_grot + 1) & 3; PlaceGhost(); Sfx.Play("click"); });
            GhostButton("check", new Color(0.18f, 0.55f, 0.25f), new Vector2(130, 0), ConfirmGhost);
            _ghostButtons.gameObject.SetActive(false);
            _buildBar.gameObject.SetActive(false);
        }

        void GhostButton(string sym, Color c, Vector2 at, System.Action act)
        {
            var b = UIKit.Button(sym, _ghostButtons, null, c, act);
            b.GetComponent<Image>().sprite = UIKit.Circle; b.GetComponent<Image>().type = Image.Type.Simple;
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, new Vector2(110, 110));
            var ic = UIKit.Img("I", b.transform, UIKit.Symbol(sym), Color.white);
            ic.rectTransform.Fill(26);
        }

        public void EnterBuild()
        {
            if (!G.Session.AtHome) { Toast("You can only build at your camp"); return; }
            CloseAll();
            _building = true;
            _buildBar.gameObject.SetActive(true);
            if (_gridLines == null) _gridLines = GridLines();
            _gridLines.SetActive(true);
            CameraRig.Instance.Distance = 26;
            Sfx.Play("click");
            RefreshBuild();
        }

        void ExitBuild()
        {
            _building = false;
            DropGhost();
            _buildBar.gameObject.SetActive(false);
            if (_gridLines != null) _gridLines.SetActive(false);
            CameraRig.Instance.Focus = null;
            CameraRig.Instance.Distance = 17;
        }

        void RefreshBuild()
        {
            if (!_building) return;
            var s = G.Session;
            for (int i = _buildBar.childCount - 1; i >= 0; i--) { var c = _buildBar.GetChild(i); if (c.name == "Tab") Destroy(c.gameObject); }
            Clear(_buildTiles);
            string[] tabIcons = { "wall", "small_box", "furnace", "bed" };
            for (int i = 0; i < Content.PieceGroups.Length; i++)
            {
                int k = i;
                var b = UIKit.Button("Tab", _buildBar, null, new Color(0, 0, 0, 0), () => { _buildTab = k; RefreshBuild(); });
                ((RectTransform)b.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(120 + i * 330, -6), new Vector2(320, 84));
                var icon = UIKit.Img("I", b.transform, ModelIcons.Piece(tabIcons[i]), Color.white);
                icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(72, 72));
                var t = UIKit.Label("T", b.transform, Content.PieceGroups[i].ToUpper(), 34, i == _buildTab ? Color.white : new Color(1, 1, 1, 0.5f), TextAnchor.MiddleLeft, false);
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(84, 0);
                if (i == _buildTab)
                {
                    var u = UIKit.Img("U", b.transform, null, new Color(0.25f, 0.7f, 0.7f));
                    u.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(300, 6));
                }
            }
            var group = Content.PieceGroups[_buildTab];
            int n = 0;
            foreach (var d in Content.Pieces.Values.Where(p => p.Group == group))
            {
                string id = d.Id;
                bool locked = s.State.level < d.Level;
                var tile = UIKit.Button("Tile", _buildTiles, null, id == _ghostId ? new Color(0.15f, 0.5f, 0.5f) : new Color(0.2f, 0.22f, 0.22f), () =>
                {
                    if (locked) { Toast("Needs level " + d.Level); return; }
                    PickPiece(id);
                });
                ((RectTransform)tile.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(n * 330, 0), new Vector2(316, 216));
                tile.GetComponent<Image>().sprite = null;
                var icon = UIKit.Img("Icon", tile.transform, ModelIcons.Piece(id), locked ? new Color(1, 1, 1, 0.35f) : Color.white);
                icon.preserveAspect = true;
                icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 14), new Vector2(170, 170));
                var nm = UIKit.Label("N", tile.transform, d.Name, 28, Color.white, TextAnchor.LowerLeft, false);
                nm.rectTransform.Fill(10);
                for (int c = 0; c < d.Cost.Length; c++)
                {
                    var cost = d.Cost[c];
                    int have = s.CountAll(cost.id);
                    var row = UIKit.Rect("Cost", tile.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -8 - c * 56), new Vector2(130, 52));
                    UIKit.ItemIcon(UIKit.Rect("I", row).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(50, 50)), cost.id, 50);
                    var h = UIKit.Label("H", row, have + "/" + cost.n, 24, have >= cost.n ? Color.white : new Color(1f, 0.5f, 0.45f), TextAnchor.MiddleRight, false);
                    h.rectTransform.Fill();
                }
                if (locked) { var lk = UIKit.Label("L", tile.transform, "LV " + d.Level, 26, UIKit.Red, TextAnchor.UpperLeft); lk.rectTransform.Fill(10); }
                n++;
            }
        }

        void PickPiece(string id)
        {
            DropGhost();
            _ghostId = id;
            _ghost = PieceModels.Create(id, null);
            if (_ghostMat == null) _ghostMat = new Material(Shader.Find("LastTide/Ghost"));
            foreach (var r in _ghost.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = _ghostMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            _ghostMat.SetFloat("_Fill", 0);
            var p = Player.Instance.transform.position + Player.Instance.transform.forward * 4;
            CampGrid.Snap(id, p, _grot, out _gi, out _gj, out _grot);
            PlaceGhost();
            RefreshBuild();
        }

        void DropGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _ghostId = null;
            _ghostButtons.gameObject.SetActive(false);
            if (_building) RefreshBuild();
        }

        void PlaceGhost()
        {
            if (_ghost == null) return;
            Vector3 pos; Quaternion rot;
            CampGrid.Pose(_ghostId, _gi, _gj, _grot, out pos, out rot);
            _ghost.transform.position = pos;
            _ghost.transform.rotation = rot;
            bool ok = G.Session.PlaceBlocked(_ghostId, _gi, _gj, _grot) == null;
            _ghostMat.SetColor("_Color", ok ? new Color(0.4f, 1f, 0.35f, 0.4f) : new Color(1f, 0.3f, 0.25f, 0.4f));
            _ghostMat.SetFloat("_MinY", pos.y);
            _ghostMat.SetFloat("_Height", 3);
            CameraRig.Instance.Focus = pos;
        }

        void ConfirmGhost()
        {
            var s = G.Session;
            var why = s.PlaceBlocked(_ghostId, _gi, _gj, _grot);
            if (why != null) { Toast(why); Sfx.Play("error"); return; }
            if (s.Place(_ghostId, _gi, _gj, _grot) != null)
            {
                Sfx.Play("built");
                Vector3 pos; Quaternion rot;
                CampGrid.Pose(_ghostId, _gi, _gj, _grot, out pos, out rot);
                Burst.Play(pos + Vector3.up * 0.5f);
                PlaceGhost();
                RefreshBuild();
            }
        }

        /// <summary>Drag to move the ghost; a tap on a built piece offers to take it down.</summary>
        void UpdateBuild()
        {
            if (!_building) return;
            if (!G.Session.AtHome || MapOpen) { ExitBuild(); return; }
            var cam = CameraRig.Instance.Cam;
            bool down = Input.touchCount > 0 || Input.GetMouseButton(0);
            Vector2 pos = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            bool began = (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) || Input.GetMouseButtonDown(0);
            bool ended = (Input.touchCount > 0 && (Input.GetTouch(0).phase == TouchPhase.Ended)) || Input.GetMouseButtonUp(0);
            bool overUi = OverUI(Input.touchCount > 0 ? Input.GetTouch(0).fingerId : -1);
            if (began && !overUi) { _buildPress = pos; _buildDrag = true; }
            if (_buildDrag && down && _ghost != null && Vector2.Distance(pos, _buildPress) > 12)
            {
                var ray = cam.ScreenPointToRay(pos);
                var plane = new Plane(Vector3.up, new Vector3(0, Island.Camp.y + Island.Height(Island.Camp.x, Island.Camp.z), 0));
                float t;
                if (plane.Raycast(ray, out t))
                {
                    int i, j, r;
                    CampGrid.Snap(_ghostId, ray.GetPoint(t), _grot, out i, out j, out r);
                    if (i != _gi || j != _gj) { _gi = i; _gj = j; _grot = r; PlaceGhost(); }
                }
            }
            if (_buildDrag && ended)
            {
                _buildDrag = false;
                if (_ghost == null && Vector2.Distance(pos, _buildPress) < 12)
                {
                    var ray = cam.ScreenPointToRay(pos);
                    var plane = new Plane(Vector3.up, new Vector3(0, Island.Height(Island.Camp.x, Island.Camp.z), 0));
                    float t;
                    if (plane.Raycast(ray, out t) && PieceViews.Instance != null)
                    {
                        var p = PieceViews.Instance.At(ray.GetPoint(t), 2f);
                        if (p != null)
                            Confirm("Take down the " + Content.Pieces[p.id].Name.ToLower() + "?", "You get half its materials back, and anything stored in it.", "TAKE DOWN", () => { if (G.Session.Demolish(p)) Sfx.Play("deliver"); });
                    }
                }
            }
            // The ghost's buttons ride beside it on screen.
            _ghostButtons.gameObject.SetActive(_ghost != null);
            if (_ghost != null)
            {
                var c = ToCanvas(_ghost.transform.position + Vector3.up * 3.2f);
                var off = ((RectTransform)_worldLayer.parent).offsetMin;
                _ghostButtons.anchoredPosition = (Vector2)c + off;
            }
        }

        /// <summary>Faint lines over the camp clearing marking the 3 m cells.</summary>
        static GameObject GridLines()
        {
            var mb = new MeshBuilder();
            float s = Content.CellSize, h = Content.GridHalf + 0.5f;
            var c = Island.Camp;
            for (int k = -Content.GridHalf; k <= Content.GridHalf + 1; k++)
            {
                float o = (k - 0.5f) * s;
                for (float u = -h * s; u < h * s; u += 1.5f)
                {
                    Line(mb, new Vector3(c.x + o, 0, c.z + u), new Vector3(c.x + o, 0, c.z + u + 1.5f));
                    Line(mb, new Vector3(c.x + u, 0, c.z + o), new Vector3(c.x + u + 1.5f, 0, c.z + o));
                }
            }
            var go = mb.ToObject("Build grid", null, false);
            var mat = new Material(Shader.Find("LastTide/Ring"));
            mat.SetColor("_Color", new Color(1, 1, 1, 0.55f));
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var uv = new Vector2[mesh.vertexCount];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(0, 0.6f);
            mesh.uv = uv;
            return go;
        }

        static void Line(MeshBuilder mb, Vector3 a, Vector3 b)
        {
            var dir = (b - a).normalized;
            var side = Vector3.Cross(Vector3.up, dir) * 0.05f;
            a = Island.Ground(a) + Vector3.up * 0.08f; b = Island.Ground(b) + Vector3.up * 0.08f;
            mb.Quad(a - side, b - side, b + side, a + side, Color.white);
        }
    }
}
