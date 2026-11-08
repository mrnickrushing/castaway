using UnityEngine;

namespace LastTide.Game
{
    /// <summary>What the player controller needs from a body: hold a tool, take a pose.</summary>
    public interface IRig
    {
        void Hold(string id);
        void Pose(float walk, float move, Act act, float actPhase, float swing);
    }

    /// <summary>
    /// The AI-made castaway (Resources/Models/People/survivor): Walk and Idle clips for moving about, and the work
    /// poses (chop, mine, gather, build, drink, rest, strike) laid over the clips by aiming the arm, spine and leg
    /// bones in LateUpdate. Aiming by direction works whatever axes the rig's bones use.
    /// </summary>
    public sealed class SurvivorArt : MonoBehaviour, IRig
    {
        Transform _model, _hips, _spine, _armL, _foreL, _handL, _armR, _foreR, _handR, _legL, _shinL, _footL, _legR, _shinR, _footR, _head;
        Animation _anim;
        GameObject _held;
        string _heldId;
        float _walk, _move, _phase, _swing = -1;
        Act _act;
        Vector3 _hipsRest;
        /// <summary>Crouched, sneaking.</summary>
        public bool Sneak;

        public static SurvivorArt TryCreate(Transform parent)
        {
            var go = ModelRegistry.Spawn(ModelRegistry.People, "survivor", parent);
            if (go == null) return null;
            var a = go.AddComponent<SurvivorArt>();
            a._model = go.transform;
            a._anim = go.GetComponent<Animation>();
            a._hips = Find(go, "Hips"); a._spine = Find(go, "Spine02"); a._head = Find(go, "Head");
            a._armL = Find(go, "LeftArm"); a._foreL = Find(go, "LeftForeArm"); a._handL = Find(go, "LeftHand");
            a._armR = Find(go, "RightArm"); a._foreR = Find(go, "RightForeArm"); a._handR = Find(go, "RightHand");
            a._legL = Find(go, "LeftUpLeg"); a._shinL = Find(go, "LeftLeg"); a._footL = Find(go, "LeftFoot");
            a._legR = Find(go, "RightUpLeg"); a._shinR = Find(go, "RightLeg"); a._footR = Find(go, "RightFoot");
            if (a._hips == null || a._armR == null || a._anim == null) { Destroy(go); return null; }
            a._hipsRest = a._hips.localPosition;
            foreach (AnimationState s in a._anim) s.wrapMode = WrapMode.Loop;
            a._anim.Play("Idle");
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()) { r.updateWhenOffscreen = true; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            return a;
        }

        // Legacy animation stops when the castaway is hidden behind the map; start it again on return.
        void OnEnable()
        {
            if (_anim == null) return;
            _anim.Stop();
            _anim.Play("Idle");
        }

        static Transform Find(GameObject go, string name)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>()) if (t.name == name) return t;
            return null;
        }

        public void Hold(string id)
        {
            if (id == _heldId) return;
            _heldId = id;
            if (_held != null) Destroy(_held);
            _held = null;
            if (string.IsNullOrEmpty(id)) return;
            string kit = id == "hatchet" ? "tool_axe" : id == "pickaxe" ? "tool_pickaxe" : id == "hammer" ? "tool_hammer" : null;
            if (kit != null && ModelRegistry.Has(ModelRegistry.Free, kit))
            {
                // Kit tools lie along their length; stand them on end so the handle runs up +Y from the grip.
                _held = new GameObject("Held " + id);
                _held.transform.SetParent(transform.parent, false);
                var t = ModelRegistry.Spawn(ModelRegistry.Free, kit, _held.transform);
                var b = ModelRegistry.RendererBounds(t);
                t.transform.localRotation = Quaternion.Euler(0, 0, 0) * t.transform.localRotation;
                return;
            }
            var mb = Models.Held(id);
            if (mb.VertexCount == 0) return;
            _held = mb.ToObject("Held " + id, transform.parent);
        }

        public void Pose(float walk, float move, Act act, float actPhase, float swing)
        {
            _walk = walk; _move = move; _act = act; _phase = actPhase; _swing = swing;
            // Clips: walk while moving (stride speed follows the stick), idle otherwise; work poses override on top.
            bool walking = move > 0.08f && act == Act.None;
            var clip = walking ? "Walk" : "Idle";
            if (!_anim.IsPlaying(clip)) _anim.CrossFade(clip, 0.18f);
            if (walking) _anim["Walk"].speed = Mathf.Lerp(0.6f, 1.35f, move);
        }

        void LateUpdate()
        {
            var root = transform.parent;
            _hips.localPosition = _hipsRest;
            if (Sneak)
            {
                _hips.position += Vector3.down * 0.28f;
                Twist(_spine, 0, 28);
                Aim(_armR, _foreR, new Vector3(0.25f, -0.6f, 0.7f));
                Aim(_armL, _foreL, new Vector3(-0.25f, -0.6f, 0.7f));
            }
            float p = _phase;
            switch (_act)
            {
                case Act.Chop:
                {
                    float t = Mathf.Repeat(p, 1f);
                    float up = t < 0.6f ? Mathf.SmoothStep(0, 1, t / 0.6f) : 1 - Mathf.SmoothStep(0, 1, (t - 0.6f) / 0.25f);
                    Twist(_spine, Mathf.Lerp(30, -25, up), 8);
                    Aim(_armR, _foreR, Vector3.Lerp(new Vector3(-0.2f, -0.3f, 1f), new Vector3(0.6f, 0.9f, -0.3f), up));
                    Aim(_foreR, _handR, Vector3.Lerp(new Vector3(-0.4f, -0.2f, 1f), new Vector3(0.2f, 1f, -0.5f), up));
                    Aim(_armL, _foreL, Vector3.Lerp(new Vector3(0.1f, -0.3f, 1f), new Vector3(0.4f, 0.6f, 0.2f), up));
                    Aim(_foreL, _handL, Vector3.Lerp(new Vector3(0.5f, -0.2f, 1f), new Vector3(0.6f, 0.8f, -0.2f), up));
                    break;
                }
                case Act.Mine:
                {
                    float t = Mathf.Repeat(p, 1f);
                    float up = t < 0.55f ? Mathf.SmoothStep(0, 1, t / 0.55f) : 1 - Mathf.SmoothStep(0, 1, (t - 0.55f) / 0.2f);
                    Twist(_spine, 0, Mathf.Lerp(28, -6, up));
                    var a = Vector3.Lerp(new Vector3(0, -0.5f, 1f), new Vector3(0, 1f, -0.25f), up);
                    Aim(_armR, _foreR, a + Vector3.right * 0.12f); Aim(_foreR, _handR, a);
                    Aim(_armL, _foreL, a - Vector3.right * 0.12f); Aim(_foreL, _handL, a + Vector3.right * 0.2f);
                    break;
                }
                case Act.Gather:
                case Act.Build:
                case Act.Drink:
                {
                    Crouch(0.42f);
                    float w = Mathf.Sin(p * Mathf.PI * 2 * (_act == Act.Build ? 2f : 1f));
                    Twist(_spine, 0, 35);
                    if (_act == Act.Drink)
                    {
                        float t = Mathf.PingPong(p * 2, 1);
                        var d = Vector3.Lerp(new Vector3(0, -0.6f, 1f), new Vector3(0, 0.3f, 1f), t);
                        Aim(_armR, _foreR, d + Vector3.right * 0.3f); Aim(_foreR, _handR, new Vector3(-0.4f, 1, 0.5f));
                        Aim(_armL, _foreL, d - Vector3.right * 0.3f); Aim(_foreL, _handL, new Vector3(0.4f, 1, 0.5f));
                    }
                    else
                    {
                        Aim(_armR, _foreR, new Vector3(0.25f, -0.8f + w * 0.3f, 0.8f)); Aim(_foreR, _handR, new Vector3(0, -0.9f + w * 0.4f, 0.6f));
                        Aim(_armL, _foreL, new Vector3(-0.25f, -0.8f - w * 0.25f, 0.8f)); Aim(_foreL, _handL, new Vector3(0, -0.9f - w * 0.3f, 0.6f));
                    }
                    break;
                }
                case Act.Rest:
                {
                    // Sitting with the knees up, arms resting on them.
                    _hips.position += Vector3.down * 0.55f;
                    Aim(_legL, _shinL, new Vector3(-0.15f, 0.25f, 1f)); Aim(_shinL, _footL, new Vector3(0, -1f, 0.15f));
                    Aim(_legR, _shinR, new Vector3(0.15f, 0.25f, 1f)); Aim(_shinR, _footR, new Vector3(0, -1f, 0.15f));
                    Twist(_spine, 0, -8 + Mathf.Sin(Time.time) * 2);
                    Aim(_armL, _foreL, new Vector3(-0.2f, -0.5f, 0.8f)); Aim(_armR, _foreR, new Vector3(0.2f, -0.5f, 0.8f));
                    break;
                }
            }
            if (_swing >= 0)
            {
                // Overhead strike with the right arm.
                float up = _swing < 0.4f ? _swing / 0.4f : _swing < 0.6f ? 1 - (_swing - 0.4f) / 0.2f : 0;
                float down = _swing >= 0.4f && _swing < 0.75f ? Mathf.Sin((_swing - 0.4f) / 0.35f * Mathf.PI) : 0;
                Twist(_spine, 20 * up - 15 * down, 18 * down);
                var a = Vector3.Lerp(new Vector3(0.1f, -0.2f, 1f), new Vector3(0.5f, 1f, -0.3f), up);
                Aim(_armR, _foreR, a); Aim(_foreR, _handR, a + Vector3.up * 0.3f * up);
            }
            PlaceHeld();
        }

        /// <summary>Lowers the hips and bends the legs into a squat.</summary>
        void Crouch(float drop)
        {
            _hips.position += Vector3.down * drop;
            Aim(_legL, _shinL, new Vector3(-0.2f, -0.35f, 1f)); Aim(_shinL, _footL, new Vector3(-0.05f, -1f, -0.35f));
            Aim(_legR, _shinR, new Vector3(0.25f, -0.9f, 0.45f)); Aim(_shinR, _footR, new Vector3(0.05f, -0.55f, -1f));
        }

        /// <summary>Turns a bone about the character's up axis (yaw) and right axis (lean forward).</summary>
        void Twist(Transform bone, float yaw, float lean)
        {
            if (bone == null) return;
            var root = transform.parent;
            bone.rotation = Quaternion.AngleAxis(yaw, root.up) * Quaternion.AngleAxis(lean, root.right) * bone.rotation;
        }

        /// <summary>Rotates <paramref name="bone"/> so it points from itself toward <paramref name="child"/> along a
        /// direction given in the character's space (+Z forward, +Y up, +X right).</summary>
        void Aim(Transform bone, Transform child, Vector3 dir)
        {
            if (bone == null || child == null) return;
            var cur = child.position - bone.position;
            var want = transform.parent.TransformDirection(dir.normalized);
            if (cur.sqrMagnitude < 1e-6f) return;
            bone.rotation = Quaternion.FromToRotation(cur, want) * bone.rotation;
        }

        /// <summary>The tool sits in the right fist, its handle across the forearm and its head forward.</summary>
        void PlaceHeld()
        {
            if (_held == null || _handR == null) return;
            var fore = (_handR.position - _foreR.position).normalized;
            var right = transform.parent.right;
            var shaft = Vector3.Cross(fore, right);
            if (shaft.sqrMagnitude < 1e-4f) shaft = transform.parent.forward;
            shaft.Normalize();
            // Tools are built with the handle along +Y from the grip and the head toward +Z.
            _held.transform.position = _handR.position + fore * 0.04f - shaft * 0.14f;
            _held.transform.rotation = Quaternion.LookRotation(fore, shaft);
        }
    }

    /// <summary>The AI-made boar (Resources/Models/Animals/boar) with its Walk clip; strikes and falls by tilting the model.</summary>
    public sealed class BoarArt
    {
        public Transform Model;
        Animation _anim;
        public const float Scale = 1.35f;

        public static BoarArt TryCreate(Transform parent)
        {
            var go = ModelRegistry.Spawn(ModelRegistry.Animals, "boar", parent);
            if (go == null) return null;
            var a = new BoarArt { Model = go.transform, _anim = go.GetComponent<Animation>() };
            go.transform.localScale *= Scale;
            if (a._anim != null) { foreach (AnimationState s in a._anim) s.wrapMode = WrapMode.Loop; a._anim.Play("Walk"); }
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
            return a;
        }

        public void Pose(float move, float attack, float dead)
        {
            if (_anim != null && _anim["Walk"] != null) _anim["Walk"].speed = dead > 0 ? 0 : Mathf.Clamp(move * 1.4f, 0.05f, 2.4f);
            // Head-down lunge and toss when it strikes; rolls onto its side when it dies.
            float lunge = attack >= 0 ? Mathf.Sin(attack * Mathf.PI) : 0;
            Model.localRotation = Quaternion.Euler(lunge * 14, 0, dead * 88);
            Model.localPosition = new Vector3(0, dead * 0.25f, lunge * 0.35f);
        }
    }
}
