#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using LastTide.Core;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// Editor-only playtest bot: plays the survival guide from the first step to the end the way a player would. It
    /// walks with the stick to the guide arrow's goal, works the rings, fights with auto-attack, sails between islands,
    /// and taps through movies and story cards. Where a step needs a menu (crafting, cooking, the radio) it calls the
    /// same Session method the button calls. Shortcuts (materials it hasn't gathered for a build, levels it would have
    /// to grind, healing) are logged with "GRANT" so the run says exactly what was tested for real.
    /// Drive it from the Unity CLI: <c>LastTide.Game.PlayBot.Begin(3)</c>, read <c>PlayBot.Report(30)</c>.
    /// </summary>
    public sealed class PlayBot : MonoBehaviour
    {
        public static PlayBot Instance { get; private set; }
        static readonly List<string> _log = new List<string>();

        string _quest;
        float _questT, _stuckT, _tapT, _bestDist = float.MaxValue;
        int _lastQuest = -1;
        public bool Paused;
        /// <summary>Off for balance tests: no free healing, the castaway has to win (or fall) on their own.</summary>
        public static bool Heal = true;

        public static void Begin(float speed = 3f)
        {
            if (Instance == null) Instance = new GameObject("PlayBot").AddComponent<PlayBot>();
            Time.timeScale = speed;
            Log("bot started at step " + G.Session.CurrentQuest.Id + ", level " + G.Session.State.level);
        }

        public static void Stop()
        {
            Player.BotStick = null;
            Time.timeScale = 1;
            if (Instance != null) Destroy(Instance.gameObject);
        }

        /// <summary>Visual tour: sets the time of day (0 dawn .. 0.67 dusk .. 1) and lands on <paramref name="loc"/>.</summary>
        public static void Visit(string loc, float phase = 0.35f)
        {
            var s = G.Session;
            if (Instance != null) Instance.Paused = true;
            Player.BotStick = null;
            Time.timeScale = 1;
            SetPhase(phase);
            s.State.health = 100; s.State.hunger = 100; s.State.thirst = 100;
            if (GameRoot.Instance.OnMap && s.State.travel == null) Hud.Instance.CloseMap();
            if (s.State.location == loc) return;
            s.State.energy = World.EnergyMax;
            s.State.travel = null;
            if (s.Travel(World.Location(loc), true)) { s.State.travel.arriveAt = s.State.travel.leftAt; GameRoot.Instance.EnterArrived(); }
        }

        public static void SetPhase(float phase)
        {
            var s = G.Session;
            float day = Mathf.Floor((float)((s.State.time + Session.DayLength * 0.1f) / Session.DayLength));
            s.State.time = day * Session.DayLength + phase * Session.DayLength - Session.DayLength * 0.1f;
        }

        public static void Log(string m)
        {
            var s = G.Session;
            string line = "[" + Time.realtimeSinceStartup.ToString("0") + "s " + (s != null ? s.State.location + " L" + s.State.level + " " + s.CurrentQuest.Id : "") + "] " + m;
            _log.Add(line);
            Debug.Log("[PlayBot] " + m);
        }

        public static string Report(int lines = 40)
        {
            var s = G.Session;
            string head = "quest=" + s.CurrentQuest.Id + " (" + s.State.quest + "/" + Quests.All.Count + ") loc=" + s.State.location + " L" + s.State.level +
                          " hp=" + s.State.health.ToString("0") + " day=" + s.Day + " t=" + s.State.time.ToString("0") + " map=" + GameRoot.Instance.OnMap +
                          " travel=" + (s.State.travel != null) + " story=" + (Hud.Instance != null && Hud.Instance.StoryPlaying) + " cinema=" + Cinema.Playing;
            return head + "\n" + string.Join("\n", _log.Skip(System.Math.Max(0, _log.Count - lines)));
        }

        static void Grant(string id, int n, string why)
        {
            G.Session.Give(id, n);
            Log("GRANT " + n + " " + id + " (" + why + ")");
        }

        void Update()
        {
            var s = G.Session;
            var hud = Hud.Instance;
            if (Paused || s == null || hud == null || Player.Instance == null || LoadingScreen.Active) { Player.BotStick = Vector2.zero; return; }
            Player.BotStick = Vector2.zero;

            // Movies and story cards: watch a moment, then tap on.
            if (Cinema.Playing) { _tapT += Time.unscaledDeltaTime; if (_tapT > 2.5f) { _tapT = 0; Cinema.Skip(); Log("skipped a movie"); } return; }
            if (hud.StoryPlaying) { _tapT += Time.unscaledDeltaTime; if (_tapT > 0.6f) { _tapT = 0; hud.TapStory(); } return; }

            if (s.State.quest != _lastQuest)
            {
                if (_lastQuest >= 0) Log("DONE step " + Quests.All[_lastQuest].Id + " in " + _questT.ToString("0") + "s");
                _lastQuest = s.State.quest; _questT = 0; _bestDist = float.MaxValue; _quest = s.CurrentQuest.Id;
                Log("STEP " + _quest + ": " + s.CurrentQuest.Text);
            }
            _questT += Time.deltaTime;

            // Survive: a castaway this far into a test isn't meant to starve or bleed out.
            if (Heal && s.State.health < 30) { s.State.health = 100; Log("GRANT full health (was low; at " + Player.Instance.transform.position.ToString("0") + ")"); }
            if (s.State.hunger < 15) { s.State.hunger = 100; Log("GRANT food"); }
            if (s.State.thirst < 15) { s.State.thirst = 100; Log("GRANT water"); }

            if (s.State.travel != null)
            {
                if (s.Arrived) GameRoot.Instance.EnterArrived();
                else if (_questT > 1) { s.State.travel.arriveAt = s.State.travel.leftAt; Log("GRANT instant arrival"); }
                return;
            }
            if (GameRoot.Instance.OnMap) { hud.CloseMap(); return; }

            Step(s);
        }

        /// <summary>Goes to <paramref name="loc"/> by the map (running, energy refilled if short). True when already there.</summary>
        bool Be(string loc)
        {
            var s = G.Session;
            if (s.State.location == loc) return true;
            var to = World.Location(loc);
            if (!s.Unlocked(to)) { LevelTo(to.Level, "to reach " + to.Name); return false; }
            if (s.TravelBlocked(to, true) == "Not enough energy") { s.State.energy = World.EnergyMax; Log("GRANT energy"); }
            Hud.Instance.OpenMap();
            if (s.Travel(to, true)) Log("sailing to " + to.Name);
            else Log("travel blocked: " + s.TravelBlocked(to, true));
            return false;
        }

        void LevelTo(int level, string why)
        {
            var s = G.Session;
            if (s.State.level >= level) return;
            int xp = 0;
            while (s.State.level < level) { int n = Session.XpToNext(s.State.level); xp += n; s.AddXp(n); }
            Log("GRANT " + xp + " xp to level " + level + " (" + why + ")");
        }

        void Need(string id, int n, string why)
        {
            int have = G.Session.Count(id);
            if (have < n) Grant(id, n - have, why);
        }

        void Step(Session s)
        {
            switch (_quest)
            {
                case "spear":
                    if (s.Has("spear")) break;
                    if (s.Job("workbench") == null)
                    {
                        string why = s.CraftBlocked("spear");
                        if (why != null) { foreach (var c in Content.Recipes["spear"].Inputs) Need(c.id, c.n, "spear: " + why); }
                        if (s.Craft("spear")) Log("crafting a spear at the workbench");
                        else Log("craft spear failed: " + s.CraftBlocked("spear"));
                    }
                    break;
                case "cook":
                    if (s.Job("campfire") == null)
                    {
                        Need("raw_meat", 1, "to cook");
                        if (s.Craft("meat")) Log("roasting meat on the campfire");
                        else Log("cook failed: " + s.CraftBlocked("meat"));
                    }
                    break;
                case "level3": LevelTo(3, "grind step"); return;
                case "map": if (!Be("grove")) return; break;
                case "dog": if (!Be("shore")) return; break;
                case "tune":
                    var keeper = World.Signals.First(x => x.Id == "keeper");
                    if (s.State.level < keeper.Level) LevelTo(keeper.Level, "radio signal");
                    if (!s.Heard(keeper)) { s.Tune(keeper); Log("tuned the radio to " + keeper.Freq); }
                    return;
                case "keeper": if (!Rescue("keeper")) return; break;
                case "pier": if (!Rescue("fisher")) return; break;
                case "captive": if (!Rescue("captive")) return; break;
                case "powder":
                    if (!Be("cave")) return;
                    if (_questT > 240) { Need("gunpowder", 6, "skeletons too slow"); }
                    break;
                case "redjack": LevelTo(12, "Red Jack's camp"); if (!Be("pirates")) return; break;
                case "sail": if (!Be("pirates")) return; break;
                case "free": if (!Be("home")) return; Log("REACHED THE END"); Paused = true; return;
                default:
                    // Gather and build steps happen at home.
                    if (!Be("home")) return;
                    break;
            }

            // Build sites: the guide arrow leads to the missing materials; only hand them over if that stalls.
            var q = s.CurrentQuest;
            if (q.PointBuild != null && Content.Builds.ContainsKey(q.PointBuild) && !s.Built(q.PointBuild) && !s.CanDeliver(q.PointBuild) && _questT > 90)
                foreach (var c in s.Remaining(q.PointBuild)) Need(c.id, c.n, "build " + q.PointBuild);

            Walk(GuideArrow.GoalPosition(Player.Instance.transform.position));
        }

        bool Rescue(string sig)
        {
            var s = G.Session;
            var d = World.Signals.First(x => x.Id == sig);
            if (s.State.level < d.Level) LevelTo(d.Level, "signal " + sig);
            if (!s.Heard(d)) { s.Tune(d); Log("tuned the radio to " + d.Npc); }
            foreach (var c in d.Need) Need(c.id, c.n, d.Npc + " needs it");
            return Be(d.Location);
        }

        List<Vector3> _path;
        Vector3 _pathGoal;
        float _pathAge;

        void Walk(Vector3? goal)
        {
            var p = Player.Instance;
            if (!goal.HasValue) { if (Time.frameCount % 600 == 0) Log("no goal to walk to"); return; }
            var to = goal.Value - p.transform.position; to.y = 0;
            float dist = to.magnitude;
            if (dist < 1.0f || p.Working) return; // in the ring: the work happens
            if (dist < _bestDist - 0.5f) { _bestDist = dist; _stuckT = 0; }
            else _stuckT += Time.deltaTime;
            // Plan around slopes, water and obstacles (A* on a 1 m grid); replan when the goal moves or progress stalls.
            _pathAge += Time.deltaTime;
            if (_path == null || (goal.Value - _pathGoal).magnitude > 2.5f || _pathAge > 4f || _stuckT > 1.5f)
            {
                _path = FindPath(p.transform.position, goal.Value);
                _pathGoal = goal.Value; _pathAge = 0;
                if (_stuckT > 1.5f) { _stuckT = 0; _bestDist = float.MaxValue; }
                if (_path == null && Time.frameCount % 300 == 0) Log("no path to the goal " + dist.ToString("0") + " m away from " + p.transform.position.ToString("0"));
            }
            Vector3 aim = goal.Value;
            if (_path != null)
            {
                while (_path.Count > 1 && Flat(_path[0] - p.transform.position) < 0.7f) _path.RemoveAt(0);
                if (_path.Count > 0) aim = _path[0];
            }
            var d = aim - p.transform.position; d.y = 0;
            if (d.sqrMagnitude < 0.01f) return;
            d.Normalize();
            Player.BotStick = new Vector2(d.x, d.z);
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        static bool Open(float x, float z)
        {
            if (!Island.Walkable(x, z)) return false;
            var q = new Vector3(x, 0, z);
            return Flat(Obstacles.Resolve(q, Player.BodyRadius) - q) < 0.05f;
        }

        /// <summary>A* from <paramref name="a"/> to (near) <paramref name="b"/> over walkable 1 m cells; null if there is no way.</summary>
        static List<Vector3> FindPath(Vector3 a, Vector3 b)
        {
            const float cell = 1f;
            int ox = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - 30), oz = Mathf.FloorToInt(Mathf.Min(a.z, b.z) - 30);
            int w = Mathf.CeilToInt(Mathf.Abs(a.x - b.x) + 60), h = Mathf.CeilToInt(Mathf.Abs(a.z - b.z) + 60);
            if (w * h > 160000) return null;
            int Idx(int x, int z) { return z * w + x; }
            int sx = Mathf.Clamp(Mathf.RoundToInt(a.x - ox), 0, w - 1), sz = Mathf.Clamp(Mathf.RoundToInt(a.z - oz), 0, h - 1);
            int gx = Mathf.Clamp(Mathf.RoundToInt(b.x - ox), 0, w - 1), gz = Mathf.Clamp(Mathf.RoundToInt(b.z - oz), 0, h - 1);
            var g = new float[w * h]; var from = new int[w * h]; var state = new byte[w * h]; // 0 new, 1 open, 2 closed, 3 blocked
            for (int i = 0; i < g.Length; i++) { g[i] = float.MaxValue; from[i] = -1; }
            var open = new SortedSet<(float, int)>();
            int start = Idx(sx, sz);
            g[start] = 0; open.Add((0, start)); state[start] = 1;
            int best = start; float bestH = float.MaxValue;
            int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 }, dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
            int guard = 0;
            while (open.Count > 0 && guard++ < 60000)
            {
                var top = open.Min; open.Remove(top);
                int c = top.Item2; if (state[c] == 2) continue; state[c] = 2;
                int cx = c % w, cz = c / w;
                float hh = Mathf.Sqrt((cx - gx) * (cx - gx) + (cz - gz) * (cz - gz));
                if (hh < bestH) { bestH = hh; best = c; }
                if (hh < 1.5f) break; // next to the goal (which may itself be inside an obstacle: a tree, a crate)
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + dx[k], nz = cz + dz[k];
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int n = Idx(nx, nz);
                    if (state[n] >= 2) continue;
                    if (!Open(ox + nx * cell, oz + nz * cell)) { state[n] = 3; continue; }
                    float ng = g[c] + (k < 4 ? 1f : 1.414f);
                    if (ng >= g[n]) continue;
                    if (state[n] == 1) open.Remove((g[n] + Mathf.Sqrt((nx - gx) * (nx - gx) + (nz - gz) * (nz - gz)), n));
                    g[n] = ng; from[n] = c; state[n] = 1;
                    open.Add((ng + Mathf.Sqrt((nx - gx) * (nx - gx) + (nz - gz) * (nz - gz)), n));
                }
            }
            if (bestH > 4f) return null;
            var path = new List<Vector3>();
            for (int c = best; c != -1 && c != start; c = from[c]) path.Add(new Vector3(ox + (c % w) * cell, 0, oz + (c / w) * cell));
            path.Reverse();
            path.Add(b);
            return path;
        }

        void OnDestroy() { Player.BotStick = null; if (Instance == this) Instance = null; }
    }
}
#endif
