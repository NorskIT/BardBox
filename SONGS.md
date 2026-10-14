# Song files and tape mapping

The 0.1.3 test package contains eleven supplied songs, one per tape.
The single source of song mapping and tooltip metadata is assets/songs.tsv.
Original MP3 copies are preserved under assets-source/songs/. The Unity builder
imports those sources and exports 44.1 kHz mono PCM16 WAV files.

| Tape | Song | Artist, where identified | WAV file |
|---|---|---|---|
| Trailblazer | fishy | polyphony_digital | trailblazer.wav |
| Second Wind | Rubbin' And Tuggin' My Nips | Obscurest Vinyl | second_wind.wav |
| Stonebeat | Storm-Crowned | Not verified | stonebeat.wav |
| Hauler | Where There's a Whip There's a Way! | Not verified | hauler.wav |
| Heartbeat | brevik sangen | Not verified | heartbeat.wav |
| Steady Rhythm | Noise Of Scandinavia (Preview) | Dj Obliviuz | steady_rhythm.wav |
| Iron Chorus | Final Fantasy 7 Boss battle | Not verified | iron_chorus.wav |
| Tailwind | Drunken Sailor | Irish Rovers | tailwind.wav |
| Needle Guard | Pickle Rick Trap Remix | Not verified | needle_guard.wav |
| Arcane Pulse | Xanadu (Extended Version) | Ummet Ozcan | arcane_pulse.wav |
| War Drums | Advent: One-Winged Angel | Nobuo Uematsu | war_drums.wav |

Replacement WAV files go in <profile>/BepInEx/plugins/BardBox/songs/.
Supported input is uncompressed PCM16 WAV, mono or stereo (downmixed), 8-96 kHz,
up to 120 MB per file. Restart the game after replacing a song. All clients must
have byte-identical WAV files: each musician advertises a SHA-256 fingerprint.
A mismatch silences that musician and hides their shockwaves on the affected
listener; buffs still work. No audio is transmitted or downloaded by the mod.
Changing displayed song titles/artists requires updating the manifest and rebuilding.
The headless server does not load audio. All eleven sources are user-supplied;
distribution permissions remain unverified (see CREDITS.md).
