using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace StorageLimits
{
    [BepInPlugin("com.teddit.storagelimits", "Storage Limits", "1.0.0")]
    [BepInDependency("com.teddit.teddit", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static string PluginDir;

        void Awake()
        {
            Log = Logger;
            PluginDir = Path.GetDirectoryName(Info.Location);
            Log.LogInfo("StorageLimits loaded.");
            new Harmony("com.teddit.storagelimits").PatchAll();
        }
    }
}
