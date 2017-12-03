﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using x360ce.Engine.Data;
using x360ce.Engine;
using JocysCom.ClassLibrary.Controls;

namespace x360ce.App.Controls
{
    public partial class SummariesGridUserControl : UserControl
    {
        public SummariesGridUserControl()
        {
            InitializeComponent();
            JocysCom.ClassLibrary.Controls.ControlsHelper.ApplyBorderStyle(SummariesDataGridView);
            EngineHelper.EnableDoubleBuffering(SummariesDataGridView);
            SummariesDataGridView.AutoGenerateColumns = false;
        }

        public BaseForm _ParentForm;

        public void InitPanel()
        {
            SettingsManager.Summaries.Items.ListChanged += Summaries_ListChanged;
            SummariesDataGridView.DataSource = SettingsManager.Summaries.Items;
            UpdateControlsFromSummaries();
        }

        public void UnInitPanel()
        {
            SettingsManager.Summaries.Items.ListChanged -= Summaries_ListChanged;
            SummariesDataGridView.DataSource = null;
        }

        void UpdateControlsFromSummaries()
        {
            var count = SettingsManager.Summaries.Items.Count;
            // Allow refresh summaries.
            ControlsHelper.SetEnabled(SummariesRefreshButton, count > 0);
        }

        void Summaries_ListChanged(object sender, ListChangedEventArgs e)
        {
            UpdateControlsFromSummaries();
        }

        private void SummariesRefreshButton_Click(object sender, EventArgs e)
        {
            RefreshSummariesGrid();
        }

        void SummariesDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var grid = (DataGridView)sender;
            var item = (Summary)grid.Rows[e.RowIndex].DataBoundItem;
            if (e.ColumnIndex == grid.Columns[SummariesSidColumn.Name].Index)
            {
                e.Value = EngineHelper.GetID(item.PadSettingChecksum);
            }
        }


        public void RefreshSummariesGrid()
        {
            //mainForm.LoadingCircle = true;

            _ParentForm.AddTask(TaskName.SearchSummaries);
            SummariesRefreshButton.Enabled = false;
            var sp = new List<SearchParameter>();
            FillSearchParameterWithProducts(sp);
            SettingsManager.Current.FillSearchParameterWithFiles(sp);
            var ws = new WebServiceClient();
            ws.Url = MainForm.Current.OptionsPanel.InternetDatabaseUrlComboBox.Text;
            ws.SearchSettingsCompleted += ws_SearchSummariesCompleted;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate (object state)
            {
                ws.SearchSettingsAsync(sp.ToArray());
            });
        }


        public void FillSearchParameterWithProducts(List<SearchParameter> sp)
        {
            // Select user devices as parameters to search.
            var userDevices = SettingsManager.Settings.Items
                .Select(x => x.ProductGuid).Distinct()
                // Do not add empty records.
                .Where(x => x != Guid.Empty)
                .Select(x => new SearchParameter() { ProductGuid = x })
                .ToArray();
            sp.AddRange(userDevices);
        }

        void ws_SearchSummariesCompleted(object sender, ResultEventArgs e)
        {
            // Make sure method is executed on the same thread as this control.
            if (InvokeRequired)
            {
                var method = new EventHandler<ResultEventArgs>(ws_SearchSummariesCompleted);
                BeginInvoke(method, new object[] { sender, e });
                return;
            }
            // Detach event handler so resource could be released.
            var ws = (WebServiceClient)sender;
            ws.SearchSettingsCompleted -= ws_SearchSummariesCompleted;
            if (e.Error != null)
            {
                var error = e.Error.Message;
                if (e.Error.InnerException != null) error += "\r\n" + e.Error.InnerException.Message;
                _ParentForm.SetHeaderError(error);
            }
            else if (e.Result == null)
            {
                _ParentForm.SetHeaderBody("No default settings received.");
            }
            else
            {
                var result = (SearchResult)e.Result;
                // Reorder summaries.
                result.Summaries = result.Summaries.OrderBy(x => x.ProductName).ThenBy(x => x.FileName).ThenBy(x => x.FileProductName).ThenByDescending(x => x.Users).ToArray();
                AppHelper.UpdateList(result.Summaries, SettingsManager.Summaries.Items);
                SettingsManager.Current.UpsertPadSettings(result.PadSettings);
                var summariesCount = (result.Summaries == null) ? 0 : result.Summaries.Length;
                var padSettingsCount = (result.PadSettings == null) ? 0 : result.PadSettings.Length;
                _ParentForm.SetHeaderBody("{0} default settings and {0} PAD settings received.", summariesCount, padSettingsCount);
            }
            _ParentForm.RemoveTask(TaskName.SearchSummaries);
            SummariesRefreshButton.Enabled = true;
        }

    }
}
