﻿/*
 * Copyright © 2016 - 2023 EDDiscovery development team
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

using EDDiscovery.Controls;
using EliteDangerousCore;
using EliteDangerousCore.EDSM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EDDiscovery.UserControls
{
    public partial class UserControlJournalGrid : UserControlCommonBase
    {
        private JournalFilterSelector cfs;
        private BaseUtils.ConditionLists fieldfilter = new BaseUtils.ConditionLists();
        private Dictionary<long, DataGridViewRow> rowsbyjournalid = new Dictionary<long, DataGridViewRow>();

        private const string dbFilter = "EventFilter2";
        private const string dbHistorySave = "EDUIHistory";
        private const string dbFieldFilter = "ControlFieldFilter";
        private const string dbUserGroups = "UserGroups";

        public delegate void PopOut();
        public PopOut OnPopOut;

        private HistoryList current_historylist;        // the last one set, for internal refresh purposes on sort

        private string searchterms = "system:body:station:stationfaction";

        #region Init

        private class Columns
        {
            public const int Time = 0;
            public const int Event = 1;
            public const int Type = 2;
            public const int Text = 3;
        }

        private Timer searchtimer;

        private Timer todotimer;
        private Queue<Action> todo = new Queue<Action>();
        private Queue<HistoryEntry> queuedadds = new Queue<HistoryEntry>();

        private int fdropdown;     // filter totals

        public UserControlJournalGrid()
        {
            InitializeComponent();
        }

        public override void Init()
        {
            DBBaseName = "JournalGrid";

            dataGridViewJournal.MakeDoubleBuffered();
            dataGridViewJournal.RowTemplate.MinimumHeight = 26;      // enough for the icon
            dataGridViewJournal.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewJournal.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;     // NEW! appears to work https://msdn.microsoft.com/en-us/library/74b2wakt(v=vs.110).aspx
            dataGridViewJournal.AllowRowHeaderVisibleSelection = true;

            cfs = new JournalFilterSelector();
            cfs.AddAllNone();
            cfs.AddJournalExtraOptions();
            cfs.AddJournalEntries();
            cfs.AddUserGroups(GetSetting(dbUserGroups, ""));
            cfs.SaveSettings += EventFilterChanged;

            checkBoxCursorToTop.Checked = true;

            string filter = GetSetting(dbFieldFilter, "");
            if (filter.Length > 0)
                fieldfilter.FromJSON(filter);        // load filter

            searchtimer = new Timer() { Interval = 500 };
            searchtimer.Tick += Searchtimer_Tick;

            todotimer = new Timer() { Interval = 10 };
            todotimer.Tick += Todotimer_Tick;

            DiscoveryForm.OnHistoryChange += HistoryChanged;
            DiscoveryForm.OnNewEntry += AddNewEntry;

            var enumlist = new Enum[] { EDTx.UserControlJournalGrid_ColumnTime, EDTx.UserControlJournalGrid_ColumnEvent, EDTx.UserControlJournalGrid_ColumnDescription, 
                EDTx.UserControlJournalGrid_ColumnInformation, EDTx.UserControlJournalGrid_labelTime, EDTx.UserControlJournalGrid_labelSearch };

            var enumlistcms = new Enum[] { EDTx.UserControlJournalGrid_removeSortingOfColumnsToolStripMenuItem, EDTx.UserControlJournalGrid_jumpToEntryToolStripMenuItem, 
                EDTx.UserControlJournalGrid_mapGotoStartoolStripMenuItem, EDTx.UserControlJournalGrid_viewOnEDSMToolStripMenuItem, EDTx.UserControlJournalGrid_toolStripMenuItemStartStop, 
                EDTx.UserControlJournalGrid_runActionsOnThisEntryToolStripMenuItem, EDTx.UserControlJournalGrid_copyJournalEntryToClipboardToolStripMenuItem };

            var enumlisttt = new Enum[] { EDTx.UserControlJournalGrid_comboBoxTime_ToolTip, EDTx.UserControlJournalGrid_textBoxSearch_ToolTip, 
                EDTx.UserControlJournalGrid_buttonFilter_ToolTip, EDTx.UserControlJournalGrid_buttonExtExcel_ToolTip, EDTx.UserControlJournalGrid_checkBoxCursorToTop_ToolTip };

            BaseUtils.Translator.Instance.TranslateControls(this, enumlist);
            BaseUtils.Translator.Instance.TranslateToolstrip(historyContextMenu, enumlistcms, this);
            BaseUtils.Translator.Instance.TranslateTooltip(toolTip, enumlisttt, this);

            TravelHistoryFilter.InitaliseComboBox(comboBoxTime, GetSetting(dbHistorySave, ""));

            if (TranslatorExtensions.TxDefined(EDTx.UserControlTravelGrid_SearchTerms))     // if translator has it defined, use it (share with travel grid)
                searchterms = searchterms.TxID(EDTx.UserControlTravelGrid_SearchTerms);
        }

        public override void LoadLayout()
        {
            dataGridViewJournal.RowTemplate.MinimumHeight = Math.Max(28, Font.ScalePixels(28));
            DGVLoadColumnLayout(dataGridViewJournal, rowheaderselection: dataGridViewJournal.AllowRowHeaderVisibleSelection);
        }

        public override void Closing()
        {
            todo.Clear();
            todotimer.Stop();
            searchtimer.Stop();
            DGVSaveColumnLayout(dataGridViewJournal);
            PutSetting(dbUserGroups, cfs.GetUserGroupDefinition(1));
            DiscoveryForm.OnHistoryChange -= HistoryChanged;
            DiscoveryForm.OnNewEntry -= AddNewEntry;
            searchtimer.Dispose();
        }

        #endregion

        #region Display

        public override void InitialDisplay()
        {
            Display(DiscoveryForm.History, false);
        }


        private void HistoryChanged()
        {
            Display(DiscoveryForm.History, false);
        }

        private void Display(HistoryList hl, bool disablesorting )
        {
            todo.Clear();           // ensure in a quiet state
            queuedadds.Clear();
            todotimer.Stop();

            this.dataGridViewJournal.Cursor = Cursors.WaitCursor;
            buttonExtExcel.Enabled = buttonFilter.Enabled = buttonField.Enabled = false;

            current_historylist = hl;       // we cache this in case it changes during sorting

            var selpos = dataGridViewJournal.GetSelectedRowOrCellPosition();
            Tuple<long, int> pos = selpos != null ? new Tuple<long, int>(((HistoryEntry)(dataGridViewJournal.Rows[selpos.Item1].Tag)).Journalid, selpos.Item2) : new Tuple<long, int>(-1, 0);

            SortOrder sortorder = dataGridViewJournal.SortOrder;
            int sortcol = dataGridViewJournal.SortedColumn?.Index ?? -1;
            if (sortcol >= 0 && disablesorting)
            {
                dataGridViewJournal.Columns[sortcol].HeaderCell.SortGlyphDirection = SortOrder.None;
                sortcol = -1;
            }

            var filter = (TravelHistoryFilter)comboBoxTime.SelectedItem ?? TravelHistoryFilter.NoFilter;

            List<HistoryEntry> result = filter.Filter(hl.EntryOrder());
            fdropdown = hl.Count - result.Count();

            dataGridViewJournal.Rows.Clear();
            rowsbyjournalid.Clear();

            dataGridViewJournal.Columns[0].HeaderText = EDDConfig.Instance.GetTimeTitle();

            List<HistoryEntry[]> chunks = new List<HistoryEntry[]>();

            int chunksize = 500;
            for (int i = 0; i < result.Count; i += chunksize, chunksize = 2000)
            {
                HistoryEntry[] chunk = new HistoryEntry[i + chunksize > result.Count ? result.Count - i : chunksize];

                result.CopyTo(i, chunk, 0, chunk.Length);
                chunks.Add(chunk);
            }

            var sst = new BaseUtils.StringSearchTerms(textBoxSearch.Text, searchterms);

            HistoryEventFilter hef = new HistoryEventFilter(GetSetting(dbFilter, "All"), fieldfilter, DiscoveryForm.Globals);

            System.Diagnostics.Stopwatch swtotal = new System.Diagnostics.Stopwatch(); swtotal.Start();

            //int lrowno = 0;

            foreach (var chunk in chunks)
            {
                todo.Enqueue(() =>
                {
                    //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch(); sw.Start();

                    List<DataGridViewRow> rowstoadd = new List<DataGridViewRow>();

                    foreach (var item in chunk)
                    {
                        var row = CreateHistoryRow(item, sst, hef);
                        if (row != null)
                        {
                            //row.Cells[2].Value = (lrowno++).ToString() + " " + item.Journalid + " " + (string)row.Cells[2].Value;
                            rowstoadd.Add(row);
                        }
                    }

                    dataGridViewJournal.Rows.AddRange(rowstoadd.ToArray()); // much faster to send in one chunk

                    // System.Diagnostics.Debug.WriteLine("J Chunk Load in " + sw.ElapsedMilliseconds);

                    if (dataGridViewJournal.SelectAndMove(rowsbyjournalid, ref pos, false))
                        FireChangeSelection();

                });
            }

            todo.Enqueue(() =>
            {
                System.Diagnostics.Debug.WriteLine(BaseUtils.AppTicks.TickCount + " JG TOTAL TIME " + swtotal.ElapsedMilliseconds);

                UpdateToolTipsForFilter();

                if (dataGridViewJournal.SelectAndMove(rowsbyjournalid, ref pos, true))
                    FireChangeSelection();

                if (sortcol >= 0)
                {
                    dataGridViewJournal.Sort(dataGridViewJournal.Columns[sortcol], (sortorder == SortOrder.Descending) ? ListSortDirection.Descending : ListSortDirection.Ascending);
                    dataGridViewJournal.Columns[sortcol].HeaderCell.SortGlyphDirection = sortorder;
                }

                this.dataGridViewJournal.Cursor = Cursors.Default;
                buttonExtExcel.Enabled = buttonFilter.Enabled = buttonField.Enabled = true;

                while (queuedadds.Count > 0)              // finally, dequeue any adds added
                {
                    System.Diagnostics.Debug.WriteLine("JG Dequeue paused adds");
                    AddEntry(queuedadds.Dequeue());
                }
            });

            todotimer.Start();
        }

        private void Todotimer_Tick(object sender, EventArgs e)
        {
            if (todo.Count != 0)
            {
                var act = todo.Dequeue();
                act();
            }
            else
            {
                todotimer.Stop();
            }
        }

        private void AddNewEntry(HistoryEntry he)               // on new entry from discovery system
        {
            if (todotimer.Enabled)       // if we have the todotimer running.. we add to the queue.  better than the old loadcomplete, no race conditions
            {
                queuedadds.Enqueue(he);
            }
            else
            {
                AddEntry(he);
            }
        }

        private void AddEntry(HistoryEntry he)
        { 
            var sst = new BaseUtils.StringSearchTerms(textBoxSearch.Text, searchterms);
            HistoryEventFilter hef = new HistoryEventFilter(GetSetting(dbFilter, "All"), fieldfilter, DiscoveryForm.Globals);

            var row = CreateHistoryRow(he, sst, hef);     // we might be filtered out by search
            if (row != null)
            { 
                dataGridViewJournal.Rows.Insert(0, row);

                var filter = (TravelHistoryFilter)comboBoxTime.SelectedItem ?? TravelHistoryFilter.NoFilter;

                if (filter.MaximumNumberOfItems != null)        // this one won't remove the latest one
                {
                    for (int r = dataGridViewJournal.Rows.Count - 1; r >= filter.MaximumNumberOfItems; r--)
                    {
                        dataGridViewJournal.Rows.RemoveAt(r);
                    }
                }

                if (checkBoxCursorToTop.Checked) // Move focus to first row
                {
                    dataGridViewJournal.ClearSelection();
                    dataGridViewJournal.SetCurrentAndSelectAllCellsOnRow(0);       // its the current cell which needs to be set, moves the row marker as well
                    FireChangeSelection();
                }
            }
        }

        private DataGridViewRow CreateHistoryRow(HistoryEntry he, BaseUtils.StringSearchTerms search, HistoryEventFilter hef)
        {
            if (!hef.IsIncluded(he))
                return null;

            DateTime time = EDDConfig.Instance.ConvertTimeToSelectedFromUTC(he.EventTimeUTC);
            he.FillInformation(out string EventDescription, out string EventDetailedInfo);
            string detail = EventDescription;
            detail = detail.AppendPrePad(EventDetailedInfo.LineLimit(15,Environment.NewLine + "..."), Environment.NewLine);

            if (search.Enabled)
            {
                bool matched = false;

                if (search.Terms[0] != null)
                {
                    string timestr = time.ToString();
                    int rown = EDDConfig.Instance.OrderRowsInverted ? he.EntryNumber : (DiscoveryForm.History.Count - he.EntryNumber + 1);
                    string entryrow = rown.ToStringInvariant();
                    matched = timestr.IndexOf(search.Terms[0], StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                                    he.EventSummary.IndexOf(search.Terms[0], StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                                    detail.IndexOf(search.Terms[0], StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                                    entryrow.IndexOf(search.Terms[0], StringComparison.InvariantCultureIgnoreCase) >= 0;

                }

                if (!matched && search.Terms[1] != null)       // system
                    matched = he.System.Name.WildCardMatch(search.Terms[1], true);
                if (!matched && search.Terms[2] != null)       // body
                    matched = he.Status.BodyName?.WildCardMatch(search.Terms[2], true) ?? false;
                if (!matched && search.Terms[3] != null)       // station
                    matched = he.Status.StationName?.WildCardMatch(search.Terms[3], true) ?? false;
                if (!matched && search.Terms[4] != null)       // stationfaction
                    matched = he.Status.StationFaction?.WildCardMatch(search.Terms[4], true) ?? false;

                if (!matched)
                    return null;
            }

            var rw = dataGridViewJournal.RowTemplate.Clone() as DataGridViewRow;
            rw.CreateCells(dataGridViewJournal, time, "", he.EventSummary, detail);

            rw.Tag = he;

            rowsbyjournalid[he.Journalid] = rw;
            return rw;
        }

        private void UpdateToolTipsForFilter()
        {
            string ms = string.Format(" showing {0} original {1}".T(EDTx.UserControlJournalGrid_TT1), dataGridViewJournal.Rows.Count, current_historylist?.Count ?? 0);
            comboBoxTime.SetTipDynamically(toolTip, fdropdown > 0 ? string.Format("Filtered {0}".T(EDTx.UserControlJournalGrid_TTFilt1), fdropdown + ms) : "Select the entries by age, ".T(EDTx.UserControlJournalGrid_TTSelAge) + ms);
        }

	    #endregion

        #region Buttons

        private void buttonFilter_Click(object sender, EventArgs e)
        {
            Button b = sender as Button;
            cfs.Open(GetSetting(dbFilter,"All"), b, this.FindForm());
        }

        private void EventFilterChanged(string newset, Object e)
        {
            string filters = GetSetting(dbFilter, "All");
            if (filters != newset)
            {
                PutSetting(dbFilter, newset);
                Display(current_historylist, false);
            }
        }

        private void textBoxSearch_TextChanged(object sender, EventArgs e)
        {
            searchtimer.Stop();
            searchtimer.Start();
        }

        private void Searchtimer_Tick(object sender, EventArgs e)
        {
            searchtimer.Stop();
            Display(current_historylist, false);
        }

        private void comboBoxJournalWindow_SelectedIndexChanged(object sender, EventArgs e)
        {
            PutSetting(dbHistorySave, comboBoxTime.Text);
            Display(current_historylist, false);
        }

        private void buttonField_Click(object sender, EventArgs e)
        {
            BaseUtils.ConditionLists res = HistoryFilterHelpers.ShowDialog(FindForm(), fieldfilter, DiscoveryForm, "Journal: Filter out fields".T(EDTx.UserControlJournalGrid_JHF));
            if ( res != null )
            {
                fieldfilter = res;
                PutSetting(dbFieldFilter, fieldfilter.GetJSON());
                Display(current_historylist, false);
            }
        }

        #endregion

        private void dataGridViewJournal_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            HistoryEntry he = (HistoryEntry)dataGridViewJournal.Rows[e.RowIndex].Tag;
            int rowno = EDDConfig.Instance.OrderRowsInverted ? he.EntryNumber : (DiscoveryForm.History.Count - he.EntryNumber + 1);
            PaintHelpers.PaintEventColumn(dataGridViewJournal, e, rowno, he, Columns.Event, false);
        }

        #region Mouse Clicks

        private void historyContextMenu_Opening(object sender, CancelEventArgs e)
        {
            toolStripMenuItemStartStop.Enabled = rightclickhe != null;
            mapGotoStartoolStripMenuItem.Enabled = (rightclickhe != null && rightclickhe.System.HasCoordinate);
            viewOnEDSMToolStripMenuItem.Enabled = (rightclickhe != null);
            removeSortingOfColumnsToolStripMenuItem.Enabled = dataGridViewJournal.SortedColumn != null;
            jumpToEntryToolStripMenuItem.Enabled = dataGridViewJournal.Rows.Count > 0;
        }

        HistoryEntry rightclickhe = null;
        HistoryEntry leftclickhe = null;

        private void dataGridViewJournal_MouseDown(object sender, MouseEventArgs e)
        {
            rightclickhe = dataGridViewJournal.RightClickRowValid ? (HistoryEntry)dataGridViewJournal.Rows[dataGridViewJournal.RightClickRow].Tag : null;
            leftclickhe = dataGridViewJournal.LeftClickRowValid ? (HistoryEntry)dataGridViewJournal.Rows[dataGridViewJournal.LeftClickRow].Tag : null;
        }

        private void dataGridViewJournal_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dataGridViewJournal.LeftClickRowValid)                                                   // Click expands it..
            {
                ExtendedControls.InfoForm info = new ExtendedControls.InfoForm();
                leftclickhe.FillInformation(out string EventDescription, out string EventDetailedInfo);
                string infodetailed = EventDescription.AppendPrePad(EventDetailedInfo, Environment.NewLine);
                info.Info( (EDDConfig.Instance.ConvertTimeToSelectedFromUTC(leftclickhe.EventTimeUTC)) + ": " + leftclickhe.EventSummary,
                    FindForm().Icon, infodetailed);
                info.Size = new Size(1200, 800);
                info.Show(FindForm());
            }
        }

        private void dataGridViewJournal_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            FireChangeSelection();
        }

        private void mapGotoStartoolStripMenuItem_Click(object sender, EventArgs e)
        {
            DiscoveryForm.Open3DMap(rightclickhe?.System);
        }

        private void viewOnEDSMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EDSMClass edsm = new EDSMClass();
            if (!edsm.ShowSystemInEDSM(rightclickhe.System.Name))
                ExtendedControls.MessageBoxTheme.Show(this.FindForm(), "System could not be found - has not been synched or EDSM is unavailable".T(EDTx.UserControlJournalGrid_NotSynced));
        }

        private void toolStripMenuItemStartStop_Click(object sender, EventArgs e)       // sync with travel grid call
        {
            this.dataGridViewJournal.Cursor = Cursors.WaitCursor;

            rightclickhe.SetStartStop();                                        // change flag
            DiscoveryForm.History.RecalculateTravel();                          // recalculate all

            foreach (DataGridViewRow row in dataGridViewJournal.Rows)            // dgv could be in any sort order, we have to do the lot
            {
                HistoryEntry he = row.Tag as HistoryEntry;
                if (he.IsFSD || he.StopMarker || he == rightclickhe)
                {
                    he.FillInformation(out string eventdescription, out string unuseddetailinfo);       // recalc it and redisplay
                    row.Cells[ColumnInformation.Index].Value = eventdescription;
                }
            }

            dataGridViewJournal.Refresh();       // to make the start/stop marker appear, refresh

            RequestPanelOperation(this, new TravelHistoryStartStopChanged());        // tell others

            this.dataGridViewJournal.Cursor = Cursors.Default;

        }

        private void runActionsOnThisEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (rightclickhe != null)
                DiscoveryForm.ActionRunOnEntry(rightclickhe, Actions.ActionEventEDList.UserRightClick(rightclickhe));
        }

        private void copyJournalEntryToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (rightclickhe != null && rightclickhe.journalEntry != null)
            {
                string json = rightclickhe.journalEntry.GetJsonString();
                if (json != null)
                {
                    SetClipboardText(json);
                }
            }
        }

        private void removeSortingOfColumnsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Display(current_historylist, true);
        }

        #endregion

        public override bool PerformPanelOperation(UserControlCommonBase sender, object actionobj)
        {
            if (actionobj is UserControlCommonBase.TravelHistoryStartStopChanged)
            {
                Display(current_historylist, false);
            }

            return false;
        }

        public void FireChangeSelection()       // keep for now
        {
        }

        private void jumpToEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int curi = rightclickhe != null ? (EDDConfig.Instance.OrderRowsInverted ? rightclickhe.EntryNumber : (DiscoveryForm.History.Count - rightclickhe.EntryNumber + 1)) : 0;
            int selrow = dataGridViewJournal.JumpToDialog(this.FindForm(), curi, r =>
            {
                HistoryEntry he = r.Tag as HistoryEntry;
                return EDDConfig.Instance.OrderRowsInverted ? he.EntryNumber : (DiscoveryForm.History.Count - he.EntryNumber + 1);
            });

            if (selrow >= 0)
            {
                dataGridViewJournal.ClearSelection();
                dataGridViewJournal.SetCurrentAndSelectAllCellsOnRow(selrow);
                FireChangeSelection();
            }
        }

        private void dataGridViewJournal_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            if (e.Column.Index == 0)
            {
                e.SortDataGridViewColumnDate();
            }
        }


        #region Excel

        private void buttonExtExcel_Click(object sender, EventArgs e)
        {
            Forms.ImportExportForm frm = new Forms.ImportExportForm();
            frm.Export( new string[] { "Export Current View", "Export as Journals" },
                new Forms.ImportExportForm.ShowFlags[] { Forms.ImportExportForm.ShowFlags.ShowDateTimeCSVOpenInclude, Forms.ImportExportForm.ShowFlags.ShowDateTimeOpenInclude },
                new string[] { "CSV export| *.csv", "Journal Export|*.log" }
                );

            if (frm.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                if (frm.SelectedIndex == 1)
                {
                    try
                    { 
                        using (StreamWriter writer = new StreamWriter(frm.Path))
                        {
                            foreach(DataGridViewRow dgvr in dataGridViewJournal.Rows)
                            {
                                HistoryEntry he = dgvr.Tag as HistoryEntry;
                                if (dgvr.Visible && he.EventTimeUTC.CompareTo(frm.StartTimeUTC) >= 0 && he.EventTimeUTC.CompareTo(frm.EndTimeUTC) <= 0)
                                {
                                    string forExport = he.journalEntry.GetJsonString().Replace("\r\n", "");
                                    if (forExport != null)
                                    {
                                        forExport = System.Text.RegularExpressions.Regex.Replace(forExport, "(\"(?:[^\"\\\\]|\\\\.)*\")|\\s+", "$1");
                                        writer.Write(forExport);
                                        writer.WriteLine();
                                    }
                                }
                            }
                        }

                        if (frm.AutoOpen)
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(frm.Path);
                            }
                            catch
                            {
                                ExtendedControls.MessageBoxTheme.Show(FindForm(), "Failed to open " + frm.Path, "Warning".TxID(EDTx.Warning), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            }
                        }
                    }
                    catch
                    {
                        ExtendedControls.MessageBoxTheme.Show(this.FindForm(), "Failed to write to " + frm.Path, "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
                else
                {
                    BaseUtils.CSVWriteGrid grd = new BaseUtils.CSVWriteGrid(frm.Delimiter);

                    grd.GetLineStatus += delegate (int r)
                    {
                        if (r < dataGridViewJournal.Rows.Count)
                        {
                            HistoryEntry he = dataGridViewJournal.Rows[r].Tag as HistoryEntry;
                            return (dataGridViewJournal.Rows[r].Visible &&
                                he.EventTimeUTC.CompareTo(frm.StartTimeUTC) >= 0 &&
                                he.EventTimeUTC.CompareTo(frm.EndTimeUTC) <= 0) ? BaseUtils.CSVWriteGrid.LineStatus.OK : BaseUtils.CSVWriteGrid.LineStatus.Skip;
                        }
                        else
                            return BaseUtils.CSVWriteGrid.LineStatus.EOF;
                    };

                    grd.GetLine += delegate (int r)
                    {
                        DataGridViewRow rw = dataGridViewJournal.Rows[r];
                        return new Object[] { rw.Cells[0].Value, rw.Cells[2].Value, rw.Cells[3].Value };
                    };

                    grd.GetHeader += delegate (int c)
                    {
                        return (c < 3 && frm.IncludeHeader) ? dataGridViewJournal.Columns[c + ((c > 0) ? 1 : 0)].HeaderText : null;
                    };

                    grd.WriteGrid(frm.Path, frm.AutoOpen, FindForm());
                }
            }
        }

        #endregion

    }
}
