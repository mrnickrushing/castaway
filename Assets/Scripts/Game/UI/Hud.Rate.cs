using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// Asking for an App Store rating, once: at a happy moment (a level's rewards claimed or home from a trip, level 4
    /// and up) a small card asks "Enjoying Last Tide?". RATE IT brings up Apple's own rating sheet; either answer
    /// means it never asks again on this device. Settings keeps a Rate row that opens the App Store page.
    /// </summary>
    public sealed partial class Hud
    {
        const string RateAskedPref = "lt_rate_asked";
        const int RateFromLevel = 4;
        public const string ReviewUrl = "https://apps.apple.com/app/id6832382482?action=write-review";

        float _rateIn = -1;

        /// <summary>Never ask (the PlayBot sets it, so the card can't stall its run).</summary>
        public static bool RateOff;

        static bool RateAsked { get { return PlayerPrefs.GetInt(RateAskedPref, 0) == 1; } }

        static void MarkRateAsked() { PlayerPrefs.SetInt(RateAskedPref, 1); PlayerPrefs.Save(); }

        /// <summary>A good moment just happened: ask once the screen is clear again.</summary>
        void RateSoon()
        {
            if (RateOff || RateAsked || G.Session.State.level < RateFromLevel || !G.Session.Settled) return;
            _rateIn = 1.5f;
        }

        void UpdateRate()
        {
            if (_rateIn < 0) return;
            // Wait for the reward card, a story scene, the map or an open panel to be gone.
            if (Blocking || MapOpen || _confirm != null || InvOpen || _building || _bpOpen || _radioOpen || _offersOpen || _shopOpen || _helpOpen || _pathOpen || _seasonOpen) return;
            if ((_rateIn -= Time.unscaledDeltaTime) > 0) return;
            _rateIn = -1;
            if (RateAsked) return;
            MarkRateAsked();
            Confirm("Enjoying Last Tide?",
                "If you're having fun on the island, a quick rating on the App Store helps other castaways find it.",
                "RATE IT", RequestRating, null, "NO THANKS");
        }

        /// <summary>Apple's in-app rating sheet (the App Store page where that isn't available).</summary>
        static void RequestRating()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (UnityEngine.iOS.Device.RequestStoreReview()) return;
#endif
            Application.OpenURL(ReviewUrl);
        }

        /// <summary>The Settings row: straight to the App Store's review page.</summary>
        static void OpenStoreReview()
        {
            MarkRateAsked();
            Application.OpenURL(ReviewUrl);
        }
    }
}
