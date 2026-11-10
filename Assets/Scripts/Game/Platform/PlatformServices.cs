using UnityEngine;

namespace LastTide.Game
{
    /// <summary>Starts the Apple services once the island is up and drives them through the app's lifecycle:
    /// Sign in with Apple, the iCloud save and reminder notifications.</summary>
    public sealed class PlatformServices : MonoBehaviour
    {
        void Start()
        {
            Reminders.Clear();
            AppleAccount.Start();
        }

        void Update() { AppleAccount.Update(); }

        void OnApplicationPause(bool paused)
        {
            if (paused) Reminders.Schedule();
            else Reminders.Clear();
        }

        void OnApplicationQuit() { Reminders.Schedule(); }
    }
}
