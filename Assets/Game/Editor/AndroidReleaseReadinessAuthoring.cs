#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace FortressFrontier.Editor
{
    public static class AndroidReleaseReadinessAuthoring
    {
        [MenuItem("Fortress Frontier/Release/Apply Android Non-Identity Settings")]
        public static void Apply()
        {
            PlayerSettings.productName = "城垒争锋";
            PlayerSettings.bundleVersion = "0.4.0";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            AssetDatabase.SaveAssets();
            Debug.Log("Android non-identity release settings applied. Bundle identifier and keystore intentionally remain publisher-owned.");
        }
    }
}
#endif
