using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ServaMap;

public record Trader {
	public long EntityId { get; set; }
	public Vec3i Pos { get; set; }
	public string Name { get; set; }
	public string Wares { get; set; }

	public Trader() { }

	public Trader(EntityTrader trader) {
		EntityId = trader.EntityId;
		Pos = trader.Pos.XYZInt;
		Name = trader.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name");
		Wares = trader.Code.EndVariant();
	}
}