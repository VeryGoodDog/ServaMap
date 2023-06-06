using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace ServaMap;

public record Trader {
	public long EntityId { get; set; }
	public EntityPos Pos { get; set; }
	public string Name { get; set; }
	public string Wares { get; set; }

	public Trader() { }

	public Trader(EntityTrader trader) {
		EntityId = trader.EntityId;
		Pos = trader.Pos;
		Name = trader.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name");
		Wares = trader.Code.EndVariant();
	}
}