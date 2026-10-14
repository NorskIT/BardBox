using UnityEngine;

namespace BardBox;

// Unity clamps AudioSource.volume to 1. Apply extra gain to this emitter's PCM
// buffer only, without changing source clips, song fingerprints or play position.
internal sealed class MusicAmplifier : MonoBehaviour
{
    internal volatile float Gain=1;
    private void OnAudioFilterRead(float[] data,int channels)=>AudioGain.Apply(data,Gain);
}
