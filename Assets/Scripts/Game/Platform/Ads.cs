using System;
using LastTide.Core;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using GoogleMobileAds.Api;
#endif

namespace LastTide.Game
{
    /// <summary>
    /// Rewarded adverts (Google AdMob): only ever shown because the player tapped WATCH, and paid out through
    /// <see cref="Session.GrantAd"/>. Requests are non-personalised ("npa"), so nothing is tracked and iOS never asks
    /// for tracking permission. In the Editor an advert is "watched" at once so the rewards can be tried.
    /// </summary>
    public static class Ads
    {
        // The rewarded unit from the AdMob account. Until it exists this is Google's public test unit, which only
        // ever serves test adverts.
        const string RewardedUnit = "ca-app-pub-3940256099942544/1712485313";

        static bool _started, _loading;
        static float _retryAt;
#if UNITY_IOS && !UNITY_EDITOR
        static RewardedAd _ad;
#endif

        /// <summary>An advert is loaded and can be shown this moment.</summary>
        public static bool Ready
        {
            get
            {
#if UNITY_EDITOR
                return true;
#elif UNITY_IOS
                return _ad != null && _ad.CanShowAd();
#else
                return false;
#endif
            }
        }

        /// <summary>Starts the SDK and loads the first advert. Called once the game is running, never at launch.</summary>
        public static void Start()
        {
            if (_started) return;
            _started = true;
#if UNITY_IOS && !UNITY_EDITOR
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.SetiOSAppPauseOnBackground(true);
            MobileAds.Initialize(_ => Load());
#endif
        }

        /// <summary>Keeps an advert waiting: tries again a while after a failed load.</summary>
        public static void Tick()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (_started && _ad == null && !_loading && _retryAt > 0 && Time.unscaledTime >= _retryAt) Load();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        static void Load()
        {
            if (_loading) return;
            _loading = true; _retryAt = 0;
            var request = new AdRequest();
            request.Extras.Add("npa", "1");       // non-personalised: no tracking
            RewardedAd.Load(RewardedUnit, request, (ad, error) =>
            {
                _loading = false;
                if (error != null || ad == null) { _retryAt = Time.unscaledTime + 60; return; }
                _ad = ad;
            });
        }
#endif

        /// <summary>Shows an advert; <paramref name="done"/> hears true only if it was watched to the reward.</summary>
        public static void Show(Action<bool> done)
        {
#if UNITY_EDITOR
            done(true);
#elif UNITY_IOS
            if (!Ready) { done(false); return; }
            var ad = _ad; _ad = null;
            bool earned = false, told = false;
            Action finish = () => { if (told) return; told = true; ad.Destroy(); Sfx.Duck = 1; Load(); done(earned); };
            ad.OnAdFullScreenContentClosed += finish;
            ad.OnAdFullScreenContentFailed += _ => finish();
            Sfx.Duck = 0;
            ad.Show(_ => earned = true);
#else
            done(false);
#endif
        }

        /// <summary>Watch an advert for a reward: the one call the HUD makes.</summary>
        public static void Watch(AdReward reward, Action<bool> after = null)
        {
            var s = G.Session;
            if (!s.AdOffered(reward) || !Ready) { Hud.Instance.Toast("No advert ready just now. Try again in a moment."); after?.Invoke(false); return; }
            Show(ok =>
            {
                bool paid = ok && s.GrantAd(reward);
                if (paid) { Sfx.Play("coin"); GameRoot.Instance.Save(); }
                else if (!ok) Hud.Instance.Toast("The advert wasn't finished, so there's no reward.");
                after?.Invoke(paid);
            });
        }
    }
}
