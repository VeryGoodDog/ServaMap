using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Newtonsoft.Json;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace ServaMap.Server;

// Handles teleporters and translocators
public class TeleporterHandlerModSystem : FeatureDatabaseHandlerModSystem<Teleporter> {
	public override string TableName => "teleporters";

	public override void StartServerSide(ICoreServerAPI api) {
		base.StartServerSide(api);

		api.Event.ChunkColumnLoaded += (_, chunks) => {
			foreach (var serverChunk in chunks)
				ProcessBlockEntities(serverChunk.BlockEntities);
		};

		serverAPI.Event.BreakBlock += OnEventOnBreakBlock;

		serverAPI.Event.RegisterGameTickListener(_ => WriteGeoJson(),
				config.GeoJsonAutoExportIntervalSeconds * 1000);

		InitializeTeleporterDatabase();
	}

	private void OnEventOnBreakBlock(IServerPlayer serverPlayer, BlockSelection blockSelection,
			ref float f, ref EnumHandling enumHandling) {
		var pos = blockSelection.Position;
		var blockEntity =
				serverAPI.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityTeleporterBase;

		if (blockEntity is null)
			return;

		Delete(new Teleporter {
			Start = blockSelection.Position
		});
	}

	private void InitializeTeleporterDatabase() {
		try {
			InitializeDatabase();
			using var command = conn.CreateCommand();
			command.CommandText = @$"
CREATE TABLE IF NOT EXISTS {TableName}
(
    start_x UNIQUE,
    start_y UNIQUE,
    start_z UNIQUE,
    
    end_x UNIQUE,
    end_y UNIQUE,
    end_z UNIQUE,
    
    label NOT NULL,
    tag NOT NULL
)
";
			command.ExecuteNonQuery();
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to open the teleporter database:");
			logger.Error(e.ToString());
		}
	}

	public override Result<bool> ProcessFeature(Teleporter teleporter) {
		logger.Notification($"Processing teleporter: {teleporter.Start} to {teleporter.End}");
		var shouldUpdateRes = ShouldUpdate(teleporter);
		if (shouldUpdateRes.IsException)
			return shouldUpdateRes;
		if (!shouldUpdateRes.Good)
			return false;
		return Update(teleporter);
	}

	public void ProcessBlockEntities(Dictionary<BlockPos, BlockEntity> blockEntities) {
		if (blockEntities is null)
			return;
		foreach (var blockEntity in blockEntities) {
			var tele = blockEntity.Value as BlockEntityTeleporterBase;
			if (tele is null)
				continue;
			ProcessFeature(new Teleporter(tele));
		}
	}

	public override Result<bool> ShouldUpdate(Teleporter teleporter) {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
SELECT * FROM {TableName}
WHERE ((start_x = $start_x AND start_y = $start_y AND start_z = $start_z 
           AND end_x = $end_x AND end_y = $end_y AND end_z = $end_z)
   OR (start_x = $end_x AND start_y = $end_y AND start_z = $end_z 
           AND end_x = $start_x AND end_y = $start_y AND end_z = $start_z))
   AND label = $label
   AND tag = $tag
";
			command.Parameters.AddWithValue("$start_x", teleporter.Start.X);
			command.Parameters.AddWithValue("$start_y", teleporter.Start.Y);
			command.Parameters.AddWithValue("$start_z", teleporter.Start.Z);

			command.Parameters.AddWithValue("$end_x", teleporter.End.X);
			command.Parameters.AddWithValue("$end_y", teleporter.End.Y);
			command.Parameters.AddWithValue("$end_z", teleporter.End.Z);

			command.Parameters.AddWithValue("$label", teleporter.Label);
			command.Parameters.AddWithValue("$tag", teleporter.Tag);
			var reader = command.ExecuteReader();
			// If the anything comes back, then there is an identical teleporter already there.
			return !reader.HasRows;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to test if a teleporter should update.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public override Result<bool> Update(Teleporter teleporter) {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
INSERT OR REPLACE INTO {TableName}
VALUES ($start_x, $start_y, $start_z, $end_x, $end_y, $end_z, $label, $tag)
";
			command.Parameters.AddWithValue("$start_x", teleporter.Start.X);
			command.Parameters.AddWithValue("$start_y", teleporter.Start.Y);
			command.Parameters.AddWithValue("$start_z", teleporter.Start.Z);

			command.Parameters.AddWithValue("$end_x", teleporter.End.X);
			command.Parameters.AddWithValue("$end_y", teleporter.End.Y);
			command.Parameters.AddWithValue("$end_z", teleporter.End.Z);

			command.Parameters.AddWithValue("$label", teleporter.Label);
			command.Parameters.AddWithValue("$tag", teleporter.Tag);
			var rowsAffected = command.ExecuteNonQuery();
			return rowsAffected > 0;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to update a teleporter.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public override Result<bool> Delete(Teleporter teleporter) {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
DELETE FROM {TableName}
WHERE (start_x = $start_x AND start_y = $start_y AND start_z = $start_z)
   OR (end_x = $start_x AND end_y = $start_y AND end_z = $start_z)
";
			command.Parameters.AddWithValue("$start_x", teleporter.Start.X);
			command.Parameters.AddWithValue("$start_y", teleporter.Start.Y);
			command.Parameters.AddWithValue("$start_z", teleporter.Start.Z);
			var rowsAffected = command.ExecuteNonQuery();
			return rowsAffected > 0;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to delete a teleporter.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public Exception WriteGeoJson() =>
			WriteFeatures((writer, reader) => {
				var start_x = reader.GetInt32(0);
				var start_y = reader.GetInt32(1);
				var start_z = reader.GetInt32(2);

				var end_x = reader.GetInt32(3);
				var end_y = reader.GetInt32(4);
				var end_z = reader.GetInt32(5);

				var label = reader.GetString(6);
				var tag = reader.GetString(7);

				writer.WriteObject(() => {
					writer.WriteObject("geometry",
									() => {
										writer.WriteArray("coordinates",
														() => {
															writer.WriteArray(start_x, start_z).WriteArray(end_x, end_z);
														})
												.WriteKeyValue("type", "LineString");
									})
							.WriteObject("properties",
									() => {
										writer.WriteKeyValue("depth1", start_y)
												.WriteKeyValue("depth2", end_y)
												.WriteKeyValue("label", label)
												.WriteKeyValue("tag", tag);
									})
							.WriteKeyValue("type", "Feature");
				});
			});
}