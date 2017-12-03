﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using x360ce.Engine;
using System.Reflection;
using JocysCom.ClassLibrary.Runtime;

namespace x360ce.App.Controls
{
	public partial class ProgramsGridUserControl : UserControl
	{
		public ProgramsGridUserControl()
		{
			InitializeComponent();
			JocysCom.ClassLibrary.Controls.ControlsHelper.ApplyBorderStyle(ProgramsDataGridView);
			EngineHelper.EnableDoubleBuffering(ProgramsDataGridView);
			ProgramsDataGridView.AutoGenerateColumns = false;
			ProgramImageColumn.Visible = false;
		}

		public void InitPanel()
		{
			// Configure Programs.
			SettingsManager.Programs.Items.ListChanged += Programs_ListChanged;
			// WORKAROUND: Remove SelectionChanged event.
			ProgramsDataGridView.SelectionChanged -= ProgramsDataGridView_SelectionChanged;
			ProgramsDataGridView.DataSource = SettingsManager.Programs.Items;
			var handle = this.Handle;
			// WORKAROUND: Use BeginInvoke to prevent SelectionChanged firing multiple times.
			BeginInvoke((MethodInvoker)delegate ()
			{
				ProgramsDataGridView.SelectionChanged += ProgramsDataGridView_SelectionChanged;
				ProgramsDataGridView_SelectionChanged(ProgramsDataGridView, new EventArgs());
			});
			UpdateControlsFromPrograms();
		}

		public void UnInitPanel()
		{
			ProgramsDataGridView.SelectionChanged -= ProgramsDataGridView_SelectionChanged;
			SettingsManager.Programs.Items.ListChanged -= Programs_ListChanged;
			ProgramsDataGridView.DataSource = null;
		}

		void Programs_ListChanged(object sender, ListChangedEventArgs e)
		{
			UpdateControlsFromPrograms();
		}

		void UpdateControlsFromPrograms()
		{
			var enabled = SettingsManager.Programs.Items.Count > 0;
			if (ExportProgramsButton.Enabled != enabled) ExportProgramsButton.Enabled = enabled;
		}

		void LoadProgramsFromLocalGdbFile()
		{
			var path = GameDatabaseManager.Current.GdbFile.FullName;
			var ini = new Ini(path);
			var sections = ini.GetSections();
			foreach (var section in sections)
			{
				var program = SettingsManager.Programs.Items.FirstOrDefault(x => x.FileName.ToLower() == section.ToLower());
				if (program == null)
				{
					program = new Engine.Data.Program();
					program.FileName = section;
					program.HookMask = 0x00000002;
					program.XInputMask = 0x00000004;
					SettingsManager.Programs.Items.Add(program);
				}
				program.FileProductName = ini.GetValue(section, "Name", section);
				int hookMask;
				var hookMaskValue = ini.GetValue(section, "HookMask", "0x00000002");
				if (int.TryParse(hookMaskValue, out hookMask))
				{
					program.HookMask = hookMask;
				}
			}
		}

		void SetProgramsSelection()
		{
			// List can't be empty, so return.
			// Issue: When DataSource is set then DataGridView fires the selectionChanged 3 times & it selects the first row. 
			if (ProgramsDataGridView.SelectedRows.Count == 0) return;
			var row = ProgramsDataGridView.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
			var item = (x360ce.Engine.Data.Program)row.DataBoundItem;
			GameDefaultDetailsControl.SetMask(false,
				(HookMask)item.HookMask,
				DInputMask.None,
				(XInputMask)item.XInputMask,
				MapToMask.None,
				item.FileName, (int)ProcessorArchitecture.None);
		}

		private void RefreshProgramsButton_Click(object sender, EventArgs e)
		{
			RefreshProgramsListFromCloud();
		}

		private void ProgramsDataGridView_SelectionChanged(object sender, EventArgs e)
		{
			SetProgramsSelection();
		}

		private void ImportProgramsButton_Click(object sender, EventArgs e)
		{
			ImportPrograms();
		}

		private void ExportProgramsButton_Click(object sender, EventArgs e)
		{
			ExportPrograms();
		}

		private void DeleteProgramsButton_Click(object sender, EventArgs e)
		{
			DeleteSelectedPrograms();
		}


		/// <summary>
		/// Import Programs (Default Game Settings) from external file.
		/// </summary>
		void ImportPrograms()
		{
			var dialog = ImportOpenFileDialog;
			dialog.DefaultExt = "*.xml";
			dialog.Filter = "Game Settings (*.xml;*.xml.gz;*.ini;*.gdb)|*.xml;*.xml.gz;*.ini;*.gdb|All files (*.*)|*.*";
			dialog.FilterIndex = 1;
			dialog.RestoreDirectory = true;
			if (string.IsNullOrEmpty(dialog.FileName)) dialog.FileName = "x360ce_Games";
			if (string.IsNullOrEmpty(dialog.InitialDirectory)) dialog.InitialDirectory = GameDatabaseManager.Current.GdbFile.Directory.FullName;
			dialog.Title = "Import Games Settings File";
			var result = dialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				List<x360ce.Engine.Data.Program> programs;
				if (dialog.FileName.EndsWith(".gz"))
				{
					var compressedBytes = System.IO.File.ReadAllBytes(dialog.FileName);
					var bytes = EngineHelper.Decompress(compressedBytes);
					var xml = System.Text.Encoding.UTF8.GetString(bytes);
					programs = Serializer.DeserializeFromXmlString<List<x360ce.Engine.Data.Program>>(xml, System.Text.Encoding.UTF8);
				}
				else if (dialog.FileName.EndsWith(".ini") || dialog.FileName.EndsWith(".gdb"))
				{
					programs = GameDatabaseManager.GetPrograms(dialog.FileName);
				}
				else
				{
					programs = Serializer.DeserializeFromXmlFile<List<x360ce.Engine.Data.Program>>(dialog.FileName);
				}
				ImportAndBindItems(programs);
			}

		}

		/// <summary>
		/// Export Programs (Default Game Settings) to external file.
		/// </summary>
		void ExportPrograms()
		{
			var dialog = ExportSaveFileDialog;
			dialog.DefaultExt = "*.xml";
			dialog.Filter = "Game Settings (*.xml)|*.xml|Compressed Game Settings (*.xml.gz)|*.gz|All files (*.*)|*.*";
			dialog.FilterIndex = 1;
			dialog.RestoreDirectory = true;
			if (string.IsNullOrEmpty(dialog.FileName)) dialog.FileName = "x360ce_Games";
			if (string.IsNullOrEmpty(dialog.InitialDirectory)) dialog.InitialDirectory = GameDatabaseManager.Current.GdbFile.Directory.FullName;
			dialog.Title = "Export Games Settings File";
			var result = dialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				var programs = SettingsManager.Programs.Items.ToList();
				foreach (var item in programs)
				{
					item.EntityKey = null;
					item.FileProductName = EngineHelper.FixName(item.FileProductName, item.FileName);
				}
				if (dialog.FileName.EndsWith(".gz"))
				{
					var s = Serializer.SerializeToXmlString(programs, System.Text.Encoding.UTF8, true);
					var bytes = System.Text.Encoding.UTF8.GetBytes(s);
					var compressedBytes = EngineHelper.Compress(bytes);
					System.IO.File.WriteAllBytes(dialog.FileName, compressedBytes);
				}
				else
				{
					Serializer.SerializeToXmlFile(programs, dialog.FileName, System.Text.Encoding.UTF8, true);
				}
			}

		}

		/// <summary>
		/// Delete selected Programs (Default Game Settings) from current settings.
		/// </summary>
		void DeleteSelectedPrograms()
		{
			var grid = ProgramsDataGridView;
			var selection = JocysCom.ClassLibrary.Controls.ControlsHelper.GetSelection<string>(grid, "FileName");
			var itemsToDelete = SettingsManager.Programs.Items.Where(x => selection.Contains(x.FileName)).ToArray();
			MessageBoxForm form = new MessageBoxForm();
			form.StartPosition = FormStartPosition.CenterParent;
			string message;
			if (itemsToDelete.Length == 1)
			{
				var item = itemsToDelete[0];
				message = string.Format("Are you sure you want to delete default settings for?\r\n\r\n\tFile Name: {0}\r\n\tProduct Name: {1}",
					item.FileName,
					item.FileProductName);
			}
			else
			{
				message = string.Format("Delete {0} default setting(s)?", itemsToDelete.Length);
			}
			var result = form.ShowForm(message, "Delete", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
			if (result == DialogResult.OK)
			{
				foreach (var item in itemsToDelete)
				{
					SettingsManager.Programs.Items.Remove(item);
				}
				SettingsManager.Save();
			}
		}

		/// <summary>
		/// Merge supplied list of programs with current settings.
		/// </summary>
		/// <param name="items">Programs list to merge.</param>
		void ImportAndBindItems(IList<Engine.Data.Program> items)
		{
			var grid = ProgramsDataGridView;
			var key = "FileName";
			var list = SettingsManager.Programs.Items;
			var selection = JocysCom.ClassLibrary.Controls.ControlsHelper.GetSelection<string>(grid, key);
			var newItems = items.ToArray();
			grid.DataSource = null;
			foreach (var newItem in newItems)
			{
				// Try to find existing item inside the list.
				var existingItems = list.Where(x => x.FileName.ToLower() == newItem.FileName.ToLower()).ToArray();
				// Remove existing items.
				for (int i = 0; i < existingItems.Length; i++)
				{
					list.Remove(existingItems[i]);
				}
				// Fix product name.
				var fixedProductName = EngineHelper.FixName(newItem.FileProductName, newItem.FileName);
				newItem.FileProductName = fixedProductName;
				// If new item is missing XInputMask setting then...
				if (newItem.XInputMask == (int)XInputMask.None)
				{
					// Assign default.
					newItem.XInputMask = (int)XInputMask.XInput13_x86;
				}
				// Add new one.
				list.Add(newItem);
			}
			MainForm.Current.SetHeaderBody(
				MessageBoxIcon.Information,
				"{0: yyyy-MM-dd HH:mm:ss}: '{1}' {2}(s) loaded.",
				DateTime.Now, items.Count(), typeof(Engine.Data.Program).Name
			);
			grid.DataSource = list;
			JocysCom.ClassLibrary.Controls.ControlsHelper.RestoreSelection(grid, key, selection);
			SettingsManager.Save(true);
		}

		/// <summary>
		/// Refresh Programs (Default Game Settings) from the cloud.
		/// </summary>
		void RefreshProgramsListFromCloud()
		{
			var ws = new WebServiceClient();
			ws.Url = SettingsManager.Options.InternetDatabaseUrl;
			var enabled = EnabledState.None;
			var checkState = MainForm.Current.OptionsPanel.IncludeEnabledCheckBox.CheckState;
			if (checkState == CheckState.Checked) enabled = EnabledState.Enabled;
			if (checkState == CheckState.Unchecked) enabled = EnabledState.Disabled;
			var minInstances = (int)MainForm.Current.OptionsPanel.MinimumInstanceCountNumericUpDown.Value;
			ws.GetProgramsCompleted += ProgramsWebServiceClient_GetProgramsCompleted;
			System.Threading.ThreadPool.QueueUserWorkItem(delegate (object state)
			{
				ws.GetProgramsAsync(enabled, minInstances);
			});
		}

		void ProgramsWebServiceClient_GetProgramsCompleted(object sender, ResultEventArgs e)
		{
			// Make sure method is executed on the same thread as this control.
			BeginInvoke((MethodInvoker)delegate ()
			{
				MainForm.Current.AddTask(TaskName.GetPrograms);
				if (e.Error != null)
				{
					var error = e.Error.Message;
					if (e.Error.InnerException != null) error += "\r\n" + e.Error.InnerException.Message;
					MainForm.Current.SetHeaderBody(MessageBoxIcon.Error, error);
				}
				else if (e.Result == null)
				{
					MainForm.Current.SetHeaderBody(MessageBoxIcon.Error, "No results were returned by the web service!");
				}
				else
				{
					var result = (List<x360ce.Engine.Data.Program>)e.Result;
					ImportAndBindItems(result);
				}
				MainForm.Current.RemoveTask(TaskName.GetPrograms);
			});
		}

		private void ProgramsDataGridView_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete) DeleteSelectedPrograms();
			else if (e.KeyCode == Keys.Insert) ImportPrograms();
		}

		private void GameDefaultDetailsControl_Load(object sender, EventArgs e)
		{
			InitPanel();
		}
	}
}
