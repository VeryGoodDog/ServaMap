using System;

using Newtonsoft.Json;

namespace ServaMap;

// I use these to make sure that I don't forget any start and end calls.
// If performance becomes an issue I'll change things.
public static class JsonTextWriterExtensions {
	public static JsonTextWriter WriteObject(this JsonTextWriter writer, Action middle) {
		writer.WriteStartObject();
		middle();
		writer.WriteEndObject();
		return writer;
	}

	public static JsonTextWriter WriteObject(this JsonTextWriter writer, string propertyName,
			Action middle) {
		writer.WritePropertyName(propertyName);
		return writer.WriteObject(middle);
	}

	public static JsonTextWriter WriteKeyValue<T>(this JsonTextWriter writer, string propertyName,
			T value) {
		writer.WritePropertyName(propertyName);
		writer.WriteValue(value.ToString());
		return writer;
	}

	public static JsonTextWriter WriteArray(this JsonTextWriter writer, Action middle) {
		writer.WriteStartArray();
		middle();
		writer.WriteEndArray();
		return writer;
	}

	public static JsonTextWriter WriteArray(this JsonTextWriter writer, string propertyName,
			Action middle) {
		writer.WritePropertyName(propertyName);
		return writer.WriteArray(middle);
	}

	public static JsonTextWriter WriteArray(this JsonTextWriter writer, params object[] values) =>
			writer.WriteArray(() => {
				foreach (var value in values)
					writer.WriteValue(value);
			});

	public static JsonTextWriter WriteArray(this JsonTextWriter writer, string propertyName,
			params object[] values) =>
			writer.WriteArray(propertyName,
					() => {
						foreach (var value in values)
							writer.WriteValue(value);
					});
}