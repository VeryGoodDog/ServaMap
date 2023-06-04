using Vintagestory.API.MathTools;

namespace ServaMap;

public record Landmark {
	public Vec3i Pos { get; set; }
	public string Label { get; set; }
	public string Type { get; set; }
	public string CreatorId { get; set; }
}