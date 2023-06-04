using System;
using System.IO;
using System.Text;

using Newtonsoft.Json;

using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace ServaMap.Server;

public class TraderHandlerModSystem : DatabaseHandlerModSystem {
	public override string TableName => "traders";

	public override void StartServerSide(ICoreServerAPI api) {
		base.StartServerSide(api);

		serverAPI.Event.ChunkColumnLoaded += (_, chunks) => {
			foreach (var serverChunk in chunks)
				ProcessEntities(serverChunk.Entities);
		};

		serverAPI.Event.RegisterGameTickListener(OnGameTick,
				config.GeoJsonAutoExportIntervalSeconds * 1000);

		try {
			InitializeDatabase();
			using (var command = conn.CreateCommand()) {
				command.CommandText = @$"
CREATE TABLE IF NOT EXISTS {TableName}
(
    id UNIQUE,
    
    x NOT NULL,
    y NOT NULL,
    z NOT NULL,
    
    name NOT NULL,
    wares NOT NULL
)
";
				command.ExecuteNonQuery();
			}
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to open the trader database:");
			logger.Error(e.ToString());
		}
	}

	private void OnGameTick(float _) {
		try {
			WriteGeoJson();
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to export trader GeoJSON!");
			logger.Error(e);
		}
	}

	public bool ProcessTrader(EntityTrader trader) {
		try {
			logger.Notification($"Processing trader: {trader.EntityId} at {trader.Pos}");
			if (!ShouldUpdateTrader(trader))
				return true;
			UpdateTrader(trader);
		}
		catch (Exception e) {
			logger.Error($"Serv-a-Map failed to update a trader: {trader.EntityId} at {trader.Pos}");
			logger.Error(e.ToString());
			return false;
		}
		return true;
	}

	public void ProcessEntities(Entity[] entities) {
		if (entities is null)
			return;
		foreach (var entity in entities) {
			var trader = entity as EntityTrader;
			if (trader is null)
				continue;
			ProcessTrader(trader);
		}
	}

	private bool ShouldUpdateTrader(EntityTrader trader) {
		using (var command = conn.CreateCommand()) {
			command.CommandText = @$"
SELECT * FROM {TableName}
WHERE id = $id
";
			command.Parameters.AddWithValue("$id", trader.EntityId);
			var reader = command.ExecuteReader();
			// If the anything comes back, the trader is already there.
			return !reader.HasRows;
		}
	}

	private void UpdateTrader(EntityTrader trader) {
		using (var command = conn.CreateCommand()) {
			command.CommandText = @$"
INSERT OR REPLACE INTO {TableName}
VALUES ($id, $x, $y, $z, $name, $wares)
";
			command.Parameters.AddWithValue("$id", trader.EntityId);

			command.Parameters.AddWithValue("$x", trader.Pos.X);
			command.Parameters.AddWithValue("$y", trader.Pos.Y);
			command.Parameters.AddWithValue("$z", trader.Pos.Z);

			command.Parameters.AddWithValue("$name",
					trader.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name"));
			command.Parameters.AddWithValue("$wares", trader.Code.EndVariant());
			command.ExecuteNonQuery();
		}
	}

	public void WriteGeoJson() {
		using (var stream = new StreamWriter(jsonFilePath, false, Encoding.UTF8)) {
			using (var writer = new JsonTextWriter(stream)) {
				writer.Formatting = Formatting.None;
				writer.StringEscapeHandling = StringEscapeHandling.EscapeHtml;
				writer.WriteObject(() => {
					writer.WriteObject("crs",
									() => {
										writer.WriteObject("properties",
														() => writer.WriteKeyValue("ame", "urn:ogc:def:crs:EPSG::3857"))
												.WriteKeyValue("type", "name");
									})
							.WriteArray("features",
									() => WriteFeatures(reader => {
										var x = reader.GetDouble(1);
										var y = reader.GetDouble(2);
										var z = reader.GetDouble(3);
										var name = reader.GetString(4);
										var wares = reader.GetString(5);
										writer.WriteObject(() => {
											writer.WriteObject("geometry",
															() => {
																writer.WriteArray("coordinates", x, z)
																		.WriteKeyValue("type", "Point");
															})
													.WriteObject("properties",
															() => {
																writer.WriteKeyValue("name", name)
																		.WriteKeyValue("wares", wares)
																		.WriteKeyValue("z", y);
															})
													.WriteKeyValue("type", "Feature");
										});
									}))
							.WriteKeyValue("name", "traders")
							.WriteKeyValue("type", "FeatureCollection");
				});
			}
		}
	}
}