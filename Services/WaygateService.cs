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

    readonly List<Entity> spawnedWaygates = [];
    readonly Dictionary<Entity, List<Entity>> unlockedSpawnedWaypoints = [];

    public WaygateService()
    {
        var spawnedWaypointQuery = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] {
                ComponentType.ReadOnly<SpawnedBy>(),
                ComponentType.ReadOnly<ChunkWaypoint>(),
            },
            Options = EntityQueryOptions.IncludeDisabled
        });

        var entities = spawnedWaypointQuery.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            spawnedWaygates.Add(entity);
        }
        entities.Dispose();
        spawnedWaypointQuery.Dispose();

        connectedUserQuery = Core.EntityManager.CreateEntityQuery(new ComponentType[]
        {
            ComponentType.ReadOnly<IsConnected>(),
            ComponentType.ReadOnly<User>()
        });

        entities = connectedUserQuery.ToEntityArray(Allocator.Temp);
        foreach (var userEntity in entities)
        {
            InitializeUnlockedWaypoints(userEntity);
        }
        entities.Dispose();

        waypointQuery = Core.EntityManager.CreateEntityQuery(new ComponentType[]
        {
            ComponentType.ReadOnly<ChunkWaypoint>(),
        });

        Core.StartCoroutine(CheckForWaypointUnlocks());
    }

    void InitializeUnlockedWaypoints(Entity userEntity)
    {
        unlockedSpawnedWaypoints.Add(userEntity, []);

        var unlockedWaypoints = Core.EntityManager.GetBuffer<UnlockedWaypointElement>(userEntity);
        foreach (var waypoint in unlockedWaypoints)
        {
            var foundWaypoint = spawnedWaygates.Where(x => x.Read<NetworkId>() == waypoint.Waypoint).ToArray();
            if (foundWaypoint.Length > 0)
            {
                unlockedSpawnedWaypoints[userEntity].Add(foundWaypoint[0]);
            }
        }

        if (Core.ServerGameSettingsSystem.Settings.AllWaypointsUnlocked)
        {
            foreach (var waygate in spawnedWaygates)
            {
                if (unlockedSpawnedWaypoints[userEntity].Contains(waygate)) continue;
                unlockedSpawnedWaypoints[userEntity].Add(waygate);
                unlockedWaypoints.Add(new() { Waypoint = waygate.Read<NetworkId>() });
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
        
        spawnedWaygates.Add(newWaypoint);        
        return true;
    }

    public bool TeleportToClosestWaygate(Entity character)
    {
        var pos = character.Read<Translation>().Value;
        var closestWaypoint = spawnedWaygates.OrderBy(x => math.distance(pos, x.Read<Translation>().Value)).FirstOrDefault();
        if (closestWaypoint == Entity.Null) return false;

        var waypointPos = closestWaypoint.Read<Translation>().Value;
        var waypointRot = closestWaypoint.Read<Rotation>().Value;

        character.Write(new Translation { Value = waypointPos });
        character.Write(new LastTranslation { Value = waypointPos });
        character.Write(new Rotation { Value = waypointRot });
        return true;
    }

    public void UnlockWaypoint(Entity userEntity, Entity waypoint)
    {
        if (!unlockedSpawnedWaypoints.TryGetValue(userEntity, out var unlockedWaypoints))
        {
            InitializeUnlockedWaypoints(userEntity);
        }
        unlockedWaypoints.Add(waypoint);
        var unlockedWaypointElements = Core.EntityManager.GetBuffer<UnlockedWaypointElement>(userEntity);

        // Verify it wasn't unlocked via some other mean
        var waypointNetworkId = waypoint.Read<NetworkId>();
        foreach(var unlockedWaypoint in unlockedWaypointElements)
        {
            if (unlockedWaypoint.Waypoint == waypointNetworkId) return;
        }

        unlockedWaypointElements.Add(new() { Waypoint = waypoint.Read<NetworkId>() });
    }

    IEnumerator CheckForWaypointUnlocks()
    {
        var timeBetweenChecks = new WaitForSeconds(0.1f);
        while (true)
        {
            var connectedUsers = connectedUserQuery.ToEntityArray(Allocator.Temp);
            foreach (var userEntity in connectedUsers)
            {
                var user = userEntity.Read<User>();
                var characterEntity = user.LocalCharacter.GetEntityOnServer();

                if (characterEntity == Entity.Null) continue;

                if (!unlockedSpawnedWaypoints.TryGetValue(userEntity, out var _))
                {
                    InitializeUnlockedWaypoints(userEntity);
                }

                if (Core.ServerGameSettingsSystem.Settings.AllWaypointsUnlocked)
                {
                    yield return timeBetweenChecks;
                    continue;
                }

                var pos = characterEntity.Read<Translation>().Value;
                foreach (var waygate in spawnedWaygates)
                {
                    if (unlockedSpawnedWaypoints[userEntity].Contains(waygate)) continue;

                    var waypointPos = waygate.Read<Translation>().Value;
                    if (math.distance(pos, waypointPos) < UnlockDistance)
                    {
                        UnlockWaypoint(userEntity, waygate);
                    }
                }
            }

            yield return timeBetweenChecks;
        }
    }

    public bool DestroyWaygate(Entity senderCharacterEntity)
    {
        const float DISTANCE_TO_DESTROY = 10f;
        var pos = senderCharacterEntity.Read<Translation>().Value;
        var closestWaypoint = spawnedWaygates
            .OrderBy(x => math.distance(pos, x.Read<Translation>().Value))
            .FirstOrDefault(x => math.distance(pos, x.Read<Translation>().Value) < DISTANCE_TO_DESTROY);
        if (closestWaypoint == Entity.Null) return false;

        DestroyUtility.Destroy(Core.EntityManager, closestWaypoint);
        spawnedWaygates.Remove(closestWaypoint);
        return true;
    }
}
