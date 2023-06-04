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
	// Used to rate limit chunk changes
	private readonly ConcurrentDictionary<Vec2i, long> chunkUpdateTime = new();
	private ChunkRenderer chunkRenderer;

	private int chunkSize;
	private ICoreClientAPI clientAPI;
	private IClientNetworkChannel clientNetworkChannel;

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
		clientAPI.ChatCommands.Create("servamapclient")
				.WithDesc("Control the Serv-a-Map client mod.")
				.WithAlias("smclient")
				.WithSub("pause",
						command => {
							command.WithDesc("Pause chunk rendering.")
									.WithAlias("stop", "off")
									.HandleWith(_ => {
										clientAPI.Event.UnregisterGameTickListener(listenerId);
										listenerId = 0;
										return TextCommandResult.Success("Paused rendering!");
									});
						})
				.WithSub("resume",
						command => {
							command.WithDesc("Resume chunk rendering.")
									.WithAlias("start", "on")
									.HandleWith(_ => {
										RegisterTickListender();
										return TextCommandResult.Success("Resumed rendering!");
									});
						})
				.WithSub("toggle",
						command => {
							command.WithDesc("Toggle chunk rendering.")
									.HandleWith(_ => {
										if (listenerId == 0)
											RegisterTickListender();
										else
											clientAPI.Event.UnregisterGameTickListener(listenerId);
										return TextCommandResult.Success("Toggled chunk rendering!");
									});
						});
	}

	private void OnLevelFinalize() {
		logger.Notification("Sending ready packet.");
		clientNetworkChannel.SendPacket(new ReadyPacket());
	}

	private void DesginatorHandler(Dictionary<int, int> designators) {
		logger.Notification($"Got {designators.Count} designators.");
		chunkRenderer = new ChunkRenderer(clientAPI, designators);
		clientAPI.Event.ChunkDirty += EventOnChunkDirty;
		RegisterTickListender();
	}

	private void RegisterTickListender() =>
			listenerId =
					clientAPI.Event.RegisterGameTickListener(_ => Task.Run(ProcessChunkUpdates), 1000);

	// Every second, go through the list of chunk changes and find the ones that havent changed in the last 5 seconds
	private void ProcessChunkUpdates() {
		// clientAPI.ShowChatMessage("start");
		var timer = Stopwatch.StartNew();
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

		timer.Stop();
		if (chunksToSend.Count > 0)
			clientNetworkChannel.SendPacket(chunksToSend);
		// clientAPI.ShowChatMessage($"Renders took {timer.ElapsedMilliseconds / chunksToSend.Count}ms each");
	}

	private void EventOnChunkDirty(Vec3i chunkcoord, IWorldChunk chunk, EnumChunkDirtyReason reason) {
		var currentTime = clientAPI.ElapsedMilliseconds;
		var actualCoords = new Vec2i(chunkcoord.X, chunkcoord.Z);
		chunkUpdateTime[actualCoords] = currentTime;
	}
}