using System;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using System.Collections.Generic;
namespace Krofiler
{
	public class StartProfilingInfo
	{

	}

	public class StartProfilingFromFileInfo : StartProfilingInfo
	{
		public string KrofFilePath { get; set; }
	}

	public class StartProfilingProcessInfo : StartProfilingInfo
	{
		public string ExePath { get; set; }
	}

	public delegate void InsertTabDelegate(IProfilingTab newTab, IProfilingTab after);

	public interface IProfilingTab
	{
		string Title { get; }
		string Details { get; }
		Control TabContent { get; }
		event InsertTabDelegate InsertTab;
	}

	public class ProfilingPage : StackLayout
	{
		StackLayout TabsOnLeft;
		Panel PlaceHolderForTabContent;
		public ProfilingPage()
		{
			Orientation = Orientation.Horizontal;
			TabsOnLeft = new StackLayout();
			Items.Add(new StackLayoutItem(TabsOnLeft));
			PlaceHolderForTabContent = new Panel();
			Items.Add(new StackLayoutItem(PlaceHolderForTabContent, VerticalAlignment.Stretch, true));
		}

		class TabView : StackLayout
		{
			public IProfilingTab Tab { get; }
			public TabView(IProfilingTab tab)
			{
				Tab = tab;
				Items.Add(new Label {
					Text = tab.Title
				});
				Items.Add(new Label {
					Text = tab.Details,
					Font = Fonts.Sans(10)
				});
				Height = 40;
				Width = 90;
			}

			public event Action<TabView> Clicked;

			protected override void OnMouseUp(MouseEventArgs e)
			{
				Clicked?.Invoke(this);
				base.OnMouseUp(e);
			}
		}

		public void StartProfiling(StartProfilingInfo profilingInfo)
		{
			var homeTab = new HomeTab();
			InsertTab(homeTab, null);
			homeTab.StartProfiling(profilingInfo);
		}

		public void InsertTab(IProfilingTab newTab, IProfilingTab after)
		{
			if (newTab == null)
				throw new ArgumentNullException(nameof(newTab));
			if (after == null && newTab.GetType() != typeof(HomeTab))//We don't want anyone to remove Home tab
				throw new ArgumentNullException(nameof(after));
			if (after != null && !Tabs.Select(t => t.Tab).Contains(after))
				throw new ArgumentOutOfRangeException($"Tab '{after.GetType().Name}' not present in current tabs.");
			//Remove everything after 'after'(if after is null remove everything)
			while (Tabs.Any() && Tabs.Last().Tab != after) {
				TabsOnLeft.Items.Remove(TabsOnLeft.Items.Last());
			}
			newTab.InsertTab += InsertTab;
			var tabView = new TabView(newTab);
			tabView.Clicked += TabView_Clicked;
			TabsOnLeft.Items.Add(tabView);
			Select(tabView);
		}

		IEnumerable<TabView> Tabs => TabsOnLeft.Items.OfType<StackLayoutItem>().Select(i => (TabView)i.Control);

		void Select(TabView tabView)
		{
			bool foundIt = false;
			foreach (var tv in Tabs) {
				if (tv == tabView) {
					foundIt = true;
					tv.BackgroundColor = Colors.Red;
				} else {
					if (foundIt) {
						tv.BackgroundColor = Colors.Blue;
					} else {
						tv.BackgroundColor = Colors.Yellow;
					}
				}
			}
			PlaceHolderForTabContent.Content = tabView.Tab.TabContent;
		}

		void TabView_Clicked(Krofiler.ProfilingPage.TabView obj)
		{
			Select(obj);
		}
	}
}

