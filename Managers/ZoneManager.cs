using Rocket.Unturned;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game4Freak.AdvancedZones.Managers
{
    public class ZoneManager
    {
        private static readonly Lazy<ZoneManager> instance = new Lazy<ZoneManager>(() => new ZoneManager());
        private ZoneManager()
        {
            playerInZoneDict = new Dictionary<CSteamID, HashSet<string>>();
        }
        public static ZoneManager Instance() { return instance.Value; }

        private Dictionary<CSteamID, HashSet<string>> playerInZoneDict;

        public void Load()
        {
            AdvancedZones.onZoneEnter += onZoneEnterHandler;
            AdvancedZones.onZoneLeave += onZoneLeaveHandler;
            U.Events.OnPlayerConnected += onPlayerConnection;
            U.Events.OnPlayerDisconnected += onPlayerDisconnection;
        }

        public void UnLoad()
        {
            playerInZoneDict.Clear();
            AdvancedZones.onZoneEnter -= onZoneEnterHandler;
            AdvancedZones.onZoneLeave -= onZoneLeaveHandler;
            U.Events.OnPlayerConnected -= onPlayerConnection;
            U.Events.OnPlayerDisconnected -= onPlayerDisconnection;
        }

        public HashSet<string> GetPlayerZoneNames(CSteamID cSteamID)
        {
            HashSet<string> zoneNames;
            if (null == cSteamID || !playerInZoneDict.TryGetValue(cSteamID, out zoneNames))
            {
                zoneNames = new HashSet<string>();
            }
            return zoneNames;
        }

        public bool IsPlayerInZone(CSteamID cSteamID, string zoneName)
        {
            if(null == cSteamID || string.IsNullOrEmpty(zoneName))
            {
                return false;
            }

            HashSet<string> playerZoneNames = GetPlayerZoneNames(cSteamID);
            if (null == playerZoneNames || playerZoneNames.Count <= 0)
            {
                return false;
            }
            return playerZoneNames.Contains(zoneName);
        }

        public HashSet<string> GetPlayerZoneNames(UnturnedPlayer player)
        {
            CSteamID? cSteamID = player?.CSteamID;
            return GetPlayerZoneNames(cSteamID.Value);
        }

        public bool IsPlayerInZone(UnturnedPlayer player, string zoneName)
        {
            CSteamID? cSteamID = player?.CSteamID;
            return IsPlayerInZone(cSteamID.Value, zoneName);
        }

        public HashSet<string> GetPlayerZoneNames(Player player)
        {
            return GetPlayerZoneNames(player.channel.owner.playerID.steamID);
        }

        public bool IsPlayerInZone(Player player, string zoneName)
        {
            return IsPlayerInZone(player.channel.owner.playerID.steamID, zoneName);
        }

        public void onZoneEnterHandler(UnturnedPlayer player, Zone zone, Vector3 lastPos)
        {
            HashSet<string> inZoneNames;
            if (playerInZoneDict.TryGetValue(player.CSteamID, out inZoneNames))
            {
                inZoneNames.Add(zone?.name);
            }
            else
            {
                inZoneNames = new HashSet<string> { zone?.name };
                playerInZoneDict.Add(player.CSteamID, inZoneNames);
            }
        }

        public void onZoneLeaveHandler(UnturnedPlayer player, Zone zone, Vector3 lastPos)
        {
            if (playerInZoneDict.TryGetValue(player.CSteamID, out HashSet<string> inZoneNames))
            {
                inZoneNames.Remove(zone?.name);
            }
        }

        public void onPlayerConnection(UnturnedPlayer player)
        {
            if (!playerInZoneDict.TryGetValue(player.CSteamID, out var _))
            {
                playerInZoneDict.Add(player.CSteamID, new HashSet<string>());
            }
        }

        public void onPlayerDisconnection(UnturnedPlayer player)
        {
            playerInZoneDict.Remove(player.CSteamID);
        }
    }
}
