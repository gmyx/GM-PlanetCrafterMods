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
        private static MeteoHandler meteoHandlerInstance;
        //private static MethodInfo launchTryToLaunchAnEvent;

        private readonly Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        private static ManualLogSource LoggerInt;

        private void Awake()
        {
            configAstroidTimeMultiplier = Config.Bind("General", "Astroid_Time_Multiplier", 1.0f,
                "How long between attempts to start an meteo events. 1.0 is no change. 0.5 would be twice as often. 2.0 would be half as often. 0 Disables");


            harmony.PatchAll(typeof(LessFrequentAsteroids_Plugin.Plugin));

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            LoggerInt = Logger;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MeteoHandler), "Start")]
        private static void MeteoHandler_Start_Postfix(MeteoHandler __instance)
        {
            meteoHandlerInstance = __instance;
            //launchTryToLaunchAnEvent = HarmonyLib.AccessTools.Method(typeof(MeteoHandler), "TryToLaunchAnEvent");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MeteoHandler), "TryToLaunchAnEvent")]
        private static void MeteoHandler_TryToLaunchAnEvent_Prefix(ref float timeRepeat)
        {
            LoggerInt.LogInfo(string.Format("timeRepeat value is {0} and changed to {1}", timeRepeat, timeRepeat * configAstroidTimeMultiplier.Value));
            timeRepeat *= configAstroidTimeMultiplier.Value;
        }

        /*[HarmonyPrefix]
        [HarmonyPatch(typeof(MeteoHandler), "TryToLaunchAnEventLogic")]
        private static void MeteoHandler_TryToLaunchAnEventLogic_Prefix(ref float ___num)
        {
            LoggerInt.LogInfo(string.Format("num value is {0}", ___num));
        }*/

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }
    }
    
}
