using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Newtonsoft.Json;

using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ServaMap.Server;

public class LandmarkHandlerModSystem : DatabaseHandlerModSystem {
	public override string TableName => "landmarks";

	public override void StartServerSide(ICoreServerAPI api) {
		base.StartServerSide(api);

		serverAPI.Event.RegisterGameTickListener(OnGameTick,
				config.GeoJsonAutoExportIntervalSeconds * 1000);

		try {
			InitializeDatabase();
			using (var command = conn.CreateCommand()) {
				command.CommandText = @$"
CREATE TABLE IF NOT EXISTS {TableName}
(
    x UNIQUE,
    y UNIQUE,
    z UNIQUE,
    label NOT NULL,
    type NOT NULL,
    creator_id UNIQUE
)
";
				command.ExecuteNonQuery();
			}
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to open the landmark database:");
			logger.Error(e.ToString());
		}

		var parsers = serverAPI.ChatCommands.Parsers;

		serverAPI.ChatCommands.Create("landmark")
				.WithAlias("landmarks")
				.RequiresPrivilege(Privilege.claimland)
				.WithDesc("Manipulate the server's landmarks.")
				.WithSub("add",
						command => command.WithAlias("update", "new", "create")
								.WithDesc("Add or update a landmark in the server's map.")
								.WithArgs(parsers.Word("type"), parsers.All("label"))
								.HandleWith(LandmarkAddHandler))
				.WithSub("list",
						command => command.WithDesc("List landmarks")
								.RequiresPlayer()
								.WithArgs(parsers.OptionalPlayerUids("player"))
								.HandleWith(ListLandmarksHandler))
				.WithSub("delete",
						command => command.WithAlias("remove")
								.RequiresPlayer()
								.WithDesc("Remove a landmark in the server's map.")
								.WithArgs(parsers.OptionalWorldPosition("position"))
								.HandleWith(LandmarkDeleteHandler))
				.WithSub("forcedelete",
						command => command.WithAlias("forceremove")
								.RequiresPrivilege(Privilege.ban)
								.WithDesc("Remove a landmark in the server's map.")
								.WithArgs(parsers.OptionalWorldPosition("position"))
								.HandleWith(LandmarkForceDeleteHandler));
	}

	private void OnGameTick(float _) {
		try {
			WriteGeoJson();
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to export landmark GeoJSON!");
			logger.Error(e);
		}
	}

	public bool ProcessLandmark(Landmark landmark) {
		try {
			logger.Notification($"Processing landmark: {landmark.Pos} {landmark.Label}");
			if (!ShouldUpdateLandmark(landmark))
				return true;
			UpdateLandmark(landmark);
		}
		catch (Exception e) {
			logger.Error($"Serv-a-Map failed to update a landmark: {landmark.Pos} {landmark.Label}");
			logger.Error(e.ToString());
			return false;
		}
		return true;
	}

	private bool ShouldUpdateLandmark(Landmark landmark) {
		using (var command = conn.CreateCommand()) {
			command.CommandText = @$"
SELECT * FROM {TableName}
WHERE x = $x
  AND y = $y
  AND z = $z
  
  AND label = $label
  AND type = $type
  AND creator_id = $creator_id
";
			command.Parameters.AddWithValue("$x", landmark.Pos.X);
			command.Parameters.AddWithValue("$y", landmark.Pos.Y);
			command.Parameters.AddWithValue("$z", landmark.Pos.Z);

			command.Parameters.AddWithValue("$label", landmark.Label);
			command.Parameters.AddWithValue("$type", landmark.Type);
			command.Parameters.AddWithValue("$creator_id", landmark.CreatorId);
			var reader = command.ExecuteReader();
			return !reader.HasRows;
		}
	}

	private void UpdateLandmark(Landmark landmark) {
		using (var command = conn.CreateCommand()) {
			command.CommandText = @$"
INSERT OR REPLACE INTO {TableName}
VALUES ($x, $y, $z, $label, $type, $creator_id)
";
			command.Parameters.AddWithValue("$x", landmark.Pos.X);
			command.Parameters.AddWithValue("$y", landmark.Pos.Y);
			command.Parameters.AddWithValue("$z", landmark.Pos.Z);

			command.Parameters.AddWithValue("$label", landmark.Label);
			command.Parameters.AddWithValue("$type", landmark.Type);

			command.Parameters.AddWithValue("$creator_id", landmark.CreatorId);
			command.ExecuteNonQuery();
		}
	}

	private List<Landmark> ListLandmarks(string playerUid) {
		using (var command = conn.CreateCommand()) {
			if (playerUid is "") {
				command.CommandText = @$"
SELECT * FROM {TableName}
";
			}
			else {
				command.CommandText = @$"
SELECT * FROM {TableName}
WHERE creator_id = $creator_id
";
				command.Parameters.AddWithValue("$creator_id", playerUid);
			}
			var reader = command.ExecuteReader();
			var landmarks = new List<Landmark>();
			while (reader.Read()) {
				var x = reader.GetInt32(0);
				var y = reader.GetInt32(1);
				var z = reader.GetInt32(2);
				var label = reader.GetString(3);
				var type = reader.GetString(4);
				var landmark = new Landmark {
					Pos = new Vec3i(x, y, z), Label = label, Type = type
				};
				landmarks.Add(landmark);
			}
			return landmarks;
		}
	}

	private bool DeleteLandmark(Landmark toDelete, bool force = false) {
		using (var command = conn.CreateCommand()) {
			if (force) {
				command.CommandText = @$"
DELETE FROM {TableName}
WHERE x = $x AND y = $y AND z = $z
";
			}
			else {
				command.CommandText = @$"
DELETE FROM {TableName}
WHERE x = $x AND y = $y AND z = $z AND creator_id = $creator_id
";
				command.Parameters.AddWithValue("$creator_id", toDelete.CreatorId);
			}

			command.Parameters.AddWithValue("$x", toDelete.Pos.X);
			command.Parameters.AddWithValue("$y", toDelete.Pos.Y);
			command.Parameters.AddWithValue("$z", toDelete.Pos.Z);
			var rowsAffected = command.ExecuteNonQuery();
			return rowsAffected > 0;
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
										var x = reader.GetInt32(0);
										var y = reader.GetInt32(1);
										var z = reader.GetInt32(2);
										var label = reader.GetString(3);
										var type = reader.GetString(4);
										writer.WriteObject(() => {
											writer.WriteObject("geometry",
															() => {
																writer.WriteArray("coordinates", x, z)
																		.WriteKeyValue("type", "Point");
															})
													.WriteObject("properties",
															() => {
																writer.WriteKeyValue("label", label)
																		.WriteKeyValue("type", type)
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

	private TextCommandResult LandmarkAddHandler(TextCommandCallingArgs args) {
		var pos = args.Caller.Pos.AsBlockPos.ToLocalPosition(serverAPI);
		var type = args[0] as string;
		var label = args[1] as string;
		var creatorId = args.Caller.Player.PlayerUID;
		var res = ProcessLandmark(new Landmark {
			Pos = pos, Type = type, Label = label, CreatorId = creatorId
		});
		return res
				? TextCommandResult.Success("Added/updated landmark.")
				: TextCommandResult.Error("Failed to add/update landmark.");
	}

	private TextCommandResult ListLandmarksHandler(TextCommandCallingArgs args) {
		var usingCallerUid = args.Parsers[0].IsMissing;
		var uid = usingCallerUid ? args.Caller.Player.PlayerUID : args[0] as string;

		var landmarks = ListLandmarks(uid);
		var count = landmarks.Count;

		var response = new StringBuilder();

		if (usingCallerUid) {
			if (count == 0)
				response.AppendLine("You have no landmarks.");
			else if (count == 1)
				response.AppendLine("You have 1 landmark:");
			else
				response.AppendLine($"You have {count} landmarks:");
		}
		else {
			var playername = serverAPI.PlayerData.GetPlayerDataByUid(uid).LastKnownPlayername;
			if (count == 0)
				response.AppendLine($"{playername} has no landmarks.");
			else if (count == 1)
				response.AppendLine($"{playername} has 1 landmark:");
			else
				response.AppendLine($"{playername} has {count} landmarks:");
		}

		foreach (var landmark in landmarks) {
			var pos = landmark.Pos;
			var label = landmark.Label;
			var type = landmark.Type;
			response.Append($"{pos} Label: \"{label}\" Type: \"{type}\"\n");
		}
		return TextCommandResult.Success(response.ToString());
	}

	private TextCommandResult LandmarkDeleteHandler(TextCommandCallingArgs args) {
		var pos = (args[0] as Vec3d).AsBlockPos.ToLocalPosition(serverAPI);
		var res = DeleteLandmark(new Landmark {
			Pos = pos, CreatorId = args.Caller.Player.PlayerUID
		});

		return res
				? TextCommandResult.Success("Deleted landmark!")
				: TextCommandResult.Error(
						"Failed to delete the landmark. Did you type the coordinates right?");
	}

	private TextCommandResult LandmarkForceDeleteHandler(TextCommandCallingArgs args) {
		var pos = (args[0] as Vec3d).AsBlockPos.ToVec3i();
		var res = DeleteLandmark(new Landmark {
					Pos = pos
				},
				true);

		return res
				? TextCommandResult.Success("Deleted landmark!")
				: TextCommandResult.Error(
						"Failed to delete the landmark. Did you type the coordinates right?");
	}
}