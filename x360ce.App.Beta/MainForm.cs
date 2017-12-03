﻿using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Windows.Forms;
using x360ce.App.Controls;
using x360ce.Engine;
using x360ce.Engine.Win32;
using x360ce.App.Properties;
using System.ComponentModel;
using JocysCom.ClassLibrary.IO;
using x360ce.Engine.Data;
using System.Text;

namespace x360ce.App
{
	public partial class MainForm : BaseForm
	{
		public MainForm()
		{
			InitializeComponent();
			InitMinimize();
		}

		public DInput.DInputHelper DHelper;

		public static MainForm Current { get; set; }


		public int oldIndex;

		public int ControllerIndex
		{
			get
			{
				int newIndex = -1;
				if (MainTabControl.SelectedTab == Pad1TabPage) newIndex = 0;
				if (MainTabControl.SelectedTab == Pad2TabPage) newIndex = 1;
				if (MainTabControl.SelectedTab == Pad3TabPage) newIndex = 2;
				if (MainTabControl.SelectedTab == Pad4TabPage) newIndex = 3;
				return newIndex;
			}
			set
			{
				switch (value)
				{
					case 0: MainTabControl.SelectedTab = Pad1TabPage; break;
					case 1: MainTabControl.SelectedTab = Pad2TabPage; break;
					case 2: MainTabControl.SelectedTab = Pad3TabPage; break;
					case 3: MainTabControl.SelectedTab = Pad4TabPage; break;
				}
			}
		}

		public AboutControl ControlAbout;
		public PadControl[] PadControls;
		public TabPage[] ControlPages;

		/// <summary>
		/// Settings timer will be used to delay applying settings, which will heavy load application, as long as user is changing them.
		/// </summary>
		public System.Timers.Timer SettingsTimer;

		public System.Timers.Timer UpdateTimer;

		public System.Timers.Timer CleanStatusTimer;
		public int DefaultPoolingInterval = 50;

		public Controller[] XiControllers = new Controller[4];

		void MainForm_Load(object sender, EventArgs e)
		{
			if (IsDesignMode) return;
			DHelper = new DInput.DInputHelper();
			if (WindowState != FormWindowState.Minimized)
			{
				EnableFormUpdates(true);
			}
			SettingsGridPanel._ParentForm = this;
			SettingsGridPanel.SettingsDataGridView.MultiSelect = true;
			SettingsGridPanel.InitPanel();
			// NotifySettingsChange will be called on event suspention and resume.
			SettingsManager.Current.NotifySettingsStatus = NotifySettingsStatus;
			// NotifySettingsChange will be called on setting changes.
			SettingsManager.Current.NotifySettingsChange = NotifySettingsChange;
			SettingsManager.Settings.Load();
			SettingsManager.Summaries.Load();
			SettingsManager.Summaries.Items.ListChanged += Summaries_ListChanged;
			// Make sure that data will be filtered before loading.
			// Note: Make sure to load Programs before Games.
			SettingsManager.Programs.FilterList = Programs_FilterList;
			SettingsManager.Programs.Load();
			// Make sure that data will be filtered before loading.
			SettingsManager.UserGames.FilterList = Games_FilterList;
			SettingsManager.UserGames.Load();
			SettingsManager.Presets.Load();
			SettingsManager.PadSettings.Load();
			SettingsManager.UserDevices.Load();
			SettingsManager.UserInstances.Load();
			SettingsManager.UserComputers.Load();
			XInputMaskScanner.FileInfoCache.Load();
			for (int i = 0; i < 4; i++)
			{
				XiControllers[i] = new Controller((UserIndex)i);
			}
			GameToCustomizeComboBox.DataSource = SettingsManager.UserGames.Items;
			// Make sure that X360CE.exe is on top.
			GameToCustomizeComboBox.DisplayMember = "DisplayName";
			GameToCustomizeComboBox.SelectedIndexChanged += GameToCustomizeComboBox_SelectedIndexChanged;

			// Select game by manually trigger event.
			GameToCustomizeComboBox_SelectedIndexChanged(GameToCustomizeComboBox, new EventArgs());

			UpdateTimer = new System.Timers.Timer();
			UpdateTimer.AutoReset = false;
			UpdateTimer.SynchronizingObject = this;
			UpdateTimer.Interval = DefaultPoolingInterval;
			UpdateTimer.Elapsed += new System.Timers.ElapsedEventHandler(UpdateTimer_Elapsed);
			SettingsTimer = new System.Timers.Timer();
			SettingsTimer.AutoReset = false;
			SettingsTimer.SynchronizingObject = this;
			SettingsTimer.Interval = 500;
			SettingsTimer.Elapsed += new System.Timers.ElapsedEventHandler(SettingsTimer_Elapsed);
			CleanStatusTimer = new System.Timers.Timer();
			CleanStatusTimer.AutoReset = false;
			CleanStatusTimer.SynchronizingObject = this;
			CleanStatusTimer.Interval = 3000;
			CleanStatusTimer.Elapsed += new System.Timers.ElapsedEventHandler(CleanStatusTimer_Elapsed);
			Text = EngineHelper.GetProductFullName();
			ShowProgramsTab(SettingsManager.Options.ShowProgramsTab);
			ShowSettingsTab(SettingsManager.Options.ShowSettingsTab);
			ShowDevicesTab(SettingsManager.Options.ShowDevicesTab);
			ShowIniTab(SettingsManager.Options.ShowIniTab);
			// Start Timers.
			UpdateTimer.Start();
			JocysCom.ClassLibrary.Win32.NativeMethods.CleanSystemTray();
			JocysCom.ClassLibrary.Controls.InfoForm.StartMonitor();
		}

		IList<Engine.Data.Program> Programs_FilterList(IList<Engine.Data.Program> items)
		{
			// Make sure default settings have unique by file name.
			var distinctItems = items
				.GroupBy(p => p.FileName.ToLower())
				.Select(g => g.First())
				.ToList();
			return distinctItems;
		}

		IList<Engine.Data.UserGame> Games_FilterList(IList<Engine.Data.UserGame> items)
		{
			// Make sure default settings have unique by file name.
			var distinctItems = items
				.GroupBy(p => p.FileName.ToLower())
				.Select(g => g.First())
				.ToList();

			// Check if current app doesn't exist in the list then...
			var appFile = new FileInfo(Application.ExecutablePath);
			var appItem = distinctItems.FirstOrDefault(x => x.FileName.ToLower() == appFile.Name.ToLower());
			if (appItem == null)
			{
				// Add x360ce.exe
				var scanner = new XInputMaskScanner();
				var item = scanner.FromDisk(appFile.Name);
				var program = SettingsManager.Programs.Items.FirstOrDefault(x => x.FileName.ToLower() == appFile.Name.ToLower());
				item.LoadDefault(program);
				// Append to top.
				distinctItems.Insert(0, item);
			}
			else
			{
				appItem.FullPath = appFile.FullName;
				// Make sure it is on top.
				if (distinctItems.IndexOf(appItem) > 0)
				{
					distinctItems.Remove(appItem);
					distinctItems.Insert(0, appItem);
				}
			}
			return distinctItems;
		}

		private void Summaries_ListChanged(object sender, ListChangedEventArgs e)
		{
			// If map to changed then re-detect devices.
			var pd = e.PropertyDescriptor;
			if (pd != null && pd.Name == "MapTo")
			{
				forceRecountDevices = true;
			}
		}


		void AutoConfigure(Engine.Data.UserGame game)
		{
			var list = SettingsManager.UserDevices.Items.ToList();
			// Filter devices.
			if (SettingsManager.Options.ExcludeSupplementalDevices)
			{
				// Supplemental devices are specialized device with functionality unsuitable for the main control of an application,
				// such as pedals used with a wheel.The following subtypes are defined.
				var supplementals = list.Where(x => x.CapType == (int)SharpDX.DirectInput.DeviceType.Supplemental).ToArray();
				foreach (var supplemental in supplementals)
				{
					list.Remove(supplemental);
				}
			}
			if (SettingsManager.Options.ExcludeVirtualDevices)
			{
				// Exclude virtual devices so application could feed them.
				var virtualDevices = list.Where(x => x.InstanceName.Contains("vJoy")).ToArray();
				foreach (var virtualDevice in virtualDevices)
				{
					list.Remove(virtualDevice);
				}
			}
			// Move gaming wheels to the top index position by default.
			// Games like GTA need wheel to be first device to work properly.
			var wheels = list.Where(x =>
				x.CapType == (int)SharpDX.DirectInput.DeviceType.Driving ||
				x.CapSubtype == (int)DeviceSubType.Wheel
			).ToArray();
			foreach (var wheel in wheels)
			{
				list.Remove(wheel);
				list.Insert(0, wheel);
			}
			// Get configuration of devices for the game.
			var settings = SettingsManager.GetSettings(game.FileName);
			var knownDevices = settings.Select(x => x.InstanceGuid).ToList();
			var newSettingsToProcess = new List<Engine.Data.Setting>();
			int i = 0;
			while (true)
			{
				i++;
				// If there are devices which occupies current position then do nothing.
				if (settings.Any(x => x.MapTo == i)) continue;
				// Try to select first unknown device.
				var newDevice = list.FirstOrDefault(x => !knownDevices.Contains(x.InstanceGuid));
				// If no device found then break.
				if (newDevice == null) break;
				// Create new setting for game/device.
				var newSetting = AppHelper.GetNewSetting(newDevice, game, i <= 4 ? (MapTo)i : MapTo.Disabled);
				newSettingsToProcess.Add(newSetting);
				// Add device to known list.
				knownDevices.Add(newDevice.InstanceGuid);
			}
			foreach (var item in newSettingsToProcess)
			{
				SettingsManager.Settings.Items.Add(item);
			}
		}

		/// <summary>
		/// Link control with INI key. Value/Text of control will be automatically tracked and INI file updated.
		/// </summary>
		void UpdateSettingsMap()
		{
			// INI setting keys with controls.
			SettingsManager.Current.ConfigSaved += Current_ConfigSaved;
			SettingsManager.Current.ConfigLoaded += Current_ConfigLoaded;
			OptionsPanel.UpdateSettingsMap();
		}

		void Current_ConfigSaved(object sender, SettingEventArgs e)
		{
			StatusSaveLabel.Text = string.Format("S {0}", e.Count);
		}

		void Current_ConfigLoaded(object sender, SettingEventArgs e)
		{
			StatusTimerLabel.Text = string.Format("'{0}' loaded.", e.Name);
		}

		public void CopyElevated(string source, string dest)
		{
			if (!WinAPI.IsVista)
			{
				File.Copy(source, dest);
				return;
			}
			var di = new DirectoryInfo(System.IO.Path.GetDirectoryName(dest));
			var security = di.GetAccessControl();
			var fi = new FileInfo(dest);
			var fileSecurity = fi.GetAccessControl();
			// Allow Users to Write.
			//SecurityIdentifier SID = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
			//fileSecurity.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.Write, AccessControlType.Allow));
			//fi.SetAccessControl(fileSecurity);
			var rules = security.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount));
			string message = string.Empty;
			foreach (var myacc in rules)
			{
				var acc = (FileSystemAccessRule)myacc;
				message += string.Format("IdentityReference: {0}\r\n", acc.IdentityReference.Value);
				message += string.Format("Access Control Type: {0}\r\n", acc.AccessControlType.ToString());
				message += string.Format("\t{0}\r\n", acc.FileSystemRights.ToString());
				//if ((acc.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl)
				//{
				//    Console.Write("FullControl");
				//}
				//if ((acc.FileSystemRights & FileSystemRights.ReadData) == FileSystemRights.ReadData)
				//{
				//    Console.Write("ReadData");
				//}
				//if ((acc.FileSystemRights & FileSystemRights.WriteData) == FileSystemRights.WriteData)
				//{
				//    Console.Write("WriteData");
				//}
				//if ((acc.FileSystemRights & FileSystemRights.ListDirectory) == FileSystemRights.ListDirectory)
				//{
				//    Console.Write("ListDirectory");
				//}
				//if ((acc.FileSystemRights & FileSystemRights.ExecuteFile) == FileSystemRights.ExecuteFile)
				//{
				//    Console.Write("ExecuteFile");
				//}
			}
			MessageBox.Show(message);
			//WindowsIdentity self = System.Security.Principal.WindowsIdentity.GetCurrent();
			//			 FileSystemAccessRule rule = new FileSystemAccessRule(
			//    self.Name, 
			//    FileSystemRights.FullControl,
			//    AccessControlType.Allow);
		}

		void MainForm_KeyDown(object sender, KeyEventArgs e)
		{
			for (int i = 0; i < PadControls.Length; i++)
			{
				// If Escape key was pressed while recording then...
				if (e.KeyCode == Keys.Escape)
				{
					var recordingWasStopped = PadControls[i].StopRecording();
					if (recordingWasStopped)
					{
						e.Handled = true;
						e.SuppressKeyPress = true;
					};
				}
			}
			StatusTimerLabel.Text = "";
		}

		void CleanStatusTimer_Elapsed(object sender, EventArgs e)
		{
			if (Program.IsClosing) return;
			StatusTimerLabel.Text = "";
		}

		#region Control Changed Events

		public void NotifySettingsStatus(int eventsSuspendCount)
		{
			StatusEventsLabel.Text = string.Format("Suspend: {0}", eventsSuspendCount);
		}

		/// <summary>
		/// Delay settings trough timer so interface will be more responsive on TrackBars.
		/// Or fast changes. Library will be reloaded as soon as user calms down (no setting changes in 500ms).
		/// </summary>
		public void NotifySettingsChange(Control changedControl)
		{
			var iniContent = GetINI();
			bool changed = false;
			changed |= SettingsManager.Current.ApplyAllSettingsToXML();
			//changed |= SettingsManager.Current.WriteSettingToIni(changedControl);
			// If settings changed then...
			if (changed)
			{
				// Stop updating forms and controls.
				// Update Timer will be started inside Settings timer.
				UpdateTimer.Stop();
				SettingsTimer.Stop();
				SettingsTimer.Start();
			}
		}

		public string GetINI()
		{
			var game = CurrentGame;
			var iniContent = SettingsManager.Current.GetIniContent(game);
			if (IniTextBox.Text != iniContent)
			{
				IniTextBox.Text = iniContent;
			}
			return iniContent;
		}

		void SettingsTimer_Elapsed(object sender, EventArgs e)
		{
			if (Program.IsClosing) return;
			settingsChanged = true;
			UpdateTimer.Start();
		}

		#endregion

		//public void ReloadXinputSettings()
		//{
		//	SuspendEvents();
		//	SettingManager.Current.ReadSettings();
		//	ResumeEvents();
		//}

		//public void SaveSettings()
		//{
		//	UpdateTimer.Stop();
		//	// Save settings to INI file.
		//	SettingManager.Current.WriteAllSettingsToInit();
		//	// Overwrite Temp file.
		//	var ini = new FileInfo(SettingManager.IniFileName);
		//	ini.CopyTo(SettingManager.TmpFileName, true);
		//	StatusTimerLabel.Text = "Settings saved";
		//	UpdateTimer.Start();
		//}

		public static object XInputLock = new object();

		void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			Program.IsClosing = true;
			if (UpdateTimer != null) UpdateTimer.Stop();
			// Disable force feedback effect before closing app.
			try
			{
				lock (XInputLock)
				{
					for (int i = 0; i < 4; i++)
					{
						if (PadControls[i].LeftMotorTestTrackBar.Value > 0 || PadControls[i].RightMotorTestTrackBar.Value > 0)
						{
							var gamePad = XiControllers[i];
							if (XInput.IsLoaded && gamePad.IsConnected)
							{
								gamePad.SetVibration(new Vibration());
							}
						}
					}
					//BeginInvoke((MethodInvoker)delegate()
					//{
					//	XInput.FreeLibrary();    
					//});
				}
				System.Threading.Thread.Sleep(100);
			}
			catch (Exception) { }
			var tmp = new FileInfo(SettingsManager.TmpFileName);
			var ini = new FileInfo(SettingsManager.IniFileName);
			if (tmp.Exists)
			{
				// Before renaming file check for changes.
				var changed = false;
				if (tmp.Length != ini.Length) { changed = true; }
				else
				{
					var tmpChecksum = EngineHelper.GetFileChecksum(tmp.FullName);
					var iniChecksum = EngineHelper.GetFileChecksum(ini.FullName);
					changed = !tmpChecksum.Equals(iniChecksum);
				}
				if (changed)
				{
					var form = new MessageBoxForm();
					form.StartPosition = FormStartPosition.CenterParent;
					var result = form.ShowForm(
					"Do you want to save changes you made to configuration?",
					"Save Changes?",
					MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
					if (result == DialogResult.Yes)
					{
						// Do nothing since INI contains latest updates.
					}
					else if (result == DialogResult.No)
					{
						// Rename temp to INI.
						tmp.CopyTo(SettingsManager.IniFileName, true);
					}
					else if (result == DialogResult.Cancel)
					{
						e.Cancel = true;
						return;
					}
				}
				// delete temp.
				tmp.Delete();
			}
			SaveAll();
		}

		public void SaveAll()
		{

			Settings.Default.Save();
			SettingsManager.OptionsData.Save();
			SettingsManager.Settings.Save();
			SettingsManager.Summaries.Save();
			SettingsManager.Programs.Save();
			SettingsManager.UserGames.Save();
			SettingsManager.Presets.Save();
			SettingsManager.UserDevices.Save();
			SettingsManager.PadSettings.Save();
			SettingsManager.UserDevices.Save();
			SettingsManager.UserInstances.Save();
			SettingsManager.UserComputers.Save();
			XInputMaskScanner.FileInfoCache.Save();
		}

		#region Timer

		public bool forceRecountDevices = true;

		//string deviceInstancesOld = "";
		//string deviceInstancesNew = "";
		public Guid AutoSelectControllerInstance = Guid.Empty;


		///// <summary>
		///// Get direct input devices.
		///// </summary>
		//DeviceInstance[] GetDevices()
		//{
		//	var devices = Manager.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AllDevices).ToList();
		//	if (SettingManager.Current.ExcludeSuplementalDevices)
		//	{
		//		// Supplemental devices are specialized device with functionality unsuitable for the main control of an application,
		//		// such as pedals used with a wheel.The following subtypes are defined.
		//		var supplementals = devices.Where(x => x.Type == SharpDX.DirectInput.DeviceType.Supplemental).ToArray();
		//		foreach (var supplemental in supplementals)
		//		{
		//			devices.Remove(supplemental);
		//		}
		//	}
		//	if (SettingManager.Current.ExcludeVirtualDevices)
		//	{
		//		// Exclude virtual devices so application could feed them.
		//		var virtualDevices = devices.Where(x => x.InstanceName.Contains("vJoy")).ToArray();
		//		foreach (var virtualDevice in virtualDevices)
		//		{
		//			devices.Remove(virtualDevice);
		//		}
		//	}
		//	// Move gaming wheels to the top index position by default.
		//	// Games like GTA need wheel to be first device to work properly.
		//	var wheels = devices.Where(x => x.Type == SharpDX.DirectInput.DeviceType.Driving || x.Subtype == (int)DeviceSubType.Wheel).ToArray();
		//	foreach (var wheel in wheels)
		//	{
		//		devices.Remove(wheel);
		//		devices.Insert(0, wheel);
		//	}
		//	var orderedDevices = new DeviceInstance[4];
		//	// Assign devices to their positions.
		//	for (int d = 0; d < devices.Count; d++)
		//	{
		//		var ig = devices[d].InstanceGuid;
		//		var section = SettingManager.Current.GetInstanceSection(ig);
		//		var ini2 = new Ini(SettingManager.IniFileName);
		//		string v = ini2.GetValue(section, SettingName.MapToPad);
		//		int mapToPad = 0;
		//		if (int.TryParse(v, out mapToPad) && mapToPad > 0 && mapToPad <= 4)
		//		{
		//			// If position is not occupied then...
		//			if (orderedDevices[mapToPad - 1] == null)
		//			{
		//				orderedDevices[mapToPad - 1] = devices[d];
		//			}
		//		}
		//	}
		//	// Get list of unassigned devices.
		//	var unassignedDevices = devices.Except(orderedDevices).ToArray();
		//	for (int i = 0; i < unassignedDevices.Length; i++)
		//	{
		//		// Assign to first empty slot.
		//		for (int d = 0; d < orderedDevices.Length; d++)
		//		{
		//			// If position is not occupied then...
		//			if (orderedDevices[d] == null)
		//			{
		//				orderedDevices[d] = unassignedDevices[i];
		//				break;
		//			}
		//		}
		//	}
		//	return orderedDevices;
		//}


		///// <summary>
		///// Access this only inside Timer_Click!
		///// </summary>
		//bool RefreshCurrentInstances(bool forceReload = false)
		//{
		//	bool instancesChanged = false;
		//	DeviceInstance[] devices = null;
		//	//var types = DeviceType.Driving | DeviceType.Flight | DeviceType.Gamepad | DeviceType.Joystick | DeviceType.FirstPerson;
		//	if (forceRecountDevices || forceReload)
		//	{
		//		devices = GetDevices();
		//		// Store device instances and their order.
		//		deviceInstancesNew = string.Join(",", devices.Select(x => x == null ? "" : x.InstanceGuid.ToString()));
		//		forceRecountDevices = false;
		//	}
		//	// If device list changed then...
		//	if (deviceInstancesNew != deviceInstancesOld)
		//	{
		//		deviceInstancesOld = deviceInstancesNew;
		//		if (devices == null) devices = GetDevices();
		//		var instances = devices;
		//		// Dispose previous list of devices.
		//		for (int i = 0; i < 4; i++)
		//		{
		//			if (DiDevices[i].State != null)
		//			{
		//				// Dispose current device.
		//				DiDevices[i].State.Unacquire();
		//				DiDevices[i].State.Dispose();
		//			}
		//		}
		//		// Create new list of devices.
		//		for (int i = 0; i < 4; i++)
		//		{
		//			var inst = instances[i];
		//			if (inst == null)
		//			{
		//				DiDevices[i].State = null;
		//				DiDevices[i].Info = null;
		//			}
		//			else
		//			{

		//				var j = new Joystick(Manager, inst.InstanceGuid);
		//				DiDevices[i].State = j;
		//				var classGuid = j.Properties.ClassGuid;
		//				var interfacePath = j.Properties.InterfacePath;
		//				// Must find better way to find Device than by Vendor ID and Product ID.
		//				var devs = DeviceDetector.GetDevices(classGuid, JocysCom.ClassLibrary.Win32.DIGCF.DIGCF_ALLCLASSES, null, j.Properties.VendorId, j.Properties.ProductId, 0);
		//				DiDevices[i].Info = devs.FirstOrDefault();
		//			}
		//		}
		//		SettingsDatabasePanel.BindDevices(instances);
		//		SettingsDatabasePanel.BindFiles();
		//		for (int i = 0; i < 4; i++)
		//		{
		//			// Backup old instance.
		//			DiDevices[i].InstanceOld = DiDevices[i].Instance;
		//			// Assign new instance.
		//			DiDevices[i].Instance = instances[i];
		//		}
		//		instancesChanged = true;
		//	}
		//	// Return true if instances changed.
		//	return instancesChanged;
		//}

		// This value will be modified to true when settings on the form changes and 
		// XInput library needs to be reload.
		bool settingsChanged = false;
		State emptyState = new State();

		//bool[] cleanPadStatus = new bool[4];

		object formLoadLock = new object();
		public bool update1Enabled = true;
		public bool? update2Enabled;
		bool update3Enabled;

		void UpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			if (Program.IsClosing) return;
			Program.TimerCount++;
			lock (formLoadLock)
			{
				if (update1Enabled)
				{
					update1Enabled = false;
					UpdateForm1();
					// Update 2 part will be enabled after all issues are checked.
				}
				if (update2Enabled.HasValue && update2Enabled.Value)
				{
					update2Enabled = false;
					UpdateForm2();
					update3Enabled = true;
				}
				if (update3Enabled && IsHandleCreated)
				{
					update3Enabled = false;
					//UpdateForm3();
					DHelper.Start();
				}
			}
			UpdateTimer.Start();
		}

		void UpdateForm1()
		{
			//if (DesignMode) return;
			OptionsPanel.InitOptions();
			// Set status.
			StatusSaveLabel.Visible = false;
			StatusEventsLabel.Visible = false;
			// Load Tab pages.
			ControlPages = new TabPage[4];
			ControlPages[0] = Pad1TabPage;
			ControlPages[1] = Pad2TabPage;
			ControlPages[2] = Pad3TabPage;
			ControlPages[3] = Pad4TabPage;
			//BuletImageList.Images.Add("bullet_square_glass_blue.png", new Bitmap(Helper.GetResource("Images.bullet_square_glass_blue.png")));
			//BuletImageList.Images.Add("bullet_square_glass_green.png", new Bitmap(Helper.GetResource("Images.bullet_square_glass_green.png")));
			//BuletImageList.Images.Add("bullet_square_glass_grey.png", new Bitmap(Helper.GetResource("Images.bullet_square_glass_grey.png")));
			//BuletImageList.Images.Add("bullet_square_glass_red.png", new Bitmap(Helper.GetResource("Images.bullet_square_glass_red.png")));
			//BuletImageList.Images.Add("bullet_square_glass_yellow.png", new Bitmap(Helper.GetResource("Images.bullet_square_glass_yellow.png")));
			foreach (var item in ControlPages) item.ImageKey = "bullet_square_glass_grey.png";
			// Hide status values.
			StatusDllLabel.Text = "";
			MainStatusStrip.Visible = false;
			// Check for various issues.
			InitWarnigForm();
			InitDeviceForm();
			InitUpdateForm();
		}

		void UpdateForm2()
		{
			// Set status labels.
			StatusIsAdminLabel.Text = WinAPI.IsVista
				? string.Format("Elevated: {0}", WinAPI.IsElevated())
				: "";
			StatusIniLabel.Text = SettingsManager.IniFileName;
			CheckEncoding(SettingsManager.TmpFileName);
			CheckEncoding(SettingsManager.IniFileName);
			// Show status values.
			MainStatusStrip.Visible = true;
			// Update settings manager with [Options] section.
			UpdateSettingsMap();
			// Load PAD controls.
			PadControls = new PadControl[4];
			for (int i = 0; i < PadControls.Length; i++)
			{
				var mapTo = (MapTo)(i + 1);
				PadControls[i] = new Controls.PadControl(mapTo);
				PadControls[i].Name = string.Format("ControlPad{0}", (int)mapTo);
				PadControls[i].Dock = DockStyle.Fill;
				ControlPages[i].Controls.Add(PadControls[i]);
				PadControls[i].InitPadControl();
				// Update settings manager with [Mappings] section.
			}
			SettingsManager.AddMap(SettingsManager.MappingsSection, () => SettingName.PAD1, PadControls[0].MappedDevicesDataGridView);
			SettingsManager.AddMap(SettingsManager.MappingsSection, () => SettingName.PAD2, PadControls[1].MappedDevicesDataGridView);
			SettingsManager.AddMap(SettingsManager.MappingsSection, () => SettingName.PAD3, PadControls[2].MappedDevicesDataGridView);
			SettingsManager.AddMap(SettingsManager.MappingsSection, () => SettingName.PAD4, PadControls[3].MappedDevicesDataGridView);
			// Update settings manager with [PAD1], [PAD2], [PAD3], [PAD4] sections.
			// Note: There must be no such sections in new config.
			for (int i = 0; i < PadControls.Length; i++)
			{
				PadControls[i].UpdateSettingsMap();
				PadControls[i].InitPadData();
			}
			// Initialize pre-sets. Execute only after name of cIniFile is set.
			//SettingsDatabasePanel.InitPresets();
			// Allow events after PAD control are loaded.
			MainTabControl.SelectedIndexChanged += new System.EventHandler(this.MainTabControl_SelectedIndexChanged);
			// Load about control.
			ControlAbout = new AboutControl();
			ControlAbout.Dock = DockStyle.Fill;
			AboutTabPage.Controls.Add(ControlAbout);
			//ReloadXinputSettings();
			// Start capture setting change events.
			SettingsManager.Current.ResumeEvents();
		}

		/// <summary>
		/// This method will run continuously if form is not minimized.
		/// </summary>
		void UpdateForm3()
		{
			//	// If settings changed then...
			//	if (settingsChanged)
			//	{
			//		ReloadLibrary();
			//	}
			//	else
			//	{
			for (int i = 0; i < 4; i++)
			{
				var game = CurrentGame;
				var currentFile = (game == null) ? null : game.FileName;
				// Get devices mapped to game and specific controller index.
				var devices = SettingsManager.GetDevices(currentFile, (MapTo)(i + 1));
				// DInput instance is ON if active devices found.
				var diOn = devices.Count(x => x.IsOnline) > 0;
				// XInput instance is ON.
				var xiOn = false;
				//			State currentGamePad = emptyState;
				//			lock (XInputLock)
				//			{
				//				var gamePad = XiControllers[i];
				//				if (XInput.IsLoaded && gamePad.IsConnected)
				//				{
				//					currentGamePad = gamePad.GetState();
				//					xiOn = true;
				//				}
				//			}
				var padControl = PadControls[i];
				//			// Update Form from DInput state.
				padControl.UpdateFromDInput();
				//			// Update Form from XInput state.
				//			padControl.UpdateFromXInput(currentGamePad, xiOn);
				//			// Update LED of GamePad state.
				string image = diOn
					// DInput ON, XInput ON 
					? xiOn ? "green"
					// DInput ON, XInput OFF
					: "red"
					// DInput OFF, XInput ON
					: xiOn ? "yellow"
					// DInput OFF, XInput OFF
					: "grey";
				string bullet = string.Format("bullet_square_glass_{0}.png", image);
				if (ControlPages[i].ImageKey != bullet)
					ControlPages[i].ImageKey = bullet;
			}
			//		UpdateStatus();
			//	}
		}

		public void ReloadLibrary()
		{
			Program.ReloadCount++;
			settingsChanged = false;
			var dllInfo = EngineHelper.GetDefaultDll();
			if (dllInfo != null && dllInfo.Exists)
			{
				bool byMicrosoft;
				var dllVersion = EngineHelper.GetDllVersion(dllInfo.FullName, out byMicrosoft);
				StatusDllLabel.Text = dllInfo.Name + " " + dllVersion.ToString() + (byMicrosoft ? " (Microsoft)" : "");
				// If fast reload of settings is supported then...
				lock (XInputLock)
				{
					if (XInput.IsResetSupported)
					{
						XInput.Reset();
					}
					// Slow: Reload whole x360ce.dll.
					Exception error;
					//forceRecountDevices = true;
					XInput.ReLoadLibrary(dllInfo.Name, out error);
					if (!XInput.IsLoaded)
					{
						var caption = string.Format("Failed to load '{0}'", dllInfo.Name);
						var text = string.Format("{0}", error == null ? "Unknown error" : error.Message);
						var form = new MessageBoxForm();
						form.StartPosition = FormStartPosition.CenterParent;
						form.ShowForm(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
					else
					{
						for (int i = 0; i < 4; i++)
						{

							var currentPadControl = PadControls[i];
							currentPadControl.UpdateForceFeedBack();
						}
					}
				}
			}
			else
			{
				StatusDllLabel.Text = "";
			}
		}

		public void UpdateStatus(string message = "")
		{
			AppHelper.SetText(StatusTimerLabel, "Count: {0}, Reloads: {1}, Errors: {2} {3}",
				Program.TimerCount, Program.ReloadCount, Program.ErrorCount, message);
		}
		#endregion

		bool HelpInit = false;

		void MainTabControl_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (MainTabControl.SelectedTab == HelpTabPage && !HelpInit)
			{
				// Move this here so interface will load one second faster.
				HelpInit = true;
				var stream = EngineHelper.GetResource("Documents.Help.htm");
				var sr = new StreamReader(stream);
				NameValueCollection list = new NameValueCollection();
				list.Add("font-name-default", "'Microsoft Sans Serif'");
				list.Add("font-size-default", "16");
				HelpRichTextBox.Rtf = Html2Rtf.Converter.Html2Rtf(sr.ReadToEnd(), list);
				HelpRichTextBox.SelectAll();
				HelpRichTextBox.SelectionIndent = 8;
				HelpRichTextBox.SelectionRightIndent = 8;
				HelpRichTextBox.DeselectAll();
			}
			else if (MainTabControl.SelectedTab == SettingsTabPage)
			{
				if (OptionsPanel.InternetCheckBox.Checked && OptionsPanel.InternetAutoLoadCheckBox.Checked)
				{
					//SettingsDatabasePanel.RefreshGrid(true);
				}
			}
			var tab = MainTabControl.SelectedTab;
			if (tab != null) SetHeaderSubject(tab.Text);
		}

		public void XInputEnable(bool enable)
		{
			lock (XInputLock)
			{
				XInput.XInputEnable(enable);
			}
		}

		#region Check Files

		void CheckEncoding(string path)
		{
			if (!File.Exists(path)) return;
			var sr = new StreamReader(path, true);
			var content = sr.ReadToEnd();
			sr.Close();
			if (sr.CurrentEncoding != System.Text.Encoding.Unicode)
			{
				File.WriteAllText(path, content, System.Text.Encoding.Unicode);
			}
		}

		bool IsFileSame(string fileName)
		{
			return false;
			//if (!System.IO.File.Exists(fileName)) return false;
			//var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
			//StreamReader sr;
			//// Get MD5 of file on the disk.
			//sr = new StreamReader(fileName);
			//var dMd5 = new Guid(md5.ComputeHash(sr.BaseStream));
			//// Get MD5 of resource file.
			//if (fileName == dllFile0) fileName = dllFile;
			//if (fileName == dllFile1) fileName = dllFile;
			//if (fileName == dllFile2) fileName = dllFile;
			//if (fileName == dllFile3) fileName = dllFile;
			//var assembly = Assembly.GetExecutingAssembly();
			//sr = new StreamReader(assembly.GetManifestResourceStream(this.GetType().Namespace + ".Presets." + fileName));
			//var rMd5 = new Guid(md5.ComputeHash(sr.BaseStream));
			//// return result.
			//return rMd5.Equals(dMd5);
		}

		public bool CreateFile(string resourceName, string destinationFileName, ProcessorArchitecture oldArchitecture, ProcessorArchitecture newArchitecture)
		{
			if (destinationFileName == null) destinationFileName = resourceName;
			DialogResult answer;
			var form = new MessageBoxForm();
			form.StartPosition = FormStartPosition.CenterParent;
			var oldDesc = EngineHelper.GetProcessorArchitectureDescription(oldArchitecture);
			var newDesc = EngineHelper.GetProcessorArchitectureDescription(newArchitecture);
			var fileName = new FileInfo(destinationFileName).Name;
			answer = form.ShowForm(
				string.Format("You are running {2} application but {0} on the disk was built for {1} architecture.\r\n\r\nDo you want to replace {0} file with {2} version?", fileName, oldDesc, newDesc),
				"Processor architecture mismatch.",
				MessageBoxButtons.YesNo, MessageBoxIcon.Information);
			if (answer == DialogResult.Yes)
			{
				return AppHelper.WriteFile(resourceName, destinationFileName);
			}
			return true;
		}

		public bool CreateFile(string resourceName, string destinationFileName, Version oldVersion = null, Version newVersion = null)
		{
			if (destinationFileName == null) destinationFileName = resourceName;
			DialogResult answer;
			var form = new MessageBoxForm();
			form.StartPosition = FormStartPosition.CenterParent;
			var fileName = new FileInfo(destinationFileName).FullName;
			if (newVersion == null)
			{
				answer = form.ShowForm(
					string.Format("'{0}' was not found.\r\nThis file is required for emulator to function properly.\r\n\r\nDo you want to create this file?", fileName),
					string.Format("'{0}' was not found.", destinationFileName),
					MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
			}
			else
			{
				answer = form.ShowForm(
					string.Format("New version of this file is available:\r\n{0}\r\n\r\nOld version: {1}\r\nNew version: {2}\r\n\r\nDo you want to update this file?", fileName, oldVersion, newVersion),
					string.Format("New version of '{0}' file is available.", destinationFileName),
					MessageBoxButtons.YesNo, MessageBoxIcon.Information);
			}
			if (answer == DialogResult.Yes)
			{
				return AppHelper.WriteFile(resourceName, destinationFileName);
			}
			return true;
		}

		#endregion

		#region Allow only one copy of Application at a time

		/// <summary>Stores the unique windows message id from the RegisterWindowMessage call.</summary>
		int _WindowMessage;
		/// <summary>Used to determine if the application is already open.</summary>
		System.Threading.Mutex _Mutex;

		public const int wParam_Restore = 1;
		public const int wParam_Close = 2;

		/// <summary>
		/// Broadcast message to other instances of this application.
		/// </summary>
		/// <param name="wParam">Send parameter to other instances of this application.</param>
		/// <returns>True - other instances exists; False - other instances doesn't exist.</returns>
		public bool BroadcastMessage(int wParam)
		{
			Exception error;
			// Check for previous instance of this app.
			var uid = Application.ProductName;
			_Mutex = new System.Threading.Mutex(false, uid);
			// Register the windows message
			_WindowMessage = NativeMethods.RegisterWindowMessage(uid, out error);
			var firsInstance = _Mutex.WaitOne(1, true);
			// If this is not the first instance then...
			if (!firsInstance)
			{
				// Broadcast a message with parameters to another instance.
				var recipients = (int)BSM.BSM_APPLICATIONS;
				var flags = BSF.BSF_IGNORECURRENTTASK | BSF.BSF_POSTMESSAGE;
				var ret = NativeMethods.BroadcastSystemMessage((int)flags, ref recipients, _WindowMessage, wParam, 0, out error);
			}
			return !firsInstance;
		}

		/// <summary>
		/// NOTE: you must be careful with this method, because this method is responsible for all the
		/// windows messages that are coming to the form.
		/// </summary>
		/// <param name="m"></param>
		/// <remarks>This overrides the windows messaging processing</remarks>
		protected override void DefWndProc(ref Message m)
		{
			// If message value was found then...
			if (m.Msg == _WindowMessage)
			{
				// Show currently running instance.
				if (m.WParam.ToInt32() == wParam_Restore)
				{
					RestoreFromTray(true);
				}
				//  Close currently running instance.
				if (m.WParam.ToInt32() == wParam_Close)
				{
					Close();
				}
			}
			// Let the normal windows messaging process it.
			base.DefWndProc(ref m);
		}

		#endregion

		#region Warning Form

		WarningsForm _WarningForm;
		object warningFormLock = new object();

		void InitWarnigForm()
		{
			lock (warningFormLock)
			{
				_WarningForm = new WarningsForm();
				_WarningForm.CheckTimer.Start();
			}
		}

		void DisposeWarnigForm()
		{
			lock (warningFormLock)
			{
				if (_WarningForm != null)
				{
					_WarningForm.Dispose();
					_WarningForm = null;
				}
			}
		}

		#endregion

		#region Device Form

		MapDeviceToControllerForm _DeviceForm;
		object DeviceFormLock = new object();

		void InitDeviceForm()
		{
			lock (DeviceFormLock)
			{
				_DeviceForm = new MapDeviceToControllerForm();
			}
		}

		void DisposeDeviceForm()
		{
			lock (DeviceFormLock)
			{
				if (_DeviceForm != null)
				{
					_DeviceForm.Dispose();
					_DeviceForm = null;
				}
			}
		}

		public UserDevice[] ShowDeviceForm()
		{
			lock (DeviceFormLock)
			{
				if (_DeviceForm == null)
					return null;
				_DeviceForm.StartPosition = FormStartPosition.CenterParent;
				var result = _DeviceForm.ShowDialog();
				return _DeviceForm.SelectedDevices;
			}
		}

		#endregion

		#region Update Form

		Forms.UpdateForm _UpdateForm;
		object UpdateFormLock = new object();

		void InitUpdateForm()
		{
			lock (UpdateFormLock)
			{
				_UpdateForm = new Forms.UpdateForm();
			}
		}

		void DisposeUpdateForm()
		{
			lock (UpdateFormLock)
			{
				if (_UpdateForm != null)
				{
					_UpdateForm.Dispose();
					_UpdateForm = null;
				}
			}
		}

		public bool? ShowUpdateForm()
		{
			lock (UpdateFormLock)
			{
				if (_UpdateForm == null)
					return null;
				var oldTab = MainTabControl.SelectedTab;
				MainTabControl.SelectedTab = CloudTabPage;
				_UpdateForm.StartPosition = FormStartPosition.CenterParent;
				_UpdateForm.OpenDialog();
				var result = _UpdateForm.ShowDialog();
				_UpdateForm.CloseDialog();
				MainTabControl.SelectedTab = oldTab;
				return null;
			}
		}

		public void ProcessUpdateResults(CloudMessage results)
		{
			lock (UpdateFormLock)
			{
				if (_UpdateForm == null)
					return;
				_UpdateForm.Step2ProcessUpdateResults(results);
			}
		}

		#endregion

		/// <summary>
		/// Clean up any 
		/// being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				if (_Mutex != null)
				{
					_Mutex.Dispose();
				}
				DisposeWarnigForm();
				DisposeDeviceForm();
				DisposeUpdateForm();
				if (DHelper != null)
					DHelper.Dispose();
				components.Dispose();
				//lock (checkTimerLock)
				//{
				//	// If timer is disposed then return;
				//	if (checkTimer == null) return;
				//	CheckAll();
				//}
			}
			base.Dispose(disposing);
		}

		private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}

		public UserGame CurrentGame;
		public object CurrentGameLock = new object();

		private void GameToCustomizeComboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			lock (CurrentGameLock)
			{
				var item = GameToCustomizeComboBox.SelectedItem as Engine.Data.UserGame;
				// If nohting changed then...
				if (Equals(item, CurrentGame))
				{
					return;
				}
				if (CurrentGame != null)
				{
					// Detach event from old game.
					CurrentGame.PropertyChanged -= CurrentGame_PropertyChanged;
				}
				if (item != null)
				{
					// attach event to new game.
					item.PropertyChanged += CurrentGame_PropertyChanged;
				}
				CurrentGame = item;
				if (PadControls != null)
				{
					// Update PAD Control.
					foreach (var ps in PadControls)
					{
						if (ps != null)
							ps.UpdateFromCurrentGame();
					}
				}
			}
		}

		private void CurrentGame_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var name = AppHelper.GetPropertyName<UserGame>(x => x.AutoMapMask);
			// If AutoMapMask Changed then...
			if (PadControls != null)
			{
				// Update PAD Control.
				foreach (var ps in PadControls)
				{
					if (ps != null)
						ps.UpdateFromCurrentGame();
				}
			}
		}

		private void StatusIniLabel_DoubleClick(object sender, EventArgs e)
		{
			var game = (Engine.Data.UserGame)GameToCustomizeComboBox.SelectedItem;
			// Get game directory.
			var dir = new FileInfo(game.FullPath).Directory;
			var fullPath = Path.Combine(dir.FullName, SettingsManager.IniFileName);
			EngineHelper.BrowsePath(fullPath);
		}


		private void SaveButton_Click(object sender, EventArgs e)
		{
			// Refresh INI tab.
			Current.GetINI();
			Save(CurrentGame);
		}

		private void SaveAllButton_Click(object sender, EventArgs e)
		{
			var games = SettingsManager.UserGames.Items.Where(x => x.IsEnabled).ToArray();
			Save(games);
		}

		void Save(params UserGame[] games)
		{
			SaveButton.Enabled = false;
			SaveAllButton.Enabled = false;
			Application.DoEvents();
			var sb = new StringBuilder();
			sb.AppendLine("Synchronize current settings to game folders?");
			sb.AppendLine();
			var values = ((GameRefreshStatus[])Enum.GetValues(typeof(GameRefreshStatus))).Except(new[] { GameRefreshStatus.OK }).ToArray();
			// Check changes first.
			for (int i = 0; i < games.Length; i++)
			{
				var game = games[i];
				var status = SettingsManager.Current.GetDllAndIniStatus(game, false);
				if (status != GameRefreshStatus.OK)
				{
					sb.AppendFormat("{0} {1}\r\n", game.FileProductName, game.FileVersion);
					sb.AppendFormat("{0}\r\n\r\n", game.FullPath);
					var errors = new List<string>();
					foreach (GameRefreshStatus value in values)
					{
						if (status.HasFlag(value))
						{
							var description = JocysCom.ClassLibrary.ClassTools.EnumTools.GetDescription(value);
							errors.Add("    " + description);
						}
					}
					sb.Append(string.Join("\r\n", errors));
					sb.AppendLine();
					sb.AppendLine();
				}
			}
			MessageBoxForm form = new MessageBoxForm();
			form.StartPosition = FormStartPosition.CenterParent;
			var result = form.ShowForm(sb.ToString(), "Synchronize", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
			if (result == DialogResult.OK)
			{
				for (int i = 0; i < games.Length; i++)
				{
					var game = games[i];
					SettingsManager.Current.SaveINI(game);
				}
				SettingsManager.Current.ApplyAllSettingsToXML();
				SaveAll();
			}
			var timer = new System.Timers.Timer();
			timer.AutoReset = false;
			timer.Interval = 520;
			timer.SynchronizingObject = this;
			timer.Elapsed += Timer_Elapsed;
			timer.Start();
		}


		private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			SaveButton.Enabled = true;
			SaveAllButton.Enabled = true;
			var timer = (System.Timers.Timer)sender;
			timer.Elapsed -= Timer_Elapsed;
			timer.Dispose();
		}

		#region Update from DHelper


		object LockFormEvents = new object();
		bool FormEventsEnabled;

		void EnableFormUpdates(bool enable)
		{
			lock (LockFormEvents)
			{
				if (enable && !FormEventsEnabled)
				{
					FormEventsEnabled = true;
					DHelper.DevicesUpdated += DHelper_DevicesUpdated;
					DHelper.StatesCombined += DHelper_StatesCombined;
					DHelper.UpdateCompleted += DHelper_UpdateCompleted;
					DHelper.FrequencyUpdated += DHelper_FrequencyUpdated;
				}
				else if (!enable && FormEventsEnabled)
				{
					FormEventsEnabled = false;
					DHelper.DevicesUpdated -= DHelper_DevicesUpdated;
					DHelper.StatesCombined -= DHelper_StatesCombined;
					DHelper.UpdateCompleted -= DHelper_UpdateCompleted;
					DHelper.FrequencyUpdated -= DHelper_FrequencyUpdated;
				}
			}
		}


		DateTime lastRun;
		bool UpdateCompletedBusy;
		object UpdateCompletedLock = new object();

		private void DHelper_UpdateCompleted(object sender, EventArgs e)
		{
			lock (UpdateCompletedLock)
			{
				var n = DateTime.Now;
				// Allow no more than 5 frames per second.
				if (UpdateCompletedBusy || n.Subtract(lastRun).Milliseconds < 200)
					return;
				lastRun = n;
				UpdateCompletedBusy = true;
			}
			// Make sure method is executed on the same thread as this control.
			var method = new EventHandler<EventArgs>(DHelper_UpdateCompletedInvoked);
			BeginInvoke(method, new object[] { sender, e });
		}

		private void DHelper_UpdateCompletedInvoked(object sender, EventArgs e)
		{
			UpdateForm3();
			UpdateCompletedBusy = false;
		}

		private void DHelper_DevicesUpdated(object sender, EventArgs e)
		{
			// Make sure method is executed on the same thread as this control.
			if (InvokeRequired)
			{
				var method = new EventHandler<EventArgs>(DHelper_DevicesUpdated);
				BeginInvoke(method, new object[] { sender, e });
				return;
			}
			SettingsManager.RefreshSettingsConnectionState();
			AppHelper.SetText(UpdateDevicesStatusLabel, "D: {0}", DHelper.RefreshDevicesCount);
		}

		private void DHelper_StatesCombined(object sender, EventArgs e)
		{
			// Get XInput states for form update.
			var combinedStates = DHelper.CombinedXInputStates.ToArray();
		}


		private void DHelper_FrequencyUpdated(object sender, EventArgs e)
		{
			// Make sure method is executed on the same thread as this control.
			if (InvokeRequired)
			{
				var method = new EventHandler<EventArgs>(DHelper_FrequencyUpdated);
				BeginInvoke(method, new object[] { sender, e });
				return;
			}
			AppHelper.SetText(UpdateFrequencyLabel, "Hz: {0}", DHelper.UpdateFrequency);
		}

		#endregion

		#region Show/Hide tabs.


		public void ShowTab(bool show, TabPage page)
		{
			var tc = MainTabControl;
			// If must hide then...
			if (!show && tc.TabPages.Contains(page))
			{
				// Hide and return.
				tc.TabPages.Remove(page);
				return;
			}
			// If must show then..
			if (show && !tc.TabPages.Contains(page))
			{
				// Create list of tabs to maintain same order when hiding and showing tabs.
				var tabs = new List<TabPage>() { ProgramsTabPage, SettingsTabPage, DevicesTabPage, IniTabPage };
				// Get index of always displayed tab.
				var index = tc.TabPages.IndexOf(GamesTabPage);
				// Get tabs in front of tab which must be inserted.
				var tabsBefore = tabs.Where(x => tabs.IndexOf(x) < tabs.IndexOf(page));
				// Count visible tabs.
				var countBefore = tabsBefore.Count(x => tc.TabPages.Contains(x));
				tc.TabPages.Insert(index + countBefore + 1, page);
			}
		}

		public void ShowProgramsTab(bool show)
		{
			ShowTab(show, ProgramsTabPage);
		}

		public void ShowSettingsTab(bool show)
		{
			ShowTab(show, SettingsTabPage);
		}
		public void ShowDevicesTab(bool show)
		{
			ShowTab(show, DevicesTabPage);
		}
		public void ShowIniTab(bool show)
		{
			ShowTab(show, IniTabPage);
		}

		#endregion

	}
}

