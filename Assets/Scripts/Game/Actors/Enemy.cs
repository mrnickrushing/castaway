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
        float _health, _gait, _move, _attack = -1, _cooldown, _think, _dead, _flash, _grunt;
        bool _hit, _angry;
        public bool Alive { get { return _health > 0; } }
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

        public void Hit(int dmg, Vector3 from)
        {
            if (!Alive) return;
            _health -= dmg;
            _angry = true;
            _flash = 0.15f;
            var push = transform.position - from; push.y = 0;
            if (push.sqrMagnitude > 0.001f) transform.position = Step(transform.position, push.normalized * 0.45f);
            Burst.Hit(transform.position + Vector3.up * 0.7f, _def.Id == "skeleton" ? new Color(0.85f, 0.85f, 0.8f) : new Color(0.55f, 0.12f, 0.08f));
            if (_health > 0) _body.Flinch();
            if (_def.Id == "boar") Sfx.Play("squeal", transform.position, 0.8f); // boars squeal, everything else thuds
            else Sfx.Play("hit", transform.position);
            Hud.Instance.Damage(transform.position + Vector3.up * 1.6f, dmg);
            if (_health <= 0)
            {
                _health = 0;
                _dead = 0.001f;
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
            if (alive && (toPlayer < aggro || (_angry && toPlayer < _def.Aggro * 2.2f)) && Vector3.Distance(pos, _home) < 30f)
            {
                if (!_angry) { AlarmCry(pos); _angry = true; }
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

            // Attack when close.
            _cooldown -= dt;
            if (_attack >= 0)
            {
                _attack += dt / 0.55f;
                if (!_hit && _attack >= 0.55f)
                {
                    _hit = true;
                    if (player != null && toPlayer < 2.1f && alive) player.Hurt(_def.Damage, pos);
                }
                if (_attack >= 1) _attack = -1;
                speed = 0;
            }
            else if (_angry && toPlayer < 1.6f && _cooldown <= 0)
            {
                _attack = 0; _hit = false; _cooldown = _def.AttackEvery;
            }

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
            _flash -= dt;
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
