﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ServaMap.Server;

/// <summary>
///   This handles all behavior that doesn't fit better anywhere elss.
/// </summary>
public class ServaMapServerMod : ModSystem {
	public const string _configFilename = @"servamap.json";
	public const string NetworkChannelName = "servamapdata";
	private ILogger logger;

	private IServerNetworkChannel networkChannel;

	private ICoreServerAPI serverAPI;

	internal PersistedConfiguration config {
		get => serverAPI.ObjectCache[_configFilename] as PersistedConfiguration;
		set => serverAPI.ObjectCache.Add(_configFilename, value);
	}

	internal SQLiteConnection dbConnection;

	public override bool ShouldLoad(EnumAppSide forSide) => forSide.IsServer();

	public override double ExecuteOrder() => 0.2;

	public override void StartServerSide(ICoreServerAPI api) {
		serverAPI = api;
		logger = serverAPI.Logger;
		PrepareConfig();

		logger.Notification("Registering channel");
		networkChannel = serverAPI.Network.RegisterChannel(NetworkChannelName)
				.RegisterMessageType<ReadyPacket>()
				.SetMessageHandler((IServerPlayer serverPlayer, ReadyPacket _) => {
					var designators = config.GetDesignatorsAsInts(serverAPI);
					logger.Notification($"Sending designators to {serverPlayer.PlayerName}.");
					networkChannel.SendPacket(designators, serverPlayer);
				})
				.RegisterMessageType<Dictionary<int, int>>();

		serverAPI.Event.GameWorldSave += () => PersistParameterChange();

		var dBFullFilePath = Path.Combine(config.GetOrCreateServerMapFullDirectory(api),
				config.DBFileName);

		if (!File.Exists(dBFullFilePath))
			SQLiteConnection.CreateFile(dBFullFilePath);
		var connBuilder = new SQLiteConnectionStringBuilder();
		connBuilder.DataSource = dBFullFilePath;
		dbConnection = new SQLiteConnection(connBuilder.ToString());
		dbConnection.Open();
	}

	public static PersistedConfiguration GetConfig(ICoreServerAPI api) =>
			api.ObjectCache[_configFilename] as PersistedConfiguration;

	private void PrepareConfig() {
		PersistedConfiguration newConfig = null;
		try {
			newConfig = serverAPI.LoadModConfig<PersistedConfiguration>(_configFilename);
		}
		catch {
			logger.Error("Failed to reload config.");
		}

		if (newConfig == null) {
			logger.Warning("Regenerating default config as it was missing / unparsable...");
			newConfig = new PersistedConfiguration();
			serverAPI.StoreModConfig(newConfig, _configFilename);
		}

		logger.Notification($"Loaded config from {_configFilename}");
		config = newConfig;
	}

	internal void PersistParameterChange() => serverAPI.StoreModConfig(config, _configFilename);
}