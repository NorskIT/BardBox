using System;
using System.Collections.Generic;

namespace BardBox;

internal enum Bonus { Speed, StaminaRegen, Blunt, Carry, HealthRegen, StaminaUse, Slash, Sail, Pierce, EitrRegen, Damage }
internal sealed class Tape
{
    public string Id, Name, Trophy;
    public Bonus Bonus;
    public float[] Values;
    public (string Item, int Count)[] Materials;
    public Tape(string id,string name,Bonus bonus,float a,float b,float c,string trophy,params (string,int)[] materials)
    { Id=id;Name=name;Bonus=bonus;Values=new[]{a,b,c};Trophy=trophy;Materials=materials; }
    public string Prefab => Catalog.Prefix+Id;
    public string Effect => Catalog.Prefix+"SE_"+Id;
    public float Value(int quality) => Values[Math.Max(0,Math.Min(2,quality-1))];
    public string HudName(int q)
    {
        var v=Value(q).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture);
        string label=Bonus switch {
            Bonus.Speed=>"Movement speed +"+v+"%",
            Bonus.StaminaRegen=>"Stamina regen +"+v+"%",
            Bonus.HealthRegen=>"Health regen +"+v+"%",
            Bonus.EitrRegen=>"Eitr regen +"+v+"%",
            Bonus.Carry=>"Carry weight +"+v,
            Bonus.Damage=>"Physical damage +"+v+"%",
            Bonus.StaminaUse=>"Stamina cost -"+v+"%",
            Bonus.Sail=>"Sail force +"+v+"%",
            _=>Bonus+" damage taken -"+v+"%"
        };
        return "♫ "+label;
    }
    public string Description(int q)
    {
        var v=Value(q).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture);
        switch(Bonus)
        {
            case Bonus.Speed:return "+"+v+"% land movement speed";
            case Bonus.StaminaRegen:return "+"+v+"% stamina regeneration";
            case Bonus.HealthRegen:return "+"+v+"% natural health regeneration";
            case Bonus.EitrRegen:return "+"+v+"% eitr regeneration";
            case Bonus.Carry:return "+"+v+" carry weight";
            case Bonus.Damage:return "+"+v+"% physical attack damage";
            case Bonus.StaminaUse:return "-"+v+"% stamina cost: run, jump, attack, block, dodge, swim and sneak";
            case Bonus.Sail:return "+"+v+"% sail force on the musician's boat";
            default:return "-"+v+"% incoming "+Bonus.ToString().ToLowerInvariant()+" attack damage (before armor)";
        }
    }
}
internal static class Catalog
{
    public const string Prefix="NorskIT_BardBox_", Box=Prefix+"Boombox", Blank=Prefix+"BlankTape", Ammo="NorskIT_BardBox_Tape";
    public static readonly Tape[] Tapes={
        new Tape("trailblazer","Trailblazer",Bonus.Speed,5,7.5f,10,"TrophyEikthyr",("Wood",5),("Resin",5),("DeerHide",2)),
        new Tape("second_wind","Second Wind",Bonus.StaminaRegen,15,20,25,"TrophyTheElder",("Copper",2),("GreydwarfEye",5)),
        new Tape("stonebeat","Stonebeat",Bonus.Blunt,5,10,15,"TrophyTheElder",("BoneFragments",5),("Stone",5)),
        new Tape("hauler","Hauler",Bonus.Carry,150,300,500,"TrophyBonemass",("Iron",2),("ElderBark",5)),
        new Tape("heartbeat","Heartbeat",Bonus.HealthRegen,15,25,35,"TrophyBonemass",("Iron",2),("Bloodbag",5)),
        new Tape("steady_rhythm","Steady Rhythm",Bonus.StaminaUse,5,10,15,"TrophyDragonQueen",("Silver",2),("WolfFang",4)),
        new Tape("iron_chorus","Iron Chorus",Bonus.Slash,5,10,15,"TrophyDragonQueen",("Silver",2),("WolfPelt",4)),
        new Tape("tailwind","Tailwind",Bonus.Sail,10,15,20,"",("BlackMetal",2),("LinenThread",5),("Needle",2)),
        new Tape("needle_guard","Needle Guard",Bonus.Pierce,5,10,15,"TrophyGoblinKing",("BlackMetal",2),("Needle",5)),
        new Tape("arcane_pulse","Arcane Pulse",Bonus.EitrRegen,15,20,25,"TrophySeekerQueen",("Eitr",2),("RoyalJelly",5)),
        new Tape("war_drums","War Drums",Bonus.Damage,5,10,15,"TrophyFader",("FlametalNew",2),("AskHide",5))
    };
    public static Tape? Find(string id) => Array.Find(Tapes,t=>t.Id==id || t.Prefab==id);
}

// No Unity dependency: receiver-local source expiry and strongest-quality selection.
internal sealed class Contributions
{
    private sealed class Entry { public string Tape=""; public int Quality; public double Until; }
    private readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
    public void Observe(string source,string tape,int quality,double now)
    {
        if(Catalog.Find(tape)==null || quality<1 || quality>3)return;
        entries[source+"/"+tape]=new Entry {Tape=tape,Quality=quality,Until=now+2};
    }
    public Dictionary<string,int> Resolve(double now)
    {
        var result=new Dictionary<string,int>();var expired=new List<string>();
        foreach(var pair in entries)
        {
            if(pair.Value.Until<=now){expired.Add(pair.Key);continue;}
            if(!result.TryGetValue(pair.Value.Tape,out var q)||q<pair.Value.Quality)result[pair.Value.Tape]=pair.Value.Quality;
        }
        foreach(var key in expired)entries.Remove(key);
        return result;
    }
    public void Clear()=>entries.Clear();
}
