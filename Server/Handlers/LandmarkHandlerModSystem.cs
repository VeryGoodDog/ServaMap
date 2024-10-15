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

public class LandmarkHandlerModSystem : FeatureDatabaseHandlerModSystem<Landmark> {
	public override string TableName => "landmarks";

	public override void StartServerSide(ICoreServerAPI api) {
		base.StartServerSide(api);

		var parsers = serverAPI.ChatCommands.Parsers;

		serverAPI.ChatCommands.Create("landmark")
				.WithAlias("landmarks")
				.RequiresPrivilege(Privilege.claimland)
				.WithDesc("Manipulate the server's landmarks.")
				.WithSub("add",
						command => command.WithAlias("update", "new", "create")
								.WithDesc("Add or update a landmark in the server's map.")
								.WithArgs(parsers.Word("color"), parsers.Word("icon"), parsers.All("label"))
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

		serverAPI.Event.RegisterGameTickListener(_ => WriteGeoJson(),
				config.GeoJsonAutoExportIntervalSeconds * 1000);
	}

	public override void InitializeDatabase() {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
CREATE TABLE IF NOT EXISTS {TableName}
(
    x NOT NULL,
    y NOT NULL,
    z NOT NULL,
    label NOT NULL,
    color NOT NULL,
    icon NOT NULL,
    creator_id NOT NULL,
    UNIQUE (x, y, z, label, color, icon, creator_id)
)
";
			command.ExecuteNonQuery();
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to open the landmark database:");
			logger.Error(e.ToString());
		}
	}

	public override Result<bool> ProcessFeature(Landmark landmark) {
		var shouldUpdate = ShouldUpdate(landmark);
		if (shouldUpdate.IsException)
			return shouldUpdate;
		if (!shouldUpdate.Good)
			return true;
		var update = Update(landmark);
		if (update.IsGood)
			return true;
		return update;
	}

	public override Result<bool> ShouldUpdate(Landmark landmark) {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
SELECT * FROM {TableName}
WHERE x = $x
  AND y = $y
  AND z = $z
  
  AND label = $label
  AND color = $color
  AND icon = $icon
  AND creator_id = $creator_id
";
			command.Parameters.AddWithValue("$x", landmark.Pos.X);
			command.Parameters.AddWithValue("$y", landmark.Pos.Y);
			command.Parameters.AddWithValue("$z", landmark.Pos.Z);

			command.Parameters.AddWithValue("$label", landmark.Label);
			command.Parameters.AddWithValue("$color", landmark.Color);
			command.Parameters.AddWithValue("$icon", landmark.Icon);
			command.Parameters.AddWithValue("$creator_id", landmark.CreatorId);
			var reader = command.ExecuteReader();
			return !reader.HasRows;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to test if a landmark should update.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public override Result<bool> Update(Landmark landmark) {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
INSERT INTO {TableName}
VALUES ($x, $y, $z, $label, $color, $icon, $creator_id)
";
			command.Parameters.AddWithValue("$x", landmark.Pos.X);
			command.Parameters.AddWithValue("$y", landmark.Pos.Y);
			command.Parameters.AddWithValue("$z", landmark.Pos.Z);

			command.Parameters.AddWithValue("$label", landmark.Label);
			command.Parameters.AddWithValue("$color", landmark.Color);
			command.Parameters.AddWithValue("$icon", landmark.Icon);

			command.Parameters.AddWithValue("$creator_id", landmark.CreatorId);
			var rowsAffected = command.ExecuteNonQuery();
			return rowsAffected > 0;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to update a landmark.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public override Result<bool> Delete(Landmark toDelete) {
		try {
			using var command = conn.CreateCommand();
			if (toDelete.CreatorId == "") {
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
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to delete a landmark.");
			logger.Error(e.ToString());
			return e;
		}
	}

	private Result<List<Landmark>> ListLandmarks(string playerUid) {
		try {
			using var command = conn.CreateCommand();
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
				var color = reader.GetString(4);
				var icon = reader.GetString(5);
				var landmark = new Landmark {
					Pos = new Vec3i(x, y, z), Label = label, Color = color, Icon = icon
				};
				landmarks.Add(landmark);
			}
			return landmarks;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to list landmarks.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public Exception WriteGeoJson() =>
			WriteFeatures((writer, reader) => {
				var x = reader.GetInt32(0);
				var y = reader.GetInt32(1);
				var z = reader.GetInt32(2);
				var label = reader.GetString(3);
				var color = reader.GetString(4);
				var icon = reader.GetString(5);
				writer.WriteObject(() => {
					writer.WriteObject("geometry",
									() => {
										writer.WriteArray("coordinates", x, -z).WriteKeyValue("type", "Point");
									})
							.WriteObject("properties",
									() => {
										writer.WriteKeyValue("label", label)
												.WriteKeyValue("color", color)
												.WriteKeyValue("icon", icon)
												.WriteKeyValue("z", y);
									})
							.WriteKeyValue("type", "Feature");
				});
			});

	private TextCommandResult LandmarkAddHandler(TextCommandCallingArgs args) {
		var pos = args.Caller.Pos.AsVec3i;
		var color = args[0] as string;
		var icon = args[1] as string;
		var label = args[2] as string;
		var creatorId = args.Caller.Player.PlayerUID;
		var res = ProcessFeature(new Landmark {
			Pos = pos, Color = color, Icon = icon, Label = label, CreatorId = creatorId
		});
		return res.IsGood
				? res.Good
						? TextCommandResult.Success("Added/updated landmark.")
						: TextCommandResult.Success("Landmark was not modified.")
				: TextCommandResult.Error("Failed to process landmark.");
	}

	private TextCommandResult ListLandmarksHandler(TextCommandCallingArgs args) {
		var usingCallerUid = args.Parsers[0].IsMissing;
		var uid = usingCallerUid ? args.Caller.Player.PlayerUID : args[0] as string;

		var landmarksRes = ListLandmarks(uid);
		if (landmarksRes.IsException)
			return TextCommandResult.Error("An error occured while trying to list landmarks.");
		var landmarks = landmarksRes.Good;

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
			var color = landmark.Color;
			var icon = landmark.Icon;
			response.Append($"{pos} Label: \"{label}\" Color: \"{color}\" Icon: \"{icon}\"\n");
		}
		return TextCommandResult.Success(response.ToString());
	}

	private TextCommandResult LandmarkDeleteHandler(TextCommandCallingArgs args) {
		var pos = (args[0] as Vec3d).AsBlockPos.ToLocalPosition(serverAPI);
		var res = Delete(new Landmark {
			Pos = pos, CreatorId = args.Caller.Player.PlayerUID
		});

		return res.IsGood
				? res.Good
						? TextCommandResult.Success("Deleted landmark!")
						: TextCommandResult.Success(
								"Landmark was not deleted. Did you type the coordinates right?")
				: TextCommandResult.Error("An error occured while trying to delete that landmark.");
	}

	private TextCommandResult LandmarkForceDeleteHandler(TextCommandCallingArgs args) {
		var pos = (args[0] as Vec3d).AsBlockPos.ToLocalPosition(serverAPI);
		var res = Delete(new Landmark {
			Pos = pos
		});

		return res.IsGood
				? res.Good
						? TextCommandResult.Success("Deleted landmark!")
						: TextCommandResult.Success(
								"Landmark was not deleted. Did you type the coordinates right?")
				: TextCommandResult.Error("An error occured while trying to delete that landmark.");
	}
}