using System;
using System.Linq;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
namespace BardBox;

internal static class TapeLoot
{
    internal static readonly (string Boss,string Tape)[] Bosses={
        ("Eikthyr","trailblazer"),("gd_king","second_wind"),("Bonemass","hauler"),
        ("Dragon","steady_rhythm"),("GoblinKing","needle_guard"),("SeekerQueen","arcane_pulse"),("Fader","war_drums")
    };
    private const string RolledKey=Catalog.Prefix+"chestLootRolled6";
    internal static bool ChestRoll()=>LootRules.Wins(UnityEngine.Random.value,LootRules.ChestChance);
    internal static void AddChestTape(Container chest)
    {
        if(!Plugin.Ready || !chest.IsOwner())return;
        var view=chest.m_rootObjectOverride?chest.m_rootObjectOverride.GetComponent<ZNetView>():chest.GetComponent<ZNetView>();
        var zdo=view?view.GetZDO():null;
        if(zdo==null || zdo.GetBool(ZDOVars.s_addedDefaultItems) || zdo.GetBool(RolledKey) || zdo.GetLong(ZDOVars.s_creator)!=0)return;
        var piece=chest.GetComponent<Piece>();if(piece && piece.IsPlacedByPlayer())return;
        // These are the dedicated vanilla dungeon/village chest prefabs, not biome-wide matches.
        var tape=LootRules.ChestTape(Utils.GetPrefabName(chest.gameObject));if(tape==null)return;
        zdo.Set(RolledKey,true);
        if(!ChestRoll())return;
        var prefab=PrefabManager.Instance.GetPrefab(Catalog.Prefix+tape);
        if(!prefab){Plugin.Instance.Warn("Missing tape prefab for chest: "+tape);return;}
        var item=prefab.GetComponent<ItemDrop>().m_itemData.Clone();item.m_dropPrefab=prefab;item.m_quality=1;item.m_stack=1;
        item.m_cheated=zdo.GetBool(ZDOVars.s_cheated) && !PlayerProfile.s_bypassCheatChecks;
        if(!chest.GetInventory().AddItem(item))Plugin.Instance.Warn("Tape loot skipped: chest is full ("+Utils.GetPrefabName(chest.gameObject)+").");
    }
    internal static void ConfigureBoss(CharacterDrop drop)
    {
        var id=Utils.GetPrefabName(drop.gameObject);
        var tape=Bosses.FirstOrDefault(x=>x.Boss==id).Tape;
        if(tape==null)return;
        var prefab=PrefabManager.Instance.GetPrefab(Catalog.Prefix+tape);
        if(!prefab || drop.m_drops.Any(d=>d.m_prefab && d.m_prefab.name==prefab.name))return;
        drop.m_drops.Add(new CharacterDrop.Drop{m_prefab=prefab,m_amountMin=1,m_amountMax=1,m_chance=LootRules.BossChance,m_onePerPlayer=false,m_levelMultiplier=false,m_dontScale=true});
    }
}
[HarmonyPatch(typeof(CharacterDrop),nameof(CharacterDrop.GenerateDropList))]
internal static class BossTapeDrops
{
    private static void Prefix(CharacterDrop __instance){if(Plugin.Ready)TapeLoot.ConfigureBoss(__instance);}
}
[HarmonyPatch(typeof(Container),"AddDefaultItems")]
internal static class ChestTapeDrops
{
    private static void Postfix(Container __instance)=>TapeLoot.AddChestTape(__instance);
}
