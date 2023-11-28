﻿/*
 * Copyright © 2023 - 2023 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 */

using EliteDangerousCore;
using ExtendedControls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EDDiscovery.UserControls.Helpers
{
    public partial class SpanshStationsUserControl : UserControl
    {
        private EliteDangerousCore.DB.IUserDatabaseSettingsSaver saver;
        private bool explicity_set_system = false;
        private ISystem defaultsystem;
        private List<StationInfo> stationdata;

        bool showcommoditiesstationtobuyprice = false;      // on columns, show buy price, else sell price (if has stock)

        private const string dbLS = "MaxLs";

        enum FilterSettings { Type, Commodities, Outfitting, Shipyard, Economy, Services};
        ExtButtonWithCheckedIconListBoxGroup[] filters;

        private const string dbWordWrap = "WordWrap";

        public SpanshStationsUserControl()
        {
            InitializeComponent();
        }

        public void Init(EliteDangerousCore.DB.IUserDatabaseSettingsSaver saver)
        {
            this.saver = saver;
            colOutfitting.DefaultCellStyle.Alignment =
              colShipyard.DefaultCellStyle.Alignment =
              colHasMarket.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            extTextBoxAutoCompleteSystem.SetAutoCompletor(SystemCache.ReturnSystemAutoCompleteList, true);
            extTextBoxAutoCompleteSystem.ReturnPressed += (s) => {
                System.Diagnostics.Debug.WriteLine($"SpanshStation return pressed {extTextBoxAutoCompleteSystem.Text}");
                extTextBoxAutoCompleteSystem.CancelAutoComplete();
                explicity_set_system = extTextBoxAutoCompleteSystem.Text.HasChars(); 
                DrawSystem(); return true;
            };
            extTextBoxAutoCompleteSystem.AutoCompleteTimeout = 1000;

            filters = new ExtButtonWithCheckedIconListBoxGroup[] { extButtonType, extButtonCommodities, extButtonOutfitting, extButtonShipyard, extButtonEconomy, extButtonServices };

            var porttype = StationDefinitions.StarportTypes.Values.Distinct().Select(x => new CheckedIconListBoxFormGroup.StandardOption(x,x));
            extButtonType.InitAllNoneAllBack(porttype,
                GetFilter(FilterSettings.Type),
                (newsetting,ch) => { SetFilter(FilterSettings.Type, newsetting, ch); });


            var comitems = MaterialCommodityMicroResourceType.GetCommodities(MaterialCommodityMicroResourceType.SortMethod.AlphabeticalRaresLast)
                            .Select(x => new CheckedIconListBoxFormGroup.StandardOption(x.FDName, x.Name));

            extButtonCommodities.InitAllNoneAllBack(comitems,
                GetFilter(FilterSettings.Commodities),
                (newsetting,ch) => { SetFilter(FilterSettings.Commodities, newsetting, ch); });

            var moditems = ItemData.GetModules().Select(x => x.ModTypeString).Distinct().      // only return buyable modules
                            Select(x2 => new CheckedIconListBoxFormGroup.StandardOption(x2, x2));

            extButtonOutfitting.InitAllNoneAllBack(moditems,
                GetFilter(FilterSettings.Outfitting),
                (newsetting, ch) => { SetFilter(FilterSettings.Outfitting, newsetting, ch); });

            var ships = ItemData.GetSpaceships().Select(x =>
                new CheckedIconListBoxFormGroup.StandardOption(((ItemData.ShipInfoString)x[ItemData.ShipPropID.FDID]).Value,
                            ((ItemData.ShipInfoString)x[ItemData.ShipPropID.Name]).Value));

            extButtonShipyard.InitAllNoneAllBack(ships,
                GetFilter(FilterSettings.Shipyard),
                (newsetting, ch) => { SetFilter(FilterSettings.Shipyard, newsetting, ch); });

            // could use Identifers to localise later
            var economy = EconomyDefinitions.Types.Select(x => new CheckedIconListBoxFormGroup.StandardOption(x.Key, x.Value));

            extButtonEconomy.SettingsSplittingChar = '\u2345';     // because ; is used in identifiers
            extButtonEconomy.InitAllNoneAllBack(economy,
                GetFilter(FilterSettings.Economy),
                (newsetting, ch) => { SetFilter(FilterSettings.Economy, newsetting, ch); });

            var services = StationDefinitions.ServiceTypes.Select(x => x.Value).Distinct().Select(x => new CheckedIconListBoxFormGroup.StandardOption(x, x));
            extButtonServices.InitAllNoneAllBack(services,
                GetFilter(FilterSettings.Services),
                (newsetting, ch) => { SetFilter(FilterSettings.Services, newsetting, ch); });

            ///  tbd   saver.DGVLoadColumnLayout(dataGridView);
            valueBoxMaxLs.ValueNoChange = saver.GetSetting(dbLS, 1000000.0);

            extCheckBoxWordWrap.Checked = saver.GetSetting(dbWordWrap, false);
            UpdateWordWrap();
            extCheckBoxWordWrap.Click += extCheckBoxWordWrap_Click;

            ColPrice1.Visible = ColPrice2.Visible = ColPrice3.Visible = 
            colDistanceRef.Visible = colSystem.Visible = false;
        }

        public void Close()
        {
            saver.DGVSaveColumnLayout(dataGridView);
            saver.PutSetting(dbLS, valueBoxMaxLs.Value);

        }

        // update the default system, and if we have not got an explicity set system, update data on screen

        public void UpdateDefaultSystem(ISystem sys)
        {
            defaultsystem = sys;
            if (explicity_set_system == false)
                DrawSystem();
        }
        
        // explicity display system
        public void DisplaySystemStations(ISystem sys)
        {
            defaultsystem = sys;
            explicity_set_system = false;
            DrawSystem();
        }

        // get data for system, either defaultsystem (explicity set system = false) or text system
        private async void DrawSystem()
        {
            // if explicity set, must have chars
            ISystem sys = explicity_set_system ? new SystemClass(extTextBoxAutoCompleteSystem.Text) : defaultsystem;

            System.Diagnostics.Debug.WriteLine($"Spansh station kick with min {valueBoxMaxLs.Value} at {sys.Name}");

            extTextBoxAutoCompleteSystem.TextNoChange = sys.Name;       // replace with system actually displayed, no text change
            extTextBoxAutoCompleteSystem.ClearOnFirstChar = true;       // reset so we can clear on next text input

            EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();
            stationdata = await sp.GetStationsByDumpAsync(sys);

            colDistanceRef.Visible = colSystem.Visible = false;
            extButtonTravelSystem.Enabled = explicity_set_system;

            Draw(true);
        }

        private bool DrawSearch(List<StationInfo> si, bool clearotherfilters, FilterSettings alwaysclear)
        {
            if (si == null)
            {
                MessageBoxTheme.Show(this.FindForm(), $"No stations returned", "Warning".TxID(EDTx.Warning), MessageBoxButtons.OK);
                return false;
            }
            else
            {
                foreach (FilterSettings e in Enum.GetValues(typeof(FilterSettings)))
                {
                    if (clearotherfilters || e == alwaysclear)     // go thru filters and reset the filter
                    {
                        SetFilter(e, CheckedIconListBoxFormGroup.Disabled, false);  // update the DB
                        filters[(int)e].Set(CheckedIconListBoxFormGroup.Disabled);  // we need to update the button with the same setting
                    }
                }

                stationdata = si.ToList();
                explicity_set_system = true;

                colDistanceRef.Visible = colSystem.Visible = true;
                extButtonTravelSystem.Enabled = true;

                if (!extTextBoxAutoCompleteSystem.Text.Contains("("))           // name gets postfix added
                    extTextBoxAutoCompleteSystem.TextNoChange += " (Search)";
                extTextBoxAutoCompleteSystem.ClearOnFirstChar = true;
                Draw(true);

                return true;
            }
        }


        private void Draw(bool removesort)
        {
            DataGridViewColumn sortcolprev = (dataGridView.SortedColumn?.Visible??false) ? dataGridView.SortedColumn : colSystem.Visible ? colSystem : colBodyName;
            SortOrder sortorderprev = dataGridView.SortedColumn != null ? dataGridView.SortOrder : SortOrder.Ascending;

            ColPrice1.Visible = ColPrice1.Tag != null;
            ColPrice2.Visible = ColPrice2.Tag != null;
            ColPrice3.Visible = ColPrice3.Tag != null;

            dataViewScrollerPanel.Suspend();
            dataGridView.Rows.Clear();

            if (stationdata != null)
            {
                foreach (var station in stationdata)
                {
                    string stationtype = station.StationType ?? "Unknown";

                    bool filterin = station.DistanceToArrival <= valueBoxMaxLs.Value;

                    if (!extButtonType.IsDisabled)
                        filterin &= extButtonType.Get().HasChars() && extButtonType.Get().SplitNoEmptyStartFinish(extButtonType.SettingsSplittingChar).Contains(stationtype, StringComparison.InvariantCultureIgnoreCase) >= 0;

                    if (!extButtonCommodities.IsDisabled)
                        filterin &= extButtonCommodities.Get().HasChars() && station.HasAnyItemToBuy(extButtonCommodities.Get().SplitNoEmptyStartFinish(extButtonCommodities.SettingsSplittingChar));

                    if (!extButtonOutfitting.IsDisabled)
                        filterin &= extButtonOutfitting.Get().HasChars() && station.HasAnyModuleTypes(extButtonOutfitting.Get().SplitNoEmptyStartFinish(extButtonOutfitting.SettingsSplittingChar));

                    if (!extButtonShipyard.IsDisabled)
                        filterin &= extButtonShipyard.Get().HasChars() && station.HasAnyShipTypes(extButtonShipyard.Get().SplitNoEmptyStartFinish(extButtonShipyard.SettingsSplittingChar));

                    if (!extButtonEconomy.IsDisabled)
                        filterin &= extButtonEconomy.Get().HasChars() && station.HasAnyEconomyTypes(extButtonEconomy.Get().SplitNoEmptyStartFinish(extButtonEconomy.SettingsSplittingChar));

                    if (!extButtonServices.IsDisabled)
                        filterin &= extButtonServices.Get().HasChars() && station.HasAnyServicesTypes(extButtonServices.Get().SplitNoEmptyStartFinish(extButtonServices.SettingsSplittingChar));

                    if (filterin)
                    {
                        string ss = station.StationServices != null ? string.Join(", ", station.StationServices) : "";
                        object[] cells = new object[]
                        {
                            station.System.Name,
                            station.DistanceRefSystem.ToString("N1"),
                            station.BodyName?.ReplaceIfStartsWith(station.System.Name) ?? "",
                            station.StationName,
                            station.DistanceToArrival > 0 ? station.DistanceToArrival.ToString("N1") : "",
                            stationtype,
                            station.Latitude.HasValue ? station.Latitude.Value.ToString("N4") : "",
                            station.Longitude.HasValue ? station.Longitude.Value.ToString("N4") : "",
                            station.MarketStateString,
                            ColPrice1.Tag != null ? station.GetItemPriceString((string)ColPrice1.Tag,showcommoditiesstationtobuyprice) ?? "" : "",
                            ColPrice2.Tag != null ? station.GetItemPriceString((string)ColPrice2.Tag,showcommoditiesstationtobuyprice) ?? "" : "",
                            ColPrice3.Tag != null ? station.GetItemPriceString((string)ColPrice3.Tag,showcommoditiesstationtobuyprice) ?? "" : "",
                            station.OutfittingStateString,
                            station.ShipyardStateString,
                            station.Allegiance ?? "",
                            station.Faction ?? "",
                            station.Economy_Localised ?? "",
                            station.Government_Localised ?? "",
                            ss,
                            station.LandingPads?.Small.ToString() ?? "",
                            station.LandingPads?.Medium.ToString() ?? "",
                            station.LandingPads?.Large.ToString() ?? "",
                        };

                        var rw = dataGridView.RowTemplate.Clone() as DataGridViewRow;
                        rw.CreateCells(dataGridView, cells);
                        rw.Tag = station;
                        dataGridView.Rows.Add(rw);
                        if (ss.HasChars())
                            rw.Cells[colServices.Index].ToolTipText = ss.Replace(", ", Environment.NewLine);
                        if ( station.EconomyList!=null)
                            rw.Cells[colEconomy.Index].ToolTipText = string.Join(Environment.NewLine, station.EconomyList.Select(x=>$"{x.Name_Localised} : {(x.Proportion*100.0):N1}%"));
                        if (ColPrice1.Tag != null)
                            rw.Cells[ColPrice1.Index].ToolTipText = station.GetItemString((string)ColPrice1.Tag);
                        if (ColPrice2.Tag != null)
                            rw.Cells[ColPrice2.Index].ToolTipText = station.GetItemString((string)ColPrice2.Tag);
                        if (ColPrice3.Tag != null)
                            rw.Cells[ColPrice3.Index].ToolTipText = station.GetItemString((string)ColPrice3.Tag);
                    }
                }
            }

            if (!removesort)
            {
                dataGridView.Sort(sortcolprev, (sortorderprev == SortOrder.Descending) ? ListSortDirection.Descending : ListSortDirection.Ascending);
                dataGridView.Columns[sortcolprev.Index].HeaderCell.SortGlyphDirection = sortorderprev;
            }
            else
            {
                foreach (DataGridViewColumn c in dataGridView.Columns)
                    c.HeaderCell.SortGlyphDirection = SortOrder.None; 
            }

            dataViewScrollerPanel.Resume();

        }

        #region UI

        private void dataGridView_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            if (e.Column == colBodyName || e.Column == colSystem)
                e.SortDataGridViewColumnAlphaInt();
            else if (e.Column == colDistance || e.Column == colDistanceRef || e.Column == colLattitude || e.Column == colLongitude ||
                     e.Column == ColPrice1 || e.Column == ColPrice2 || e.Column == ColPrice3)
                e.SortDataGridViewColumnNumeric();
            else if ( e.Column == colHasMarket || e.Column == colOutfitting || e.Column == colShipyard )
                e.SortDataGridViewColumnNumeric("\u2713 ");
        }

        private void valueBoxMaxLs_ValueChanged(object sender, EventArgs e)
        {
            Draw(false);
        }
        private void extButtonTravelSystem_Click(object sender, EventArgs e)
        {
            if (explicity_set_system)
            {
                explicity_set_system = false;
                DrawSystem();
            }
        }

        #endregion

        #region Right click and UIs

        private void extCheckBoxWordWrap_Click(object sender, EventArgs e)
        {
            saver.PutSetting(dbWordWrap, extCheckBoxWordWrap.Checked);
            UpdateWordWrap();
        }


        private void dataGridView_MouseDown(object sender, MouseEventArgs e)
        {
            EliteDangerousCore.StationInfo si = dataGridView.RightClickRowValid ? dataGridView.Rows[dataGridView.RightClickRow].Tag as EliteDangerousCore.StationInfo : null;
            viewMarketToolStripMenuItem.Enabled = si?.HasMarket ?? false;
            viewOutfittingToolStripMenuItem.Enabled = si?.HasOutfitting ?? false;
            viewShipyardToolStripMenuItem.Enabled = si?.HasShipyard ?? false;
            viewOnSpanshToolStripMenuItem.Enabled = si?.MarketID.HasValue ?? false;
        }

        private void viewOnSpanshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StationInfo si = dataGridView.Rows[dataGridView.RightClickRow].Tag as EliteDangerousCore.StationInfo;
            EliteDangerousCore.Spansh.SpanshClass.LaunchBrowserForStationByMarketID(si.MarketID.Value);
        }

        private void viewMarketToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StationInfo si = dataGridView.Rows[dataGridView.RightClickRow].Tag as EliteDangerousCore.StationInfo;
            si.ViewMarket(FindForm(), saver);
        }

  
        private void viewOutfittingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StationInfo si = dataGridView.Rows[dataGridView.RightClickRow].Tag as EliteDangerousCore.StationInfo;
            si.ViewOutfitting(FindForm(), saver);
        }

  
        private void viewShipyardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StationInfo si = dataGridView.Rows[dataGridView.RightClickRow].Tag as StationInfo;
            si.ViewShipyard(FindForm(), saver);
        }

        private void dataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if ( e.RowIndex>=0)
            {
                EliteDangerousCore.StationInfo si = dataGridView.Rows[e.RowIndex].Tag as EliteDangerousCore.StationInfo;
                if (e.ColumnIndex == colOutfitting.Index && si.HasOutfitting)
                    si.ViewOutfitting(FindForm(), saver);
                else if (e.ColumnIndex == colShipyard.Index && si.HasShipyard)
                    si.ViewShipyard(FindForm(), saver);
                else if (e.ColumnIndex == colHasMarket.Index && si.HasMarket)
                    si.ViewMarket(FindForm(), saver);
            }
        }

   
        #endregion

        #region Searches

        Size numberboxsize = new Size(60, 24);
        Size labelsize = new Size(120, 24);
        const int maxresults = 200;

        int servicessearchdistance = 40;
        HashSet<string> servicestate = new HashSet<string>();
        bool servicesclearfilters = true;

        private void extButtonSearchServiceTypes_Click(object sender, EventArgs e)
        {
            string systemname = extTextBoxAutoCompleteSystem.Text.Substring(0, extTextBoxAutoCompleteSystem.Text.IndexOfOrLength("(")).Trim();

            SearchDialog((f)=> {
                var services = StationDefinitions.ServiceTypes.Select(x => x.Value).Distinct().ToArray();
                f.AddBools(services, services, servicestate, 4, 24, 200, 4, 200, "S_");
                AddSearchEntries(f, servicesclearfilters, servicessearchdistance);
            },
            (f) => {
                servicestate = f.GetCheckedListNames("S_").ToHashSet();
                servicessearchdistance = f.GetInt("radius").Value;
                servicesclearfilters = f.GetBool("CLRF").Value;
                var checkedlist = f.GetCheckedListEntries("S_").Select(x => x.Name.Substring(2)).ToArray();
                if (checkedlist.Length > 0)
                {
                    EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();
                    DrawSearch(sp.SearchServices(systemname, checkedlist, servicessearchdistance, maxresults), servicesclearfilters, FilterSettings.Services);
                }
            },
            $"Services from {systemname}");
        }

        int commoditiessearchdistance = 40;
        HashSet<string> commoditiesstate = new HashSet<string>();
        bool commoditiesclearfilters = true;

        private void extButtonSearchCommodities_Click(object sender, EventArgs e)
        {
            string systemname = extTextBoxAutoCompleteSystem.Text.Substring(0, extTextBoxAutoCompleteSystem.Text.IndexOfOrLength("(")).Trim();
            SearchDialog((f) => {
                var commodities = MaterialCommodityMicroResourceType.GetNormalCommodities(MaterialCommodityMicroResourceType.SortMethod.Alphabetical);
                var rarecommodities = MaterialCommodityMicroResourceType.GetRareCommodities(MaterialCommodityMicroResourceType.SortMethod.Alphabetical);
                int max = f.AddBools(commodities.Select(x => x.EnglishName + "\u2345" + x.FDName).ToArray(), commodities.Select(x => x.Name).ToArray(), commoditiesstate, 4, 24, 1000, 4, 200, "S_");

                // tag consists of english name <separ> fdname
                f.AddBools(rarecommodities.Select(x => x.EnglishName + "\u2345" + x.FDName).ToArray(), rarecommodities.Select(x => x.Name).ToArray(), commoditiesstate, max + 16, 24, 500, 4, 200, "S_");
                AddSearchEntries(f, commoditiesclearfilters, commoditiessearchdistance,700);

                f.Add(new ConfigurableForm.Entry("B_Buy", showcommoditiesstationtobuyprice, "Sell to Station", new Point(600, 4), new Size(160, 22), "Set = Search for stations with a station buy price and has demand, Clear = Search for stations with stock to sell") { Panel = ConfigurableForm.Entry.PanelType.Top });
            }, 
            (f)=>
            {
                commoditiesstate = f.GetCheckedListNames("S_").ToHashSet();
                commoditiessearchdistance = f.GetInt("radius").Value;
                commoditiesclearfilters = f.GetBool("CLRF").Value;
                showcommoditiesstationtobuyprice = f.GetBool("B_Buy").Value;

                var entries = f.GetCheckedListEntries("S_");

                if (entries.Length > 0)
                {
                    EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();
                
                    var search = entries.Select(x => new EliteDangerousCore.Spansh.SpanshClass.SearchCommoditity 
                    {   EnglishName = x.Name.Substring(2,x.Name.IndexOf('\u2345')-2),                                  // extract the english name
                        supply = !showcommoditiesstationtobuyprice ? new Tuple<int, int>(1, int.MaxValue) : null,      // if we want to buy, we need the supply to be >=1
                        demand = showcommoditiesstationtobuyprice ? new Tuple<int, int>(1, int.MaxValue) : null,       // if we want to sell, we need demand
                        buyprice = showcommoditiesstationtobuyprice ? new Tuple<int, int>(1, int.MaxValue) : null      // if we want to sell, we need a price
                    }).ToArray();

                    var res = sp.SearchCommodities(systemname, search, commoditiessearchdistance, maxresults);

                    if ( res != null )
                    {
                        ColPrice1.Tag = entries.Length >= 1 ? entries[0].Name.Substring(entries[0].Name.IndexOf('\u2345') + 1) : null;      // need to get the fdname for the columns
                        ColPrice1.HeaderText = entries.Length >= 1 ? entries[0].TextValue : "?";

                        ColPrice2.Tag = entries.Length >= 2 ? entries[0].Name.Substring(entries[1].Name.IndexOf('\u2345') + 1) : null;
                        ColPrice2.HeaderText = entries.Length >= 2 ? entries[1].TextValue : "?";

                        ColPrice3.Tag = entries.Length >= 3 ? entries[0].Name.Substring(entries[2].Name.IndexOf('\u2345') + 1) : null;
                        ColPrice3.HeaderText = entries.Length >= 3 ? entries[2].TextValue : "?";

                        showcommoditiesstate = commoditiesstate.Take(3).Select(x=>x.Substring(x.IndexOf('\u2345')+1)).ToHashSet();     // take the list, limit to 3, get the fdname only, fill in the c-state

                        DrawSearch(res, commoditiesclearfilters, FilterSettings.Commodities);
                        //Draw(false);
                    }
                }
            },
            $"Commodities from {systemname}");
        }

        HashSet<string> showcommoditiesstate = new HashSet<string>();
        private void extButtonEditCommodities_Click(object sender, EventArgs e)
        {
            SearchDialog((f) => {
                var commodities = MaterialCommodityMicroResourceType.GetNormalCommodities(MaterialCommodityMicroResourceType.SortMethod.Alphabetical);
                var rarecommodities = MaterialCommodityMicroResourceType.GetRareCommodities(MaterialCommodityMicroResourceType.SortMethod.Alphabetical);
                f.Add(new ConfigurableForm.Entry("B_Buy", showcommoditiesstationtobuyprice, "Sell to Station", new Point(600, 4), new Size(160, 22), 
                                    $"Set = Show price station buys the commodity at {Environment.NewLine} Clear = Show station sell price") { Panel = ConfigurableForm.Entry.PanelType.Top });

                int max = f.AddBools(commodities.Select(x => x.FDName).ToArray(), commodities.Select(x => x.Name).ToArray(), showcommoditiesstate, 4, 24, 1000, 4, 200, "S_");
                f.AddBools(rarecommodities.Select(x => x.FDName).ToArray(), rarecommodities.Select(x => x.Name).ToArray(), showcommoditiesstate, max + 16, 24, 500, 4, 200, "S_");

                f.Trigger += (d, ctrlname, text) => { f.RadioButton("S_", ctrlname, 3); };
            },
            (f) =>
            {
                showcommoditiesstate = f.GetCheckedListNames("S_").ToHashSet();
                showcommoditiesstationtobuyprice = f.GetBool("B_Buy").Value;

                var entries = f.GetCheckedListEntries("S_");        // 0 to 3.. 

                ColPrice1.Tag = entries.Length >= 1 ? entries[0].Name.Substring(2) : null;
                ColPrice1.HeaderText = entries.Length >= 1 ? entries[0].TextValue : "?";

                ColPrice2.Tag = entries.Length >= 2 ? entries[1].Name.Substring(2) : null;
                ColPrice2.HeaderText = entries.Length >= 2 ? entries[1].TextValue : "?";

                ColPrice3.Tag = entries.Length >= 3 ? entries[2].Name.Substring(2) : null;
                ColPrice3.HeaderText = entries.Length >= 3 ? entries[2].TextValue : "?";

                Draw(false);
            },
            $"Select Commodities to show");
        }


        int economysearchdistance = 40;
        HashSet<string> economystate = new HashSet<string>();
        bool economyclearfilters = true;
        private void extButtonSearchEconomy_Click(object sender, EventArgs e)
        {
            string systemname = extTextBoxAutoCompleteSystem.Text.Substring(0, extTextBoxAutoCompleteSystem.Text.IndexOfOrLength("(")).Trim();

            SearchDialog((f) => {
                var economy = EconomyDefinitions.Types.Values.Select(x => x).ToArray();
                f.AddBools(economy,economy, economystate, 4, 24, 200, 4, 120, "S_");
                AddSearchEntries(f, economyclearfilters, economysearchdistance);
            },
            (f) => {
                economystate = f.GetCheckedListNames("S_").ToHashSet();
                economysearchdistance = f.GetInt("radius").Value;
                economyclearfilters = f.GetBool("CLRF").Value;
                var checkedlist = f.GetCheckedListEntries("S_").Select(x => x.Name.Substring(2)).ToArray();
                if (checkedlist.Length > 0)
                {
                    EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();
                    DrawSearch(sp.SearchEconomy(systemname, checkedlist, economysearchdistance, maxresults), economyclearfilters, FilterSettings.Economy);
                }
            },
            $"Economies from {systemname}");
        }

        int shipssearchdistance = 40;
        HashSet<string> shipsstate = new HashSet<string>();
        bool shipsclearfilters = true;
        private void extButtonSearchShips_Click(object sender, EventArgs e)
        {
            string systemname = extTextBoxAutoCompleteSystem.Text.Substring(0, extTextBoxAutoCompleteSystem.Text.IndexOfOrLength("(")).Trim();

            SearchDialog((f) => {
                var ships = ItemData.GetSpaceships().Select(x => x.ContainsKey(ItemData.ShipPropID.EDCDName) ? ((ItemData.ShipInfoString)x[ItemData.ShipPropID.EDCDName]).Value : ((ItemData.ShipInfoString)x[ItemData.ShipPropID.Name]).Value).ToArray();
                f.AddBools(ships, ships, shipsstate, 4, 24, 200, 4, 200, "S_");
                AddSearchEntries(f, shipsclearfilters, shipssearchdistance);
            },
            (f) => {
                shipsstate = f.GetCheckedListNames("S_").ToHashSet();
                shipssearchdistance = f.GetInt("radius").Value;
                shipsclearfilters = f.GetBool("CLRF").Value;
                var checkedlist = f.GetCheckedListEntries("S_").Select(x => x.Name.Substring(2)).ToArray();
                if (checkedlist.Length > 0)
                {
                    EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();
                    DrawSearch(sp.SearchShips(systemname, checkedlist, shipssearchdistance, maxresults), shipsclearfilters, FilterSettings.Shipyard);
                }
            },
            $"Ships from {systemname}");
        }

        private void AddSearchEntries(ConfigurableForm f, bool clearfilters, int searchdistance, int posclearfilter = 600)
        {
            f.AddLabelAndEntry("Maximum Distance", new Point(400, 8), labelsize, new ConfigurableForm.Entry("radius", searchdistance, new Point(400 + labelsize.Width, 4), numberboxsize, "Maximum distance") { NumberBoxLongMinimum = 1, Panel = ConfigurableForm.Entry.PanelType.Top });
            f.Add(new ConfigurableForm.Entry("CLRF", clearfilters, "Clear other filters", new Point(posclearfilter, 4), new Size(160, 22), "Type filter is cleared, clear the others as well") { Panel = ConfigurableForm.Entry.PanelType.Top });
        }

        private void SearchDialog(Action<ConfigurableForm> addtoform, Action<ConfigurableForm> okpressed, string title)
        {
            ConfigurableForm f = new ConfigurableForm();
            f.TopPanelHeight = 32;
            f.Add(new ConfigurableForm.Entry("OK", typeof(ExtButton), "Show", new Point(300, 4), new Size(80, 24), null) { Panel = ConfigurableForm.Entry.PanelType.Top });
            addtoform(f);
            f.InstallStandardTriggers();
            f.Trigger += (name, text, obj) => { f.GetControl("OK").Enabled = f.IsAllValid(); };

            if (f.ShowDialogCentred(FindForm(), FindForm().Icon, title, closeicon: true) == DialogResult.OK)
            {
                okpressed(f);
            }
        }


        int outfittingsearchdistance = 40;
        bool[] outfittingmodtypes = new bool[256];
        bool[] outfittingclasses = new bool[8] { true, false, false, false, false, false, false, false };   // 0 =all, 0..6
        bool[] outfittingratings = new bool[8] { true, false, false, false, false, false, false, false };   // 0 = all, A..G
        bool outfittingclearfilters = true;

        private void extButtonSearchOutfitting_Click(object sender, EventArgs e)
        {
            string title = "Outfitting";

            var moditems = ItemData.GetModules().Select(x => x.ModTypeString).Distinct().ToArray();      // only return buyable modules

            ConfigurableForm f = new ConfigurableForm();
            f.TopPanelHeight = 32;
            f.Add(new ConfigurableForm.Entry("OK", typeof(ExtButton), "Search", new Point(300, 4), new Size(80, 24), null) { Panel = ConfigurableForm.Entry.PanelType.Top });
            f.AddLabelAndEntry("Maximum Distance", new Point(400, 8), labelsize, new ConfigurableForm.Entry("radius", outfittingsearchdistance, new Point(400 + labelsize.Width, 4), numberboxsize, "Maximum distance") { NumberBoxLongMinimum = 1, Panel = ConfigurableForm.Entry.PanelType.Top });
            f.Add(new ConfigurableForm.Entry("CLRF", outfittingclearfilters, "Clear other filters", new Point(600, 4), new Size(160, 22), title + " is cleared, clear the others as well") { Panel = ConfigurableForm.Entry.PanelType.Top });

            f.AddBools(moditems, moditems, outfittingmodtypes, 4, 24, 550, 4, 200, "M_");

            int vpos = 580;
            for (int cls = 0; cls <= 7; cls++)
            {
                f.Add(new ConfigurableForm.Entry("C_" + cls, outfittingclasses[cls], cls == 0 ? "All Classes" : "Class " + (cls-1), new Point(4+cls * 150, vpos), new Size(140, 22), null));
            }

            vpos += 30;
            for (int rating = 0; rating <= 7; rating++)
            {
                f.Add(new ConfigurableForm.Entry("R_" + rating, outfittingratings[rating], rating == 0 ? "All Ratings" : "Rating " + (char)('A'-1+rating), new Point(4+rating * 150, vpos), new Size(140, 22), null));
            }

            f.InstallStandardTriggers();
            f.Trigger += (name, ctrl, obj) => {
                System.Diagnostics.Debug.WriteLine($"Click on {name} {ctrl}");
                f.GetControl("OK").Enabled = f.IsAllValid();
                if (ctrl == "C_0")
                    f.SetCheckedList(new string[] { "C_1", "C_2", "C_3", "C_4", "C_5", "C_6", "C_7" }, false);
                else if (ctrl.StartsWith("C_"))
                    f.SetCheckedList(new string[] { "C_0" }, false);
                if (ctrl == "R_0")
                    f.SetCheckedList(new string[] { "R_1", "R_2", "R_3", "R_4", "R_5", "R_6" , "R_7" }, false);
                else if (ctrl.StartsWith("R_"))
                    f.SetCheckedList(new string[] { "R_0" }, false);
            };

            if (extTextBoxAutoCompleteSystem.Text.HasChars())
            {
                string systemname = extTextBoxAutoCompleteSystem.Text.Substring(0, extTextBoxAutoCompleteSystem.Text.IndexOfOrLength("(")).Trim();

                if (f.ShowDialogCentred(FindForm(), FindForm().Icon, $"Find {title} from {systemname}", closeicon: true) == DialogResult.OK)
                {
                    var modlist = f.GetCheckedListNames("M_").ToArray();

                    outfittingmodtypes = f.GetCheckBoxBools("M_");
                    outfittingclasses = f.GetCheckBoxBools("C_");
                    outfittingratings = f.GetCheckBoxBools("R_");
                    outfittingsearchdistance = f.GetInt("radius").Value;
                    outfittingclearfilters = f.GetBool("CLRF").Value;

                    if ( modlist.Length>0 && outfittingclasses.Contains(true) && outfittingratings.Contains(true))
                    {
                        EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();
                        List<StationInfo> ssd = sp.SearchOutfitting(systemname, modlist, outfittingclasses, outfittingratings, outfittingsearchdistance, maxresults);
                        if (ssd?.Count > 0)
                        {
                            DrawSearch(ssd, outfittingclearfilters, FilterSettings.Outfitting);
                        }
                        else
                        {
                            MessageBoxTheme.Show(this.FindForm(), $"No stations returned", "Warning".TxID(EDTx.Warning), MessageBoxButtons.OK);
                        }

                    }
                }
            }
        }




        #endregion

        #region Misc

        private void UpdateWordWrap()
        {
            dataGridView.SetWordWrap(extCheckBoxWordWrap.Checked);
            dataViewScrollerPanel.UpdateScroll();
        }

        private void SetFilter(FilterSettings f, string newsetting, bool redraw)
        {
            saver.PutSetting(f.ToString(), newsetting);
            if (redraw)
                Draw(false);
        }

        private string GetFilter(FilterSettings f)
        {
            return saver.GetSetting(f.ToString(), CheckedIconListBoxFormGroup.Disabled);
        }

        private void buttonExtExcel_Click(object sender, EventArgs e)
        {
            Forms.ImportExportForm frm = new Forms.ImportExportForm();
            frm.Export(new string[] { "View" },
                            new Forms.ImportExportForm.ShowFlags[] { Forms.ImportExportForm.ShowFlags.ShowCSVOpenInclude, Forms.ImportExportForm.ShowFlags.None },
                            new string[] { "CSV|*.csv" },
                            new string[] { "stations" }
                );

            if (frm.ShowDialog(FindForm()) == DialogResult.OK)
            {
                if (frm.SelectedIndex == 0)
                {
                    BaseUtils.CSVWriteGrid grd = new BaseUtils.CSVWriteGrid(frm.Delimiter);

                    grd.GetLine += delegate (int r)
                    {
                        if (r < dataGridView.RowCount)
                        {
                            DataGridViewRow rw = dataGridView.Rows[r];
                            object[] ret = new object[dataGridView.ColumnCount];
                            for (int i = 0; i < dataGridView.ColumnCount; i++)
                                ret[i] = rw.Cells[i].Value;

                            return ret;
                        }
                        else
                            return null;
                    };

                    grd.GetHeader += delegate (int c)
                    {
                        if (frm.IncludeHeader)
                        {
                            if (c < dataGridView.ColumnCount)
                                return dataGridView.Columns[c].HeaderText;
                        }
                        return null;
                    };

                    grd.WriteGrid(frm.Path, frm.AutoOpen, FindForm());
                }
            }
        }

        #endregion

    }
}