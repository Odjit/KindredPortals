using KindredPortals.Commands;
using ProjectM;
using ProjectM.Network;
using ProjectM.Terrain;
using Stunlock.Core;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KindredPortals.Services;
class PortalService
{
    Entity portalPrefab;

    Dictionary<Entity, (float3 pos, quaternion rot, TerrainChunk chunk, int index, PrefabGUID mapIcon)> portalStartPos = [];

    public PortalService()
    {
        if(!Core.PrefabCollection._NameToPrefabGuidDictionary.TryGetValue("TM_General_Entrance_Gate", out var portalPrefabGUID))
            Core.Log.LogError("Failed to find TM_General_Entrance_Gate PrefabGUID");
        if(!Core.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(portalPrefabGUID, out portalPrefab))
            Core.Log.LogError("Failed to find TM_General_Entrance_Gate Prefab entity");
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

        CreatePortal(start.pos, start.rot, start.chunk, start.index, start.mapIcon, endChunk, endIndex);

        var endRot = playerEntity.Read<Rotation>().Value;
        CreatePortal(endPos, endRot, endChunk, endIndex, endMapIcon, start.chunk, start.index);

        Core.Log.LogInfo($"Start: {start.chunk} {start.index} End: {endChunk} {endIndex}");
        return null;
    }

    private void CreatePortal(float3 pos, quaternion rot, TerrainChunk chunk, int index, PrefabGUID mapIcon, TerrainChunk toChunk, int toIndex)
    {
        var startPortal = Core.EntityManager.Instantiate(portalPrefab);
        startPortal.Write(new Translation { Value = pos });
        startPortal.Write(new Rotation { Value = rot });

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
}
