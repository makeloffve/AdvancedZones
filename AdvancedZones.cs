﻿using Rocket.API;
using Rocket.Core;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Enumerations;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace Game4Freak.AdvancedZones
{
    public class AdvancedZones : RocketPlugin<AdvancedZonesConfiguration>
    {
        public static AdvancedZones Instance;
        public const string VERSION = "0.7.0.0";
        public string newVersion = null;
        private int frame = 10;
        private Dictionary<string, Vector3> lastPosition;
        private bool notifyUpdate = false;
        // Events
        public delegate void onZoneLeaveHandler(UnturnedPlayer player, Zone zone, Vector3 lastPos);
        public static onZoneLeaveHandler onZoneLeave;

        public delegate void onZoneEnterHandler(UnturnedPlayer player, Zone zone, Vector3 lastPos);
        public static onZoneEnterHandler onZoneEnter;
       
        /**
         * TODO:
         * Translations
         * 
         * NOTES:
         * "IGNORE" in Buildables List --> all other listed ids - ignored ids
         * "ALL" in Buildables List --> all barricades + all structures
         * permissions with zonenames for custom override (eg. advancedzones.override.build.testZone for building in the zone testZone)
         * DEBUG: UnturnedChat.Say("");
         * 
         * IMPORTANT:
         * check on version > 0.7.0.0 if prior version was 0.7.0.0 or later for right xml upgrade
         * ctrl + f and search for IMPORTANT
         **/

        protected override void Load()
        {
            Instance = this;
            Logger.Log("AdvancedZones v" + VERSION);

            WebClient client = new WebClient();
            try
            {
                newVersion = client.DownloadString("http://pastebin.com/raw/CnLNQehG");
            }
            catch (WebException e)
            {
                Logger.Log("No connection to version-check");
            }
            if (newVersion != null)
            {
                if (VERSION != newVersion)
                {
                    Logger.Log("A new AdvancedZones version (" + newVersion + ") is available !!!");
                    notifyUpdate = true;
                }
            }
            // Update config
            if (Configuration.Instance.version != VERSION)
            {
                updateConfig();
                Configuration.Instance.version = VERSION;
                Configuration.Save();
            }

            lastPosition = new Dictionary<string, Vector3>();
            foreach (var splayer in Provider.clients)
            {
                onPlayerConnection(UnturnedPlayer.FromSteamPlayer(splayer));
            }

            // Init
            onZoneLeave += onZoneLeft;
            onZoneEnter += onZoneEntered;
            // Enter / Leave
            U.Events.OnPlayerConnected += onPlayerConnection;
            U.Events.OnPlayerDisconnected += onPlayerDisconnection;
            // Block Damage
            BarricadeManager.onDamageBarricadeRequested += onBarricadeDamage;
            StructureManager.onDamageStructureRequested += onStructureDamage;
            VehicleManager.onVehicleLockpicked += onVehicleLockpick;
            VehicleManager.onDamageVehicleRequested += onVehicleDamage;
            DamageTool.playerDamaged += onPlayerDamage;
            VehicleManager.onDamageTireRequested += onTireDamage;
            // Block Buildable
            BarricadeManager.onDeployBarricadeRequested += onBarricadeDeploy;
            StructureManager.onDeployStructureRequested += onStructureDepoly;
        }

        protected override void Unload()
        {
            lastPosition.Clear();

            // Enter / Leave
            U.Events.OnPlayerConnected -= onPlayerConnection;
            U.Events.OnPlayerDisconnected -= onPlayerDisconnection;
            // Block Damage
            BarricadeManager.onDamageBarricadeRequested -= onBarricadeDamage;
            StructureManager.onDamageStructureRequested -= onStructureDamage;
            VehicleManager.onVehicleLockpicked -= onVehicleLockpick;
            VehicleManager.onDamageVehicleRequested -= onVehicleDamage;
            DamageTool.playerDamaged -= onPlayerDamage;
            VehicleManager.onDamageTireRequested -= onTireDamage;
            // Block Buildable
            BarricadeManager.onDeployBarricadeRequested -= onBarricadeDeploy;
            StructureManager.onDeployStructureRequested -= onStructureDepoly;
        }

        private void updateConfig()
        {
            // ALWAYS UPDATES WHEN NEW THING ARE ADDED TO THE CONFIG
            Logger.Log("Updating plugin config");

            if (Configuration.Instance.ZoneNodes.Count < Configuration.Instance.ZoneNames.Count)
            {
                for (int i = 0; i < (Configuration.Instance.ZoneNames.Count - Configuration.Instance.ZoneNodes.Count); i++)
                {
                    Configuration.Instance.ZoneNodes.Add(new List<float[]>());
                }
            }
            if (Configuration.Instance.ZoneFlags.Count < Configuration.Instance.ZoneNames.Count)
            {
                for (int i = 0; i < (Configuration.Instance.ZoneNames.Count - Configuration.Instance.ZoneFlags.Count); i++)
                {
                    Configuration.Instance.ZoneFlags.Add(new List<int>());
                }
            }
            if (Configuration.Instance.ZoneBlockedBuildables.Count < Configuration.Instance.ZoneNames.Count)
            {
                for (int i = 0; i < (Configuration.Instance.ZoneNames.Count - Configuration.Instance.ZoneBlockedBuildables.Count); i++)
                {
                    Configuration.Instance.ZoneBlockedBuildables.Add(new List<string>());
                }
            }
            if (Configuration.Instance.ZoneBlockedEquip.Count < Configuration.Instance.ZoneNames.Count)
            {
                for (int i = 0; i < (Configuration.Instance.ZoneNames.Count - Configuration.Instance.ZoneBlockedEquip.Count); i++)
                {
                    Configuration.Instance.ZoneBlockedEquip.Add(new List<string>());
                }
            }
            // Added in 0.5.0.0
            if (Configuration.Instance.ZoneEnterAddGroups.Count < Configuration.Instance.ZoneNames.Count)
            {
                for (int i = 0; i < (Configuration.Instance.ZoneNames.Count - Configuration.Instance.ZoneEnterAddGroups.Count); i++)
                {
                    Configuration.Instance.ZoneEnterAddGroups.Add(new List<string>());
                }
            }
            if (Configuration.Instance.ZoneEnterRemoveGroups.Count < Configuration.Instance.ZoneNames.Count)
            {
                for (int i = 0; i < (Configuration.Instance.ZoneNames.Count - Configuration.Instance.ZoneEnterRemoveGroups.Count); i++)
                {
                    Configuration.Instance.ZoneEnterRemoveGroups.Add(new List<string>());
                }
            }
            if (Configuration.Instance.ZoneLeaveAddGroups.Count < Configuration.Instance.ZoneNames.Count)
            {
                for (int i = 0; i < (Configuration.Instance.ZoneNames.Count - Configuration.Instance.ZoneLeaveAddGroups.Count); i++)
                {
                    Configuration.Instance.ZoneLeaveAddGroups.Add(new List<string>());
                }
            }
            if (Configuration.Instance.ZoneLeaveRemoveGroups.Count < Configuration.Instance.ZoneNames.Count)
            {
                for (int i = 0; i < (Configuration.Instance.ZoneNames.Count - Configuration.Instance.ZoneLeaveRemoveGroups.Count); i++)
                {
                    Configuration.Instance.ZoneLeaveRemoveGroups.Add(new List<string>());
                }
            }
            // Added in 0.6.2.0
            if (Configuration.Instance.ZoneEnterMessages.Count < Configuration.Instance.ZoneNames.Count)
            {
                for (int i = 0; i < (Configuration.Instance.ZoneNames.Count - Configuration.Instance.ZoneEnterMessages.Count); i++)
                {
                    Configuration.Instance.ZoneEnterMessages.Add(new List<string>());
                }
            }
            if (Configuration.Instance.ZoneLeaveMessages.Count < Configuration.Instance.ZoneNames.Count)
            {
                for (int i = 0; i < (Configuration.Instance.ZoneNames.Count - Configuration.Instance.ZoneLeaveMessages.Count); i++)
                {
                    Configuration.Instance.ZoneLeaveMessages.Add(new List<string>());
                }
            }
            // add default message to zone
            int y = 0;
            foreach (var eML in Configuration.Instance.ZoneEnterMessages)
            {
                if (eML.Count == 0 && Configuration.Instance.ZoneFlags.ElementAt(y).Contains(Zone.enterMessage))
                {
                    eML.Add("Now entering the zone: " + Configuration.Instance.ZoneNames.ElementAt(y));
                }
                y++;
            }
            y = 0;
            foreach (var lML in Configuration.Instance.ZoneLeaveMessages)
            {
                if (lML.Count == 0 && Configuration.Instance.ZoneFlags.ElementAt(y).Contains(Zone.leaveMessage))
                {
                    lML.Add("Now leaving the zone: " + Configuration.Instance.ZoneNames.ElementAt(y));
                }
                y++;
            }
            Configuration.Save();
            // Convert config to new config style IMPORTANT: remove upper part and clearing lists for the next update
            if (Configuration.Instance.version != VERSION)
            {
                Logger.Log("Converting old Xml layout into the new one");

                for (int x = 0; x < Configuration.Instance.BlockedBuildablesListNames.Count; x++)
                {
                    Configuration.Instance.BuildBlocklists.Add(new BuildBlocklist(Configuration.Instance.BlockedBuildablesListNames.ElementAt(x)));
                    foreach (var itemID in Configuration.Instance.BlockedBuildables.ElementAt(x))
                    {
                        Configuration.Instance.BuildBlocklists.ElementAt(x).addItem(itemID);
                    }
                }

                for (int x = 0; x < Configuration.Instance.BlockedEquipListNames.Count; x++)
                {
                    Configuration.Instance.EquipBlocklists.Add(new EquipBlocklist(Configuration.Instance.BlockedEquipListNames.ElementAt(x)));
                    foreach (var itemID in Configuration.Instance.BlockedEquip.ElementAt(x))
                    {
                        Configuration.Instance.EquipBlocklists.ElementAt(x).addItem(itemID);
                    }
                }

                Configuration.Save();

                for (int x = 0; x < Configuration.Instance.ZoneNames.Count; x++)
                {
                    Zone temp = new Zone(Configuration.Instance.ZoneNames.ElementAt(x));
                    foreach (var n in Configuration.Instance.ZoneNodes.ElementAt(x))
                    {
                        temp.addNode(new Node(n[0], n[1], n[2]));
                    }
                    foreach (var f in Configuration.Instance.ZoneFlags.ElementAt(x))
                    {
                        temp.addFlag(Zone.flagTypes[f]);
                    }
                    foreach (var bE in Configuration.Instance.ZoneBlockedEquip.ElementAt(x))
                    {
                        temp.addEquipBlocklist(bE);
                    }
                    foreach (var bB in Configuration.Instance.ZoneBlockedBuildables.ElementAt(x))
                    {
                        temp.addBuildBlocklist(bB);
                    }
                    foreach (var eAG in Configuration.Instance.ZoneEnterAddGroups.ElementAt(x))
                    {
                        temp.addEnterAddGroup(eAG);
                    }
                    foreach (var eRG in Configuration.Instance.ZoneEnterRemoveGroups.ElementAt(x))
                    {
                        temp.addEnterRemoveGroup(eRG);
                    }
                    foreach (var lAG in Configuration.Instance.ZoneLeaveAddGroups.ElementAt(x))
                    {
                        temp.addLeaveAddGroup(lAG);
                    }
                    foreach (var lRG in Configuration.Instance.ZoneLeaveRemoveGroups.ElementAt(x))
                    {
                        temp.addLeaveRemoveGroup(lRG);
                    }
                    foreach (var eM in Configuration.Instance.ZoneEnterMessages.ElementAt(x))
                    {
                        temp.addEnterMessage(eM);
                    }
                    foreach (var lM in Configuration.Instance.ZoneLeaveMessages.ElementAt(x))
                    {
                        temp.addLeaveMessage(lM);
                    }
                    Configuration.Instance.Zones.Add(temp);
                    x++;
                }
            }

            Logger.Log("Updating done");
        }

        private void Update()
        {
            // TODO: set with command
            frame++;
            if (frame % 10 != 0) return;

            foreach (var splayer in Provider.clients)
            {
                Vector3 lastPos;
                UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(splayer);
                // Enter / Leave region
                if (!lastPosition.ContainsKey(player.Id))
                {
                    onPlayerConnection(player);
                }
                else
                {
                    lastPosition.TryGetValue(player.Id, out lastPos);
                    if (!lastPos.Equals(player.Position))
                    {
                        List<string> lastZoneNames = new List<string>();
                        foreach (var zone in getPositionZones(lastPos))
                        {
                            lastZoneNames.Add(zone.getName());
                        }
                        List<string> currentZoneNames = new List<string>();
                        foreach (var zone in getPositionZones(player.Position))
                        {
                            currentZoneNames.Add(zone.getName());
                        }
                        foreach (var zoneName in lastZoneNames.Except(currentZoneNames))
                        {
                            // Leaving
                            onZoneLeave(player, getZoneByName(zoneName), lastPos);
                        }
                        foreach (var zoneName in currentZoneNames.Except(lastZoneNames))
                        {
                            // Entering
                            onZoneEnter(player, getZoneByName(zoneName), lastPos);
                        }
                    }
                    lastPosition[player.Id] = player.Position;
                }

                // Player Equip
                if (player.Player.equipment.isSelected && playerInZoneType(player, Zone.flagTypes[Zone.noItemEquip]))
                {
                    onPlayerEquiped(player.Player, player.Player.equipment);
                }
            }

            // infiniteGenerator flag
            InteractableGenerator[] generators = FindObjectsOfType<InteractableGenerator>();
            foreach (var generator in generators)
            {
                if (transformInZoneType(generator.transform, Zone.flagTypes[Zone.infiniteGenerator]))
                {
                    if (generator.fuel < generator.capacity - 10)
                    {
                        BarricadeManager.sendFuel(generator.transform, generator.capacity);
                    }
                }
            }

            // noZombie flag
            if (ZombieManager.regions != null)
            {
                foreach (ZombieRegion t in ZombieManager.regions.Where(t => t.zombies != null))
                {
                    foreach (var zombie in t.zombies.Where(z => z != null && z.transform?.position != null))
                    {
                        if (zombie.isDead) continue;
                        if (!transformInZoneType(zombie.transform, Zone.flagTypes[Zone.noZombie])) continue;
                        zombie.gear = 0;
                        zombie.isDead = true;
                        ZombieManager.sendZombieDead(zombie, new Vector3(0, 0, 0));
                    }
                }
            }
        }

        private void onZoneLeft(UnturnedPlayer player, Zone zone, Vector3 lastPos)
        {
            if (zone.hasFlag(Zone.flagTypes[Zone.noLeave]))
            {
                if (!player.HasPermission("advancedzones.override.noleave") && !player.HasPermission("advancedzones.override.noleave." + zone.getName().ToLower()))
                {
                    if (player.IsInVehicle)
                    {
                        player.CurrentVehicle.forceRemoveAllPlayers();
                    }
                    player.Teleport(new Vector3(lastPos.x, lastPos.y - (float)0.6, lastPos.z), player.Rotation);
                    return;
                }
            }
            if (zone.hasFlag(Zone.flagTypes[Zone.leaveMessage]))
            {
                foreach (var leaveMessage in zone.getleaveMessages())
                {
                    UnturnedChat.Say(player, leaveMessage, UnityEngine.Color.green);
                }               
            }
            if (zone.hasFlag(Zone.flagTypes[Zone.leaveRemoveGroup]))
            {
                foreach (var group in zone.getLeaveRemoveGroups())
                {
                    R.Permissions.RemovePlayerFromGroup(group, player);
                }
            }
            if (zone.hasFlag(Zone.flagTypes[Zone.leaveAddGroup]))
            {
                foreach (var group in zone.getLeaveAddGroups())
                {
                    R.Permissions.RemovePlayerFromGroup(group, player);
                }
            }
        }

        private void onZoneEntered(UnturnedPlayer player, Zone zone, Vector3 lastPos)
        {
            if (zone.hasFlag(Zone.flagTypes[Zone.noEnter]))
            {
                if (!player.HasPermission("advancedzones.override.noenter") && !player.HasPermission("advancedzones.override.noenter." + zone.getName().ToLower()))
                {
                    if (player.IsInVehicle)
                    {
                        player.CurrentVehicle.forceRemoveAllPlayers();
                    }
                    player.Teleport(new Vector3(lastPos.x, lastPos.y - (float)0.6, lastPos.z), player.Rotation);
                    return;
                }
            }
            if (zone.hasFlag(Zone.flagTypes[Zone.enterMessage]))
            {
                foreach (var enterMessage in zone.getEnterMessages())
                {
                    UnturnedChat.Say(player, enterMessage, UnityEngine.Color.green);
                }
            }
            if (zone.hasFlag(Zone.flagTypes[Zone.enterRemoveGroup]))
            {
                foreach (var group in zone.getEnterRemoveGroups())
                {
                    R.Permissions.RemovePlayerFromGroup(group, player);
                }
            }
            if (zone.hasFlag(Zone.flagTypes[Zone.enterAddGroup]))
            {
                foreach (var group in zone.getEnterAddGroups())
                {
                    R.Permissions.AddPlayerToGroup(group, player);
                }
            }
        }

        private void onPlayerDisconnection(UnturnedPlayer player)
        {
            lastPosition.Remove(player.Id);
            foreach (var zone in getPositionZones(player.Position))
            {
                onZoneLeave(player, zone, player.Position);
            }
        }

        private void onPlayerConnection(UnturnedPlayer player)
        {
            lastPosition.Add(player.Id, player.Position);
            foreach (var zone in getPositionZones(player.Position))
            {
                onZoneEnter(player, zone, player.Position);
            }
            if (player.HasPermission("advancedzones") && notifyUpdate)
            {
                UnturnedChat.Say(player, "A new AdvancedZones version (" + newVersion + ") is available !!! Yours is: " + VERSION, Color.red);
                notifyUpdate = false;
            }
        }

        private void onTireDamage(CSteamID instigatorSteamID, InteractableVehicle vehicle, int tireIndex, ref bool shouldAllow, EDamageOrigin damageOrigin)
        {
            if (transformInZoneType(vehicle.transform, Zone.flagTypes[Zone.noTireDamage]) && !UnturnedPlayer.FromCSteamID(instigatorSteamID).HasPermission("advancedzones.override.tiredamage"))
            {
                List<Zone> currentZones = getPositionZones(vehicle.transform.position);
                foreach (var zone in currentZones)
                {
                    if (zone.hasFlag(Zone.flagTypes[Zone.noTireDamage]) && !UnturnedPlayer.FromCSteamID(instigatorSteamID).HasPermission(("advancedzones.override.tiredamage." + zone.getName()).ToLower()))
                    {
                        shouldAllow = false;
                    }
                }
            }
            else
            {
                shouldAllow = true;
            }
        }

        private void onPlayerEquiped(Player player, PlayerEquipment equipment)
        {
            if (!UnturnedPlayer.FromPlayer(player).HasPermission("advancedzones.override.equip"))
            {
                List<Zone> currentZones = getPositionZones(player.transform.position);
                foreach (var zone in currentZones)
                {
                    if (zone.hasFlag(Zone.flagTypes[Zone.noItemEquip]))
                    {
                        if (!UnturnedPlayer.FromPlayer(player).HasPermission(("advancedzones.override.equip." + zone.getName()).ToLower()))
                        {
                            List<EquipBlocklist> currentEquipBlocklists = getEquipBlocklists(zone.getEquipBlocklists());
                            List<EquipBlocklist> currentIgnoredEquipBlocklists = new List<EquipBlocklist>();
                            for (int i = 0; i < currentEquipBlocklists.Count; i++)
                            {
                                if (currentEquipBlocklists.ElementAt(i).name.ToLower().Contains("ignore"))
                                {
                                    currentIgnoredEquipBlocklists.Add(currentEquipBlocklists.ElementAt(i));
                                    currentEquipBlocklists.Remove(currentEquipBlocklists.ElementAt(i));
                                }
                            }

                            if (currentIgnoredEquipBlocklists.Count > 0)
                            {
                                foreach (var ignoredBlocklist in currentIgnoredEquipBlocklists)
                                {
                                    if (ignoredBlocklist.hasItem(equipment.asset.id))
                                        return;
                                }
                            }

                            foreach (var blocklist in currentEquipBlocklists)
                            {
                                if (blocklist.name == "ALL")
                                    return;
                            }

                            foreach (var blocklist in currentEquipBlocklists)
                            {
                                if (blocklist.hasItem(equipment.asset.id))
                                {
                                    equipment.dequip();
                                    return;
                                }
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }
        }

        private void onPlayerDamage(Player player, ref EDeathCause cause, ref ELimb limb, ref CSteamID killer, ref Vector3 direction, ref float damage, ref float times, ref bool canDamage)
        {
            if (cause == EDeathCause.BLEEDING || cause == EDeathCause.BONES || cause == EDeathCause.BREATH || cause == EDeathCause.BURNING || cause == EDeathCause.FOOD || cause == EDeathCause.FREEZING 
                || cause == EDeathCause.INFECTION || cause == EDeathCause.ARENA || cause == EDeathCause.KILL || cause == EDeathCause.SUICIDE || cause == EDeathCause.WATER)
            {
                return;
            }
            if (cause == EDeathCause.LANDMINE || cause == EDeathCause.SHRED || cause == EDeathCause.SENTRY || cause == EDeathCause.VEHICLE || cause == EDeathCause.ROADKILL)
            {
                if (playerInZoneType(UnturnedPlayer.FromPlayer(player), Zone.flagTypes[Zone.noPlayerDamage]))
                {
                    if (cause == EDeathCause.VEHICLE)
                    {
                        if (UnturnedPlayer.FromPlayer(player).IsInVehicle)
                            return;
                    }
                    canDamage = false;
                    return;
                } else
                {
                    return;
                }
            } 
            if (UnturnedPlayer.FromCSteamID(killer).Player == null && playerInZoneType(UnturnedPlayer.FromPlayer(player), Zone.flagTypes[Zone.noPlayerDamage]))
            {
                if (cause == EDeathCause.ZOMBIE)
                {
                    UnturnedPlayer.FromPlayer(player).Infection = 0;
                }
                canDamage = false;
                return;
            }
            else if (UnturnedPlayer.FromCSteamID(killer).Player == null)
            {
                return;
            }
            if ((playerInZoneType(UnturnedPlayer.FromPlayer(player), Zone.flagTypes[Zone.noPlayerDamage]) || playerInZoneType(UnturnedPlayer.FromCSteamID(killer), Zone.flagTypes[Zone.noPlayerDamage])) && !UnturnedPlayer.FromCSteamID(killer).HasPermission("advancedzones.override.playerdamage"))
            {
                List<Zone> currentZones = getPositionZones(player.transform.position);
                foreach (var zone in currentZones)
                {
                    if (zone.hasFlag(Zone.flagTypes[Zone.noPlayerDamage]) && !UnturnedPlayer.FromCSteamID(killer).HasPermission(("advancedzones.override.playerdamage." + zone.getName()).ToLower()))
                    {
                        canDamage = false;
                    }
                }
            }
        }

        private void onStructureDepoly(Structure structure, ItemStructureAsset asset, ref Vector3 point, ref float angle_x, ref float angle_y, ref float angle_z, ref ulong owner, ref ulong group, ref bool shouldAllow)
        {
            if (!UnturnedPlayer.FromCSteamID(new CSteamID(owner)).HasPermission("advancedzones.override.build"))
            {
                List<Zone> currentZones = getPositionZones(point);
                foreach (var zone in currentZones)
                {
                    if (zone.hasFlag(Zone.flagTypes[Zone.noBuild]))
                    {
                        if (!UnturnedPlayer.FromCSteamID(new CSteamID(owner)).HasPermission(("advancedzones.override.build." + zone.getName()).ToLower()))
                        {
                            List<BuildBlocklist> currentBuildBlocklists = getBuildBlocklists(zone.getBuildBlocklists());
                            List<BuildBlocklist> currentIgnoredBuildBlocklists = new List<BuildBlocklist>();
                            for (int i = 0; i < currentBuildBlocklists.Count; i++)
                            {
                                if (currentBuildBlocklists.ElementAt(i).name.ToLower().Contains("ignore"))
                                {
                                    currentIgnoredBuildBlocklists.Add(currentBuildBlocklists.ElementAt(i));
                                    currentBuildBlocklists.Remove(currentBuildBlocklists.ElementAt(i));
                                }
                            }

                            if (currentIgnoredBuildBlocklists.Count > 0)
                            {
                                foreach (var ignoredBlocklist in currentIgnoredBuildBlocklists)
                                {
                                    if (ignoredBlocklist.hasItem(asset.id))
                                        return;
                                }
                            }

                            foreach (var blocklist in currentBuildBlocklists)
                            {
                                if (blocklist.name == "ALL")
                                    return;
                            }

                            foreach (var blocklist in currentBuildBlocklists)
                            {
                                if (blocklist.hasItem(asset.id))
                                {
                                    shouldAllow = false;
                                    return;
                                }
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }
        }

        private void onBarricadeDeploy(Barricade barricade, ItemBarricadeAsset asset, Transform hit, ref Vector3 point, ref float angle_x, ref float angle_y, ref float angle_z, ref ulong owner, ref ulong group, ref bool shouldAllow)
        {
            if (UnturnedPlayer.FromCSteamID(new CSteamID(owner)) == null)
                return;
            if (!UnturnedPlayer.FromCSteamID(new CSteamID(owner)).HasPermission("advancedzones.override.build"))
            {
                List<Zone> currentZones = getPositionZones(point);
                foreach (var zone in currentZones)
                {
                    if (zone.hasFlag(Zone.flagTypes[Zone.noBuild]))
                    {
                        if (!UnturnedPlayer.FromCSteamID(new CSteamID(owner)).HasPermission(("advancedzones.override.build." + zone.getName()).ToLower()))
                        {
                            List<BuildBlocklist> currentBuildBlocklists = getBuildBlocklists(zone.getBuildBlocklists());
                            List<BuildBlocklist> currentIgnoredBuildBlocklists = new List<BuildBlocklist>();
                            for (int i = 0; i < currentBuildBlocklists.Count; i++)
                            {
                                if (currentBuildBlocklists.ElementAt(i).name.ToLower().Contains("ignore"))
                                {
                                    currentIgnoredBuildBlocklists.Add(currentBuildBlocklists.ElementAt(i));
                                    currentBuildBlocklists.Remove(currentBuildBlocklists.ElementAt(i));
                                }
                            }

                            if (currentIgnoredBuildBlocklists.Count > 0)
                            {
                                foreach (var ignoredBlocklist in currentIgnoredBuildBlocklists)
                                {
                                    if (ignoredBlocklist.hasItem(asset.id))
                                        return;
                                }
                            }

                            foreach (var blocklist in currentBuildBlocklists)
                            {
                                if (blocklist.name == "ALL")
                                    return;
                            }

                            foreach (var blocklist in currentBuildBlocklists)
                            {
                                if (blocklist.hasItem(asset.id))
                                {
                                    shouldAllow = false;
                                    return;
                                }
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }
        }

        private void onVehicleLockpick(InteractableVehicle vehicle, Player instigatingPlayer, ref bool allow)
        {
            if (transformInZoneType(vehicle.transform, Zone.flagTypes[Zone.noLockpick]) && !UnturnedPlayer.FromPlayer(instigatingPlayer).HasPermission("advancedzones.override.lockpick"))
            {
                List<Zone> currentZones = getPositionZones(vehicle.transform.position);
                foreach (var zone in currentZones)
                {
                    if (zone.hasFlag(Zone.flagTypes[Zone.noLockpick]) && !UnturnedPlayer.FromPlayer(instigatingPlayer).HasPermission(("advancedzones.override.lockpick." + zone.getName()).ToLower()))
                    {
                        allow = false; ;
                    }
                }
            }
            else
            {
                allow = true;
            }
        }

        private void onVehicleDamage(CSteamID instigatorSteamID, InteractableVehicle vehicle, ref ushort pendingTotalDamage, ref bool canRepair, ref bool shouldAllow, EDamageOrigin damageOrigin)
        {
            if ((transformInZoneType(vehicle.transform, Zone.flagTypes[Zone.noVehicleDamage]) && !UnturnedPlayer.FromCSteamID(instigatorSteamID).HasPermission("advancedzones.override.vehicledamage")) && pendingTotalDamage > 0)
            {
                List<Zone> currentZones = getPositionZones(vehicle.transform.position);
                foreach (var zone in currentZones)
                {
                    if (zone.hasFlag(Zone.flagTypes[Zone.noVehicleDamage]) && !UnturnedPlayer.FromCSteamID(instigatorSteamID).HasPermission(("advancedzones.override.vehicledamage." + zone.getName()).ToLower()))
                    {
                        shouldAllow = false;
                    }
                }
            }
            else
            {
                shouldAllow = true;
            }
        }

        private void onStructureDamage(CSteamID instigatorSteamID, Transform structureTransform, ref ushort pendingTotalDamage, ref bool shouldAllow, EDamageOrigin damageOrigin)
        {
            if (transformInZoneType(structureTransform, Zone.flagTypes[Zone.noDamage]))
            {
                if (UnturnedPlayer.FromCSteamID(instigatorSteamID) != null)
                {
                    if (!UnturnedPlayer.FromCSteamID(instigatorSteamID).HasPermission("advancedzones.override.damage") && pendingTotalDamage > 0)
                    {
                        List<Zone> currentZones = getPositionZones(structureTransform.transform.position);
                        foreach (var zone in currentZones)
                        {
                            if (zone.hasFlag(Zone.flagTypes[Zone.noDamage]) && !UnturnedPlayer.FromCSteamID(instigatorSteamID).HasPermission(("advancedzones.override.damage." + zone.getName()).ToLower()))
                            {
                                shouldAllow = false;
                            }
                        }
                    }
                }
                else if (damageOrigin.ToString() == "Bullet_Explosion"
                || damageOrigin.ToString() == "Charge_Explosion"
                || damageOrigin.ToString() == "Food_Explosion"
                || damageOrigin.ToString() == "Rocket_Explosion"
                || damageOrigin.ToString() == "Sentry"
                || damageOrigin.ToString() == "Trap_Explosion"
                || damageOrigin.ToString() == "Vehicle_Explosion"
                || damageOrigin.ToString() == "Zombie_Swipe")
                {
                    shouldAllow = false;
                }
            }
        }

        private void onBarricadeDamage(CSteamID instigatorSteamID, Transform barricadeTransform, ref ushort pendingTotalDamage, ref bool shouldAllow, EDamageOrigin damageOrigin)
        {
            if (transformInZoneType(barricadeTransform, Zone.flagTypes[Zone.noDamage]))
            {
                if (UnturnedPlayer.FromCSteamID(instigatorSteamID) != null)
                {
                    if (!UnturnedPlayer.FromCSteamID(instigatorSteamID).HasPermission("advancedzones.override.damage") && pendingTotalDamage > 0)
                    {
                        if (barricadeTransform.name.ToString() != "1102"
                                && barricadeTransform.name.ToString() != "1101"
                                && barricadeTransform.name.ToString() != "1393"
                                && barricadeTransform.name.ToString() != "1241")
                        {
                            List<Zone> currentZones = getPositionZones(barricadeTransform.transform.position);
                            foreach (var zone in currentZones)
                            {
                                if (zone.hasFlag(Zone.flagTypes[Zone.noDamage]) && !UnturnedPlayer.FromCSteamID(instigatorSteamID).HasPermission(("advancedzones.override.damage." + zone.getName()).ToLower()))
                                {
                                    shouldAllow = false;
                                }
                            }
                        }
                    }
                }
                else if ((damageOrigin.ToString() == "Bullet_Explosion"
                    || (damageOrigin.ToString() == "Charge_Explosion")
                    || damageOrigin.ToString() == "Food_Explosion"
                    || damageOrigin.ToString() == "Rocket_Explosion"
                    || damageOrigin.ToString() == "Sentry"
                    || damageOrigin.ToString() == "Trap_Explosion"
                    || damageOrigin.ToString() == "Vehicle_Explosion"
                    || damageOrigin.ToString() == "Zombie_Swipe") &&
                    (barricadeTransform.name.ToString() != "1102"
                    && barricadeTransform.name.ToString() != "1101"
                    && barricadeTransform.name.ToString() != "1393"
                    && barricadeTransform.name.ToString() != "1241"))
                {
                    shouldAllow = false;
                }
            }
        }

        public bool playerInAZone(UnturnedPlayer player)
        {
            return getPositionZones(player.Position).Count > 0;
        }

        public bool playerInZoneType(UnturnedPlayer player, string type)
        {
            List<Zone> zones = getPositionZones(player.Position);
            if (zones.Count == 0)
            {
                return false;
            } else
            {
                foreach (var z in zones)
                {
                    if (z.hasFlag(type))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public List<Zone> getPositionZones(Vector3 position)
        {
            List<Zone> zones = new List<Zone>();
            foreach (var z in Configuration.Instance.Zones)
            {
                if (z.isReady())
                {
                    float playerX = position.x;
                    float playerZ = position.z;
                    float playerY = position.y;

                    HeightNode[] heightNodes = z.GetHeightNodes();

                    Node[] zoneNodes = z.getNodes();

                    int j = zoneNodes.Length - 1;
                    bool oddNodes = false;

                    for (int i = 0; i < zoneNodes.Length; i++)
                    {
                        if ((zoneNodes[i].z < playerZ && zoneNodes[j].z >= playerZ
                             || zoneNodes[j].z < playerZ && zoneNodes[i].z >= playerZ)
                             && (zoneNodes[i].x <= playerX || zoneNodes[j].x <= playerX))
                        {
                            if (zoneNodes[i].x + (playerZ - zoneNodes[i].z) / (zoneNodes[j].z - zoneNodes[i].z) * (zoneNodes[j].x - zoneNodes[i].x) < playerX)
                            {
                                oddNodes = !oddNodes;
                            }
                        }
                        j = i;
                    }
                    if (oddNodes)
                    {
                        if (heightNodes[0] != null && heightNodes[1] != null)
                        {
                            if (heightNodes[0].isUpper)
                            {
                                if (playerY < heightNodes[0].y && playerY > heightNodes[1].y)
                                {
                                    zones.Add(z);
                                }
                            }
                        }
                        else if (heightNodes[0] != null)
                        {
                            if (heightNodes[0].isUpper && playerY < heightNodes[0].y)
                                zones.Add(z);
                            else if (!heightNodes[0].isUpper && playerY > heightNodes[0].y)
                                zones.Add(z);
                        }
                        else
                        {
                            zones.Add(z);
                        }
                    }
                }
            }
            return zones;
        }

        public bool transformInZoneType(Transform transform, string type)
        {

            if (getPositionZones(transform.position).Count == 0)
            {
                return false;
            }
            else
            {
                foreach (var z in getPositionZones(transform.position))
                {
                    if (z.hasFlag(type))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public Zone getZoneByName(string zoneName)
        {
            foreach (var z in Configuration.Instance.Zones)
            {
                if (z.getName().ToLower() == zoneName.ToLower())
                {
                    return z;
                }
            }
            return null;
        }

        public List<BuildBlocklist> getBuildBlocklists(List<string> blocklistNames)
        {
            List<BuildBlocklist> temp = new List<BuildBlocklist>();
            foreach (var blocklistName in blocklistNames)
            {
                foreach (var blocklist in Configuration.Instance.BuildBlocklists)
                {
                    if (blocklist.name == blocklistName)
                    {
                        temp.Add(blocklist);
                        break;
                    }
                }
            }
            return temp;
        }

        public List<EquipBlocklist> getEquipBlocklists(List<string> blocklistNames)
        {
            List<EquipBlocklist> temp = new List<EquipBlocklist>();
            foreach (var blocklistName in blocklistNames)
            {
                foreach (var blocklist in Configuration.Instance.EquipBlocklists)
                {
                    if (blocklist.name == blocklistName)
                    {
                        temp.Add(blocklist);
                        break;
                    }
                }
            }
            return temp;
        }

        public void addCustomFlag(string flagName, int flagID, string flagDescription)
        {
            foreach (var customFlag in Configuration.Instance.CustomFlags)
            {
                if (customFlag.name == flagName)
                    return;
            }
            Configuration.Instance.CustomFlags.Add(new CustomFlag(flagName, flagID, flagDescription));
            Configuration.Save();
            return;
        }

        public void removeCustomFlag(string flagName)
        {
            foreach (var customFlag in Configuration.Instance.CustomFlags)
            {
                if (customFlag.name == flagName)
                {
                    Configuration.Instance.CustomFlags.Remove(customFlag);
                    Configuration.Save();
                }
            }
        }

        public List<CustomFlag> GetCustomFlags()
        {
            return Configuration.Instance.CustomFlags;
        }
    }
}