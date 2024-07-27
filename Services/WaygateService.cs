using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace KindredPortals.Services;
class WaygateService
{
    const float UnlockDistance = 25f;
    readonly EntityQuery connectedUserQuery;
    readonly EntityQuery waypointQuery;
    readonly EntityQuery spawnedWaypointQuery;

    readonly Dictionary<Entity, List<NetworkId>> unlockedSpawnedWaypoints = [];

    public WaygateService()
    {
        spawnedWaypointQuery = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] {
                ComponentType.ReadOnly<SpawnedBy>(),
                ComponentType.ReadOnly<ChunkWaypoint>(),
            },
            Options = EntityQueryOptions.IncludeDisabled
        });

        connectedUserQuery = Core.EntityManager.CreateEntityQuery(new ComponentType[]
        {
            ComponentType.ReadOnly<IsConnected>(),
            ComponentType.ReadOnly<User>()
        });

        waypointQuery = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] { ComponentType.ReadOnly<ChunkWaypoint>() },
            Options = EntityQueryOptions.IncludeDisabled
        });

        Core.StartCoroutine(CheckForWaypointUnlocks());
    }

    void InitializeUnlockedWaypoints(Entity userEntity)
    {
        unlockedSpawnedWaypoints.Add(userEntity, []);

        var unlockedWaypoints = Core.EntityManager.GetBuffer<UnlockedWaypointElement>(userEntity);
        var spawnedWaypoints = spawnedWaypointQuery.ToEntityArray(Allocator.Temp);
        var spawnedWaypointsArray = spawnedWaypoints.ToArray();
        spawnedWaypoints.Dispose();

        foreach (var waypoint in unlockedWaypoints)
        {
            if (spawnedWaypointsArray.Any(x => x.Read<NetworkId>() == waypoint.Waypoint))
            {
                unlockedSpawnedWaypoints[userEntity].Add(waypoint.Waypoint);
            }
        }

        if (Core.ServerGameSettingsSystem.Settings.AllWaypointsUnlocked)
        {
            foreach (var waygate in spawnedWaypointsArray)
            {
                var networkId = waygate.Read<NetworkId>();
                if (unlockedSpawnedWaypoints[userEntity].Contains(networkId)) continue;
                unlockedSpawnedWaypoints[userEntity].Add(networkId);
                unlockedWaypoints.Add(new() { Waypoint = networkId });
            }
        }
    }

    public bool CreateWaygate(Entity character, PrefabGUID waypointPrefabGUID)
    {
        if (!Core.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(waypointPrefabGUID, out var waypointPrefab))
        {
            Core.Log.LogError($"Failed to find {waypointPrefabGUID.LookupName()} Prefab entity");
            return false;
        }

        var pos = character.Read<Translation>().Value;
        var chunk = pos.GetChunk();
        var waypoints = waypointQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var waypoint in waypoints)
            {
                if (waypoint.Has<CastleWorkstation>())
                    continue;
                if (waypoint.GetChunk() == chunk)
                    return false;
            }
        }
        finally
        {
            waypoints.Dispose();
        }

        var rot = character.Read<Rotation>().Value;

        var newWaypoint = Core.EntityManager.Instantiate(waypointPrefab);

        newWaypoint.Write(new Translation { Value = pos });
        newWaypoint.Write(new Rotation { Value = rot });
        newWaypoint.Add<SpawnedBy>();
        newWaypoint.Write(new SpawnedBy { Value = character });

        if (Core.ServerGameSettingsSystem.Settings.AllWaypointsUnlocked)
        {
            foreach(var userEntity in unlockedSpawnedWaypoints.Keys)
            {
                UnlockWaypoint(userEntity, newWaypoint.Read<NetworkId>());
            }
        }
        return true;
    }

    public bool TeleportToClosestWaygate(Entity character)
    {
        var pos = character.Read<Translation>().Value;
        var spawnedWaypoints = spawnedWaypointQuery.ToEntityArray(Allocator.Temp);
        var closestWaypoint = spawnedWaypoints.ToArray().OrderBy(x => math.distance(pos, x.Read<Translation>().Value)).FirstOrDefault();
        spawnedWaypoints.Dispose();
        if (closestWaypoint == Entity.Null) return false;

        var waypointPos = closestWaypoint.Read<Translation>().Value;
        var waypointRot = closestWaypoint.Read<Rotation>().Value;

        character.Write(new Translation { Value = waypointPos });
        character.Write(new LastTranslation { Value = waypointPos });
        character.Write(new Rotation { Value = waypointRot });
        return true;
    }

    public void UnlockWaypoint(Entity userEntity, NetworkId waypointNetworkId)
    {
        if (!unlockedSpawnedWaypoints.TryGetValue(userEntity, out var unlockedWaypoints))
        {
            InitializeUnlockedWaypoints(userEntity);
        }
        unlockedWaypoints.Add(waypointNetworkId);

        // Verify it wasn't unlocked via some other mean
        var unlockedWaypointElements = Core.EntityManager.GetBuffer<UnlockedWaypointElement>(userEntity);
        foreach (var unlockedWaypoint in unlockedWaypointElements)
        {
            if (unlockedWaypoint.Waypoint == waypointNetworkId) return;
        }

        unlockedWaypointElements.Add(new() { Waypoint = waypointNetworkId });
    }

    IEnumerator CheckForWaypointUnlocks()
    {
        var timeBetweenChecks = new WaitForSeconds(0.1f);
        while (true)
        {
            yield return timeBetweenChecks;
            var spawnedWaygates = spawnedWaypointQuery.ToEntityArray(Allocator.Temp);

            if (spawnedWaygates.Length == 0)
            {
                spawnedWaygates.Dispose();
                continue;
            }

            var connectedUsers = connectedUserQuery.ToEntityArray(Allocator.Temp);
            foreach (var userEntity in connectedUsers)
            {
                var user = userEntity.Read<User>();
                if (!unlockedSpawnedWaypoints.TryGetValue(userEntity, out var _))
                {
                    InitializeUnlockedWaypoints(userEntity);
                }

                if (Core.ServerGameSettingsSystem.Settings.AllWaypointsUnlocked)
                {
                    continue;
                }
                
                var characterEntity = user.LocalCharacter.GetEntityOnServer();
                if (characterEntity == Entity.Null) continue;

                var pos = characterEntity.Read<Translation>().Value;
                foreach (var waygate in spawnedWaygates)
                {
                    var waygateNetworkId = waygate.Read<NetworkId>();
                    if (unlockedSpawnedWaypoints[userEntity].Contains(waygateNetworkId)) continue;

                    var waypointPos = waygate.Read<Translation>().Value;
                    if (math.distance(pos, waypointPos) < UnlockDistance)
                    {
                        UnlockWaypoint(userEntity, waygateNetworkId);
                    }
                }
            }

            connectedUsers.Dispose();
            spawnedWaygates.Dispose();
        }
    }

    public bool DestroyWaygate(Entity senderCharacterEntity)
    {
        const float DISTANCE_TO_DESTROY = 10f;
        var pos = senderCharacterEntity.Read<Translation>().Value;
        var spawnedWaygates = spawnedWaypointQuery.ToEntityArray(Allocator.Temp);
        var closestWaypoint = spawnedWaygates.ToArray()
            .OrderBy(x => math.distance(pos, x.Read<Translation>().Value))
            .FirstOrDefault(x => math.distance(pos, x.Read<Translation>().Value) < DISTANCE_TO_DESTROY);
        spawnedWaygates.Dispose();
        if (closestWaypoint == Entity.Null) return false;

        DestroyUtility.Destroy(Core.EntityManager, closestWaypoint);
        return true;
    }
}
