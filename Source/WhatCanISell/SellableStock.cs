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

    public readonly struct Buyer
    {
        public readonly Settlement settlement;
        public readonly int tiles;

        public Buyer(Settlement settlement, int tiles)
        {
            this.settlement = settlement;
            this.tiles = tiles;
        }
    }

    public class SellableEntry
    {
        public readonly TransferableOneWay transferable;
        public readonly SellableKind kind;
        public float marketValue;
        public float sellPrice;
        public float massEach;
        // Only set for things the current trader won't buy: other settlements that will, nearest first.
        public List<Buyer> buyers;
        private string label;

        // Nothing moves while the window has the game paused, so these are filled once per price update.
        public int available;
        public string availableText;
        public string marketText;
        public string priceText;

        public SellableEntry(TransferableOneWay transferable, SellableKind kind)
        {
            this.transferable = transferable;
            this.kind = kind;
        }

        public bool IsPawn => kind != SellableKind.Item;
        public int Selected => transferable.CountToTransfer;
        public float TotalValueAvailable => sellPrice * available;

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

        // Splits the colony's sellable stock into what this settlement buys and, in elsewhere, what it doesn't.
        public static List<SellableEntry> Gather(Map map, Settlement settlement, Pawn negotiator, List<SellableEntry> elsewhere)
        {
            var result = new List<SellableEntry>();
            elsewhere.Clear();
            TraderKindDef kind = settlement?.TraderKind;
            if (map == null || kind == null)
            {
                return result;
            }

            // Grouped with the same mode Dialog_FormCaravan uses, so each row maps onto one of its rows.
            var items = new Groups();
            var otherItems = new Groups();
            foreach (Thing thing in CaravanFormingUtility.AllReachableColonyItems(map))
            {
                Thing inner = thing.GetInnerIfMinified();
                if (inner.def == ThingDefOf.Silver || !TradeUtility.EverPlayerSellable(inner.def))
                {
                    continue;
                }
                if (kind.WillTrade(inner.def) && TradeUtility.PlayerSellableNow(thing, settlement))
                {
                    items.Add(thing);
                }
                else
                {
                    otherItems.Add(thing);
                }
            }

            var sendable = new HashSet<Pawn>(Dialog_FormCaravan.AllSendablePawns(map, reform: false));
            var pawns = new Groups();
            var otherPawns = new Groups();
            foreach (Pawn pawn in TradeUtility.AllSellableColonyPawns(map, checkAcceptableTemperatureOfAnimals: false))
            {
                if (!sendable.Contains(pawn))
                {
                    continue;
                }
                if (kind.WillTrade(pawn.def) && TradeUtility.PlayerSellableNow(pawn, settlement))
                {
                    pawns.Add(pawn);
                }
                else
                {
                    otherPawns.Add(pawn);
                }
            }

            AddEntries(result, items, pawns);
            AddEntries(elsewhere, otherItems, otherPawns);
            UpdatePrices(result, settlement, negotiator);
            return result;
        }

        private static void AddEntries(List<SellableEntry> into, Groups items, Groups pawns)
        {
            foreach (TransferableOneWay group in items.all)
            {
                into.Add(new SellableEntry(group, SellableKind.Item));
            }
            foreach (TransferableOneWay group in pawns.all)
            {
                Pawn pawn = (Pawn)group.AnyThing;
                into.Add(new SellableEntry(group, pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony ? SellableKind.Captive : SellableKind.Animal));
            }
        }

        // TransferableMatching only ever matches things of the same def, so bucketing by def
        // avoids comparing every thing against every group.
        private sealed class Groups
        {
            public readonly List<TransferableOneWay> all = new List<TransferableOneWay>();
            private readonly Dictionary<ThingDef, List<TransferableOneWay>> byDef = new Dictionary<ThingDef, List<TransferableOneWay>>();

            public void Add(Thing thing)
            {
                ThingDef def = thing.GetInnerIfMinified().def;
                if (!byDef.TryGetValue(def, out List<TransferableOneWay> groups))
                {
                    groups = new List<TransferableOneWay>();
                    byDef[def] = groups;
                }
                TransferableOneWay group = TransferableUtility.TransferableMatching(thing, groups, TransferAsOneMode.PodsOrCaravanPacking);
                if (group == null)
                {
                    group = new TransferableOneWay();
                    groups.Add(group);
                    all.Add(group);
                }
                if (!group.things.Contains(thing))
                {
                    group.things.Add(thing);
                }
            }
        }

        // Fills in buyers for the entries the current settlement won't buy and returns those that have any.
        public static List<SellableEntry> FindBuyers(Map map, Settlement here, List<SellableEntry> candidates, int maxTiles)
        {
            var result = new List<SellableEntry>();
            if (map == null || candidates.Count == 0)
            {
                return result;
            }

            List<Buyer> nearby = NearbyTradingSettlements(map, here, maxTiles);
            if (nearby.Count == 0)
            {
                return result;
            }

            var willTrade = new Dictionary<(TraderKindDef, ThingDef), bool>();
            foreach (SellableEntry entry in candidates)
            {
                entry.buyers = null;
                Thing any = entry.transferable.AnyThing;
                ThingDef def = any.GetInnerIfMinified().def;
                foreach (Buyer buyer in nearby)
                {
                    TraderKindDef kind = buyer.settlement.TraderKind;
                    if (!willTrade.TryGetValue((kind, def), out bool trades))
                    {
                        trades = kind.WillTrade(def);
                        willTrade[(kind, def)] = trades;
                    }
                    if (trades && TradeUtility.PlayerSellableNow(any, buyer.settlement))
                    {
                        (entry.buyers ??= new List<Buyer>()).Add(buyer);
                    }
                }
                if (entry.buyers != null)
                {
                    result.Add(entry);
                }
            }
            return result;
        }

        // Settlements a colonist could reach and trade with, within maxTiles of the colony, nearest first.
        private static List<Buyer> NearbyTradingSettlements(Map map, Settlement here, int maxTiles)
        {
            var result = new List<Buyer>();
            PlanetTile origin = map.Tile;
            var byTile = new Dictionary<PlanetTile, Settlement>();
            foreach (Settlement settlement in Find.WorldObjects.Settlements)
            {
                if (settlement != here && settlement.Tile.Layer == origin.Layer && CanSendCaravanTo(settlement))
                {
                    byTile[settlement.Tile] = settlement;
                }
            }
            if (byTile.Count == 0)
            {
                return result;
            }

            // One breadth-first fill gives the traversal distance to every settlement in range.
            int remaining = byTile.Count;
            origin.Layer.Filler.FloodFill(origin, tile => !Find.World.Impassable(tile), new Predicate<PlanetTile, int>((tile, dist) =>
            {
                if (dist > maxTiles)
                {
                    return true;
                }
                if (byTile.TryGetValue(tile, out Settlement settlement))
                {
                    result.Add(new Buyer(settlement, dist));
                    return --remaining == 0;
                }
                return false;
            }), Find.WorldGrid.TilesNumWithinTraversalDistance(maxTiles + 1));

            List<Pawn> colonists = PotentialNegotiators(map);
            result.RemoveAll(b => !colonists.Any(p => p.CanTradeWith(b.settlement.Faction, b.settlement.TraderKind).Accepted));
            return result;
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
                Settlement buyer = PriceSettlement(entry, settlement);
                entry.sellPrice = EstimateSellPrice(inner, buyer, negotiator);
                entry.available = entry.transferable.GetMaximumToTransfer();
                entry.availableText = entry.available.ToStringCached();
                entry.marketText = entry.marketValue.ToStringMoney();
                entry.priceText = FormatPrice(entry.sellPrice, buyer);
            }
        }

        // Entries sold elsewhere are priced at their nearest buyer.
        public static Settlement PriceSettlement(SellableEntry entry, Settlement settlement)
        {
            return entry.buyers.NullOrEmpty() ? settlement : entry.buyers[0].settlement;
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

        // Same rules vanilla uses to pick a caravan's negotiator (BestCaravanPawnUtility, CaravanVisitUtility),
        // limited to colonists the Form Caravan window can send.
        public static List<Pawn> NegotiatorCandidates(Map map, Settlement settlement)
        {
            return PotentialNegotiators(map)
                .Where(p => p.CanTradeWith(settlement.Faction, settlement.TraderKind).Accepted)
                .OrderByDescending(p => p.GetStatValue(StatDefOf.TradePriceImprovement))
                .ToList();
        }

        private static List<Pawn> PotentialNegotiators(Map map)
        {
            if (map == null)
            {
                return new List<Pawn>();
            }
            var sendable = new HashSet<Pawn>(Dialog_FormCaravan.AllSendablePawns(map, reform: false));
            return map.mapPawns.FreeColonistsSpawned
                .Where(p => sendable.Contains(p)
                    && !p.Downed && !p.InMentalState
                    && !StatDefOf.TradePriceImprovement.Worker.IsDisabledFor(p))
                .ToList();
        }

        public static string NoNegotiatorReason(Settlement settlement)
        {
            RoyalTitlePermitDef permit = settlement.TraderKind.permitRequiredForTrading;
            return permit != null
                ? "CommandTradeFailNeedPermit".Translate(permit.LabelCap)
                : "CommandTradeFailNoNegotiator".Translate();
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
