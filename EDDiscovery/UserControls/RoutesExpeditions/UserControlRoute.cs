﻿/*
 * Copyright © 2019-2023 EDDiscovery development team
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
using EMK.LightGeometry;
using ExtendedControls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace EDDiscovery.UserControls
{
    public partial class UserControlRoute : UserControlCommonBase
    {
        private List<ISystem> routeSystems; // only valid systems get passed back
        private bool changesilence;

        private System.Windows.Forms.Timer fromupdatetimer;
        private System.Windows.Forms.Timer toupdatetimer;
        private ManualResetEvent CloseRequested = new ManualResetEvent(false);

        #region  Init

        public UserControlRoute()
        {
            InitializeComponent();
        }

        public override void Init()
        {
            DBBaseName = "UCRoute";

            EnableOutputButtons();

            fromupdatetimer = new System.Windows.Forms.Timer();
            toupdatetimer = new System.Windows.Forms.Timer();

            fromupdatetimer.Interval = 1000;
            fromupdatetimer.Tick += FromUpdateTick;
            toupdatetimer.Interval = 1000;
            toupdatetimer.Tick += ToUpdateTick;

            string[] MetricNames = {        // synchronise with SystemCache.SystemsNearestMetric, really should be translated, but there you go.
                "Nearest to Waypoint".T(EDTx.UserControlRoute_M1),
                "Minimum Deviation from Path".T(EDTx.UserControlRoute_M2),
                "Nearest to Waypoint with dev<=100ly".T(EDTx.UserControlRoute_M3),
                "Nearest to Waypoint with dev<=250ly".T(EDTx.UserControlRoute_M4),
                "Nearest to Waypoint with dev<=500ly".T(EDTx.UserControlRoute_M5),
                "Nearest to Waypoint + Deviation / 2".T(EDTx.UserControlRoute_M6),
                };

            foreach (SystemCache.SystemsNearestMetric values in Enum.GetValues(typeof(SystemCache.SystemsNearestMetric)))
                comboBoxRoutingMetric.Items.Insert((int)values, MetricNames[(int)values]);

            changesilence = true;

            textBox_From.SetAutoCompletor(SystemCache.ReturnSystemAdditionalListForAutoComplete, true);
            textBox_From.AutoCompleteTimeout = 500;
            textBox_To.SetAutoCompletor(SystemCache.ReturnSystemAdditionalListForAutoComplete , true);
            textBox_To.AutoCompleteTimeout = 500;

            textBox_From.Text = GetSetting("_RouteFrom", "");
            textBox_To.Text = GetSetting("_RouteTo", "");
            valueBox_Range.Value = GetSetting("_RouteRange", 30);
            valueBox_FromX.ValueNoChange = GetSetting("_RouteFromX", 0.0);
            valueBox_FromY.ValueNoChange = GetSetting("_RouteFromY", 0.0);
            valueBox_FromZ.ValueNoChange = GetSetting("_RouteFromZ", 0.0);
            valueBox_ToX.ValueNoChange = GetSetting("_RouteToX", 0.0);
            valueBox_ToY.ValueNoChange = GetSetting("_RouteToY", 0.0);
            valueBox_ToZ.ValueNoChange = GetSetting("_RouteToZ", 0.0);

            int metricvalue = GetSetting("RouteMetric", 0);
            comboBoxRoutingMetric.SelectedIndex = Enum.IsDefined(typeof(SystemCache.SystemsNearestMetric), metricvalue)
                ? metricvalue
                : (int) SystemCache.SystemsNearestMetric.IterativeNearestWaypoint;

            UpdateDistance();
            EnableRouteButtonsIfValid();
            
            changesilence = false;

            edsmSpanshButton.Init(this, "EDSMSpansh", "");

            var enumlist = new Enum[] { EDTx.UserControlRoute_SystemCol, EDTx.UserControlRoute_NoteCol, EDTx.UserControlRoute_DistanceCol, EDTx.UserControlRoute_StarClassCol, EDTx.UserControlRoute_WayPointDistCol,
                                        EDTx.UserControlRoute_DeviationCol,
                                        EDTx.UserControlRoute_checkBox_FsdBoost, EDTx.UserControlRoute_buttonExtTravelTo, EDTx.UserControlRoute_buttonExtTravelFrom,
                                        EDTx.UserControlRoute_buttonExtTargetTo,  EDTx.UserControlRoute_buttonTargetFrom, EDTx.UserControlRoute_labelEDSMBut,
                                        EDTx.UserControlRoute_cmd3DMap, EDTx.UserControlRoute_labelLy2, EDTx.UserControlRoute_labelLy1, EDTx.UserControlRoute_labelTo,
                                        EDTx.UserControlRoute_labelMaxJump, EDTx.UserControlRoute_labelDistance, EDTx.UserControlRoute_labelMetric,
                                        EDTx.UserControlRoute_extButtonRoute, EDTx.UserControlRoute_labelFrom,
                                        EDTx.UserControlRoute_groupBoxSpansh, EDTx.UserControlRoute_extButtonSpanshRoadToRiches, EDTx.UserControlRoute_extButtonNeutronRouter,
                                        EDTx.UserControlRoute_extButtonFleetCarrier,EDTx.UserControlRoute_extButtonSpanshGalaxyPlotter,EDTx.UserControlRoute_extButtonExoMastery,
                                        EDTx.UserControlRoute_extButtonSpanshAmmoniaWorlds,EDTx.UserControlRoute_extButtonSpanshEarthLikes,EDTx.UserControlRoute_extButtonSpanshTradeRouter,
                                        EDTx.UserControlRoute_groupBoxInternal,EDTx.UserControlRoute_groupBoxPara};
                                        
            BaseUtils.Translator.Instance.TranslateControls(this, enumlist);

            var enumlistcms = new Enum[] { EDTx.UserControlRoute_showInEDSMToolStripMenuItem, EDTx.UserControlRoute_copyToolStripMenuItem, EDTx.UserControlRoute_showScanToolStripMenuItem };
            BaseUtils.Translator.Instance.TranslateToolstrip(contextMenuStrip, enumlistcms, this);

            var enumlisttt = new Enum[] { EDTx.UserControlRoute_checkBox_FsdBoost_ToolTip, EDTx.UserControlRoute_buttonExtExcel_ToolTip, EDTx.UserControlRoute_textBox_ToName_ToolTip, 
                                        EDTx.UserControlRoute_textBox_FromName_ToolTip, EDTx.UserControlRoute_comboBoxRoutingMetric_ToolTip, EDTx.UserControlRoute_buttonExtTravelTo_ToolTip, 
                                        EDTx.UserControlRoute_buttonExtTravelFrom_ToolTip, EDTx.UserControlRoute_buttonExtTargetTo_ToolTip, EDTx.UserControlRoute_buttonToEDSM_ToolTip, 
                                        EDTx.UserControlRoute_buttonFromEDSM_ToolTip, EDTx.UserControlRoute_buttonTargetFrom_ToolTip, EDTx.UserControlRoute_checkBoxEDSM_ToolTip, 
                                        EDTx.UserControlRoute_cmd3DMap_ToolTip, EDTx.UserControlRoute_textBox_From_ToolTip, EDTx.UserControlRoute_textBox_Range_ToolTip, 
                                        EDTx.UserControlRoute_textBox_To_ToolTip, EDTx.UserControlRoute_textBox_Distance_ToolTip, EDTx.UserControlRoute_textBox_ToZ_ToolTip, 
                                        EDTx.UserControlRoute_textBox_ToY_ToolTip, EDTx.UserControlRoute_textBox_ToX_ToolTip, EDTx.UserControlRoute_textBox_FromZ_ToolTip, 
                                        EDTx.UserControlRoute_extButtonRoute_ToolTip, EDTx.UserControlRoute_textBox_FromY_ToolTip, EDTx.UserControlRoute_textBox_FromX_ToolTip };
            BaseUtils.Translator.Instance.TranslateTooltip(toolTip, enumlisttt, this);

            waitforspanshresulttimer.Interval = 1000;
            waitforspanshresulttimer.Tick += Waitforspanshresulttimer_Tick;

            NoteCol.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        }

        public override void LoadLayout()
        {
           // tbd DGVLoadColumnLayout(dataGridViewRoute);
        }

        public override void InitialDisplay()
        {
            RequestPanelOperation(this, new UserControlCommonBase.RequestTravelHistoryPos());     //request an update 
        }

        public override void Closing()
        {
            DGVSaveColumnLayout(dataGridViewRoute);

            if (routingthread != null && routingthread.IsAlive && plotter != null)
            {
                plotter.StopPlotter = true;
                CloseRequested.Set();
                routingthread.Join();
            }

            PutSetting("_RouteFrom", textBox_From.Text);
            PutSetting("_RouteTo", textBox_To.Text);
            PutSetting("_RouteRange", (int)valueBox_Range.Value);
            PutSetting("_RouteFromX", valueBox_FromX.Value);
            PutSetting("_RouteFromY", valueBox_FromY.Value);
            PutSetting("_RouteFromZ", valueBox_FromZ.Value);
            PutSetting("_RouteToX", valueBox_ToX.Value);
            PutSetting("_RouteToY", valueBox_ToY.Value);
            PutSetting("_RouteToZ", valueBox_ToZ.Value);
            PutSetting("_RouteMetric", comboBoxRoutingMetric.SelectedIndex);
        }

        #endregion

        #region From

        private bool GetCoordsFrom(out Point3D pos)
        {
            if (valueBox_FromX.IsValid && valueBox_FromY.IsValid && valueBox_FromZ.IsValid)
            {
                pos = new Point3D(valueBox_FromX.Value, valueBox_FromY.Value, valueBox_FromZ.Value);
                return true;
            }
            else
            {
                pos = new Point3D(0, 0, 0);
                return false;
            }
        }

        // give box updating, and optional new From name

        private void UpdateFrom(object sender, string optupdatefrom = null)
        {
            changesilence = true;

            if (optupdatefrom != null)
                textBox_From.Text = optupdatefrom;

            if (sender == textBox_From)
            {
                ISystem ds1 = SystemCache.FindSystem(SystemNameOnly(textBox_From.Text), DiscoveryForm.GalacticMapping, EliteDangerousCore.WebExternalDataLookup.All);     // if we have a name, find it

                if (ds1 != null)
                {
                    textBox_FromName.Text = ds1.Name;
                    valueBox_FromX.ValueNoChange = ds1.X;
                    valueBox_FromY.ValueNoChange = ds1.Y;
                    valueBox_FromZ.ValueNoChange = ds1.Z;
                }
                else
                {
                    valueBox_FromX.SetBlank();
                    valueBox_FromY.SetBlank();
                    valueBox_FromZ.SetBlank();
                }
            }
            else
            {
                string res = "",resname="";
                if (GetCoordsFrom(out Point3D curpos))          // else if we have co-ords, find nearest
                {
                    Cursor = Cursors.WaitCursor;

                    ISystem nearest = SystemCache.FindNearestSystemTo(curpos.X, curpos.Y, curpos.Z, 40, edsmSpanshButton.WebLookup, DiscoveryForm.GalacticMapping);

                    if (nearest != null)
                    {
                        res = resname = nearest.Name;

                        double distance = Point3D.DistanceBetween(curpos, new Point3D(nearest.X, nearest.Y, nearest.Z));
                        if (distance > 0.1)
                            resname = nearest.Name + " @ " + distance.ToString("0.00") + "ly";
                    }
                    Cursor = Cursors.Default;
                }

                textBox_From.Text = res;
                textBox_FromName.Text = resname;
            }

            UpdateDistance();
            EnableRouteButtonsIfValid();
            changesilence = false;
        }

        void FromUpdateTick(object sender, EventArgs e)                 // timer timed out, 
        {
            fromupdatetimer.Stop();
            UpdateFrom(fromupdatetimer.Tag);
        }

        private void textBox_From_TextChanged(object sender, EventArgs e)
        {
            if (!changesilence)
            {
                fromupdatetimer.Stop();
                fromupdatetimer.Tag = sender;
                fromupdatetimer.Start();
            }
        }

        private void valueBox_From_ValueChanged(object sender, EventArgs e)
        {
            fromupdatetimer.Stop();
            fromupdatetimer.Tag = sender;
            fromupdatetimer.Start();
        }

        private void buttonFromHistory_Click(object sender, EventArgs e)
        {
            var last_history_he = DiscoveryForm.History.GetLast;
            if (last_history_he != null)
                UpdateFrom(textBox_From, last_history_he.System.Name);
        }

        private void buttonFromTarget_Click(object sender, EventArgs e)
        {
            if (TargetClass.GetTargetPosition(out string name, out double x, out double y, out double z))
            {
                UpdateFrom(textBox_From, name);
            }
        }

        private void buttonFromEDSM_Click(object sender, EventArgs e)
        {
            string sysname = SystemNameOnly(textBox_From.Text);
            EDSMClass edsm = new EDSMClass();
            if (!edsm.ShowSystemInEDSM(sysname))
                MessageBoxTheme.Show(FindForm(), "System unknown to EDSM");
        }

        private void extButtonFromSpansh_Click(object sender, EventArgs e)
        {
            string sysname = SystemNameOnly(textBox_From.Text);
            EliteDangerousCore.Spansh.SpanshClass.LaunchBrowserForSystem(sysname);
        }

        #endregion

        #region To

        public bool GetCoordsTo(out Point3D pos)
        {
            if (valueBox_ToX.IsValid && valueBox_ToY.IsValid && valueBox_ToZ.IsValid)
            {
                pos = new Point3D(valueBox_ToX.Value, valueBox_ToY.Value, valueBox_ToZ.Value);
                return true;
            }
            else
            {
                pos = new Point3D(0, 0, 0);
                return false;
            }
        }

        private void UpdateTo(object sender, string optupdateto = null)
        {
            changesilence = true;

            if (optupdateto!= null)
                textBox_To.Text = optupdateto;

            if (sender == textBox_To)
            {
                ISystem ds1 = SystemCache.FindSystem(SystemNameOnly(textBox_To.Text), DiscoveryForm.GalacticMapping, EliteDangerousCore.WebExternalDataLookup.All);
                if (ds1 != null)
                {
                    textBox_ToName.Text = ds1.Name;
                    valueBox_ToX.ValueNoChange = ds1.X;
                    valueBox_ToY.ValueNoChange = ds1.Y;
                    valueBox_ToZ.ValueNoChange = ds1.Z;
                }
                else
                {
                    valueBox_ToX.SetBlank();
                    valueBox_ToY.SetBlank();
                    valueBox_ToZ.SetBlank();
                }
            }
            else
            {
                string res = "", resname = "";

                if (GetCoordsTo(out Point3D curpos))
                {
                    Cursor = Cursors.WaitCursor;

                    ISystem nearest = SystemCache.FindNearestSystemTo(curpos.X, curpos.Y, curpos.Z, 40, edsmSpanshButton.WebLookup, DiscoveryForm.GalacticMapping);

                    if (nearest != null)
                    {
                        res = resname = nearest.Name;

                        double distance = Point3D.DistanceBetween(curpos, new Point3D(nearest.X, nearest.Y, nearest.Z));
                        if (distance > 0.1)
                            resname = nearest.Name + " @ " + distance.ToString("0.00") + "ly";
                    }
                    Cursor = Cursors.Default;
                }

                textBox_To.Text = res;
                textBox_ToName.Text = resname;
            }

            UpdateDistance();
            EnableRouteButtonsIfValid();
            changesilence = false;
        }

        void ToUpdateTick(object sender, EventArgs e)
        {
            toupdatetimer.Stop();
            UpdateTo(toupdatetimer.Tag);
        }

        private void textBox_To_TextChanged(object sender, EventArgs e)
        {
            if (!changesilence)
            {
                toupdatetimer.Stop();
                toupdatetimer.Tag = sender;
                toupdatetimer.Start();
            }
        }

        private void valueBox_To_ValueChanged(object sender, EventArgs e)
        {
            toupdatetimer.Stop();
            toupdatetimer.Tag = sender;
            toupdatetimer.Start();
        }

        private void buttonToHistory_Click(object sender, EventArgs e)
        {
            var last_history_he = DiscoveryForm.History.GetLast;
            if (last_history_he != null)
                UpdateTo(textBox_To, last_history_he.System.Name);
        }

        private void buttonToTarget_Click(object sender, EventArgs e)
        {
            if (TargetClass.GetTargetPosition(out string name, out double x, out double y, out double z))
            {
                UpdateTo(textBox_To, name);
            }
        }

        private void buttonToEDSM_Click(object sender, EventArgs e)
        {
            string sysname = SystemNameOnly(textBox_To.Text);
            EDSMClass edsm = new EDSMClass();
            if (!edsm.ShowSystemInEDSM(sysname))
                MessageBoxTheme.Show(FindForm(), "System unknown to EDSM");
        }

        private void extButtonToSpansh_Click(object sender, EventArgs e)
        {
            string sysname = SystemNameOnly(textBox_To.Text);
            EliteDangerousCore.Spansh.SpanshClass.LaunchBrowserForSystem(sysname);
        }

        #endregion

        #region Internal Route Plotter

        private RoutePlotter plotter = null;

        private void button_Route_Click(object sender, EventArgs e)
        {
            if (routingthread == null  || !routingthread.IsAlive)
            {
                plotter = new RoutePlotter();
                plotter.MaxRange = valueBox_Range.Value;
                GetCoordsFrom(out plotter.Coordsfrom);                      // will be valid for a system or a co-ords box
                GetCoordsTo(out plotter.Coordsto);
                plotter.FromSystem = !textBox_FromName.Text.Contains("@") && textBox_From.Text.HasChars() ? textBox_From.Text : "START POINT";
                plotter.ToSystem = !textBox_ToName.Text.Contains("@") && textBox_To.Text.HasChars() ? textBox_To.Text : "END POINT";
                plotter.RouteMethod = (SystemCache.SystemsNearestMetric) comboBoxRoutingMetric.SelectedIndex;
                plotter.UseFsdBoost = checkBox_FsdBoost.Checked;
                plotter.WebLookup = edsmSpanshButton.WebLookup;

                int PossibleJumps = (int)(Point3D.DistanceBetween(plotter.Coordsfrom, plotter.Coordsto) / plotter.MaxRange);

                if (PossibleJumps > 100)
                {
                    DialogResult res = MessageBoxTheme.Show(FindForm(),
                        string.Format(("This will result in a large number ({0}) of jumps" + Environment.NewLine + "Confirm please").T(EDTx.UserControlRoute_Confirm),
                        PossibleJumps), "Warning".T(EDTx.Warning), MessageBoxButtons.YesNo);
                    if (res != System.Windows.Forms.DialogResult.Yes)
                    {
                        return;
                    }
                }

                dataGridViewRoute.Rows.Clear();
                routingthread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(EDDRoutingThread));
                routingthread.Name = "Thread Route";

                extButtonRoute.Text = "Cancel".T(EDTx.Cancel);

                EnableOutputButtons();
                EnableRouteButtons(true, false);        // keep cancel valid

                routingthread.Start(plotter);
            }
            else
            {
                plotter.StopPlotter = true;
                EnableRouteButtons();
            }
        }

        private Thread routingthread;

        private void EDDRoutingThread(object _plotter)
        {
            RoutePlotter p = (RoutePlotter)_plotter;

            routeSystems = null;    // so its null until route interative finishes

            routeSystems = p.RouteIterative(EDDAppendData);

            this.BeginInvoke(new Action(() =>
                {
                    RequestPanelOperation(this, new PushRouteList() { Systems = routeSystems });
                    EnableOutputButtons(true);
                    extButtonRoute.Text = "Find Route".TxID(EDTx.UserControlRoute_extButtonRoute);
                    EnableRouteButtonsIfValid();
                }));
        }

        private void EDDAppendData(RoutePlotter.ReturnInfo info)   // IN thread context, need to invoke
        {
            var ar = BeginInvoke((MethodInvoker)delegate      // using Invoke blocks the thread until the UI finishes.  Using BeginInvoke async causes it to overload the UI
            {
                DataGridViewRow rw = dataGridViewRoute.RowTemplate.Clone() as DataGridViewRow;
                rw.CreateCells(dataGridViewRoute,
                        info.name,
                        info?.system?.Tag as string ?? "",
                        double.IsNaN(info.dist) ? "" : info.dist.ToString("N2"),
                        info.pos == null ? "" : info.pos.X.ToString("0.####"),
                        info.pos == null ? "" : info.pos.Y.ToString("0.####"),
                        info.pos == null ? "" : info.pos.Z.ToString("0.####"),
                        info.pos == null ? "" : info.system == null ? Bodies.StarName(EDStar.Unknown) : Bodies.StarName(info.system.MainStarType),
                        double.IsNaN(info.waypointdist) ? "" : info.waypointdist.ToString("0.0"),
                        double.IsNaN(info.deviation) ? "" : info.deviation.ToString("0.0")
                        );

                rw.Tag = info.system;       // may be null if waypoint or not a system
                rw.Cells[0].Tag = info.system?.Name;    // write the name of the system into the cells'tag for copying
                rw.HeaderCell.Value = info.pos != null ? (dataGridViewRoute.Rows.Count + 1).ToStringInvariant() : "-";
                dataGridViewRoute.Rows.Add(rw);
                if (!rw.Displayed)
                {
                    dataGridViewRoute.SafeFirstDisplayedScrollingRowIndex(dataGridViewRoute.SafeFirstDisplayedScrollingRowIndex()+1);
                }
            });

            WaitHandle.WaitAny(new WaitHandle[] { CloseRequested, ar.AsyncWaitHandle });
        }

        #endregion

        #region Spansh

        const int topmargin = 30;
        const int dataleft = 140;
        Size numberboxsize = new Size(64, 24);
        Size comboboxsize = new Size(200, 24);
        Size textboxsize = new Size(200, 28);
        Size checkboxsize = new Size(154, 24);
        Size labelsize = new Size(138, 24);
        System.Windows.Forms.Timer waitforspanshresulttimer = new System.Windows.Forms.Timer();
        string spanshjobname;
        enum Spanshquerytype { RoadToRiches, Neutron, AmmoniaWorlds, EarthLikes, TradeRouter, FleetCarrier, GalaxyPlotter, ExoMastery };
        Spanshquerytype spanshquerytype;

        private void extButtonSpanshRoadToRiches_Click(object sender, EventArgs e)
        {
            CommonSpanshQuery(Spanshquerytype.RoadToRiches);
        }

        private void extButtonSpanshAmmoniaWorlds_Click(object sender, EventArgs e)
        {
            CommonSpanshQuery(Spanshquerytype.AmmoniaWorlds);
        }

        private void extButtonSpanshEarthLikes_Click(object sender, EventArgs e)
        {
            CommonSpanshQuery(Spanshquerytype.EarthLikes);
        }

        private void CommonSpanshQuery(Spanshquerytype qt)
        {
            bool roadtoriches = qt == Spanshquerytype.RoadToRiches;

            ConfigurableForm f = new ConfigurableForm();

            int vpos = topmargin;

            f.AddLabelAndEntry("Search radius", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("radius", typeof(NumberBoxInt), qt == Spanshquerytype.RoadToRiches ? "25" : "500", new Point(dataleft, 0), numberboxsize, "Search radius along path to search for worlds") { NumberBoxLongMinimum = 10 });
            f.AddLabelAndEntry("Max Systems", new Point(4, 4), ref vpos, 32, labelsize,new ConfigurableForm.Entry("maxsystems", typeof(NumberBoxInt), "100", new Point(dataleft, 0), numberboxsize, "Maximum systems to route through") { NumberBoxLongMinimum = 1 });

            if (roadtoriches)
                f.Add(ref vpos, 32, new ConfigurableForm.Entry("mappingvalue", typeof(ExtCheckBox), "Use mapping value", new Point(4, 0), checkboxsize, "Base on mapping not scan value") { CheckBoxChecked = false, ContentAlign = ContentAlignment.MiddleRight });

            f.Add(ref vpos, 32, new ConfigurableForm.Entry("avoidthargoids", typeof(ExtCheckBox), "Avoid thargoids", new Point(4, 0), checkboxsize, "Avoid Thargoids") { CheckBoxChecked = true, ContentAlign = ContentAlignment.MiddleRight });
            f.AddLabelAndEntry("Max LS", new Point(4,4), ref vpos, 32, labelsize,new ConfigurableForm.Entry("maxls", typeof(NumberBoxInt), qt == Spanshquerytype.RoadToRiches ? "1000000" : "50000", new Point(dataleft, 0), numberboxsize, "Maximum LS from arrival to consider") { NumberBoxLongMinimum = 10 });

            if (roadtoriches)
                f.AddLabelAndEntry("Min Scan Value", new Point(4,4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("minscan", typeof(NumberBoxInt), "100000", new Point(dataleft, 0), numberboxsize, "Minimum value of body") { NumberBoxLongMinimum = 100 });

            f.Add(ref vpos, 32, new ConfigurableForm.Entry("loop", typeof(ExtCheckBox), "Return to start", new Point(4, 0), checkboxsize, "Return to start system for route") { CheckBoxChecked = true, ContentAlign = ContentAlignment.MiddleRight });
          
            f.AddOK(new Point(140, vpos+16), "OK", anchor: AnchorStyles.Right | AnchorStyles.Bottom);
            f.InstallStandardTriggers();
            f.Trigger += (name, text, obj) => { f.GetControl("OK").Enabled = f.IsAllValid(); };

            if (f.ShowDialogCentred(FindForm(), FindForm().Icon, qt.ToString().SplitCapsWordFull(), closeicon: true) == DialogResult.OK)
            {
                EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();

                spanshjobname = sp.RequestRoadToRichesAmmoniaEarthlikes(textBox_From.Text, textBox_To.Text, (int)valueBox_Range.Value, 
                                                    f.GetInt("radius").Value, f.GetInt("maxsystems").Value,
                                                    f.GetBool("avoidthargoids").Value, f.GetBool("loop").Value, f.GetInt("maxls").Value,
                                                    roadtoriches ? f.GetInt("minscan").Value : 1,
                                                    roadtoriches ? f.GetBool("mappingvalue").Value : default(bool?),
                                                    roadtoriches ? null : qt == Spanshquerytype.AmmoniaWorlds ? "Ammonia world" : "Earth-like world"
                                                    );
                StartSpanshQueryOp(qt);
            }
        }


        private void extButtonNeutronRouter_Click(object sender, EventArgs e)
        {
            ConfigurableForm f = new ConfigurableForm();

            int vpos = topmargin;

            f.AddLabelAndEntry("Efficiency", new Point(4,4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("efficiency", typeof(NumberBoxInt), "60", new Point(dataleft, 0), numberboxsize, "How far off the straight line route to allow. 100 means no deviation") { NumberBoxLongMinimum = 1 });
            f.AddOK(new Point(140, vpos+16), "OK", anchor: AnchorStyles.Right | AnchorStyles.Bottom);
            f.InstallStandardTriggers();
            f.Trigger += (name, text, obj) => { f.GetControl("OK").Enabled = f.IsAllValid(); };

            if (f.ShowDialogCentred(FindForm(), FindForm().Icon, "Neutron Router", closeicon: true) == DialogResult.OK)
            {
                EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();
                spanshjobname = sp.RequestNeutronRouter(textBox_From.Text, textBox_To.Text, (int)valueBox_Range.Value, f.GetInt("efficiency").Value);
                StartSpanshQueryOp(Spanshquerytype.Neutron);
            }
        }

        private void extButtonSpanshTradeRouter_Click(object sender, EventArgs e)
        {
            ConfigurableForm f = new ConfigurableForm();

            EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();
            var stationlist = sp.GetStations(textBox_From.Text, 0.25);
            var withmarket = stationlist?.Where(x => x.HasMarket && x.Market != null && x.Market.Count > 0).ToList();
            if (withmarket != null && withmarket.Count>0)
            {
                var stationnames = withmarket.Select(x => x.StationName).OrderBy(x=>x).ToList();

                int vpos = topmargin;

                var jumprange = DiscoveryForm.History.GetLast?.ShipInformation?.GetJumpRange() ?? 25;

                f.AddLabelAndEntry("Station", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("station", stationnames[0], new Point(dataleft, 0), comboboxsize, "Station name", stationnames));
                f.AddLabelAndEntry("Starting Capital", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("capital", typeof(NumberBoxLong), "1000", new Point(dataleft, 0), numberboxsize, "Starting capital") { NumberBoxLongMinimum = 100 });
                f.AddLabelAndEntry("Max Hop Distance", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("hopd", typeof(NumberBoxDouble), jumprange.ToString("N2"), new Point(dataleft, 0), numberboxsize, "Maximum distance you can jump") { NumberBoxLongMinimum = 1 });
                f.AddLabelAndEntry("Max Cargo", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("cargo", typeof(NumberBoxInt), "7", new Point(dataleft, 0), numberboxsize, "Maximum cargo you can carry") { NumberBoxLongMinimum = 1 });
                f.AddLabelAndEntry("Max Hops", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("hops", typeof(NumberBoxInt), "5", new Point(dataleft, 0), numberboxsize, "Maximum hops between stations") { NumberBoxLongMinimum = 1 });
                f.AddLabelAndEntry("Max Arrival distance", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("dls", typeof(NumberBoxInt), "1000000", new Point(dataleft, 0), numberboxsize, "Maximum arrival distance of station") { NumberBoxLongMinimum = 1 });
                f.AddLabelAndEntry("Max Market Age (Days)", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("mage", typeof(NumberBoxDouble), "30", new Point(dataleft, 0), numberboxsize, "Maximum age of the station data you accept") { NumberBoxDoubleMinimum = 0.01 });
                f.Add(ref vpos, 32, new ConfigurableForm.Entry("largepad", typeof(ExtCheckBox), "Require Large Pad", new Point(4, 0), checkboxsize, "Ship needs a large pad") { CheckBoxChecked = false, ContentAlign = ContentAlignment.MiddleRight });
                f.Add(ref vpos, 32, new ConfigurableForm.Entry("planetary", typeof(ExtCheckBox), "Allow Planetary", new Point(4, 0), checkboxsize, "Accept planetary ports") { CheckBoxChecked = false, ContentAlign = ContentAlignment.MiddleRight });
                f.Add(ref vpos, 32, new ConfigurableForm.Entry("prohibited", typeof(ExtCheckBox), "Allow Prohibited", new Point(4, 0), checkboxsize, "Allow prohibited commodities") { CheckBoxChecked = false, ContentAlign = ContentAlignment.MiddleRight });
                f.Add(ref vpos, 32, new ConfigurableForm.Entry("loop", typeof(ExtCheckBox), "Avoid Loops", new Point(4, 0), checkboxsize, "Don't loop back to previous station") { CheckBoxChecked = false, ContentAlign = ContentAlignment.MiddleRight });
                f.Add(ref vpos, 32, new ConfigurableForm.Entry("permit", typeof(ExtCheckBox), "Allow Permit Systems", new Point(4, 0), checkboxsize, "You have the permit to these systems") { CheckBoxChecked = false, ContentAlign = ContentAlignment.MiddleRight });
                f.AddOK(new Point(140, vpos + 16), "OK", anchor: AnchorStyles.Right | AnchorStyles.Bottom);
                f.InstallStandardTriggers();
                f.Trigger += (name, text, obj) => { f.GetControl("OK").Enabled = f.IsAllValid(); };

                if (f.ShowDialogCentred(FindForm(), FindForm().Icon, "Trade Router", closeicon: true) == DialogResult.OK)
                {
                    spanshjobname = sp.RequestTradeRouter(textBox_From.Text, f.Get("station"),
                        f.GetInt("hops").Value, f.GetDouble("hopd").Value, f.GetLong("capital").Value, f.GetInt("cargo").Value, f.GetInt("dls").Value, (int)(f.GetDouble("mage").Value * 86400),
                        f.GetBool("largepad").Value, f.GetBool("prohibited").Value, f.GetBool("planetary").Value, f.GetBool("loop").Value, f.GetBool("permit").Value);
                    StartSpanshQueryOp(Spanshquerytype.TradeRouter);
                }
            }
            else
            {
                MessageBoxTheme.Show(this.FindForm(), $"No stations found at {textBox_From.Text}", "Warning".TxID(EDTx.Warning), MessageBoxButtons.OK);
            }
        }

        private void extButtonFleetCarrier_Click(object sender, EventArgs e)
        {
            ConfigurableForm f = new ConfigurableForm();

            int vpos = topmargin;

            f.AddLabelAndEntry("Capacity Used", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("cap", typeof(NumberBoxInt), "0", new Point(dataleft, 0), numberboxsize, "Capacity in use from upper right corner of carrier management screen") { NumberBoxLongMinimum = 0 });
            f.Add(ref vpos, 32, new ConfigurableForm.Entry("tritium", typeof(ExtCheckBox), "Determine Tritium", new Point(4, 0), checkboxsize, "Calculate how much tritium is needed") { CheckBoxChecked = true, ContentAlign = ContentAlignment.MiddleRight });
            int addpos = vpos;
            f.Add(ref vpos, 32, new ConfigurableForm.Entry("add", typeof(ExtButton), "+ Stop", new Point(4, 0), numberboxsize, "Add a new stop"));
            f.AddOK(new Point(140, vpos + 16), "OK", anchor: AnchorStyles.Right | AnchorStyles.Bottom);
            f.InstallStandardTriggers();
            f.Trigger += (name, control, obj) =>
            {
                if ( control == "add")
                {
                    f.MoveControls(addpos-10, 32);
                    ExtTextBoxAutoComplete ac = new ExtTextBoxAutoComplete();
                    ac.SetAutoCompletor(SystemCache.ReturnSystemAutoCompleteList, true);
                    f.Add(ref addpos, 32, new ConfigurableForm.Entry(ac, "idest", "", new Point(4, 0), textboxsize, "Add a intermediate stop"));
                    f.UpdateDisplayAfterAddNewControls();
                }
                else
                    f.GetControl("OK").Enabled = f.IsAllValid();
            };

            if (f.ShowDialogCentred(FindForm(), FindForm().Icon, "Fleet Carrier Router", closeicon: true) == DialogResult.OK)
            {
                List<string> destlist = f.GetList("idest").Where(x=>x.Length>0).ToList();
                destlist.Add(textBox_To.Text);
                EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();
                spanshjobname = sp.RequestFleetCarrierRouter(textBox_From.Text, destlist, f.GetInt("cap").Value, f.GetBool("tritium").Value);
                StartSpanshQueryOp(Spanshquerytype.FleetCarrier);
            }
        }

        private void extButtonSpanshGalaxyPlotter_Click(object sender, EventArgs e)
        {
            ConfigurableForm f = new ConfigurableForm();

            ShipInformation si = DiscoveryForm.History.GetLast?.ShipInformation;

            if (si != null)
            {
                int vpos = topmargin;

                f.AddLabelAndEntry("Cargo", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("cargo", typeof(NumberBoxInt), "0", new Point(dataleft, 0), numberboxsize, "Amount of cargo to carry") { NumberBoxLongMinimum = 0 });
                f.Add(ref vpos, 32, new ConfigurableForm.Entry("asc", typeof(ExtCheckBox), "Already supercharged", new Point(4, 0), checkboxsize, "Ship already is neutron boosted") { CheckBoxChecked = false, ContentAlign = ContentAlignment.MiddleRight });
                f.Add(ref vpos, 32, new ConfigurableForm.Entry("usc", typeof(ExtCheckBox), "Use supercharge", new Point(4, 0), checkboxsize, "Use neutron boosts") { CheckBoxChecked = true, ContentAlign = ContentAlignment.MiddleRight });
                f.Add(ref vpos, 32, new ConfigurableForm.Entry("fsd", typeof(ExtCheckBox), "Use FSD Injections", new Point(4, 0), checkboxsize, "Use FSD Injections to speed travel") { CheckBoxChecked = false, ContentAlign = ContentAlignment.MiddleRight });
                f.Add(ref vpos, 32, new ConfigurableForm.Entry("ess", typeof(ExtCheckBox), "Exclude secondary stars", new Point(4, 0), checkboxsize, "Exclude secondary stars from consideration for neutron boosting/scooping") { CheckBoxChecked = false, ContentAlign = ContentAlignment.MiddleRight });
                f.AddOK(new Point(140, vpos + 16), "OK", anchor: AnchorStyles.Right | AnchorStyles.Bottom);
                f.InstallStandardTriggers();
                f.Trigger += (name, text, obj) => { f.GetControl("OK").Enabled = f.IsAllValid(); };

                if (f.ShowDialogCentred(FindForm(), FindForm().Icon, "Galaxy Plotter", closeicon: true) == DialogResult.OK)
                {
                    EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();
                    spanshjobname = sp.RequestGalaxyPlotter(textBox_From.Text, textBox_To.Text, f.GetInt("cargo").Value, f.GetBool("asc").Value, f.GetBool("usc").Value, f.GetBool("fsd").Value, f.GetBool("ess").Value, si);
                    StartSpanshQueryOp(Spanshquerytype.GalaxyPlotter);
                }
            }
        }

        private void extButtonExoMastery_Click(object sender, EventArgs e)
        {
            ConfigurableForm f = new ConfigurableForm();

            int vpos = topmargin;

            var jumprange = DiscoveryForm.History.GetLast?.ShipInformation?.GetJumpRange(0) ?? 25;

            f.AddLabelAndEntry("Jump Range", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("jr", typeof(NumberBoxDouble), jumprange.ToString("N1"), new Point(dataleft, 0), numberboxsize, "Jump range of ship, based on no cargo but max fuel") { NumberBoxDoubleMinimum = 3});
            f.AddLabelAndEntry("Search radius", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("radius", typeof(NumberBoxInt), "25", new Point(dataleft, 0), numberboxsize, "Search radius along path to search for worlds") { NumberBoxLongMinimum = 10 });
            f.AddLabelAndEntry("Max Systems", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("maxsystems", typeof(NumberBoxInt), "100", new Point(dataleft, 0), numberboxsize, "Maximum systems to route through") { NumberBoxLongMinimum = 1 });
            f.Add(ref vpos, 32, new ConfigurableForm.Entry("loop", typeof(ExtCheckBox), "Return to start", new Point(4, 0), checkboxsize, "Return to start system for route") { CheckBoxChecked = true, ContentAlign = ContentAlignment.MiddleRight });
            f.AddLabelAndEntry("Max LS", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("maxls", typeof(NumberBoxInt), "1000000" , new Point(dataleft, 0), numberboxsize, "Maximum LS from arrival to consider") { NumberBoxLongMinimum = 10 });
            f.AddLabelAndEntry("Min Value", new Point(4, 4), ref vpos, 32, labelsize, new ConfigurableForm.Entry("minv", typeof(NumberBoxInt), "10000000", new Point(dataleft, 0), numberboxsize, "Minimum value of scans") { NumberBoxLongMinimum = 100 });
            f.AddOK(new Point(140, vpos + 16), "OK", anchor: AnchorStyles.Right | AnchorStyles.Bottom);
            f.InstallStandardTriggers();
            f.Trigger += (name, text, obj) => { f.GetControl("OK").Enabled = f.IsAllValid(); };

            if (f.ShowDialogCentred(FindForm(), FindForm().Icon,"Expressway to Exomastery", closeicon: true) == DialogResult.OK)
            {
                EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();

                spanshjobname = sp.RequestExomastery(textBox_From.Text, textBox_To.Text, f.GetDouble("jr").Value,
                                                    f.GetInt("radius").Value, f.GetInt("maxsystems").Value,
                                                    f.GetBool("loop").Value, f.GetInt("maxls").Value, f.GetInt("minv").Value);
                StartSpanshQueryOp(Spanshquerytype.ExoMastery);

            }

        }


        private void StartSpanshQueryOp(Spanshquerytype qt)
        {
            if (spanshjobname != null)
            {
                if (spanshjobname.StartsWith("!"))
                {
                    MessageBoxTheme.Show(this.FindForm(), $"Spansh returned error: {spanshjobname.Substring(1)}", "Warning".TxID(EDTx.Warning), MessageBoxButtons.OK);
                }
                else
                {
                    spanshquerytype = qt;
                    dataGridViewRoute.Rows.Clear();
                    EnableOutputButtons();
                    EnableRouteButtons();
                    waitforspanshresulttimer.Interval = 2000;
                    waitforspanshresulttimer.Start();
                }
            }
            else
            {
                MessageBoxTheme.Show(this.FindForm(), $"Spansh failed to return a job id. Try again!", "Warning".TxID(EDTx.Warning), MessageBoxButtons.OK);
            }
        }

        private void Waitforspanshresulttimer_Tick(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Spansh job tick {Environment.TickCount} for {spanshjobname}");
            EliteDangerousCore.Spansh.SpanshClass sp = new EliteDangerousCore.Spansh.SpanshClass();

            string errstring;

            try
            {
                var res = spanshquerytype == Spanshquerytype.TradeRouter ? sp.TryGetTradeRouter(spanshjobname) :
                            spanshquerytype == Spanshquerytype.FleetCarrier ? sp.TryGetFleetCarrierRouter(spanshjobname) :
                            spanshquerytype == Spanshquerytype.GalaxyPlotter ? sp.TryGetGalaxyPlotter(spanshjobname) :
                            spanshquerytype == Spanshquerytype.ExoMastery ? sp.TryGetExomastery(spanshjobname) :
                          spanshquerytype == Spanshquerytype.Neutron ? sp.TryGetNeutronRouter(spanshjobname) : sp.TryGetRoadToRichesAmmonia(spanshjobname);

                if (res.Item1 != null)          // error return
                {
                    errstring = res.Item1;
                }
                else if (res.Item2 == null)      // if not ready, no error
                {
                    waitforspanshresulttimer.Stop();
                    waitforspanshresulttimer.Interval = waitforspanshresulttimer.Interval < 8000 ? waitforspanshresulttimer.Interval * 2 : 8000;
                    waitforspanshresulttimer.Start();
                    return;
                }
                else
                {
                    waitforspanshresulttimer.Stop();

                    ISystem prev = null;
                    foreach (ISystem system in res.Item2)
                    {
                        DataGridViewRow rw = dataGridViewRoute.RowTemplate.Clone() as DataGridViewRow;
                        rw.CreateCells(dataGridViewRoute,
                                system.Name + ":" + system.SystemAddress,
                                system.Tag as string ?? "",
                                prev != null ? system.Distance(prev).ToString("0.#") : "",
                                system.X.ToString("0.####"),
                                system.Y.ToString("0.####"),
                                system.Z.ToString("0.####"),
                                "",
                                "",
                                ""
                                );

                        rw.Tag = system;       // may be null if waypoint or not a system
                        rw.Cells[0].Tag = system.Name;    // write the name of the system into the cells'tag for copying
                        dataGridViewRoute.Rows.Add(rw);
                        prev = system;
                    }

                    routeSystems = res.Item2;
                    EnableOutputButtons(res.Item2.Count > 0);
                    EnableRouteButtonsIfValid();
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Spansh result exception {ex}");
                errstring = ex.Message;
            }

            waitforspanshresulttimer.Stop();
            MessageBoxTheme.Show(this.FindForm(), $"Spansh returned: {errstring}", "Warning".TxID(EDTx.Warning), MessageBoxButtons.OK);
            System.Diagnostics.Debug.WriteLine($"Spansh failed with {errstring}");
            EnableRouteButtonsIfValid();
        }

        #endregion

    }
}
