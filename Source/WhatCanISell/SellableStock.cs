using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WhatCanISell
{
    public enum SellableKind
    {
        Item,
        Captive,
        Animal,
    }

    public class SellableEntry
    {
        public readonly TransferableOneWay transferable;
        public readonly SellableKind kind;
        public float marketValue;
        public float sellPrice;
        public float massEach;
        private string label;

        public SellableEntry(TransferableOneWay transferable, SellableKind kind)
        {
            this.transferable = transferable;
            this.kind = kind;
        }

        public bool IsPawn => kind != SellableKind.Item;
        public int Available => transferable.GetMaximumToTransfer();
        public int Selected => transferable.CountToTransfer;
        public float TotalValueAvailable => sellPrice * Available;

        // Matches Tradeable_Pawn.Label for pawns.
        public string Label => label ??= BuildLabel();

        public string Tooltip
        {
            get
            {
                if (transferable.AnyThing is Pawn pawn)
                {
                    return Label + "\n\n" + pawn.MainDesc(writeFaction: true) + "\n\n" + pawn.DescriptionDetailed;
                }
                return Label + "\n\n" + transferable.TipDescription;
            }
        }

        private string BuildLabel()
        {
            if (!(transferable.AnyThing is Pawn pawn))
            {
                return transferable.LabelCap;
            }
            string text = pawn.LabelNoCount;
            if (pawn.Name != null && !pawn.Name.Numerical && !pawn.RaceProps.Humanlike)
            {
                text += ", " + pawn.def.label;
            }
            text += " (" + pawn.GetGenderLabel() + ", " + Mathf.FloorToInt(pawn.ageTracker.AgeBiologicalYearsFloat) + ")";
            if (kind == SellableKind.Captive)
            {
                text += " - " + (pawn.IsSlave ? "WCIS_Slave" : "WCIS_Prisoner").Translate();
            }
            return text.CapitalizeFirst();
        }
    }

    public static class SellableStock
    {
        public static bool CanShowFor(Settlement settlement)
        {
            return settlement?.TraderKind != null
                && settlement.Faction != null
                && settlement.Faction != Faction.OfPlayer
                && !settlement.Faction.def.permanentEnemy;
        }

        public static bool CanSendCaravanTo(Settlement settlement)
        {
            return CanShowFor(settlement) && !settlement.Faction.HostileTo(Faction.OfPlayer);
        }

        public static List<SellableEntry> Gather(Map map, Settlement settlement, Pawn negotiator)
        {
            var result = new List<SellableEntry>();
            TraderKindDef kind = settlement?.TraderKind;
            if (map == null || kind == null)
            {
                return result;
            }

            // Grouped with the same mode Dialog_FormCaravan uses, so each row maps onto one of its rows.
            var itemGroups = new List<TransferableOneWay>();
            foreach (Thing thing in CaravanFormingUtility.AllReachableColonyItems(map))
            {
                Thing inner = thing.GetInnerIfMinified();
                if (inner.def == ThingDefOf.Silver || !kind.WillTrade(inner.def) || !TradeUtility.PlayerSellableNow(thing, settlement))
                {
                    continue;
                }
                AddToGroup(itemGroups, thing);
            }

            var sendable = new HashSet<Pawn>(Dialog_FormCaravan.AllSendablePawns(map, reform: false));
            var pawnGroups = new List<TransferableOneWay>();
            foreach (Pawn pawn in TradeUtility.AllSellableColonyPawns(map, checkAcceptableTemperatureOfAnimals: false))
            {
                if (sendable.Contains(pawn) && kind.WillTrade(pawn.def) && TradeUtility.PlayerSellableNow(pawn, settlement))
                {
                    AddToGroup(pawnGroups, pawn);
                }
            }

            result.AddRange(itemGroups.Select(g => new SellableEntry(g, SellableKind.Item)));
            foreach (TransferableOneWay group in pawnGroups)
            {
                Pawn pawn = (Pawn)group.AnyThing;
                result.Add(new SellableEntry(group, pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony ? SellableKind.Captive : SellableKind.Animal));
            }

            UpdatePrices(result, settlement, negotiator);
            return result;
        }

        private static void AddToGroup(List<TransferableOneWay> groups, Thing thing)
        {
            TransferableOneWay group = TransferableUtility.TransferableMatching(thing, groups, TransferAsOneMode.PodsOrCaravanPacking);
            if (group == null)
            {
                group = new TransferableOneWay();
                groups.Add(group);
            }
            if (!group.things.Contains(thing))
            {
                group.things.Add(thing);
            }
        }

        public static void UpdatePrices(List<SellableEntry> entries, Settlement settlement, Pawn negotiator)
        {
            foreach (SellableEntry entry in entries)
            {
                Thing any = entry.transferable.AnyThing;
                Thing inner = any.GetInnerIfMinified();
                entry.marketValue = inner.MarketValue;
                // Caravan mass usage excludes pawns' own body mass.
                entry.massEach = entry.IsPawn ? 0f : any.GetStatValue(StatDefOf.Mass);
                entry.sellPrice = EstimateSellPrice(inner, settlement, negotiator);
            }
        }

        // Player-sells side of Tradeable.InitPriceDataIfNeeded, without a TradeSession.
        public static float EstimateSellPrice(Thing thing, Settlement settlement, Pawn negotiator)
        {
            TraderKindDef kind = settlement.TraderKind;
            TradeCurrency currency = settlement.TradeCurrency;

            float negotiatorGain = negotiator?.GetStatValue(StatDefOf.TradePriceImprovement) ?? 0f;
            float settlementGain = settlement.TradePriceImprovementOffsetForPlayer;
            float drugBonus = 0f;
            float animalProduceBonus = 0f;
            if (negotiator != null)
            {
                if (thing.def.IsNonMedicalDrug)
                {
                    drugBonus = negotiator.GetStatValue(StatDefOf.DrugSellPriceImprovement);
                }
                if (ModsConfig.IdeologyActive && (thing.def.IsLeather || thing.def.IsMeat || thing.def.IsWool))
                {
                    animalProduceBonus = negotiator.GetStatValue(StatDefOf.AnimalProductsSellImprovement);
                }
            }

            float humanPawnSellFactor = 1f;
            float joinAsBuyFactor = 1f;
            if (ModsConfig.IdeologyActive && thing is Pawn pawn && pawn.RaceProps.Humanlike && pawn.guest != null)
            {
                humanPawnSellFactor = 0.6f;
                if (pawn.guest.joinStatus == JoinStatus.JoinAsSlave)
                {
                    joinAsBuyFactor = 0.6f;
                }
            }

            float sellFactor = kind.PriceTypeFor(thing.def, TradeAction.PlayerSells).PriceMultiplier();
            float sell = TradeUtility.GetPricePlayerSell(thing, sellFactor, humanPawnSellFactor, negotiatorGain, settlementGain, drugBonus, animalProduceBonus, currency);

            if (currency == TradeCurrency.Silver)
            {
                float buyFactor = kind.PriceTypeFor(thing.def, TradeAction.PlayerBuys).PriceMultiplier();
                float buy = TradeUtility.GetPricePlayerBuy(thing, buyFactor, joinAsBuyFactor, negotiatorGain, settlementGain);
                sell = Mathf.Min(sell, buy);
            }
            return sell;
        }

        public static List<Pawn> NegotiatorCandidates(Map map)
        {
            if (map == null)
            {
                return new List<Pawn>();
            }
            return map.mapPawns.FreeColonistsSpawned
                .Where(p => p.skills != null && !p.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
                .OrderByDescending(p => p.GetStatValue(StatDefOf.TradePriceImprovement))
                .ToList();
        }

        public static string FormatPrice(float amount, Settlement settlement)
        {
            if (settlement.TradeCurrency == TradeCurrency.Favor)
            {
                return "WCIS_Favor".Translate(amount.ToString("0.#"));
            }
            return amount.ToStringMoney();
        }
    }
}
