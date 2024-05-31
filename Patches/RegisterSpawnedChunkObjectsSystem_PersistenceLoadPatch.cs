using HarmonyLib;
using ProjectM;
using ProjectM.Shared.Systems;
using ProjectM.Terrain;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace KindredPortals.Patches;

[HarmonyPatch(typeof(RegisterSpawnedChunkObjectsSystem<PersistenceV2.LoadedTag>), nameof(RegisterSpawnedChunkObjectsSystem<PersistenceV2.LoadedTag>.OnUpdate))]
class RegisterSpawnedChunkObjectsSystem_PersistenceLoadPatch
{
    static Dictionary<Entity, ChunkPortal> entityChunkPortals = [];
    public static void Prefix()
    {
        var portalQuery = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] { ComponentType.ReadOnly<ChunkPortal>() },
            Options = EntityQueryOptions.IncludeDisabled
        });

        var entities = portalQuery.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            var chunkPortal = entity.Read<ChunkPortal>();
            entityChunkPortals.Add(entity, chunkPortal);
            // RegisterSpawnedChunkObjectsSystem will throw an exception if this is 4 or greater
            chunkPortal.FromChunkPortalIndex = 0;
            entity.Write(chunkPortal);
        }
        entities.Dispose();
        portalQuery.Dispose();
    }

    public static void Postfix()
    {
        foreach (var (entity, chunkPortal) in entityChunkPortals)
        {
            if(!Core.ChunkObjectManager._ChunkPortals.TryGetValue(chunkPortal.FromChunk, out var portalDataList))
            {
                Core.Log.LogError($"Missing portalData for chunk {chunkPortal.FromChunk}");
                continue;
            }

            // Write back out the correct FromChunkPortalIndex
            entity.Write(chunkPortal);

            var newData = new ChunkPortalData
            {
                PortalEntity = entity,
                PortalPosition = entity.Read<Translation>().Value,
                PortalRotation = entity.Read<Rotation>().Value,
                InPositionOffset = chunkPortal.InPositionOffset,
                ToChunk = chunkPortal.ToChunk,
                ToChunkPortalIndex = chunkPortal.ToChunkPortalIndex
            };

            if (chunkPortal.FromChunkPortalIndex < portalDataList.Length)
            {
                portalDataList[chunkPortal.FromChunkPortalIndex] = newData;
            }
            else
            {
                portalDataList.Length = (ushort)(chunkPortal.FromChunkPortalIndex + 1);
                portalDataList[chunkPortal.FromChunkPortalIndex] = newData;
            }

            Core.ChunkObjectManager._ChunkPortals.Remove(chunkPortal.FromChunk);
            Core.ChunkObjectManager._ChunkPortals.Add(chunkPortal.FromChunk, portalDataList);
        }
        entityChunkPortals.Clear();
    }
}
