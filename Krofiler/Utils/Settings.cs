using System;
using System.Collections.Generic;
using System.IO;

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
			if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX) {
				dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".krofiler");
			} else {
				dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Krofiler");
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

