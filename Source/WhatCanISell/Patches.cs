using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WhatCanISell
{
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.GetGizmos))]
    public static class Patch_Settlement_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Settlement __instance)
        {
            bool show = SellableStock.CanShowFor(__instance);
            bool added = false;
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
                if (show && !added && gizmo is Command_Action cmd && cmd.icon == Settlement.ShowSellableItemsCommand)
                {
                    added = true;
                    yield return MakeCommand(__instance);
                }
            }
            if (show && !added)
            {
                yield return MakeCommand(__instance);
            }
        }

        private static Command_Action MakeCommand(Settlement settlement)
        {
            return new Command_Action
            {
                defaultLabel = "WCIS_CommandLabel".Translate(),
                defaultDesc = "WCIS_CommandDesc".Translate(),
                icon = Textures.ThingsYouCanSell,
                action = () => Find.WindowStack.Add(new Dialog_ThingsYouCanSell(settlement)),
            };
        }
    }

    [HarmonyPatch(typeof(Dialog_SellableItems), "DoBottomButtons")]
    public static class Patch_Dialog_SellableItems_DoBottomButtons
    {
        private const float ButtonWidth = 200f;
        private const float ButtonHeight = 40f;

        public static void Postfix(Dialog_SellableItems __instance, Rect rect, ITrader ___trader)
        {
            if (!(___trader is Settlement settlement) || !SellableStock.CanShowFor(settlement))
            {
                return;
            }
            Rect button = new Rect(rect.width - ButtonWidth, rect.height - 55f, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(button, "WCIS_CommandLabel".Translate()))
            {
                __instance.Close();
                Find.WindowStack.Add(new Dialog_ThingsYouCanSell(settlement));
            }
        }
    }
}
