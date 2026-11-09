using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LastTide.EditorTools
{
    /// <summary>
    /// Command-line build entry points (used by Codemagic):
    ///   Unity -batchmode -quit -projectPath . -executeMethod LastTide.EditorTools.BuildScript.BuildIos
    /// Produces an Xcode project in ./ios which is then signed and archived on the Mac build machine.
    /// You can also run it from the editor menu on Windows (needs the iOS Build Support module).
    /// </summary>
    public static class BuildScript
    {
        public const string IosOutput = "ios";

        [MenuItem("LastTide/Build iOS Xcode Project")]
        public static void BuildIos()
        {
            ProjectSetup.EnsureScene();
            ProjectSetup.ApplyPlayerSettings();
            // The time scale is a project setting the play bot changes; a build must ship at real time.
            Time.timeScale = 1f;
#if LASTTIDE_URP
            UrpSetup.Run();
#endif
            AppIcon.Generate();
            ProjectSetup.IncludeShaders();

            var buildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER");
            if (!string.IsNullOrEmpty(buildNumber)) PlayerSettings.iOS.buildNumber = buildNumber;
            var version = Environment.GetEnvironmentVariable("APP_VERSION");
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
            // On CI, Codemagic applies the fetched App Store profile (xcode-project use-profiles);
            // the project only needs the team.
            var team = Environment.GetEnvironmentVariable("APPLE_TEAM_ID");
            if (!string.IsNullOrEmpty(team)) PlayerSettings.iOS.appleDeveloperTeamID = team;

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = IosOutput,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[LastTide] iOS build " + report.summary.result + " in " + report.summary.totalTime + ", " + report.summary.totalErrors + " errors");
            // A post-process failure (e.g. a platform module installed while the Editor was open) can still
            // report Succeeded, so errors count as a failure too.
            bool ok = report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0;
            if (!ok) Debug.LogError("[LastTide] iOS build failed");
            if (Application.isBatchMode && !ok) EditorApplication.Exit(1);
        }
    }
}
