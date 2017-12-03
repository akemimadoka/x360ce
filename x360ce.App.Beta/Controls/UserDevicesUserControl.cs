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

namespace x360ce.App.Controls
{
	public partial class UserDevicesUserControl : UserControl
	{
		public UserDevicesUserControl()
		{
			InitializeComponent();
			JocysCom.ClassLibrary.Controls.ControlsHelper.ApplyBorderStyle(ControllersDataGridView);
			EngineHelper.EnableDoubleBuffering(ControllersDataGridView);
		}

		private void ControllersUserControl_Load(object sender, EventArgs e)
		{
			UpdateButtons();
			ControllersDataGridView.AutoGenerateColumns = false;
			// WORKAROUND: Remove SelectionChanged event.
			ControllersDataGridView.SelectionChanged -= ControllersDataGridView_SelectionChanged;
			ControllersDataGridView.DataSource = SettingsManager.UserDevices.Items;
			// WORKAROUND: Use BeginInvoke to prevent SelectionChanged firing multiple times.
			BeginInvoke((MethodInvoker)delegate ()
			{
				ControllersDataGridView.SelectionChanged += ControllersDataGridView_SelectionChanged;
				ControllersDataGridView_SelectionChanged(ControllersDataGridView, new EventArgs());
			});
		}

		private void ControllersDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
		{
			var grid = (DataGridView)sender;
			var item = ((UserDevice)grid.Rows[e.RowIndex].DataBoundItem);
			if (e.ColumnIndex == grid.Columns[IsOnlineColumn.Name].Index)
			{
				e.Value = item.IsOnline
					? Properties.Resources.bullet_square_glass_green
					: Properties.Resources.bullet_square_glass_grey;
			}
		}

		public UserDevice[] GetSelected()
		{
			var grid = ControllersDataGridView;
			var items = grid.SelectedRows.Cast<DataGridViewRow>().Select(x => (UserDevice)x.DataBoundItem).ToArray();
			return items;
		}

		private void RefreshButton_Click(object sender, EventArgs e)
		{
			ControllersDataGridView.Invalidate();
		}

		private void ControllerDeleteButton_Click(object sender, EventArgs e)
		{
			var items = GetSelected();
			foreach (var item in items)
			{
				SettingsManager.UserDevices.Items.Remove(item);
			}
			MainForm.Current.CloudPanel.Add(CloudAction.Delete, items, true);
		}

		private void ControllersDataGridView_SelectionChanged(object sender, EventArgs e)
		{
			UpdateButtons();
		}

		void UpdateButtons()
		{
			var grid = ControllersDataGridView;
			ControllerDeleteButton.Enabled = grid.SelectedRows.Count > 0;
		}

	}
}
