using System.IO;

using SkiaSharp;

namespace ServaMap;

public record Tile {
	public int ScaleLevel { get; init; }
	public int X { get; init; }
	public int Y { get; init; }
	public SKBitmap Texture { get; set; }

	public Tile() { }

	public Tile(int scaleLevel, int x, int y) {
		ScaleLevel = scaleLevel;
		X = x;
		Y = y;
	}

	public string TilePath => $"{ScaleLevel}_{X}_{Y}.png";

	public override int GetHashCode() => GetTextureHash() ^ X ^ Y ^ ScaleLevel;

	public int GetTextureHash() {
		if (Texture == null) return 0;
		int textureHash = 0;
		var texBytes = Texture.Bytes;
		for (int i = 0; i < texBytes.Length; i += 4)
			textureHash ^= texBytes[i + 0] << 24 | texBytes[i + 1] << 16 | texBytes[i + 2] << 8 | texBytes[i + 3] << 0;
		return textureHash;
	}

	public virtual bool Equals(Tile other) =>
			other.X == X && other.Y == Y && other.ScaleLevel == ScaleLevel && other.GetTextureHash() == GetTextureHash();
}