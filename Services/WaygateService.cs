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
        var spawnedWaypointQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
            .AddAll(ComponentType.ReadOnly<ChunkWaypoint>())
            .AddAll(ComponentType.ReadOnly<SpawnedBy>())
            .WithOptions(EntityQueryOptions.IncludeDisabled);
        spawnedWaypointQuery = Core.EntityManager.CreateEntityQuery(ref spawnedWaypointQueryBuilder);
        spawnedWaypointQueryBuilder.Dispose();

        var connectedUserQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
            .AddAll(ComponentType.ReadOnly<IsConnected>())
            .AddAll(ComponentType.ReadOnly<User>());

        connectedUserQuery = Core.EntityManager.CreateEntityQuery(ref connectedUserQueryBuilder);
        connectedUserQueryBuilder.Dispose();

        var waypointQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
            .AddAll(ComponentType.ReadOnly<ChunkWaypoint>())
            .WithOptions(EntityQueryOptions.IncludeDisabled);
        waypointQuery = Core.EntityManager.CreateEntityQuery(ref waypointQueryBuilder);
        waypointQueryBuilder.Dispose();

        if (!Core.ServerGameSettingsSystem.Settings.AllWaypointsUnlocked)
            Core.StartCoroutine(CheckForWaypointUnlocks());
    }

    List<NetworkId> InitializeUnlockedWaypoints(Entity userEntity)
    {
        var unlockedUserSpawnedWaypoints = new List<NetworkId>();
        unlockedSpawnedWaypoints.Add(userEntity, unlockedUserSpawnedWaypoints);

        var unlockedWaypoints = Core.EntityManager.GetBuffer<UnlockedWaypointElement>(userEntity);
        var spawnedWaypoints = spawnedWaypointQuery.ToEntityArray(Allocator.Temp);
        var spawnedWaypointsArray = spawnedWaypoints.ToArray();
        spawnedWaypoints.Dispose();

        foreach (var waypoint in unlockedWaypoints)
        {
            if (spawnedWaypointsArray.Any(x => x.Read<NetworkId>() == waypoint.Waypoint))
            {
                unlockedUserSpawnedWaypoints.Add(waypoint.Waypoint);
            }
        }

        return unlockedUserSpawnedWaypoints;
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
        if (waypointNetworkId == NetworkId.Empty)
        {
            Core.Log.LogError("Attempted to unlock an empty waypoint");
            return;
        }
        if (!unlockedSpawnedWaypoints.TryGetValue(userEntity, out var unlockedWaypoints))
        {
            unlockedWaypoints = InitializeUnlockedWaypoints(userEntity);
        }
        unlockedWaypoints.Add(waypointNetworkId);

        // Verify it wasn't unlocked via some other mean
        var unlockedWaypointElements = Core.EntityManager.GetBuffer<UnlockedWaypointElement>(userEntity);
        foreach (var unlockedWaypoint in unlockedWaypointElements)
        {
            if (unlockedWaypoint.Waypoint == waypointNetworkId) return;
        }

        Core.Log.LogInfo($"Waypoint {waypointNetworkId} unlocked for {userEntity.Read<User>().CharacterName}");
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
                if (!unlockedSpawnedWaypoints.TryGetValue(userEntity, out var unlockedWaypoints))
                {
                    unlockedWaypoints = InitializeUnlockedWaypoints(userEntity);
                }
                
                var characterEntity = user.LocalCharacter.GetEntityOnServer();
                if (characterEntity == Entity.Null) continue;

                var pos = characterEntity.Read<Translation>().Value;
                foreach (var waygate in spawnedWaygates)
                {
                    var waygateNetworkId = waygate.Read<NetworkId>();
                    if (unlockedWaypoints.Contains(waygateNetworkId)) continue;

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
