# Valheim MP (VMP)

Valheim MP or VMP for short is Valheim mod designed to make all authority server based. With the goal of stopping the most offensive cheats and allowing for better public servers.

## Features

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

- Chat commands (Easily created with server side only mods, allowing things such as \/tp, \/giveitem,\/claim)

- Full inventory networking; allowing multiple users to access the same chest without issues. And for mods there are SetCustomData(...) and GetCustomData(...)  extensions for ItemDrop.ItemData, easily allowing you to add networked variables to items.

- Overal network optimizations, send less!

## TODO

- Fishing?
- Player map markers?
- Destruction of ZDO's via SendZDOData (to prevent sending unneeded RPCs across the map of objects you've never even heard of)


## Known issues 

- Upgrading\crafting does not properly update the craft window forcing you to refresh for multiple upgrades.
- Hide weapons hides them completely rather then stashing them on your person
- Raven shows up because tutorial text is still out of sync when it triggers.
- When rocks turn into fragmentable rocks, the first rock is destroyed before it turns into a fragmentable rock making you see a short flicker.
- Sometimes items fail to show the durability properly in the inventory grid


## Continues tweaks

- Tweak all movement related syncing
- Tweak all combat related syncing
- Allow for better interfaces to act as library.


## Far-fetched features

- Map generation on server side only with clients receiving only what is relevant to them