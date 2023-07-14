using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using ProtoBuf;

using SkiaSharp;

using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ServaMap.Server;

public class ShardHandlerModSystem : FeatureDatabaseHandlerModSystem<ChunkShard> {
	private int chunkSize => serverAPI.World.BlockAccessor.ChunkSize;
	private string shardTexturePath => config.GetOrCreateSubDirectory(serverAPI, config.ShardTextureDataPath);
	private TileHandlerModSystem tileHandlerModSystem => serverAPI.ModLoader.GetModSystem<TileHandlerModSystem>();

	public override string TableName => "chunks";

	public override void StartServerSide(ICoreServerAPI api) {
		base.StartServerSide(api);

		serverAPI.Network.GetChannel(ServaMapServerMod.NetworkChannelName)
				.RegisterMessageType<List<ChunkShard>>()
				.SetMessageHandler((IServerPlayer serverPlayer, List<ChunkShard> shards) => {
					logger.Notification($"{serverPlayer.PlayerName} sent: {shards.Count} chunks");
					foreach (var chunkShard in shards) {
						chunkShard.GenerationTime = DateTime.UtcNow;
						chunkShard.GeneratingPlayerId = serverPlayer.PlayerUID;
						var res = ProcessFeature(chunkShard);
						if (res.IsException)
							logger.Error(res.Exception);
						else if (res.Good)
							tileHandlerModSystem.AddShardToTile(chunkShard);
					}
				});

		serverAPI.ChatCommands.GetOrCreate("servamapadmin")
				.WithSub("exportwholemap",
						command => command.WithDesc("Export the whole server map as one .PNG")
								.RequiresPrivilege(Privilege.controlserver)
								.HandleWith(ExportWholeMapHandler));
	}

	public override void InitializeDatabase() {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
CREATE TABLE IF NOT EXISTS {TableName}
(
    x UNIQUE,
    y UNIQUE,
    
    generation_time UNIQUE,
    image_hash UNIQUE,
    generating_player_id NOT NULL
)
";
			command.ExecuteNonQuery();
			Directory.CreateDirectory(shardTexturePath);
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to open the tile path or database:");
			logger.Error(e.ToString());
		}
	}

	public override Result<bool> ProcessFeature(ChunkShard shard) {
		if (shard.ShardImage is null)
			return new ArgumentException("Given chunk shard image is null.");
		if (shard.ChunkCoords is null)
			return new ArgumentException("Given chunk shard coordinates are null.");
		if (shard.ShardImage.Length != chunkSize * chunkSize)
			return new ArgumentException(
					$"Given chunk shard is an invalid size. Expected {chunkSize * chunkSize}, got {shard.ShardImage.Length}.");
		var shouldUpdate = ShouldUpdate(shard);
		if (shouldUpdate.IsException)
			return shouldUpdate;
		if (!shouldUpdate.Good)
			return false;
		var writeRes = WriteImage(shard);
		if (writeRes is not null)
			return writeRes;
		return Update(shard);
	}

	private Exception WriteImage(ChunkShard shard) {
		try {
			var coords = shard.ChunkCoords;

			var path = Path.Combine(shardTexturePath, $"{coords.X}_{coords.Y}.proto");

			if (File.Exists(path))
				File.Delete(path);

			using var stream = File.OpenWrite(path);
			Serializer.Serialize(stream, shard);
			return null;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to write a chunk shard image.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public override Result<bool> ShouldUpdate(ChunkShard shard) {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
SELECT * FROM {TableName}
WHERE x = $x
  AND y = $y
  AND image_hash = $image_hash
  AND generation_time >= $new_generation_time
";
			command.Parameters.AddWithValue("$x", shard.ChunkCoords.X);
			command.Parameters.AddWithValue("$y", shard.ChunkCoords.Y);

			command.Parameters.AddWithValue("$new_generation_time", shard.GenerationTime);
			command.Parameters.AddWithValue("$image_hash", shard.ShardImage.GetHashCode());
			var reader = command.ExecuteReader();
			// This is a totally new shard.
			return !reader.HasRows;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to test if a chunk shard should update.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public override Result<bool> Update(ChunkShard shard) {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
INSERT OR REPLACE INTO {TableName}
VALUES ($x, $y, $generation_time, $image_hash, $generating_player_id)
";
			command.Parameters.AddWithValue("$x", shard.ChunkCoords.X);
			command.Parameters.AddWithValue("$y", shard.ChunkCoords.Y);

			command.Parameters.AddWithValue("$generation_time", shard.GenerationTime);
			command.Parameters.AddWithValue("$image_hash", shard.ShardImage.GetHashCode());
			command.Parameters.AddWithValue("$generating_player_id", shard.GeneratingPlayerId);
			var rowsAffected = command.ExecuteNonQuery();
			return rowsAffected > 0;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to update a shard.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public override Result<bool> Delete(ChunkShard shard) {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
DELETE FROM {TableName}
WHERE x = $x
  AND y = $y
";
			command.Parameters.AddWithValue("$x", shard.ChunkCoords.X);
			command.Parameters.AddWithValue("$y", shard.ChunkCoords.Y);
			var rowsAffected = command.ExecuteNonQuery();
			return rowsAffected > 0;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to delete a chunk shard.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public override Exception Clear() {
		var res = base.Clear();
		if (res is not null)
			return res;
		try {
			var allFiles = Directory.GetFiles(shardTexturePath);
			foreach (var file in allFiles)
				File.Delete(file);
			return null;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to clear the chunk tile path.");
			logger.Error(e.ToString());
			return e;
		}
	}

	public Exception ExportWholeMap() {
		string[] shardsPaths;
		try {
			shardsPaths = Directory.GetFiles(shardTexturePath);
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to enumerate chunk shards while exporting the whole map.");
			logger.Error(e);
			return e;
		}

		int maxX = int.MinValue, maxY = int.MinValue, minX = int.MaxValue, minY = int.MaxValue;
		var texs = new List<(Vec2i, int[])>();
		foreach (var shardPath in shardsPaths) {
			var s = Path.GetFileNameWithoutExtension(shardPath).Split('_');

			var x = int.Parse(s[0]);
			var y = int.Parse(s[1]);

			maxX = Helpers.Max(x, maxX);
			maxY = Helpers.Max(y, maxY);
			minX = Helpers.Min(x, minX);
			minY = Helpers.Min(y, minY);

			try {
				using var readerStream = File.OpenRead(shardPath);
				var shard = Serializer.Deserialize<ChunkShard>(readerStream);
				texs.Add((new Vec2i(x, y), shard.ShardImage));
			}
			catch (Exception e) {
				logger.Error("Serv-a-Map failed to load a chunk shards while exporting the whole map.");
				logger.Error(e);
				return e;
			}
		}

		var width = (maxX - minX + 1) * chunkSize;
		var height = (maxY - minY + 1) * chunkSize;

		var origin = new Vec2i(minX, minY);

		var fullTex = new int[height * width];

		InsertIntoFullTexture(texs, origin, width, fullTex);

		logger.Notification("starting write");

		var fullMapPath = Path.Combine(config.GetOrCreateServerMapFullDirectory(serverAPI), "fullmap.png");

		var exception = WriteBitmapToFile(width, height, fullTex, fullMapPath);
		
		if (exception is not null)
			return exception;

		logger.Notification("Done writing");

		return null;
	}

	private Exception WriteBitmapToFile(int width, int height, int[] fullTex, string fullMapPath) {
		try {
			using var bitMap = new SKBitmap(width, height, true);
			bitMap.CopyToBitmap(fullTex);
			using var file = File.OpenWrite(fullMapPath);
			bitMap.Encode(SKEncodedImageFormat.Png, 100).SaveTo(file);
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to create the final image file while exporting the whole map.");
			logger.Error(e);
			return e;
		}
		return null;
	}

	private void InsertIntoFullTexture(List<(Vec2i, int[])> texs, Vec2i origin, int width, int[] fullTex) {
		foreach (var (coord, tex) in texs) {
			coord.Add(-origin.X, -origin.Y);

			for (var y = 0; y < chunkSize; y++) {
				var actualY = (coord.Y * chunkSize + y) * width;
				var row = y * chunkSize;

				for (var x = 0; x < chunkSize; x++) {
					var actualX = coord.X * chunkSize + x;
					var index = row + x;
					fullTex[actualY + actualX] = tex[index];
				}
			}
		}
	}

	private TextCommandResult ExportWholeMapHandler(TextCommandCallingArgs args) =>
			ExportWholeMap() is null
					? TextCommandResult.Success("Serv-a-Map exported the whole map.")
					: TextCommandResult.Error("Serv-a-Map failed to export the map! Check server-main.txt for more information.");
}