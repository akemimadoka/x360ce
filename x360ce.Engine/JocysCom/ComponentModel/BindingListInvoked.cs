﻿using System;
using System.ComponentModel;

namespace JocysCom.ClassLibrary.ComponentModel
{
	public class BindingListInvoked<T> : BindingList<T>
	{
		#region ISynchronizeInvoker

		public ISynchronizeInvoke SynchronizingObject { get; set; }

		delegate void ItemDelegate(int index, T item);

		void Invoke(Delegate method, params object[] args)
		{
			var so = SynchronizingObject;
			if (so != null && so.InvokeRequired)
				so.Invoke(method, args);
			else
				method.DynamicInvoke(args);
		}

		protected override void RemoveItem(int index)
		{
			Invoke((Action<int>)base.RemoveItem, index);
		}

		protected override void InsertItem(int index, T item)
		{
			Invoke((ItemDelegate)base.InsertItem, index, item);
		}

		protected override void SetItem(int index, T item)
		{
			Invoke((ItemDelegate)base.SetItem, index, item);
		}

		protected override void OnListChanged(ListChangedEventArgs e)
		{
			Invoke((Action<ListChangedEventArgs>)base.OnListChanged, e);
		}

		protected override void OnAddingNew(AddingNewEventArgs e)
		{
			Invoke((Action<AddingNewEventArgs>)base.OnAddingNew, e);
		}

		#endregion
	}
}
