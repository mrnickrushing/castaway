using System.Collections.Generic;
using LastTide.Core;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// Anything that fights back: wanders its patch, charges anyone who comes close and fights to the death. Boars,
    /// giant crabs, vipers and Red Jack's raiders share the behaviour; they differ in their numbers and their bodies.
    /// </summary>
    public sealed class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();
        public string Kind { get { return _def.Id; } }
        EnemyDef _def;
        EnemyBody _body;
        Vector3 _home, _wander;
        float _health, _gait, _move, _attack = -1, _windup = -1, _cooldown, _think, _dead, _punch, _alertAt = -9;
        bool _hit, _angry, _reachAtWindup;
        Ring _ring;
        const float Reach = 2.1f;   // a strike lands if the castaway is still this close (along the ground)
        public bool Alive { get { return _health > 0; } }
        public EnemyDef Def { get { return _def; } }
        /// <summary>0..1 while it gathers itself for a strike (the HUD's red "!" and the ring), else -1.</summary>
        public float Windup01 { get { return _windup; } }
        /// <summary>Chasing the castaway (counts toward the HUD's DANGER).</summary>
        public bool Angry { get { return _angry && Alive; } }
        /// <summary>Seconds since it noticed the castaway (the yellow "!" pops for a moment).</summary>
        public float SinceAlert { get { return Time.time - _alertAt; } }
        /// <summary>Would have noticed the castaway but for sneaking (the HUD's grey "?": sneaking works).</summary>
        public bool Suspicious { get; private set; }
        public float Health01 { get { return _health / _def.Health; } }
        public bool Hurt { get { return _health < _def.Health; } }

        public static Enemy Spawn(Transform parent, string kind, Vector3 home)
        {
            var go = new GameObject(kind);
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<Enemy>();
            b._def = Enemies.All[kind];
            b._health = b._def.Health;
            b._home = home;
            b._body = EnemyBody.Create(kind, go.transform);
            go.transform.position = Island.Ground(home);
            go.transform.rotation = Quaternion.Euler(0, Random.value * 360, 0);
            b._wander = home;
            return b;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
        void OnDestroy() { if (_ring != null) Destroy(_ring.transform.parent.gameObject); }

        public static Enemy Nearest(Vector3 p, float range)
        {
            Enemy best = null;
            float bd = range;
            foreach (var b in All)
            {
                if (!b.Alive) continue;
                // Along the ground (with a sane height limit): on a slope one of them stands higher, and a 3D
                // distance left foes a step apart, neither able to strike.
                var off = b.transform.position - p;
                if (Mathf.Abs(off.y) > 2.5f) continue;
                off.y = 0;
                float d = off.magnitude;
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        /// <summary>A blow from the castaway (<paramref name="weapon"/>: the item held, null for fists) or a companion ("bite").</summary>
        public void Hit(int dmg, Vector3 from, string weapon = null)
        {
            if (!Alive) return;
            _health -= dmg;
            if (!_angry) _alertAt = Time.time;
            _angry = true;
            _punch = 1;
            var push = transform.position - from; push.y = 0;
            if (push.sqrMagnitude > 0.001f) transform.position = Step(transform.position, push.normalized * 0.45f);
            Burst.Hit(transform.position + Vector3.up * 0.7f, _def.Id == "skeleton" ? new Color(0.85f, 0.85f, 0.8f) : new Color(0.55f, 0.12f, 0.08f));
            if (_health > 0) _body.Flinch();
            CombatFx.Impact(weapon, transform.position);                                    // what it was hit with
            if (_def.Id == "boar") Sfx.Play("squeal", transform.position, 0.65f);        // and how it takes it
            Hud.Instance.Damage(transform.position + Vector3.up * 1.6f, dmg);
            if (weapon != "bite") CombatFx.HitStop(_health <= 0 ? 0.09f : 0.045f);
            if (_health <= 0)
            {
                _health = 0;
                _dead = 0.001f;
                _windup = _attack = -1;
                if (_ring != null) _ring.gameObject.SetActive(false);
                DeathCry(transform.position);
                G.Session.Killed(_def.Id);
            }
        }

        /// <summary>The sound it makes on noticing the castaway: boars grunt, crabs and vipers hiss, a raider draws steel.</summary>
        void AlarmCry(Vector3 pos)
        {
            switch (_def.Id)
            {
                case "boar": Sfx.Play("grunt", pos); break;
                case "crab": Sfx.Play("hiss", pos, 0.8f, 0.7f); break; // lower and shorter than the snake
                case "snake": Sfx.Play("hiss", pos); break;
                default: Sfx.Play("swing", pos, 0.7f); break;        // raiders, Red Jack, the drowned smugglers
            }
        }

        /// <summary>The sound it makes going down (a boar's squeal only for boars).</summary>
        void DeathCry(Vector3 pos)
        {
            switch (_def.Id)
            {
                case "boar": Sfx.Play("boardie", pos); break;
                case "crab": Sfx.Play("mine", pos, 0.8f, 1.35f); break; // the shell cracks
                case "snake": Sfx.Play("hiss", pos, 1f, 0.75f); break;
                default: Sfx.Play("pirate_die", pos); break;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_dead > 0)
            {
                _dead += dt;
                Pose(0, -1, Mathf.Clamp01(_dead / 0.4f));
                if (_dead > 4f) transform.position += Vector3.down * dt * 0.6f;
                if (_dead > 6f) Destroy(gameObject);
                return;
            }
            var player = Player.Instance;
            var pos = transform.position;
            Vector3 goal = _wander;
            float speed = 1.4f;
            float toPlayer = 999;
            if (player != null) { var off = player.transform.position - pos; toPlayer = Mathf.Abs(off.y) > 2.5f ? 999 : new Vector2(off.x, off.z).magnitude; }
            bool alive = G.Session.State.health > 0;
            float aggro = _def.Aggro * G.Session.AggroScale(player != null && player.Sneaking);
            bool chase = alive && (toPlayer < aggro || (_angry && toPlayer < _def.Aggro * 2.2f)) && Vector3.Distance(pos, _home) < 30f;
            // Sneaking is what kept it from noticing: say so (the HUD's grey "?").
            Suspicious = !chase && alive && player != null && player.Sneaking && toPlayer < _def.Aggro * G.Session.AggroScale(false);
            if (chase)
            {
                if (!_angry) { AlarmCry(pos); _angry = true; _alertAt = Time.time; }
                goal = player.transform.position;
                speed = _def.Speed;
            }
            else
            {
                _angry = false;
                _think -= dt;
                if (_think <= 0 || Vector3.Distance(pos, _wander) < 0.8f)
                {
                    _think = Random.Range(3f, 7f);
                    for (int i = 0; i < 8; i++)
                    {
                        var c = _home + new Vector3(Random.Range(-12f, 12f), 0, Random.Range(-12f, 12f));
                        if (Island.CanStep(c.x, c.z) && Island.Height(c.x, c.z) > 0.6f) { _wander = c; break; }
                    }
                }
                if (_def.Id == "boar" && Random.value < dt * 0.15f) Sfx.Play("grunt", pos, 0.4f);
            }

            // Attack when close: first stand and gather itself (the red ring fills, a red "!" over its head), then
            // strike. Stepping out of reach before the blow lands dodges it. The cooldown starts with the windup, so
            // the time between blows is what it always was.
            _cooldown -= dt;
            if (_windup >= 0)
            {
                _windup += dt / Mathf.Max(0.1f, _def.Windup);
                speed = 0;
                if (_windup >= 1) { _windup = -1; _attack = 0; _hit = false; }
            }
            else if (_attack >= 0)
            {
                _attack += dt / 0.55f;
                if (!_hit && _attack >= 0.55f)
                {
                    _hit = true;
                    if (player != null && toPlayer < Reach && alive) player.Hurt(_def.Damage, pos);
                    else if (player != null && alive && _reachAtWindup && toPlayer < Reach + 4f)
                    {
                        Sfx.Play("swing", pos, 0.8f, 0.85f);
                        Hud.Instance.Pop(player.transform.position + Vector3.up * 2f, "DODGED", new Color(0.75f, 0.95f, 1f));
                    }
                }
                if (_attack >= 1) _attack = -1;
                speed = 0;
            }
            else if (_angry && toPlayer < 1.6f && _cooldown <= 0)
            {
                _windup = 0; _cooldown = _def.AttackEvery; _reachAtWindup = toPlayer < Reach;
                _alertAt = -9; // the red "!" takes over from the yellow one
            }
            StrikeRing();

            var d = goal - pos; d.y = 0;
            float m = d.magnitude;
            float want = m > (_angry ? 1.3f : 0.3f) ? speed : 0;
            _move = Mathf.MoveTowards(_move, want, dt * 8);
            if (m > 0.05f)
            {
                var face = Quaternion.LookRotation(d / m);
                transform.rotation = Quaternion.Slerp(transform.rotation, face, dt * (_angry ? 8 : 3));
            }
            if (_move > 0.01f) pos = Step(pos, transform.forward * _move * dt);
            // Never stand inside the castaway.
            if (player != null)
            {
                var gap = pos - player.transform.position; gap.y = 0;
                if (gap.magnitude < 1.15f)
                {
                    var out_ = player.transform.position + (gap.sqrMagnitude > 1e-4f ? gap.normalized : -transform.forward) * 1.15f;
                    // Never shoved somewhere it couldn't walk to (a cliff edge it can't come back from).
                    if (Island.CanStep(out_.x, out_.z) && Island.Height(out_.x, out_.z) >= 0.4f) pos = out_;
                }
            }
            pos.y = Island.Ground(pos).y;
            transform.position = pos;
            _gait += dt * (2 + _move * 3.2f);
            Pose(_move, _attack, 0);
            // Gathering itself: crouches a little, as if to spring. Hit: a quick squash that springs back.
            float w = _windup >= 0 ? Mathf.SmoothStep(0, 1, Mathf.Min(1, _windup * 1.6f)) : 0;
            _punch = Mathf.MoveTowards(_punch, 0, dt * 7f);
            float p = Mathf.Sin(_punch * Mathf.PI) * 0.12f;
            transform.localScale = new Vector3(1 + 0.06f * w + p, 1 - 0.1f * w - p, 1 + 0.06f * w + p);
        }

        /// <summary>The red ring on the ground at strike reach, filling as it winds up.</summary>
        void StrikeRing()
        {
            bool on = _windup >= 0 || (_attack >= 0 && _attack < 0.55f);
            if (on && _ring == null)
            {
                // Beside the enemy, not under it: the ring's mesh is in world space and the body squashes.
                var holder = new GameObject(name + " strike");
                holder.transform.SetParent(transform.parent, false);
                _ring = Ring.Create(holder.transform, Reach);
            }
            if (_ring == null) return;
            _ring.transform.parent.position = transform.position;
            if (_ring.gameObject.activeSelf != on) _ring.gameObject.SetActive(on);
            if (on) _ring.Paint(_windup >= 0 ? _windup : 1, 1, new Color(0.75f, 0.06f, 0.04f, 0.8f), new Color(0.45f, 0.02f, 0f, 1));
        }

        void Pose(float speed, float attack, float dead) { _body.Pose(_gait, speed, attack, dead); }

        static Vector3 Step(Vector3 pos, Vector3 step)
        {
            var next = pos + step;
            if (!Island.CanStep(next.x, next.z) || Island.Height(next.x, next.z) < 0.4f) return pos;
            return Obstacles.Resolve(next, 0.6f);
        }
    }

    /// <summary>Keeps the island stocked: each den has one animal (or raider), and a new one turns up a while after it is killed.</summary>
    public sealed class Dens : MonoBehaviour
    {
        public const float Respawn = 75f;
        /// <summary>The drowned smugglers rise again sooner: their gunpowder drives a story step.</summary>
        static float RespawnOf(string kind) { return kind == "skeleton" ? 40f : Respawn; }

        public static Dens Instance { get; private set; }
        void Awake() { Instance = this; }

        /// <summary>Where the next <paramref name="kind"/> will turn up (a den whose occupant is down), or null.</summary>
        public Vector3? NextDen(string kind, Vector3 from)
        {
            Vector3? best = null; float bd = float.MaxValue;
            for (int i = 0; i < _dens.Count; i++)
            {
                if (_kinds[i] != kind) continue;
                Enemy b;
                if (_boars.TryGetValue(i, out b) && b != null && b.Alive) continue;
                float d = Vector3.Distance(_dens[i], from);
                if (d < bd) { bd = d; best = _dens[i]; }
            }
            return best;
        }
        readonly List<Vector3> _dens = new List<Vector3>();
        readonly List<string> _kinds = new List<string>();
        readonly Dictionary<int, Enemy> _boars = new Dictionary<int, Enemy>();
        readonly Dictionary<int, float> _gone = new Dictionary<int, float>();

        public void Add(string kind, Vector3 den) { _kinds.Add(kind); _dens.Add(den); }

        void Update()
        {
            var player = Player.Instance;
            for (int i = 0; i < _dens.Count; i++)
            {
                Enemy b;
                if (_boars.TryGetValue(i, out b) && b != null && b.Alive) continue;
                if (b != null && !b.Alive) { _boars.Remove(i); _gone[i] = Time.time; continue; }
                if (_kinds[i] == "redjack" && G.Session.Flag("redjack_dead")) continue; // the captain falls once
                float since;
                bool waited = !_gone.TryGetValue(i, out since) || Time.time - since > RespawnOf(_kinds[i]);
                // Don't pop one into view right next to the player.
                bool first = !_gone.ContainsKey(i);
                bool far = first || player == null || Vector3.Distance(player.transform.position, _dens[i]) > 26f;
                if (waited && far) _boars[i] = Enemy.Spawn(transform, _kinds[i], _dens[i]);
            }
        }
    }
}
