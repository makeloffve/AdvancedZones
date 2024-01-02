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
            if (null == cSteamID || cSteamID == CSteamID.Nil || !playerInZoneDict.TryGetValue(cSteamID, out zoneNames))
            {
                zoneNames = new HashSet<string>();
            }
            return zoneNames;
        }

        public bool IsPlayerInZone(CSteamID cSteamID, string zoneName)
        {
            if(null == cSteamID || cSteamID == CSteamID.Nil || string.IsNullOrEmpty(zoneName))
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
            return GetPlayerZoneNames(getCSteamID(player));
        }

        public bool IsPlayerInZone(UnturnedPlayer player, string zoneName)
        {
            return IsPlayerInZone(getCSteamID(player), zoneName);
        }

        public HashSet<string> GetPlayerZoneNames(Player player)
        {
            return GetPlayerZoneNames(getCSteamID(player));
        }

        public bool IsPlayerInZone(Player player, string zoneName)
        {
            return IsPlayerInZone(getCSteamID(player), zoneName);
        }

        public List<UnturnedPlayer> GetZonePlayers(string zoneName)
        {
            List<UnturnedPlayer> players = new List<UnturnedPlayer>();
            Provider.clients.ForEach((client) =>
            {
                if (null == client) return;
                try
                {
                    UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(client);
                    if(IsPlayerInZone(player, zoneName))
                    {
                        players.Add(player);
                    }
                }
                catch
                {
                    return;
                }
            });
            return players;
        }

        public void onZoneEnterHandler(UnturnedPlayer player, Zone zone, Vector3 lastPos)
        {
            HashSet<string> inZoneNames;

            CSteamID cSteamID = getCSteamID(player);
            if(null == cSteamID || cSteamID == CSteamID.Nil)
            {
                return;
            }

            if (playerInZoneDict.TryGetValue(cSteamID, out inZoneNames))
            {
                inZoneNames.Add(zone?.name);
            }
            else
            {
                inZoneNames = new HashSet<string> { zone?.name };
                playerInZoneDict.Add(cSteamID, inZoneNames);
            }
        }

        public void onZoneLeaveHandler(UnturnedPlayer player, Zone zone, Vector3 lastPos)
        {
            CSteamID cSteamID = getCSteamID(player);
            if (null == cSteamID || cSteamID == CSteamID.Nil)
            {
                return;
            }

            if (playerInZoneDict.TryGetValue(cSteamID, out HashSet<string> inZoneNames))
            {
                inZoneNames.Remove(zone?.name);
            }
        }

        public void onPlayerConnection(UnturnedPlayer player)
        {
            CSteamID cSteamID = getCSteamID(player);
            if (null == cSteamID || cSteamID == CSteamID.Nil)
            {
                return;
            }

            if (playerInZoneDict.ContainsKey(cSteamID))
            {
                playerInZoneDict.Remove(cSteamID);
            }

            playerInZoneDict.Add(cSteamID, new HashSet<string>());
        }

        public void onPlayerDisconnection(UnturnedPlayer player)
        {
            Clean(getCSteamID(player));
        }

        public void Clean(CSteamID cSteamID)
        {
            if(null == cSteamID || cSteamID == CSteamID.Nil)
            {
                return;
            }
            playerInZoneDict.Remove(cSteamID);
        }

        private CSteamID getCSteamID(UnturnedPlayer uPlayer)
        {
            return getCSteamID(uPlayer.Player);
        }

        private CSteamID getCSteamID(Player player)
        {
            if (null == player || null == player.transform)
            {
                return CSteamID.Nil;
            }
            return player.channel.owner.playerID.steamID;
        }

    }
}
