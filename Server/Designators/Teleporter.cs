using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ServaMap;

public record Teleporter {
	public BlockPos Start { get; set; }
	public BlockPos End { get; set; }
	public string Label { get; set; }
	public string Tag { get; set; }

	public Teleporter() { }

	public Teleporter(BlockEntityTeleporterBase teleporter) {
		Start = teleporter.Pos;
		End = teleporter.Target();
		Label = "";
		Tag = "";
	}
}