using Vintagestory.API.MathTools;

namespace ServaMap;

public record Teleporter {
	public BlockPos Start { get; set; }
	public BlockPos End { get; set; }
	public string Label { get; set; }
	public string Tag { get; set; }
}