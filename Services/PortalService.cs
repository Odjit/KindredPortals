using KindredPortals.Data;
using ProjectM;
using ProjectM.Terrain;
using System.Collections.Generic;
using System.Text;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KindredPortals.Services;
class PortalService
{
    Entity portalPrefab;

    Dictionary<Entity, (float3 pos, quaternion rot, TerrainChunk chunk, int index)> portalStartPos = [];

    public PortalService()
    {
        Core.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(Prefabs.TM_General_Entrance_Gate, out portalPrefab);
    }

    public bool StartPortal(Entity playerEntity)
    {
        var pos = playerEntity.Read<Translation>().Value;
        var chunk = new TerrainChunk { X = (sbyte)((pos.x + 3200) / 160), Y = (sbyte)((pos.z + 3200) / 160) };
        var index = GetNextAvailableIndex(chunk);

        if(index >= 9)
            return false;

        portalStartPos[playerEntity] = (pos, playerEntity.Read<Rotation>().Value, chunk, index);
        return true;
    }

    public string EndPortal(Entity playerEntity)
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

        var startPortal = Core.EntityManager.Instantiate(portalPrefab);
        startPortal.Write(new Translation { Value = start.pos });
        startPortal.Write(new Rotation { Value = start.rot });

        var endRot = playerEntity.Read<Rotation>().Value;
        var endPortal = Core.EntityManager.Instantiate(portalPrefab);
        endPortal.Write(new Translation { Value = endPos });
        endPortal.Write(new Rotation { Value = endRot });

        Core.Log.LogInfo($"Start: {start.chunk} {start.index} End: {endChunk} {endIndex}");

        startPortal.Write(new ChunkPortal { FromChunk = start.chunk, FromChunkPortalIndex = start.index, ToChunk = endChunk, ToChunkPortalIndex = endIndex });
        endPortal.Write(new ChunkPortal { FromChunk = endChunk, FromChunkPortalIndex = endIndex, ToChunk = start.chunk, ToChunkPortalIndex = start.index });
        return null;
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
