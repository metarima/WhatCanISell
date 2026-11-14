using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WhatCanISell
{
    // The "won't buy" list: things this place turns down, and the nearest settlements that take them.
    public partial class Dialog_ThingsYouCanSell
    {
        private bool showElsewhere;
        private readonly List<SellableEntry> elsewhereCandidates = new List<SellableEntry>();
        private List<SellableEntry> elsewhere = new List<SellableEntry>();
        private readonly Dictionary<WorldObject, string> travelTimes = new Dictionary<WorldObject, string>();

        private static readonly int[] LookWithinPresets = { 10, 25, 50, 100, 200 };

        // Right to left: nearest buyer, sell price there, available.
        private static readonly Columns BuyerColumns = new Columns(330f, 100f, 90f);
        private const int ColBuyer = 0, ColBuyerPrice = 1, ColBuyerAvailable = 2;

        private static int LookWithin => WhatCanISellMod.Settings.lookWithinTiles;

        private void UpdateBuyers()
        {
            elsewhere = SellableStock.FindBuyers(map, place, elsewhereCandidates, LookWithin);
            SellableStock.UpdatePrices(elsewhere, place, negotiator);
            dirty = true;
        }

        private void SetLookWithin(int tiles)
        {
            WhatCanISellMod.Settings.lookWithinTiles = Mathf.Clamp(tiles, 1, WhatCanISellSettings.MaxLookWithinTiles);
            WhatCanISellMod.Instance.WriteSettings();
            UpdateBuyers();
        }

        private void DrawWontBuyList(Rect rect)
        {
            float width = rect.width - 16f;
            Rect rangeRect = new Rect(rect.x + width - 200f, rect.y, 200f, ControlsHeight);
            if (Widgets.ButtonText(rangeRect, "WCIS_LookWithin".Translate(LookWithin)))
            {
                List<FloatMenuOption> options = LookWithinPresets
                    .Select(n => new FloatMenuOption("WCIS_Tiles".Translate(n), () => SetLookWithin(n)))
                    .ToList();
                options.Add(new FloatMenuOption("WCIS_LookWithinCustom".Translate(), () =>
                    Find.WindowStack.Add(new Dialog_Slider(n => "WCIS_LookWithinSlider".Translate(n), 1, WhatCanISellSettings.MaxLookWithinTiles, SetLookWithin, LookWithin))));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (Mouse.IsOver(rangeRect))
            {
                TooltipHandler.TipRegion(rangeRect, "WCIS_LookWithinTip".Translate());
            }

            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + LabelX, rect.y, rangeRect.x - rect.x - LabelX - 8f, ControlsHeight), "WCIS_ElsewhereHint".Translate(place.LabelCap));

            Rect header = new Rect(rect.x, rangeRect.yMax + 6f, width, HeaderHeight);
            Widgets.Label(new Rect(rect.x + LabelX, header.y, BuyerColumns.Start(width, ColBuyerAvailable) - LabelX, HeaderHeight), "WCIS_ColItem".Translate());
            Rect buyerHeader = BuyerColumns.Rect(header, width, ColBuyer);
            buyerHeader.xMin += 12f;
            Widgets.Label(buyerHeader, "WCIS_ColNearestBuyer".Translate());
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(BuyerColumns.Rect(header, width, ColBuyerAvailable), "WCIS_ColInStorage".Translate());
            Widgets.Label(BuyerColumns.Rect(header, width, ColBuyerPrice), "WCIS_ColSellPrice".Translate());
            GUI.color = Color.white;

            DrawRows(new Rect(rect.x, header.yMax, rect.width, rect.yMax - header.yMax), DrawWontBuyRow, "WCIS_NothingElsewhere".Translate(LookWithin));
        }

        private void DrawWontBuyRow(Rect row, SellableEntry entry, int index)
        {
            float width = row.width;
            WorldObject nearest = entry.buyers[0].place;
            DrawRowStart(row, entry, index, BuyerColumns.Start(width, ColBuyerAvailable), nearest);

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(BuyerColumns.Rect(row, width, ColBuyerAvailable), entry.availableText);
            Widgets.Label(BuyerColumns.Rect(row, width, ColBuyerPrice), entry.priceText);

            Rect buyerRect = BuyerColumns.Rect(row, width, ColBuyer);
            buyerRect.xMin += 12f;
            bool over = Mouse.IsOver(buyerRect);
            string name = nearest.LabelCap;
            float nameWidth = Mathf.Min(Text.CalcSize(name).x, buyerRect.width * 0.55f);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = over ? GenUI.MouseoverColor : Color.white;
            Widgets.Label(new Rect(buyerRect.x, buyerRect.y, nameWidth, buyerRect.height), name.Truncate(nameWidth, truncated));
            GUI.color = Color.gray;
            Widgets.Label(new Rect(buyerRect.x + nameWidth, buyerRect.y, buyerRect.width - nameWidth, buyerRect.height),
                " · " + "WCIS_Tiles".Translate(entry.buyers[0].tiles) + " · " + TravelTime(nearest));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            if (over)
            {
                TooltipHandler.TipRegion(buyerRect, BuyersTooltip(entry));
            }
            if (Widgets.ButtonInvisible(buyerRect))
            {
                // Show it on the world map rather than opening its window, so the player sees where it is first.
                Close(doCloseSound: false);
                CameraJumper.TryJumpAndSelect(nearest);
            }
        }

        // Pathfinding is the costly part, so it runs once per place and only for buyers actually on screen.
        private string TravelTime(WorldObject buyer)
        {
            if (!travelTimes.TryGetValue(buyer, out string text))
            {
                int ticks = CaravanArrivalTimeEstimator.EstimatedTicksToArrive(map.Tile, buyer.Tile, null);
                text = ticks > 0 ? ticks.ToStringTicksToDays("0.#") : "-";
                travelTimes[buyer] = text;
            }
            return text;
        }

        private string BuyersTooltip(SellableEntry entry)
        {
            const int MaxListed = 8;
            var sb = new StringBuilder();
            sb.Append("WCIS_BuyersTip".Translate(LookWithin));
            foreach (Buyer buyer in entry.buyers.Take(MaxListed))
            {
                sb.Append("\n  ").Append(buyer.place.LabelCap).Append(" (").Append(buyer.place.Faction.Name).Append(") · ")
                    .Append("WCIS_Tiles".Translate(buyer.tiles));
            }
            if (entry.buyers.Count > MaxListed)
            {
                sb.Append("\n  ...");
            }
            sb.Append("\n\n").Append("WCIS_OpenBuyer".Translate(entry.buyers[0].place.LabelCap));
            return sb.ToString();
        }
    }
}
