﻿/*
 * Copyright © 2016 - 2020 EDDiscovery development team
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
 * 
 * 
 */
using EDDiscovery.Controls;
using EliteDangerousCore;
using EliteDangerousCore.JournalEvents;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace EDDiscovery.UserControls
{
    public partial class UserControlMarketData : UserControlCommonBase
    {
        private string dbBuyOnly = "BuyOnly";
        private string dbHasDemand = "HasDemand";
        private string dbAutoSwap = "AutoSwap";

        #region Init

        public UserControlMarketData()
        {
            InitializeComponent();
        }

        public override void Init()
        {
            DBBaseName = "MarketData";

            dataGridView.MakeDoubleBuffered();
            dataGridView.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            checkBoxBuyOnly.Checked = GetSetting(dbBuyOnly, false);
            this.checkBoxBuyOnly.CheckedChanged += new System.EventHandler(this.checkBoxBuyOnly_CheckedChanged);

            checkBoxHasDemand.Checked = GetSetting(dbHasDemand, false);
            this.checkBoxHasDemand.CheckedChanged += new System.EventHandler(this.checkBoxHasDemand_CheckedChanged);

            checkBoxAutoSwap.Checked = GetSetting(dbAutoSwap, false);

            DiscoveryForm.OnNewEntry += OnNewEntry;
            DiscoveryForm.OnHistoryChange += Discoveryform_OnHistoryChange;

            var enumlist = new Enum[] { EDTx.UserControlMarketData_CategoryCol, EDTx.UserControlMarketData_NameCol, EDTx.UserControlMarketData_SellCol, EDTx.UserControlMarketData_BuyCol, 
                            EDTx.UserControlMarketData_CargoCol, EDTx.UserControlMarketData_DemandCol, EDTx.UserControlMarketData_SupplyCol, EDTx.UserControlMarketData_GalAvgCol, 
                            EDTx.UserControlMarketData_ProfitToCol, EDTx.UserControlMarketData_ProfitFromCol, EDTx.UserControlMarketData_labelLocation, EDTx.UserControlMarketData_labelVs, 
                            EDTx.UserControlMarketData_checkBoxBuyOnly, EDTx.UserControlMarketData_checkBoxHasDemand, EDTx.UserControlMarketData_checkBoxAutoSwap };
            var enumlisttt = new Enum[] { EDTx.UserControlMarketData_comboBoxCustomFrom_ToolTip, EDTx.UserControlMarketData_comboBoxCustomTo_ToolTip, 
                                    EDTx.UserControlMarketData_checkBoxBuyOnly_ToolTip, EDTx.UserControlMarketData_checkBoxHasDemand_ToolTip };

            BaseUtils.Translator.Instance.TranslateControls(this, enumlist);
            BaseUtils.Translator.Instance.TranslateTooltip(toolTip, enumlisttt, this);
        }

        public override void LoadLayout()
        {
            dataGridView.RowTemplate.MinimumHeight= Font.ScalePixels(26);
            DGVLoadColumnLayout(dataGridView);
        }

        public override void Closing()
        {
            DGVSaveColumnLayout(dataGridView);
            PutSetting(dbBuyOnly, checkBoxBuyOnly.Checked);
            PutSetting(dbHasDemand, checkBoxHasDemand.Checked);
            PutSetting(dbAutoSwap, checkBoxAutoSwap.Checked);
            DiscoveryForm.OnNewEntry -= OnNewEntry;
            DiscoveryForm.OnHistoryChange -= Discoveryform_OnHistoryChange;
        }

        public override void InitialDisplay()
        {
            FillComboBoxes();
            RequestPanelOperation(this, new UserControlCommonBase.RequestTravelHistoryPos());     //request an update 
        }

        #endregion

        #region History

        HistoryEntry last_he;           // last HE
        HistoryEntry last_eddmd;        // last edd market data from last_he
        bool notfoundeddmd = false;     // if we did not find one, record it, so we don't keep on checking. cleared on history change or an new journal entry of right type
        HistoryEntry eddmd_left;        // eddmd left comparision, null means use last_he
        HistoryEntry eddmd_right;       // what we are comparing with, null means none
        HistoryEntry current_displayed; // what we are displaying

        List<HistoryEntry> comboboxentries = new List<HistoryEntry>(); // filled by combobox

        private void Discoveryform_OnHistoryChange()
        {
            FillComboBoxes();     // display time or list may have changed
            notfoundeddmd = false;
        }

        private void OnNewEntry(HistoryEntry he)
        {
            if (he.journalEntry is JournalCommodityPricesBase)            // new CMPB, update combo boxes
            {
                FillComboBoxes();
                notfoundeddmd = false;
            }
        }

        public override void ReceiveHistoryEntry(HistoryEntry he)
        {
            if (!Object.ReferenceEquals(he, last_he))       // if last was null, or he has changed, we have a possible change..
            {
                // Find last commodity entry, if notfoundeedmd is true.  notfoundeddmd is cleared on a new market data entry of commodity prices
                HistoryEntry new_last_eddmd = notfoundeddmd ? null : DiscoveryForm.History.GetLastHistoryEntry(x => x.journalEntry is JournalCommodityPricesBase && (x.journalEntry as JournalCommodityPricesBase).Commodities.Count > 0, he);
                notfoundeddmd = new_last_eddmd == null;

                bool eddmdchanged = !Object.ReferenceEquals(new_last_eddmd, last_eddmd);
                bool cargochanged = !Object.ReferenceEquals(last_he?.MaterialCommodity, he?.MaterialCommodity); // is cargo different between he and last_he

                last_eddmd = new_last_eddmd;
                last_he = he;

                //System.Diagnostics.Debug.WriteLine("left {0} right {1} eddmchanged {2} cargo {3}", last_eddmd?.Indexno, last_he?.Indexno, eddmdchanged, cargochanged);

                if (eddmd_left == null)    // if showing travel.. if not, no update due to this.  Need to keep the last_he/last_eddmd going for swapping back
                {
                    if (eddmdchanged || cargochanged)        // if last_eddmd changed.. or cargo
                        Display();
                }
            }
        }

        #endregion

        #region Display

        private void Display()
        {
            DataGridViewColumn sortcol = dataGridView.SortedColumn != null ? dataGridView.SortedColumn : dataGridView.Columns[0];
            SortOrder sortorder = dataGridView.SortOrder;

            dataViewScrollerPanel.SuspendLayout();

            int firstdisplayed = dataGridView.SafeFirstDisplayedScrollingRowIndex();
            string commodity = (dataGridView.CurrentRow != null) ? (string)dataGridView.CurrentRow.Cells[1].Value : null;
            int currentoffset = (dataGridView.CurrentRow != null) ? Math.Max(0,dataGridView.CurrentRow.Index - firstdisplayed) : 0;
            
            dataGridView.Rows.Clear();

            HistoryEntry left = (eddmd_left != null) ? eddmd_left : last_eddmd;       // if we have a selected left, use it, else use the last eddmd
            HistoryEntry cargo = (eddmd_left != null) ? eddmd_left : last_he;           // if we have a selected left, use it, else use the last he

            if (left != null)       // we know it has a journal entry of EDD commodity..
            {
                //System.Diagnostics.Debug.WriteLine(Environment.NewLine + "From " + current_displayed?.WhereAmI + " to " + left.WhereAmI);

                JournalCommodityPricesBase ecp = left.journalEntry as JournalCommodityPricesBase;
                List<CCommodities> list = ecp.Commodities;

                //System.Diagnostics.Debug.WriteLine("Test Right " + eddmd_right?.WhereAmI + " vs " + left.WhereAmI);
                if (eddmd_right != null && !Object.ReferenceEquals(eddmd_right, left))   // if got a comparision, and not the same data..
                {
                    if (checkBoxAutoSwap.Checked &&
                        left.System.Name.Equals(eddmd_right.System.Name) &&     // if left system being displayed is same as right system
                        left.WhereAmI.Equals(eddmd_right.WhereAmI))            // that means we can autoswap comparisions around
                    {
                        //System.Diagnostics.Debug.WriteLine("Arrived at last left station, repick " + current_displayed.WhereAmI + " as comparision");

                        int index = comboboxentries.FindIndex(x => x.System.Name.Equals(current_displayed.System.Name) && x.WhereAmI.Equals(current_displayed.WhereAmI));
                        if (index >= 0)       // if found it, swap to last instance of system
                        {
                            comboBoxCustomTo.Enabled = false;
                            comboBoxCustomTo.SelectedIndex = index + 1;
                            comboBoxCustomTo.Enabled = true;
                            eddmd_right = comboboxentries[index];
                            //System.Diagnostics.Debug.WriteLine("Right is now " + eddmd_right.WhereAmI);
                        }

                    }

                    //System.Diagnostics.Debug.WriteLine("Right " + eddmd_right.System.Name + " " + eddmd_right.WhereAmI);
                    list = CCommodities.Merge(list, ((JournalCommodityPricesBase)eddmd_right.journalEntry).Commodities);
                }

                // look thru out mclist and copmare it with market data, and produce a not found list to show

                List<MaterialCommodityMicroResource> mclist = DiscoveryForm.History.MaterialCommoditiesMicroResources.GetCommoditiesSorted(cargo.MaterialCommodity);      // stuff we have..  commodities only
                List<MaterialCommodityMicroResource> notfound = new List<MaterialCommodityMicroResource>();
                foreach (MaterialCommodityMicroResource m in mclist)
                {
                    int index = list.FindIndex(x => x.fdname.EqualsAlphaNumOnlyNoCase(m.Details.FDName));   // try and match, remove any spaces/_ and lower case it for matching
                    if (index >= 0)
                        list[index].CargoCarried = m.Count; // found it, set cargo count..
                    else if ( m.Details.Type != MaterialCommodityMicroResourceType.ItemType.Drones) // don't add drones
                        notfound.Add(m);        // not found, add to list for bottom
                }

                FontFamily ff = new FontFamily(this.Font.Name);
                bool buyonly = checkBoxBuyOnly.Checked;
                bool hasdemand = checkBoxHasDemand.Checked;

                foreach (CCommodities c in list)
                {
                    // logic here is:
                    // if hasdemand is set, we display if we have demand, or if we have a comparision to display
                    // else if not buy toggle is off, or when on its when it can be bought or we have a comparision to display

                    if (hasdemand ? (c.HasGoodDemand || c.ComparisionBuy) : (!buyonly || c.HasStock || c.ComparisionBuy))
                    {
                        MaterialCommodityMicroResourceType mc = MaterialCommodityMicroResourceType.GetByFDName(c.fdname);

                        string name = mc?.TranslatedName ?? c.locName;
                        if (c.ComparisionRightOnly)
                            name += " @ " + eddmd_right.WhereAmI;

                        object[] rowobj = { mc?.TranslatedType ?? c.loccategory.Alt(c.category) ,
                                            name,
                                            c.sellPrice > 0 ? c.sellPrice.ToString() : "" ,
                                            c.buyPrice > 0 ? c.buyPrice.ToString() : "" ,
                                            c.CargoCarried,
                                            c.HasGoodDemand ? c.demand.ToString() : "" ,       
                                            c.HasStock ? c.stock.ToString() : "" ,
                                            c.meanPrice > 0 ? c.meanPrice.ToString() : "",
                                            c.ComparisionLR,
                                            c.ComparisionRL };

                        DataGridViewRow rw = dataGridView.Rows[dataGridView.Rows.Add(rowobj)];

                        if (c.ComparisionRightOnly && ff != null && ff.IsStyleAvailable(FontStyle.Italic))
                        {
                            for (int i = 1; i < dataGridView.Columns.Count; i++)
                                rw.Cells[i].Style.Font = new Font(this.Font, FontStyle.Italic);
                        }

                        // rw.Cells[0].Value = mc?.TranslatedType + " " + rw.Index.ToString(); // debug
                        //System.Diagnostics.Debug.WriteLine($"Display {c.fdname} {c.demandBracket} {c.stockBracket}");

                        rw.Cells[DemandCol.Index].Tag = c.demandBracket;
                        rw.Cells[SupplyCol.Index].Tag = c.stockBracket;
                        rw.Cells[0].ToolTipText =
                        rw.Cells[1].ToolTipText = c.ToString();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Do not display {c.fdname}");
                    }
                }

                // things in your cargo which are not found

                foreach (MaterialCommodityMicroResource m in notfound)
                {
                    if (m.Count > 0)
                    {
                        object[] rowobj = {     m.Details.TranslatedType + "!!!!",
                                                m.Details.TranslatedName,
                                                "",
                                                "",
                                                m.Count,
                                                "",
                                                "",
                                                "",
                                                "",
                                                "" };

                        int rowno = dataGridView.Rows.Add(rowobj);
                        dataGridView.Rows[rowno].Cells[0].ToolTipText =
                        dataGridView.Rows[rowno].Cells[1].ToolTipText = "Cargo only, no market data on this item".T(EDTx.UserControlMarketData_Conly);
                    }
                }

                current_displayed = left;
                labelLocation.Text = left.System.Name + ":" + left.WhereAmI;
                string r = EDDConfig.Instance.ConvertTimeToSelectedFromUTC(left.EventTimeUTC).ToString();
                toolTip.SetToolTip(labelLocation, r);

            }
            else
            {
                toolTip.SetToolTip(labelLocation, null);
                labelLocation.Text = "No Data".T(EDTx.NoData);
            }

            dataViewScrollerPanel.ResumeLayout();

            dataGridView.Sort(sortcol, (sortorder == SortOrder.Descending) ? ListSortDirection.Descending : ListSortDirection.Ascending);
            dataGridView.Columns[sortcol.Index].HeaderCell.SortGlyphDirection = sortorder;

            if ( commodity != null )
            {
                foreach( DataGridViewRow rw in dataGridView.Rows)
                {
                    string v = (string)rw.Cells[1].Value;
                    if ( v.Equals(commodity))           // Find the commodity, and set it to the same relative position as before.
                    {
                        dataGridView.SetCurrentAndSelectAllCellsOnRow(rw.Index);
                        dataGridView.SafeFirstDisplayedScrollingRowIndex( Math.Max(rw.Index - currentoffset,0));
                        break;
                    }
                }

            }

            //System.Diagnostics.Debug.WriteLine("Stop watch" + swp.ElapsedMilliseconds);
        }

        static Image[] demandsupplybracketimages = new Image[] { EDDiscovery.Icons.Controls.Bar0, EDDiscovery.Icons.Controls.Bar1,
                                        EDDiscovery.Icons.Controls.Bar2, EDDiscovery.Icons.Controls.Bar3};

        private void dataGridViewMarketData_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridViewRow rw = dataGridView.Rows[e.RowIndex];

            // if we have a tag, we may not because of the notfound list
            if (rw.Cells[SupplyCol.Index].Tag != null)
            {
                int supplybracket = (int)rw.Cells[SupplyCol.Index].Tag;
                if (supplybracket >= 0 && supplybracket < demandsupplybracketimages.Length)     // defend against frontier dark arts
                {
                    var supplyrect = dataGridView.GetCellDisplayRectangle(SupplyCol.Index, rw.Index, false);
                    supplyrect.X += supplyrect.Width - supplyrect.Height - 4;
                    supplyrect.Width = supplyrect.Height;
                    e.Graphics.DrawImage(demandsupplybracketimages[supplybracket], supplyrect);
                }

                int demandbracket = (int)rw.Cells[DemandCol.Index].Tag;
                if (demandbracket >= 0 && demandbracket < demandsupplybracketimages.Length)
                {
                    var demandrect = dataGridView.GetCellDisplayRectangle(DemandCol.Index, rw.Index, false);
                    demandrect.X += demandrect.Width - demandrect.Height - 4;
                    demandrect.Width = demandrect.Height;
                    e.Graphics.DrawImage(demandsupplybracketimages[demandbracket], demandrect);
                }
            }
            else
            {

            }
        }

        #endregion

        #region UI

        private void comboBoxCustomFrom_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxCustomFrom.Enabled)
            {
                if (comboBoxCustomFrom.SelectedIndex == 0)
                {
                    eddmd_left = null;
                    Display();
                }
                else if (!Object.ReferenceEquals(comboboxentries[comboBoxCustomFrom.SelectedIndex - 1], eddmd_left))
                {
                    eddmd_left = comboboxentries[comboBoxCustomFrom.SelectedIndex - 1];
                    Display();
                }
            }
        }

        private void comboBoxCustomTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxCustomTo.Enabled)
            {
                HistoryEntry new_compare = comboBoxCustomTo.SelectedIndex == 0 ? null : comboboxentries[comboBoxCustomTo.SelectedIndex - 1];

                if (!Object.ReferenceEquals(eddmd_right, new_compare))
                {
                    eddmd_right = new_compare;
                    Display();
                }
            }
        }

        private void checkBoxBuyOnly_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxBuyOnly.Checked)
                checkBoxHasDemand.Checked = false;

            Display();
        }

        private void checkBoxHasDemand_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxHasDemand.Checked)
                checkBoxBuyOnly.Checked = false;

            Display();
        }

        private void FillComboBoxes()
        {
            string selfrom = comboBoxCustomFrom.Text;
            string selto = comboBoxCustomTo.Text;

            comboBoxCustomFrom.Items.Clear();
            comboBoxCustomTo.Items.Clear();

            comboBoxCustomFrom.Items.Add("Travel History Entry Last".T(EDTx.UserControlMarketData_LEntry));
            comboBoxCustomTo.Items.Add("None".T(EDTx.None));

            comboboxentries.Clear();

            List<HistoryEntry> hlcpb = HistoryList.FilterByCommodityPricesBackwards(DiscoveryForm.History.EntryOrder());
            JournalCommodityPricesBase last = null;

            foreach (HistoryEntry h in hlcpb)
            {
                var j = (JournalCommodityPricesBase)h.journalEntry;
                if (last == null || !j.Station.Equals(last.Station) || last.EventTimeUTC - j.EventTimeUTC >= new TimeSpan(0, 15, 0))
                {
                    comboboxentries.Add(h);
                    string v = h.System.Name + ":" + h.WhereAmI + " " + "on".T(EDTx.on) + " " + EDDConfig.Instance.ConvertTimeToSelectedFromUTC(h.EventTimeUTC).ToString();
                    if (h.journalEntry is JournalEDDCommodityPrices)
                        v += " (CAPI)";
                    comboBoxCustomFrom.Items.Add(v);
                    comboBoxCustomTo.Items.Add(v);
                    last = j;
                }
            }

            comboBoxCustomFrom.Enabled = comboBoxCustomTo.Enabled = false;

            if (comboBoxCustomFrom.Items.Contains(selfrom))
                comboBoxCustomFrom.SelectedItem = selfrom;
            else
                comboBoxCustomFrom.SelectedIndex = 0;

            if (comboBoxCustomTo.Items.Contains(selto))
                comboBoxCustomTo.SelectedItem = selto;
            else
                comboBoxCustomTo.SelectedIndex = 0;

            comboBoxCustomFrom.Enabled = comboBoxCustomTo.Enabled = true;
        }

        #endregion


        #region Grid

        private void dataGridViewMarketData_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            if (e.Column.Index >= 2)        // 2 on are numbers
                e.SortDataGridViewColumnNumeric();
        }

        #endregion


    }
}
