using ProjectM;
using ProjectM.Terrain;
using Stunlock.Core;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KindredPortals.Services;
class PortalService
{
    Entity portalPrefab;
    EntityQuery spawnedPortalQuery;

    Dictionary<Entity, (float3 pos, quaternion rot, TerrainChunk chunk, int index, PrefabGUID mapIcon)> portalStartPos = [];

    public PortalService()
    {
        if(!Core.PrefabCollection._NameToPrefabGuidDictionary.TryGetValue("TM_General_Entrance_Gate", out var portalPrefabGUID))
            Core.Log.LogError("Failed to find TM_General_Entrance_Gate PrefabGUID");
        if(!Core.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(portalPrefabGUID, out portalPrefab))
            Core.Log.LogError("Failed to find TM_General_Entrance_Gate Prefab entity");

        spawnedPortalQuery = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] {
                ComponentType.ReadOnly<ChunkPortal>(),
                ComponentType.ReadOnly<SpawnedBy>(),
            },
            Options = EntityQueryOptions.IncludeDisabled
        });
    }

    public bool StartPortal(Entity playerEntity, PrefabGUID mapIcon)
    {
        var pos = playerEntity.Read<Translation>().Value;
        var chunk = new TerrainChunk { X = (sbyte)((pos.x + 3200) / 160), Y = (sbyte)((pos.z + 3200) / 160) };
        var index = GetNextAvailableIndex(chunk);

        if(index >= 9)
            return false;

        portalStartPos[playerEntity] = (pos, playerEntity.Read<Rotation>().Value, chunk, index, mapIcon);
        return true;
    }

    public string EndPortal(Entity playerEntity, PrefabGUID endMapIcon)
    {
        if (!portalStartPos.TryGetValue(playerEntity, out var start))
            return "Start portal hasn't been specified";

        var endPos = playerEntity.Read<Translation>().Value;
        var endChunk = new TerrainChunk { X = (sbyte)((endPos.x + 3200) / 160), Y = (sbyte)((endPos.z + 3200) / 160) };
        var endIndex = GetNextAvailableIndex(endChunk);
        if (endChunk == start.chunk)
        {
            endIndex += 1;
        }

        if (endIndex >= 9)
            return "Can't have more than 9 portals in a chunk";

        CreatePortal(playerEntity, start.pos, start.rot, start.chunk, start.index, start.mapIcon, endChunk, endIndex);

        var endRot = playerEntity.Read<Rotation>().Value;
        CreatePortal(playerEntity, endPos, endRot, endChunk, endIndex, endMapIcon, start.chunk, start.index);

        Core.Log.LogInfo($"Start: {start.chunk} {start.index} End: {endChunk} {endIndex}");
        return null;
    }

    private void CreatePortal(Entity creator, float3 pos, quaternion rot, TerrainChunk chunk, int index, PrefabGUID mapIcon, TerrainChunk toChunk, int toIndex)
    {
        var startPortal = Core.EntityManager.Instantiate(portalPrefab);
        startPortal.Write(new Translation { Value = pos });
        startPortal.Write(new Rotation { Value = rot });

        startPortal.Add<SpawnedBy>();
        startPortal.Write(new SpawnedBy { Value = creator });

        var attachMapIconsToEntity = Core.EntityManager.GetBuffer<AttachMapIconsToEntity>(startPortal);
        attachMapIconsToEntity.Clear();
        if (!mapIcon.Equals(PrefabGUID.Empty))
            attachMapIconsToEntity.Add(new() { Prefab = mapIcon });
        startPortal.Write(new ChunkPortal { FromChunk = chunk, FromChunkPortalIndex = index, ToChunk = toChunk, ToChunkPortalIndex = toIndex });
    }

    int GetNextAvailableIndex(TerrainChunk chunk)
    {
        if (!Core.ChunkObjectManager._ChunkPortals.TryGetValue(chunk, out var portalList))
            return 0;

        for (var i = 0; i < portalList.Length; i++)
        {
            if (portalList[i].PortalEntity == Entity.Null)
            {
                return i;
            }
        }

        return portalList.Length;
    }

    public bool TeleportToClosestPortal(Entity playerEntity)
    {
        var portalEntities = spawnedPortalQuery.ToEntityArray(Allocator.Temp);
        var pos = playerEntity.Read<Translation>().Value;
        var closestPortal = portalEntities.ToArray().OrderBy(x => math.distance(pos, x.Read<Translation>().Value)).FirstOrDefault();
        if (closestPortal == Entity.Null) return false;

        var portalPos = closestPortal.Read<Translation>().Value;
        var portalRot = closestPortal.Read<Rotation>().Value;
        playerEntity.Write(new Translation { Value = portalPos });
        playerEntity.Write(new LastTranslation { Value = portalPos });
        playerEntity.Write(new Rotation { Value = portalRot });
        return true;
    }

    Entity GetConnectedPortal(ChunkPortal chunkPortal)
    {
        if (!Core.ChunkObjectManager._ChunkPortals.TryGetValue(chunkPortal.ToChunk, out var portalDataList))
            return Entity.Null;
        return portalDataList[chunkPortal.ToChunkPortalIndex].PortalEntity;
    }

    void RemovePortalEntry(ChunkPortal chunkPortal)
    {
        if (!Core.ChunkObjectManager._ChunkPortals.TryGetValue(chunkPortal.FromChunk, out var portalDataList))
            return;
        portalDataList[chunkPortal.FromChunkPortalIndex] = new();
        Core.ChunkObjectManager._ChunkPortals.Remove(chunkPortal.FromChunk);
        Core.ChunkObjectManager._ChunkPortals.Add(chunkPortal.FromChunk, portalDataList);
    }

    public bool DestroyPortal(Entity senderCharacterEntity)
    {
        const float DISTANCE_TO_DESTROY = 5f;
        var pos = senderCharacterEntity.Read<Translation>().Value;
        var spawnedPortals = spawnedPortalQuery.ToEntityArray(Allocator.Temp);
        var closestPortal = spawnedPortals.ToArray()
            .OrderBy(x => math.distance(pos, x.Read<Translation>().Value))
            .FirstOrDefault(x => math.distance(pos, x.Read<Translation>().Value) < DISTANCE_TO_DESTROY);
        spawnedPortals.Dispose();

        if (closestPortal == Entity.Null) return false;

        var closestChunkPortal = closestPortal.Read<ChunkPortal>();

        var connectedPortal = GetConnectedPortal(closestChunkPortal);

        RemovePortalEntry(closestChunkPortal);
        if (closestPortal.Has<AttachedBuffer>())
        {
            var attachedBuffer = Core.EntityManager.GetBuffer<AttachedBuffer>(closestPortal);
            for (var i = 0; i < attachedBuffer.Length; i++)
            {
                var attachedEntity = attachedBuffer[i].Entity;
                if (attachedEntity == Entity.Null) continue;
                Core.EntityManager.DestroyEntity(attachedEntity);
            }
        }
        Core.EntityManager.DestroyEntity(closestPortal);

        if (connectedPortal != Entity.Null)
        {
            RemovePortalEntry(connectedPortal.Read<ChunkPortal>());

            if (connectedPortal.Has<AttachedBuffer>())
            {
                var attachedBuffer = Core.EntityManager.GetBuffer<AttachedBuffer>(connectedPortal);
                for (var i = 0; i < attachedBuffer.Length; i++)
                {
                    var attachedEntity = attachedBuffer[i].Entity;
                    if (attachedEntity == Entity.Null) continue;
                    Core.EntityManager.DestroyEntity(attachedEntity);
                }
            }
            Core.EntityManager.DestroyEntity(connectedPortal);
        }
        return true;
    }
}
