namespace BardBox;
internal static class LootRules
{
    internal const float BossChance=.33f,ChestChance=.01f;
    internal static string? ChestTape(string prefab)=>prefab switch {
        "TreasureChest_forestcrypt"=>"stonebeat",
        "TreasureChest_sunkencrypt"=>"heartbeat",
        "TreasureChest_mountaincave"=>"iron_chorus",
        "TreasureChest_heath"=>"tailwind",
        _=>null
    };
    internal static bool Wins(float roll,float chance)=>roll>=0 && roll<chance;
}
