using LastTide.Core;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// Biscuit, the Gull's dog: trots at the castaway's heel, gallops to catch up, and goes for anything that attacks.
    /// Follows from island to island.
    /// </summary>
    public sealed class Pet : MonoBehaviour
    {
        public static Pet Instance { get; private set; }
        CharacterArt _art;
        Enemy _target;
        float _bite, _bark;

        public string Model { get; private set; }

        public static Pet Create(Transform parent, string model = "shiba")
        {
            var go = new GameObject("Pet " + model);
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<Pet>();
            p.Model = model;
            p._art = CharacterArt.TryCreate(model, go.transform);
            var def = Season.Pets.Find(d => d.Model == model);
            if (p._art != null) FitHeight(p._art.gameObject, def != null ? def.Height : 0.6f);
            Instance = p;
            return p;
        }

        /// <summary>The free animals come in at odd scales: size one by its skeleton in the idle pose.</summary>
        public static void FitHeight(GameObject art, float height)
        {
            var anim = art.GetComponent<Animation>();
            if (anim != null) anim.Sample();
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var t in art.GetComponentsInChildren<Transform>())
            {
                if (t.GetComponent<Renderer>() != null || t == art.transform) continue;
                lo = Mathf.Min(lo, t.position.y); hi = Mathf.Max(hi, t.position.y);
            }
            float h = (hi - lo) * 0.85f;
            if (h > 0.01f) art.transform.localScale *= height / h;
        }

        public void Place(Vector3 at) { transform.position = Island.Ground(at); }

        void Update()
        {
            var pl = Player.Instance;
            if (pl == null) return;
            float dt = Time.deltaTime;
            var me = transform.position;
            // A fight: anything hostile close to the castaway.
            if (_target == null || !_target.Alive || Vector3.Distance(_target.transform.position, pl.transform.position) > 9)
                _target = Enemy.Nearest(pl.transform.position, 7f);
            Vector3 goal; float speed;
            if (_target != null)
            {
                goal = _target.transform.position;
                speed = 6.5f;
                _bite -= dt;
                if (new Vector2(goal.x - me.x, goal.z - me.z).magnitude < 1.4f) // along the ground: works on slopes too
                {
                    speed = 0;
                    if (_bite <= 0)
                    {
                        _bite = 1.2f;
                        if (_art != null) _art.Once("Attack", 1.3f);
                        _target.Hit(Session.PetDamage, me);
                    }
                }
            }
            else
            {
                // At heel: a couple of metres behind and to the side.
                goal = pl.transform.position - pl.transform.forward * 1.8f + pl.transform.right * 1.1f;
                float d = Vector3.Distance(goal, me);
                speed = d > 6 ? Player.Speed * 1.25f : d > 1.2f ? Player.Speed * 0.85f : 0;
                if (d > 25) { Place(goal); me = transform.position; }
                _bark -= dt;
                if (_bark <= 0) { _bark = Random.Range(20f, 40f); if (Random.value < 0.5f) Sfx.Play("bark", me, 0.5f); }
            }
            var dir = goal - me; dir.y = 0;
            if (speed > 0 && dir.sqrMagnitude > 0.04f)
            {
                var step = dir.normalized * Mathf.Min(speed * dt, dir.magnitude);
                var next = me + step;
                if (Island.Walkable(next.x, next.z)) me = Obstacles.Resolve(next, 0.3f);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 10);
            }
            transform.position = Island.Ground(me);
            if (_art != null)
            {
                if (speed > Player.Speed) _art.Loop("Gallop", 1.2f);
                else if (speed > 0.1f) _art.Loop("Walk", 1.4f);
                else _art.Loop(_target != null ? "Idle_2" : "Idle");
            }
        }
    }
}
