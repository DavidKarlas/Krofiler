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
		FilterCollection<Tuple<long, string, int>> typesCollection = new FilterCollection<Tuple<long, string, int>>();
		string typeNameFilter;
		ObservableCollection<ObjectInfo> objectsCollection = new ObservableCollection<ObjectInfo>();
		ObjectDetailsPanel objectPanel;
		readonly Dictionary<long, List<long>> typesToObjectsListMap;

		public ObjectListTab(KrofilerSession session, Heapshot heapshot, Dictionary<long, List<long>> typesToObjectsListMap)
		{
			this.typesToObjectsListMap = typesToObjectsListMap;
			this.session = session;
			this.heapshot = heapshot;
			this.Orientation = Orientation.Horizontal;
			CreateTypesView();
			filterTypesTextBox = new TextBox();
			filterTypesTextBox.TextChanged += FilterTypesTextBox_TextChanged;

			var filterAndTypesStackLayout = new StackLayout();
			filterAndTypesStackLayout.Items.Add(new StackLayoutItem(filterTypesTextBox, HorizontalAlignment.Stretch));
			filterAndTypesStackLayout.Items.Add(new StackLayoutItem(typesGrid, HorizontalAlignment.Stretch, true));

			this.Items.Add(new StackLayoutItem(filterAndTypesStackLayout, VerticalAlignment.Stretch));
			CreateObjectsView();
			this.Items.Add(new StackLayoutItem(objectsGrid, VerticalAlignment.Stretch));
			objectPanel = new ObjectDetailsPanel(session, this.heapshot);
			this.Items.Add(new StackLayoutItem(objectPanel, VerticalAlignment.Stretch, true));
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
			foreach (var type in typesToObjectsListMap) {
				typesCollection.Add(Tuple.Create(type.Key, session.GetTypeName(type.Key), type.Value.Count));
			}
			typesGrid = new GridView {
				DataStore = typesCollection
			};
			typesGrid.AllowMultipleSelection = false;

			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<Tuple<long, string, int>, string>(r => r.Item3.ToString()) },
				HeaderText = "Objects #"
			});

			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<Tuple<long, string, int>, string>(r => r.Item2) },
				HeaderText = "Type Name"
			});

			typesGrid.SelectedRowsChanged += Grid_SelectedRowsChanged;
		}

		void Grid_SelectedRowsChanged(object sender, EventArgs e)
		{
			var selectedItem = typesGrid.SelectedItem as Tuple<long, string, int>;
			objectsCollection.Clear();
			if (selectedItem == null) {
				return;
			}
			foreach (var id in typesToObjectsListMap[selectedItem.Item1]) {
				objectsCollection.Add(new ObjectInfo(id, session.allocs.ContainsKey(id) ? TimeSpan.FromTicks(session.allocs[id].Item1 * 10) : TimeSpan.FromTicks(0)));
			}
		}

		void CreateObjectsView()
		{
			objectsGrid = new GridView() {
				DataStore = objectsCollection
			};
			objectsGrid.AllowMultipleSelection = false;


			objectsGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<ObjectInfo, string>(r => r.Id.ToString()) },
				HeaderText = "Object Id"
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

