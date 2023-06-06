using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ServaMap.Server;

public abstract class DatabaseHandlerModSystem<T> : ModSystem {
	protected PersistedConfiguration config;

	protected SQLiteConnection conn;
	protected string dBFullPath;
	protected string jsonFilePath;
	protected ILogger logger;
	protected ICoreServerAPI serverAPI;

	public abstract string TableName { get; }

	public override bool ShouldLoad(EnumAppSide forSide) => forSide.IsServer();

	public override double ExecuteOrder() => 0.3;

	public override void StartServerSide(ICoreServerAPI api) {
		serverAPI = api;
		logger = serverAPI.Logger;

		config = ServaMapServerMod.GetConfig(api);
		dBFullPath = config.GetDBFullPath(serverAPI);
		var jsonFilename = Path.ChangeExtension(TableName, "geojson");
		jsonFilePath = Path.Combine(config.GetServerMapFullPath(serverAPI), jsonFilename);
	}

	/// <summary>
	///   Opens the database, or creates it if needed.
	/// </summary>
	public void InitializeDatabase() {
		if (!File.Exists(dBFullPath))
			SQLiteConnection.CreateFile(dBFullPath);
		var connBuilder = new SQLiteConnectionStringBuilder();
		connBuilder.DataSource = dBFullPath;
		conn = new SQLiteConnection(connBuilder.ToString());
		conn.Open();
	}

	public abstract Result<bool> ProcessFeature(T toProcess);

	public abstract Result<bool> ShouldUpdate(T toTest);

	public abstract Result<bool> Update(T toUpdate);

	public abstract Result<bool> Delete(T toDelete);

	public virtual Exception Clear() {
		try {
			using var command = conn.CreateCommand();
			command.CommandText = @$"
DELETE FROM {TableName}
";
			command.ExecuteNonQuery();
			return null;
		}
		catch (Exception e) {
			logger.Error("Serv-a-Map failed to clear a database.");
			logger.Error(e.ToString());
			return e;
		}
	}

	protected void WriteFeatures(Action<SQLiteDataReader> middle) {
		using var command = conn.CreateCommand();
		command.CommandText = @$"
SELECT * FROM {TableName}
";
		var reader = command.ExecuteReader();
		while (reader.Read())
			middle(reader);
	}
}