# What Can I Sell?

A RimWorld 1.7 mod. On the world map, a settlement's vanilla **"Show what will buy"** button lists every item the trader *could* buy, so you have to check it against your storage yourself. This mod adds a **"Things you can sell"** button that does that comparison for you.

## Features

- **"Things you can sell" gizmo.** It sits next to "Show what will buy" on any non-player, non-permanent-enemy settlement that trades. The vanilla "Things this trader will buy" window gets the same button next to *Close*.
- **Trade-style list** of the items in your colony that this trader buys. For each item you see:
  - how many you have in storage
  - market value
  - estimated sell price per unit
  - a count picker (the same arrows and text box as the trade screen)
  - an estimated total for the amount you picked
- **Negotiator picker.** Prices are estimated with your best negotiator by default, and you can pick another colonist from the dropdown. The estimate uses the same formula as vanilla trading, including the faction's price offset, drug and animal-product bonuses, and difficulty.
- **Colony picker**, shown when you have more than one colony.
- **Search box**, plus sorting by total value or by name.
- **Form caravan.** Opens the vanilla Form Caravan window with your chosen items already selected and the route already set to the settlement, so the route planner is skipped. Use *Change route* if you want a different path. Add pawns and travel supplies as usual.
  - If you chose to sell food, medicine or beds, the mod turns off *Automatically select travel supplies*. Otherwise vanilla would reset those items to its own picks.

You cannot trade from this window. It is only for planning.

Item list source: the same reachable home-area items the Form Caravan window offers (`CaravanFormingUtility.AllReachableColonyItems`). These are filtered by the trader's `TraderKindDef.WillTrade` and `TradeUtility.PlayerSellableNow`, so rotten food, apparel worn by corpses and biocoded gear are left out, just like in the real trade screen.

## Requirements

- RimWorld 1.7
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

## Building

RimWorld 1.7 runs on .NET 10. The project compiles against the framework DLLs that ship with the game, so the .NET 8 SDK or newer is enough.

```bash
dotnet build Source/WhatCanISell -c Release
```

The output goes to `1.7/Assemblies/WhatCanISell.dll`. To also copy the mod into RimWorld's `Mods` folder:

```bash
dotnet build Source/WhatCanISell -c Release -p:Deploy=true
```

If your RimWorld or Harmony install is somewhere else, pass `-p:RimWorldDir="..."` and/or `-p:HarmonyDir="..."`.

## Layout

```
About/About.xml                         mod metadata
Languages/English/Keyed/WhatCanISell.xml UI strings
Source/WhatCanISell/
  WhatCanISellMod.cs          entry point, applies Harmony patches
  Patches.cs                  settlement gizmo + button in vanilla "will buy" window
  SellableStock.cs            gathers sellable colony items, estimates prices
  Dialog_ThingsYouCanSell.cs  the window
  FormCaravanPrefill.cs       opens Form Caravan with the selection applied
```

## Not covered yet

- Pawns: animals, prisoners and slaves are not listed. Only items are.
- Items already inside caravans or in other pawns' inventories are not counted.
