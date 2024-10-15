using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ServaMap;

public record Teleporter {
	public Vec3i Start { get; set; }
	public Vec3i End { get; set; }
	public string Label { get; set; }
	public string Tag { get; set; }

	public Teleporter() { }

	public Teleporter(BlockEntityTeleporterBase teleporter) {
		Start = teleporter.Pos.AsVec3i;
		End = teleporter.Target().AsVec3i;
		Label = "";
		Tag = "";
	}
}