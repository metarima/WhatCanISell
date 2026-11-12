# In-game checklist

Things to verify by hand before a release. Restart RimWorld after deploying a new build.

## Buttons
- [ ] Friendly settlement shows **Things you can sell** next to *Show what will buy*
- [ ] Vanilla *Things this trader will buy* window has a **Things you can sell** button next to *Close*
- [ ] No button on your own settlements or permanent-enemy factions

## List
- [ ] Only items the trader buys and you have are listed; silver isn't
- [ ] Rotten food, corpse-worn apparel and biocoded gear are not listed
- [ ] Prisoners, slaves and animals appear in their tabs; tab counts match
- [ ] Identical unnamed animals share one row with a count; named, bonded or pregnant ones get their own row
- [ ] Captive rows show the ransom/slavery icon; animal rows show bonded/pregnant/sick/rideable icons
- [ ] Search and *Sort: total value / name* work; search box fully visible
- [ ] Single pawns show a centred checkbox; selected rows stay highlighted
- [ ] With more than one colony, the colony dropdown switches the list

## Prices
- [ ] *Est. sell price* matches the real trade screen for the same negotiator (try an item, a drug, a prisoner)
- [ ] Changing the negotiator updates prices

## Negotiator
- [ ] Only colonists who can trade with this faction are offered
- [ ] Empire settlement with no permit holder: *Negotiator: none available*, **Form caravan** disabled, tooltip names the missing permit
- [ ] Empire settlement with a permit holder: only that colonist (or those colonists) can be picked

## Form caravan
- [ ] Hostile settlement: **Form caravan** disabled with the hostile tooltip
- [ ] Selected items, pawns and the negotiator are pre-selected in the caravan window
- [ ] Picking one animal from a grouped row sends that exact animal
- [ ] Route skips the planner and is set to the settlement (*Change route* still works)
- [ ] Caravan travels to the settlement, with and without Instant Caravan
- [ ] Selling food or medicine turns off auto travel supplies and shows the message

## Won't buy list
- [ ] *X buys* / *X won't buy* switch sits above the tabs and shows the selected list; counts match
- [ ] Category tabs filter both lists, with their own counts
- [ ] A tab that's empty in one list shows *Nothing in this tab.*, not the "no buyer" message
- [ ] The list shows only things this trader won't buy that another settlement in range will
- [ ] Nearest buyer, tiles and travel days look right; hover lists the other buyers
- [ ] *Look within* presets and *Custom...* slider (up to 500) update the list; the choice is remembered after a restart
- [ ] Hostile settlements, and Empire settlements nobody can trade with, are never listed as buyers
- [ ] Travel days are grey, like the tile count
- [ ] Clicking a buyer closes the window, moves the world camera to that settlement and selects it
- [ ] *Select all* is hidden in this list

## Performance
- [ ] With dev mode on, closing the window logs gather, buyers and per-frame times

## Saves
- [ ] Adding the mod to an existing save loads without errors
- [ ] Removing it loads without errors (only the mod-list mismatch warning)
- [ ] No red errors from *What Can I Sell?* in the log after all of the above
