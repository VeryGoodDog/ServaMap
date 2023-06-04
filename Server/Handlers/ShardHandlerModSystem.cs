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

public class ShardHandlerModSystem : DatabaseHandlerModSystem {
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
						ProcessShard(chunkShard);
					}
				});

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

		serverAPI.ChatCommands.GetOrCreate("servamapadmin")
				.WithSub("exportwholemap",
						command => command.WithDesc("Export the whole server map as one .PNG")
								.RequiresPrivilege(Privilege.controlserver)
								.HandleWith(ExportWholeMapHandler));
	}

	/// <summary>
	///   Checks against the shard database to see if the shard is actually different.
	///   If it is different, then make the change in the DB and the chunk tile.
	/// </summary>
	/// <param name="shard"></param>
	/// <returns>true if the shard was processed without error; otherwise, false.</returns>
	public bool ProcessShard(ChunkShard shard) {
		if (shard.IsInvalid)
			return false;
		if (shard.ShardImage.Length != chunkSize * chunkSize)
			return false;
		try {
			logger.Notification($"Processing shard: {shard.ChunkCoords} {shard.GenerationTime}");
			if (!ShouldUpdateShard(shard))
				return true;
			UpdateShard(shard);
			WriteImage(shard);
		}
		catch (Exception e) {
			logger.Error($"Serv-a-Map failed to update a shard: {shard.ChunkCoords}");
			logger.Error(e.ToString());
			return false;
		}
		return true;
	}

	private void WriteImage(ChunkShard shard) {
		var coords = shard.ChunkCoords;
		var shardImage = shard.SquareShardImage;

		var path = Path.Combine(shardTilePath, $"{coords.X}_{coords.Y}.png");

		using (var stream = FileHelper.OpenFileForWriting(path, true)) {
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
		}
	}

	private bool ShouldUpdateShard(ChunkShard shard) {
		using (var command = conn.CreateCommand()) {
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
	}

	private void UpdateShard(ChunkShard shard) {
		using (var command = conn.CreateCommand()) {
			command.CommandText = @$"
INSERT OR REPLACE INTO {TableName}
VALUES ($x, $y, $generation_time, $image_hash, $generating_player_id)
";
			command.Parameters.AddWithValue("$x", shard.ChunkCoords.X);
			command.Parameters.AddWithValue("$y", shard.ChunkCoords.Y);

			command.Parameters.AddWithValue("$generation_time", shard.GenerationTime);
			command.Parameters.AddWithValue("$image_hash", shard.ShardImage.GetHashCode());
			command.Parameters.AddWithValue("$generating_player_id", shard.GeneratingPlayerId);
			command.ExecuteNonQuery();
		}
	}

	public override void Clear() {
		base.Clear();
		var allFiles = Directory.GetFiles(shardTilePath);
		foreach (var file in allFiles)
			File.Delete(file);
	}

	public bool ExportWholeMap() {
		string[] pngs;
		try {
			pngs = Directory.GetFiles(config.GetShardTileFullPath(serverAPI));
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to enumerate chunk shards while exporting the whole map.");
			logger.Error(e);
			return false;
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
				using (var readerStream = FileHelper.OpenFileForReading(png)) {
					var reader = new PngReader(readerStream);
					var rows = reader.ReadRowsInt();
					var lines = rows.Scanlines;
					texs.Add((new Vec2i(x, y), lines));
				}
			}
			catch (Exception e) {
				logger.Error("Serv-a-Map failed to load a chunk shards while exporting the whole map.");
				logger.Error(e);
				return false;
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
			return false;
		}

		logger.Notification("Done writing");

		return true;
	}

	private TextCommandResult ExportWholeMapHandler(TextCommandCallingArgs args) =>
			ExportWholeMap()
					? TextCommandResult.Success("Serv-a-Map exported the whole map.")
					: TextCommandResult.Error(
							"Serv-a-Map failed to export the map! Check server-main.txt for more information.");
}