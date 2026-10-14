# BardBox 0.1.4 — development build

Craft a Boombox, select a reusable tape like ammunition, and hold Attack to play.
Release to stop; each new press starts the sound from the beginning. Set
Controls.TogglePlayback=true to press once to play and again to stop. Players within
20 metres receive the tape's bonus, including the musician. Different tapes combine;
the strongest quality wins when several musicians play the same tape.

**Development test build.** Includes the supplied boombox model and eleven songs.
Version 0.1.3 adds the agreed song mapping, song titles in tape tooltips, optional
toggle controls and a latency-compensated music clock with playback correction.
Running/jumping, actual-bonus HUD names and automatic shockwaves are retained.
See TESTING.md for measured results and remaining multiplayer checks.

## Requirements

- Valheim 1.0.17 / Unity 6000.0.75f1, used for this build.
- BepInExPack Valheim and Jotunn 2.30.2 or later.
- The same BardBox version on every client and the host/dedicated server.
- Identical WAV files on clients to hear identical music. Restart the game after replacing files.

## Installation of the test package

Extract the package's `BepInEx` folder into an isolated test profile. The resulting
layout is `BepInEx/plugins/BardBox/BardBox.dll`, `bardbox.assets`, `icons/`, and `songs/`.
This project does not install itself, publish releases, or update a server.

## Crafting

All recipes use a level 1 Workbench. Tape upgrades use levels 2 and 3.
Boombox: 8 Fine Wood, 4 Bronze, 2 Surtling Cores.
Blank Tape: 2 Thistle, 2 Greydwarf Eyes. Each finished tape uses one Blank Tape.
Each recipe below also needs **1 Blank Tape**. Bonuses are quality I / II / III.

| Tape | Materials | Boss trophy | Bonus |
|---|---|---|---|
| Trailblazer | 5 Wood, 5 Resin, 2 Deer Hide | Eikthyr | +5 / 7.5 / 10% land speed |
| Second Wind | 2 Copper, 5 Greydwarf Eyes | Elder | +15 / 20 / 25% stamina regeneration |
| Stonebeat | 5 Bone Fragments, 5 Stone | Elder | -5 / 10 / 15% blunt attack damage received |
| Hauler | 2 Iron, 5 Ancient Bark | Bonemass | +150 / 300 / 500 carry weight |
| Heartbeat | 2 Iron, 5 Bloodbags | Bonemass | +15 / 25 / 35% natural health regeneration |
| Steady Rhythm | 2 Silver, 4 Wolf Fangs | Moder | -5 / 10 / 15% stamina costs |
| Iron Chorus | 2 Silver, 4 Wolf Pelts | Moder | -5 / 10 / 15% slash attack damage received |
| Tailwind | 2 Black Metal, 5 Linen Thread, 2 Needles | None | +10 / 15 / 20% sail force |
| Needle Guard | 2 Black Metal, 5 Needles | Yagluth | -5 / 10 / 15% pierce attack damage received |
| Arcane Pulse | 2 Refined Eitr, 5 Royal Jelly | Queen | +15 / 20 / 25% eitr regeneration |
| War Drums | 2 Flametal, 5 Asksvin Hide | Fader | +5 / 10 / 15% physical attack damage |

Quality 2 costs another Blank Tape and the original non-trophy materials;
quality 3 costs twice those amounts. Boss trophies are only consumed for initial crafting.

## Songs

See [SONGS.md](SONGS.md). All eleven tapes use supplied songs. The converted WAV files are in `assets/audio/`.
The server-controlled volume is `[Audio] Volume` in `norskit_bardbox_plugin.cfg` (0-5, default 0.5). ServerSync distributes the host/server value and locks it for non-admin clients. `1` is 100%, `5` is 500%. `[Audio] ListenerVolume` is a local multiplier (0-1, default 1); zero mutes without removing buffs. Changing volume applies during playback without restarting the song. High gain may distort loud recordings.

## Build

`dotnet build src/BardBox/BardBox.csproj -c Release`

The project reads game and reference DLLs from the Steam installation and the Prod-v1
Gale profile. These locations are **read-only build references**; no post-build copy
or process launch occurs. Override `GameDir` and `ProfileDir` with MSBuild properties.
The game DLLs have transitive System.Net.Http/System.IO.Compression reference-version
mismatches that produce two MSB3277 warnings; no game/system DLLs are shipped.

`dotnet run --project tests/BardBox.Tests.csproj -c Release`

The supplied FBX is imported in `unity/Assets/Model`; source MP3s are in
`unity/Assets/AudioSource`. Run `build/Build-Assets.ps1` using Unity **6000.0.75f1**
(pass `-Unity <path-to-Unity.exe>` for another installation location). This also
reconverts the eleven supplied songs from assets/songs.tsv. Unity 6000.6 bundles cannot load in this game.
`build/Generate-TestAudio.ps1` resets every song to a test loop; do not run it to
rebuild the package with the supplied songs.
The current bundle build targets Windows clients. A headless server does not load it.

`build/Package.ps1` builds, runs tests and creates a timestamped ZIP under `artifacts/`.
It deliberately refuses to package without the approved model bundle. If PowerShell
requires it, invoke scripts with `powershell -NoProfile -ExecutionPolicy Bypass -File ...`.

## Implementation notes

Items/recipes/status effects use Jotunn. Tapes use normal Ammo equipment and native
quality persistence. Music does not invoke Attack.UseAmmo. Playback is a dedicated
held-input action scoped to the Boombox. The overhead pose rotates only the six arm
bones after normal animation and restores those rotations before the next evaluation.
It reapplies the pose immediately before rendering because the world's fixed-mode animation
interpolation can otherwise overwrite the arms immediately before rendering.
It never rebinds or replaces the Animator. This fixes the 0.1.0 skeleton binding failure
when equipped skins (for example capes) contain duplicate bone names.

Playback identity, quality, start time and heartbeat are stored in the player's ZDO.
Remote equipment is checked using the game's replicated right-hand item hash.
Receivers calculate the strongest buff independently on their own character owner.
The boat owner applies the strongest active onboard Tailwind to sail force.

There is no MIDI, song streaming, personal music download, or Bardheim dependency.
Deep North and the blocking tape are deferred.

## Music shockwaves

Strong bass/energy increases trigger a cyan ring expanding to 4 m over 0.6 seconds.
This is cosmetic and adds no force, damage, sound or camera shake. It is visible
within the 20 m music radius. The local setting `Visuals.ShockwavesEnabled` in
`BepInEx/config/norskit_bardbox_plugin.cfg` defaults to true; music volume does not
affect detection. Stop, leaving range or disabling the setting hides the ring.

Each WAV is analyzed once when loaded: 50 ms windows, a 180 Hz low-pass bass
envelope, a 1.5 s rolling baseline and a minimum 1.5 s interval between pulses.
Cues follow the replicated song start time without extra network messages. Use
identical WAV files on all clients. Strong transients can trigger it too; this is
automatic onset detection, not a manually authored map of musical drops.
All eleven tapes now use supplied songs.

## Music synchronization and controls

The musician starts immediately; a late listener seeks to the current song position.
A 0.5-second delivery delay skips the first 0.5 seconds instead of delaying the song.
A server-session music clock uses four-timestamp RPC probes and the lowest RTT of
the latest eight samples, independently of world/day time. Five initial probes
are 200 ms apart; subsequent probes are five seconds apart.
Playback position is checked every 250 ms and corrected when error exceeds 35 ms.
The threshold leaves margin for errors in opposite directions on different clients.
This targets about 100 ms under normal conditions; hardware latency, strongly
asymmetric networks or a frozen client cannot be made perfectly simultaneous.
A stale playback heartbeat is discarded after 3.5 seconds.

In BepInEx/config/norskit_bardbox_plugin.cfg, Controls.TogglePlayback defaults to
false (hold mode). True enables press-to-start/press-to-stop. Changing mode stops
current playback. Tape changes, menus, swimming, dodge, teleport and death still
interrupt music; running and jumping do not. A fresh start begins at sample zero.
Diagnostics.SyncLogging=true logs clock estimates and expected/actual song position
for multiplayer diagnosis; it is off by default.

## Source checkout

Music recordings, converted audio, generated bundles, local test artifacts and game libraries are excluded from Git. Supply your own appropriately licensed recordings using `assets/songs.tsv` before building the audio assets. The source checkout alone is not a playable mod package.

## Server-synced volume (0.1.4)

ServerSync is embedded in BardBox.dll, with its license included in the package. Install the same patch version on every client and server. The existing Audio.Volume key is retained and now server-controlled. Offline/hosting uses your own value; joining a server temporarily adopts its value. ListenerVolume, toggle controls, visuals and diagnostics remain local. The 20 m range, song files, synchronization timeline and buff values are unchanged. Extra gain is applied only to each BardBox emitter through an audio filter, with saturated PCM output; source clips and song hashes are not modified.
