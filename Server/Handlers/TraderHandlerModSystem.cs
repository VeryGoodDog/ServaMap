using System;
using System.IO;
using System.Text;

using Newtonsoft.Json;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace ServaMap.Server;

public class TraderHandlerModSystem : FeatureDatabaseHandlerModSystem<Trader> {
	public override string TableName => "traders";

	public override void StartServerSide(ICoreServerAPI api) {
		base.StartServerSide(api);
		var serverMain = serverAPI.World as ServerMain;
		if (serverMain is not null) {
			serverMain.ModEventManager.OnEntityLoaded += OnLoadOrSpawn;
			serverMain.ModEventManager.OnEntitySpawn += OnLoadOrSpawn;
		}

		serverAPI.Event.RegisterGameTickListener(_ => WriteGeoJson(),
				config.GeoJsonAutoExportIntervalSeconds * 1000);
	}

	private void OnLoadOrSpawn(Entity entity){
		var trader = entity as EntityTrader;
		if (trader is null)
			return;
		ProcessFeature(new Trader(trader));
	}

	public override void InitializeDatabase() {
		try {
			using var command = conn.CreateCommand();
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
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to open the trader database:");
			logger.Error(e.ToString());
		}
	}

	public override Result<bool> ProcessFeature(Trader trader) {
		var shouldUpdateRes = ShouldUpdate(trader);
		if (shouldUpdateRes.IsException)
			return shouldUpdateRes;
		if (!shouldUpdateRes.Good)
			return true;
		return Update(trader);
	}

	public void ProcessEntities(Entity[] entities) {
		if (entities is null || entities.Length == 0)
			return;
		foreach (var entity in entities) {
			var trader = entity as EntityTrader;
			if (trader is null)
				continue;
			ProcessFeature(new Trader(trader));
		}
	}

	public override Result<bool> ShouldUpdate(Trader trader) {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
SELECT * FROM {TableName}
WHERE id = $id
";
			command.Parameters.AddWithValue("$id", trader.EntityId);
			var reader = command.ExecuteReader();
			// If the anything comes back, the trader is already there.
			return !reader.HasRows;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to test if a trader should update.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public override Result<bool> Update(Trader trader) {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
INSERT OR REPLACE INTO {TableName}
VALUES ($id, $x, $y, $z, $name, $wares)
";
			command.Parameters.AddWithValue("$id", trader.EntityId);

			command.Parameters.AddWithValue("$x", trader.Pos.X);
			command.Parameters.AddWithValue("$y", trader.Pos.Y);
			command.Parameters.AddWithValue("$z", trader.Pos.Z);

			command.Parameters.AddWithValue("$name", trader.Name);
			command.Parameters.AddWithValue("$wares", trader.Wares);
			var rowsAffected = command.ExecuteNonQuery();
			return rowsAffected > 0;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to update a trader.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public override Result<bool> Delete(Trader trader) {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
DELETE FROM {TableName}
WHERE id = $id
";
			command.Parameters.AddWithValue("$id", trader.EntityId);
			var reader = command.ExecuteReader();
			return !reader.HasRows;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to delete a trader.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public Exception WriteGeoJson() =>
			WriteFeatures((writer, reader) => {
				var x = reader.GetDouble(1);
				var y = reader.GetDouble(2);
				var z = reader.GetDouble(3);
				var name = reader.GetString(4);
				var wares = reader.GetString(5);
				writer.WriteObject(() => {
					writer.WriteObject("geometry",
									() => {
										writer.WriteArray("coordinates", x, z).WriteKeyValue("type", "Point");
									})
							.WriteObject("properties",
									() => {
										writer.WriteKeyValue("name", name)
												.WriteKeyValue("wares", wares)
												.WriteKeyValue("z", y);
									})
							.WriteKeyValue("type", "Feature");
				});
			});
}