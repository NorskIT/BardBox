# Changelog

## 0.1.6

- Replace tape crafting with 33% boss drops and 1% loot chances in four specific dungeon/village chest types.
- Keep tapes reusable and upgradeable at the workbench; no boss trophy required for upgrades.
- Award at most one level-I tape per roll, without player-count or resource scaling.
- Preserve existing tapes and already-generated chest contents; no rerolls on opening, reload or ownership transfer.

## 0.1.5

- Add server-synced sound radius: default 500 m, adjustable from 1 to 2000 m; buffs stay at 20 m.
- Relay playback metadata through the server so music works beyond loaded player objects.
- Add local world-space sound-radius preview with an optional shortcut.
- Fade game music out while a boombox is audible, preserving personal settings and independent ForestCrawler muting.

## 0.1.4

- Raise music volume to 500% with actual audio amplification.
- Synchronize Audio.Volume through embedded ServerSync; lock changes to server/admins.
- Add local ListenerVolume for personal attenuation and muting.
- Apply volume changes during playback without restarting songs.

## 0.1.3

- Boombox, Blank Tape and eleven reusable, upgradeable tapes.
- Nearby music and buffs, synchronized playback and optional toggle controls.
- Run and jump while playing; music-reactive shockwaves.
- Song names in tape tooltips and YouTube links in the song list.
- Updated README, screenshots, mod icon and package metadata.
