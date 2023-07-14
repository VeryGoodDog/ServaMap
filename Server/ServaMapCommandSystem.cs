using System;

using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Server;

namespace ServaMap.Server;

/// <summary>
///   Handles commands that don't belong in more specific places.
/// </summary>
public class ServaMapCommandSystem : ModSystem {
	private ServaMapServerMod coreMod;

	private ILogger logger;
	private ICoreServerAPI serverAPI;
	internal LandmarkHandlerModSystem landmarkHandlerModSystem;
	internal ShardHandlerModSystem shardHandlerModSystem;
	internal TeleporterHandlerModSystem teleporterHandlerModSystem;
	internal TraderHandlerModSystem traderHandlerModSystem;
	internal TileHandlerModSystem tileHandlerModSystem;

	public override bool ShouldLoad(EnumAppSide forSide) => forSide.IsServer();
	public override double ExecuteOrder() => 0.3;

	public override void StartServerSide(ICoreServerAPI api) {
		serverAPI = api;
		logger = serverAPI.Logger;
		coreMod = serverAPI.ModLoader.GetModSystem<ServaMapServerMod>();

		serverAPI.ChatCommands.GetOrCreate("servamapadmin")
				.RequiresPrivilege(Privilege.controlserver)
				.WithDesc("Administrator controls for Serv-a-Map")
				.WithAlias("smadmin")
				.WithSub("clear",
						command => command.WithDesc("Clear the entire map for the server. THIS CANNOT BE UNDONE!")
								.HandleWith(ClearCommandHandler))
				.WithSub("exportjson",
						command => command.WithDesc("Export point of interest data.").HandleWith(ExportJsonHandler))
				.WithSub("resample", command => command.WithDesc("Resample all tiles.").HandleWith(ResampleTilesHandler));

		shardHandlerModSystem = serverAPI.ModLoader.GetModSystem<ShardHandlerModSystem>();
		teleporterHandlerModSystem = serverAPI.ModLoader.GetModSystem<TeleporterHandlerModSystem>();
		traderHandlerModSystem = serverAPI.ModLoader.GetModSystem<TraderHandlerModSystem>();
		landmarkHandlerModSystem = serverAPI.ModLoader.GetModSystem<LandmarkHandlerModSystem>();
		tileHandlerModSystem = serverAPI.ModLoader.GetModSystem<TileHandlerModSystem>();
	}
	
	// There has GOT to be a better way to do these
	private TextCommandResult ExportJsonHandler(TextCommandCallingArgs args) {
		var tpRes = teleporterHandlerModSystem.WriteGeoJson();
		var trRes = traderHandlerModSystem.WriteGeoJson();
		var lmRes = landmarkHandlerModSystem.WriteGeoJson();

		if (tpRes is null && trRes is null && lmRes is null)
			return TextCommandResult.Success("Successfully wrote JSON.");

		logger.Error("Serv-a-Map failed to export PoI JSON.");

		if (tpRes is not null)
			logger.Error(tpRes);
		if (trRes is not null)
			logger.Error(trRes);
		if (lmRes is not null)
			logger.Error(lmRes);

		return TextCommandResult.Error("Serv-a-Map failed to export PoI JSON. Check server-main.txt for more information.");
	}

	private TextCommandResult ClearCommandHandler(TextCommandCallingArgs args) {
		var shardClear = shardHandlerModSystem.Clear();
		var tpClear = teleporterHandlerModSystem.Clear();
		var trClear = traderHandlerModSystem.Clear();
		var lmClear = landmarkHandlerModSystem.Clear();
		var tileClear = tileHandlerModSystem.Clear();

		if (shardClear is null && tpClear is null && trClear is null && lmClear is null && tileClear is null)
			return TextCommandResult.Success("Successfully cleared the map.");

		logger.Error("Serv-a-Map failed to clear the map.");

		if (shardClear is not null)
			logger.Error(shardClear);
		if (tpClear is not null)
			logger.Error(tpClear);
		if (trClear is not null)
			logger.Error(trClear);
		if (lmClear is not null)
			logger.Error(lmClear);
		if (tileClear is not null)
			logger.Error(tileClear);

		return TextCommandResult.Error("Serv-a-Map failed to clear the map. Check server-main.txt for more information.");
	}

	private TextCommandResult ResampleTilesHandler(TextCommandCallingArgs args) {
		tileHandlerModSystem.StartCompleteResample();
		return TextCommandResult.Success("Started resample.");
	}
}