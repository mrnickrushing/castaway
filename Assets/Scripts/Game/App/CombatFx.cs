using System.Collections;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// How a fight feels: a split-second hit-stop when a blow lands (longer on a kill), the camera jolting when the
    /// castaway is struck, and an impact sound that matches the weapon (a cutlass slashes, a spear stabs, a club or a
    /// fist thuds, tools bite like they do on wood and rock).
    /// </summary>
    public sealed class CombatFx : MonoBehaviour
    {
        static CombatFx _instance;
        static float _stopUntil, _base = 1f;
        static bool _stopped;

        static CombatFx Instance
        {
            get
            {
                if (_instance == null) { var go = new GameObject("CombatFx"); _instance = go.AddComponent<CombatFx>(); }
                return _instance;
            }
        }

        /// <summary>Freezes the world for <paramref name="seconds"/> of real time. Keeps whatever speed the game ran
        /// at (the play bot runs it fast), and never stacks into a long freeze.</summary>
        public static void HitStop(float seconds)
        {
            if (!Application.isPlaying) return;
            if (!_stopped) { _base = Time.timeScale; if (_base <= 0.01f) return; }
            _stopUntil = Mathf.Max(_stopUntil, Time.unscaledTime + Mathf.Min(seconds, 0.12f));
            if (!_stopped) { _stopped = true; Time.timeScale = _base * 0.04f; Instance.StartCoroutine(Release()); }
        }

        static IEnumerator Release()
        {
            while (Time.unscaledTime < _stopUntil) yield return null;
            Time.timeScale = _base;
            _stopped = false;
        }

        /// <summary>The castaway's swing landing on <paramref name="at"/> with <paramref name="weapon"/> (null: fists).</summary>
        public static void Impact(string weapon, Vector3 at)
        {
            switch (weapon)
            {
                case "cutlass": Sfx.Play("slash", at, 0.9f); break;
                case "spear": case "harpoon": Sfx.Play("stab", at, 0.95f); break;
                case "hatchet": case "iron_hatchet": Sfx.Play("chop", at, 0.75f, 1.15f); Sfx.Play("hit", at, 0.5f); break;
                case "pickaxe": case "iron_pickaxe": Sfx.Play("mine", at, 0.65f, 1.1f); Sfx.Play("hit", at, 0.5f); break;
                case "bite": Sfx.Play("hit", at, 0.55f, 1.4f); break;                 // Biscuit and the other companions
                default: Sfx.Play("hit", at, weapon == null ? 0.7f : 1f, weapon == null ? 1.2f : 1f); break; // club, rod, fists
            }
        }
    }
}
