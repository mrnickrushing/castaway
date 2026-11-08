using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// The castaway's body, built from simple parts on pivots and animated in code: walking, chopping, mining,
    /// gathering, hammering, drinking, resting and swinging a weapon.
    /// </summary>
    public sealed class SurvivorRig : IRig
    {
        public Transform Root, Hips, Torso, Head, ArmL, ArmR, LegL, LegR, Hand;
        GameObject _held;
        string _heldId;

        /// <summary>Clothes and colouring: the castaway, the survivors on the radio, Red Jack's raiders.</summary>
        public sealed class Look
        {
            public Color Shirt = Tropic.Shirt, Pants = Tropic.Pants, Hair = Tropic.Hair, Skin = Tropic.Skin, Band = Palette.Hex(0xA8322A), Boots = Palette.Hex(0x3A2A1C);
            public bool Pack = true, Hat, Coat, Beard = true;
        }

        public static readonly Look Raider = new Look { Shirt = Palette.Hex(0x8C1F1F), Pants = Palette.Hex(0x2A2A30), Band = Palette.Hex(0x1A1A1A), Pack = false, Hat = true, Coat = true, Skin = Palette.Hex(0xB07A55) };

        public static SurvivorRig Build(Transform parent, Look look = null)
        {
            look = look ?? new Look();
            var r = new SurvivorRig();
            r.Root = new GameObject("Survivor").transform;
            r.Root.SetParent(parent, false);
            r.Hips = Pivot("Hips", r.Root, new Vector3(0, 0.92f, 0));
            r.Torso = Pivot("Torso", r.Hips, new Vector3(0, 0.05f, 0));
            var torso = new MeshBuilder();
            // Shirt with the sleeves rolled up, belt, backpack with a bedroll.
            torso.Box(new Vector3(0, 0.33f, 0), new Vector3(0.5f, 0.6f, 0.28f), look.Shirt);
            torso.Box(new Vector3(0, 0.02f, 0), new Vector3(0.48f, 0.1f, 0.27f), Palette.Hex(0x4A3424));
            if (look.Coat)
            {
                // Long coat tails and a gold-buttoned front.
                torso.Box(new Vector3(0, -0.2f, -0.05f), new Vector3(0.52f, 0.4f, 0.22f), MeshBuilder.Darken(look.Shirt, 0.8f));
                for (int i = 0; i < 4; i++) torso.Box(new Vector3(0.07f, 0.12f + i * 0.13f, 0.145f), new Vector3(0.04f, 0.04f, 0.02f), Palette.Brass);
            }
            if (look.Pack)
            {
                torso.Box(new Vector3(0, 0.36f, -0.25f), new Vector3(0.42f, 0.5f, 0.22f), Tropic.Pack);
                torso.Box(new Vector3(0, 0.66f, -0.25f), new Vector3(0.46f, 0.12f, 0.2f), MeshBuilder.Darken(Tropic.Pack, 0.8f));
                torso.Cylinder(new Vector3(-0.26f, 0.12f, -0.27f), new Vector3(0.26f, 0.12f, -0.27f), 0.1f, 8, Palette.Hex(0x8C5A3A));
                foreach (float x in new[] { -0.16f, 0.16f })
                    torso.Box(new Vector3(x, 0.4f, 0.15f), new Vector3(0.05f, 0.5f, 0.02f), Palette.Hex(0x4A3A24));
            }
            torso.ToObject("Torso", r.Torso);
            r.Head = Pivot("Head", r.Torso, new Vector3(0, 0.66f, 0));
            var head = new MeshBuilder();
            head.Cylinder(new Vector3(0, 0, 0), new Vector3(0, 0.1f, 0), 0.08f, 6, look.Skin);
            head.Sphere(new Vector3(0, 0.22f, 0.01f), new Vector3(0.15f, 0.17f, 0.16f), 8, look.Skin);
            head.Sphere(new Vector3(0, 0.29f, -0.02f), new Vector3(0.16f, 0.12f, 0.16f), 8, look.Hair);
            if (look.Beard) head.Box(new Vector3(0, 0.12f, 0.08f), new Vector3(0.2f, 0.1f, 0.12f), look.Hair);
            if (look.Hat)
            {
                // A black tricorn.
                head.Cylinder(new Vector3(0, 0.33f, 0), new Vector3(0, 0.36f, 0), 0.32f, 3, look.Band);
                head.Cylinder(new Vector3(0, 0.34f, 0), new Vector3(0, 0.5f, 0), 0.17f, 0.14f, 8, look.Band);
            }
            else head.Box(new Vector3(0, 0.33f, 0.0f), new Vector3(0.34f, 0.05f, 0.34f), look.Band); // bandana band
            head.ToObject("Head", r.Head);
            r.ArmL = Limb("ArmL", r.Torso, new Vector3(-0.31f, 0.56f, 0), 0.62f, look.Shirt, look.Skin, 0.075f, out _);
            r.ArmR = Limb("ArmR", r.Torso, new Vector3(0.31f, 0.56f, 0), 0.62f, look.Shirt, look.Skin, 0.075f, out r.Hand);
            r.LegL = Limb("LegL", r.Hips, new Vector3(-0.13f, 0, 0), 0.9f, look.Pants, look.Boots, 0.1f, out _);
            r.LegR = Limb("LegR", r.Hips, new Vector3(0.13f, 0, 0), 0.9f, look.Pants, look.Boots, 0.1f, out _);
            return r;
        }

        static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        /// <summary>A limb hanging down from its pivot: upper part in cloth, lower part skin (or a boot).</summary>
        static Transform Limb(string name, Transform parent, Vector3 pos, float len, Color upper, Color lower, float r, out Transform end)
        {
            var t = Pivot(name, parent, pos);
            var mb = new MeshBuilder();
            mb.Cylinder(Vector3.zero, new Vector3(0, -len * 0.55f, 0), r, r * 0.9f, 7, upper);
            mb.Cylinder(new Vector3(0, -len * 0.55f, 0), new Vector3(0, -len, 0), r * 0.85f, r * 0.75f, 7, lower);
            if (name.StartsWith("Leg")) mb.Box(new Vector3(0, -len + 0.04f, 0.06f), new Vector3(r * 2f, 0.09f, 0.26f), lower);
            else mb.Sphere(new Vector3(0, -len - 0.03f, 0), Vector3.one * r * 0.95f, 6, lower);
            mb.ToObject("Mesh", t);
            end = Pivot("End", t, new Vector3(0, -len - 0.02f, 0));
            return t;
        }

        /// <summary>Puts the right tool in the hand (null = empty-handed).</summary>
        public void Hold(string id)
        {
            if (id == _heldId) return;
            _heldId = id;
            if (_held != null) Object.Destroy(_held);
            _held = null;
            if (string.IsNullOrEmpty(id)) return;
            var mb = Models.Held(id);
            if (mb.VertexCount == 0) return;
            _held = mb.ToObject("Held " + id, Hand);
            // Grip points forward out of the fist.
            _held.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }

        /// <summary>
        /// Poses the body. <paramref name="walk"/> is the stride phase (radians), <paramref name="move"/> 0..1 of full
        /// speed, <paramref name="act"/> what the hands do with its own phase, <paramref name="swing"/> 0..1 through an
        /// attack (or -1).
        /// </summary>
        public void Pose(float walk, float move, Act act, float actPhase, float swing)
        {
            float s = Mathf.Sin(walk), c = Mathf.Cos(walk);
            float stride = 38 * move;
            Hips.localPosition = new Vector3(0, 0.92f + Mathf.Abs(c) * 0.05f * move, 0);
            Hips.localRotation = Quaternion.Euler(0, s * 6 * move, 0);
            Torso.localRotation = Quaternion.Euler(6 * move, -s * 8 * move, 0);
            LegL.localRotation = Quaternion.Euler(s * stride, 0, 0);
            LegR.localRotation = Quaternion.Euler(-s * stride, 0, 0);
            ArmL.localRotation = Quaternion.Euler(-s * stride * 0.8f, 0, -6);
            ArmR.localRotation = Quaternion.Euler(s * stride * 0.8f, 0, 6);
            Head.localRotation = Quaternion.identity;

            if (move < 0.1f)
            {
                // Breathing.
                float b = Mathf.Sin(Time.time * 2.2f);
                Torso.localRotation = Quaternion.Euler(b * 1.2f, 0, 0);
                ArmL.localRotation = Quaternion.Euler(0, 0, -5 - b);
                ArmR.localRotation = Quaternion.Euler(0, 0, 5 + b);
            }

            float p = actPhase;
            switch (act)
            {
                case Act.Chop:
                {
                    // Wind up over the shoulder, then a hard swing across into the trunk.
                    float t = Mathf.Repeat(p, 1f);
                    float raise = t < 0.6f ? Mathf.SmoothStep(0, 1, t / 0.6f) : 1 - Mathf.SmoothStep(0, 1, (t - 0.6f) / 0.25f);
                    ArmR.localRotation = Quaternion.Euler(-40 - raise * 100, 0, 20 + raise * 30);
                    ArmL.localRotation = Quaternion.Euler(-50 - raise * 60, 0, -25);
                    Torso.localRotation = Quaternion.Euler(10 - raise * 8, -25 + raise * 45, 0);
                    break;
                }
                case Act.Mine:
                {
                    float t = Mathf.Repeat(p, 1f);
                    float raise = t < 0.55f ? Mathf.SmoothStep(0, 1, t / 0.55f) : 1 - Mathf.SmoothStep(0, 1, (t - 0.55f) / 0.2f);
                    ArmR.localRotation = Quaternion.Euler(-30 - raise * 150, 0, 8);
                    ArmL.localRotation = Quaternion.Euler(-30 - raise * 145, 0, -8);
                    Torso.localRotation = Quaternion.Euler(25 - raise * 25, 0, 0);
                    break;
                }
                case Act.Gather:
                case Act.Build:
                {
                    // Crouch and work with both hands; building hammers faster.
                    float speed = act == Act.Build ? 2f : 1f;
                    float w = Mathf.Sin(p * Mathf.PI * 2 * speed);
                    Hips.localPosition = new Vector3(0, 0.62f, 0);
                    LegL.localRotation = Quaternion.Euler(-70, 0, -4);
                    LegR.localRotation = Quaternion.Euler(-20, 0, 4);
                    Torso.localRotation = Quaternion.Euler(38, 0, 0);
                    ArmR.localRotation = Quaternion.Euler(-60 - w * 25, 0, 8);
                    ArmL.localRotation = Quaternion.Euler(-55 + w * 20, 0, -8);
                    Head.localRotation = Quaternion.Euler(-15, 0, 0);
                    break;
                }
                case Act.Drink:
                {
                    Hips.localPosition = new Vector3(0, 0.55f, 0);
                    LegL.localRotation = Quaternion.Euler(-80, 0, -6);
                    LegR.localRotation = Quaternion.Euler(-80, 0, 6);
                    float t = Mathf.PingPong(p * 2, 1);
                    Torso.localRotation = Quaternion.Euler(Mathf.Lerp(55, 25, t), 0, 0);
                    ArmR.localRotation = Quaternion.Euler(Mathf.Lerp(-40, -110, t), 0, 20);
                    ArmL.localRotation = Quaternion.Euler(Mathf.Lerp(-40, -110, t), 0, -20);
                    break;
                }
                case Act.Rest:
                {
                    Hips.localPosition = new Vector3(0, 0.3f, 0);
                    LegL.localRotation = Quaternion.Euler(-85, 0, -8);
                    LegR.localRotation = Quaternion.Euler(-85, 0, 8);
                    Torso.localRotation = Quaternion.Euler(-12 + Mathf.Sin(Time.time) * 1.5f, 0, 0);
                    ArmL.localRotation = Quaternion.Euler(-20, 0, -12);
                    ArmR.localRotation = Quaternion.Euler(-20, 0, 12);
                    break;
                }
            }

            if (swing >= 0)
            {
                // Overhead strike: up for the first 40%, down hard, then recover.
                float up = swing < 0.4f ? swing / 0.4f : swing < 0.6f ? 1 - (swing - 0.4f) / 0.2f : 0;
                float down = swing >= 0.4f && swing < 0.75f ? Mathf.Sin((swing - 0.4f) / 0.35f * Mathf.PI) : 0;
                ArmR.localRotation = Quaternion.Euler(-160 * up - 30 * down + (1 - up) * -20, 0, 15);
                Torso.localRotation = Quaternion.Euler(-8 * up + 18 * down, 15 * up - 10 * down, 0);
            }
        }
    }

    /// <summary>A wild boar: barrel body, big head with tusks, four legs, a bristly ridge.</summary>
    public sealed class BoarRig
    {
        public Transform Root, Body, Head, FL, FR, BL, BR;

        public static BoarRig Build(Transform parent)
        {
            var r = new BoarRig();
            r.Root = new GameObject("Boar").transform;
            r.Root.SetParent(parent, false);
            r.Body = new GameObject("Body").transform;
            r.Body.SetParent(r.Root, false);
            r.Body.localPosition = new Vector3(0, 0.62f, 0);
            var body = new MeshBuilder();
            body.Sphere(new Vector3(0, 0, -0.05f), new Vector3(0.42f, 0.4f, 0.75f), 10, Tropic.Boar);
            body.Sphere(new Vector3(0, 0.05f, 0.35f), new Vector3(0.44f, 0.44f, 0.4f), 10, Tropic.Boar); // shoulders
            for (int i = 0; i < 9; i++)
            {
                float z = 0.55f - i * 0.13f;
                body.Box(new Vector3(0, 0.38f + Mathf.Sin(i * 0.4f) * 0.05f - i * 0.012f, z), new Vector3(0.08f, 0.16f, 0.12f), Tropic.BoarDark);
            }
            body.Cylinder(new Vector3(0, 0.08f, -0.78f), new Vector3(0, -0.12f, -0.92f), 0.03f, 0.01f, 4, Tropic.BoarDark); // tail
            body.ToObject("Body", r.Body);
            r.Head = new GameObject("Head").transform;
            r.Head.SetParent(r.Body, false);
            r.Head.localPosition = new Vector3(0, 0.02f, 0.7f);
            var head = new MeshBuilder();
            head.Sphere(new Vector3(0, -0.02f, 0.12f), new Vector3(0.28f, 0.28f, 0.34f), 9, Tropic.Boar);
            head.Cylinder(new Vector3(0, -0.08f, 0.3f), new Vector3(0, -0.14f, 0.58f), 0.15f, 0.12f, 8, Tropic.Boar);
            head.Cylinder(new Vector3(0, -0.14f, 0.58f), new Vector3(0, -0.14f, 0.62f), 0.12f, 0.12f, 8, Palette.Hex(0x4A3028)); // snout
            foreach (float x in new[] { -0.09f, 0.09f })
            {
                head.Cylinder(new Vector3(x, -0.16f, 0.48f), new Vector3(x * 1.8f, 0.0f, 0.6f), 0.025f, 0.006f, 5, Tropic.Tusk);
                head.Box(new Vector3(x * 1.7f, 0.22f, 0.08f), new Vector3(0.08f, 0.14f, 0.04f), Tropic.BoarDark); // ears
                head.Sphere(new Vector3(x * 1.2f, 0.06f, 0.36f), Vector3.one * 0.03f, 5, Palette.Hex(0x111111));
            }
            head.ToObject("Head", r.Head);
            r.FL = Leg(r.Body, new Vector3(-0.2f, -0.12f, 0.42f));
            r.FR = Leg(r.Body, new Vector3(0.2f, -0.12f, 0.42f));
            r.BL = Leg(r.Body, new Vector3(-0.2f, -0.12f, -0.45f));
            r.BR = Leg(r.Body, new Vector3(0.2f, -0.12f, -0.45f));
            return r;
        }

        static Transform Leg(Transform body, Vector3 pos)
        {
            var t = new GameObject("Leg").transform;
            t.SetParent(body, false);
            t.localPosition = pos;
            var mb = new MeshBuilder();
            mb.Cylinder(Vector3.zero, new Vector3(0, -0.48f, 0), 0.085f, 0.05f, 6, Tropic.BoarDark);
            mb.Box(new Vector3(0, -0.5f, 0.02f), new Vector3(0.09f, 0.06f, 0.1f), Palette.Hex(0x222222));
            mb.ToObject("Leg", t);
            return t;
        }

        public void Pose(float gait, float move, float attack, float dead)
        {
            float s = Mathf.Sin(gait) * 35 * move;
            FL.localRotation = Quaternion.Euler(s, 0, 0);
            BR.localRotation = Quaternion.Euler(s, 0, 0);
            FR.localRotation = Quaternion.Euler(-s, 0, 0);
            BL.localRotation = Quaternion.Euler(-s, 0, 0);
            Body.localPosition = new Vector3(0, 0.62f + Mathf.Abs(Mathf.Cos(gait)) * 0.04f * move, 0);
            // Head down and a toss upward with the tusks when it strikes.
            float toss = attack >= 0 ? (attack < 0.5f ? -attack * 50 : (attack - 0.5f) * 2 * 40 - 25) : Mathf.Sin(Time.time * 1.3f) * 4;
            Head.localRotation = Quaternion.Euler(10 - toss, 0, 0);
            Body.localRotation = Quaternion.Euler(0, 0, dead * 90);
            if (dead > 0) Body.localPosition = new Vector3(0, Mathf.Lerp(0.62f, 0.38f, dead), 0);
        }
    }
}
