- 1.1.0
  - Added StarlancerEnemyEscape.
  - Updated dependencies.
  
- 1.0.0
  - Added StarlancerWarehouse to the Starlancer modpack.
  - Updated dependencies.

- 0.8.0
  - Updated dependencies.

- 0.7.0
  - Updated dependencies to reflect new versions of Starlancer mods.
  
- 0.6.0
  - Updated dependencies, no longer keeping parity with StarlancerMoons version number due to instances where dependencies need to be updated without warranting a major version progression.

- 0.5.0
  - Updated dependencies to reflect new version of StarlancerMoons.

- 0.4.1
  - Actually updated the versions of StarlancerMoons and StarlancerMusic the modpack targets.

- 0.4.0
  - Updated StarlancerMoons to no longer be dependent on LethalExpansion. It is now dependent on LethalLevelLoader.
  - Polished up the songs in StarlancerMusic.

- 0.3.0
  - Added StarlancerMusic to the Starlancer modpack.

- 0.2.0
  - Auralis is now part of "Starlancer Moons" and bundled into the "Starlancer" modpack. I'll be releasing some custom music in a couple of days as a separate mod. This way everyone has the choice of what Starlancer content they'd like to use.
  - Starlancer Moons is now dependent on "Lethal Expansion Core" instead of "Lethal Expansion", since LE's author is looking to step down from modding for the time being.
  - Completely reworked the terrain down to the intended playable area. This should improve performance, but please let me know.
  - Removed a bunch of pointless baked lighting data that was bloating the file size. Oops. (The .lem file is now 44mb instead of 196mb)
  - Added a second fire exit.
  - Lowered masked enemy spawns slightly.
  - Decreased the size of the dungeon generation to be equivalent to March. My tests showed there was way too much empty space inside, even with the higher scrap amount that should be present.
  - Decreased the rate of outside enemy spawns slightly and decreased the cap to help limit the number of dogs. Baboon Hawks can now spawn.
  - Significantly increased the falloff of daytime enemy spawn rates. Removed Circuit Bees for thematic reasons.
  - Added a small exterior building halfway between the ship and the facility. ~~You are being watched~~
  - Added some environmental decorations.
  - Adjusted volumetric fog and lighting.
  - Hopefully fixed the issue where water isn't drowning employees >:|
  - Changed the tag on the ice floes to hopefully resolve some reported audio issues. If these persist, it's possible that it's a bug in the SDK. I'll keep a watch on it.
  - Decreased the routing cost to 750.
  - Lowered the chance for the mansion tileset to spawn.
  - Added collision to the walls around the doors. No idea how this didn't get discovered by anyone.

- 0.1.4
  - Adjusted scrap spawn rates on Auralis and removed certain low-value scrap (don't worry, duckies _can_ still spawn, just very rarely). It should be much more profitable now on average.
  - Performance optimizations.
  - Small adjustment to interior enemy spawn rates.
  - Very small adjustment to dungeon size.

- 0.1.3
  - Performance optimizations.
  - Fixed an issue where walking into the lake wouldn't trigger water effects.
  - The path to the fire exit is now shorter.
  - Fog adjustment.
  - Added footstep sounds.
  - Changed the color of the sun.
  - Changed orbit prefab for Auralis to Moon3.

- 0.1.2
  - Fixed item dropship not appearing. (Note for other modders: This was caused by a change to the dropship object in Lethal Expansion.)
  - Raised starting height of falling snow.

- 0.1.1
  - Fixed navmesh issues.
  - Fixed scrap falling through landing pad.
  - Shifted a tree out of the way of the ship's landing sequence.
  - Adjusted the ice floe path. It's still treacherous, especially while encumbered, but it should be a bit easier to make the jumps.

- 0.1.0  
  - Initial Release.
  - Added custom moon "Auralis".