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

	internal LandmarkHandlerModSystem landmarkHandlerModSystem;
	private ILogger logger;
	private ICoreServerAPI serverAPI;
	internal ShardHandlerModSystem shardHandlerModSystem;
	internal TeleporterHandlerModSystem teleporterHandlerModSystem;
	internal TraderHandlerModSystem traderHandlerModSystem;

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
						command =>
								command.WithDesc("Clear the entire map for the server. THIS CANNOT BE UNDONE!")
										.HandleWith(ClearCommandHandler))
				.WithSub("exportjson",
						command => command.WithDesc("Export point of interest data.")
								.HandleWith(ExportJsonHandler));

		shardHandlerModSystem = serverAPI.ModLoader.GetModSystem<ShardHandlerModSystem>();
		teleporterHandlerModSystem = serverAPI.ModLoader.GetModSystem<TeleporterHandlerModSystem>();
		traderHandlerModSystem = serverAPI.ModLoader.GetModSystem<TraderHandlerModSystem>();
		landmarkHandlerModSystem = serverAPI.ModLoader.GetModSystem<LandmarkHandlerModSystem>();
	}

	private TextCommandResult ExportJsonHandler(TextCommandCallingArgs args) {
		try {
			teleporterHandlerModSystem.WriteGeoJson();
			traderHandlerModSystem.WriteGeoJson();
			landmarkHandlerModSystem.WriteGeoJson();
		}
		catch (Exception e) {
			logger.Error(
					"Serv-a-Map failed to export PoI JSON. Check server-main.txt for more information.");
			logger.Error(e);
			return TextCommandResult.Error(
					"Serv-a-Map failed to export PoI JSON. Check server-main.txt for more information.");
		}
		return TextCommandResult.Success("Successfully wrote JSON.");
	}

	private TextCommandResult ClearCommandHandler(TextCommandCallingArgs args) {
		try {
			shardHandlerModSystem.Clear();
			teleporterHandlerModSystem.Clear();
			traderHandlerModSystem.Clear();
			landmarkHandlerModSystem.Clear();
		}
		catch (Exception e) {
			logger.Error(
					"Serv-a-Map failed to clear the map. Check server-main.txt for more information.");
			logger.Error(e);
			return TextCommandResult.Error(
					"Serv-a-Map failed to clear the map. Check server-main.txt for more information.");
		}
		return TextCommandResult.Success("Successfully cleared the map.");
	}
}