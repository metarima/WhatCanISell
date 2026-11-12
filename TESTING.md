# In-game checklist

Things to verify by hand before a release. Restart RimWorld after deploying a new build.

## Buttons
- [x] Friendly settlement shows **Things you can sell** next to *Show what will buy*
- [x] Vanilla *Things this trader will buy* window has a **Things you can sell** button next to *Close*
- [x] No button on your own settlements or permanent-enemy factions

## List
- [x] Only items the trader buys and you have are listed; silver isn't
- [x] Rotten food, corpse-worn apparel and biocoded gear are not listed
- [x] Prisoners, slaves and animals appear in their tabs; tab counts match
- [x] Identical unnamed animals share one row with a count; named, bonded or pregnant ones get their own row
- [x] Captive rows show the ransom/slavery icon; animal rows show bonded/pregnant/sick/rideable icons
- [x] Search and *Sort: total value / name* work; search box fully visible
- [x] Single pawns show a centred checkbox; selected rows stay highlighted
- [x] With more than one colony, the colony dropdown switches the list

## Prices
- [x] *Est. sell price* matches the real trade screen for the same negotiator (try an item, a drug, a prisoner)
- [x] Changing the negotiator updates prices

## Negotiator
- [x] Only colonists who can trade with this faction are offered
- [x] Empire settlement with no permit holder: *Negotiator: none available*, **Form caravan** disabled, tooltip names the missing permit
- [x] Empire settlement with a permit holder: only that colonist (or those colonists) can be picked

## Form caravan
- [x] Hostile settlement: **Form caravan** disabled with the hostile tooltip
- [x] Selected items, pawns and the negotiator are pre-selected in the caravan window
- [x] Picking one animal from a grouped row sends that exact animal
- [x] Route skips the planner and is set to the settlement (*Change route* still works)
- [x] Caravan travels to the settlement, with and without Instant Caravan
- [x] Selling food or medicine turns off auto travel supplies and shows the message

## Won't buy list
- [x] *X buys* / *X won't buy* switch sits above the tabs and shows the selected list; counts match
- [x] Category tabs filter both lists, with their own counts
- [x] A tab that's empty in one list shows *Nothing in this tab.*, not the "no buyer" message
- [x] The list shows only things this trader won't buy that another settlement in range will
- [x] Nearest buyer, tiles and travel days look right; hover lists the other buyers
- [x] *Look within* presets and *Custom...* slider (up to 500) update the list; the choice is remembered after a restart
- [x] Hostile settlements, and Empire settlements nobody can trade with, are never listed as buyers
- [x] Travel days are grey, like the tile count
- [x] Clicking a buyer closes the window, moves the world camera to that settlement and selects it
- [x] *Select all* is hidden in this list

## Performance
- [x] With dev mode on, closing the window logs gather, buyers and per-frame times

## Saves
- [x] Adding the mod to an existing save loads without errors
- [x] Removing it loads without errors (only the mod-list mismatch warning)
- [x] No red errors from *What Can I Sell?* in the log after all of the above
