﻿using JocysCom.ClassLibrary.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Controls
{
	public partial class CloudUserControl : UserControl
	{

		public class BindingListInvoked<T> : BindingList<T>
		{
			public BindingListInvoked(ISynchronizeInvoke synchronizingObject)
			{
				_SynchronizingObject = synchronizingObject;
			}

			public ISynchronizeInvoke _SynchronizingObject { get; set; }

			protected override void OnListChanged(ListChangedEventArgs e)
			{
				var result = _SynchronizingObject.BeginInvoke((MethodInvoker)delegate ()
				{
					base.OnListChanged(e);
				}, new object[] { });
				_SynchronizingObject.EndInvoke(result);
			}
		}


		public CloudUserControl()
		{
			InitializeComponent();
			JocysCom.ClassLibrary.Controls.ControlsHelper.ApplyBorderStyle(TasksDataGridView);
			EngineHelper.EnableDoubleBuffering(TasksDataGridView);
			queueTimer = new JocysCom.ClassLibrary.Threading.QueueTimer<CloudItem>(0, 5000);
			queueTimer.DoAction = DoAction;
			queueTimer.Queue = new BindingListInvoked<CloudItem>(this);
			queueTimer.Queue.ListChanged += Data_ListChanged;
			//queueTimer.SynchronizingObject = this;
			TasksDataGridView.AutoGenerateColumns = false;
			TasksDataGridView.DataSource = queueTimer.Queue;
			// Force to create handle.
			var handle = this.Handle;
			QueueMonitorTimer.Start();
		}

		private void Data_ListChanged(object sender, ListChangedEventArgs e)
		{
			if (e.ListChangedType == ListChangedType.ItemAdded || e.ListChangedType == ListChangedType.ItemDeleted)
			{
				var f = MainForm.Current;
				if (f == null) return;
				var count = queueTimer.Queue.Count;
				AppHelper.SetText(f.CloudMessagesLabel, "M: {0}", count);
			}
		}

		JocysCom.ClassLibrary.Threading.QueueTimer<CloudItem> queueTimer;

		public void Add<T>(CloudAction action, T[] items = null)
		{
			BeginInvoke((MethodInvoker)delegate ()
			{
				var allow = MainForm.Current.OptionsPanel.InternetAutoSaveCheckBox.Checked;
				if (!allow)
				{
					return;
				}
				for (int i = 0; i < items.Length; i++)
				{
					var item = new CloudItem()
					{
						Action = action,
						Date = DateTime.Now,
						Item = items[i],
						State = CloudState.None,
					};
					queueTimer.DoActionNow(item);
				}
			});
		}

		void DoAction(CloudItem item)
		{
			MainForm.Current.AddTask(TaskName.SaveToCloud);
			Exception error = null;
			try
			{
				var ws = new WebServiceClient();
				ws.Url = SettingsManager.Options.InternetDatabaseUrl;
				CloudMessage result = null;
				// Add security.
				var o = SettingsManager.Options;
				var command = CloudHelper.NewMessage(item.Action, o.UserRsaPublicKey, o.CloudRsaPublicKey, o.Username, o.Password);
				command.Values.Add(CloudKey.HashedDiskId, o.HashedDiskId, true);
				//// Add secure credentials.
				//var rsa = new JocysCom.ClassLibrary.Security.Encryption("Cloud");
				//if (string.IsNullOrEmpty(rsa.RsaPublicKeyValue))
				//{
				//	var username = rsa.RsaEncrypt("username");
				//	var password = rsa.RsaEncrypt("password");
				//	ws.SetCredentials(username, password);
				//}
				// Add changes.
				if (item.Item.GetType() == typeof(UserGame))
				{
					command.UserGames = new List<UserGame>() { (UserGame)item.Item };
				}
				else if (item.Item.GetType() == typeof(UserDevice))
				{
					command.UserControllers = new List<UserDevice>() { (UserDevice)item.Item };
				}
				result = ws.Execute(command);
				if (result.ErrorCode > 0)
				{
					queueTimer.ChangeSleepInterval(5 * 60 * 1000);
					error = new Exception(result.ErrorMessage);
				}
				ws.Dispose();
			}
			catch (Exception ex)
			{
				error = ex;
			}
			MainForm.Current.RemoveTask(TaskName.SaveToCloud);
			if (error == null)
			{
				MainForm.Current.SetHeaderBody(MessageBoxIcon.Information);
			}
			else
			{
				var body = string.Format("Cloud Error: {0}", error.Message);
				if (error.InnerException != null) body += "\r\n" + error.InnerException.Message;
				MainForm.Current.SetHeaderBody(MessageBoxIcon.Error, body);
			}
		}

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			if (queueTimer != null)
			{
				queueTimer.Dispose();
				queueTimer = null;
			}

			base.Dispose(disposing);
		}

		/// <summary>
		/// Reupload all data to the cloud.
		/// </summary>
		private void UploadToCloudButton_Click(object sender, EventArgs e)
		{
			queueTimer.Queue.Clear();
			//queueTimer.ChangeSleepInterval(1000);
			// For test purposes take only one record for processing.
			var allControllers = SettingsManager.UserDevices.Items.Take(1).ToArray();
			Add(CloudAction.Insert, allControllers);
			//Add(CloudAction.Insert, allControllers);
			//var allGames = SettingsManager.UserGames.Items.ToArray();
			//Add(CloudAction.Insert, allGames);
		}

		/// <summary>
		/// Download all data from the cloud.
		/// </summary>
		private void DownloadFromCloudButton_Click(object sender, EventArgs e)
		{
			var device = new UserDevice();
			Add(CloudAction.Select, new UserDevice[] { device });
		}

		private void QueueMonitorTimer_Tick(object sender, EventArgs e)
		{
			var nextRunTime = queueTimer.NextRunTime;
			TimeSpan remains = new TimeSpan();
			if (nextRunTime.Ticks > 0)
			{
				remains = nextRunTime.Subtract(DateTime.Now);
			}
			var time = string.Format("{0} Next Run: {1:00}:{2:00.000}", queueTimer.IsRunning ? "↻" : " ",
				remains.Minutes, remains.Seconds + (remains.Milliseconds / 1000m));
			AppHelper.SetText(NextRunLabel, time);
		}

		private void DeleteButton_Click(object sender, EventArgs e)
		{
			queueTimer.Queue.Clear();
		}
	}
}
