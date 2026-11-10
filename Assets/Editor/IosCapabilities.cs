#if UNITY_IOS
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

namespace LastTide.EditorTools
{
    /// <summary>
    /// The app's capabilities in the exported Xcode project: Sign in with Apple and iCloud key-value storage (the
    /// save), in LastTide.entitlements with the team and bundle written out (the TestFlight job stamps them onto an
    /// unsigned archive with an ad-hoc signature, which doesn't expand $(TeamIdentifierPrefix)). The bundle id has
    /// both capabilities enabled on App Store Connect (Tools/appstore/capabilities.py), so the App Store profile made
    /// at export carries them. Reminders are local notifications and need no capability.
    /// </summary>
    public sealed class IosCapabilities : IPreprocessBuildWithReport
    {
        public const string Entitlements = "LastTide.entitlements";
        public int callbackOrder { get { return 0; } }

        /// <summary>The game asks for notification permission on the first voyage, not at launch.</summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            Unity.Notifications.NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch = false;
            Unity.Notifications.NotificationSettings.iOSSettings.AddRemoteNotificationCapability = false;
        }

        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string team = PlayerSettings.iOS.appleDeveloperTeamID;
            string bundle = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);

            var ent = new PlistDocument();
            ent.root.CreateArray("com.apple.developer.applesignin").AddString("Default");
            ent.root.SetString("com.apple.developer.ubiquity-kvstore-identifier", team + "." + bundle);
            ent.WriteToFile(Path.Combine(path, Entitlements));

            var projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string main = proj.GetUnityMainTargetGuid();
            proj.AddFile(Entitlements, Entitlements);
            proj.SetBuildProperty(main, "CODE_SIGN_ENTITLEMENTS", Entitlements);
            // Sign in with Apple's native half lives in UnityFramework.
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "AuthenticationServices.framework", true);
            proj.WriteToFile(projPath);
        }
    }
}
#endif
