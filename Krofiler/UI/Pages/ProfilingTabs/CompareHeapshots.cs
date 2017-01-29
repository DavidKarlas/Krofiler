using System;
using System.Collections.Generic;
using Eto.Forms;
using System.Collections;
using System.Linq;
using Krofiler.Reader;
using System.Diagnostics;

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
			public ushort TypeId;
			public List<long> NewObjects;
			public List<long> CollectedObjects;
			public List<long> NewHsObjects;
			public List<long> OldHsObjects;
		}
		TextBox filterTypesTextBox;

		static List<long> EmptyList = new List<long>();

		internal static IEnumerable<long> GetObjects(Heapshot oldHeapshot, Heapshot newHeapshot, bool newOrCollected)
		{
			var oldHeapshotClone = new Heapshot(oldHeapshot.Id, oldHeapshot.Session, oldHeapshot.AllocsAndMovesStartPosition);
			foreach (var item in oldHeapshot) {
				oldHeapshotClone.Add(item.Key, item.Value);
			}
			//for (int i = oldHeapshot.AllocsAndMovesStartPosition; i < newHeapshot.AllocsAndMovesStartPosition; i++) {
			//	var obj = newHeapshot.Session.AllAllocsAndMoves[i];
			//	if (obj is HeapAlloc) {
			//		Debug.WriteLine($"{((HeapAlloc)obj).Address:X}");
			//	} else if (obj is HeapMoves) {
			//		var movs = ((HeapMoves)obj).Moves;
			//		for (int j = 0; j < movs.Length; j += 2) {
			//			Debug.WriteLine($"{j} {movs[j]:X} - {movs[j + 1]:X}");
			//		}
			//	}
			//}
			for (int i = oldHeapshot.AllocsAndMovesStartPosition; i < newHeapshot.AllocsAndMovesStartPosition; i++) {
				var obj = newHeapshot.Session.AllAllocsAndMoves[i];
				if (obj is HeapAlloc) {
					oldHeapshotClone[((HeapAlloc)obj).Address] = new HeapObject(((HeapAlloc)obj).Address, ((HeapAlloc)obj).AllocStack);
				} else if (obj is HeapMoves) {
					var movs = ((HeapMoves)obj).Moves;
					for (int j = 0; j < movs.Length; j += 2) {
						HeapObject hObj;
						if (oldHeapshotClone.TryGetValue(movs[j], out hObj))
							oldHeapshotClone[movs[j + 1]] = hObj;
						//oldHeapshotClone[movs[j + 1]] = oldHeapshotClone[movs[j]];
					}
				}
			}
			if (newOrCollected) {
				foreach (var item in newHeapshot.Keys) {
					HeapObject ob;
					if (oldHeapshotClone.TryGetValue(item, out ob)) {
						if (ob.ClassId == 0) {
							newHeapshot[item].allocStack = ob.allocStack;
							newHeapshot[item].AllocAddress = ob.Address;
							yield return item;
						}
					} else {
						yield return item;
					}
				}
			} else {
				foreach (var item in oldHeapshotClone) {
					if (!newHeapshot.ContainsKey(item.Key) && item.Value.ClassId != 0)
						yield return item.Value.Address;
				}
			}
		}

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

			var newObjects = GetObjects(oldHeapshot, newHeapshot, true).GroupBy(addr => newHeapshot[addr].ClassId).ToDictionary(d => d.Key, d => d.ToList());
			var collectedObjects = GetObjects(oldHeapshot, newHeapshot, false).GroupBy(addr => oldHeapshot[addr].ClassId).ToDictionary(d => d.Key, d => d.ToList());
			var allInOld = oldHeapshot.GroupBy(addr => addr.Value.ClassId).ToDictionary(d => d.Key, d => d.Select(o => o.Value.Address).ToList());
			var allInNew = newHeapshot.GroupBy(addr => addr.Value.ClassId).ToDictionary(d => d.Key, d => d.Select(o => o.Value.Address).ToList());
			var hashTableAllTypes = new HashSet<ushort>();
			foreach (var t in newObjects)
				hashTableAllTypes.Add(t.Key);
			foreach (var t in collectedObjects)
				hashTableAllTypes.Add(t.Key);
			foreach (var typeId in hashTableAllTypes) {
				typesCollection.Add(new TypeChangeInfo {
					TypeId = typeId,
					TypeName = newHeapshot.GetTypeName(typeId),
					NewObjects = newObjects.ContainsKey(typeId) ? newObjects[typeId] : EmptyList,
					CollectedObjects = collectedObjects.ContainsKey(typeId) ? collectedObjects[typeId] : EmptyList,
					OldHsObjects = allInOld.ContainsKey(typeId) ? allInOld[typeId] : EmptyList,
					NewHsObjects = allInNew.ContainsKey(typeId) ? allInNew[typeId] : EmptyList
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
				typesCollection.Filter = (i) => i.TypeName.Contains(typeNameFilter);
		}

		GridView typesGrid;
		void CreateTypesView()
		{
			typesGrid = new GridView {
				DataStore = typesCollection
			};
			typesGrid.AllowMultipleSelection = false;
			typesCollection.Sort = (x, y) => (y.NewObjects.Count - y.CollectedObjects.Count).CompareTo(x.NewObjects.Count - x.CollectedObjects.Count);
			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<TypeChangeInfo, string>(r => (r.NewObjects.Count - r.CollectedObjects.Count).ToString()) },
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
				DataCell = new TextBoxCell { Binding = Binding.Delegate<TypeChangeInfo, string>(r => r.CollectedObjects.Count.ToString()) },
				HeaderText = "Collected Objects"
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
				InsertTab(new ObjectListTab(session, newHeapshot, new Dictionary<ushort, List<long>>() { { ((TypeChangeInfo)typesGrid.SelectedItem).TypeId, ((TypeChangeInfo)typesGrid.SelectedItem).NewObjects } }), this);
			};
			var collectedObjs = new Command() {
				MenuText = "Select Collected objects"
			};
			collectedObjs.Executed += (sender, e) => {
				InsertTab(new ObjectListTab(session, oldHeapshot, new Dictionary<ushort, List<long>>() { { ((TypeChangeInfo)typesGrid.SelectedItem).TypeId, ((TypeChangeInfo)typesGrid.SelectedItem).CollectedObjects } }), this);
			};
			var newHs = new Command() {
				MenuText = "Select All in New Heapshot"
			};
			newHs.Executed += (sender, e) => {
				InsertTab(new ObjectListTab(session, newHeapshot, new Dictionary<ushort, List<long>>() { { ((TypeChangeInfo)typesGrid.SelectedItem).TypeId, ((TypeChangeInfo)typesGrid.SelectedItem).NewHsObjects } }), this);
			};
			var oldHs = new Command() {
				MenuText = "Select All in Old Heapshot"
			};
			oldHs.Executed += (sender, e) => {
				InsertTab(new ObjectListTab(session, oldHeapshot, new Dictionary<ushort, List<long>>() { { ((TypeChangeInfo)typesGrid.SelectedItem).TypeId, ((TypeChangeInfo)typesGrid.SelectedItem).OldHsObjects } }), this);
			};

			return new ContextMenu(newObjs, collectedObjs, newHs, oldHs);
		}
	}
}

