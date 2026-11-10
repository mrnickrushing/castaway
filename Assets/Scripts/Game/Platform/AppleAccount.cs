using System;
using UnityEngine;
#if UNITY_IOS
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Extensions;
using AppleAuth.Interfaces;
using AppleAuth.Native;
#endif

namespace LastTide.Game
{
    /// <summary>
    /// Sign in with Apple (optional). Last Tide has no server: signing in ties purchases (RevenueCat) to the
    /// player's Apple ID so they come back on any device, and names the castaway from the first name Apple sends
    /// if the player hasn't chosen one. The island itself travels through iCloud (<see cref="CloudSave"/>).
    /// </summary>
    public static class AppleAccount
    {
        const string UserKey = "apple.user";
        public static string UserId { get { return PlayerPrefs.GetString(UserKey, ""); } }
        public static bool SignedIn { get { return UserId.Length > 0; } }
        public static event Action Changed;

#if UNITY_IOS
        static IAppleAuthManager _auth;
        public static bool Supported { get { return Application.platform == RuntimePlatform.IPhonePlayer && AppleAuthManager.IsCurrentPlatformSupported; } }

        /// <summary>Call once at startup: checks a saved sign-in is still valid and links purchases to it.</summary>
        public static void Start()
        {
            if (!Supported) return;
            _auth = new AppleAuthManager(new PayloadDeserializer());
            if (!SignedIn) return;
            _auth.GetCredentialState(UserId, state =>
            {
                if (state == CredentialState.Authorized) Store.Current.Identify(UserId);
                else SignOut(); // revoked in Settings, or a different Apple ID
            }, error => Store.Current.Identify(UserId)); // offline: keep the saved sign-in
        }

        /// <summary>Pumps the native callbacks (call every frame).</summary>
        public static void Update() { if (_auth != null) _auth.Update(); }

        public static void SignIn(Action<bool, string> done)
        {
            if (_auth == null) { done(false, "Sign in with Apple works on iPhone and iPad."); return; }
            _auth.LoginWithAppleId(new AppleAuthLoginArgs(LoginOptions.IncludeFullName), credential =>
            {
                var apple = credential as IAppleIDCredential;
                PlayerPrefs.SetString(UserKey, credential.User);
                PlayerPrefs.Save();
                // Apple sends the name only on the very first sign-in.
                if (apple != null && apple.FullName != null && !string.IsNullOrEmpty(apple.FullName.GivenName)
                    && G.Session != null && string.IsNullOrEmpty(G.Session.State.name))
                    G.Session.Rename(apple.FullName.GivenName);
                Store.Current.Identify(credential.User);
                Changed?.Invoke();
                done(true, null);
            }, error =>
            {
                var cancelled = error.GetAuthorizationErrorCode() == AuthorizationErrorCode.Canceled;
                done(false, cancelled ? null : "Sign in with Apple failed. Please try again.");
            });
        }
#else
        public static bool Supported { get { return false; } }
        public static void Start() { }
        public static void Update() { }
        public static void SignIn(Action<bool, string> done) { done(false, "Sign in with Apple works on iPhone and iPad."); }
#endif

        public static void SignOut()
        {
            PlayerPrefs.DeleteKey(UserKey);
            PlayerPrefs.Save();
            Store.Current.Identify(null);
            Changed?.Invoke();
        }
    }
}
