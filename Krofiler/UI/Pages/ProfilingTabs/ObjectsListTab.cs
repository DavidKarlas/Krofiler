using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Eto.Forms;

namespace Krofiler
{
	public class ObjectListTab : StackLayout, IProfilingTab
	{
		readonly KrofilerSession session;
		readonly Heapshot heapshot;
		GridView typesGrid;
		GridView objectsGrid;
		TextBox filterTypesTextBox;
		FilterCollection<Tuple<ushort, string, int>> typesCollection = new FilterCollection<Tuple<ushort, string, int>>();
		string typeNameFilter;
		ObservableCollection<ObjectInfo> objectsCollection = new ObservableCollection<ObjectInfo>();
		ObjectDetailsPanel objectPanel;
		readonly Dictionary<ushort, List<long>> typesToObjectsListMap;

		StackLayout filterAndTypesStackLayout;
		public ObjectListTab(KrofilerSession session, Heapshot heapshot, Dictionary<ushort, List<long>> typesToObjectsListMap)
		{
			this.typesToObjectsListMap = typesToObjectsListMap;
			this.session = session;
			this.heapshot = heapshot;
			this.Orientation = Orientation.Horizontal;
			CreateTypesView();
			filterTypesTextBox = new TextBox();
			filterTypesTextBox.TextChanged += FilterTypesTextBox_TextChanged;

			filterAndTypesStackLayout = new StackLayout();
			filterAndTypesStackLayout.Items.Add(new StackLayoutItem(filterTypesTextBox, HorizontalAlignment.Stretch));
			filterAndTypesStackLayout.Items.Add(new StackLayoutItem(typesGrid, HorizontalAlignment.Stretch, true));
			filterAndTypesStackLayout.Width = this.Width / 3;
			this.Items.Add(new StackLayoutItem(filterAndTypesStackLayout, VerticalAlignment.Stretch));
			CreateObjectsView();
			this.Items.Add(new StackLayoutItem(objectsGrid, VerticalAlignment.Stretch));
			objectPanel = new ObjectDetailsPanel(session, this.heapshot);
			this.Items.Add(new StackLayoutItem(objectPanel, HorizontalAlignment.Stretch, true));
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			base.OnSizeChanged(e);
			filterAndTypesStackLayout.Width = this.Width / 3;
		}

		public string Title {
			get {
				return "Objects list";
			}
		}

		public string Details {
			get {
				return "";
			}
		}

		public Control TabContent {
			get {
				return this;
			}
		}

		public event InsertTabDelegate InsertTab;

		void FilterTypesTextBox_TextChanged(object sender, EventArgs e)
		{
			typeNameFilter = filterTypesTextBox.Text;
			if (string.IsNullOrWhiteSpace(typeNameFilter))
				typesCollection.Filter = null;
			else
				typesCollection.Filter = (i) => i.Item2.Contains(typeNameFilter);
		}

		void CreateTypesView()
		{
			foreach (var type in typesToObjectsListMap.OrderByDescending(t => t.Value.Count)) {
				typesCollection.Add(Tuple.Create(type.Key, heapshot.GetTypeName(type.Key), type.Value.Count));
			}
			typesGrid = new GridView {
				DataStore = typesCollection
			};
			typesGrid.AllowMultipleSelection = false;

			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<Tuple<ushort, string, int>, string>(r => r.Item3.ToString()) },
				HeaderText = "Objects #"
			});
			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<Tuple<ushort, string, int>, string>(r => r.Item2) },
				HeaderText = "Type Name"
			});

			typesGrid.SelectedRowsChanged += Grid_SelectedRowsChanged;
		}

		void Grid_SelectedRowsChanged(object sender, EventArgs e)
		{
			var selectedItem = typesGrid.SelectedItem as Tuple<ushort, string, int>;
			objectsCollection.Clear();
			if (selectedItem == null) {
				return;
			}
			objectsGrid.DataStore = null;
			foreach (var id in typesToObjectsListMap[selectedItem.Item1]) {
				objectsCollection.Add(new ObjectInfo(id, session.allocs.ContainsKey(id) ? TimeSpan.FromTicks(session.allocs[id].Item1 * 10) : TimeSpan.FromTicks(0)));
			}
			objectsGrid.DataStore = objectsCollection;
		}

		void CreateObjectsView()
		{
			objectsGrid = new GridView() {
				DataStore = objectsCollection
			};
			objectsGrid.ContextMenu = new ContextMenu(new ButtonMenuItem(new Command((sender, e) => {
				using (var clipboard = new Clipboard())
					clipboard.Text = "0x" + ((ObjectInfo)objectsGrid.SelectedItem).Id.ToString("X");
			}) {
				MenuText = "Copy"
			}));
			objectsGrid.AllowMultipleSelection = false;


			objectsGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<ObjectInfo, string>(r => "0x" + r.Id.ToString("X")) },
				HeaderText = "Address"
			});
			objectsGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<ObjectInfo, string>(r => r.Time.ToString("G")) },
				HeaderText = "Time"
			});

			objectsGrid.SelectedRowsChanged += ObjectsGrid_SelectedRowsChanged;
		}

		void ObjectsGrid_SelectedRowsChanged(object sender, EventArgs e)
		{
			var selectedItem = objectsGrid.SelectedItem as ObjectInfo;
			if (selectedItem == null) {
				return;
			}
			objectPanel.ObjectId = selectedItem.Id;
		}

		class ObjectInfo
		{
			public readonly long Id;
			public readonly TimeSpan Time;

			public ObjectInfo(long id, TimeSpan time)
			{
				this.Id = id;
				this.Time = time;
			}
		}
	}
}

