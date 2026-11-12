# In-game checklist

Things to verify by hand before a release. Restart RimWorld after deploying a new build.

Last run: 2026-11-12 on build bae185b. The two fixes in b26c863 are deployed but not yet seen in game. Unticked items with a note were not fully tested.

## Buttons
- [x] Friendly settlement shows **Things you can sell** next to *Show what will buy*
- [x] Vanilla *Things this trader will buy* window has a **Things you can sell** button next to *Close*
- [ ] No button on your own settlements or permanent-enemy factions — *not tested; a hostile quest site correctly had no button*

## List
- [x] Only items the trader buys and you have are listed; silver isn't — *silver confirmed absent*
- [ ] Rotten food, corpse-worn apparel and biocoded gear are not listed — *not tested, none in the save*
- [ ] Prisoners, slaves and animals appear in their tabs; tab counts match — *animals only; no prisoners or slaves in the save*
- [x] Identical unnamed animals share one row with a count; named, bonded or pregnant ones get their own row
- [ ] Captive rows show the ransom/slavery icon; animal rows show bonded/pregnant/sick/rideable icons — *animal icons confirmed; no captives in the save*
- [x] Search and *Sort: total value / name* work; search box fully visible
- [x] Single pawns show a centred checkbox; selected rows stay highlighted
- [ ] With more than one colony, the colony dropdown switches the list — *not tested, only one colony*

## Prices
- [ ] *Est. sell price* matches the real trade screen for the same negotiator (try an item, a drug, a prisoner) — *not tested, needs a caravan at a settlement*
- [x] Changing the negotiator updates prices — *$753 to $563 switching Byrne to Ani*

## Negotiator
- [ ] Only colonists who can trade with this faction are offered — *two offered (Byrne, Ani); not checked against the full colony*
- [x] Empire settlement with no permit holder: *Negotiator: none available*, **Form caravan** disabled, tooltip names the missing permit
- [ ] Empire settlement with a permit holder: only that colonist (or those colonists) can be picked — *not tested, no permit holder*

## Form caravan
- [ ] Hostile settlement: **Form caravan** disabled with the hostile tooltip — *not tested, no hostile trading settlement found*
- [x] Selected items, pawns and the negotiator are pre-selected in the caravan window
- [x] Picking one animal from a grouped row sends that exact animal
- [ ] Route skips the planner and is set to the settlement (*Change route* still works) — *route set to the settlement; Change route not tried*
- [ ] Caravan travels to the settlement, with and without Instant Caravan — *not tested, caravan not sent*
- [ ] Selling food or medicine turns off auto travel supplies and shows the message — *auto supplies turned off; the message itself not seen*

## Won't buy list
- [x] *X buys* / *X won't buy* switch sits above the tabs and shows the selected list; counts match
- [x] Category tabs filter both lists, with their own counts
- [ ] A tab that's empty in one list shows *Nothing in this tab.*, not the "no buyer" message — *fixed in b26c863, not yet seen in game*
- [x] The list shows only things this trader won't buy that another settlement in range will
- [x] Nearest buyer, tiles and travel days look right; hover lists the other buyers
- [ ] *Look within* presets and *Custom...* slider (up to 500) update the list; the choice is remembered after a restart — *saved to the config file; reload after restart not seen*
- [ ] Hostile settlements, and Empire settlements nobody can trade with, are never listed as buyers — *Empire confirmed; hostile not checked*
- [ ] Travel days are grey, like the tile count — *fixed in b26c863, not yet seen in game*
- [x] Clicking a buyer closes the window, moves the world camera to that settlement and selects it
- [x] *Select all* is hidden in this list

## Performance
- [x] With dev mode on, closing the window logs gather, buyers and per-frame times — *gather 13.82 ms, buyers within 162 tiles 8.89 ms, window 0.665 ms per frame*

## Saves
- [ ] Adding the mod to an existing save loads without errors — *not tested*
- [ ] Removing it loads without errors (only the mod-list mismatch warning) — *not tested*
- [x] No red errors from *What Can I Sell?* in the log after all of the above — *none during the tests above*
