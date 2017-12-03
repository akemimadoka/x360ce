﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using x360ce.Engine;
using System.Linq;
using x360ce.Engine.Data;

namespace x360ce.App
{
	/// <summary>
	/// Contains Item which needs to be updated on the cloud (Cloud Message will be created from it).
	/// </summary>
	public class CloudItem : INotifyPropertyChanged
	{
		public CloudAction Action { get { return Message == null ? CloudAction.None : Message.Action; } }

		public CloudState State { get { return _State; } set { _State = value; NotifyPropertyChanged("State"); } }
		CloudState _State;

		public int Try { get { return _Try; } set { _Try = value; NotifyPropertyChanged("Try"); } }
		int _Try;

		public DateTime Date { get; set; }

		[XmlIgnore]
		public Exception Error { get; set; }

		/// <summary>
		/// Message command to send.
		/// </summary>
		public CloudMessage Message
		{
			get { return _Message; }
			set
			{
				_Message = value;
				NotifyPropertyChanged("Action");
				NotifyPropertyChanged("Description");
			}
		}
		CloudMessage _Message;

		public string Description
		{
			get
			{
				var list = new List<string>();
				if (Message.UserDevices != null)
				{
					list.AddRange(Message.UserDevices.Select(x => string.Format("{0}: {1}", typeof(UserDevice).Name, x.DisplayName)));
					if (Message.UserDevices.Length == 0)
						list.Add(string.Format("{0}s", typeof(UserDevice).Name));
				}
				if (Message.UserGames != null)
				{
					list.AddRange(Message.UserGames.Select(x => string.Format("{0}: {1}", typeof(UserGame).Name, x.DisplayName)));
					if (Message.UserGames.Length == 0)
						list.Add(string.Format("{0}s", typeof(UserGame).Name));
				}
				return string.Join(", ", list);
			}
		}

		#region INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		private void NotifyPropertyChanged(string propertyName)
		{
			var ev = PropertyChanged;
			if (ev == null) return;
			ev(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion
	}

}
