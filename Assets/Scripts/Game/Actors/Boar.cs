using System.Collections.Generic;
using LastTide.Core;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>A wild boar: roots about its patch of jungle, charges anyone who comes close, and fights to the death.</summary>
    public sealed class Boar : MonoBehaviour
    {
        public static readonly List<Boar> All = new List<Boar>();
        EnemyDef _def;
        BoarRig _rig;
        Vector3 _home, _wander;
        float _health, _gait, _move, _attack = -1, _cooldown, _think, _dead, _flash, _grunt;
        bool _hit, _angry;
        public bool Alive { get { return _health > 0; } }
        public float Health01 { get { return _health / _def.Health; } }
        public bool Hurt { get { return _health < _def.Health; } }

        public static Boar Spawn(Transform parent, Vector3 home)
        {
            var go = new GameObject("Boar");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<Boar>();
            b._def = Enemies.All["boar"];
            b._health = b._def.Health;
            b._home = home;
            b._rig = BoarRig.Build(go.transform);
            go.transform.position = Island.Ground(home);
            go.transform.rotation = Quaternion.Euler(0, Random.value * 360, 0);
            b._wander = home;
            return b;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static Boar Nearest(Vector3 p, float range)
        {
            Boar best = null;
            float bd = range;
            foreach (var b in All)
            {
                if (!b.Alive) continue;
                float d = Vector3.Distance(b.transform.position, p);
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
            Burst.Hit(transform.position + Vector3.up * 0.7f, new Color(0.55f, 0.12f, 0.08f));
            Sfx.Play("hit", transform.position);
            Hud.Instance.Damage(transform.position + Vector3.up * 1.6f, dmg);
            if (_health <= 0)
            {
                _health = 0;
                _dead = 0.001f;
                Sfx.Play("boardie", transform.position);
                G.Session.Killed(_def.Id);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_dead > 0)
            {
                _dead += dt;
                _rig.Pose(_gait, 0, -1, Mathf.Clamp01(_dead / 0.4f));
                if (_dead > 4f) transform.position += Vector3.down * dt * 0.6f;
                if (_dead > 6f) Destroy(gameObject);
                return;
            }
            var player = Player.Instance;
            var pos = transform.position;
            Vector3 goal = _wander;
            float speed = 1.4f;
            float toPlayer = player != null ? Vector3.Distance(player.transform.position, pos) : 999;
            bool alive = G.Session.State.health > 0;
            if (alive && (toPlayer < _def.Aggro || (_angry && toPlayer < _def.Aggro * 2.2f)) && Vector3.Distance(pos, _home) < 30f)
            {
                if (!_angry) { Sfx.Play("grunt", pos); _angry = true; }
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
                        if (Island.Walkable(c.x, c.z) && Island.Height(c.x, c.z) > 0.6f) { _wander = c; break; }
                    }
                }
                if (Random.value < dt * 0.15f) Sfx.Play("grunt", pos, 0.4f);
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
                if (gap.magnitude < 1.15f) pos = player.transform.position + (gap.sqrMagnitude > 1e-4f ? gap.normalized : -transform.forward) * 1.15f;
            }
            pos.y = Island.Ground(pos).y;
            transform.position = pos;
            _gait += dt * (2 + _move * 3.2f);
            _rig.Pose(_gait, Mathf.Clamp01(_move / 2.5f), _attack, 0);
            _flash -= dt;
        }

        static Vector3 Step(Vector3 pos, Vector3 step)
        {
            var next = pos + step;
            if (!Island.Walkable(next.x, next.z) || Island.Height(next.x, next.z) < 0.4f) return pos;
            return Obstacles.Resolve(next, 0.6f);
        }
    }

    /// <summary>Keeps the jungle stocked with boars: each den has one, and a new one wanders in a while after it is killed.</summary>
    public sealed class BoarDens : MonoBehaviour
    {
        public const float Respawn = 75f;
        readonly List<Vector3> _dens = new List<Vector3>();
        readonly Dictionary<int, Boar> _boars = new Dictionary<int, Boar>();
        readonly Dictionary<int, float> _gone = new Dictionary<int, float>();

        public void Add(Vector3 den) { _dens.Add(den); }

        void Update()
        {
            var player = Player.Instance;
            for (int i = 0; i < _dens.Count; i++)
            {
                Boar b;
                if (_boars.TryGetValue(i, out b) && b != null && b.Alive) continue;
                if (b != null && !b.Alive) { _boars.Remove(i); _gone[i] = Time.time; continue; }
                float since;
                bool waited = !_gone.TryGetValue(i, out since) || Time.time - since > Respawn;
                // Don't pop one into view right next to the player.
                bool far = player == null || Vector3.Distance(player.transform.position, _dens[i]) > 26f;
                if (waited && far) _boars[i] = Boar.Spawn(transform, _dens[i]);
            }
        }
    }
}
