﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers.MockSystem;
using Jotunn.Utils;
using UnityEngine;
using Object = UnityEngine.Object;
using ZoneLocation = ZoneSystem.ZoneLocation;

namespace Jotunn.Managers
{
    /// <summary>
    ///     Manager for adding custom Locations, Vegetation and Clutter.
    /// </summary>
    public class ZoneManager : IManager
    {
        private static ZoneManager _instance;

        /// <summary>
        ///     The singleton instance of this manager.
        /// </summary>
        public static ZoneManager Instance => _instance ??= new ZoneManager();

        /// <summary>
        ///     Hide .ctor
        /// </summary>
        private ZoneManager() { }

        static ZoneManager()
        {
            ((IManager)Instance).Init();
        }

        /// <summary>
        ///     Event that gets fired after the vanilla locations are in memory and available for cloning or editing.
        ///     Your code will execute every time a new <see cref="ZoneSystem"/> is available.
        ///     If you want to execute just once you will need to unregister from the event after execution.
        /// </summary>
        public static event Action OnVanillaLocationsAvailable;

        /// <summary>
        ///     Event that gets fired after all <see cref="CustomLocation"/> are registered in the <see cref="ZoneSystem"/>.
        ///     Your code will execute every time a new <see cref="ZoneSystem"/> is available.
        ///     If you want to execute just once you will need to unregister from the event after execution.
        /// </summary>
        public static event Action OnLocationsRegistered;

        /// <summary>
        ///     Event that gets fired after the vanilla clutter is in memory and available obtain.
        ///     Your code will execute every time a new <see cref="ClutterSystem"/> is available.
        ///     If you want to execute just once you will need to unregister from the event after execution.
        /// </summary>
        public static event Action OnVanillaClutterAvailable;

        /// <summary>
        ///     Event that gets fired after all <see cref="CustomClutter"/> are registered in the <see cref="ClutterSystem"/>.
        ///     Your code will execute every time a new <see cref="ClutterSystem"/> is available.
        ///     If you want to execute just once you will need to unregister from the event after execution.
        /// </summary>
        public static event Action OnClutterRegistered;

        /// <summary>
        ///     Event that gets fired after the vanilla vegetation is in memory and available obtain.
        ///     Your code will execute every time a new <see cref="ZoneSystem"/> is available.
        ///     If you want to execute just once you will need to unregister from the event after execution.
        /// </summary>
        public static event Action OnVanillaVegetationAvailable;

        /// <summary>
        ///     Event that gets fired after all <see cref="CustomVegetation"/> are registered in the <see cref="ZoneSystem"/>.
        ///     Your code will execute every time a new <see cref="ZoneSystem"/> is available.
        ///     If you want to execute just once you will need to unregister from the event after execution.
        /// </summary>
        public static event Action OnVegetationRegistered;

        /// <summary>
        ///     Container for custom locations in the DontDestroyOnLoad scene.
        /// </summary>
        internal GameObject LocationContainer;

        internal Dictionary<string, CustomLocation> Locations { get; } = new Dictionary<string, CustomLocation>();
        internal Dictionary<string, CustomVegetation> Vegetations { get; } = new Dictionary<string, CustomVegetation>();
        internal Dictionary<string, CustomClutter> Clutter { get; } = new Dictionary<string, CustomClutter>();

        /// <summary>
        ///     Initialize the manager
        /// </summary>
        void IManager.Init()
        {
            Main.LogInit("ZoneManager");

            LocationContainer = new GameObject("Locations");
            LocationContainer.transform.parent = Main.RootObject.transform;
            LocationContainer.SetActive(false);

            Main.Harmony.PatchAll(typeof(Patches));
            PrefabManager.Instance.Activate();
        }

        private static class Patches
        {
            [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.SetupLocations)), HarmonyPostfix]
            private static void ZoneSystem_SetupLocations(ZoneSystem __instance)
            {
                Instance.RegisterLocations(__instance);
                Instance.RegisterVegetation(__instance);
            }

            [HarmonyPatch(typeof(ClutterSystem), nameof(ClutterSystem.Awake)), HarmonyPostfix]
            private static void ClutterSystem_Awake(ClutterSystem __instance) => Instance.ClutterSystem_Awake(__instance);
        }

        /// <summary>
        ///     Return a <see cref="Heightmap.Biome"/> that matches any of the provided Biomes
        /// </summary>
        /// <param name="biomes">Biomes that should match</param> 
        public static Heightmap.Biome AnyBiomeOf(params Heightmap.Biome[] biomes)
        {
            Heightmap.Biome result = Heightmap.Biome.None;
            foreach (var biome in biomes)
            {
                result |= biome;
            }

            return result;
        }

        /// <summary>
        ///     Returns a list of all <see cref="Heightmap.Biome"/> that match <paramref name="biome"/>
        /// </summary>
        /// <param name="biome"></param>
        /// <returns></returns>
        public static List<Heightmap.Biome> GetMatchingBiomes(Heightmap.Biome biome)
        {
            List<Heightmap.Biome> biomes = new List<Heightmap.Biome>();
            foreach (Heightmap.Biome area in Enum.GetValues(typeof(Heightmap.Biome)))
            {
                if ((biome & area) == 0)
                {
                    continue;
                }

                biomes.Add(area);
            }

            return biomes;
        }

        /// <summary>
        ///     Create an empty GameObject that is disabled, so any Components in instantiated GameObjects will not start their lifecycle.
        /// </summary>
        /// <param name="name">Name of the location</param>
        /// <returns>Empty and hierarchy disabled GameObject</returns>
        public GameObject CreateLocationContainer(string name)
        {
            GameObject container = new GameObject
            {
                name = name
            };
            container.transform.SetParent(LocationContainer.transform);
            return container;
        }

        /// <summary>
        ///     Create a copy that is disabled, so any Components in instantiated child GameObjects will not start their lifecycle.<br />
        ///     Use this if you plan to alter your location prefab in code after importing it. <br />
        ///     Don't create a separate container if you won't alter the prefab afterwards as it creates a new instance for the container.
        /// </summary>
        /// <param name="gameObject">Instantiated and hierarchy disabled location prefab</param>
        public GameObject CreateLocationContainer(GameObject gameObject)
        {
            var container = Object.Instantiate(gameObject, LocationContainer.transform);
            container.name = gameObject.name;
            return container;
        }

        /// <summary>
        ///     Loads and spawns a GameObject from an AssetBundle as a location container.<br />
        ///     The copy is disabled, so any Components in instantiated child GameObjects will not start their lifecycle.<br />
        ///     Use this if you plan to alter your location prefab in code after importing it. <br />
        ///     Don't create a separate container if you won't alter the prefab afterwards as it creates a new instance for the container.
        /// </summary>
        /// <param name="assetBundle">A preloaded <see cref="AssetBundle"/></param>
        /// <param name="assetName">Name of the prefab in the bundle to be instantiated as the location cotainer</param>
        public GameObject CreateLocationContainer(AssetBundle assetBundle, string assetName)
        {
            var sourceMod = BepInExUtils.GetPluginInfoFromAssembly(Assembly.GetCallingAssembly())?.Metadata;

            if (sourceMod == null || sourceMod.GUID == Main.Instance.Info.Metadata.GUID)
            {
                sourceMod = BepInExUtils.GetSourceModMetadata();
            }

            if (!AssetUtils.TryLoadPrefab(sourceMod, assetBundle, assetName, out GameObject prefab))
            {
                Logger.LogError(sourceMod, $"Failed to create location container for '{assetName}'");
                return null;
            }

            return CreateLocationContainer(prefab);
        }

        /// <summary>
        ///     Create a copy that is disabled, so any Components in instantiated GameObjects will not start their lifecycle     
        /// </summary>
        /// <param name="gameObject">Prefab to copy</param>
        /// <param name="fixLocationReferences">Replace JVLmock GameObjects with a copy of their real prefab</param>
        /// <returns></returns>
        [Obsolete("Use CreateLocationContainer(GameObject) instead and define if references should be fixed in CustomLocation")]
        public GameObject CreateLocationContainer(GameObject gameObject, bool fixLocationReferences = false)
        {
            var locationContainer = Object.Instantiate(gameObject, LocationContainer.transform);
            locationContainer.name = gameObject.name;
            if (fixLocationReferences)
            {
                locationContainer.FixReferences(true);
            }

            return locationContainer;
        }

        /// <summary>
        ///     Register a CustomLocation to be added to the ZoneSystem
        /// </summary>
        /// <param name="customLocation"></param>
        /// <returns>true if the custom location could be added to the manager</returns>
        public bool AddCustomLocation(CustomLocation customLocation)
        {
            if (Locations.ContainsKey(customLocation.Name))
            {
                Logger.LogWarning(customLocation.SourceMod, $"Location {customLocation.Name} already exists");
                return false;
            }

            customLocation.Prefab.transform.SetParent(LocationContainer.transform);

            // The root prefab needs to be active, otherwise ZNetViews are not prepared correctly
            customLocation.Prefab.SetActive(true);

            Locations.Add(customLocation.Name, customLocation);
            return true;
        }

        /// <summary>
        ///     Get a custom location by name.
        /// </summary>
        /// <param name="name">Name of the location (normally the prefab name)</param>
        /// <returns>The <see cref="CustomLocation"/> object with the given name if found</returns>
        public CustomLocation GetCustomLocation(string name)
        {
            return Locations[name];
        }
        
        /// <summary>
        ///     Get a ZoneLocation by its name.<br /><br />
        ///     Search hierarchy:
        ///     <list type="number">
        ///         <item>Custom Location with the exact name</item>
        ///         <item>Vanilla Location with the exact name from <see cref="ZoneSystem"/></item>
        ///     </list>
        /// </summary>
        /// <param name="name">Name of the ZoneLocation to search for.</param>
        /// <returns>The existing ZoneLocation, or null if none exists with given name</returns>
        public ZoneLocation GetZoneLocation(string name)
        {
            if (Locations.TryGetValue(name, out CustomLocation customLocation))
            {
                return customLocation.ZoneLocation;
            }

            int hash = name.GetStableHashCode();

            if (ZoneSystem.instance && ZoneSystem.instance.m_locationsByHash.TryGetValue(hash, out ZoneLocation location))
            {
                return location;
            }

            return null;
        }
        
        /// <summary>
        ///     Create a CustomLocation that is a deep copy of the original.<br />
        ///     Changes will not affect the original. The CustomLocation is already registered in the manager.
        /// </summary>
        /// <param name="name">name of the custom location</param>
        /// <param name="baseName">name of the existing location to copy</param>
        /// <returns>A CustomLocation object with the cloned location prefab</returns>
        public CustomLocation CreateClonedLocation(string name, string baseName)
        {
            var baseZoneLocation = GetZoneLocation(baseName);
            baseZoneLocation.m_prefab.Load();

            var copiedPrefab = AssetManager.Instance.ClonePrefab(baseZoneLocation.m_prefab.Asset, name, LocationContainer.transform);
            var clonedLocation = new CustomLocation(copiedPrefab, false, new LocationConfig(baseZoneLocation));
            AddCustomLocation(clonedLocation);

            baseZoneLocation.m_prefab.Release();
            return clonedLocation;
        }
        
        /// <summary>
        ///     Remove a CustomLocation by its name.<br />
        ///     Removes the CustomLocation from the manager.
        ///     Does not remove the location from any current ZoneSystem instance.
        /// </summary>
        /// <param name="name">Name of the CustomLocation to search for.</param>
        public bool RemoveCustomLocation(string name)
        {
            return Locations.Remove(name);
        }
        
        /// <summary>
        ///     Destroy a CustomLocation by its name.<br />
        ///     Removes the CustomLocation from the manager and from the <see cref="ZoneSystem"/> if instantiated.
        /// </summary>
        /// <param name="name">Name of the CustomLocation to search for.</param>
        public bool DestroyCustomLocation(string name)
        {
            if (!Locations.TryGetValue(name, out CustomLocation customLocation))
            {
                return false;
            }

            int hash = name.GetStableHashCode();

            if (ZoneSystem.instance && ZoneSystem.instance.m_locationsByHash.TryGetValue(hash, out ZoneLocation location))
            {
                ZoneSystem.instance.m_locationsByHash.Remove(hash);
                ZoneSystem.instance.m_locations.Remove(location);
            }

            if (customLocation.Prefab)
            {
                Object.Destroy(customLocation.Prefab);
            }

            return Locations.Remove(name);
        }

        /// <summary>
        ///     Register a CustomVegetation to be added to the ZoneSystem
        /// </summary>
        /// <param name="customVegetation"></param>
        /// <returns></returns>
        public bool AddCustomVegetation(CustomVegetation customVegetation)
        {
            if (!customVegetation.IsValid())
            {
                return false;
            }

            if (!PrefabManager.Instance.AddPrefab(customVegetation.Prefab, customVegetation.SourceMod))
            {
                return false;
            }

            Vegetations.Add(customVegetation.Name, customVegetation);
            return true;
        }

        /// <summary>
        ///     Get a ZoneVegetation by its name.<br /><br />
        ///     Search hierarchy:
        ///     <list type="number">
        ///         <item>Custom Vegetation with the exact name</item>
        ///         <item>Vanilla Vegetation with the exact name from <see cref="ZoneSystem"/></item>
        ///     </list>
        /// </summary>
        /// <param name="name">Name of the ZoneVegetation to search for.</param>
        /// <returns>The existing ZoneVegetation, or null if none exists with given name</returns>
        public ZoneSystem.ZoneVegetation GetZoneVegetation(string name)
        {
            if (Vegetations.TryGetValue(name, out CustomVegetation customVegetation))
            {
                return customVegetation.Vegetation;
            }

            return ZoneSystem.instance.m_vegetation
                .DefaultIfEmpty(null)
                .FirstOrDefault(zv => zv.m_prefab && zv.m_prefab.name == name);
        }
        
        /// <summary>
        ///     Remove a CustomVegetation from this manager by its name.<br />
        ///     Does not remove it from any current ZoneSystem instance.
        /// </summary>
        /// <param name="name">Name of the CustomVegetation to search for.</param>
        public bool RemoveCustomVegetation(string name)
        {
            return Vegetations.Remove(name);
        }

        /// <summary>
        ///     Register a CustomClutter to be added to the ClutterSystem
        /// </summary>
        /// <param name="customClutter"></param>
        /// <returns></returns>
        public bool AddCustomClutter(CustomClutter customClutter)
        {
            if (!customClutter.IsValid())
            {
                Logger.LogWarning(customClutter.SourceMod, $"Custom clutter '{customClutter}' is not valid");
                return false;
            }

            if (Clutter.ContainsKey(customClutter.Name))
            {
                return false;
            }

            Clutter.Add(customClutter.Name, customClutter);
            return true;
        }

        /// <summary>
        ///     Get a Clutter by its name.<br /><br />
        ///     Search hierarchy:
        ///     <list type="number">
        ///         <item>Custom Clutter with the exact name</item>
        ///         <item>Vanilla Clutter with the exact name from <see cref="ClutterSystem"/></item>
        ///     </list>
        /// </summary>
        /// <param name="name">Name of the Clutter to search for.</param>
        /// <returns>The existing Clutter, or null if none exists with given name</returns>
        public ClutterSystem.Clutter GetClutter(string name)
        {
            if (Clutter.TryGetValue(name, out CustomClutter customClutter))
            {
                return customClutter.Clutter;
            }

            if (!ClutterSystem.instance)
            {
                return null;
            }

            return ClutterSystem.instance.m_clutter
                .DefaultIfEmpty(null)
                .FirstOrDefault(zv => zv?.m_name == name);
        }
        
        /// <summary>
        ///     Remove a CustomClutter from this manager by its name.<br />
        ///     Does not remove it from any current ClutterSystem instance.
        /// </summary>
        /// <param name="name">Name of the CustomClutter to search for.</param>
        public bool RemoveCustomClutter(string name)
        {
            return Clutter.Remove(name);
        }

        private void ClutterSystem_Awake(ClutterSystem instance)
        {
            InvokeOnVanillaClutterAvailable();

            if (Clutter.Count > 0)
            {
                Logger.LogInfo($"Injecting {Clutter.Count} custom clutter");
                List<string> toDelete = new List<string>();

                foreach (var customClutter in Clutter.Values)
                {
                    try
                    {
                        // Fix references if needed
                        if (customClutter.FixReference)
                        {
                            customClutter.Prefab.FixReferences(true);
                            customClutter.FixReference = false;
                        }

                        instance.m_clutter.Add(customClutter.Clutter);
                    }
                    catch (MockResolveException ex)
                    {
                        Logger.LogWarning(customClutter?.SourceMod, $"Skipping clutter {customClutter}: {ex.Message}");
                        toDelete.Add(customClutter.Name);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(customClutter?.SourceMod, $"Exception caught while adding clutter: {ex}");
                        toDelete.Add(customClutter.Name);
                    }
                }

                foreach (var name in toDelete)
                {
                    Clutter.Remove(name);
                }
            }

            InvokeOnClutterRegistered();
        }

        private void RegisterLocations(ZoneSystem self)
        {
            InvokeOnVanillaLocationsAvailable();

            if (Locations.Count > 0)
            {
                List<string> toDelete = new List<string>();

                Logger.LogInfo($"Injecting {Locations.Count} custom locations");
                foreach (CustomLocation customLocation in Locations.Values)
                {
                    try
                    {
                        Logger.LogDebug(
                            $"Adding custom location {customLocation} in {string.Join(", ", GetMatchingBiomes(customLocation.ZoneLocation.m_biome))}");

                        // Fix references if needed
                        if (customLocation.FixReference)
                        {
                            customLocation.Prefab.FixReferences(true);
                            customLocation.FixReference = false;
                        }

                        var zoneLocation = customLocation.ZoneLocation;

                        RegisterLocationInZoneSystem(self, zoneLocation, customLocation.SourceMod);
                    }
                    catch (MockResolveException ex)
                    {
                        Logger.LogWarning(customLocation?.SourceMod, $"Skipping location {customLocation}: {ex.Message}");
                        toDelete.Add(customLocation.Name);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(customLocation?.SourceMod, $"Exception caught while adding location: {ex}");
                        toDelete.Add(customLocation.Name);
                    }
                }

                foreach (var name in toDelete)
                {
                    Locations.Remove(name);
                }
            }

            InvokeOnLocationsRegistered();
        }

        private void RegisterVegetation(ZoneSystem self)
        {
            InvokeOnVanillaVegetationAvailable();

            if (Vegetations.Count > 0)
            {
                List<string> toDelete = new List<string>();

                Logger.LogInfo($"Injecting {Vegetations.Count} custom vegetation");
                foreach (CustomVegetation customVegetation in Vegetations.Values)
                {
                    try
                    {
                        Logger.LogDebug(
                            $"Adding custom vegetation {customVegetation} in {string.Join(", ", GetMatchingBiomes(customVegetation.Vegetation.m_biome))}");

                        // Fix references if needed
                        if (customVegetation.FixReference)
                        {
                            customVegetation.Prefab.FixReferences(true);
                            customVegetation.FixReference = false;
                        }

                        self.m_vegetation.Add(customVegetation.Vegetation);
                    }
                    catch (MockResolveException ex)
                    {
                        Logger.LogWarning(customVegetation?.SourceMod, $"Skipping vegetation {customVegetation}: {ex.Message}");
                        toDelete.Add(customVegetation.Name);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(customVegetation?.SourceMod, $"Exception caught while adding vegetation: {ex}");
                        toDelete.Add(customVegetation.Name);
                    }
                }

                foreach (var name in toDelete)
                {
                    Vegetations.Remove(name);
                }
            }

            InvokeOnVegetationRegistered();
        }

        /// <summary>
        ///     Register a single ZoneLocaton in the current ZoneSystem.
        ///     Also adds the location prefabs to the <see cref="PrefabManager"/> and <see cref="ZNetScene"/> if necessary.<br />
        ///     No mock references are fixed.
        /// </summary>
        /// <param name="zoneLocation"><see cref="ZoneLocation"/> to add to the <see cref="ZoneSystem"/></param>
        public void RegisterLocationInZoneSystem(ZoneLocation zoneLocation) =>
            RegisterLocationInZoneSystem(ZoneSystem.instance, zoneLocation, BepInExUtils.GetSourceModMetadata());

        /// <summary>
        ///     Internal method for adding a ZoneLocation to a specific ZoneSystem.
        /// </summary>
        /// <param name="zoneSystem"><see cref="ZoneSystem"/> the location should be added to</param>
        /// <param name="zoneLocation"><see cref="ZoneLocation"/> to add</param>
        /// <param name="sourceMod"><see cref="BepInPlugin"/> which created the location</param>
        private void RegisterLocationInZoneSystem(ZoneSystem zoneSystem, ZoneLocation zoneLocation, BepInPlugin sourceMod)
        {
            zoneLocation.m_prefab.Load();

            foreach (var znet in global::Utils.GetEnabledComponentsInChildren<ZNetView>(zoneLocation.m_prefab.Asset))
            {
                string prefabName = znet.GetPrefabName();
                if (!ZNetScene.instance.m_namedPrefabs.ContainsKey(prefabName.GetStableHashCode()))
                {
                    var prefab = Object.Instantiate(znet.gameObject, PrefabManager.Instance.PrefabContainer.transform);
                    prefab.name = prefabName;
                    CustomPrefab customPrefab = new CustomPrefab(prefab, sourceMod);
                    PrefabManager.Instance.AddPrefab(customPrefab);
                    PrefabManager.Instance.RegisterToZNetScene(customPrefab.Prefab);
                }
            }

            RandomSpawn[] randomSpawns = global::Utils.GetEnabledComponentsInChildren<RandomSpawn>(zoneLocation.m_prefab.Asset);
            foreach (var randomSpawn in randomSpawns)
            {
                randomSpawn.Prepare();
            }

            foreach (var znet in randomSpawns.SelectMany(x => x.m_childNetViews))
            {
                string prefabName = znet.GetPrefabName();
                if (!ZNetScene.instance.m_namedPrefabs.ContainsKey(prefabName.GetStableHashCode()))
                {
                    var prefab = Object.Instantiate(znet.gameObject, PrefabManager.Instance.PrefabContainer.transform);
                    prefab.name = prefabName;
                    CustomPrefab customPrefab = new CustomPrefab(prefab, sourceMod);
                    PrefabManager.Instance.AddPrefab(customPrefab);
                    PrefabManager.Instance.RegisterToZNetScene(customPrefab.Prefab);
                }
            }

            if (!zoneSystem.m_locationsByHash.ContainsKey(zoneLocation.Hash))
            {
                zoneSystem.m_locationsByHash.Add(zoneLocation.Hash, zoneLocation);
                zoneSystem.m_locations.Add(zoneLocation);
            }

            zoneLocation.m_prefab.Release();
        }

        private static void InvokeOnVanillaLocationsAvailable() => OnVanillaLocationsAvailable?.SafeInvoke();
        private static void InvokeOnLocationsRegistered() => OnLocationsRegistered?.SafeInvoke();
        private static void InvokeOnVanillaVegetationAvailable() => OnVanillaVegetationAvailable?.SafeInvoke();
        private static void InvokeOnVegetationRegistered() => OnVegetationRegistered?.SafeInvoke();
        private static void InvokeOnVanillaClutterAvailable() => OnVanillaClutterAvailable?.SafeInvoke();
        private static void InvokeOnClutterRegistered() => OnClutterRegistered?.SafeInvoke();
    }
}
