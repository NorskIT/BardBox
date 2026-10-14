using UnityEngine;

namespace BardBox;

public sealed class MusicEffect : SE_Stats
{
    internal Tape Definition=null!;
    internal int Quality;
    public override void SetLevel(int itemLevel,float skillLevel)
    {
        Quality=Mathf.Clamp(itemLevel,1,3);
        var value=Definition.Value(Quality);var fraction=value/100f;
        m_name=Definition.HudName(Quality);
        m_tooltip=Definition.Name+" - Quality "+Quality+"/3\n"+Definition.Description(Quality)+"\nMusic aura · 20 m · fades after 2 seconds";
        m_staminaRegenMultiplier=Definition.Bonus==Bonus.StaminaRegen?1+fraction:1;
        m_healthRegenMultiplier=Definition.Bonus==Bonus.HealthRegen?1+fraction:1;
        m_eitrRegenMultiplier=Definition.Bonus==Bonus.EitrRegen?1+fraction:1;
        m_addMaxCarryWeight=Definition.Bonus==Bonus.Carry?value:0;
        var drain=Definition.Bonus==Bonus.StaminaUse?-fraction:0;
        m_runStaminaDrainModifier=drain;m_jumpStaminaUseModifier=drain;m_attackStaminaUseModifier=drain;
        m_blockStaminaUseModifier=drain;m_dodgeStaminaUseModifier=drain;m_swimStaminaUseModifier=drain;m_sneakStaminaUseModifier=drain;
        m_percentigeDamageModifiers=default;
        if(Definition.Bonus==Bonus.Damage)
        {m_percentigeDamageModifiers.m_blunt=fraction;m_percentigeDamageModifiers.m_slash=fraction;m_percentigeDamageModifiers.m_pierce=fraction;}
    }
    public override void ModifySpeed(float baseSpeed,ref float speed,Character character,Vector3 dir)
    {
        if(Definition.Bonus==Bonus.Speed && !character.IsSwimming())speed+=baseSpeed*Definition.Value(Quality)/100f;
    }
    public override void OnDamaged(HitData hit,Character attacker)
    {
        if(!attacker)return;
        float factor=1-Definition.Value(Quality)/100f;
        switch(Definition.Bonus)
        {
            case Bonus.Blunt:hit.m_damage.m_blunt*=factor;break;
            case Bonus.Slash:hit.m_damage.m_slash*=factor;break;
            case Bonus.Pierce:hit.m_damage.m_pierce*=factor;break;
        }
    }
    public override string GetTooltipString()=>m_tooltip;
}
