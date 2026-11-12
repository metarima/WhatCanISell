using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WhatCanISell
{
    public static class FormCaravanPrefill
    {
        private class Pending
        {
            public Dialog_FormCaravan dialog;
            public Settlement destination;
            public List<(List<Thing> things, int count)> selections;
        }

        private static Pending pending;

        private static readonly AccessTools.FieldRef<Dialog_FormCaravan, bool> AutoSelectTravelSupplies =
            AccessTools.FieldRefAccess<Dialog_FormCaravan, bool>("autoSelectTravelSupplies");

        private static readonly Action<Dialog_FormCaravan> NotifyTransferablesChanged =
            AccessTools.MethodDelegate<Action<Dialog_FormCaravan>>(AccessTools.Method(typeof(Dialog_FormCaravan), "Notify_TransferablesChanged"));

        public static void Open(Map map, Settlement destination, List<(List<Thing> things, int count)> selections)
        {
            var dialog = new Dialog_FormCaravan(map);
            pending = new Pending { dialog = dialog, destination = destination, selections = selections };
            Find.WindowStack.Add(dialog);
        }

        internal static void TryApply(Dialog_FormCaravan dialog)
        {
            if (pending == null || pending.dialog != dialog)
            {
                return;
            }
            Pending p = pending;
            pending = null;

            try
            {
                Apply(dialog, p);
            }
            catch (Exception e)
            {
                Log.Error(WhatCanISellMod.LogPrefix + "Failed to pre-fill the caravan: " + e);
            }
        }

        private static void Apply(Dialog_FormCaravan dialog, Pending p)
        {
            var wanted = new Dictionary<TransferableOneWay, int>();
            var chosen = new Dictionary<TransferableOneWay, List<Thing>>();

            foreach ((List<Thing> things, int count) in p.selections)
            {
                int remaining = count;
                foreach (Thing thing in things)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }
                    TransferableOneWay target = dialog.transferables.FirstOrDefault(t => t.things.Contains(thing));
                    if (target == null)
                    {
                        continue;
                    }
                    int take = Math.Min(remaining, thing.stackCount);
                    wanted.TryGetValue(target, out int already);
                    wanted[target] = already + take;
                    if (!chosen.TryGetValue(target, out List<Thing> picks))
                    {
                        chosen[target] = picks = new List<Thing>();
                    }
                    picks.Add(thing);
                    remaining -= take;
                }
            }

            bool selectedTravelSupplies = false;
            foreach (KeyValuePair<TransferableOneWay, int> kv in wanted)
            {
                TransferableOneWay target = kv.Key;
                // The caravan takes things from the front of the list, so put the player's
                // picks first; otherwise a grouped row could send a different animal.
                List<Thing> picks = chosen[target];
                target.things.RemoveAll(picks.Contains);
                target.things.InsertRange(0, picks);
                target.AdjustTo(target.ClampAmount(kv.Value));
                selectedTravelSupplies |= IsAutoSelectedTravelSupply(target);
            }

            // Auto-selection would reset food/medicine/beds to its own amounts.
            if (selectedTravelSupplies && AutoSelectTravelSupplies(dialog))
            {
                AutoSelectTravelSupplies(dialog) = false;
                Messages.Message("WCIS_AutoSuppliesOff".Translate(), MessageTypeDefOf.CautionInput, historical: false);
            }
            NotifyTransferablesChanged(dialog);
            SetDestination(dialog, p.destination);
        }

        // Set the route directly instead of relying on the route planner's Accept button:
        // leaving the planner any other way, or mods that send without a route (Instant Caravan),
        // would otherwise form a caravan with no destination.
        private static void SetDestination(Dialog_FormCaravan dialog, Settlement destination)
        {
            if (destination == null || destination.Destroyed)
            {
                return;
            }
            PlanetTile from = dialog.CurrentTile;
            PlanetTile to = destination.Tile;
            if (to.Layer != from.Layer || !Find.WorldReachability.CanReach(from, to))
            {
                return;
            }

            WorldRoutePlanner planner = Find.WorldRoutePlanner;
            if (planner.FormingCaravan)
            {
                planner.Stop();
            }
            if (!Find.WindowStack.IsOpen(dialog))
            {
                Find.WindowStack.Add(dialog);
            }
            dialog.Notify_ChoseRoute(to);
        }

        // Same categories Dialog_FormCaravan.SelectApproximateBestTravelSupplies resets.
        private static bool IsAutoSelectedTravelSupply(TransferableOneWay t)
        {
            if (!t.HasAnyThing || t.ThingDef.category == ThingCategory.Pawn)
            {
                return false;
            }
            ThingDef def = t.ThingDef;
            if (!def.thingCategories.NullOrEmpty() && def.thingCategories.Contains(ThingCategoryDefOf.Medicine))
            {
                return true;
            }
            if (def.IsIngestible && !def.IsDrug && !def.IsCorpse)
            {
                return true;
            }
            if (ModsConfig.BiotechActive && def == ThingDefOf.HemogenPack)
            {
                return true;
            }
            return t.AnyThing.GetInnerIfMinified() is Building_Bed bed && bed.def.building.bed_caravansCanUse;
        }
    }

    [HarmonyPatch(typeof(Dialog_FormCaravan), nameof(Dialog_FormCaravan.PostOpen))]
    public static class Patch_Dialog_FormCaravan_PostOpen
    {
        public static void Postfix(Dialog_FormCaravan __instance)
        {
            FormCaravanPrefill.TryApply(__instance);
        }
    }
}
