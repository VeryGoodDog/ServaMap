using System;
using System.Collections.Generic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ServaMap.Client;

public class ChunkRenderer {
	private readonly Dictionary<int, int> blockDesignators;
	private readonly int chunkSize;
	private readonly ICoreClientAPI clientAPI;
	private ILogger logger;

	/// <summary>
	///   Renders shards similar to the Default VS version, plus P.O.I. markings.
	/// </summary>
	/// <param name="api">Client API.</param>
	/// <param name="designators"></param>
	public ChunkRenderer(ICoreClientAPI api, Dictionary<int, int> designators) {
		clientAPI = api;
		logger = clientAPI.Logger;
		chunkSize = clientAPI.World.BlockAccessor.ChunkSize;
		blockDesignators = designators;
	}

	private IBlockAccessor BlockAccessor => clientAPI.World.BlockAccessor;

	public ChunkShard GenerateChunkShard(Vec2i chunkPos) {
		// Prefetch current chunk column
		var tempChunkColumn = GetTempChunkColumn(chunkPos);
		if (tempChunkColumn is null)
			return null;

		// Fetch map chunks
		var northWestChunk = BlockAccessor.GetMapChunk(chunkPos.X - 1, chunkPos.Y - 1);
		var southWestChunk = BlockAccessor.GetMapChunk(chunkPos.X - 1, chunkPos.Y);
		var northEastChunk = BlockAccessor.GetMapChunk(chunkPos.X, chunkPos.Y - 1);
		var southEastChunk = BlockAccessor.GetMapChunk(chunkPos.X, chunkPos.Y);

		var chunkShard = new int[chunkSize * chunkSize];

		var tmpPos = new BlockPos();
		var localPos = new Vec2i();

		for (var index = 0; index < chunkSize * chunkSize; index++) {
			MapUtil.PosInt2d(index, chunkSize, localPos);
			var x = localPos.X;
			var z = localPos.Y;
			int currentBlockY = southEastChunk.RainHeightMap[index];
			var chunkColumnY = currentBlockY / chunkSize;
			if (chunkColumnY >= tempChunkColumn.Length)
				continue;

			tmpPos.Set(chunkSize * chunkPos.X + x, currentBlockY, chunkSize * chunkPos.Y + z);

			var (northWest, northEast, southWest) = CalculateAltitudeDiff(x,
					currentBlockY,
					z,
					southEastChunk,
					northWestChunk,
					northEastChunk,
					southWestChunk);

			var boostMultiplier = CalculateSlopeBoost(northWest, northEast, southWest);

			var blockIndex = MapUtil.Index3d(x, currentBlockY % chunkSize, z, chunkSize, chunkSize);

			var blockId = tempChunkColumn[chunkColumnY]
					.UnpackAndReadBlock(blockIndex, BlockLayersAccess.FluidOrSolid);

			var col = GetColor(blockId, tmpPos);

			chunkShard[index] = ColorUtil.ColorMultiply3Clamped(col, boostMultiplier);
		}
		return new ChunkShard(chunkPos, chunkShard, DateTime.UtcNow);
	}

	private IWorldChunk[] GetTempChunkColumn(Vec2i chunkPos) {
		var tempChunkColumn = new IWorldChunk[BlockAccessor.MapSizeY / chunkSize];
		for (var chunkYLevel = 0; chunkYLevel < tempChunkColumn.Length; chunkYLevel++) {
			var chunk = BlockAccessor.GetChunk(chunkPos.X, chunkYLevel, chunkPos.Y);

			if (chunk == null || !(chunk as IClientChunk).LoadedFromServer)
				return null;
			chunk.Unpack_ReadOnly();
			tempChunkColumn[chunkYLevel] = chunk;
		}
		return tempChunkColumn;
	}

	private int GetColor(int blockId, BlockPos tmpPos) {
		int col;
		if (blockDesignators.TryGetValue(blockId, out var designator)) {
			col = designator;
		}
		else {
			var block = clientAPI.World.Blocks[blockId];

			col = SwapChannels(block.GetColorWithoutTint(clientAPI, tmpPos));
			var climateMap = block.ClimateColorMapResolved;
			col = clientAPI.World.ApplyColorMapOnRgba(climateMap,
					null, // Ignore the season map to make the map look better
					col,
					tmpPos.X,
					tmpPos.Y,
					tmpPos.Z);
		}
		return col;
	}

	private int SwapChannels(int colorIn) =>
			((colorIn & byte.MaxValue) << 16)
			| // b to r
			(((colorIn >> 8) & byte.MaxValue) << 8)
			| // keep g
			((colorIn >> 16) & byte.MaxValue); // r to b

	private float CalculateSlopeBoost(int northWest, int northEast, int southWest) {
		// nw (-1, -1) | ne (0, -1)
		// sw (-1, 0)  | se (0, 0)
		var direction = Math.Sign(northWest) + Math.Sign(northEast) + Math.Sign(southWest);
		var slope = Helpers.Max(Math.Abs(northWest), Math.Abs(northEast), Math.Abs(southWest));
		var slopeFactor = Math.Min(0.5f, slope / 10f) / 1.25f;

		if (direction > 0)
			return 1.08f + slopeFactor;
		if (direction < 0)
			return 0.92f - slopeFactor;
		return 1;
	}

	private (int northWest, int northEast, int southWest) CalculateAltitudeDiff(int x, int y, int z,
			IMapChunk southEastChunk, IMapChunk northWestChunk, IMapChunk northEastChunk,
			IMapChunk southWestChunk) {
		// The slope boost calculation uses 4 blocks
		// north west, north east, south east (the current chunk), and south west
		// These chunks are selected relative to the current BLOCK
		// nw (-1, -1) | ne (0, -1)
		// sw (-1, 0)  | se (0, 0) < the current chunk is se
		IMapChunk northWestMapChunk;
		IMapChunk northEastMapChunk;
		IMapChunk southWestMapChunk;

		var westernX = x - 1;
		var easternX = x;
		var northernZ = z - 1;
		var southernZ = z;

		if (westernX < 0 && northernZ < 0) {
			// north west corner
			northWestMapChunk = northWestChunk;
			northEastMapChunk = northEastChunk;
			southWestMapChunk = southWestChunk;
		}
		else if (westernX < 0) {
			// western edge
			northWestMapChunk = southWestChunk;
			northEastMapChunk = southEastChunk;
			southWestMapChunk = southWestChunk;
		}
		else if (northernZ < 0) {
			// northern edge
			northWestMapChunk = northEastChunk;
			northEastMapChunk = northEastChunk;
			southWestMapChunk = southEastChunk;
		}
		else {
			// somewhere in the middle
			northWestMapChunk = southEastChunk;
			northEastMapChunk = southEastChunk;
			southWestMapChunk = southEastChunk;
		}

		westernX = GameMath.Mod(westernX, chunkSize);
		northernZ = GameMath.Mod(northernZ, chunkSize);

		// difference from current height to the height of the block in this direction

		var northWest = MapUtil.Index2d(westernX, northernZ, chunkSize);
		var northWestDelta = northWestMapChunk == null
				? 0
				: y - northWestMapChunk.RainHeightMap[northWest];

		var northEast = MapUtil.Index2d(easternX, northernZ, chunkSize);
		var northEastDelta = northEastMapChunk == null
				? 0
				: y - northEastMapChunk.RainHeightMap[northEast];

		var southWest = MapUtil.Index2d(westernX, southernZ, chunkSize);
		var southWestDelta = southWestMapChunk == null
				? 0
				: y - southWestMapChunk.RainHeightMap[southWest];

		return (northWestDelta, northEastDelta, southWestDelta);
	}
}