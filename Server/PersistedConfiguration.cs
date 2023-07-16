using System.Collections.Generic;
using System.Drawing;
using System.IO;

using Newtonsoft.Json;

using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ServaMap.Server;

public class PersistedConfiguration {
	public string DBFileName { get; set; } = "serverMapDB";

	[JsonIgnore]
	private Dictionary<int, int> designatorsAsInts = null;

	[JsonIgnore]
	private string serverMapFullPath = null;

	public string ServerMapPath { get; set; } = "ServerMapData";

	public string ShardTextureDataPath { get; set; } = "shardTextureData";

	public string TilePath { get; set; } = "tiles";

	public int GeoJsonAutoExportIntervalSeconds { get; set; } = 10;

	public int TileResampleIntervalSeconds { get; set; } = 30;

	public int TileMaxScaleLevel { get; set; } = 4;
	
	/// <summary>
	/// The size of tiles in terms of the shard size.
	/// eg, 4 is 4 x 4 shards, at 32px x 32px shards that's 128px x 128px tiles.
	/// </summary>
	public int TileResampleSize { get; set; } = 4;
	
	public string TileApiUrlPrefix { get; set; } = "http://localhost:8080/";

	//All - Designators, setup
	public Dictionary<AssetLocation, Color> BlockReplacementDesignators { get; set; } = new() {
		{
			new AssetLocation("game", "stonepath*"), Color.Yellow
		}, {
			new AssetLocation("game", "sign*"), Color.Teal
		}, {
			new AssetLocation("game", "statictranslocator-normal*"), Color.SteelBlue
		}, {
			new AssetLocation("game", "teleporterbase"), Color.SeaGreen
		}
	};

	public Dictionary<int, int> GetDesignatorsAsInts(ICoreServerAPI api) {
		if (designatorsAsInts is null) {
			var designatorList = new Dictionary<int, int>();

			var blocks = api.World.Blocks;

			foreach (var designator in BlockReplacementDesignators)
					// api.Logger.Notification(designator.Key.ToString());
			foreach (var block in blocks) {
				if (block.WildCardMatch(designator.Key.Path))
					designatorList.Add(block.Id, designator.Value.ToArgb());
			}
			designatorsAsInts = designatorList;
		}
		return designatorsAsInts;
	}

	/// <summary>
	///   Returns the full path of the Serv-a-Map data directory.
	/// </summary>
	/// <param name="api">The server API.</param>
	/// <returns></returns>
	public string GetOrCreateServerMapFullDirectory(ICoreServerAPI api) {
		if (serverMapFullPath is null) {
			var worldId = Path.GetFileNameWithoutExtension(api.WorldManager.CurrentWorldName);
			var finalPath = Path.Combine(ServerMapPath, worldId);
			serverMapFullPath = api.GetOrCreateDataPath(finalPath);
		}
		return serverMapFullPath;
	}

	public string GetOrCreateSubDirectory(ICoreServerAPI api, string subPath) {
		var fullSubPath = Path.Combine(GetOrCreateServerMapFullDirectory(api), subPath);
		if (!Directory.Exists(fullSubPath))
			Directory.CreateDirectory(fullSubPath);
		return fullSubPath;
	}
}