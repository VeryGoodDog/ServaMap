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
		var api = teleporter.Api;
		Start = teleporter.Pos.ToLocalPosition(api);
		End = teleporter.Target().ToLocalPosition(api);
		Label = "";
		Tag = "";
	}
}