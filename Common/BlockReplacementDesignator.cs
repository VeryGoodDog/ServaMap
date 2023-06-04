using System.Drawing;

using Vintagestory.API.Common;

namespace ServaMap;

/// <summary>
///   A designator that represents a color replacement, eg for paths.
/// </summary>
public record BlockReplacementDesignator {
	public BlockReplacementDesignator() { }

	public BlockReplacementDesignator(AssetLocation Pattern, Color OverwriteColor) {
		this.Pattern = Pattern;
		this.OverwriteColor = OverwriteColor;
	}

	public AssetLocation Pattern { get; set; }
	public Color OverwriteColor { get; set; }
}