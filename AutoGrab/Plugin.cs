using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
//using MijuTools;
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

#if DEBUG
        private InputAction actionGetMOGs;
#endif

        private readonly Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        private static ManualLogSource LoggerInt;

        static MethodInfo updateGrowing;
        static MethodInfo instantiateAtRandomPosition;

        static bool loadCompleted;

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
            maxRangeMine = Config.Bind<float>("Options", "MaxRangeMine", 20f, "Range to check in meters for minables");
            checkToggleKeyMine = Config.Bind<string>("Options", "IntervalCheckKeyMine", "<Keyboard>/v", "Key to enable / disable interval checking for minables");
            checkKeyMine = Config.Bind<string>("Options", "CheckKeyMine", "<Keyboard>/c", "Key to check manually for minables");

            // grababbles settings
            intervalCheckGrab = Config.Bind<bool>("Options", "IntervalCheckGrab", true, "Enable interval checking for grabables");
            checkIntervalGrab = Config.Bind<float>("Options", "CheckIntervalGrab", 3f, "Seconds betweeen check for grabables");
            maxRangeGrab = Config.Bind<float>("Options", "MaxRangeGrab", 20f, "Range to check in meters for grabables");
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

#if DEBUG
            actionGetMOGs = new InputAction(binding: "<Keyboard>/z");
            actionGetMOGs.Enable();
#endif

            //connect to functions
            updateGrowing = AccessTools.Method(typeof(MachineOutsideGrower), "UpdateGrowing", new Type[] { typeof(float) });
            instantiateAtRandomPosition = AccessTools.Method(typeof(MachineOutsideGrower), "InstantiateAtRandomPosition", new Type[] { typeof(GameObject), typeof(bool) });

            LoggerInt = Logger;
            harmony.PatchAll(typeof(AutoMineAndGrab_Plugin.Plugin));
            Dbgl($"Plugin awake; AutoMine: {intervalCheckMine.Value}; AutoGrab: {intervalCheckGrab.Value}");
        }

        /*[HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGrower), "Update")]
        private static void MachineGrower(MachineGrower __instance, GameObject ___instantiatedGameObject, bool ___hasEnergy, WorldObject ___worldObjectGrower, Inventory ___inventory)
        {
            //force seed to be re-inserted
            if (___hasEnergy && ___worldObjectGrower.GetGrowth() == 100f && ___instantiatedGameObject == null && loadCompleted == true)
            {
                if (___inventory.GetSize() == 1)
                {
                    //WorldObject inventoryObject = ___inventory.GetInsideWorldObjects()[0];
                    //___inventory.RemoveItem(inventoryObject, false);
                    ___worldObjectGrower.SetGrowth(30f);
                    __instance.SetGrowerInventory(___inventory);
                    Dbgl("Fixed instance of grower...");
                }
            }
        }*/

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionGrabable), "Grab")]
        private static bool Grab(ActionGrabable __instance, bool ___canGrab, ref PlayerMainController ___playerSource, 
            ItemWorldDislpayer ___itemWorldDisplayer, ref Grabed ___grabedEvent)
        {
            Dbgl("inside grab");
            //re implement due to some fields being null sometimes
            if (___canGrab)
            {
                try
                {                    
                    WorldObject worldObject = __instance.GetComponent<WorldObjectAssociated>().GetWorldObject();
                    if (worldObject == null)
                    {
                        Dbgl("worldObject is null"); //don't know why, but makes it ungrabbable
                        return false;
                    }

                    //validate params, can cause errors
                    if (___playerSource == null)
                    {
                        ___playerSource = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                    }

                    UnityEngine.Object.Destroy(__instance.gameObject);
                    ___playerSource.GetPlayerBackpack().GetInventory().AddItem(worldObject);
                    worldObject.SetDontSaveMe(_dontSaveMe: false);
                    ___playerSource.GetPlayerAudio().PlayGrab();
                    if (___itemWorldDisplayer == null) //can be null, but no harm
                    { 
                        ___itemWorldDisplayer.Hide();
                    }
                    if (___grabedEvent != null)
                    {
                        ___grabedEvent(worldObject);
                    }
                    ___grabedEvent = null;                    
                } catch (Exception e)
                {
                    Dbgl(e.Message);
                    Dbgl(e.StackTrace);
                }
            }
            

            return false; //do not run original
        }

        private bool CheckMOGList(List<GameObject> ligo, UnityEngine.Vector3 pos, ConfigEntry<float> maxRange, PlayerMainController player, MachineOutsideGrower mog, InformationsDisplayer informationsDisplayer, bool pickup)
        {
            try
            {
                foreach (GameObject gameObject in ligo)
                {
                    var worldObjectAssociated = gameObject.GetComponent<WorldObjectAssociated>();
                    if (worldObjectAssociated == null)
                        continue;

                    WorldObject worldObject = worldObjectAssociated.GetWorldObject();
                    Dbgl($"worldObject: {worldObject.GetGroup().GetId()}; Growth: {worldObject.GetGameObject().transform.localScale.x}");

                    if (worldObject.GetGroup().GetId() == "Algae1Seed" && worldObject.GetGameObject().transform.localScale.x > 0.5)
                    {
                        //secondary range check
                        var dist2 = UnityEngine.Vector3.Distance(gameObject.transform.position, pos);
                        if (dist2 > maxRange.Value) //item must be in range, not just grower
                            continue;

                        //pick it up
                        if (pickup)
                        {
                            if (player.GetPlayerBackpack().GetInventory().AddItem(worldObject))
                            {
                                Dbgl($"{dist2}");

                                // call on grabbed, so it regrows
                                MethodInfo dynMethod = mog.GetType().GetMethod("OnGrabedAGrowing", BindingFlags.NonPublic | BindingFlags.Instance);
                                dynMethod.Invoke(mog, new object[] { worldObject });
                                Dbgl("onGrabbed");                                

                                //display results
                                informationsDisplayer.AddInformation(2f, Readable.GetGroupName(worldObject.GetGroup()), DataConfig.UiInformationsType.InInventory, worldObject.GetGroup().GetImage());                                
                                Managers.GetManager<DisplayersHandler>().GetItemWorldDislpayer().Hide();

                                //destroy worldObject                                
                                worldObject.SetDontSaveMe(false); //don't - already done in OnGrabedAGrowing
                                worldObject = null;
                                gameObject.SetActive(false); //hide it and stop events?
                                //Destroy(gameObject); //if destory, cause doubling, without memoryleak?

                                Dbgl("do grab!");

                                return true;
                            }
                        }
                    }
                }

                return false;
            } catch
            {
                return false; //stop
            }
        }

        private void CheckForNearbyMachineOutsideGrower(ConfigEntry<float> maxRange, bool pickup = true)
        {
            var player = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            UnityEngine.Vector3 pos = player.transform.position;
            InformationsDisplayer informationsDisplayer = Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer();
            int count = 0;

            //call a function on grower - can find by looking for growser a location                            
            foreach (MachineOutsideGrower mog in FindObjectsOfType<MachineOutsideGrower>())
            {                
                //since we need to check inside growers, do range * 5 to ensure we capture everything
                var dist = UnityEngine.Vector3.Distance(mog.transform.position, pos);
                if (dist > maxRange.Value * 5) //to add a config if this works
                    continue;

                bool cont = false;
                int failSafe = 0;
                do
                {
                    if (++failSafe >= 10)
                        continue;

                    //get it's list of instantiatedGameObjects
                    FieldInfo instantiatedGameObjects = mog.GetType().GetField("instantiatedGameObjects", BindingFlags.NonPublic | BindingFlags.Instance);
                    List<GameObject> ligo = (List<GameObject>)instantiatedGameObjects.GetValue(mog);
                    Dbgl($"mog: {mog.GetInstanceID()}; {ligo.Count}; {dist}");

                    cont = CheckMOGList(ligo, pos, maxRange, player, mog, informationsDisplayer, pickup);
                    if (cont)
                        count++;
                } while (cont == true); //will only be true if item found
            }
            
            //wrap up
            if (count > 0)
            {
                player.GetPlayerAudio().PlayGrab();
            }
            Dbgl($"Grabbed {count} algae !");

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
            UnityEngine.Vector3 pos = player.transform.position;

            //get all object of type, either minable or grabbale (TBD if grababble still valid)
            foreach (var m in FindObjectsOfType<T>())
            {                
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
                Dbgl($"checking {worldObject.GetGroup().GetId()}");
                if (typeof(ActionGrabable).IsAssignableFrom(typeof(T)))
                {
                    //this works on vegetables, no algea
                    if (worldObject.GetGroup().GetId() != "Algae1Seed")
                    {
                        Dbgl($"{worldObject.GetGroup().GetId()} is Grabbable");                        
                        Dbgl($"{m.GetInstanceID()}: {worldObject.GetGroup().GetId()}? {worldObject.GetGameObject().transform.localScale.x}");

                        //pick it up if backpack not full
                        if (!player.GetPlayerBackpack().GetInventory().IsFull()) // && worldObject.GetGroup().GetId() != "Algae1Seed")
                        {                            
                            MethodInfo dynMethod = typeof(ActionGrabable).GetMethod("Grab", BindingFlags.NonPublic | BindingFlags.Instance);                         
                            dynMethod.Invoke(m, new object[] { });                            
                            //informationsDisplayer.AddInformation(2f, Readable.GetGroupName(worldObject.GetGroup()), DataConfig.UiInformationsType.InInventory, worldObject.GetGroup().GetImage());
                            //Managers.GetManager<DisplayersHandler>().GetItemWorldDislpayer().Hide();                            
                            count++;
                        }
                    }
                } else if (player.GetPlayerBackpack().GetInventory().AddItem(worldObject))
                {
                    //minables / pickup-a-bles
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
                CheckForNearbyMachineOutsideGrower(maxRangeGrab);
                CheckForNearbyType<ActionGrabable>(maxRangeGrab, "Grabed");
                return;
            }
            if (intervalCheckGrab.Value) 
            { 
                elapsedGrab += Time.deltaTime;
                if (elapsedGrab > checkIntervalGrab.Value)
                {
                    elapsedGrab = 0;
                    CheckForNearbyMachineOutsideGrower(maxRangeGrab);
                    CheckForNearbyType<ActionGrabable>(maxRangeGrab, "Grabed");                    
                }
            }
#if DEBUG
            if (actionGetMOGs.WasPressedThisFrame())
            {
                Dbgl($"Pressed manual check key for non-pickup grabbale");
                elapsedGrab = 0;
                CheckForNearbyMachineOutsideGrower(maxRangeGrab, pickup: false);                
                return;
            }
#endif
        }

        private void Update()
        {
            if (loadCompleted) //if run too fast, will cause grab errors
            {
                UpdateMinable();
                UpdateGrabable();
            }            
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        static void SessionController_Start()
        {
            loadCompleted = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            loadCompleted = false;
        }
    }
}