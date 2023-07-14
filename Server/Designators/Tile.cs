using System.IO;

using SkiaSharp;

namespace ServaMap;

public record Tile {
	public int ScaleLevel { get; set; }
	public int X { get; set; }
	public int Y { get; set; }
	public SKBitmap Texture { get; set; }

	public Tile() { }
	
	public Tile(int scaleLevel, int x, int y) {
		ScaleLevel = scaleLevel;
		X = x;
		Y = y;
	}
	
	public string TilePath => $"{ScaleLevel}_{X}_{Y}.png";
}