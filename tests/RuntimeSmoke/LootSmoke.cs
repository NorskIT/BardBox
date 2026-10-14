using System;
using System.IO;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
internal static class LootSmoke
{
    private static Type Loot=>typeof(BardBox.Plugin).Assembly.GetType("BardBox.TapeLoot")!;
    private static bool win;private static int rolls;
    private static bool ForceRoll(ref bool __result){rolls++;__result=win;return false;}
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Loot: "+message);}
    internal static void Dump(string folder)
    {
        File.WriteAllLines(Path.Combine(folder,"loot-prefabs.txt"),Resources.FindObjectsOfTypeAll<Container>().Select(c=>c.name+" parent="+(c.transform.parent?c.transform.parent.name:"")+" loot="+string.Join(",",c.m_defaultItems.m_drops.Select(d=>d.m_item?d.m_item.name:"null"))).OrderBy(s=>s));
    }
    private static void TryChest(Container c)=>AccessTools.Method(Loot,"AddChestTape").Invoke(null,new object[]{c});
    private static bool IsTape(ItemDrop.ItemData i)=>i.m_dropPrefab && i.m_dropPrefab.name.StartsWith("NorskIT_BardBox_");
    internal static void RunWorld(string folder)
    {
        var random=UnityEngine.Random.state;
        foreach(var pair in new[]{("Eikthyr","trailblazer"),("gd_king","second_wind"),("Bonemass","hauler"),("Dragon","steady_rhythm"),("GoblinKing","needle_guard"),("SeekerQueen","arcane_pulse"),("Fader","war_drums")})
        {
            var prefab=PrefabManager.Instance.GetPrefab(pair.Item1);Check(prefab && prefab.GetComponent<CharacterDrop>(),"Missing boss "+pair.Item1);
            var go=new GameObject(pair.Item1);go.SetActive(false);var drop=go.AddComponent<CharacterDrop>();
            drop.m_drops=new List<CharacterDrop.Drop>(prefab.GetComponent<CharacterDrop>().m_drops);
            int original=drop.m_drops.Count,hits=0,misses=0;
            for(int seed=1;seed<=120;seed++)
            {
                UnityEngine.Random.InitState(seed);
                var list=drop.GenerateDropList();var tapes=list.Where(x=>x.Key.name=="NorskIT_BardBox_"+pair.Item2).ToArray();
                Check(tapes.Length<=1 && tapes.All(x=>x.Value==1),"Boss quantity multiplied");if(tapes.Length==1)hits++;else misses++;
            }
            Check(hits>0 && misses>0,"Missing win or loss "+pair.Item1);
            Check(drop.m_drops.Count==original+1,"Duplicate or removed vanilla drops");
            var entry=drop.m_drops.Last();Check(entry.m_chance==.33f && entry.m_dontScale && !entry.m_onePerPlayer && !entry.m_levelMultiplier,"Boss drop policy");
            UnityEngine.Object.DestroyImmediate(go);
        }
        UnityEngine.Random.state=random;
        var harmony=new Harmony("norskit_bardbox_loot_test");
        harmony.Patch(AccessTools.Method(Loot,"ChestRoll"),prefix:new HarmonyMethod(typeof(LootSmoke),nameof(ForceRoll)));
        var spawned=new List<GameObject>();
        try
        {
            foreach(var pair in new[]{("TreasureChest_forestcrypt","stonebeat"),("TreasureChest_sunkencrypt","heartbeat"),("TreasureChest_mountaincave","iron_chorus"),("TreasureChest_heath","tailwind")})
            {
                var prefab=PrefabManager.Instance.GetPrefab(pair.Item1);Check(prefab && prefab.GetComponent<Container>(),"Missing chest "+pair.Item1);
                win=true;int before=rolls;
                var go=UnityEngine.Object.Instantiate(prefab,Player.m_localPlayer.transform.position+Vector3.up*5,Quaternion.identity);spawned.Add(go);
                var c=go.GetComponent<Container>();var view=go.GetComponent<ZNetView>();var inv=c.GetInventory();
                Check(rolls==before+1,"New chest must roll once");
                Check(inv.GetAllItems().Count(i=>i.m_dropPrefab && i.m_dropPrefab.name=="NorskIT_BardBox_"+pair.Item2)==1,"Wrong/missing chest tape");
                Check(inv.GetAllItems().Any(i=>!IsTape(i)),"Vanilla loot removed");Check(inv.GetAllItems().Where(IsTape).All(i=>i.m_quality==1 && i.m_stack==1),"Loot quality/stack");
                TryChest(c);Check(rolls==before+1,"Existing chest rerolled");
                var loaded=new Inventory("reload",null,c.m_width,c.m_height);loaded.Load(new ZPackage(view.GetZDO().GetByteArray(ZDOVars.s_items)));
                Check(loaded.GetAllItems().Count(IsTape)==1,"Tape not persisted in ZDO inventory");
                view.GetZDO().SetOwner(ZNet.GetUID()+100);TryChest(c);Check(rolls==before+1,"Non-owner rolled");view.GetZDO().SetOwner(ZNet.GetUID());TryChest(c);Check(rolls==before+1,"Owner transfer rerolled");
            }
            win=false;
            var failed=UnityEngine.Object.Instantiate(PrefabManager.Instance.GetPrefab("TreasureChest_heath"),Player.m_localPlayer.transform.position+Vector3.up*5,Quaternion.identity);spawned.Add(failed);
            var chest=failed.GetComponent<Container>();Check(!chest.GetInventory().GetAllItems().Any(IsTape),"Failed roll awarded tape");
            int recorded=rolls;win=true;TryChest(chest);Check(rolls==recorded,"Failed roll retried");
            var zdo=failed.GetComponent<ZNetView>().GetZDO();
            zdo.Set("NorskIT_BardBox_chestLootRolled6",false);TryChest(chest);Check(rolls==recorded,"Old vanilla chest backfilled");
            zdo.Set(ZDOVars.s_addedDefaultItems,false);zdo.Set(ZDOVars.s_creator,123L);TryChest(chest);Check(rolls==recorded,"Player-owned chest received loot");zdo.Set(ZDOVars.s_creator,0L);
            var fill=PrefabManager.Instance.GetPrefab("NorskIT_BardBox_trailblazer");
            for(int i=0;i<100 && chest.GetInventory().CanAddItem(fill);i++)chest.GetInventory().AddItem(fill,1);
            int full=chest.GetInventory().NrOfItems();TryChest(chest);Check(chest.GetInventory().NrOfItems()==full,"Full chest items changed");Check(zdo.GetBool("NorskIT_BardBox_chestLootRolled6"),"Full chest may reroll");
            foreach(string id in new[]{"piece_chest_wood","TreasureChest_blackforest","TreasureChest_forestcrypt_hildir","TreasureChest_heath_hildir"})
            {
                int prior=rolls;var go=UnityEngine.Object.Instantiate(PrefabManager.Instance.GetPrefab(id),Player.m_localPlayer.transform.position+Vector3.up*5,Quaternion.identity);spawned.Add(go);Check(rolls==prior,"Excluded chest rolled: "+id);
            }
            File.WriteAllText(Path.Combine(folder,"loot-result.txt"),"PASS: seven native boss prefabs, deterministic wins/losses, single unscaled drop, idempotent registration; four actual chest prefabs with native loot, owner-only single roll, persisted inventory, failed/old/player/full/excluded chest cases; existing item IDs and quality preserved.");
        }
        finally {harmony.UnpatchSelf();foreach(var go in spawned)if(go){var view=go.GetComponent<ZNetView>();if(view && view.IsValid())view.Destroy();else UnityEngine.Object.Destroy(go);}}
    }

    internal static IEnumerator Network(bool host,string coord,string folder)
    {
        string metadata=Path.Combine(coord,"loot-ids.bin"),seen=Path.Combine(coord,"loot-seen.txt"),owned=Path.Combine(coord,"loot-owner.txt");
        Container? chest=null;GameObject? item=null;
        if(host)
        {
            var h=new Harmony("norskit_bardbox_network_loot_test");win=true;
            h.Patch(AccessTools.Method(Loot,"ChestRoll"),prefix:new HarmonyMethod(typeof(LootSmoke),nameof(ForceRoll)));
            try{chest=UnityEngine.Object.Instantiate(PrefabManager.Instance.GetPrefab("TreasureChest_heath"),Player.m_localPlayer.transform.position+Vector3.right*4,Quaternion.identity).GetComponent<Container>();}
            finally{h.UnpatchSelf();}
            // Use the native boss generator and its native item-spawn path.
            var boss=new GameObject("Eikthyr");boss.SetActive(false);var drops=boss.AddComponent<CharacterDrop>();
            var random=UnityEngine.Random.state;List<KeyValuePair<GameObject,int>> result=null!;
            for(int seed=0;seed<100;seed++){UnityEngine.Random.InitState(seed);result=drops.GenerateDropList();if(result.Count>0)break;}
            UnityEngine.Random.state=random;Check(result.Count==1 && result[0].Value==1,"Network boss drop missing");
            CharacterDrop.DropItems(result,Player.m_localPlayer.transform.position+Vector3.right*5,0,false);UnityEngine.Object.DestroyImmediate(boss);
            item=UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None).First(x=>Utils.GetPrefabName(x.gameObject)=="NorskIT_BardBox_trailblazer" && Vector3.Distance(x.transform.position,Player.m_localPlayer.transform.position)<8).gameObject;
            var p=new ZPackage();p.Write(chest.GetComponent<ZNetView>().GetZDO().m_uid);p.Write(item.GetComponent<ZNetView>().GetZDO().m_uid);File.WriteAllBytes(metadata,p.GetArray());
        }
        double deadline=Time.realtimeSinceStartup+30;
        while(!File.Exists(metadata)){Check(Time.realtimeSinceStartup<deadline,"Loot metadata timeout");yield return new WaitForSecondsRealtime(.1f);}
        var packet=new ZPackage(File.ReadAllBytes(metadata));var chestId=packet.ReadZDOID();var itemId=packet.ReadZDOID();
        if(!host)
        {
            while(!chest || !item)
            {
                var go=ZNetScene.instance.FindInstance(chestId);if(go)chest=go.GetComponent<Container>();item=ZNetScene.instance.FindInstance(itemId);
                Check(Time.realtimeSinceStartup<deadline,"Replicated loot objects timeout");yield return new WaitForSecondsRealtime(.1f);
            }
            while(!chest!.GetInventory().GetAllItems().Any(IsTape)){Check(Time.realtimeSinceStartup<deadline,"Replicated chest inventory timeout");yield return new WaitForSecondsRealtime(.1f);}
            Check(chest.GetInventory().GetAllItems().Count(IsTape)==1 && chest.GetInventory().GetAllItems().First(IsTape).m_dropPrefab.name=="NorskIT_BardBox_tailwind","Client chest differs");
            Check(Utils.GetPrefabName(item!)=="NorskIT_BardBox_trailblazer" && item!.GetComponent<ItemDrop>().m_itemData.m_quality==1,"Client boss drop differs");
            File.WriteAllText(seen,"PASS");
            while(!chest.IsOwner()){Check(Time.realtimeSinceStartup<deadline,"Chest ownership transfer timeout");yield return new WaitForSecondsRealtime(.1f);}
            TryChest(chest);Check(chest.GetInventory().GetAllItems().Count(IsTape)==1,"Ownership transfer duplicated loot");File.WriteAllText(owned,"PASS");
        }
        else
        {
            while(!File.Exists(seen)){Check(Time.realtimeSinceStartup<deadline,"Client loot check timeout");yield return new WaitForSecondsRealtime(.1f);}
            chest!.GetComponent<ZNetView>().GetZDO().SetOwner(ZNet.instance.GetPeers().First(p=>p.IsReady()).m_uid);
            while(!File.Exists(owned)){Check(Time.realtimeSinceStartup<deadline,"Client ownership check timeout");yield return new WaitForSecondsRealtime(.1f);}
        }
        File.WriteAllText(Path.Combine(folder,"network-loot-result.txt"),"PASS: native boss drop and chest tape replicated as the same ZDOs; one shared level-I tape; chest ownership transfer does not reroll.");
    }
}
