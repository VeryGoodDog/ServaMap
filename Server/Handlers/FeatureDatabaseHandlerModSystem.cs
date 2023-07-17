using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text;

using Newtonsoft.Json;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ServaMap.Server;

/// <summary>
/// Like a DatabaseHanderModSystem, except this has features that can be exported as GeoJSON.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class FeatureDatabaseHandlerModSystem<T> : DatabaseHandlerModSystem<T> {
	protected string jsonFilePath;

	public override bool ShouldLoad(EnumAppSide forSide) => forSide.IsServer();

	public override double ExecuteOrder() => 0.3;

	public override void StartServerSide(ICoreServerAPI api) {
		base.StartServerSide(api);
		var jsonFilename = Path.ChangeExtension(TableName, "geojson");
		jsonFilePath =
				Path.Combine(
						config.GetOrCreateWebMapSubDirectory(serverAPI, config.MapApiDataPath, config.GeoJsonPath),
						jsonFilename);
	}

	public abstract Result<bool> ProcessFeature(T toProcess);

	public abstract Result<bool> ShouldUpdate(T toTest);

	protected Exception WriteFeatures(Action<JsonTextWriter, SQLiteDataReader> middle) {
		try {
			using var stream = new StreamWriter(jsonFilePath, false, Encoding.UTF8);
			using var writer = new JsonTextWriter(stream);
			writer.Formatting = Formatting.None;
			writer.StringEscapeHandling = StringEscapeHandling.EscapeHtml;
			writer.WriteObject(() => {
				writer.WriteObject("crs",
								() => {
									writer.WriteObject("properties", () => writer.WriteKeyValue("ame", "urn:ogc:def:crs:EPSG::3857"))
											.WriteKeyValue("type", "name");
								})
						.WriteArray("features",
								() => {
									using var command = conn.CreateCommand();
									command.CommandText = $"SELECT * FROM {TableName}";
									var reader = command.ExecuteReader();
									while (reader.Read())
										middle(writer, reader);
								})
						.WriteKeyValue("name", TableName)
						.WriteKeyValue("type", "FeatureCollection");
			});
			return null;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to export landmark GeoJSON.");
			logger.Error(e.ToString());
			return e;
		}
	}
}