using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using ServaMap.Server;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;

namespace ServaMap.Client;

public class ServaMapClientMod : ModSystem {
	private const string CLIENT_CONFIG_FILENAME = "servamapclientconfig.json";

	// Used to rate limit chunk changes
	private readonly ConcurrentDictionary<Vec2i, long> chunkUpdateTime = new();
	private ChunkRenderer chunkRenderer;

	private int chunkSize;
	private ICoreClientAPI clientAPI;
	private IClientNetworkChannel clientNetworkChannel;
	private ClientConfig clientConfig;

	private long listenerId = 0;
	private ILogger logger;

	public override bool ShouldLoad(EnumAppSide forSide) => forSide.IsClient();
	public override double ExecuteOrder() => 0.2;

	public override void StartClientSide(ICoreClientAPI api) {
		clientAPI = api;
		logger = clientAPI.Logger;
		chunkSize = clientAPI.World.BlockAccessor.ChunkSize;

		clientNetworkChannel = clientAPI.Network.RegisterChannel(ServaMapServerMod.NetworkChannelName);
		clientNetworkChannel.RegisterMessageType<ReadyPacket>()
				.RegisterMessageType<Dictionary<int, int>>()
				.SetMessageHandler<Dictionary<int, int>>(DesginatorHandler)
				.RegisterMessageType<List<ChunkShard>>();

		clientAPI.Event.LevelFinalize += OnLevelFinalize;
		clientAPI.Event.LeftWorld += OnLeftWorld;
		clientAPI.Event.ChunkDirty += EventOnChunkDirty;

		clientAPI.ChatCommands.Create("servamapclient")
				.WithDesc("Control the Serv-a-Map client mod.")
				.WithAlias("smclient")
				.WithSub("pause",
						command => {
							command.WithDesc("Pause chunk rendering.")
									.WithAlias("stop", "off")
									.HandleWith(_ => {
										UnregisterGameTickListener();
										clientConfig.Render = false;
										return TextCommandResult.Success("Paused rendering!");
									});
						})
				.WithSub("resume",
						command => {
							command.WithDesc("Resume chunk rendering.")
									.WithAlias("start", "on")
									.HandleWith(_ => {
										RegisterGameTickListener();
										clientConfig.Render = true;
										return TextCommandResult.Success("Resumed rendering!");
									});
						})
				.WithSub("toggle",
						command => {
							command.WithDesc("Toggle chunk rendering.")
									.HandleWith(_ => {
										if (listenerId == 0) {
											RegisterGameTickListener();
											clientConfig.Render = true;
											return TextCommandResult.Success("Toggled on chunk rendering!");
										}
										else {
											UnregisterGameTickListener();
											clientConfig.Render = false;
											return TextCommandResult.Success("Toggled off chunk rendering!");
										}
									});
						})
				.WithSub("state",
						command => command.WithDesc("Print the rendering state.")
								.WithAlias("running", "rendering", "stopped")
								.HandleWith(_ =>
										clientConfig.Render
												? TextCommandResult.Success("The client is rendering chunks.")
												: TextCommandResult.Success("The client is NOT rendering chunks.")));

		ReadConfig();
	}

	private void OnLeftWorld() { WriteConfig(); }

	private void ReadConfig() {
		try {
			clientConfig = clientAPI.LoadModConfig<ClientConfig>(CLIENT_CONFIG_FILENAME);
		}
		catch {
			clientConfig = null;
		}
		if (clientConfig is null)
			clientConfig = new ClientConfig();
		WriteConfig();
	}

	private void WriteConfig() => clientAPI.StoreModConfig(clientConfig, CLIENT_CONFIG_FILENAME);

	private void OnLevelFinalize() {
		logger.Notification("Sending ready packet.");
		clientNetworkChannel.SendPacket(new ReadyPacket());
	}

	private void DesginatorHandler(Dictionary<int, int> designators) {
		logger.Notification($"Got {designators.Count} designators.");
		chunkRenderer = new ChunkRenderer(clientAPI, designators);
		if (clientConfig.Render)
			RegisterGameTickListener();
	}

	private void RegisterGameTickListener() {
		if (listenerId != 0)
			return;
		listenerId = clientAPI.Event.RegisterGameTickListener(_ => Task.Run(ProcessChunkUpdates), 1000);
	}

	private void UnregisterGameTickListener() {
		if (listenerId == 0)
			return;
		clientAPI.Event.UnregisterGameTickListener(listenerId);
		listenerId = 0;
	}

	// Every second, go through the list of chunk changes and find the ones that havent changed in the last 5 seconds
	private void ProcessChunkUpdates() {
		var chunksToUpdate = new List<Vec2i>();
		var currentTime = clientAPI.ElapsedMilliseconds;
		foreach (var chunkTime in chunkUpdateTime) {
			if (currentTime - chunkTime.Value > 5000)
				chunksToUpdate.Add(chunkTime.Key);
			if (chunksToUpdate.Count >= 16)
				break;
		}

		var chunksToSend = new List<ChunkShard>();

		foreach (var chunkCoord in chunksToUpdate) {
			chunkUpdateTime.TryRemove(chunkCoord, out _);
			var tex = chunkRenderer.GenerateChunkShard(chunkCoord);
			if (tex is null)
				continue;
			chunksToSend.Add(tex);
		}

		if (chunksToSend.Count > 0)
			clientNetworkChannel.SendPacket(chunksToSend);
	}

	private void EventOnChunkDirty(Vec3i chunkcoord, IWorldChunk chunk, EnumChunkDirtyReason reason) {
		var currentTime = clientAPI.ElapsedMilliseconds;
		var actualCoords = new Vec2i(chunkcoord.X, chunkcoord.Z);
		chunkUpdateTime[actualCoords] = currentTime;
	}
}