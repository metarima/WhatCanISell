using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WhatCanISell
{
    public class Dialog_ThingsYouCanSell : Window
    {
        private readonly Settlement settlement;
        private readonly List<Map> colonyMaps;
        private Map map;
        private Pawn negotiator;
        private string negotiatorLabel;
        private List<SellableEntry> entries = new List<SellableEntry>();
        private readonly List<SellableEntry> elsewhereCandidates = new List<SellableEntry>();
        private List<SellableEntry> elsewhere = new List<SellableEntry>();
        private readonly List<SellableEntry> visible = new List<SellableEntry>();
        private bool visibleDirty = true;
        private bool showElsewhere;
        private readonly int[,] tabCounts = new int[2, 4];
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Vector2 scrollPosition;
        private bool sortByValue = true;
        private Tab tab = Tab.All;
        private readonly List<TabRecord> tabs = new List<TabRecord>();
        private readonly Dictionary<string, string> truncated = new Dictionary<string, string>();
        private readonly Dictionary<Settlement, string> travelTimes = new Dictionary<Settlement, string>();

        private readonly Stopwatch watch = new Stopwatch();
        private double gatherMs;
        private double buyersMs;
        private double drawMs;
        private int drawFrames;

        private enum Tab
        {
            All,
            Items,
            Captives,
            Animals,
        }

        private const float RowHeight = 30f;
        private const float TabsHeight = 32f;
        private const float ModesHeight = 32f;
        private const float HeaderHeight = 24f;
        private const float BottomAreaHeight = 78f;
        private const float ControlsHeight = 28f;
        private const float ColTotal = 90f;
        private const float ColAdjust = 240f;
        private const float ColPrice = 100f;
        private const float ColMarket = 100f;
        private const float ColStorage = 90f;
        private const float ColBuyer = 330f;
        private const float SearchWidth = 220f;
        private const float IconX = 30f;
        private const float LabelX = 60f;
        private static readonly Vector2 BottomButtonSize = new Vector2(160f, 40f);
        private static readonly Color DisabledButtonColor = new Color(1f, 1f, 1f, 0.35f);
        private static readonly int[] LookWithinPresets = { 10, 25, 50, 100, 200 };

        public override Vector2 InitialSize => new Vector2(Mathf.Min(UI.screenWidth, 980f), Mathf.Min(UI.screenHeight, 900f));

        private static int LookWithin => WhatCanISellMod.Settings.lookWithinTiles;

        public Dialog_ThingsYouCanSell(Settlement settlement)
        {
            this.settlement = settlement;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnClickedOutside = false;

            colonyMaps = Find.Maps.Where(m => m.IsPlayerHome).ToList();
            Map current = Find.CurrentMap;
            map = current != null && current.IsPlayerHome ? current : colonyMaps.FirstOrDefault();
            SetMap(map);
        }

        private void SetMap(Map newMap)
        {
            map = newMap;
            // Travel times are measured from the colony, so they don't carry over to another one.
            travelTimes.Clear();
            watch.Restart();
            negotiator = SellableStock.NegotiatorCandidates(map, settlement).FirstOrDefault();
            negotiatorLabel = null;
            entries = SellableStock.Gather(map, settlement, negotiator, elsewhereCandidates);
            gatherMs = watch.Elapsed.TotalMilliseconds;
            UpdateBuyers();
            scrollPosition = Vector2.zero;
        }

        private void UpdateBuyers()
        {
            watch.Restart();
            elsewhere = SellableStock.FindBuyers(map, settlement, elsewhereCandidates, LookWithin);
            SellableStock.UpdatePrices(elsewhere, settlement, negotiator);
            buyersMs = watch.Elapsed.TotalMilliseconds;
            CountTabs(0, entries);
            CountTabs(1, elsewhere);
            visibleDirty = true;
        }

        private void CountTabs(int mode, List<SellableEntry> list)
        {
            tabCounts[mode, (int)Tab.All] = list.Count;
            tabCounts[mode, (int)Tab.Items] = list.Count(e => e.kind == SellableKind.Item);
            tabCounts[mode, (int)Tab.Captives] = list.Count(e => e.kind == SellableKind.Captive);
            tabCounts[mode, (int)Tab.Animals] = list.Count(e => e.kind == SellableKind.Animal);
        }

        private void SetNegotiator(Pawn pawn)
        {
            negotiator = pawn;
            negotiatorLabel = null;
            SellableStock.UpdatePrices(entries, settlement, negotiator);
            SellableStock.UpdatePrices(elsewhere, settlement, negotiator);
            visibleDirty = true;
        }

        private void SetLookWithin(int tiles)
        {
            WhatCanISellMod.Settings.lookWithinTiles = Mathf.Clamp(tiles, 1, WhatCanISellSettings.MaxLookWithinTiles);
            WhatCanISellMod.Instance.WriteSettings();
            UpdateBuyers();
        }

        public override void DoWindowContents(Rect inRect)
        {
            long start = Stopwatch.GetTimestamp();
            DrawContents(inRect);
            drawMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            if (Event.current.type == EventType.Repaint)
            {
                drawFrames++;
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            if (Prefs.DevMode && drawFrames > 0)
            {
                Log.Message(WhatCanISellMod.LogPrefix + $"gather {gatherMs:0.00} ms, buyers within {LookWithin} tiles {buyersMs:0.00} ms, window {drawMs / drawFrames:0.000} ms per frame over {drawFrames} frames");
            }
        }

        private void DrawContents(Rect inRect)
        {
            float y = 0f;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(0f, y, inRect.width - 30f, 35f), "WCIS_Title".Translate(settlement.LabelCap));
            y += 36f;

            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            string subtitle = settlement.Faction.Name;
            string traderLabel = settlement.TraderKind.LabelCap;
            if (!traderLabel.NullOrEmpty())
            {
                subtitle += " · " + traderLabel;
            }
            Widgets.Label(new Rect(0f, y, inRect.width, 24f), subtitle);
            GUI.color = Color.white;
            y += 24f;

            if (!SellableStock.CanSendCaravanTo(settlement))
            {
                GUI.color = ColorLibrary.RedReadable;
                Widgets.Label(new Rect(0f, y, inRect.width, 24f), "WCIS_HostileBanner".Translate());
                GUI.color = Color.white;
                y += 24f;
            }

            RoyalTitleDef titleRequired = settlement.TraderKind.TitleRequiredToTrade;
            if (titleRequired != null)
            {
                GUI.color = negotiator == null ? ColorLibrary.RedReadable : Color.gray;
                Widgets.Label(new Rect(0f, y, inRect.width, 24f), "WCIS_RequiresTitle".Translate(titleRequired.GetLabelCapForBothGenders()));
                GUI.color = Color.white;
                y += 24f;
            }

            y += 6f;
            DrawControls(new Rect(0f, y, inRect.width, ControlsHeight));
            y += ControlsHeight + 10f;
            DrawModes(new Rect(0f, y, inRect.width, ModesHeight));
            y += ModesHeight + 8f;
            Text.Anchor = TextAnchor.UpperLeft;

            y += TabsHeight;
            Rect listRect = new Rect(0f, y, inRect.width, inRect.height - y - BottomAreaHeight);
            DrawTabs(listRect);
            if (showElsewhere)
            {
                DrawElsewhereList(listRect.ContractedBy(4f));
            }
            else
            {
                DrawList(listRect.ContractedBy(4f));
            }

            DrawBottom(new Rect(0f, inRect.height - BottomAreaHeight, inRect.width, BottomAreaHeight));
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawControls(Rect rect)
        {
            float x = rect.x;
            if (colonyMaps.Count > 1 && map != null)
            {
                Rect colonyRect = new Rect(x, rect.y, 230f, rect.height);
                if (Widgets.ButtonText(colonyRect, "WCIS_Colony".Translate(map.Parent.LabelCap)))
                {
                    var options = colonyMaps.Select(m => new FloatMenuOption(m.Parent.LabelCap, () => SetMap(m))).ToList();
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                x += colonyRect.width + 8f;
            }

            Rect negotiatorRect = new Rect(x, rect.y, 290f, rect.height);
            negotiatorLabel ??= negotiator != null
                ? "WCIS_Negotiator".Translate(NegotiatorLabel(negotiator)).ToString()
                : "WCIS_NoNegotiator".Translate().ToString();
            if (Widgets.ButtonText(negotiatorRect, negotiatorLabel))
            {
                List<Pawn> candidates = SellableStock.NegotiatorCandidates(map, settlement);
                if (candidates.Any())
                {
                    var options = candidates.Select(p => new FloatMenuOption(NegotiatorLabel(p), () => SetNegotiator(p))).ToList();
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }
            if (Mouse.IsOver(negotiatorRect))
            {
                TooltipHandler.TipRegion(negotiatorRect, negotiator != null
                    ? "WCIS_NegotiatorTip".Translate()
                    : SellableStock.NoNegotiatorReason(settlement));
            }
            x += negotiatorRect.width + 8f;

            Rect sortRect = new Rect(x, rect.y, 170f, rect.height);
            if (Widgets.ButtonText(sortRect, sortByValue ? "WCIS_SortByValue".Translate() : "WCIS_SortByName".Translate()))
            {
                sortByValue = !sortByValue;
                visibleDirty = true;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }

            // Inset from the window edge: a text field ending exactly on it loses its right border.
            Rect searchRect = new Rect(rect.xMax - SearchWidth - 4f, rect.y + (rect.height - QuickSearchWidget.WidgetHeight) / 2f, SearchWidth, QuickSearchWidget.WidgetHeight);
            search.OnGUI(searchRect, () =>
            {
                scrollPosition = Vector2.zero;
                visibleDirty = true;
            });
        }

        private static string NegotiatorLabel(Pawn pawn)
        {
            return pawn.LabelShortCap + " (" + pawn.GetStatValue(StatDefOf.TradePriceImprovement).ToStringPercent() + ")";
        }

        // Two lists, not two categories: what this settlement buys, and what it won't (with who will).
        private void DrawModes(Rect rect)
        {
            float width = Mathf.Min(320f, (rect.width - 8f) / 2f);
            DrawMode(new Rect(rect.x, rect.y, width, rect.height), false,
                "WCIS_ModeBuys".Translate(settlement.LabelCap, tabCounts[0, (int)Tab.All]), "WCIS_ModeBuysTip".Translate(settlement.LabelCap));
            DrawMode(new Rect(rect.x + width + 8f, rect.y, width, rect.height), true,
                "WCIS_ModeWontBuy".Translate(settlement.LabelCap, tabCounts[1, (int)Tab.All]), "WCIS_ModeWontBuyTip".Translate(settlement.LabelCap));
        }

        private void DrawMode(Rect rect, bool elsewhereMode, string label, string tip)
        {
            bool selected = showElsewhere == elsewhereMode;
            if (Widgets.ButtonText(rect, label.Truncate(rect.width - 16f, truncated)) && !selected)
            {
                showElsewhere = elsewhereMode;
                scrollPosition = Vector2.zero;
                visibleDirty = true;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }
            if (selected)
            {
                Widgets.DrawHighlightSelected(rect);
                Widgets.DrawBox(rect);
            }
            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, tip);
            }
        }

        private void DrawTabs(Rect listRect)
        {
            Widgets.DrawMenuSection(listRect);
            tabs.Clear();
            AddTab(Tab.All, "WCIS_TabAll");
            AddTab(Tab.Items, "WCIS_TabItems");
            AddTab(Tab.Captives, "WCIS_TabCaptives");
            AddTab(Tab.Animals, "WCIS_TabAnimals");
            TabDrawer.DrawTabs(listRect, tabs);
        }

        private void AddTab(Tab which, string key)
        {
            tabs.Add(new TabRecord(key.Translate() + " (" + tabCounts[showElsewhere ? 1 : 0, (int)which] + ")", () =>
            {
                tab = which;
                scrollPosition = Vector2.zero;
                visibleDirty = true;
            }, tab == which));
        }

        private bool InCurrentTab(SellableEntry entry)
        {
            switch (tab)
            {
                case Tab.Items: return entry.kind == SellableKind.Item;
                case Tab.Captives: return entry.kind == SellableKind.Captive;
                case Tab.Animals: return entry.kind == SellableKind.Animal;
                default: return true;
            }
        }

        // Rebuilt only when the tab, search, sort, prices or stock change; nothing else moves while the game is paused.
        private void RebuildVisibleIfDirty()
        {
            if (!visibleDirty)
            {
                return;
            }
            visibleDirty = false;
            visible.Clear();
            foreach (SellableEntry entry in showElsewhere ? elsewhere : entries)
            {
                if (!entry.transferable.HasAnyThing || !InCurrentTab(entry))
                {
                    continue;
                }
                if (search.filter.Active && !search.filter.Matches(entry.Label) && !search.filter.Matches(entry.transferable.ThingDef.defName))
                {
                    continue;
                }
                visible.Add(entry);
            }
            if (sortByValue)
            {
                visible.SortByDescending(e => e.TotalValueAvailable);
            }
            else
            {
                visible.SortBy(e => e.Label);
            }
            search.noResultsMatched = search.filter.Active && visible.Count == 0;
        }

        private void DrawList(Rect rect)
        {
            if (map == null)
            {
                Widgets.Label(rect, "WCIS_NoColony".Translate());
                return;
            }

            float width = rect.width - 16f;
            Rect header = new Rect(rect.x, rect.y, width, HeaderHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x + LabelX, header.y, ColumnStart(width, 4) - LabelX, HeaderHeight), "WCIS_ColItem".Translate());
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(ColumnRect(header, width, 4), "WCIS_ColInStorage".Translate());
            Widgets.Label(ColumnRect(header, width, 3), "WCIS_ColMarketValue".Translate());
            Rect priceHeader = ColumnRect(header, width, 2);
            Widgets.Label(priceHeader, "WCIS_ColSellPrice".Translate());
            TooltipHandler.TipRegion(priceHeader, "WCIS_SellPriceTip".Translate());
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(ColumnRect(header, width, 1), "WCIS_ColSell".Translate());
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(ColumnRect(header, width, 0), "WCIS_ColTotal".Translate());
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(rect.x, header.yMax + 1f, width);
            Text.Anchor = TextAnchor.UpperLeft;

            Rect outRect = new Rect(rect.x, header.yMax + 4f, rect.width, rect.height - HeaderHeight - 4f);
            RebuildVisibleIfDirty();
            if (visible.Count == 0)
            {
                if (!search.filter.Active)
                {
                    DrawEmpty(outRect, EmptyText("WCIS_Nothing".Translate()));
                }
                return;
            }

            Rect viewRect = new Rect(0f, 0f, width, visible.Count * RowHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float minY = scrollPosition.y - RowHeight;
            float maxY = scrollPosition.y + outRect.height;
            for (int i = 0; i < visible.Count; i++)
            {
                float rowY = i * RowHeight;
                if (rowY > minY && rowY < maxY)
                {
                    DrawRow(new Rect(0f, rowY, width, RowHeight), visible[i], i);
                }
            }
            Widgets.EndScrollView();
        }

        // A tab can be empty while the list itself isn't, e.g. Animals in the "won't buy" list.
        private string EmptyText(string whenListEmpty)
        {
            return tabCounts[showElsewhere ? 1 : 0, (int)Tab.All] > 0 ? "WCIS_NothingInTab".Translate().ToString() : whenListEmpty;
        }

        private static void DrawEmpty(Rect outRect, string text)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(outRect.x, outRect.y, outRect.width, 60f), text);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Shared by both lists: highlight, info card, icon, pawn icons and label up to labelEnd.
        private void DrawRowStart(Rect row, SellableEntry entry, int index, float labelEnd, Settlement trader)
        {
            TransferableOneWay trad = entry.transferable;
            Thing thing = trad.AnyThing;

            if (entry.Selected > 0)
            {
                Widgets.DrawHighlightSelected(row);
            }
            else if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(row);
            }
            Widgets.DrawHighlightIfMouseover(row);

            Widgets.InfoCardButton(row.x + 2f, row.y + (row.height - 24f) / 2f, thing);
            Widgets.ThingIcon(new Rect(IconX, row.y + (row.height - 27f) / 2f, 27f, 27f), thing);

            // These vanilla helpers draw right-to-left from curX in row-local coordinates.
            float curX = labelEnd - 6f;
            if (entry.IsPawn)
            {
                Widgets.BeginGroup(row);
                Rect local = row.AtZero();
                TransferableUIUtility.DoExtraIcons(trad, local, ref curX);
                TransferableUIUtility.DrawCaptiveTradeInfo(trad, trader, local, ref curX);
                Widgets.EndGroup();
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Rect labelRect = new Rect(LabelX, row.y, curX - LabelX - 4f, row.height);
            Widgets.Label(labelRect, entry.Label.Truncate(labelRect.width, truncated));
            if (Mouse.IsOver(labelRect))
            {
                TooltipHandler.TipRegion(labelRect, entry.Tooltip);
            }
        }

        private void DrawRow(Rect row, SellableEntry entry, int index)
        {
            TransferableOneWay trad = entry.transferable;
            float width = row.width;
            DrawRowStart(row, entry, index, ColumnStart(width, 4), settlement);

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(ColumnRect(row, width, 4), entry.Available.ToStringCached());
            Widgets.Label(ColumnRect(row, width, 3), entry.marketValue.ToStringMoney());
            Rect priceRect = ColumnRect(row, width, 2);
            Widgets.Label(priceRect, SellableStock.FormatPrice(entry.sellPrice, settlement));
            if (Mouse.IsOver(priceRect))
            {
                TooltipHandler.TipRegion(priceRect, "WCIS_SellPriceTip".Translate());
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Rect adjustRect = ColumnRect(row, width, 1);
            if (entry.IsPawn && entry.Available == 1)
            {
                // Vanilla draws single pawns as an off-centre checkbox; centre it in the column instead.
                bool on = entry.Selected > 0;
                bool was = on;
                Widgets.Checkbox(new Vector2(adjustRect.center.x - 12f, adjustRect.center.y - 12f), ref on, 24f, paintable: true);
                if (on != was)
                {
                    trad.AdjustTo(on ? 1 : 0);
                }
            }
            else
            {
                TransferableUIUtility.DoCountAdjustInterface(adjustRect, trad, index, 0, entry.Available);
            }

            Text.Anchor = TextAnchor.MiddleRight;
            if (entry.Selected > 0)
            {
                Widgets.Label(ColumnRect(row, width, 0), SellableStock.FormatPrice(entry.sellPrice * entry.Selected, settlement));
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(ColumnRect(row, width, 0), "-");
                GUI.color = Color.white;
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Columns counted from the right: 0 total, 1 count adjuster, 2 sell price, 3 market value, 4 in storage.
        private static readonly float[] ColumnWidths = { ColTotal, ColAdjust, ColPrice, ColMarket, ColStorage };

        // Sell elsewhere columns from the right: 0 nearest buyer, 1 sell price there, 2 available.
        private static readonly float[] ElsewhereWidths = { ColBuyer, ColPrice, ColStorage };

        private static float ColumnStart(float width, int column, float[] widths = null)
        {
            widths ??= ColumnWidths;
            float x = width;
            for (int i = 0; i <= column; i++)
            {
                x -= widths[i];
            }
            return x;
        }

        private static Rect ColumnRect(Rect row, float width, int column, float[] widths = null)
        {
            widths ??= ColumnWidths;
            float start = row.x + ColumnStart(width, column, widths);
            float colWidth = widths[column];
            return widths == ColumnWidths && column == 1
                ? new Rect(start, row.y, colWidth, row.height)
                : new Rect(start, row.y, colWidth - 8f, row.height);
        }

        private void DrawElsewhereList(Rect rect)
        {
            if (map == null)
            {
                Widgets.Label(rect, "WCIS_NoColony".Translate());
                return;
            }

            float width = rect.width - 16f;
            Rect rangeRect = new Rect(rect.x + width - 200f, rect.y, 200f, ControlsHeight);
            if (Widgets.ButtonText(rangeRect, "WCIS_LookWithin".Translate(LookWithin)))
            {
                var options = LookWithinPresets
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
            Widgets.Label(new Rect(rect.x + LabelX, rect.y, rangeRect.x - rect.x - LabelX - 8f, ControlsHeight), "WCIS_ElsewhereHint".Translate(settlement.LabelCap));

            Rect header = new Rect(rect.x, rangeRect.yMax + 6f, width, HeaderHeight);
            Widgets.Label(new Rect(rect.x + LabelX, header.y, ColumnStart(width, 2, ElsewhereWidths) - LabelX, HeaderHeight), "WCIS_ColItem".Translate());
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(ColumnRect(header, width, 2, ElsewhereWidths), "WCIS_ColInStorage".Translate());
            Widgets.Label(ColumnRect(header, width, 1, ElsewhereWidths), "WCIS_ColSellPrice".Translate());
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect buyerHeader = ColumnRect(header, width, 0, ElsewhereWidths);
            Widgets.Label(new Rect(buyerHeader.x + 12f, buyerHeader.y, buyerHeader.width - 12f, buyerHeader.height), "WCIS_ColNearestBuyer".Translate());
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(rect.x, header.yMax + 1f, width);
            Text.Anchor = TextAnchor.UpperLeft;

            float top = header.yMax + 4f;
            Rect outRect = new Rect(rect.x, top, rect.width, rect.yMax - top);
            RebuildVisibleIfDirty();
            if (visible.Count == 0)
            {
                if (!search.filter.Active)
                {
                    DrawEmpty(outRect, EmptyText("WCIS_NothingElsewhere".Translate(LookWithin)));
                }
                return;
            }

            Rect viewRect = new Rect(0f, 0f, width, visible.Count * RowHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float minY = scrollPosition.y - RowHeight;
            float maxY = scrollPosition.y + outRect.height;
            for (int i = 0; i < visible.Count; i++)
            {
                float rowY = i * RowHeight;
                if (rowY > minY && rowY < maxY)
                {
                    DrawElsewhereRow(new Rect(0f, rowY, width, RowHeight), visible[i], i);
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawElsewhereRow(Rect row, SellableEntry entry, int index)
        {
            float width = row.width;
            Buyer nearest = entry.buyers[0];
            DrawRowStart(row, entry, index, ColumnStart(width, 2, ElsewhereWidths), nearest.settlement);

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(ColumnRect(row, width, 2, ElsewhereWidths), entry.Available.ToStringCached());
            Widgets.Label(ColumnRect(row, width, 1, ElsewhereWidths), SellableStock.FormatPrice(entry.sellPrice, nearest.settlement));

            Rect buyerRect = ColumnRect(row, width, 0, ElsewhereWidths);
            buyerRect.xMin += 12f;
            Text.Anchor = TextAnchor.MiddleLeft;
            bool over = Mouse.IsOver(buyerRect);
            GUI.color = over ? GenUI.MouseoverColor : Color.white;
            string name = nearest.settlement.LabelCap;
            float nameWidth = Mathf.Min(Text.CalcSize(name).x, buyerRect.width * 0.55f);
            Widgets.Label(new Rect(buyerRect.x, buyerRect.y, nameWidth, buyerRect.height), name.Truncate(nameWidth, truncated));
            GUI.color = Color.gray;
            Widgets.Label(new Rect(buyerRect.x + nameWidth, buyerRect.y, buyerRect.width - nameWidth, buyerRect.height),
                " · " + "WCIS_Tiles".Translate(nearest.tiles) + " · " + TravelTime(nearest.settlement));
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
                CameraJumper.TryJumpAndSelect(nearest.settlement);
            }
        }

        // Pathfinding is the costly part, so it runs once per settlement and only for buyers actually on screen.
        private string TravelTime(Settlement buyer)
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
            var sb = new StringBuilder();
            sb.Append("WCIS_BuyersTip".Translate(LookWithin));
            foreach (Buyer buyer in entry.buyers.Take(8))
            {
                sb.Append("\n  ").Append(buyer.settlement.LabelCap).Append(" (").Append(buyer.settlement.Faction.Name).Append(") · ")
                    .Append("WCIS_Tiles".Translate(buyer.tiles));
            }
            if (entry.buyers.Count > 8)
            {
                sb.Append("\n  ...");
            }
            sb.Append("\n\n").Append("WCIS_OpenBuyer".Translate(entry.buyers[0].settlement.LabelCap));
            return sb.ToString();
        }

        private void DrawBottom(Rect rect)
        {
            int itemCount = 0;
            int pawnCount = 0;
            float mass = 0f;
            float value = 0f;
            foreach (SellableEntry entry in entries)
            {
                int selected = entry.Selected;
                if (selected <= 0)
                {
                    continue;
                }
                if (entry.IsPawn)
                {
                    pawnCount += selected;
                }
                else
                {
                    itemCount += selected;
                }
                mass += entry.massEach * selected;
                value += entry.sellPrice * selected;
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 26f),
                "WCIS_Summary".Translate(itemCount.ToStringCached(), pawnCount.ToStringCached(), mass.ToString("0.#"), SellableStock.FormatPrice(value, settlement)));
            Text.Anchor = TextAnchor.UpperLeft;

            float buttonY = rect.yMax - BottomButtonSize.y;
            if (Widgets.ButtonText(new Rect(rect.x, buttonY, BottomButtonSize.x, BottomButtonSize.y), "CloseButton".Translate()))
            {
                Close();
            }

            float midX = rect.center.x;
            if (!showElsewhere
                && Widgets.ButtonText(new Rect(midX - BottomButtonSize.x - 5f, buttonY, BottomButtonSize.x, BottomButtonSize.y), "WCIS_SelectAll".Translate()))
            {
                RebuildVisibleIfDirty();
                foreach (SellableEntry entry in visible)
                {
                    entry.transferable.AdjustTo(entry.Available);
                }
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            if (Widgets.ButtonText(new Rect(midX + 5f, buttonY, BottomButtonSize.x, BottomButtonSize.y), "WCIS_Clear".Translate()))
            {
                foreach (SellableEntry entry in entries)
                {
                    entry.transferable.AdjustTo(0);
                }
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }

            string cantSendReason = CantSendReason();
            Rect formRect = new Rect(rect.xMax - BottomButtonSize.x, buttonY, BottomButtonSize.x, BottomButtonSize.y);
            // Vanilla's inactive button barely changes, so dim the whole thing.
            if (cantSendReason != null)
            {
                GUI.color = DisabledButtonColor;
            }
            if (Widgets.ButtonText(formRect, "WCIS_FormCaravan".Translate(), active: cantSendReason == null) && cantSendReason == null)
            {
                FormCaravan();
            }
            GUI.color = Color.white;
            if (cantSendReason != null)
            {
                TooltipHandler.TipRegion(formRect, cantSendReason);
            }
        }

        private string CantSendReason()
        {
            if (map == null)
            {
                return "WCIS_NoColony".Translate();
            }
            // A caravan sent to a hostile settlement takes the first available arrival
            // action there, which is "attack".
            if (!SellableStock.CanSendCaravanTo(settlement))
            {
                return "WCIS_HostileNoCaravan".Translate();
            }
            if (negotiator == null)
            {
                return SellableStock.NoNegotiatorReason(settlement);
            }
            return null;
        }

        private void FormCaravan()
        {
            var selections = entries
                .Where(e => e.Selected > 0)
                .Select(e => (e.transferable.things.ToList(), e.Selected))
                .ToList();
            selections.Add((new List<Thing> { negotiator }, 1));
            Close(doCloseSound: false);
            FormCaravanPrefill.Open(map, settlement, selections);
        }
    }
}
