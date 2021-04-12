# Valheim MP (VMP)

Valheim MP or VMP for short is Valheim mod designed to make all authority server based. With the goal of stopping the most offensive cheats and allowing for better public servers.

## Features

- Overal network optimizations, send less! Also, have the serve actually send, not clients, clients will send around 4kb/s max (though slightly dependend on settings), no more clients handling an area they dont have the network capacity for.

- Server based characters (the client will *never* send their character to the server.)

- Fully server controlled world (In the vanilla game everything is controlled by the client, monsters, damage calculations, item spawning, everything, making it super easy to cheat in about anything. But VMP prevents all that.)

- Better ward protection
  - Damage protection (lower the amount of damage taken) and Damage reflection (reflect damage back to the attacker), configurable for:
    - Player vs Player
    - Player vs Non Player
    - Monster vs Player
    - Monster vs Non Player
  - Item protection (all those iron bars dropped on the ground, and all that coal? now people will no longer be able to pick it up within your ward radius)
  - Crops protection (nobody likes having their carrots stolen!)

- Chat commands (Easily created with server side only mods, allowing things such as \/teleport, type \/help in game for an actual list.)

- Full inventory networking; Making it impossible for clients to cheat items, since the server is in full control of them. As a nice side effect, there is no longer a need to lock chests to a single person use, and so multiple users can access the same chest without issues (In vanilla this is impossible without risk of duplication and item loss). And for mods there are several utility extensions to make life easier when networking items.

- Party system and clan system. 
  - Allowing you to form parties and clans. 
  - Party and Clan members can see each other on the map
  - Send messages to only your party or clan. 
  - Party and Clan members can not damage each other. 
  - Party and Clan are both persitant if you leave and rejoin later.

- ValheimMP Extension functions and events for modders.

## TODO

- Environment manager currently doesn't work in mp, and there need to be several changes done to make it work on an authoritive server.
- Fishing? I havent tried it at all, but I assume it's somewhat unuseable with an authoritive server as is.
- Player map markers? *I will probably* sync these at some point. (Probably with support for clan or party level markers)


## Known issues 

- Hide weapons hides them completely rather then stashing them on your person
- Raven shows up because tutorial text is still out of sync when it triggers.
- Because there is no Environment Manager for each player you can not freeze to death, because the location of the server env is always fixed at spawn.
- Sometimes items fail to show the durability properly in the inventory grid (I've actually seen this in other peoples screenshots, is this a vanilla game bug?)

## MOD Compatibility

Any mod that does client side alteration of the inventory will **not** work. 
For example by default Epic Loot adds monster drops, but they are dropped by monsters on death, because the server handles monsters as well as their deaths it works.
But at the same time Epic Loot has a crafting system where the client directly adds items to their own inventory through a crafting system, this will **not** work.
I have personally written an Epic Loot patch that makes it so all crafting done is done on the server side.

Any mod that spawns monsters on the client side will **not** work.
For example Creature Loot and Control that just alters the way monsters work, will work as long as it is installed on both client and server. But the menu to actively spawn in monsters will **not**.

For all other mods, as long as they do not actively try to take control of object ownership or spawn things that need to be interactable online locally then they will work. (Assuming there are no general Patch conflicts, I modify quite a lof of functions so that can still happen...)

## Continues tweaks

- Tweak all movement related syncing
- Tweak all combat related syncing
- Allow for better interfaces to act as library.


## Far-fetched features

- Map generation on server side only with clients receiving only what is relevant to them