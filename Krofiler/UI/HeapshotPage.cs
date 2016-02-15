using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Eto.Forms;

namespace Krofiler
{
	public class HeapshotPage : StackLayout
	{
		readonly KrofilerSession session;
		readonly Heapshot heapshot;
		GridView typesGrid;
		GridView objectsGrid;
		TextBox filterTypesTextBox;
		FilterCollection<Tuple<long, string, int>> typesCollection = new FilterCollection<Tuple<long, string, int>>();
		string typeNameFilter;
		ObservableCollection<Dummy> objectsCollection = new ObservableCollection<Dummy>();
		ObjectDetailsPanel objectPanel;

		public HeapshotPage(KrofilerSession session, Heapshot heapshot)
		{
			this.session = session;
			this.heapshot = heapshot;
			this.Orientation = Orientation.Horizontal;
			CreateTypesView();
			filterTypesTextBox = new TextBox();
			filterTypesTextBox.TextChanged += FilterTypesTextBox_TextChanged;

			var filterAndTypesStackLayout = new StackLayout();
			filterAndTypesStackLayout.Items.Add(new StackLayoutItem(filterTypesTextBox, HorizontalAlignment.Stretch));
			filterAndTypesStackLayout.Items.Add(new StackLayoutItem(typesGrid, HorizontalAlignment.Stretch, true));

			this.Items.Add(new StackLayoutItem(filterAndTypesStackLayout, VerticalAlignment.Stretch, true));
			CreateObjectsView();
			this.Items.Add(new StackLayoutItem(objectsGrid, VerticalAlignment.Stretch));
			objectPanel = new ObjectDetailsPanel(session, heapshot);
			this.Items.Add(new StackLayoutItem(objectPanel, VerticalAlignment.Stretch, true));
		}

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
			foreach (var type in heapshot.TypesToObjectsListMap) {
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
			foreach (var id in heapshot.TypesToObjectsListMap[selectedItem.Item1]) {
				objectsCollection.Add(new Dummy(id));
			}
		}

		void CreateObjectsView()
		{
			objectsGrid = new GridView() {
				DataStore = objectsCollection
			};
			objectsGrid.AllowMultipleSelection = false;


			objectsGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<Dummy, string>(r => r.id.ToString()) },
				HeaderText = "Object Id"
			});

			objectsGrid.SelectedRowsChanged += ObjectsGrid_SelectedRowsChanged;
		}

		void ObjectsGrid_SelectedRowsChanged(object sender, EventArgs e)
		{
			var selectedItem = objectsGrid.SelectedItem as Dummy;
			if (selectedItem == null) {
				return;
			}
			objectPanel.ObjectId = selectedItem.id;
		}

		class Dummy
		{
			public readonly long id;

			public Dummy(long id)
			{
				this.id = id;
			}
		}
	}
}

