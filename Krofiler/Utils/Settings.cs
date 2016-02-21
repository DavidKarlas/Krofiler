using System;
using System.Collections.Generic;
using System.IO;
using Eto;

namespace Krofiler
{
	public class Settings
	{
		static Settings instance;
		public static Settings Instance {
			get {
				if (instance == null)
					instance = LoadSettings();
				return instance;
			}
		}

		static Settings LoadSettings()
		{
			var settingsPath = SettingsPath();
			var settings = new Settings();
			XmlSettings.Load(settingsPath, ref settings);
			return settings;
		}

		static string SettingsPath()
		{
			string dataFolder;
			if (EtoEnvironment.Platform.IsWindows) {
				dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Krofiler");
			} else {
				dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".krofiler");
			}

			return Path.Combine(dataFolder, "Settings.xml");
		}

		public void Save()
		{
			XmlSettings.Save(SettingsPath(), this);
		}

		public List<string> RecentlyOpenedFiles = new List<string>();
		public List<string> RecentlyRecordedFiles = new List<string>();
		public List<string> RecentlyProfiledApps = new List<string>();
	}
}

