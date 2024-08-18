using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using KindredPortals.Commands.Converters;
using KindredPortals.Services;
using ProjectM;
using ProjectM.Physics;
using ProjectM.Scripting;
using ProjectM.Terrain;
using Stunlock.Core;
using Stunlock.Localization;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace KindredPortals;

internal static class Core
{
	public static World Server { get; } = GetWorld("Server") ?? throw new System.Exception("There is no Server world (yet). Did you install a server mod on the client?");

	public static EntityManager EntityManager { get; } = Server.EntityManager;
    public static ChunkObjectManager ChunkObjectManager { get; } = Server.GetExistingSystemManaged<ChunkObjectManager>();
    public static PrefabCollectionSystem PrefabCollection { get; } = Server.GetExistingSystemManaged<PrefabCollectionSystem>();
    public static ServerGameSettingsSystem ServerGameSettingsSystem { get; internal set; }
    public static ServerScriptMapper ServerScriptMapper { get; internal set; }
    public static double ServerTime => ServerGameManager.ServerTime;
    public static ServerGameManager ServerGameManager => ServerScriptMapper.GetServerGameManager();
	public static ManualLogSource Log { get; } = Plugin.LogInstance;

    public static LocalizationService LocalizationService { get; } = new();
    public static MapIconService MapIconService { get; internal set; }
    public static PortalService PortalService { get; } = new();
    public static WaygateService WaygateService { get; internal set; }

    static MonoBehaviour monoBehaviour;

    public static void LogException(System.Exception e, [CallerMemberName] string caller = null)
	{
		Core.Log.LogError($"Failure in {caller}\nMessage: {e.Message} Inner:{e.InnerException?.Message}\n\nStack: {e.StackTrace}\nInner Stack: {e.InnerException?.StackTrace}");
	}

	internal static void InitializeAfterLoaded()
	{
		if (_hasInitialized) return;

        ServerGameSettingsSystem = Server.GetExistingSystemManaged<ServerGameSettingsSystem>();
        ServerScriptMapper = Server.GetExistingSystemManaged<ServerScriptMapper>();

        FoundMapIconConverter.Initialize();
        FoundWaygatePrefabConverter.Initialize();

        MapIconService = new();
        WaygateService = new();

        _hasInitialized = true;
		Log.LogInfo($"KindredPortals initialized");
    }
	private static bool _hasInitialized = false;

	private static World GetWorld(string name)
	{
		foreach (var world in World.s_AllWorlds)
		{
			if (world.Name == name)
			{
				return world;
			}
		}

		return null;
    }

    public static Coroutine StartCoroutine(IEnumerator routine)
    {
        if (monoBehaviour == null)
        {
            var go = new GameObject("KindredExtract");
            monoBehaviour = go.AddComponent<IgnorePhysicsDebugSystem>();
            Object.DontDestroyOnLoad(go);
        }

        return monoBehaviour.StartCoroutine(routine.WrapToIl2Cpp());
    }

    public static void StopCoroutine(Coroutine coroutine)
    {
        if (monoBehaviour == null)
        {
            return;
        }

        monoBehaviour.StopCoroutine(coroutine);
    }
}
