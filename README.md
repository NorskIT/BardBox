# BardBox

<img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/docs/images/boombox-in-game.png" alt="Playing the Boombox in Valheim" width="420">

Bring music to your adventures! Craft a Boombox, find a reusable tape and equip it like arrows, and give yourself and friends within **20 metres** a bonus. Run and jump while playing, with shockwaves that react to the music.

**Play:** hold Attack to play and release to stop. For toggle controls, set `Controls.TogglePlayback = true` in `BepInEx/config/norskit_bardbox_plugin.cfg`. Each new start plays from the beginning.

**Requires:** BepInExPack Valheim and Jotunn 2.30.2+. Install the same BardBox version on all players and the server, with matching song files on players' clients. Version 0.1.7 is a test build.

**Volume:** `Audio.Volume` is server-synced: `1.0` = 100%, `5.0` = 500%. `Audio.ListenerVolume` is your personal volume (0 = mute, 1 = full server volume).

**Range:** `Audio.Range` sets the server-wide sound radius (default 500 m, maximum 2000 m). Buffs stay within 20 m. Game music fades out while a boombox is audible; disable this with `Audio.MuteGameMusic`.

**Show the zone:** enable `Debug.ShowAudioRange`. The marker is visible only to you, including a preview while holding your Boombox.

## Items

Tapes drop from bosses (33%) or appear in new dungeon/village chests (1%). They cannot be crafted, but are reusable and upgradeable. Existing chest loot is unchanged. Values below are for quality **I / II / III**. Different tapes combine; multiple copies of the same effect use the strongest quality.

| Image | Name | Description | Found / crafted | Spawn command |
|---|---|---|---|---|
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/boombox.png" alt="Boombox" width="48"> | Boombox | Play music and give nearby players the selected tape's bonus. | Workbench | `spawn NorskIT_BardBox_Boombox 1` |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/blank_tape.png" alt="Blank Tape" width="48"> | Blank Tape | Material for upgrading found tapes. | Workbench | `spawn NorskIT_BardBox_BlankTape 1` |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/trailblazer.png" alt="Trailblazer" width="48"> | Trailblazer | +5 / 7.5 / 10% movement speed. | Eikthyr (33%) | `spawn NorskIT_BardBox_trailblazer 1` |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/second_wind.png" alt="Second Wind" width="48"> | Second Wind | +15 / 20 / 25% stamina regeneration. | The Elder (33%) | `spawn NorskIT_BardBox_second_wind 1` |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/stonebeat.png" alt="Stonebeat" width="48"> | Stonebeat | 5 / 10 / 15% less blunt attack damage received. | Burial Chambers chests (1%) | `spawn NorskIT_BardBox_stonebeat 1` |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/hauler.png" alt="Hauler" width="48"> | Hauler | +150 / 300 / 500 carry weight. | Bonemass (33%) | `spawn NorskIT_BardBox_hauler 1` |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/heartbeat.png" alt="Heartbeat" width="48"> | Heartbeat | +15 / 25 / 35% natural health regeneration. | Sunken Crypts chests (1%) | `spawn NorskIT_BardBox_heartbeat 1` |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/steady_rhythm.png" alt="Steady Rhythm" width="48"> | Steady Rhythm | 5 / 10 / 15% lower stamina costs for running, jumping, attacking, blocking, dodging, swimming and sneaking. | Moder (33%) | `spawn NorskIT_BardBox_steady_rhythm 1` |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/iron_chorus.png" alt="Iron Chorus" width="48"> | Iron Chorus | 5 / 10 / 15% less slash attack damage received. | Frost Caves chests (1%) | `spawn NorskIT_BardBox_iron_chorus 1` |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/tailwind.png" alt="Tailwind" width="48"> | Tailwind | +10 / 15 / 20% sail force on the musician's boat. | Fuling village chests (1%) | `spawn NorskIT_BardBox_tailwind 1` |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/needle_guard.png" alt="Needle Guard" width="48"> | Needle Guard | 5 / 10 / 15% less pierce attack damage received. | Yagluth (33%) | `spawn NorskIT_BardBox_needle_guard 1` |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/arcane_pulse.png" alt="Arcane Pulse" width="48"> | Arcane Pulse | +15 / 20 / 25% eitr regeneration. | The Queen (33%) | `spawn NorskIT_BardBox_arcane_pulse 1` |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/war_drums.png" alt="War Drums" width="48"> | War Drums | +5 / 10 / 15% physical attack damage. | Fader (33%) | `spawn NorskIT_BardBox_war_drums 1` |

[Crafting and setup](DEVELOPMENT.md) ? [Songs](SONGS.md) ? [Credits](CREDITS.md)

Music recordings are not included in this source repository. Permission to distribute the supplied test songs has not been verified.
