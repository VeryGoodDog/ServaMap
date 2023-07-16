using System;
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
	private HttpListener listener = new HttpListener();
	private string tilePath;

	private ICoreServerAPI serverAPI;

	internal PersistedConfiguration CachedConfiguration {
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
					var designators = CachedConfiguration.GetDesignatorsAsInts(serverAPI);
					logger.Notification($"Sending designators to {serverPlayer.PlayerName}.");
					networkChannel.SendPacket(designators, serverPlayer);
				})
				.RegisterMessageType<Dictionary<int, int>>();

		serverAPI.Event.GameWorldSave += () => PersistParameterChange();

		var dBFullFilePath = Path.Combine(CachedConfiguration.GetOrCreateServerMapFullDirectory(api),
				CachedConfiguration.DBFileName);

		
		if (!File.Exists(dBFullFilePath))
			SQLiteConnection.CreateFile(dBFullFilePath);
		var connBuilder = new SQLiteConnectionStringBuilder();
		connBuilder.DataSource = dBFullFilePath;
		dbConnection = new SQLiteConnection(connBuilder.ToString());
		dbConnection.Open();

		listener.Prefixes.Add(CachedConfiguration.TileApiUrlPrefix);
		listener.Start();
		
		tilePath = Path.Combine(CachedConfiguration.GetOrCreateServerMapFullDirectory(api), CachedConfiguration.TilePath);

		Task.Run(() => {
			logger.Notification("Starting listener task!");
			while (listener.IsListening)
				listener.BeginGetContext(ListenerCallback, listener).AsyncWaitHandle.WaitOne();
		});
		
		serverAPI.Event.ServerRunPhase(EnumServerRunPhase.Shutdown,
				() => listener.Stop());
	}

	private void ListenerCallback(IAsyncResult result) {
		var lis = (HttpListener)result.AsyncState;
		var context = lis.EndGetContext(result);
		var request = context.Request;
		var response = context.Response;

		var loc = request.Url;
		var localLoc = tilePath + loc.LocalPath;
		

		byte[] buffer;
		if (File.Exists(localLoc)) {
			buffer = File.ReadAllBytes(localLoc);
			response.StatusCode = 200;
		}
		else {
			buffer = Encoding.UTF8.GetBytes("<html><body>file not found</body></html>");
			response.StatusCode = 404;
		}

		response.ContentLength64 = buffer.Length;
		response.OutputStream.Write(buffer, 0, buffer.Length);
		response.OutputStream.Close();
	}

	public static PersistedConfiguration GetConfig(ICoreServerAPI api) =>
			api.ObjectCache[_configFilename] as PersistedConfiguration;

	private void PrepareConfig() {
		PersistedConfiguration config = null;
		try {
			config = serverAPI.LoadModConfig<PersistedConfiguration>(_configFilename);
		}
		catch {
			logger.Error("Failed to reload config.");
		}

		if (config == null) {
			logger.Warning("Regenerating default config as it was missing / unparsable...");
			config = new PersistedConfiguration();
			serverAPI.StoreModConfig(config, _configFilename);
		}

		logger.Notification($"Loaded config from {_configFilename}");
		CachedConfiguration = config;
	}

	internal void PersistParameterChange() => serverAPI.StoreModConfig(CachedConfiguration, _configFilename);
}