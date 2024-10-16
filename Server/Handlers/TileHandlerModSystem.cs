using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SkiaSharp;

using Vintagestory.API.Server;

namespace ServaMap.Server;

public class TileHandlerModSystem : DatabaseHandlerModSystem<Tile> {
	private string tilePath => config.GetOrCreateWebMapSubDirectory(serverAPI, config.MapApiDataPath, config.TilePath);
	private int tileResampleSize => config.ShardToTileResampleSize;
	private int tileSize => chunkSize * tileResampleSize;

	private int chunkSize => serverAPI.World.BlockAccessor.ChunkSize;
	private int baseChunkOffsetX => serverAPI.World.BlockAccessor.MapSizeX / tileResampleSize / chunkSize / 2;
	private int baseChunkOffsetY => serverAPI.World.BlockAccessor.MapSizeZ / tileResampleSize / chunkSize / 2;

	public override string TableName => "tiles";

	private ConcurrentQueue<Tile> toResample = new();

	public override void StartServerSide(ICoreServerAPI api) {
		base.StartServerSide(api);

		serverAPI.Event.RegisterGameTickListener(_ => ResamplePendingTiles(), config.TileResampleIntervalSeconds * 1000);
	}

	public override void InitializeDatabase() {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
CREATE TABLE IF NOT EXISTS {TableName}
(
    x NOT NULL,
    y NOT NULL,
    scale_level NOT NULL,
    UNIQUE(x, y, scale_level)
)
";
			command.ExecuteNonQuery();
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to open the tile database:");
			logger.Error(e.ToString());
		}
	}

	public override Result<bool> Update(Tile toUpdate) => default;

	public override Result<bool> Delete(Tile toDelete) => default;

	public void AddLotsOfShardsToTiles(List<ChunkShard> shards) {
		var tilesToWrite = new List<Tile>();
		foreach (var shard in shards) {
			var shardX = shard.ChunkCoords.X;
			var shardY = shard.ChunkCoords.Y;

			var tileX = shardX / tileResampleSize - baseChunkOffsetX;
			var tileY = shardY / tileResampleSize - baseChunkOffsetY;

			var scaleLevel = config.TileMaxScaleLevel;

			var tileIndex =
					tilesToWrite.FindIndex(other => other.X == tileX && other.Y == tileY && other.ScaleLevel == scaleLevel);
			Tile tile = null;
			if (tileIndex != -1) {
				tile = tilesToWrite[tileIndex];
			}
			else {
				tile = new Tile(scaleLevel, tileX, tileY);
				var res = GetTile(tile);
				if (res.IsException) {
					logger.Error(res.Exception);
					if (res.Exception.InnerException is { } inner)
						logger.Error(inner);
					return;
				}
				tilesToWrite.Add(tile);
			}

			var overlayShard = OverlayShard(tile, shard);
			if (overlayShard is { } overlayRes) {
				logger.Error(overlayRes);
				if (overlayRes.InnerException is { } inner)
					logger.Error(inner);
				continue;
			}

		}
		
		WriteLotsOfTiles(tilesToWrite);
	}

	public void AddShardToTile(ChunkShard shard) {
		var shardX = shard.ChunkCoords.X;
		var shardY = shard.ChunkCoords.Y;

		var tileX = shardX / tileResampleSize - baseChunkOffsetX;
		var tileY = shardY / tileResampleSize - baseChunkOffsetY;

		var scaleLevel = config.TileMaxScaleLevel;

		var tile = new Tile(scaleLevel, tileX, tileY);

		var res = GetTile(tile);
		if (res.IsException) {
			logger.Error(res.Exception);
			if (res.Exception.InnerException is { } inner)
				logger.Error(inner);
			return;
		}

		var overlayShard = OverlayShard(tile, shard);
		if (overlayShard is { } overlayRes) {
			logger.Error(overlayRes);
			if (overlayRes.InnerException is { } inner)
				logger.Error(inner);
			return;
		}

		var writeTile = WriteTile(tile);

		if (writeTile is { } writeRes) {
			logger.Error(writeRes);
			if (writeRes.InnerException is { } inner)
				logger.Error(inner);
		}
	}

	public void ResamplePendingTiles() {
		Task.Run(() => {
			logger.Debug("Starting Resample");
			if (toResample.IsEmpty) {
				logger.Debug("Nothing to resample");
				return;
			}
			while (toResample.TryDequeue(out var tile))
				ResampleTile(tile);
			logger.Debug("Done resampling");
		});
	}

	public void ResampleTile(Tile tile) {
		var res = GetTile(tile);
		if (res.IsException) {
			logger.Error(res.Exception);
			return;
		}

		if (!res.Good)
			tile.Texture = new SKBitmap(tileSize, tileSize, true);

		var subRes = GetSubTiles(tile);
		if (subRes.IsException) {
			logger.Error(subRes.Exception);
			return;
		}
		var subTiles = subRes.Good;

		foreach (var subTile in subTiles) {
			if (subTile.Texture is null)
				continue;
			if (OverlayResample(tile, subTile) is { } overlayRes) {
				logger.Error(overlayRes);
				if (overlayRes.InnerException is { } inner)
					logger.Error(inner);
			}
		}
		if (WriteTile(tile) is { } writeRes) {
			logger.Error(writeRes);
			if (writeRes.InnerException is { } inner)
				logger.Error(inner);
		}
	}

	private Result<Tile[]> GetSubTiles(Tile tile) {
		var scaleLevel = tile.ScaleLevel;
		if (scaleLevel == config.TileMaxScaleLevel)
			return new ArgumentOutOfRangeException("tile", "Tile's scale level was too big!");
		var x = tile.X * 2;
		var y = tile.Y * 2;
		var subScaleLevel = scaleLevel + 1;
		var offsetX = 1;
		var offsetY = 1;

		var subTiles = new[] {
			new Tile(subScaleLevel, x, y),
			new Tile(subScaleLevel, x, y + offsetY),
			new Tile(subScaleLevel, x + offsetX, y),
			new Tile(subScaleLevel, x + offsetX, y + offsetY)
		};

		foreach (var subTile in subTiles) {
			var res = GetTile(subTile);
			if (res.IsException)
				return res.Exception;
		}
		return subTiles;
	}

	/// <summary>
	/// Given a tile with scale and position check if the tile exists and, if it does, attempt to load the texture.
	/// </summary>
	/// <param name="tile">The tile to fetch.</param>
	/// <returns>A result containing either an error, or a bool.
	/// The bool is true if the tile was loaded succesfully.</returns>
	public Result<bool> GetTile(Tile tile) {
		var scaleLevel = tile.ScaleLevel;
		var x = tile.X;
		var y = tile.Y;
		try {
			using var command = conn.CreateCommand();
			command.CommandText = $@"
SELECT * FROM {TableName}
WHERE x = $x AND y = $y and scale_level = $scale_level
";
			command.Parameters.AddWithValue("$x", x);
			command.Parameters.AddWithValue("$y", y);
			command.Parameters.AddWithValue("$scale_level", scaleLevel);

			var reader = command.ExecuteReader();
			// new tile!
			if (!reader.HasRows) {
				tile.Texture = new SKBitmap(tileSize, tileSize, true);
				return false;
			}

			var path = Path.Combine(tilePath, tile.TilePath);
			using var stream = new SKFileStream(path);
			using var image = SKImage.FromEncodedData(stream);
			tile.Texture = SKBitmap.FromImage(image);
			return true;
		}
		catch (Exception e) {
			return new Exception("Serv-a-Map failed to get a tile from the database.", e);
		}
	}

	private Exception OverlayShard(Tile tile, ChunkShard shard) {
		try {
			var tileBm = tile.Texture;
			if (tile.Texture is null)
				throw new NullReferenceException("Tile texture is null!");

			using var shardBm = shard.ToBitmap();

			var shardSubX = (shard.ChunkCoords.X % tileResampleSize) * chunkSize;
			var shardSubY = (shard.ChunkCoords.Y % tileResampleSize) * chunkSize;

			using var canvas = new SKCanvas(tileBm);
			canvas.DrawBitmap(shardBm, shardSubX, shardSubY);
			return null;
		}
		catch (Exception e) {
			return new Exception("Serv-a-Map failed to overlay a shard onto a tile.", e);
		}
	}

	private Exception OverlayResample(Tile tile, Tile subTile) {
		try {
			var tileBm = tile.Texture;
			var subTileBm = subTile.Texture;
			if (tileBm is null)
				throw new NullReferenceException("Tile's texture is null!");
			if (subTileBm is null)
				throw new NullReferenceException("SubTile's texture is null!");

			var tileSubX = Math.Abs(subTile.X % 2) * (tileSize / 2);
			var tileSubY = Math.Abs(subTile.Y % 2) * (tileSize / 2);

			var resizedTile = subTileBm.Resize(new SKSizeI(tileSize / 2, tileSize / 2), SKFilterQuality.High);

			using var canvas = new SKCanvas(tileBm);
			canvas.DrawBitmap(resizedTile, tileSubX, tileSubY);
			return null;
		}
		catch (Exception e) {
			return e;
		}
	}

	public Exception WriteTile(Tile tile) {
		try {
			if (tile.Texture is null)
				throw new NullReferenceException("Tile texture is null!");
			if (tile.Texture.DrawsNothing)
				return null;
			var path = Path.Combine(tilePath, tile.TilePath);

			using var file = File.Open(path, FileMode.OpenOrCreate);
			tile.Texture.Encode(file, SKEncodedImageFormat.Png, 100);

			using var command = conn.CreateCommand();
			command.CommandText = @$"
INSERT OR REPLACE INTO {TableName}
VALUES ($x, $y, $scale_level)
";
			command.Parameters.AddWithValue("$x", tile.X);
			command.Parameters.AddWithValue("$y", tile.Y);
			command.Parameters.AddWithValue("$scale_level", tile.ScaleLevel);
			command.ExecuteNonQuery();

			if (tile.ScaleLevel > 0) {
				AddSuperTileToResample(tile);
			}

			return null;
		}
		catch (Exception e) {
			return new Exception("Serv-a-Map failed to write a tile to the disk.", e);
		}
	}

	public Exception WriteLotsOfTiles(List<Tile> tiles) {
		var tilesToWrite = new List<Tile>();

		foreach (var tile in tiles) {
			if (tile.Texture is null)
				if (tile.Texture.DrawsNothing)
					continue;
			try {
				var path = Path.Combine(tilePath, tile.TilePath);

				using var file = File.Open(path, FileMode.OpenOrCreate);
				tile.Texture.Encode(file, SKEncodedImageFormat.Png, 100);
				tilesToWrite.Add(tile);
			}
			catch (Exception) { }
		}
		
		// this is gross but i dont know how else to make this a batch operation.
		var commandText = new StringBuilder();
		foreach (var tile in tilesToWrite) {
			commandText.Append($"INSERT OR REPLACE INTO {TableName} VALUES ({tile.X}, {tile.Y}, {tile.ScaleLevel}); ");
			if (tile.ScaleLevel > 0)
				AddSuperTileToResample(tile);
		}

		using var command = conn.CreateCommand();
		command.CommandText = commandText.ToString();
		command.ExecuteNonQuery();
		return null;
	}

	private void AddSuperTileToResample(Tile tile) {
		var superScale = tile.ScaleLevel - 1;
		var x = tile.X == -1 ? -1 : (tile.X / 2);
		var y = tile.Y == -1 ? -1 : (tile.Y / 2);

		var superTile = new Tile(superScale, x, y);
		if (!toResample.Contains(superTile))
			toResample.Enqueue(superTile);
	}

	public Exception StartCompleteResample() {
		try {
			var baseDeleteRes = DeleteAllButBaseTiles();
			if (baseDeleteRes is not null)
				throw baseDeleteRes;

			using var command = conn.CreateCommand();
			command.CommandText = $@"
SELECT * FROM {TableName}
";

			var reader = command.ExecuteReader();
			if (!reader.HasRows)
				return null;

			while (reader.Read()) {
				var x = reader.GetInt32(0);
				var y = reader.GetInt32(1);
				var tile = new Tile(0, x, y);

				AddSuperTileToResample(tile);
			}
			return null;
		}
		catch (Exception e) {
			return e;
		}
	}

	private Exception DeleteAllButBaseTiles() {
		try {
			var tilePaths = Directory.GetFiles(tilePath);
			foreach (var thisTilePath in tilePaths) {
				var basePath = Path.GetFileName(thisTilePath);
				if (!basePath.StartsWith(config.TileMaxScaleLevel.ToString()))
					File.Delete(thisTilePath);
			}

			using var command = conn.CreateCommand();
			command.CommandText = @$"
DELETE FROM {TableName}
WHERE scale_level != {config.TileMaxScaleLevel}
";
			command.ExecuteNonQuery();
			return null;
		}
		catch (Exception e) {
			return new Exception("Serv-a-Map failed to delete all but base tiles.", e);
		}
	}

	public override Exception Clear() {
		if (base.Clear() is { } res)
			return res;
		try {
			var allFiles = Directory.GetFiles(tilePath);
			foreach (var file in allFiles)
				File.Delete(file);
			return null;
		}
		catch (Exception e) {
			return new Exception("Serv-a-Map failed to clear the chunk tile path.", e);
		}
	}
}