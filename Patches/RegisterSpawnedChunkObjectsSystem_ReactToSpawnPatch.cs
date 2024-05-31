using HarmonyLib;
using ProjectM;
using ProjectM.Shared.Systems;
using ProjectM.Terrain;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace KindredPortals.Patches;
[HarmonyPatch(typeof(RegisterSpawnedChunkObjectsSystem<SpawnTag>), nameof(RegisterSpawnedChunkObjectsSystem<SpawnTag>.OnUpdate))]
static class RegisterSpawnedChunkObjectsSystem_ReactToSpawnPatch
{
    static Dictionary<TerrainChunk, (FixedList512Bytes<ChunkPortalData> initialList, List<(int, ChunkPortalData)> additions)> data = [];

    public static void Prefix(RegisterSpawnedChunkObjectsSystem<SpawnTag> __instance)
    {
        var entities = __instance._ChunkPortalQuery.ToEntityArray(Allocator.Temp);
        foreach(var entity in entities)
        {
            var chunkPortal = entity.Read<ChunkPortal>();
            if(!data.TryGetValue(chunkPortal.FromChunk, out var portalData))
            {
                portalData = new();

                if(Core.ChunkObjectManager._ChunkPortals.TryGetValue(chunkPortal.FromChunk, out var portalDataList))
                {
                    portalData.initialList = portalDataList;
                }

                portalData.additions = [];
                data.Add(chunkPortal.FromChunk, portalData);
            }

            portalData.additions.Add((chunkPortal.FromChunkPortalIndex, new()
            {
                PortalEntity = entity,
                PortalPosition = entity.Read<Translation>().Value,
                PortalRotation = entity.Read<Rotation>().Value,
                InPositionOffset = chunkPortal.InPositionOffset,
                ToChunk = chunkPortal.ToChunk,
                ToChunkPortalIndex = chunkPortal.ToChunkPortalIndex
            }));

            // Make things happy for RegisterSpawnedChunkObjectsSystem_ReactToSpawn
            chunkPortal.FromChunkPortalIndex = 0;
            entity.Write(chunkPortal);
        }
        entities.Dispose();
    }

    public static void Postfix(RegisterSpawnedChunkObjectsSystem<SpawnTag> __instance)
    {
        foreach(var (chunk, (initialList, additions)) in data)
        {
            var portalDataList = initialList;
            var largestIndex = additions.Select(x => x.Item1).Max();

            if (largestIndex >= portalDataList.Length)
                portalDataList.length = (ushort)(largestIndex + 1);

            foreach (var (index, data) in additions)
            {
                portalDataList[index] = data;

                // Save it back out
                var chunkPortal = data.PortalEntity.Read<ChunkPortal>();
                chunkPortal.FromChunkPortalIndex = index;
                data.PortalEntity.Write(chunkPortal);
            }

            Core.ChunkObjectManager._ChunkPortals.Remove(chunk);
            Core.ChunkObjectManager._ChunkPortals.Add(chunk, portalDataList);

        }
    }
}
