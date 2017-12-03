﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using x360ce.Engine;
using System.Linq;
using System.Windows.Forms;

namespace x360ce.App.Issues
{
	public class ArchitectureIssue : WarningItem
	{
		public ArchitectureIssue() : base()
		{
			Name = "Architecture";
			FixName = "Download";
		}

		// Check file only once.
		bool CheckFile = true;

		public override void Check()
		{
			var required = SettingsManager.UserGames.Items.Any(x => x.EmulationType == (int)EmulationType.Library);
			if (!required)
			{
				SetSeverity(IssueSeverity.None);
				return;
			}
			var architectures = new Dictionary<string, ProcessorArchitecture>();
			var architecture = Assembly.GetExecutingAssembly().GetName().ProcessorArchitecture;
			var exes = EngineHelper.GetFiles(".", "*.exe");
			// Exclude x360ce files.
			exes = exes.Where(x => !x.ToLower().Contains("x360ce")).ToArray();
			// If single executable was found then...
			if (exes.Length == 1 && CheckFile)
			{
				CheckFile = false;
				// Update current settings file.
				MainForm.Current.Invoke((Action)delegate ()
				{
					MainForm.Current.GameSettingsPanel.ProcessExecutable(exes[0]);
				});
			}
			foreach (var exe in exes)
			{
				var pa = Engine.Win32.PEReader.GetProcessorArchitecture(exe);
				architectures.Add(exe, pa);
			}
			var fi = new FileInfo(Application.ExecutablePath);
			// Select all architectures of executables.
			var archs = architectures.Select(x => x.Value).ToArray();
			var x86Count = archs.Count(x => x == ProcessorArchitecture.X86);
			var x64Count = archs.Count(x => x == ProcessorArchitecture.Amd64);
			// If executables are 32-bit, but this program is 64-bit then...
			if (x86Count > 0 && x64Count == 0 && architecture == ProcessorArchitecture.Amd64)
			{
				SetSeverity(
					IssueSeverity.Moderate, 1,
					"This folder contains 32-bit game. You should use 32-bit X360CE Application:\r\n" +
					"http://www.x360ce.com/Files/x360ce.zip"
				);
				return;
			}
			// If executables are 64-bit, but this program is 32-bit then...
			if (x64Count > 0 && x86Count == 0 && architecture == ProcessorArchitecture.X86)
			{
				SetSeverity(
					IssueSeverity.Moderate, 2,
					"This folder contains 64-bit game. You should use 64-bit X360CE Application:\r\n" +
					"http://www.x360ce.com/Files/x360ce_x64.zip"
				);
				return;
			}
			SetSeverity(IssueSeverity.None);
		}

		public override void Fix()
		{
			if (FixType == 1)
			{
				EngineHelper.OpenUrl("http://www.x360ce.com/Files/x360ce.zip");
			}
			else if (FixType == 2)
			{
				EngineHelper.OpenUrl("http://www.x360ce.com/Files/x360ce_x64.zip");
			}
			RaiseFixApplied();
		}

	}
}
