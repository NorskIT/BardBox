# BardBox

<img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/docs/images/boombox-in-game.png" alt="Playing the Boombox in Valheim" width="420">

Bring music to your adventures! Craft a Boombox, equip a reusable tape like arrows, and give yourself and friends within **20 metres** a bonus. Run and jump while playing, with shockwaves that react to the music.

**Play:** hold Attack to play and release to stop. For toggle controls, set `Controls.TogglePlayback = true` in `BepInEx/config/norskit_bardbox_plugin.cfg`. Each new start plays from the beginning.

**Requires:** BepInExPack Valheim and Jotunn 2.30.2+. Install the same BardBox version on all players and the server, with matching song files on players' clients. Version 0.1.4 is a test build.

**Volume:** `Audio.Volume` is server-synced: `1.0` = 100%, `5.0` = 500%. `Audio.ListenerVolume` is your personal volume (0 = mute, 1 = full server volume).

## Items

Tapes are reusable and upgradeable. Values below are for quality **I / II / III**. Different tapes combine; multiple copies of the same effect use the strongest quality.

| Image | Name | Description |
|---|---|---|
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/boombox.png" alt="Boombox" width="48"> | Boombox | Play music and give nearby players the selected tape's bonus. |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/blank_tape.png" alt="Blank Tape" width="48"> | Blank Tape | Crafting material for tapes and their upgrades. |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/trailblazer.png" alt="Trailblazer" width="48"> | Trailblazer | +5 / 7.5 / 10% movement speed. |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/second_wind.png" alt="Second Wind" width="48"> | Second Wind | +15 / 20 / 25% stamina regeneration. |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/stonebeat.png" alt="Stonebeat" width="48"> | Stonebeat | 5 / 10 / 15% less blunt attack damage received. |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/hauler.png" alt="Hauler" width="48"> | Hauler | +150 / 300 / 500 carry weight. |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/heartbeat.png" alt="Heartbeat" width="48"> | Heartbeat | +15 / 25 / 35% natural health regeneration. |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/steady_rhythm.png" alt="Steady Rhythm" width="48"> | Steady Rhythm | 5 / 10 / 15% lower stamina costs for running, jumping, attacking, blocking, dodging, swimming and sneaking. |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/iron_chorus.png" alt="Iron Chorus" width="48"> | Iron Chorus | 5 / 10 / 15% less slash attack damage received. |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/tailwind.png" alt="Tailwind" width="48"> | Tailwind | +10 / 15 / 20% sail force on the musician's boat. |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/needle_guard.png" alt="Needle Guard" width="48"> | Needle Guard | 5 / 10 / 15% less pierce attack damage received. |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/arcane_pulse.png" alt="Arcane Pulse" width="48"> | Arcane Pulse | +15 / 20 / 25% eitr regeneration. |
| <img src="https://raw.githubusercontent.com/NorskIT/BardBox/main/assets/icons/war_drums.png" alt="War Drums" width="48"> | War Drums | +5 / 10 / 15% physical attack damage. |

[Crafting and setup](DEVELOPMENT.md) ? [Songs](SONGS.md) ? [Credits](CREDITS.md)

Music recordings are not included in this source repository. Permission to distribute the supplied test songs has not been verified.
