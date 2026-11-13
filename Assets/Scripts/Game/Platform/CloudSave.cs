using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using LastTide.Core;
using UnityEngine;

namespace LastTide.Game
{
    /// <summary>
    /// The save in the player's own iCloud (key-value storage, no server of ours): written beside the local file
    /// at most once a minute and when the game goes to the background, read at launch. A new phone continues the
    /// island at once; a newer save from another device is offered, never forced (<see cref="CloudRules"/>).
    /// Only on an iPhone signed in to iCloud; elsewhere every call does nothing.
    /// </summary>
    public static class CloudSave
    {
        const string SaveKey = "lasttide.save", MetaKey = "lasttide.meta", SeenPref = "cloud.seen", PushedPref = "cloud.pushed", StartOverPref = "cloud.startover";
        const float Every = 60f;
        static float _next;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern bool LTCloudAvailable();
        [DllImport("__Internal")] static extern void LTCloudSet(string key, string value);
        [DllImport("__Internal")] static extern string LTCloudGet(string key);
        [DllImport("__Internal")] static extern void LTCloudRemove(string key);
        [DllImport("__Internal")] static extern void LTCloudSync();
        public static bool Available { get { try { return LTCloudAvailable(); } catch (Exception) { return false; } } }
#else
        static bool LTCloudAvailable() { return false; }
        static void LTCloudSet(string key, string value) { }
        static string LTCloudGet(string key) { return null; }
        static void LTCloudRemove(string key) { }
        static void LTCloudSync() { }
        public static bool Available { get { return false; } }
#endif

        /// <summary>This device, as the meta names it.</summary>
        public static string Me { get { return SystemInfo.deviceUniqueIdentifier; } }

        /// <summary>Saved-at of the newest iCloud save this device took, kept over or wrote itself.</summary>
        public static double Seen
        {
            get { double v; return double.TryParse(PlayerPrefs.GetString(SeenPref, "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0; }
            set { PlayerPrefs.SetString(SeenPref, value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); PlayerPrefs.Save(); }
        }

        /// <summary>Real time of this device's last upload (0 = never), for Settings.</summary>
        public static double LastPushed
        {
            get { double v; return double.TryParse(PlayerPrefs.GetString(PushedPref, "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0; }
        }

        /// <summary>Raised when a newer save from another device turns up mid-game (the HUD asks what to do).</summary>
        public static event Action<CloudMeta> NewerElsewhere;

        public static CloudMeta Meta()
        {
            if (!Available) return null;
            return CloudMeta.Parse(LTCloudGet(MetaKey));
        }

        /// <summary>The save in iCloud, or null (none, unreadable, or no iCloud). Call at launch.</summary>
        public static GameState Fetch()
        {
            if (!Available) return null;
            try
            {
                LTCloudSync();
                var packed = LTCloudGet(SaveKey);
                if (string.IsNullOrEmpty(packed)) return null;
                var s = JsonUtility.FromJson<GameState>(Unpack(packed));
                return s != null && s.backpack != null && s.backpack.Count > 0 ? s : null;
            }
            catch (Exception e) { Debug.LogWarning("iCloud save unreadable: " + e.Message); return null; }
        }

        /// <summary>Writes the save to iCloud (at most once a minute unless <paramref name="now"/>), unless another
        /// device has saved since: then <see cref="NewerElsewhere"/> fires and nothing is written.</summary>
        public static void Push(GameState s, bool now = false)
        {
            if (!now && Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Every;
            if (!Available || s == null) return;
            try
            {
                var meta = Meta();
                if (CloudRules.Decide(true, meta, Me, Seen) == CloudStep.Offer) { NewerElsewhere?.Invoke(meta); return; }
                LTCloudSet(SaveKey, Pack(JsonUtility.ToJson(s)));
                LTCloudSet(MetaKey, new CloudMeta { SavedAt = s.savedAt, Level = s.level, Device = Me }.ToString());
                Seen = s.savedAt;
                PlayerPrefs.SetString(PushedPref, s.savedAt.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("iCloud save failed: " + e.Message); }
        }

        /// <summary>
        /// START OVER: the next launch begins a new island instead of continuing the one in iCloud (with nothing saved
        /// on the phone it would adopt it again), and that new island replaces the iCloud copy.
        /// </summary>
        public static void StartOver()
        {
            PlayerPrefs.SetInt(StartOverPref, 1);
            var meta = Meta();
            if (meta != null) Seen = meta.SavedAt;   // ours may overwrite it
            PlayerPrefs.Save();
        }

        /// <summary>True once after <see cref="StartOver"/>.</summary>
        public static bool TakeStartOver()
        {
            if (PlayerPrefs.GetInt(StartOverPref, 0) == 0) return false;
            PlayerPrefs.DeleteKey(StartOverPref);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Removes the save from iCloud (Delete account).</summary>
        public static void Erase()
        {
            if (Available) { LTCloudRemove(SaveKey); LTCloudRemove(MetaKey); }
            PlayerPrefs.DeleteKey(SeenPref);
            PlayerPrefs.DeleteKey(PushedPref);
        }

        static string Pack(string json)
        {
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionMode.Compress))
                {
                    var b = Encoding.UTF8.GetBytes(json);
                    gz.Write(b, 0, b.Length);
                }
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        static string Unpack(string packed)
        {
            using (var ms = new MemoryStream(Convert.FromBase64String(packed)))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            using (var r = new StreamReader(gz, Encoding.UTF8))
                return r.ReadToEnd();
        }
    }
}
