using KindredPortals.Commands.Converters;
using ProjectM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VampireCommandFramework;

namespace KindredPortals.Commands;

[CommandGroup("mapicon", "Commands for managing map icons.")]
class MapIconCommands
{
    [Command("create", "Creates a map icon at the player's location.", adminOnly: true)]
    public static void CreateMapIcon(ChatCommandContext ctx, FoundMapIcon foundMapIcon)
    {
        Core.MapIconService.CreateMapIcon(ctx.Event.SenderCharacterEntity, foundMapIcon.Value);
        ctx.Reply("Map icon created");
    }

    [Command("destroy", "Destroys a map icon you're standing on", adminOnly: true)]
    public static void DestroyMapIcon(ChatCommandContext ctx)
    {
        if(Core.MapIconService.RemoveMapIcon(ctx.Event.SenderCharacterEntity))
            ctx.Reply("Destroyed map icon");
        else
            ctx.Reply("Not standing on a map icon");
    }

    [Command("list", "Lists all map icon prefabs with text", adminOnly: true)]
    public static void ListMapIcons(ChatCommandContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Map Icons:");
        foreach (var (prefabGUID, name) in Core.PrefabCollection._PrefabGuidToNameDictionary)
        {
            if (!name.StartsWith("MapIcon_"))
                continue;

            if (!Core.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(prefabGUID, out var entity))
                continue;

            if (!entity.Has<MapIconData>())
                continue;

            var nameTrimmed = name["MapIcon_".Length..];
            var nextLine = $"<color=yellow>{nameTrimmed}</color> - ";
            var mapIconData = entity.Read<MapIconData>();
            if (!mapIconData.HeaderLocalizedKey.IsEmpty)
                nextLine += $"<color=white>{Core.LocalizationService.GetLocalization(mapIconData.HeaderLocalizedKey.GetGuid().ToGuid().ToString())}</color>";
            else
                nextLine += $"No Label";

            if (sb.Length + nextLine.Length > 500)
            {
                ctx.Reply("\n" + sb.ToString());
                sb.Clear();
            }

            sb.AppendLine(nextLine);
        }
        ctx.Reply("\n" + sb.ToString());
    }
}
