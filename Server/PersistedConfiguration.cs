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

	public int GeoJsonAutoExportIntervalSeconds { get; set; } = 10;

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
	public string GetServerMapFullPath(ICoreServerAPI api) {
		if (serverMapFullPath is null) {
			var worldId = Path.GetFileNameWithoutExtension(api.WorldManager.CurrentWorldName);
			var finalPath = Path.Combine(ServerMapPath, worldId);
			serverMapFullPath = api.GetOrCreateDataPath(finalPath);
		}
		return serverMapFullPath;
	}

	public string GetSubPath(ICoreServerAPI api, string subPath) =>
			Path.Combine(GetServerMapFullPath(api), subPath);
}