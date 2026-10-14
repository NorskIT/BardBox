# Verification status

## Completed without launching the game

- Release compilation against installed Valheim DLLs succeeds.
- The current test run passes 258 assertions, including installed-game Harmony method
  signatures, all shipped WAV files and all PNG dimensions/color types.
- Core tests exercise strongest source, weaker-source fallback, stacking of different
  tapes, source expiry, invalid identifiers/quality and death/logout clearing.
- Catalog tests check eleven unique tapes, three increasing quality values and the
  sailing exception to boss requirements.
- WAV tests exercise PCM decoding, stereo downmix, optional padded chunks, malformed
  files and truncation.
- Thirteen AI-generated icons exported as 128 x 128 PNG with transparency and visually
  reviewed in `assets/icon-preview.png`.
- Eleven supplied MP3 songs decoded to 44.1 kHz mono PCM16 WAV, one per tape.
  The source manifest, original files and displayed song names are verified.

## Model build

The user's Sketchfab FBX is imported and bundled using Unity 6000.0.75f1, matching
the installed game. A first runtime test rejected the bundle built with 6000.6.1f1;
rebuilding with the matching editor resolved that incompatibility.

## Isolated Valheim startup check

`build/Test-Runtime.ps1` starts a separate client with its own BepInEx directory and
save directory, then exits automatically. The default run stays in the menu;
`-World` additionally opens a newly generated local test world. Passed on
Valheim 1.0.17 with BepInEx 5.4.23.5 and Jotunn 2.30.2:

- Native plugin initialization and 13 item prefabs with icons.
- Thirteen recipes, crafting stations and all material references resolved using
  Jotunn's world-registration routines against the disposable menu ObjectDB.
  These routines are invoked explicitly because the menu normally registers only items.
- Eleven status effects cloned at all three quality levels with names/icons/tooltips.
- Quality-3 tapes saved and loaded through the game's Inventory/ZPackage methods.
- Three Harmony hooks installed and custom tape tooltips returned.
- All eleven WAV files decoded to Unity AudioClips at the expected durations.
- Model materials supported by the game, and an offscreen render visually inspected.

## Still needs playtesting

Compatibility of the arm pose with other animation mods;
recipe discovery, crafting and upgrading in a world;
equipping/dropping/storage/gravestone/relogin; actual stat changes, damage order and
compatibility with the installed mods; dedicated-server load and playback over actual Steam/PlayFab internet connections.
Passing the core tests is not a claim that these checks passed.

## 0.1.1 animation regression

Reproduced with a real player preview wearing Leather Chest/Legs and Deer Hide Cape.
The cape adds a second `Hips` hierarchy. The 0.1.0 animation graph caused 44 skeleton
binding errors; the same test with only CarryPose.Update bypassed passed. A naked or
leather-only preview did not reproduce it, explaining the earlier test gap.

The replacement uses six arm transforms after animation; it does not bind an animation
stream or alter the Avatar/controller. The dressed preview test passes for male and
female models, repeated music start/stop, pose disable and blend-out. Assertions check
that non-arm rotations and all bone positions remain unchanged and the Animator's
state time continues advancing. Runtime captures show the box held overhead.

The world uses Fixed Animator updates, unlike the menu preview. A render regression
also caught interpolation overwriting the arm pose (right-hand height changed from
2.02 m before Camera.Render to 1.59 m afterward). CarryPose now reapplies only the arm
rotations through [Camera.onPreRender](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-onPreRender.html).
Instrumentation placed the overwrite between pre-cull and pre-render; pre-cull is too early.
The world acceptance run checks the grip height after rendering as well as before it.

Final 0.1.1 acceptance run: `artifacts/smoke-20261014-211807/result.txt` passed.
The rendered grip remains at 2.02 m and `world-playing.png` was visually inspected.
The isolated world test passed walking while playing, release/restart from the start,
tape switching, audio and buff expiry, teleport interruption/recovery, lethal damage
and respawn without new skeleton binding errors. Test input replaces PlayerController
input only in that isolated process; the installed mod contains none of the harness.
Actual two-client networking and the full Development mod combination remain manual
acceptance checks; this run does not claim to validate them.

`build/Test-Runtime.ps1 -World` additionally loads a newly created local world in the
test run's own save directory. Its test-only focus override permits simulated attack
input in a hidden client; it is never shipped in BardBox.dll. `-PoseBaseline` is a
diagnostic switch for reproducing the original comparison, not an acceptance run.

## Multiplayer acceptance checklist for the completed package

Use isolated test profiles and a disposable world. All clients and the host need the
same mod version and song files. Do not test with a production world or profile.

1. Spawn `NorskIT_BardBox_Boombox`, `NorskIT_BardBox_BlankTape`, and each tape by appending
   its song filename without `.wav` to `NorskIT_BardBox_` (for example
   `NorskIT_BardBox_trailblazer`). Verify all eleven unique icons and sounds.
2. Equip a tape and the box. Hold Attack, walk, release, press again. Verify restart
   from sample zero, clean loop transitions and matching overhead pose on both clients.
3. Enter while a loop is playing. Check synchronized position, spatial direction and
   attenuation; walk beyond 20 m. Buffs should disappear about two seconds later.
4. Stop by switching tape/item, opening inventory/chat/menu, dying, teleporting,
   rolling, swimming and taking the rudder. Release and press again to resume.
5. Use two musicians with the same tape at qualities 1 and 3; only quality 3 applies.
   Move the quality-3 source away and verify fallback to quality 1. Try different tapes
   together. Disconnect one musician; its sound and buffs must not remain indefinitely.
6. Measure every bonus with and without the tape. Check Rested, Megingjord, armor,
   potions and boss powers. Physical reduction affects only the named damage component
   from an attacker; fall and elemental damage remain unchanged.
7. Test Tailwind with raised sails and fixed wind, two boats, multiple musicians,
   walking off the boat and changing boat owner. No rowing or wind-direction change.
8. Craft/upgrade normally. Only initial crafting consumes a boss trophy. Playing never
   consumes a tape. Confirm all item qualities survive storage, death and relogging.
9. Run and jump repeatedly while holding Attack. Music should continue without
   restarting; the buff HUD should show the actual bonus and its current value.
10. Compare shockwaves on two clients with identical songs, including loop boundaries,
    entering/leaving range, mute, toggling ShockwavesEnabled and multiple musicians.
    Judge the automatic onset timing by listening to the supplied songs.
11. Record BepInEx logs, game/mod versions and reproduction steps for any failure.

## 0.1.2 verification

Core tests cover all 33 effect/value HUD labels, silence, constant tones, synthetic
bass bursts, cooldown and deterministic detection. Cue-clock tests cover entry,
crossings, duplicate suppression, seeking, backward clock changes, loops, switching
tapes, range reentry, cooldown across loop boundaries and agreement between independent clocks.

Analyzed supplied songs: Fishy 15 cues / 83.44 s; Whip 24 / 97.10 s;
Storm-Crowned 56 / 341.81 s; Drunken Sailor 40 / 159.19 s.
These are signal-derived onsets; their musical timing still needs listening tests.
SHA256 comparison against the previous package confirms the song swap exactly.

World regression `artifacts/smoke-20261014-213919/result.txt` passed, including
three running jumps without changing the song start timestamp or losing audio,
buff or arm weight, and the existing stop/teleport/death/respawn checks.
The ring shader, geometry, expansion, fade, expiry and release cleanup passed.
`shockwave-preview.png` and `world-jumping.png` were visually inspected.
Additional world integration run `artifacts/smoke-20261014-214120/result.txt` passed:
a cue from the actual Fishy WAV automatically spawned a ring with music volume zero;
disabling visuals hid it, and re-enabling did not replay the previous cue.
Actual two-client visual synchronization remains a multiplayer playtest.

## 0.1.3 synchronization and toggle controls

- 211 core assertions pass, including song mapping, hold/toggle input edges,
  interruptions and setting changes, four-timestamp clock estimation, minimum-RTT
  selection/aging, malformed samples, delayed song entry, file fingerprints,
  circular seek error and deterministic loop cues for late listeners.
- The 30-minute deterministic simulation uses two client clocks drifting by
  +/-150 ppm, sound cards drifting by +/-300 ppm, and varying one-way transport
  delays of 30-50 ms. Maximum pair error: 77.98 ms; maximum clock-estimation error:
  13.75 ms; each simulated sound card needed 14 corrections. This is a simulation,
  not a physical output-latency measurement.
- The correction threshold is 35 ms (tighter than the proposed 50 ms): the initial
  50 ms threshold allowed 113.24 ms pair error because errors add in opposite
  directions. Checks still run every 250 ms.
- The first 0.1.3 world run (smoke-20261014-221451) passed and corrected an injected
  five-second audio offset to about 20 ms. Changing world time by two hours did
  not move the music clock. This run preceded the toggle addition.
- The toggle world run initially caught a first-press latch while idle, fixed by
  latching timeout/block interruptions only when playback was active.

The test-only NetworkSmoke plugin starts a private host and client in separate
processes using the native CustomSocket/ZSocket2 transport over 127.0.0.1. It
replaces only the obsolete TCP connect entrypoint in the harness; native peer
handshake, routed RPC, ZDO replication and production music code are exercised.
No listener is opened outside loopback, and this harness is never packaged.
Actual Steam/PlayFab transport, internet jitter, hardware output latency and
listening comparisons remain user playtests even when loopback passes.

Final 0.1.3 world acceptance: artifacts/smoke-20261014-222347/result.txt passed,
including hold/toggle start/release/stop/restart, all previous pose regressions,
world-time independence and five-second audio drift correction. Concurrent runs
under heavy world-generation load hit the harness input-timeout guard; the final
world acceptance ran alone.

Separate-process loopback acceptance passed on both sides:
- Host: artifacts/smoke-20261014-222130/network-result.txt
- Client: artifacts/smoke-20261014-222131/network-result.txt
- Comparison: artifacts/network-20261014-222128/comparison.json

Both processes agreed on epoch, playback session and exact start timestamp.
Across 150 samples each, maximum host timeline error was 26.023 ms, client error
21.574 ms, and nearest-time pair difference 37.605 ms. Remote buffs, muted
receiver catch-up and replicated stop also passed. Measurements use native audio
playback positions against the synchronized clock, not recordings from physical
speakers. Listening/drop comparisons and Steam/PlayFab internet testing remain.

## 0.1.4: 500% volume and ServerSync

- Core suite: 221 assertions, including PCM gain, saturation, mute, bounds and invalid gain fallback.
- ServerSync v1.20 source revision c57c2aa is embedded, with its license in the ZIP. Vendor synchronization logic is unchanged.
- Initial integration exposed that a direct ConfigEntry assignment can bypass the ServerSync UI lock locally. BardBox now restores the last server value for locked clients; the regression passes.
- Two isolated Valheim processes, native localhost networking: smoke-20261014-231720 (host) and smoke-20261014-231722 (client), both PASS. The client starts at 20%, receives server 500%, cannot override that value, then receives a live change to 300%. Emitter PCM gain and personal mute/unmute were verified.
- Both peers retained synchronized playback over 150 samples each: maximum individual timeline error 40.33 ms (host), 23.62 ms (client). This does not measure physical speaker latency or Internet conditions.
- Live audio audit in smoke-20261014-231842: 48 real audio buffers processed at 5x gain with zero sample errors. AudioSource.volume remains at Unity's maximum of 1; the filter supplies the extra gain.
- No Ranged 1.2.3 compatibility in the same isolated process: all eleven tapes equip/select, retain enabled recipes and pass the crafting gate; bow/arrow recipes and equipment remain blocked.
- Production profiles and servers were not modified.
- The full world regression in smoke-20261014-231842 also passed: movement/jumping, overhead pose, hold/toggle controls, music/buffs, shockwaves, drift correction, tape changes, stop, teleport, death and respawn.


## 0.1.5 range and background music

Core tests cover radius limits/fallback, distance attenuation, heartbeat expiry,
metadata validation and monotonic sequences (242 assertions overall).

Isolated Valheim 1.0.17 world test with No Ranged 1.2.3 passed:
- World sphere geometry at 20/500/2000 m, live updates, emitter centre and cleanup.
- Actual MusicMan source retains playback while its PCM filter produces silence.
- Music source replacement transfers the filter and releases the old source.
- Personal mute restores game music; unmute applies attenuation again.
- ForestCrawler's real MusicSilence helper loaded separately and tested in both
  stop orders; neither helper releases the other's attenuation/mute.
- Native music volume and saved preferences preserved.
- Existing movement, jumps, pose, tape selection, death/respawn and audio tests.

Two isolated game processes using native ZSocket2, ZNet and ZRoutedRpc passed:
- Late join seeks into an already playing song.
- Initial radius 500 m overrides the client's 20 m; locked client edits rejected.
- Live 500/2000/500 m changes propagate through ServerSync.
- Music continues at 490/1990 m with the musician's Player object unloaded.
- Sound attenuates, cuts off outside radius, and seeks on reentry; one emitter.
- Buffs remain within 20 m and return when the listener approaches again.
- Server volume, personal mute and stop propagation retain their behaviour.
- 150 timeline samples per process: maximum error 26.5 ms host, 31.6 ms client
  against the synchronized clock in the recorded localhost run.

Evidence: artifacts/smoke-20261014-235930 (world/presentation),
artifacts/smoke-20261014-235941 and smoke-20261014-235943 (network).
These are automated local tests, not a measurement of speaker/headphone latency.
Internet multiplayer, subjective loudness and the debug sphere's usability in
varied terrain still need user playtesting.

A second two-process run also passed with roles reversed: a remote client
published its music through the server, the host received sound and the nearby
buff, and the client's release stopped the host emitter. Evidence:
artifacts/smoke-20261015-000212 and smoke-20261015-000213.


## 0.1.6 tape loot

The core suite passes 258 assertions, including exact chance boundaries and the
four allowed chest prefab names. Native recipe checks assert that all eleven
tapes use m_noCraftOnlyUpgrade, have no trophy requirement, and retain quality-II
and quality-III material amounts and workbench levels. Boombox and Blank Tape
retain ordinary crafting. No Ranged 1.2.3 compatibility remains covered.

Isolated world loot checks passed in artifacts/smoke-20261015-001713:
- All seven installed boss prefabs resolve; their native CharacterDrop generator
  produces both wins and losses with fixed test seeds. Registration adds exactly
  one entry, preserves vanilla entries and disables player/level/resource scaling.
- All four installed chest prefabs generate their ordinary contents plus exactly
  one quality-I tape when the separate tape roll is forced to succeed.
- Failed rolls, already-generated vanilla chests, creator-tagged chests and
  excluded prefab types do not award or retry tape rolls.
- Inventory persisted into the native ZDO loads with the same tape.
- Non-owners cannot roll; changing ownership and retrying does not duplicate loot.
- A full inventory keeps all its contents and records the attempt without rerolling.

Boss tests use the game's loot generator rather than fighting all seven bosses.
Chest tests instantiate actual chest prefabs in a disposable world rather than
manually exploring every dungeon. These controlled tests do not measure random
loot frequency during a normal campaign or compatibility with other loot mods.

The complete isolated world regression passed, including No Ranged 1.2.3,
playback/movement and range/music-presentation checks. The two-process run in
artifacts/smoke-20261015-001714 and smoke-20261015-001716 passed the existing
network/audio tests and the new loot checks: native boss item spawn and chest
inventory resolve to the same ZDO IDs on both peers; the receiving client gets
one level-I tape; transfer of chest ownership does not trigger another roll.
No exception/error entries were found in these three final game logs.
