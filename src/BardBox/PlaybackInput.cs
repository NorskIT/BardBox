namespace BardBox;

// Only input policy; interruptions always require release before another start.
internal sealed class PlaybackInput
{
    internal bool Playing {get;private set;}
    private bool pressed,latched,toggle;
    internal void Interrupt(){Playing=false;latched=true;}
    internal void Update(bool hold,bool interrupted,bool useToggle)
    {
        if(useToggle!=toggle)
        {toggle=useToggle;Playing=false;latched=hold;pressed=hold;return;}
        if(!hold)
        {pressed=false;latched=false;if(!toggle || interrupted)Playing=false;return;}
        if(interrupted){Interrupt();pressed=true;return;}
        bool rising=!pressed;pressed=true;
        if(latched)return;
        if(toggle){if(rising)Playing=!Playing;}
        else Playing=true;
    }
}
