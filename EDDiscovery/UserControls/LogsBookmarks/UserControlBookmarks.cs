﻿/*
 * Copyright © 2016-2024 EDDiscovery development team
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
using EliteDangerousCore.DB;
using EliteDangerousCore.EDSM;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EDDiscovery.UserControls
{
    public partial class UserControlBookmarks : UserControlCommonBase
    {
        private DataGridViewRow currentedit = null;
        private Timer searchtimer;

        const int TagSize = 24;
        private bool updating_grid;

        #region init
        public UserControlBookmarks()
        {
            InitializeComponent();
        }

        public override void Init()
        {
            DBBaseName = "UCBookmarks";

            searchtimer = new Timer() { Interval = 500 };
            searchtimer.Tick += Searchtimer_Tick;
            GlobalBookMarkList.Instance.OnBookmarkChange += BookmarksChanged;

            var enumlist = new Enum[] { EDTx.UserControlBookmarks_ColType, EDTx.UserControlBookmarks_ColBookmarkName,
                                            EDTx.UserControlBookmarks_ColDescription, EDTx.UserControlBookmarks_ColTags,
                                            EDTx.UserControlBookmarks_labelSearch };
            var enumlistcms = new Enum[] { EDTx.UserControlBookmarks_toolStripMenuItemGotoStar3dmap, EDTx.UserControlBookmarks_openInEDSMToolStripMenuItem };
            var enumlisttt = new Enum[] { EDTx.UserControlBookmarks_textBoxFilter_ToolTip, EDTx.UserControlBookmarks_buttonNew_ToolTip, 
                                            EDTx.UserControlBookmarks_buttonEdit_ToolTip, EDTx.UserControlBookmarks_extButtonEditSystem_ToolTip, 
                                            EDTx.UserControlBookmarks_buttonDelete_ToolTip, EDTx.UserControlBookmarks_buttonExtExcel_ToolTip, 
                                            EDTx.UserControlBookmarks_buttonExtImport_ToolTip,
                                            EDTx.UserControlBookmarks_extButtonNewRegion_ToolTip};
            
            BaseUtils.Translator.Instance.TranslateControls(this, enumlist, new Control[] { userControlSurfaceBookmarks });
            BaseUtils.Translator.Instance.TranslateToolstrip(contextMenuStripBookmarks, enumlistcms, this);
            BaseUtils.Translator.Instance.TranslateTooltip(toolTip, enumlisttt, this);
        }

        public override void LoadLayout()
        {
            DGVLoadColumnLayout(dataGridView);
        }

        public override void Closing()
        {
            DGVSaveColumnLayout(dataGridView);

            SaveBackAnyChanges();

            searchtimer.Dispose();

            GlobalBookMarkList.Instance.OnBookmarkChange -= BookmarksChanged;
        }
        #endregion

        #region Display
        public override void InitialDisplay()
        {
            Display();
            userControlSurfaceBookmarks.Changed += (p) => SaveBackAnyChanges();
            userControlSurfaceBookmarks.CompassSelected += (p,d,lat,lon) => RequestPanelOperation(this, new UserControlCommonBase.SetCompassTarget() { Name = p + ": " + d, Latitude = lat, Longitude = lon });
        }

        private void Display()
        {
            this.dataGridView.SelectionChanged -= new System.EventHandler(this.dataGridViewBookMarks_SelectionChanged);

            int lastrow = dataGridView.CurrentCell != null ? dataGridView.CurrentCell.RowIndex : -1;

            DataGridViewColumn sortcol = dataGridView.SortedColumn != null ? dataGridView.SortedColumn : dataGridView.Columns[0];
            SortOrder sortorder = dataGridView.SortOrder;

            dataViewScrollerPanel.SuspendLayout();
            dataGridView.SuspendLayout();

            dataGridView.Rows.Clear();
            
            foreach (BookmarkClass bk in GlobalBookMarkList.Instance.Bookmarks)
            {
                //System.Diagnostics.Debug.WriteLine("Bookmark " + bk.Name  +":" + bk.Note);
                var rw = dataGridView.RowTemplate.Clone() as DataGridViewRow;
                rw.CreateCells( dataGridView , bk.IsRegion ? "Region" : "System" ,
                    bk.IsRegion ? bk.Heading : bk.StarName,
                    bk.Note,
                    bk.X.ToString("0.##"),
                    bk.Y.ToString("0.##"),
                    bk.Z.ToString("0.##"),
                    "");
                rw.Tag = bk;
                string tags = bk.Tags ?? "";
                rw.Cells[ColTags.Index].Tag = tags;
                rw.Cells[ColTags.Index].ToolTipText = tags;
                TagsForm.SetMinHeight(tags, rw, ColTags.Width, TagSize);

                dataGridView.Rows.Add(rw);
            }

            dataGridView.ResumeLayout();
            dataViewScrollerPanel.ResumeLayout();

            dataGridView.Sort(sortcol, (sortorder == SortOrder.Descending) ? System.ComponentModel.ListSortDirection.Descending : System.ComponentModel.ListSortDirection.Ascending);
            dataGridView.Columns[sortcol.Index].HeaderCell.SortGlyphDirection = sortorder;

            if (lastrow >= 0 && lastrow < dataGridView.Rows.Count)
                dataGridView.SetCurrentAndSelectAllCellsOnRow(Math.Min(lastrow, dataGridView.Rows.Count - 1));

            RefreshCurrentEdit();

            this.dataGridView.SelectionChanged += new System.EventHandler(this.dataGridViewBookMarks_SelectionChanged);
        }

        private void dataGridViewBookMarks_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridViewRow rw = dataGridView.Rows[e.RowIndex];
            TagsForm.PaintTags(rw.Cells[ColTags.Index].Tag as string, EDDConfig.Instance.BookmarkTagImage,
                            dataGridView.GetCellDisplayRectangle(ColTags.Index, rw.Index, false), e.Graphics, TagSize);
        }

        private void dataGridViewBookMarks_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (e.Column == ColTags)
            {
                foreach (DataGridViewRow rw in dataGridView.Rows)
                    TagsForm.SetMinHeight(rw.Cells[ColTags.Index].Tag as string, rw, ColTags.Width, TagSize);
            }
        }

        private void dataGridViewBookMarks_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            if (e.Column.Index == 6)
                e.SortDataGridViewColumnAlpha(true);
            else if (e.Column.Index >= 3)
                e.SortDataGridViewColumnNumeric();
        }


        private void RefreshCurrentEdit()
        {
            if (dataGridView.CurrentCell != null)
            {
                currentedit = dataGridView.Rows[dataGridView.CurrentCell.RowIndex];
                BookmarkClass bk = (BookmarkClass)(currentedit.Tag);
                //System.Diagnostics.Debug.WriteLine("Move to row " + currentedit.Index + " Notes " + bk.Name);
                if (bk.IsRegion)
                    userControlSurfaceBookmarks.Disable();
                else
                    userControlSurfaceBookmarks.Init(bk.StarName,DiscoveryForm.History, bk.PlanetaryMarks);
            }
            else
            {
                currentedit = null;
                userControlSurfaceBookmarks.Disable();
            }
        }

        private void SaveBackAnyChanges()
        {
            if (currentedit != null)        // if editing
            {
                BookmarkClass bk = (BookmarkClass)currentedit.Tag;
                string descr = "";
                if (null != currentedit.Cells[ColDescription.Index].Value)
                {
                    descr = currentedit.Cells[ColDescription.Index].Value.ToString();
                }
                //System.Diagnostics.Debug.WriteLine("Checking for save " + currentedit.Index);

                if (!descr.Equals(bk.Note) || userControlSurfaceBookmarks.Edited)     // notes or planet marks changed
                {
                    updating_grid = true;
                    //System.Diagnostics.Debug.WriteLine("Save back " + bk.Name + " " + newNote);
                    currentedit.Tag = GlobalBookMarkList.Instance.AddOrUpdateBookmark(bk, !bk.IsRegion,
                                    bk.IsRegion ? bk.Heading : bk.StarName,
                                    bk.X, bk.Y, bk.Z, bk.TimeUTC,
                                    descr, bk.Tags,    
                                    userControlSurfaceBookmarks.PlanetMarks);
                    updating_grid = false;
                    userControlSurfaceBookmarks.Edited = false;
                }
            }
        }

        #endregion

        #region Toolbar UI

        private void buttonNew_Click(object sender, EventArgs e)
        {
            updating_grid = true;
            BookmarkHelpers.ShowBookmarkForm(this.FindForm(), DiscoveryForm, null, null);
            updating_grid = false;
            Display();
        }

        private void extButtonNewRegion_Click(object sender, EventArgs e)
        {
            updating_grid = true;
            BookmarkHelpers.ShowBookmarkForm(this.FindForm(), DiscoveryForm, null, null, true);
            updating_grid = false;
            Display();
        }

        private void extButtonEditSystem_Click(object sender, EventArgs e)
        {
            updating_grid = true;
            BookmarkHelpers.ShowBookmarkForm(this.FindForm(), DiscoveryForm, DiscoveryForm.History.GetLast?.System, null);
            updating_grid = false;
            Display();
        }

        private void buttonEdit_Click(object sender, EventArgs e)
        {
            if (currentedit != null)      // if we have a current cell.. 
            {
                BookmarkClass bk = (BookmarkClass)currentedit.Tag;

                SaveBackAnyChanges();
                EliteDangerousCore.ISystem sys = bk.IsStar ? SystemCache.FindSystem(bk.Name, DiscoveryForm.GalacticMapping, EliteDangerousCore.WebExternalDataLookup.All) : null;

                updating_grid = true;
                BookmarkHelpers.ShowBookmarkForm(this.FindForm(), DiscoveryForm, sys, bk);
                updating_grid = false;
                Display();
            }
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            int[] rows = null;

            if (dataGridView.SelectedCells.Count > 0)      // being paranoid
            {
                rows = (from DataGridViewCell x in dataGridView.SelectedCells select x.RowIndex).Distinct().ToArray();
            }

            //System.Diagnostics.Debug.WriteLine("cells {0} rows {1} selrows {2}", dataGridViewBookMarks.SelectedCells.Count, dataGridViewBookMarks.SelectedRows.Count , rows.Length);

            if (rows != null && rows.Length > 1)
            {
                if (ExtendedControls.MessageBoxTheme.Show(FindForm(), string.Format(("Do you really want to delete {0} bookmarks?" + Environment.NewLine + "Confirm or Cancel").T(EDTx.UserControlBookmarks_CFN), rows.Length), "Warning".T(EDTx.Warning), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                {
                    updating_grid = true;
                    foreach (int r in rows)
                    {
                        BookmarkClass bk = (BookmarkClass)dataGridView.Rows[r].Tag;
                        //System.Diagnostics.Debug.WriteLine("Delete " + bk.Name);
                        GlobalBookMarkList.Instance.Delete(bk);
                    }
                    updating_grid = false;
                    Display();
                }

            }
            else if (currentedit != null)      // if we have a current cell.. 
            {
                BookmarkClass bk = (BookmarkClass)currentedit.Tag;

                if (ExtendedControls.MessageBoxTheme.Show(FindForm(), string.Format(("Do you really want to delete the bookmark for {0}" + Environment.NewLine + "Confirm or Cancel").T(EDTx.UserControlBookmarks_CF), bk.Name), "Warning".T(EDTx.Warning), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                {
                    updating_grid = true;
                    GlobalBookMarkList.Instance.Delete(bk);
                    updating_grid = false;
                    Display();
                }
            }
        }

        private void buttonTags_Click(object sender, EventArgs e)
        {
            TagsForm tg = new TagsForm();
            tg.Init("Set Tags".T(EDTx.CaptainsLogEntries_SetTags), this.FindForm().Icon, EDDConfig.Instance.BookmarkTagImage);

            if (tg.ShowDialog() == DialogResult.OK)
            {
                EDDConfig.Instance.BookmarkTagImage = tg.Result;
            }
        }

        private void textBoxFilter_TextChanged(object sender, EventArgs e)
        {
            searchtimer.Stop();
            searchtimer.Start();
        }

        private void Searchtimer_Tick(object sender, EventArgs e)
        {
            searchtimer.Stop();
            this.Cursor = Cursors.WaitCursor;

            SaveBackAnyChanges();

            dataGridView.FilterGridView(textBoxFilter.Text);

            RefreshCurrentEdit();

            this.Cursor = Cursors.Default;
        }



        #endregion

        #region Grid Editing

        private void dataGridViewBookMarks_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)    // row -1 is the header..
            {
                DataGridViewRow rw = dataGridView.Rows[e.RowIndex];
                if (e.ColumnIndex == ColTags.Index)
                    EditTags(rw);
            }
        }

        private void EditTags(DataGridViewRow rw)
        {
            TagsForm.EditTags(this.FindForm(),
                                        EDDConfig.Instance.BookmarkTagImage, rw.Cells[ColTags.Index].Tag as string,
                                        dataGridView.PointToScreen(dataGridView.GetCellDisplayRectangle(ColTags.Index, rw.Index, false).Location),
                                        TagsChanged, rw);
        }

        private void TagsChanged(string newtags, Object tag)
        {
            DataGridViewRow rw = tag as DataGridViewRow;
            rw.Cells[ColTags.Index].Tag = newtags;
            TagsForm.SetMinHeight(rw.Cells[ColTags.Index].Tag as string, rw, ColTags.Width, TagSize);
            dataGridView.InvalidateRow(rw.Index);

            BookmarkClass bk = (BookmarkClass)rw.Tag;
            updating_grid = true;
            GlobalBookMarkList.Instance.AddOrUpdateBookmark(bk, !bk.IsRegion,
                                bk.IsRegion ? bk.Heading : bk.StarName,
                                bk.X, bk.Y, bk.Z, bk.TimeUTC,
                                bk.Note, newtags,
                                userControlSurfaceBookmarks.PlanetMarks);
            updating_grid = false;
        }

        private void dataGridViewBookMarks_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if ( e.ColumnIndex != ColDescription.Index && e.ColumnIndex != ColTags.Index)
                buttonEdit_Click(sender, e);
        }
        private void dataGridViewBookMarks_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if(e.ColumnIndex == ColDescription.Index)
            {
                SaveBackAnyChanges();
            }
        }

        private void dataGridViewBookMarks_SelectionChanged(object sender, EventArgs e)
        {
            SaveBackAnyChanges();
            RefreshCurrentEdit();
        }

        #endregion

        #region Reaction to bookmarks doing stuff from outside sources

        private void BookmarksChanged(BookmarkClass bk, bool deleted)
        {
            //System.Diagnostics.Debug.WriteLine("Changed called " + updating);
            if (updating_grid)
                return;

            // removed this - this can overwrite commanded changes SaveBackAnyChanges();
            Display();
        }

        #endregion

        #region Right clicks

        BookmarkClass rightclickbookmark = null;

        private void dataGridViewBookMarks_MouseDown(object sender, MouseEventArgs e)
        {
            rightclickbookmark = (dataGridView.RightClickRowValid) ? (BookmarkClass)dataGridView.Rows[dataGridView.RightClickRow].Tag : null;
        }

        private void contextMenuStripBookmarks_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            toolStripMenuItemGotoStar3dmap.Enabled = rightclickbookmark != null;
            openInEDSMToolStripMenuItem.Enabled = rightclickbookmark != null && rightclickbookmark.IsStar;
        }

        private void toolStripMenuItemGotoStar3dmap_Click(object sender, EventArgs e)
        {
            DiscoveryForm.Open3DMap(new EliteDangerousCore.SystemClass("Unknown", null, rightclickbookmark.X, rightclickbookmark.Y, rightclickbookmark.Z));
        }

        private void openInEDSMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;
            EliteDangerousCore.EDSM.EDSMClass edsm = new EDSMClass();
            
            if (!edsm.ShowSystemInEDSM(rightclickbookmark.StarName))
                ExtendedControls.MessageBoxTheme.Show(FindForm(), "System could not be found - has not been synched or EDSM is unavailable".T(EDTx.UserControlBookmarks_SysU));

            this.Cursor = Cursors.Default;
        }

        #endregion


        #region Excel

        private void buttonExtExcel_Click(object sender, EventArgs e)
        {
            if (dataGridView.Rows.Count == 0)
            {
                ExtendedControls.MessageBoxTheme.Show(FindForm(), "No data to export", "Export EDSM", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            Forms.ImportExportForm frm = new Forms.ImportExportForm();
            frm.Export( new string[] { "Export Current View" }, 
                            new Forms.ImportExportForm.ShowFlags[] { Forms.ImportExportForm.ShowFlags.ShowCSVOpenInclude },
                            suggestedfilenamesp:new string[] { "Bookmarks" });

            if (frm.ShowDialog(FindForm()) == DialogResult.OK)
            {
                if (frm.SelectedIndex == 0)
                {
                    string path = frm.Path;               //string path = "C:\\code\\f.csv"; // debug

                    BaseUtils.CSVWriteGrid grd = new BaseUtils.CSVWriteGrid(frm.Delimiter);

                    List<string> colh = new List<string>();
                    colh.AddRange(new string[] { "Type", "Time", "System/Region", "Note","X","Y","Z", "Tags", "Planet", "Name", "Comment", "Lat","Long", "Tags"});

                    grd.GetHeader += delegate (int c)
                    {
                        return (c < colh.Count && frm.IncludeHeader) ? colh[c] : null;
                    };

                    int bkrowno = 0;
                    IEnumerator<Tuple<PlanetMarks.Planet, PlanetMarks.Location>> planetloc = null;

                    System.Diagnostics.Debug.WriteLine("Rows " + dataGridView.Rows.Count);

                    grd.GetLineStatus += delegate (int r)
                    {
                        return bkrowno < dataGridView.Rows.Count ? BaseUtils.CSVWriteGrid.LineStatus.OK : BaseUtils.CSVWriteGrid.LineStatus.EOF;
                    };

                    grd.GetLine += delegate (int r)
                    {
                        DataGridViewRow rw = dataGridView.Rows[bkrowno];
                        BookmarkClass bk = rw.Tag as BookmarkClass;
                        bool firstplanetrow = false;

                        if (planetloc == null && bk.HasPlanetaryMarks)          // if not iterating planets, but it has one, iterate
                        {
                            planetloc = bk.PlanetaryMarks.GetEnumerator();
                            planetloc.MoveNext();       // move to first
                            firstplanetrow = true;
                        }

                        List<Object> retrow = new List<Object>
                        {
                            bk.IsRegion ? "Region" : "System",
                            EDDConfig.Instance.ConvertTimeToSelectedFromUTC(bk.TimeUTC),
                            bk.IsRegion ? bk.Heading : bk.StarName,
                            bk.Note,
                            bk.X,
                            bk.Y,
                            bk.Z,
                            bk.Tags,
                        };

                        System.Diagnostics.Debug.WriteLine("Export system " + bkrowno + " " + bk.StarName);

                        if (planetloc != null)
                        {
                            var plloc = planetloc.Current;
                            List<Object> planetrow = new List<Object>
                            {
                                plloc.Item1.Name,       // planet name
                                plloc.Item2.Name,       // loc name
                                plloc.Item2.Comment,    // loc comment
                            };

                            if (plloc.Item2.IsWholePlanetBookmark)
                            {
                                planetrow.Add("");
                                planetrow.Add("");
                            }
                            else
                            {
                                planetrow.Add(plloc.Item2.Latitude);
                                planetrow.Add(plloc.Item2.Longitude);
                            };

                            planetrow.Add(plloc.Item2.Tags);

                            if (!firstplanetrow)
                            {
                                retrow = new List<object>() { "", "", "", "",  "", "", "","" };     // same number at LIST<retrow> above
                            }

                            retrow.AddRange(planetrow);
                        }

                        if (planetloc == null || planetloc.MoveNext() == false)
                        {
                            planetloc = null;
                            bkrowno++;
                        }

                        return retrow.ToArray();
                    };

                    grd.WriteGrid(path, frm.AutoOpen, FindForm());
                }
            }
        }

        private void buttonExtImport_Click(object sender, EventArgs e)
        {
            var frm = new Forms.ImportExportForm();

            frm.Import(new string[] { "CSV"},
                new Forms.ImportExportForm.ShowFlags[] { Forms.ImportExportForm.ShowFlags.ShowImportOptions },
                 new string[] { "CSV|*.csv" }
                 );

            if (frm.ShowDialog(FindForm()) == DialogResult.OK)
            {
                var csv = frm.CSVRead();

                if (csv != null)
                {
                    var rows = frm.ExcludeHeader ? csv.RowsExcludingHeaderRow : csv.Rows;

                    BookmarkClass currentbk = null;

                    
                    foreach (var r in rows)
                    {
                        // 0: "Type", "Time", 2: "System/Region", "Note","X","Y","Z", 7:"Tags", 8:"Planet", "Name", "Comment", 11:"Lat","Long", 13:"Tags"

                        string type = r[0];

                        if (type.HasChars())
                        {
                            bool region = type?.Equals("Region", StringComparison.InvariantCultureIgnoreCase) ?? false;

                            DateTime? timeutc = r.GetDateTime(1);

                            if (timeutc.HasValue)       // need time
                            {
                                timeutc = EDDConfig.Instance.ConvertTimeToUTCFromSelected(timeutc.Value);     // assume import is in selected time base, convert

                                string name = r[2];
                                string note = r[3] ?? "";
                                double? x = r.GetDouble(4);
                                double? y = r.GetDouble(5);
                                double? z = r.GetDouble(6);
                                string tags = r[7] ?? "";

                                if (x != null && y != null && z != null)
                                {
                                    System.Diagnostics.Debug.WriteLine("Bookmark {0} {1} {2} {3} ({4},{5},{6},{7}", type, timeutc.Value.ToStringZulu(), name, note, x, y, z, tags);

                                    currentbk = GlobalBookMarkList.Instance.FindBookmark(name, region);

                                    if (currentbk != null)  // if we have it don't wipe planet marks out
                                    {
                                        GlobalBookMarkList.Instance.AddOrUpdateBookmark(currentbk, !region, name, x.Value, y.Value, z.Value, timeutc.Value, note, tags, currentbk.PlanetaryMarks);
                                    }
                                    else
                                        currentbk = GlobalBookMarkList.Instance.AddOrUpdateBookmark(null, !region, name, x.Value, y.Value, z.Value, timeutc.Value, note, tags, null);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("Not a system with valid coords {0} {1}", r[0], r[1]);
                                }
                            }
                            else
                                System.Diagnostics.Debug.WriteLine("Rejected due to date {0} {1}", r[0], r[1]);
                        }

                        string planet = r[8];

                        if (planet.HasChars() && currentbk != null)
                        {
                            string locname = r[9];
                            string comment = r[10] ?? "";
                            double? latitude = r.GetDouble(11);
                            double? longitude = r.GetDouble(12);
                            string tags = r[13] ?? "";

                            if (!locname.HasChars() && latitude == null && longitude == null) // whole planet bookmark
                            {
                                currentbk.AddOrUpdatePlanetBookmark(planet, comment, tags);
                            }
                            else if (locname.HasChars() && latitude.HasValue && longitude.HasValue)
                            {
                                currentbk.AddOrUpdateLocation(planet, locname, comment, latitude.Value, longitude.Value, tags);
                            }
                        }
                    }

                    PutSetting("ImportExcelFolder", System.IO.Path.GetDirectoryName(frm.Path));
                }
                else
                    ExtendedControls.MessageBoxTheme.Show(FindForm(), "Failed to read " + frm.Path, "Import Failed", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        #endregion
    }
}
