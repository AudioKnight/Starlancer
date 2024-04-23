# Starlancer Enemy Escape
This mod allows enemies to travel both into and out of the facility while seamlessly switching AI modes. Part of my Starlancer Series!

[Get the full modpack!](https://thunderstore.io/c/lethal-company/p/AudioKnight/Starlancer/)

## Presets
    ReasonableDefaults: Blob:0, Bunker Spider:10, Butler:5, Butler Bees:5, Centipede:0, Crawler:10, Flowerman:10, Hoarding bug:10, Jester:1, Nutcracker:5, Puffer:10, Spring:5, Baboon hawk:15, Earth Leviathan:0, ForestGiant:0, MouthDog:0, RadMech:0, Red Locust Bees:5
	Disabled: All 0s
	Minimal: All 1s
	Chaos: All 100s (Use at your own risk :3 )

## Important Notes
StarlancerAIFix is a hard dependency for StarlancerEnemyEscape, as it prevents issues with certain enemies that are not in their native environment.

### Blob Issues
The blob's body physics can sometimes get messy during teleport, so it might appear glitched out for a bit until it settles itself.
The blob's NavMeshAgent does not allow it to traverse Jump or Climb areas, so if it tries to go outside on Experimentation, it will briefly appear stuck on the door (barring any player interaction) and then return inside and resume normal blob activities.
By default, the blob is not able to move from inside to outside or vice versa.

### Bracken Issues
The bracken does not enjoy being told to go to the opposite half of the level if it is waiting for a player to be in its area. My system accounts for this by not allowing it to randomly path, but it may still follow a player out of (or into) the facility if it maintains a target on that player.

### Baboon Hawk & Hoarding Bug Issues
My system has allowed the hawks and lootbugs the opportunity to carry scrap out of (or into) the facility. At times, there is the possibility of slightly glitchy behavior while carrying scrap, though I've tried to account for this as best I can. If you're close enough notice this issue, their AI will probably fix itself anyway.

## Credits
HUGE thanks to **[Zaggy1024](https://thunderstore.io/c/lethal-company/p/Zaggy1024/)**, **[IAmBatby](https://thunderstore.io/c/lethal-company/p/IAmBatby/)**, **[mrov](https://thunderstore.io/c/lethal-company/p/mrov/)**, and **[qwbarch](https://thunderstore.io/c/lethal-company/p/qwbarch/)** for their support and help with the code!