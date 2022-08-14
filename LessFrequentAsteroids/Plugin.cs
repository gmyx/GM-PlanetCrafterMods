using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using UnityEngine.InputSystem;

namespace LessFrequentAsteroids_Plugin
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Planet Crafter.exe")]
    public class Plugin : BaseUnityPlugin
    {
        private static ConfigEntry<float> configAstroidTimeMultiplier;
        private static ConfigEntry<bool> configAstroidIsDebug;
        private static MeteoHandler meteoHandlerInstance;        

        private readonly Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        private static ManualLogSource LoggerInt;

        private void Awake()
        {
            configAstroidTimeMultiplier = Config.Bind("General", "Astroid_Time_Multiplier", 1.0f,
                "How long between attempts to start an meteo events. 1.0 is no change. 0.5 would be twice as often. 2.0 would be half as often. 0 Disables");
            configAstroidIsDebug = Config.Bind<bool>("General", "IsDebug", false, "Enable debug logs");

            harmony.PatchAll(typeof(LessFrequentAsteroids_Plugin.Plugin));
            
            if (configAstroidIsDebug.Value != false)
                Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            LoggerInt = Logger;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MeteoHandler), "Start")]
        private static void MeteoHandler_Start_Postfix(MeteoHandler __instance)
        {
            meteoHandlerInstance = __instance;            
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MeteoHandler), "TryToLaunchAnEvent")]
        private static void MeteoHandler_TryToLaunchAnEvent_Prefix(ref float timeRepeat)
        {
            if (configAstroidIsDebug.Value != false)
                LoggerInt.LogInfo(string.Format("timeRepeat value is {0} and changed to {1}", timeRepeat, timeRepeat * configAstroidTimeMultiplier.Value));
            timeRepeat *= configAstroidTimeMultiplier.Value;
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }
    }
    
}
