using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Eto.Forms;
using Mono.Profiler.Log;

namespace Krofiler
{
	public class ObjectListTab : StackLayout, IProfilingTab
	{
		readonly KrofilerSession session;
		readonly Heapshot heapshot;
		GridView typesGrid;
		GridView objectsGrid;
		TextBox filterTypesTextBox;
		FilterCollection<Tuple<long, string, int, long>> typesCollection = new FilterCollection<Tuple<long, string, int, long>>();
		string typeNameFilter;
		ObjectDetailsPanel objectPanel;
		readonly Dictionary<long, LazyObjectsList> typesToObjectsListMap;
		Label sumLabel = new Label();
		GridView countersView = new GridView();
		Splitter splitter = new Splitter();

		public ObjectListTab(KrofilerSession session, Heapshot heapshot, Dictionary<long, LazyObjectsList> typesToObject)
		{
			countersView.DataStore = new ObservableCollection<CountersRow>();
			countersView.Columns.Add(new GridColumn() {
				Resizable = true,
				AutoSize = true,
				Editable = false,
				HeaderText = "Time since start",
				DataCell = new TextBoxCell { Binding = Binding.Delegate<CountersRow, string>(r => r.time.ToString()) },
			});
			countersView.Columns.Add(new GridColumn() {
				Resizable = true,
				AutoSize = true,
				Editable = false,
				HeaderText = "GCResize",
				DataCell = new TextBoxCell { Binding = Binding.Delegate<CountersRow, string>(r => PrettyPrint.PrintBytes(r.GcResize)) },
			});
			if (heapshot.CountersDescriptions != null) {
				foreach (var counterDescription in heapshot.CountersDescriptions) {
					var item = counterDescription.Value;
					countersView.Columns.Add(new GridColumn() {
						Resizable = true,
						AutoSize = false,
						Editable = false,
						Sortable = true,
						HeaderText = item.GetCounterName(session.processor) + $"({(LogCounterUnit)(item.CounterDescriptionsEvent_SectionTypeUnitVariance & (0xF << 24))}) {item.GetSectionName(session.processor)}",
						DataCell = new TextBoxCell {
							Binding = Binding.Delegate<CountersRow, string>(r => {
								if (!r.Counters.ContainsKey(item.CounterDescriptionsEvent_Index))
									return "";
								if (((LogCounterUnit)(item.CounterDescriptionsEvent_SectionTypeUnitVariance & (31 << 24))) == LogCounterUnit.Bytes)
									return PrettyPrint.PrintBytes((long)r.Counters[item.CounterDescriptionsEvent_Index]);
								return r.Counters[item.CounterDescriptionsEvent_Index].ToString();
							}),
						}
					});
				}
			}
			if (heapshot.Counters != null) {
				((ObservableCollection<CountersRow>)countersView.DataStore).Add(heapshot.Counters);
			}
			this.typesToObjectsListMap = typesToObject;
			this.session = session;
			this.heapshot = heapshot;
			CreateTypesView();
			filterTypesTextBox = new TextBox();
			filterTypesTextBox.TextChanged += FilterTypesTextBox_TextChanged;
			splitter.SplitterWidth = 10;
			var filterAndTypesStackLayout = new StackLayout();
			filterAndTypesStackLayout.Items.Add(new StackLayoutItem(filterTypesTextBox, HorizontalAlignment.Stretch));
			filterAndTypesStackLayout.Items.Add(sumLabel);
			filterAndTypesStackLayout.Items.Add(new StackLayoutItem(typesGrid, HorizontalAlignment.Stretch, true));

			splitter.Panel1 = filterAndTypesStackLayout;
			CreateObjectsView();
			var splitter2 = new Splitter() {
				Orientation = Orientation.Horizontal
			};
			splitter2.SplitterWidth = 5;
			splitter2.Panel1 = objectsGrid;
			objectPanel = new ObjectDetailsPanel(session, this.heapshot);
			objectPanel.InsertTab += (a, b) => InsertTab?.Invoke(a, b ?? this);
			splitter2.Panel2 = objectPanel;
			splitter.Panel2 = splitter2;
			splitter.Position = (int)Screen.PrimaryScreen.Bounds.Width / 3;
			this.Items.Add(new StackLayoutItem(splitter, HorizontalAlignment.Stretch, true));
			this.Items.Add(new StackLayoutItem(countersView, HorizontalAlignment.Stretch, false));
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
				typesCollection.Filter = (i) => i.Item2.IndexOf(typeNameFilter, StringComparison.OrdinalIgnoreCase) != -1;
		}

		void CreateTypesView()
		{
			int countSum = 0;
			long sizeSum = 0;
			foreach (var type in typesToObjectsListMap) {
				long size = type.Value.Size;
				int count = type.Value.Count;
				typesCollection.Add(Tuple.Create(type.Key, session.GetTypeName(type.Key), count, size));
				sizeSum += size;
				countSum += count;
			}
			sumLabel.Text = $"Object # sum:{countSum:N0} Size sum:{PrettyPrint.PrintBytes(sizeSum)}";
			typesCollection.Sort = (x, y) => (y.Item4).CompareTo(x.Item4);
			typesGrid = new GridView {
				DataStore = typesCollection
			};
			typesGrid.AllowMultipleSelection = false;

			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<Tuple<long, string, int, long>, string>(r => r.Item3.ToString("N0")) },
				HeaderText = "Objects #"
			});
			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<Tuple<long, string, int, long>, string>(r => PrettyPrint.PrintBytes(r.Item4)) },
				HeaderText = "Size"
			});
			typesGrid.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Delegate<Tuple<long, string, int, long>, string>(r => r.Item2) },
				HeaderText = "Type Name"
			});
			typesGrid.SelectedRowsChanged += Grid_SelectedRowsChanged;
		}

		void Grid_SelectedRowsChanged(object sender, EventArgs e)
		{
			var selectedItem = typesGrid.SelectedItem as Tuple<long, string, int, long>;
			if (selectedItem == null) {
				return;
			}
			objectsGrid.DataStore = typesToObjectsListMap[selectedItem.Item1].CreateList().ToList();
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
				DataCell = new TextBoxCell { Binding = Binding.Delegate<ObjectInfo, string>(r => PrettyPrint.PrintBytes(r.Size)) },
				HeaderText = "Size"
			});

			objectsGrid.SelectedRowsChanged += ObjectsGrid_SelectedRowsChanged;
		}

		void ObjectsGrid_SelectedRowsChanged(object sender, EventArgs e)
		{
			if (objectsGrid.SelectedItem is ObjectInfo obj) {
				objectPanel.Object = obj;
			}
		}
	}
}