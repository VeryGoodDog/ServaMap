using System;
using System.Collections.Generic;
using System.IO;

using Hjg.Pngcs;
using Hjg.Pngcs.Zlib;

using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ServaMap.Server;

public class ShardHandlerModSystem : DatabaseHandlerModSystem<ChunkShard> {
	private int chunkSize;
	private ImageInfo imageInfo;
	private string shardTilePath;

	public override string TableName => "chunks";

	public override void StartServerSide(ICoreServerAPI api) {
		base.StartServerSide(api);
		shardTilePath = config.GetShardTileFullPath(api);

		chunkSize = serverAPI.World.BlockAccessor.ChunkSize;
		imageInfo = new ImageInfo(chunkSize, chunkSize, 8, false);

		serverAPI.Network.GetChannel(ServaMapServerMod.NetworkChannelName)
				.RegisterMessageType<List<ChunkShard>>()
				.SetMessageHandler((IServerPlayer serverPlayer, List<ChunkShard> shards) => {
					logger.Notification($"{serverPlayer.PlayerName} sent: {shards.Count} chunks");
					foreach (var chunkShard in shards) {
						chunkShard.GeneratingPlayerId = serverPlayer.PlayerUID;
						ProcessFeature(chunkShard);
					}
				});

		serverAPI.ChatCommands.GetOrCreate("servamapadmin")
				.WithSub("exportwholemap",
						command => command.WithDesc("Export the whole server map as one .PNG")
								.RequiresPrivilege(Privilege.controlserver)
								.HandleWith(ExportWholeMapHandler));

		InitializeShardDatabase();
	}

	private void InitializeShardDatabase() {
		try {
			InitializeDatabase();
			using (var command = conn.CreateCommand()) {
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
			}
			Directory.CreateDirectory(shardTilePath);
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
			var shardImage = shard.SquareShardImage;

			var path = Path.Combine(shardTilePath, $"{coords.X}_{coords.Y}.png");

			using var stream = FileHelper.OpenFileForWriting(path, true);
			var pngWriter = new PngWriter(stream, imageInfo);
			var imgLine = new ImageLine(imageInfo);
			for (var row = 0; row < chunkSize; row++) {
				for (var col = 0; col < chunkSize; col++)
					ImageLineHelper.SetPixelFromARGB8(imgLine, col, shardImage[row, col]);
				pngWriter.WriteRow(imgLine.Scanline);
			}
			pngWriter.CompLevel = 5;
			pngWriter.CompressionStrategy = EDeflateCompressStrategy.Default;
			pngWriter.End();
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
			logger.Error("Serv-a-Map failed to test if a chunk shard should update.");
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
			var allFiles = Directory.GetFiles(shardTilePath);
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
		string[] pngs;
		try {
			pngs = Directory.GetFiles(config.GetShardTileFullPath(serverAPI));
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to enumerate chunk shards while exporting the whole map.");
			logger.Error(e);
			return e;
		}

		int maxX = int.MinValue, maxY = int.MinValue, minX = int.MaxValue, minY = int.MaxValue;
		var texs = new List<(Vec2i, int[][])>();
		foreach (var png in pngs) {
			var s = Path.GetFileNameWithoutExtension(png).Split('_');

			var x = int.Parse(s[0]);
			var y = int.Parse(s[1]);

			maxX = Helpers.Max(x, maxX);
			maxY = Helpers.Max(y, maxY);
			minX = Helpers.Min(x, minX);
			minY = Helpers.Min(y, minY);
			try {
				using var readerStream = FileHelper.OpenFileForReading(png);
				var reader = new PngReader(readerStream);
				var rows = reader.ReadRowsInt();
				var lines = rows.Scanlines;
				texs.Add((new Vec2i(x, y), lines));
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

		var fullTex = new int[height, width];

		foreach (var (coord, tex) in texs) {
			coord.Add(-origin.X, -origin.Y);
			for (var y = 0; y < chunkSize; y++) {
				var actualY = coord.Y * chunkSize + y;
				var row = tex[y];
				for (var x = 0; x < chunkSize; x++) {
					var actualX = coord.X * chunkSize + x;
					var r = row[3 * x + 0];
					var g = row[3 * x + 1];
					var b = row[3 * x + 2];
					fullTex[actualY, actualX] = ImageLineHelper.ToARGB8(r, g, b);
				}
			}
		}

		logger.Notification("starting write");

		var imgInfo = new ImageInfo(width, height, 8, false);
		var fullMapPath = Path.Combine(config.GetServerMapFullPath(serverAPI), "fullmap.png");

		try {
			var stream = FileHelper.OpenFileForWriting(fullMapPath, true);
			var writer = new PngWriter(stream, imgInfo);
			var imgLine = new ImageLine(imgInfo);
			for (var row = 0; row < height; row++) {
				for (var col = 0; col < width; col++)
					ImageLineHelper.SetPixelFromARGB8(imgLine, col, fullTex[row, col]);
				writer.WriteRow(imgLine.Scanline);
			}
			writer.End();
		}
		catch (Exception e) {
			logger.Error(
					"Serv-a-Map failed to create the final image file while exporting the whole map.");
			logger.Error(e);
			return e;
		}

		logger.Notification("Done writing");

		return null;
	}

	private TextCommandResult ExportWholeMapHandler(TextCommandCallingArgs args) =>
			ExportWholeMap() is null
					? TextCommandResult.Success("Serv-a-Map exported the whole map.")
					: TextCommandResult.Error(
							"Serv-a-Map failed to export the map! Check server-main.txt for more information.");
}