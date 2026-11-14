using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WhatCanISell
{
    // Trading places from other mods that don't behave like a vanilla settlement. Looked up by type name,
    // so this mod doesn't depend on them.
    public static class ModdedPlaces
    {
        // Vanilla Expanded Framework camps that wander the world map, e.g. VFE Medieval 2's Merchant's guild.
        private const string MovingBaseType = "VEF.Planet.MovingBase";
        // VFE Medieval 2's Merchant's guild barters, and scales what it pays for each kind of item by 0.1 to 2.
        private const string BarteringType = "VFEMedieval.MerchantGuild";

        private static readonly Color WarningColor = new Color(1f, 0.8f, 0.35f);

        // Lines for the header of the sell window. Nothing moves while it has the game paused,
        // so these are worked out once when it opens.
        public static List<(string text, Color color)> Notes(WorldObject place)
        {
            var notes = new List<(string text, Color color)>();
            try
            {
                Type type = place.GetType();
                if (InheritsFrom(type, MovingBaseType))
                {
                    if (IsMovingNow(place))
                    {
                        notes.Add(("WCIS_MovingNow".Translate(place.LabelCap), WarningColor));
                    }
                    else
                    {
                        // Their own inspect string says how long they stay, e.g. "will pack and move in: 3.6 days".
                        string stay = place.GetInspectString();
                        if (!stay.NullOrEmpty())
                        {
                            notes.Add((stay.Split('\n')[0], Color.gray));
                        }
                    }
                }
                if (InheritsFrom(type, BarteringType))
                {
                    notes.Add(("WCIS_BarterNote".Translate(place.LabelCap), Color.gray));
                }
            }
            catch (Exception e)
            {
                Log.WarningOnce(WhatCanISellMod.LogPrefix + "Couldn't read details of " + place.GetType().FullName + ": " + e, place.GetType().GetHashCode());
            }
            return notes;
        }

        private static bool IsMovingNow(WorldObject place)
        {
            object pather = AccessTools.Field(place.GetType(), "pather")?.GetValue(place);
            return pather != null && Traverse.Create(pather).Property("MovingNow").GetValue<bool>();
        }

        private static bool InheritsFrom(Type type, string fullName)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                if (t.FullName == fullName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
