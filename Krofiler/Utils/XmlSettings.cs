using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Krofiler
{
	class XmlSettings
	{
		public static void Save<T>(Stream stream, T settingsObject)
		{
			var x = new XmlSerializer(settingsObject.GetType());
			x.Serialize(stream, settingsObject);
		}

		public static void Save<T>(string filePath, T settingsObject)
		{
			using (FileStream FS = new FileStream(filePath, FileMode.Create)) {
				Save(FS, settingsObject);
			}
		}


		public static void Load<T>(string filePath, ref T settingsObject)
		{
			if (File.Exists(filePath)) {
				using (FileStream FS = new FileStream(filePath, FileMode.Open)) {
					Load(FS, ref settingsObject);
				}
			} else {
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				Save(filePath, settingsObject);
			}
		}

		public static void Load<T>(Stream stream, ref T settingsObject)
		{
			var xmlSerializer = new XmlSerializer(settingsObject.GetType());
			settingsObject = (T)xmlSerializer.Deserialize(stream);
		}
	}

	[XmlRoot("dictionary")]
	public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
	{
		public System.Xml.Schema.XmlSchema GetSchema()
		{
			return null;
		}
		public void ReadXml(System.Xml.XmlReader reader)
		{
			XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
			XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));
			bool wasEmpty = reader.IsEmptyElement;
			reader.Read();
			if (wasEmpty) {
				return;
			}
			while (reader.NodeType != System.Xml.XmlNodeType.EndElement) {
				reader.ReadStartElement("item");
				reader.ReadStartElement("key");
				TKey key = (TKey)keySerializer.Deserialize(reader);
				reader.ReadEndElement();
				reader.ReadStartElement("value");
				TValue value = (TValue)valueSerializer.Deserialize(reader);
				reader.ReadEndElement();
				this.Add(key, value);
				reader.ReadEndElement();
				reader.MoveToContent();
			}
			reader.ReadEndElement();
		}
		public void WriteXml(System.Xml.XmlWriter writer)
		{
			XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
			XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));
			foreach (TKey key in this.Keys) {
				writer.WriteStartElement("item");
				writer.WriteStartElement("key");
				keySerializer.Serialize(writer, key);
				writer.WriteEndElement();
				writer.WriteStartElement("value");
				TValue value = this[key];
				valueSerializer.Serialize(writer, value);
				writer.WriteEndElement();
				writer.WriteEndElement();
			}
		}
	}
	public static class SerializableDictionaryExtensions
	{
		public static void addOrChange(this SerializableDictionary<string, string> dictionary, string key, string value)
		{
			if (dictionary.ContainsKey(key)) {
				dictionary[key] = value;
			} else {
				dictionary.Add(key, value);
			}
		}
		public static string defaultIfNoMatch(this SerializableDictionary<string, string> dictionary, string key, string deft)
		{
			if (dictionary.ContainsKey(key)) {
				return dictionary[key];
			} else {
				return deft;
			}
		}
	}
}
