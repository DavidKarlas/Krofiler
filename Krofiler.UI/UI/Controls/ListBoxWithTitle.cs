using System;
using Eto.Forms;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Krofiler
{
	public class ListBoxWithTitle : StackLayout
	{
		public event Action<string> ActivatedItem;
		public ListBoxWithTitle(string title, IEnumerable<string> filenames)
		{
			var listbox = new ListBox();
			listbox.MouseDoubleClick += delegate {
				ActivatedItem?.Invoke(listbox.SelectedKey);
			};
			listbox.Items.AddRange(filenames.Where(p => Directory.Exists(p) || File.Exists(p)).Select(s => new ListItem() {
				Text = s,
				Key = s
			}));
			Items.Add(new Label() {
				Text = title
			});
			Items.Add(new StackLayoutItem(listbox, HorizontalAlignment.Stretch, true));
		}
	}
}

