using UnityEngine;

namespace LastTide.Game
{
    /// <summary>A campfire's flames: additive licks rising and shrinking, with a few embers.</summary>
    public static class Flames
    {
        static Material _mat;

        public static ParticleSystem Create(Transform parent, Vector3 localPos)
        {
            if (_mat == null) { _mat = new Material(Shader.Find("LastTide/Additive")) { mainTexture = Materials.SoftCircle }; }
            var go = new GameObject("Flames");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.rotation = Quaternion.Euler(-90, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.05f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 34;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0.15f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            // HDR colours (above 1) so the fire blooms and still reads as fire at night.
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.5f), new GradientColorKey(new Color(0.6f, 0.15f, 0.05f), 1) },
                new[] { new GradientAlphaKey(0.0f, 0), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0, 1) });
            main.startColor = new Color(2.2f, 1.9f, 1.6f, 1);
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = _mat;
            ps.Play();
            return ps;
        }
    }

    /// <summary>One-shot effects: a puff of dust and sparkles when something is built, hit sparks on animals.</summary>
    public static class Burst
    {
        static ParticleSystem _dust, _hit;

        public static void Play(Vector3 pos)
        {
            if (_dust == null) _dust = Fx.Quiet(FxKind.Dust);
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < 26; i++)
            {
                var d = Quaternion.Euler(0, i * 360f / 26, 0) * Vector3.forward;
                p.position = pos + d * 1.2f;
                p.velocity = d * Random.Range(2f, 4f) + Vector3.up * Random.Range(0.5f, 1.5f);
                p.startSize = Random.Range(1f, 1.8f);
                p.startLifetime = Random.Range(0.8f, 1.4f);
                _dust.Emit(p, 1);
            }
            Fx.Burst(pos + Vector3.up, 30);
        }

        public static void Hit(Vector3 pos, Color c)
        {
            if (_hit == null)
            {
                _hit = Fx.Quiet(FxKind.Dust);
                var main = _hit.main;
                main.gravityModifier = 1.2f;
            }
            var p = new ParticleSystem.EmitParams { startColor = c };
            for (int i = 0; i < 10; i++)
            {
                p.position = pos;
                p.velocity = Random.insideUnitSphere * 3f + Vector3.up * 2.5f;
                p.startSize = Random.Range(0.15f, 0.35f);
                p.startLifetime = Random.Range(0.4f, 0.7f);
                _hit.Emit(p, 1);
            }
        }
    }
}
