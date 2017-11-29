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
		ObjectDetailsPanel objectPanel;
		readonly Dictionary<long, List<ObjectInfo>> typesToObjectsListMap;

		public ObjectListTab(KrofilerSession session, Heapshot heapshot, Dictionary<long, List<ObjectInfo>> typesToObjectsListMap)
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
			filterAndTypesStackLayout.Width = (int)Screen.PrimaryScreen.Bounds.Width / 2;
			this.Items.Add(new StackLayoutItem(filterAndTypesStackLayout, VerticalAlignment.Stretch));
			CreateObjectsView();
			this.Items.Add(new StackLayoutItem(objectsGrid, VerticalAlignment.Stretch));
			objectPanel = new ObjectDetailsPanel(session, this.heapshot);
			objectPanel.InsertTab += (a, b) => InsertTab?.Invoke(a, b ?? this);
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
			if (selectedItem == null) {
				return;
			}
			objectsGrid.DataStore = typesToObjectsListMap[selectedItem.Item1];
			objectsGrid.SelectedRows = Array.Empty<int>();
			objectsGrid.SelectRow(0);
		}

		void CreateObjectsView()
		{
			objectsGrid = new GridView();
			objectsGrid.AllowMultipleSelection = false;


			objectsGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<ObjectInfo, string>(r => r.ObjAddr.ToString()) },
				HeaderText = "Object Id"
			});
			objectsGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<ObjectInfo, string>(r => r.Allocation.Timestamp.ToString()) },
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
			objectPanel.Object = selectedItem;
		}
	}
}

