using System.Collections.Generic;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// The first twenty minutes are about surviving: the season button, the trading post (gift), the shop's "+" and
    /// the map's RADIO / TRADE / SHOP wait until the castaway is settled (<see cref="Core.Session.Settled"/>), then
    /// appear with a word about where they are.
    /// </summary>
    public sealed partial class Hud
    {
        readonly List<GameObject> _later = new List<GameObject>();
        int _settled = -1; // -1 not checked yet

        void UpdatePacing()
        {
            bool now = G.Session.Settled;
            int v = now ? 1 : 0;
            if (v == _settled) return;
            if (_settled == 0 && now) Toast("Camp is settled: the season pass and the trading post are open (top right)");
            _settled = v;
            foreach (var g in _later) if (g != null) g.SetActive(now);
        }
    }
}
