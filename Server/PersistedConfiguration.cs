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

	public string WebMapPath { get; set; } = "webmap";

	public string MapApiDataPath { get; set; } = "data";

	public string TilePath { get; set; } = "world";

	public string GeoJsonPath { get; set; } = "geojson";

	public int GeoJsonAutoExportIntervalSeconds { get; set; } = 10;

	public int TileResampleIntervalSeconds { get; set; } = 30;

	public int TileMaxScaleLevel { get; set; } = 4;

	/// <summary>
	/// The size of tiles in terms of the shard size.
	/// eg, 8 is 8 x 8 shards, at 32px x 32px shards that's 256px x 256px tiles.
	/// </summary>
	public int ShardToTileResampleSize { get; set; } = 8;

	public string WebServerUrlPrefix { get; set; } = "http://localhost:8080/";

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

	public string GetOrCreateSubDirectory(ICoreServerAPI api, params string[] subPath) {
		var fullSubPath = Path.Combine(GetOrCreateServerMapFullDirectory(api), Path.Combine(subPath));
		if (!Directory.Exists(fullSubPath))
			Directory.CreateDirectory(fullSubPath);
		return fullSubPath;
	}

	public string GetOrCreateWebMapSubDirectory(ICoreServerAPI api, params string[] subPath) =>
			GetOrCreateSubDirectory(api, WebMapPath, Path.Combine(subPath));
}