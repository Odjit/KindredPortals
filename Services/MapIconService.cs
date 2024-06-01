using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KindredPortals.Services;
class MapIconService
{
    PrefabGUID mapIconProxyPrefabGUID;
    Entity mapIconProxyPrefab;
    EntityQuery mapIconProxyQuery;

    public MapIconService()
    {
        if (!Core.PrefabCollection._NameToPrefabGuidDictionary.TryGetValue("MapIcon_ProxyObject_POI_Unknown", out mapIconProxyPrefabGUID))
            Core.Log.LogError("Failed to find MapIcon_ProxyObject_POI_Unknown PrefabGUID");
        if (!Core.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(mapIconProxyPrefabGUID, out mapIconProxyPrefab))
            Core.Log.LogError("Failed to find MapIcon_ProxyObject_POI_Unknown Prefab entity");

        mapIconProxyQuery = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] {
                ComponentType.ReadOnly<AttachMapIconsToEntity>(),
                ComponentType.ReadOnly<SpawnedBy>(),
            },
            None = new ComponentType[]
            {
                ComponentType.ReadOnly<ChunkPortal>(),
                ComponentType.ReadOnly<ChunkWaypoint>(),
            },
            Options = EntityQueryOptions.IncludeDisabled
        });
    }

    public void CreateMapIcon(Entity characterEntity, PrefabGUID mapIcon)
    {
        var pos = characterEntity.Read<Translation>().Value;
        var mapIconProxy = Core.EntityManager.Instantiate(mapIconProxyPrefab);
        mapIconProxy.Write(new Translation { Value = pos });

        mapIconProxy.Add<SpawnedBy>();
        mapIconProxy.Write(new SpawnedBy { Value = characterEntity });

        mapIconProxy.Remove<SyncToUserBitMask>();
        mapIconProxy.Remove<SyncToUserBuffer>();
        mapIconProxy.Remove<OnlySyncToUsersTag>();

        var attachMapIconsToEntity = Core.EntityManager.GetBuffer<AttachMapIconsToEntity>(mapIconProxy);
        attachMapIconsToEntity.Clear();
        attachMapIconsToEntity.Add(new() { Prefab = mapIcon });
    }

    public bool RemoveMapIcon(Entity characterEntity)
    {
        const float DISTANCE_TO_DESTROY = 5f;
        var pos = characterEntity.Read<Translation>().Value;
        var mapIconProxies = mapIconProxyQuery.ToEntityArray(Allocator.Temp);
        var iconToDestroy = mapIconProxies.ToArray()
            .Where(x => x.Has<PrefabGUID>() && x.Read<PrefabGUID>().Equals(mapIconProxyPrefabGUID))
            .OrderBy(x => math.distance(pos, x.Read<Translation>().Value))
            .FirstOrDefault(x => math.distance(pos, x.Read<Translation>().Value) < DISTANCE_TO_DESTROY);
        mapIconProxies.Dispose();

        if (iconToDestroy == Entity.Null)
            return false;

        if (iconToDestroy.Has<AttachedBuffer>())
        {
            var attachedBuffer = Core.EntityManager.GetBuffer<AttachedBuffer>(iconToDestroy);
            for(var i = 0; i < attachedBuffer.Length; i++)
            {
                var attachedEntity = attachedBuffer[i].Entity;
                if (attachedEntity == Entity.Null) continue;
                Core.EntityManager.DestroyEntity(attachedEntity);
            }
        }
        
        Core.EntityManager.DestroyEntity(iconToDestroy);
        return true;
    }
}
