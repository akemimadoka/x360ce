﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Objects.DataClasses;
using System.Windows.Forms;
using System.Linq;
using System.Drawing;
using System.ComponentModel;
using System.Reflection;

namespace JocysCom.ClassLibrary.Controls
{
	public partial class ControlsHelper
	{
		private const int WM_SETREDRAW = 0x000B;

		public static void SuspendDrawing(Control control)
		{
			var msgSuspendUpdate = Message.Create(control.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
			var window = NativeWindow.FromHandle(control.Handle);
			window.DefWndProc(ref msgSuspendUpdate);
		}

		public static void ResumeDrawing(Control control)
		{
			var wparam = new IntPtr(1);
			var msgResumeUpdate = Message.Create(control.Handle, WM_SETREDRAW, wparam, IntPtr.Zero);
			var window = NativeWindow.FromHandle(control.Handle);
			window.DefWndProc(ref msgResumeUpdate);
			control.Invalidate();
		}

		public static void RebindGrid<T>(DataGridView grid, object data, string primaryKeyPropertyName = null, bool selectFirst = true, List<T> selection = null)
		{
			int rowIndex = 0;
			if (grid.Rows.Count > 0) rowIndex = grid.FirstDisplayedCell.RowIndex;
			var sel = (selection == null)
				? GetSelection<T>(grid, primaryKeyPropertyName)
				: selection;
			grid.DataSource = data;
			if (rowIndex != 0 && rowIndex < grid.Rows.Count)
			{
				grid.FirstDisplayedScrollingRowIndex = rowIndex;
			}
			RestoreSelection(grid, primaryKeyPropertyName, sel, selectFirst);
		}

		static public string GetPrimaryKey(EntityObject eo)
		{
			// Try to select primary key name.
			if (eo.EntityKey != null && eo.EntityKey.EntityKeyValues.Length > 0)
			{
				return eo.EntityKey.EntityKeyValues[0].Key;
			}
			// Try to find primary key by [EdmScalarPropertyAttribute] attribute.
			var properties = eo.GetType().GetProperties();
			foreach (var pi in properties)
			{
				var attributes = pi.GetCustomAttributes(true);
				foreach (var attribute in attributes)
				{
					var ea = attribute as EdmScalarPropertyAttribute;
					if (ea != null && ea.EntityKeyProperty)
					{
						return pi.Name;
					}
				}
			}
			return null;
		}

		/// <summary>
		/// Get list of primary keys of items selected in the grid.
		/// </summary>
		/// <typeparam name="T">Type of Primary key.</typeparam>
		/// <param name="grid">Grid for geting selection</param>
		/// <param name="primaryKeyPropertyName">Primary key name.</param>
		public static List<T> GetSelection<T>(DataGridView grid, string primaryKeyPropertyName = null)
		{
			List<T> list = new List<T>();
			var rows = grid.SelectedRows.Cast<DataGridViewRow>().ToArray();
			// If nothing selected then return.
			if (rows.Length == 0)
				return list;
			if (string.IsNullOrEmpty(primaryKeyPropertyName))
			{
				var item = rows.First().DataBoundItem;
				var eo = item as EntityObject;
				if (eo != null)
				{
					primaryKeyPropertyName = GetPrimaryKey(eo);
				}
			}
			for (int i = 0; i < rows.Length; i++)
			{
				var item = rows[i].DataBoundItem;
				var val = GetValue<T>(item, primaryKeyPropertyName);
				list.Add(val);
			}
			return list;
		}

		public static void RestoreSelection<T>(DataGridView grid, string primaryKeyPropertyName, List<T> list, bool selectFirst = true)
		{
			var rows = grid.Rows.Cast<DataGridViewRow>().ToArray();
			// Return if grid is empty.
			if (rows.Length == 0)
				return;
			// If something to restore then...
			if (list.Count > 0)
			{
				if (string.IsNullOrEmpty(primaryKeyPropertyName))
				{
					var item = rows.First().DataBoundItem;
					var eo = item as EntityObject;
					if (eo != null)
					{
						primaryKeyPropertyName = GetPrimaryKey(eo);
					}
				}
				DataGridViewRow firstVisibleRow = null;
				for (int i = 0; i < rows.Length; i++)
				{
					var row = rows[i];
					if ((firstVisibleRow == null && row.Visible))
						firstVisibleRow = row;
					var item = row.DataBoundItem;
					var val = GetValue<T>(item, primaryKeyPropertyName);
					if (list.Contains(val) != row.Selected)
					{
						var selected = list.Contains(val);
						// Select visible rows only, because invisible rows can't be selected or they will throw exception:
						// Row associated with the currency manager's position cannot be made invisible.'
						row.Selected = selected && row.Visible;
					}
				}
			}
			// If must select first row and nothing is selected then...
			if (selectFirst && grid.SelectedRows.Count == 0)
			{
				var firstVisibleRow = rows.FirstOrDefault(x => x.Visible);
				if (firstVisibleRow != null)
				{
					// Select first visible row.
					firstVisibleRow.Selected = true;
				}
			}
		}

		private static T GetValue<T>(object item, string dataPropertyName)
		{
			object val = null;
			if (item is DataRowView)
			{
				var row = ((DataRowView)item).Row;
				if (!row.IsNull(dataPropertyName))
				{
					val = (T)row[dataPropertyName];
				}
			}
			else
			{
				var pi = item.GetType().GetProperty(dataPropertyName);
				val = (T)pi.GetValue(item, null);
			}
			return (T)val;
		}

		#region "UserControl is Visible"

		public static bool IsControlVisibleOnForm(Control control)
		{
			if (control == null) return false;
			if (!control.IsHandleCreated) return false;
			if (control.Parent == null) return false;
			var pointsToCheck = GetPoints(control, true);
			foreach (var p in pointsToCheck)
			{
				var child = control.Parent.GetChildAtPoint(p);
				if (child == null) continue;
				if (control == child || control.Contains(child)) return true;
			}
			return false;
		}

		public static Point[] GetPoints(Control control, bool relative = false)
		{
			var pos = relative
				? System.Drawing.Point.Empty
				// Get control position on the screen
				: control.PointToScreen(System.Drawing.Point.Empty);
			var pointsToCheck =
				new Point[]
					{
						// Top-Left.
						pos,
						// Top-Right.
						new Point(pos.X + control.Width - 1, pos.Y),
						// Bottom-Left.
						new Point(pos.X, pos.Y + control.Height - 1),
						// Bottom-Right.
						new Point(pos.X + control.Width - 1, pos.Y + control.Height - 1),
						// Middle-Centre.
						new Point(pos.X + control.Width/2, pos.Y + control.Height/2)
					};
			return pointsToCheck;
		}

		public static bool IsControlVisibleToUser(Control control)
		{
			if (!control.IsHandleCreated) return false;
			var pointsToCheck = GetPoints(control);
			foreach (var p in pointsToCheck)
			{
				var hwnd = JocysCom.ClassLibrary.Win32.NativeMethods.WindowFromPoint(p);
				var other = Control.FromChildHandle(hwnd);
				if (other == null) continue;
				if (GetAll(control, null, true).Contains(other)) return true;
			}
			return false;
		}

		/// <summary>
		/// Get all child controls.
		/// </summary>
		public static IEnumerable<Control> GetAll(Control control, Type type = null, bool includeTop = false)
		{
			// Get all child controls.
			var controls = control.Controls.Cast<Control>();
			return controls
				// Get children controls and flatten resulting sequences into one sequence.
				.SelectMany(x => GetAll(x))
				// Merge controls with their children.
				.Concat(controls)
				// Include top control if required.
				.Concat(includeTop ? new[] { control } : new Control[0])
				// Filter controls by type.
				.Where(x => type == null || (type.IsInterface ? x.GetType().GetInterfaces().Contains(type) : type.IsAssignableFrom(x.GetType())));
		}

		/// <summary>
		/// Get all child controls.
		/// </summary>
		public static T[] GetAll<T>(Control control, bool includeTop = false)
		{
			if (control == null) return new T[0];
			var type = typeof(T);
			// Get all child controls.
			var controls = control.Controls.Cast<Control>();
			// Get children of controls and flatten resulting sequences into one sequence.
			var result = controls.SelectMany(x => GetAll(x)).ToArray();
			// Merge controls with their children.
			result = result.Concat(controls).ToArray();
			// Include top control if required.
			if (includeTop) result = result.Concat(new[] { control }).ToArray();
			// Filter controls by type.
			result = type.IsInterface
				? result.Where(x => x.GetType().GetInterfaces().Contains(type)).ToArray()
				: result.Where(x => type.IsAssignableFrom(x.GetType())).ToArray();
			// Cast to required type.
			var result2 = result.Select(x => (T)(object)x).ToArray();
			return result2;
		}

		#endregion

		#region Set Visible, Enabled and Text

		internal const int STATE_VISIBLE = 0x00000002;
		internal const int STATE_ENABLED = 0x00000004;

		static MethodInfo _GetState;

		public static void SetVisible(Control control, bool visible)
		{
			_GetState = _GetState ?? typeof(Control).GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic);
			// Can't check property directly, because it will return false if parent is not visible.
			bool stateValue = (bool)_GetState.Invoke(control, new object[] { STATE_VISIBLE });
			if (stateValue != visible) control.Visible = visible;
		}

		public static void SetEnabled(ToolStripItem control, bool enabled)
		{
			if (control.Enabled != enabled) control.Enabled = enabled;
		}

		public static void SetEnabled(Control control, bool enabled)
		{
			_GetState = _GetState ?? typeof(Control).GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic);
			// Can't check property directly, because it will return false if parent is not enabled.
			bool stateValue = (bool)_GetState.Invoke(control, new object[] { STATE_ENABLED });
			if (stateValue != enabled) control.Enabled = enabled;
		}

		public static void SetText(Control control, string text)
		{
			if (control.Text != text)
			{
				control.Text = text;
			}
		}

		public static void SetReadOnly(Control control, bool readOnly)
		{
			var p = control.GetType().GetProperty("ReadOnly");
			if (p == null || !p.CanWrite)
				return;
			var value = (bool)p.GetValue(control, null);
			if (value != readOnly)
			{
				p.SetValue(control, readOnly, null);
			}
		}

		#endregion

		#region Add Grip to SplitContainer 

		public static void ApplySplitterStyle(SplitContainer control)
		{
			// Paint 3 dots on the splitter.
			control.Paint += SplitContainer_Paint;
			// Remove focus from splitter after it moved.
			control.SplitterMoved += SplitContainer_SplitterMoved;
		}

		static void SplitContainer_SplitterMoved(object sender, SplitterEventArgs e)
		{
			var s = sender as Control;
			if (s.CanFocus)
			{
				while (true)
				{
					s = s.Parent;
					if (s == null)
						return;
					if (s.CanFocus)
						s.Focus();
				}
			}
		}

		static void SplitContainer_Paint(object sender, PaintEventArgs e)
		{
			// base.OnPaint(e);
			var s = sender as SplitContainer;
			// Paint the three dots.
			Point[] points = new Point[3];
			var w = s.Width;
			var h = s.Height;
			var d = s.SplitterDistance;
			var sW = s.SplitterWidth;
			int x;
			int y;
			int spacing = 10;
			// Calculate the position of the points.
			if (s.Orientation == Orientation.Horizontal)
			{
				x = (w / 2);
				y = d + (sW / 2);
				points[0] = new Point(x, y);
				points[1] = new Point(x - spacing, y);
				points[2] = new Point(x + spacing, y);
			}
			else
			{
				x = d + (sW / 2);
				y = (h / 2);
				points[0] = new Point(x, y);
				points[1] = new Point(x, y - spacing);
				points[2] = new Point(x, y + spacing);
			}
			foreach (Point p in points)
			{
				p.Offset(-2, -2);
				e.Graphics.FillEllipse(SystemBrushes.ControlDark, new Rectangle(p, new Size(3, 3)));
				p.Offset(1, 1);
				e.Graphics.FillEllipse(SystemBrushes.ControlLight, new Rectangle(p, new Size(3, 3)));
			}
		}

		#endregion

		#region Apply Grid Border Style

		public static void ApplyBorderStyle(DataGridView grid)
		{
			grid.BackgroundColor = Color.White;
			grid.BorderStyle = BorderStyle.None;
			grid.EnableHeadersVisualStyles = false;
			grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
			grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
			grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
			grid.RowHeadersDefaultCellStyle.BackColor = SystemColors.Control;
			grid.BackColor = SystemColors.Window;
			grid.DefaultCellStyle.BackColor = SystemColors.Window;
			grid.CellPainting += Grid_CellPainting;
			grid.SelectionChanged += Grid_SelectionChanged;
			grid.CellFormatting += Grid_CellFormatting;
		}

		private static void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
		{
			if (e.RowIndex == -1) return;

			var grid = (DataGridView)sender;
			var row = grid.Rows[e.RowIndex];
			if (e.RowIndex > -1 && e.ColumnIndex > -1)
			{
				var enabled = GetEnabled(row.DataBoundItem);
				var fore = enabled ? grid.DefaultCellStyle.ForeColor : SystemColors.ControlDark;
				var selectedBack = enabled ? grid.DefaultCellStyle.SelectionBackColor : SystemColors.ControlDark;
				// Apply style to row header.
				if (row.HeaderCell.Style.ForeColor != fore)
					row.HeaderCell.Style.ForeColor = fore;
				if (row.HeaderCell.Style.SelectionBackColor != selectedBack)
					row.HeaderCell.Style.SelectionBackColor = selectedBack;
				// Apply style to cell
				var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
				if (cell.Style.ForeColor != fore)
					cell.Style.ForeColor = fore;
				if (cell.Style.SelectionBackColor != selectedBack)
					cell.Style.SelectionBackColor = selectedBack;
			}
		}

		private static void Grid_SelectionChanged(object sender, EventArgs e)
		{
			// Sort issue with paint artifcats.
			var grid = (DataGridView)sender;
			grid.Invalidate();
		}

		static bool GetEnabled(object item)
		{
			var enabledProperty = item.GetType().GetProperties().FirstOrDefault(x => x.Name == "Enabled" || x.Name == "IsEnabled");
			var enabled = enabledProperty == null ? true : (bool)enabledProperty.GetValue(item, null);
			return enabled;
		}

		private static void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
		{
			// Header and cell borders must be set to "Single" style.
			var grid = (DataGridView)sender;
			var firstVisibleColumn = grid.Columns.Cast<DataGridViewColumn>().Where(x => x.Displayed).Min(x => x.Index);
			var lastVisibleColumn = grid.Columns.Cast<DataGridViewColumn>().Where(x => x.Displayed).Max(x => x.Index);
			var selected = e.RowIndex > -1 ? grid.Rows[e.RowIndex].Selected : false;
			e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.Border);
			var bounds = e.CellBounds;
			var tl = new Point(bounds.X, bounds.Y);
			var tr = new Point(bounds.X + bounds.Width - 1, bounds.Y);
			var bl = new Point(bounds.X, bounds.Y + bounds.Height - 1);
			var br = new Point(bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1);
			Color backColor;
			// If top left corner and column header then...
			if (e.RowIndex == -1)
			{
				backColor = selected
					? grid.ColumnHeadersDefaultCellStyle.SelectionBackColor
					: grid.ColumnHeadersDefaultCellStyle.BackColor;
			}
			// If row header then...
			else if (e.ColumnIndex == -1 && e.RowIndex > -1)
			{
				var row = grid.Rows[e.RowIndex];
				backColor = selected
					? row.HeaderCell.Style.SelectionBackColor
					: grid.RowHeadersDefaultCellStyle.BackColor;
			}
			// If normal cell then...
			else
			{
				var row = grid.Rows[e.RowIndex];
				var cell = row.Cells[e.ColumnIndex];
				backColor = selected
					? cell.InheritedStyle.SelectionBackColor
					: cell.InheritedStyle.BackColor;
			}
			// Cell background colour.
			var back = new Pen(backColor, 1);
			// Border colour.
			var border = new Pen(SystemColors.Control, 1);
			// Do not draw borders for selected device.
			Pen c;
			// Top
			e.Graphics.DrawLine(back, tl, tr);
			// Left (only if not first)
			c = !selected && e.ColumnIndex > firstVisibleColumn ? border : back;
			e.Graphics.DrawLine(c, bl, tl);
			// Right (always)
			c = back;
			e.Graphics.DrawLine(c, tr, br);
			// Bottom (always)
			c = border;
			e.Graphics.DrawLine(c, bl, br);
			back.Dispose();
			border.Dispose();
			e.Handled = true;
		}

		#endregion

		#region  Apply ToolStrip Border Style

		public static void ApplyBorderStyle(ToolStrip control)
		{
			control.Renderer = new ToolStripBorderlessRenderer();
		}

		#endregion

		#region IsDesignMode

		public static bool _IsDesignMode(IComponent component, IComponent parent)
		{
			// Check 1.
			if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
				return true;
			// Check 2 (DesignMode).
			var site = component.Site;
			if (site != null && site.DesignMode)
				return true;
			if (parent != null && parent.GetType().FullName.Contains("VisualStudio"))
				return true;
			// Not design mode.
			return false;
		}

		public static bool IsDesignMode(Component component)
		{
			var form = component as Form;
			if (form != null)
				return _IsDesignMode(form, form.ParentForm ?? form.Owner);
			var control = component as Control;
			if (control != null)
				return _IsDesignMode(control, control.Parent);
			return _IsDesignMode(component, null);
		}

		#endregion

	}
}
