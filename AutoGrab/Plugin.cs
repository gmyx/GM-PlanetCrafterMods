using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AutoMineAndGrab_Plugin
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Planet Crafter.exe")]
    public class Plugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> isDebug;
        private static ConfigEntry<bool> intervalCheckMine;
        private static ConfigEntry<bool> intervalCheckGrab;
        private static ConfigEntry<string> checkToggleKeyMine;
        private static ConfigEntry<string> checkToggleKeyGrab;
        private static ConfigEntry<string> checkKeyMine;
        private static ConfigEntry<string> checkKeyGrab;        
        private static ConfigEntry<float> checkIntervalMine;
        private static ConfigEntry<float> checkIntervalGrab;
        private static ConfigEntry<float> maxRangeMine;
        private static ConfigEntry<float> maxRangeGrab;
        private static ConfigEntry<string> allowList;
        private static ConfigEntry<string> disallowList;

        private static float elapsedMine;
        private static float elapsedGrab;

        private InputAction actionToggleMine;
        private InputAction actionCheckMine;
        private InputAction actionToggleGrab;
        private InputAction actionCheckGrab;

        private readonly Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        private static ManualLogSource LoggerInt;

        public static void Dbgl(string str = "", LogLevel logLevel = LogLevel.Debug)
        {
            if (isDebug.Value)
                LoggerInt.LogInfo(str);
        }
        private void Awake()
        {
            //context = this;            
            isDebug = Config.Bind<bool>("General", "IsDebug", false, "Enable debug logs");

            // minables settings
            intervalCheckMine = Config.Bind<bool>("Options", "IntervalCheckMine", true, "Enable interval checking for minables");
            checkIntervalMine = Config.Bind<float>("Options", "CheckIntervaMine", 3f, "Seconds betweeen check for minables");
            maxRangeMine = Config.Bind<float>("Options", "MaxRangeMine", 10f, "Range to check in meters for minables");
            checkToggleKeyMine = Config.Bind<string>("Options", "IntervalCheckKeyMine", "<Keyboard>/v", "Key to enable / disable interval checking for minables");
            checkKeyMine = Config.Bind<string>("Options", "CheckKeyMine", "<Keyboard>/c", "Key to check manually for minables");

            // grababbles settings
            intervalCheckGrab = Config.Bind<bool>("Options", "IntervalCheckGrab", true, "Enable interval checking for grabables");
            checkIntervalGrab = Config.Bind<float>("Options", "CheckIntervalGrab", 3f, "Seconds betweeen check for grabables");
            maxRangeGrab = Config.Bind<float>("Options", "MaxRangeGrab", 10f, "Range to check in meters for grabables");
            checkToggleKeyGrab = Config.Bind<string>("Options", "IntervalCheckKeyGrab", "<Keyboard>/b", "Key to enable / disable interval checking for grabables");
            checkKeyGrab = Config.Bind<string>("Options", "CheckKeyGrab", "<Keyboard>/n", "Key to check manually for grabables");

            //global allowLists/disallowList
            allowList = Config.Bind<string>("Options", "AllowList", "", "Comma-separated list of item IDs to allow mining (overrides DisallowList).");
            disallowList = Config.Bind<string>("Options", "DisallowList", "GoldenEffigie1,GoldenEffigie2,GoldenEffigie3", "Comma-separated list of item IDs to disallow mining (if AllowList is empty)");

            actionToggleMine = new InputAction(binding: checkToggleKeyMine.Value);
            actionToggleMine.Enable();
            actionCheckMine = new InputAction(binding: checkKeyMine.Value);
            actionCheckMine.Enable();

            actionToggleGrab = new InputAction(binding: checkToggleKeyGrab.Value);
            actionToggleGrab.Enable();
            actionCheckGrab = new InputAction(binding: checkKeyGrab.Value);
            actionCheckGrab.Enable();

            LoggerInt = Logger;
            harmony.PatchAll(typeof(AutoMineAndGrab_Plugin.Plugin));
            Dbgl("Plugin awake");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGrower), "Update")]
        private static void MachineGrower_Update_Postfix(GameObject ___instantiatedGameObject, bool ___hasEnergy, WorldObject ___worldObjectGrower, Inventory ___inventory)
        {
            if (___hasEnergy && ___worldObjectGrower.GetGrowth() == 100f && ___instantiatedGameObject == null)
            {
                if (___inventory.GetSize() == 1)
                {
                    WorldObject inventoryObject = ___inventory.GetInsideWorldObjects()[0];
                    ___inventory.RemoveItem(inventoryObject, false);
                    ___inventory.AddItem(inventoryObject);
                    Dbgl("Fixed instance of grower...");
                }
            }
        }

        private void CheckForNearbyType<T>(ConfigEntry<float> maxRange, string type) where T : SpaceCraft.Actionnable
        {
            if (!Managers.GetManager<PlayersManager>() || Managers.GetManager<WindowsHandler>().GetHasUiOpen())
                return;

            List<string> allow = allowList.Value.Split(',').ToList();
            List<string> disallow = disallowList.Value.Split(',').ToList();
            var player = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            InformationsDisplayer informationsDisplayer = Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer();
            int count = 0;
            foreach (var m in FindObjectsOfType<T>())
            {
                UnityEngine.Vector3 pos = player.transform.position;
                var dist = UnityEngine.Vector3.Distance(m.transform.position, pos);
                if (dist > maxRange.Value)
                    continue;

                var worldObjectAssociated = m.GetComponent<WorldObjectAssociated>();
                if (worldObjectAssociated == null)
                    continue;

                WorldObject worldObject = worldObjectAssociated.GetWorldObject();

                if (allowList.Value.Length > 0)
                {
                    if (!allow.Contains(worldObject.GetGroup().GetId()))
                        continue;
                }
                else if (disallowList.Value.Length > 0)
                {
                    if (disallow.Contains(worldObject.GetGroup().GetId()))
                        continue;
                }

                //if T is grabable, call Grab(), else 'mine'
                /*if (typeof(ActionGrabable).IsAssignableFrom(typeof(T)))
                {
                    ActionGrabable ag = m as ActionGrabable;
                    ag.OnAction();
                    //if (ag.gameObject == null)
                    //{
                        // display message
                        informationsDisplayer.AddInformation(2f, Readable.GetGroupName(worldObject.GetGroup()), DataConfig.UiInformationsType.InInventory, worldObject.GetGroup().GetImage());
                        worldObject.SetDontSaveMe(false);
                        Managers.GetManager<DisplayersHandler>().GetItemWorldDislpayer().Hide();
                    //}                       
                    
                } else*/ if (player.GetPlayerBackpack().GetInventory().AddItem(worldObject))
                {
                    Destroy(m.gameObject);
                    informationsDisplayer.AddInformation(2f, Readable.GetGroupName(worldObject.GetGroup()), DataConfig.UiInformationsType.InInventory, worldObject.GetGroup().GetImage());
                    worldObject.SetDontSaveMe(false);
                    Managers.GetManager<DisplayersHandler>().GetItemWorldDislpayer().Hide();
                    count++;
                }
                else
                {
                    break;
                }
                
            }
            if (count > 0)
            {
                player.GetPlayerAudio().PlayGrab();
            }
            Dbgl($"{type} {count} items");

        }

        private void UpdateMinable()
        {
            if (actionToggleMine.WasPressedThisFrame())
            {
                intervalCheckMine.Value = !intervalCheckMine.Value;
                if (Managers.GetManager<PopupsHandler>() != null)
                    AccessTools.FieldRefAccess<PopupsHandler, List<PopupData>>(Managers.GetManager<PopupsHandler>(), "popupsToPop").Add(new PopupData(null, $"AutoMine {(intervalCheckMine.Value ? "Enabled" : "Disabled")}", 2));
                if (intervalCheckMine.Value)
                    elapsedMine = checkIntervalMine.Value;
                return;
            }
            if (actionCheckMine.WasPressedThisFrame())
            {
                Dbgl($"Pressed manual check key for minables");
                elapsedMine = 0;
                CheckForNearbyType<ActionMinable>(maxRangeMine, "Mined");
                return;
            }
            if (intervalCheckMine.Value)
            {
                elapsedMine += Time.deltaTime;
                if (elapsedMine > checkIntervalMine.Value)
                {
                    elapsedMine = 0;
                    CheckForNearbyType<ActionMinable>(maxRangeMine, "Mined");
                    return;
                }
            }
        }

        private void UpdateGrabable()
        {
            if (actionToggleGrab.WasPressedThisFrame())
            {
                intervalCheckGrab.Value = !intervalCheckGrab.Value;
                if (Managers.GetManager<PopupsHandler>() != null)
                    AccessTools.FieldRefAccess<PopupsHandler, List<PopupData>>(Managers.GetManager<PopupsHandler>(), "popupsToPop").Add(new PopupData(null, $"AutoGrab {(intervalCheckGrab.Value ? "Enabled" : "Disabled")}", 2));
                if (intervalCheckGrab.Value)
                    elapsedGrab = checkIntervalGrab.Value;
                return;
            }
            if (actionCheckGrab.WasPressedThisFrame())
            {
                Dbgl($"Pressed manual check key for grabable");
                elapsedGrab = 0;
                CheckForNearbyType<ActionGrabable>(maxRangeGrab, "Grabed");
                return;
            }
            if (intervalCheckGrab.Value)
            {
                elapsedGrab += Time.deltaTime;
                if (elapsedGrab > checkIntervalGrab.Value)
                {
                    elapsedGrab = 0;
                    CheckForNearbyType<ActionGrabable>(maxRangeGrab, "Grabed");
                    return;
                }
            }
        }

        private void Update()
        {
            UpdateMinable();
            UpdateGrabable();
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }
    }
}