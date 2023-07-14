using System;
using System.Data.SQLite;
using System.IO;

using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ServaMap.Server;

/// <summary>
/// A mod system that strictly interacts with a database.
/// </summary>
/// <typeparam name="T">The type of the database entries.</typeparam>
public abstract class DatabaseHandlerModSystem<T> : ModSystem {
	protected PersistedConfiguration config;

	protected SQLiteConnection conn;
	protected ILogger logger;
	protected ICoreServerAPI serverAPI;

	public abstract string TableName { get; }

	public override bool ShouldLoad(EnumAppSide forSide) => forSide.IsServer();

	public override double ExecuteOrder() => 0.3;

	public override void StartServerSide(ICoreServerAPI api) {
		serverAPI = api;
		logger = serverAPI.Logger;

		config = ServaMapServerMod.GetConfig(api);

		conn = serverAPI.ModLoader.GetModSystem<ServaMapServerMod>().dbConnection;

		InitializeDatabase();
	}

	public abstract void InitializeDatabase();

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
}