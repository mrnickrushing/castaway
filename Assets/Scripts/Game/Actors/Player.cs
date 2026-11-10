using LastTide.Core;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// The castaway: walks with the stick, works whatever ring they stand in once the stick is let go, and fights
    /// anything that comes close.
    /// </summary>
    public sealed class Player : MonoBehaviour
    {
        public static Player Instance { get; private set; }
        public const float Speed = 5.4f, BodyRadius = 0.4f;
        const float AttackRange = 2.3f, AttackEvery = 0.8f;

        public Spot Current { get; private set; }
        /// <summary>Crouched and quiet: slower, and animals notice you much later.</summary>
        public bool Sneaking;
        /// <summary>Swing at anything that comes close by yourself (off: only when the weapon button is pressed).</summary>
        public bool AutoAttack = true;
#if UNITY_EDITOR
        /// <summary>Stick input from the editor play bot (null: the real stick).</summary>
        public static Vector2? BotStick;
#endif
        bool _attackPressed;
        Spot _walkTo;
        Enemy _locked;
        public float Cooldown01 { get { return Mathf.Clamp01(_cooldown / AttackEvery); } }
        public Enemy Locked { get { return _locked != null && _locked.Alive ? _locked : null; } }

        /// <summary>The weapon button: swing now (at the locked or nearest enemy).</summary>
        public void Attack() { _attackPressed = true; }

        /// <summary>The hand button: walk to the nearest thing to work on and get to it.</summary>
        public void Interact()
        {
            Spot best = null; float bd = 12f;
            foreach (var sp in Spot.All)
            {
                if (!sp.Active || sp.Blocked) continue;
                float d = Vector3.Distance(sp.Pos, transform.position);
                if (d < bd) { bd = d; best = sp; }
            }
            _walkTo = best;
            if (best == null && Hud.Instance != null) Hud.Instance.Toast("Nothing to do nearby");
        }

        /// <summary>The target button: lock on to the next enemy in reach, nearest first.</summary>
        public void CycleTarget()
        {
            var list = new System.Collections.Generic.List<Enemy>();
            foreach (var e in Enemy.All) if (e.Alive && Vector3.Distance(e.transform.position, transform.position) < 14) list.Add(e);
            list.Sort((a, b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
            if (list.Count == 0) { _locked = null; return; }
            int i = _locked != null ? list.IndexOf(_locked) : -1;
            _locked = list[(i + 1) % list.Count];
        }
        public Vector3 Velocity { get; private set; }
        public bool Working { get; private set; }

        IRig _rig;
        float _walk, _move, _actPhase, _swing = -1, _cooldown, _hurt;
        bool _struck;
        Act _act;
        Enemy _target;

        public static Player Create(Transform parent)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<Player>();
            p._rig = (IRig)SurvivorArt.TryCreate(go.transform) ?? SurvivorRig.Build(go.transform);
            return p;
        }

        void Awake() { Instance = this; }

        public void Place(Vector3 p, float facing)
        {
            transform.position = Island.Ground(p);
            transform.rotation = Quaternion.Euler(0, facing, 0);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var s = G.Session;
            var input = Hud.Instance != null ? Hud.Instance.Stick : Vector2.zero;
#if UNITY_EDITOR || UNITY_STANDALONE
            var kb = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            if (kb.sqrMagnitude > 0) input = kb.normalized;
#endif
#if UNITY_EDITOR
            if (BotStick.HasValue) input = BotStick.Value; // PlayBot (editor playtests)
#endif
            if (Hud.Instance != null && Hud.Instance.Blocking) input = Vector2.zero;

            // The hand button walks to its target until the stick takes over.
            if (input.sqrMagnitude > 0.01f) _walkTo = null;
            if (_walkTo != null)
            {
                var to = _walkTo.Pos - transform.position; to.y = 0;
                if (!_walkTo.Active || to.magnitude < _walkTo.Radius * 0.6f) _walkTo = null;
                else { var n = to.normalized; input = new Vector2(n.x, n.z); }
            }
            // Move: camera-relative (the camera looks north).
            var want = new Vector3(input.x, 0, input.y);
            float mag = Mathf.Clamp01(want.magnitude);
            var pos = transform.position;
            if (mag > 0.05f)
            {
                var step = want.normalized * Speed * (Sneaking ? 0.55f : 1f) * mag * dt;
                var next = Move(pos, step);
                Velocity = (next - pos) / Mathf.Max(dt, 1e-4f);
                pos = next;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(want), dt * 14);
            }
            else Velocity = Vector3.zero;
            pos.y = Island.Ground(pos).y;
            transform.position = pos;
            s.State.px = pos.x; s.State.pz = pos.z; s.State.facing = transform.eulerAngles.y;
            // Animate by how fast the castaway really moves: pushing into a wall or a cliff shouldn't run on the spot.
            float actual = Mathf.Clamp01(new Vector2(Velocity.x, Velocity.z).magnitude / Speed);
            _move = Mathf.MoveTowards(_move, Mathf.Min(mag, actual * 1.15f), dt * 6);
            _walk += dt * (2.5f + 8.5f * _move) * (_move > 0.05f ? 1 : 0);

            // Fight first: anything hostile in reach gets hit.
            _cooldown -= dt;
            _target = Locked != null && Flat(Locked.transform.position - pos) < AttackRange ? Locked : Enemy.Nearest(pos, AttackRange);
            bool swingNow = _attackPressed || AutoAttack;
            _attackPressed = false;
            if (_swing >= 0)
            {
                _swing += dt / 0.45f;
                if (!_struck && _swing >= 0.5f)
                {
                    _struck = true;
                    if (_target != null && Flat(_target.transform.position - pos) < AttackRange + 0.4f)
                    {
                        _target.Hit(s.AttackDamage(), pos);
                        s.Swung();
                    }
                }
                if (_swing >= 1) _swing = -1;
            }
            else if (_target != null && _cooldown <= 0 && mag < 0.95f && swingNow)
            {
                _swing = 0; _struck = false; _cooldown = AttackEvery;
                Sfx.Play("swing", pos, 0.6f);
            }
            if (_target != null && mag < 0.05f) Face(_target.transform.position, dt);

            // Work the ring we're standing in once the stick is let go.
            var spot = FindSpot(pos);
            if (spot != Current)
            {
                if (Current != null) Current.Leave();
                Current = spot;
                _actPhase = 0;
            }
            _act = Act.None;
            Working = false;
            if (Current != null && mag < 0.08f && _target == null)
            {
                _act = Current.Work(this, dt);
                Working = _act != Act.None;
                if (Working && _act != Act.Rest) Face(Current.Pos, dt);
            }
            float rate = _act == Act.Chop ? 1.25f : _act == Act.Mine ? 1.05f : _act == Act.Build ? 1.6f : 0.9f;
            _actPhase += dt * rate;

            // The tool in hand: what we're using, else the weapon.
            string held = _act == Act.Chop ? s.ToolFor(ToolUse.Chop) >= 0 ? s.State.backpack[s.ToolFor(ToolUse.Chop)].id : null
                : _act == Act.Mine ? s.ToolFor(ToolUse.Mine) >= 0 ? s.State.backpack[s.ToolFor(ToolUse.Mine)].id : null
                : _act == Act.Build ? "hammer"
                : _act == Act.Gather || _act == Act.Drink || _act == Act.Rest ? null
                : s.WeaponId;
            _rig.Hold(held);
            if (_rig is SurvivorArt art) art.Sneak = Sneaking && _act == Act.None;
            _rig.Pose(_walk, _move, _act, _actPhase, _swing);

            if (_hurt > 0) _hurt -= dt;
            Sfx.Ambience(pos);
        }

        void Face(Vector3 target, float dt)
        {
            var d = target - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), dt * 10);
        }

        /// <summary>Steps along the ground, sliding along water's edge, cliffs and obstacles.</summary>
        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        static Vector3 Move(Vector3 pos, Vector3 step)
        {
            var next = pos + step;
            if (!Island.CanStep(next.x, next.z))
            {
                var nx = pos + new Vector3(step.x, 0, 0);
                var nz = pos + new Vector3(0, 0, step.z);
                if (Island.CanStep(nx.x, nx.z)) next = nx;
                else if (Island.CanStep(nz.x, nz.z)) next = nz;
                else next = pos;
            }
            next = Obstacles.Resolve(next, BodyRadius);
            if (!Island.CanStep(next.x, next.z)) next = pos;
            return next;
        }

        Spot FindSpot(Vector3 pos)
        {
            Spot best = null;
            float bestD = float.MaxValue;
            foreach (var sp in Spot.All)
            {
                if (!sp.Active) continue;
                var d = sp.Pos - pos; d.y = 0;
                float m = d.magnitude;
                float reach = sp.Radius + (sp == Current ? 0.35f : 0);
                if (m < reach && m < bestD) { best = sp; bestD = m; }
            }
            return best;
        }

        public void Hurt(float dmg, Vector3 from)
        {
            G.Session.Damage(dmg, "boar");
            _hurt = 0.3f;
            var push = transform.position - from; push.y = 0;
            if (push.sqrMagnitude > 0.001f) transform.position = Island.Ground(Move(transform.position, push.normalized * 0.5f));
            Burst.Hit(transform.position + Vector3.up * 1.2f, new Color(0.7f, 0.1f, 0.1f));
            Sfx.Play("hurt", transform.position);
            if (Hud.Instance != null) Hud.Instance.Flash();
        }

        public bool Hurting { get { return _hurt > 0; } }
    }
}
