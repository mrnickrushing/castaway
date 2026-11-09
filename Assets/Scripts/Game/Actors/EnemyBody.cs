using UnityEngine;

namespace LastTide.Game
{
    /// <summary>The look and animation of each kind of enemy.</summary>
    public abstract class EnemyBody
    {
        public abstract void Pose(float gait, float speed, float attack, float dead);

        public static EnemyBody Create(string kind, Transform parent)
        {
            switch (kind)
            {
                case "pirate":
                case "skeleton":
                case "redjack":
                {
                    // Realistic cast members (Models/Cast), else the old Quaternius stand-ins.
                    string cast = kind == "skeleton" ? (Random.value < 0.5f ? "smuggler" : "smuggler2") : kind == "redjack" ? "captain" : "hunter";
                    var person = CharacterArt.TryCreate(cast, parent, kind == "redjack" ? 1.1f : 1f)
                                 ?? CharacterArt.TryCreate(kind == "skeleton" ? "skeleton" : "sharky", parent, kind == "redjack" ? 1.3f : 1f);
                    if (person != null) return new CharacterBody(person);
                    return new PirateBody(parent);
                }
                case "crab": return new CrabBody(parent);
                case "snake": return new SnakeBody(parent);
                default:
                    var boar = BoarArt.TryCreate(parent);
                    return boar != null ? (EnemyBody)new BoarArtBody(boar) : new BoarRigBody(BoarRig.Build(parent));
            }
        }

        /// <summary>An animated pirate or drowned smuggler: walks and runs, strikes with the cutlass, flinches, falls.</summary>
        sealed class CharacterBody : EnemyBody
        {
            readonly CharacterArt _a;
            float _lastAttack = -1;
            bool _dead;
            public CharacterBody(CharacterArt a) { _a = a; }

            public override void Pose(float gait, float speed, float attack, float dead)
            {
                if (dead > 0) { if (!_dead) { _dead = true; _a.Die(); } return; }
                if (attack >= 0 && (_lastAttack < 0 || attack < _lastAttack)) _a.Once("Sword", 1.3f);
                _lastAttack = attack;
                if (speed > 3f) _a.Loop("Run", Mathf.Clamp(speed / 4.5f, 0.7f, 1.4f));
                else if (speed > 0.2f) _a.Loop("Walk", Mathf.Clamp(speed / 1.6f, 0.6f, 1.4f));
                else _a.Loop("Idle");
            }

            public override void Flinch() { _a.Once("HitReact", 1.4f); }
        }

        /// <summary>A short flinch when hit (animated characters only).</summary>
        public virtual void Flinch() { }

        sealed class BoarArtBody : EnemyBody
        {
            readonly BoarArt _a;
            public BoarArtBody(BoarArt a) { _a = a; }
            public override void Pose(float gait, float speed, float attack, float dead) { _a.Pose(speed / 1.6f, attack, dead); }
        }

        sealed class BoarRigBody : EnemyBody
        {
            readonly BoarRig _r;
            public BoarRigBody(BoarRig r) { _r = r; }
            public override void Pose(float gait, float speed, float attack, float dead) { _r.Pose(gait, Mathf.Clamp01(speed / 2.5f), attack, dead); }
        }

        /// <summary>A giant coconut crab: wide shell, eight scuttling legs, two big claws that snap.</summary>
        sealed class CrabBody : EnemyBody
        {
            readonly Transform _body, _clawL, _clawR;
            readonly Transform[] _legs = new Transform[8];

            public CrabBody(Transform parent)
            {
                var shell = Palette.Hex(0xC4502E);
                _body = new GameObject("Crab").transform;
                _body.SetParent(parent, false);
                _body.localScale = Vector3.one * 1.3f;
                var mb = new MeshBuilder();
                mb.Sphere(new Vector3(0, 0.38f, 0), new Vector3(0.5f, 0.2f, 0.4f), 10, shell);
                mb.Sphere(new Vector3(0, 0.44f, 0.05f), new Vector3(0.36f, 0.12f, 0.28f), 8, MeshBuilder.Lighten(shell, 0.15f));
                foreach (float x in new[] { -0.1f, 0.1f })
                {
                    mb.Cylinder(new Vector3(x, 0.45f, 0.3f), new Vector3(x * 1.3f, 0.6f, 0.38f), 0.02f, 4, shell);
                    mb.Sphere(new Vector3(x * 1.3f, 0.62f, 0.38f), Vector3.one * 0.04f, 5, Palette.Hex(0x111111));
                }
                mb.ToObject("Shell", _body);
                for (int i = 0; i < 8; i++)
                {
                    int side = i < 4 ? -1 : 1, k = i % 4;
                    var t = new GameObject("Leg").transform;
                    t.SetParent(_body, false);
                    t.localPosition = new Vector3(side * 0.38f, 0.36f, 0.2f - k * 0.14f);
                    var leg = new MeshBuilder();
                    leg.Cylinder(Vector3.zero, new Vector3(side * 0.3f, 0.12f, 0), 0.035f, 0.03f, 4, shell);
                    leg.Cylinder(new Vector3(side * 0.3f, 0.12f, 0), new Vector3(side * 0.42f, -0.36f, 0), 0.03f, 0.015f, 4, MeshBuilder.Darken(shell, 0.85f));
                    leg.ToObject("Leg", t);
                    _legs[i] = t;
                }
                _clawL = Claw(-1, shell); _clawR = Claw(1, shell);
            }

            Transform Claw(int side, Color c)
            {
                var t = new GameObject("Claw").transform;
                t.SetParent(_body, false);
                t.localPosition = new Vector3(side * 0.32f, 0.4f, 0.35f);
                var mb = new MeshBuilder();
                mb.Cylinder(Vector3.zero, new Vector3(side * 0.12f, 0.05f, 0.25f), 0.06f, 0.05f, 5, c);
                mb.Sphere(new Vector3(side * 0.14f, 0.08f, 0.38f), new Vector3(0.11f, 0.08f, 0.16f), 7, MeshBuilder.Lighten(c, 0.1f));
                mb.ToObject("Claw", t);
                return t;
            }

            public override void Pose(float gait, float speed, float attack, float dead)
            {
                for (int i = 0; i < 8; i++)
                    _legs[i].localRotation = Quaternion.Euler(Mathf.Sin(gait * 2.5f + i * 1.7f) * 25 * Mathf.Clamp01(speed), 0, 0);
                float snap = attack >= 0 ? Mathf.Sin(attack * Mathf.PI) : 0.1f * Mathf.Sin(Time.time * 3);
                _clawL.localRotation = Quaternion.Euler(-snap * 50, 0, 0);
                _clawR.localRotation = Quaternion.Euler(-snap * 50, 0, 0);
                _body.localRotation = Quaternion.Euler(0, 0, dead * 180);
                _body.localPosition = Vector3.up * dead * 0.55f;
            }
        }

        /// <summary>A viper: a chain of segments slithering in an S, rearing up to strike.</summary>
        sealed class SnakeBody : EnemyBody
        {
            readonly Transform[] _seg = new Transform[10];
            readonly Transform _root;

            public SnakeBody(Transform parent)
            {
                _root = new GameObject("Snake").transform;
                _root.SetParent(parent, false);
                var green = Palette.Hex(0x5E7A2E);
                for (int i = 0; i < _seg.Length; i++)
                {
                    var t = new GameObject("Seg").transform;
                    t.SetParent(_root, false);
                    float r = i == 0 ? 0.13f : Mathf.Lerp(0.11f, 0.04f, i / (float)_seg.Length);
                    var mb = new MeshBuilder();
                    var c = i % 2 == 0 ? green : Palette.Hex(0xC8B04A);
                    mb.Sphere(Vector3.up * r, new Vector3(r * (i == 0 ? 1.2f : 1), r, i == 0 ? r * 1.6f : 0.17f), 7, c);
                    if (i == 0) foreach (float x in new[] { -0.06f, 0.06f }) mb.Sphere(new Vector3(x, r * 1.4f, 0.1f), Vector3.one * 0.025f, 4, Palette.Hex(0xE8C020));
                    mb.ToObject("Seg", t);
                    _seg[i] = t;
                }
            }

            public override void Pose(float gait, float speed, float attack, float dead)
            {
                float rear = attack >= 0 ? Mathf.Sin(attack * Mathf.PI) : 0.25f;
                for (int i = 0; i < _seg.Length; i++)
                {
                    float z = -i * 0.17f;
                    float x = Mathf.Sin(gait * 2.2f - i * 0.8f) * 0.18f * (i / (float)_seg.Length + 0.2f) * (1 - dead);
                    float y = i < 3 ? rear * (3 - i) * 0.18f : 0;
                    _seg[i].localPosition = new Vector3(x, y * (1 - dead), z + (i < 3 ? rear * 0.15f : 0));
                }
                _root.localRotation = Quaternion.Euler(0, 0, dead * 140);
            }
        }

        /// <summary>One of Red Jack's raiders: red coat, tricorn, cutlass.</summary>
        sealed class PirateBody : EnemyBody
        {
            readonly SurvivorRig _rig;
            readonly Transform _root;

            public PirateBody(Transform parent)
            {
                _rig = SurvivorRig.Build(parent, SurvivorRig.Raider);
                _rig.Hold("cutlass");
                _root = _rig.Root;
            }

            public override void Pose(float gait, float speed, float attack, float dead)
            {
                _rig.Pose(gait * 1.7f, Mathf.Clamp01(speed / 4.6f), Act.None, 0, attack);
                _root.localRotation = Quaternion.Euler(-dead * 85, 0, 0);
                _root.localPosition = Vector3.up * dead * 0.15f;
            }
        }
    }
}
