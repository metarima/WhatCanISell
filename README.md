# What Can I Sell?

A RimWorld 1.7 mod. On the world map, a settlement's vanilla **"Show what will buy"** button lists every item the trader *could* buy, so you have to check it against your storage yourself. This mod adds a **"Things you can sell"** button that does that comparison for you.

## Features

- **"Things you can sell" gizmo.** It sits next to "Show what will buy" on any non-player, non-permanent-enemy settlement that trades, including modded trading places that aren't settlements, such as VFE Medieval 2's Merchant's guild camps. The vanilla "Things this trader will buy" window gets the same button next to *Close*.
- **Trade-style list** of the items, prisoners, slaves and animals in your colony that this trader buys, split into tabs (All / Items / Prisoners & slaves / Animals). For each row you see:
  - how many you have in storage
  - market value
  - estimated sell price per unit
  - a count picker (the same arrows and text box as the trade screen)
  - an estimated total for the amount you picked
- **Negotiator picker.** Prices are estimated with your best negotiator by default, and you can pick another colonist from the dropdown. The estimate uses the same formula as vanilla trading, including the faction's price offset, drug and animal-product bonuses, and difficulty.
  - Only colonists vanilla would let negotiate are listed (`FactionUtility.CanTradeWith`), so for the Empire that means someone with the required title's trade permit. If nobody qualifies, *Form caravan* is disabled with vanilla's reason.
  - The chosen negotiator is added to the caravan.
- **"Won't buy" list.** A switch above the tabs flips between what the settlement buys and what it won't. The second list shows the nearest settlement that will buy each thing, with distance in tiles and travel time. *Look within* sets the range (10 to 200 tiles, or any value up to 500), and is remembered. Click a buyer to find it on the world map.
- **Colony picker**, shown when you have more than one colony.
- **Search box**, plus sorting by total value or by name.
- **Form caravan.** Opens the vanilla Form Caravan window with your chosen items already selected and the route already set to the settlement, so the route planner is skipped. Use *Change route* if you want a different path. Add pawns and travel supplies as usual.
  - If you chose to sell food, medicine or beds, the mod turns off *Automatically select travel supplies*. Otherwise vanilla would reset those items to its own picks.
  - Disabled for hostile factions: you can still browse the list, but a caravan routed there would attack on arrival.

You cannot trade from this window. It is only for planning.

- **Pawns** show the same warnings as the vanilla trade screen: ransom or slavery icons for captives, and bonded, pregnant, sick and rideable icons for animals. Estimated prices include Ideology's 0.6× factor for selling humanlike captives.

Item list source: the same reachable home-area items the Form Caravan window offers (`CaravanFormingUtility.AllReachableColonyItems`). Pawns come from `TradeUtility.AllSellableColonyPawns` (secure prisoners and slaves; healthy, calm, tame animals), limited to the pawns the Form Caravan window lists. Everything is filtered by the trader's `TraderKindDef.WillTrade` and `TradeUtility.PlayerSellableNow`, so rotten food, apparel worn by corpses, biocoded gear and captives from the trader's own faction (unless they're prisoners or slaves) are left out, just like in the real trade screen.

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
Textures/UI/Commands/                   settlement button icon
Source/WhatCanISell/
  WhatCanISellMod.cs          entry point, applies Harmony patches
  Patches.cs                  gizmo on trading places + button in vanilla "will buy" window
  ModdedPlaces.cs             notes for modded trading places (moving camps, barter)
  Textures.cs                 loads the button icon
  SellableStock.cs            gathers sellable colony items, estimates prices
  Dialog_ThingsYouCanSell.cs  the window and its "buys" list
  Dialog_ThingsYouCanSell.WontBuy.cs  the "won't buy" list and buyer search
  FormCaravanPrefill.cs       opens Form Caravan with the selection applied
```

## Not covered yet

- The "won't buy" list only checks settlements on the colony's own planet layer.
- Items already inside caravans or in pawns' inventories are not counted.

## Licence

MIT, see [LICENSE](LICENSE).
