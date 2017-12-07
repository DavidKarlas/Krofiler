using System;
using System.Collections.Generic;
using Eto.Forms;
using System.Collections;
using System.Linq;
namespace Krofiler
{
	public class CompareHeapshotsTab : Panel, IProfilingTab
	{
		readonly KrofilerSession session;
		readonly Heapshot newHeapshot;
		readonly Heapshot oldHeapshot;

		FilterCollection<TypeChangeInfo> typesCollection = new FilterCollection<TypeChangeInfo>();

		public string Title {
			get {
				return "Compare";
			}
		}

		public string Details {
			get {
				return oldHeapshot.Name + " " + newHeapshot.Name;
			}
		}

		public Control TabContent {
			get {
				return this;
			}
		}

		public event InsertTabDelegate InsertTab;

		class TypeChangeInfo
		{
			public string TypeName;
			public long TypeId;
			public List<ObjectInfo> NewObjects;
			public List<ObjectInfo> DeadObjects;
			public List<ObjectInfo> NewHsObjects;
			public List<ObjectInfo> OldHsObjects;
		}
		TextBox filterTypesTextBox;

		static List<ObjectInfo> EmptyList = new List<ObjectInfo>();

		public CompareHeapshotsTab(KrofilerSession session, Heapshot hs1, Heapshot hs2)
		{
			this.session = session;
			if (hs2.Id > hs1.Id) {
				newHeapshot = hs2;
				oldHeapshot = hs1;
			} else {
				newHeapshot = hs1;
				oldHeapshot = hs2;
			}
			var diff = new DiffHeap(oldHeapshot, newHeapshot);
			var newObjects = diff.NewObjects.GroupBy(addr => addr.TypeId).ToDictionary(d => d.Key, d => d.ToList());
			var deleted = diff.DeletedObjects.GroupBy(addr => addr.TypeId).ToDictionary(d => d.Key, d => d.ToList());
			var allObjectsInOldHs = oldHeapshot.TypesToObjectsListMap;
			var allObjectsInNewHs = newHeapshot.TypesToObjectsListMap;
			var hashTableAllTypes = new HashSet<long>();
			foreach (var t in allObjectsInOldHs)
				hashTableAllTypes.Add(t.Key);
			foreach (var t in allObjectsInNewHs)
				hashTableAllTypes.Add(t.Key);
			foreach (var typeId in hashTableAllTypes) {
				typesCollection.Add(new TypeChangeInfo {
					TypeId = typeId,
					TypeName = session.GetTypeName(typeId),
					NewObjects = newObjects.ContainsKey(typeId) ? newObjects[typeId] : EmptyList,
					DeadObjects = deleted.ContainsKey(typeId) ? deleted[typeId] : EmptyList,
					OldHsObjects = allObjectsInOldHs.ContainsKey(typeId) ? allObjectsInOldHs[typeId] : EmptyList,
					NewHsObjects = allObjectsInNewHs.ContainsKey(typeId) ? allObjectsInNewHs[typeId] : EmptyList
				});
			}
			filterTypesTextBox = new TextBox();
			filterTypesTextBox.TextChanged += FilterTypesTextBox_TextChanged;
			CreateTypesView();
			var filterAndTypesStackLayout = new StackLayout();
			filterAndTypesStackLayout.Items.Add(new StackLayoutItem(filterTypesTextBox, HorizontalAlignment.Stretch));
			filterAndTypesStackLayout.Items.Add(new StackLayoutItem(typesGrid, HorizontalAlignment.Stretch, true));

			Content = filterAndTypesStackLayout;
		}

		void FilterTypesTextBox_TextChanged(object sender, EventArgs e)
		{
			var typeNameFilter = filterTypesTextBox.Text;
			if (string.IsNullOrWhiteSpace(typeNameFilter))
				typesCollection.Filter = null;
			else
				typesCollection.Filter = (i) => i.TypeName.IndexOf (typeNameFilter, StringComparison.OrdinalIgnoreCase) != -1;
		}

		GridView typesGrid;
		void CreateTypesView()
		{
			typesGrid = new GridView {
				DataStore = typesCollection
			};
			typesGrid.AllowMultipleSelection = false;
			typesCollection.Sort = (x, y) => (y.NewObjects.Count - y.DeadObjects.Count).CompareTo(x.NewObjects.Count - x.DeadObjects.Count);
			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<TypeChangeInfo, string>(r => (r.NewObjects.Count - r.DeadObjects.Count).ToString()) },
				HeaderText = "Diff"
			});
			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<TypeChangeInfo, string>(r => r.NewHsObjects.Count.ToString()) },
				HeaderText = "Objects"
			});
			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<TypeChangeInfo, string>(r => r.NewObjects.Count.ToString()) },
				HeaderText = "New Objects"
			});
			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<TypeChangeInfo, string>(r => r.DeadObjects.Count.ToString()) },
				HeaderText = "Dead Objects"
			});
			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<TypeChangeInfo, string>(r => r.TypeName) },
				HeaderText = "Type Name"
			});
			typesGrid.ContextMenu = CreateContextMenu();
		}

		ContextMenu CreateContextMenu()
		{
			var newObjs = new Command() {
				MenuText = "Select New objects"
			};
			newObjs.Executed += (sender, e) => {
				if (typesGrid.SelectedItem == null) {
					MessageBox.Show("Select item in list before right-clicking(I know, I know)...");
					return;
				}
				InsertTab(new ObjectListTab(session, newHeapshot, new Dictionary<long, List<ObjectInfo>>() { { ((TypeChangeInfo)typesGrid.SelectedItem).TypeId, ((TypeChangeInfo)typesGrid.SelectedItem).NewObjects } }), this);
			};
			var deadObjs = new Command() {
				MenuText = "Select Dead objects"
			};
			deadObjs.Executed += (sender, e) => {
				if (typesGrid.SelectedItem == null) {
					MessageBox.Show("Select item in list before right-clicking(I know, I know)...");
					return;
				}
				InsertTab(new ObjectListTab(session, oldHeapshot, new Dictionary<long, List<ObjectInfo>>() { { ((TypeChangeInfo)typesGrid.SelectedItem).TypeId, ((TypeChangeInfo)typesGrid.SelectedItem).DeadObjects } }), this);
			};
			var newHs = new Command() {
				MenuText = "Select All in New Heapshot"
			};
			newHs.Executed += (sender, e) => {
				if (typesGrid.SelectedItem == null) {
					MessageBox.Show("Select item in list before right-clicking(I know, I know)...");
					return;
				}
				InsertTab(new ObjectListTab(session, newHeapshot, new Dictionary<long, List<ObjectInfo>>() { { ((TypeChangeInfo)typesGrid.SelectedItem).TypeId, ((TypeChangeInfo)typesGrid.SelectedItem).NewHsObjects } }), this);
			};
			var oldHs = new Command() {
				MenuText = "Select All in Old Heapshot"
			};
			oldHs.Executed += (sender, e) => {
				if (typesGrid.SelectedItem == null) {
					MessageBox.Show("Select item in list before right-clicking(I know, I know)...");
					return;
				}
				InsertTab(new ObjectListTab(session, oldHeapshot, new Dictionary<long, List<ObjectInfo>>() { { ((TypeChangeInfo)typesGrid.SelectedItem).TypeId, ((TypeChangeInfo)typesGrid.SelectedItem).OldHsObjects } }), this);
			};

			return new ContextMenu(newObjs, deadObjs, newHs, oldHs);
		}
	}
}

