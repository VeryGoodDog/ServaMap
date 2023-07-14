using System;

using ProtoBuf;

using SkiaSharp;

using Vintagestory.API.MathTools;

namespace ServaMap;

[ProtoContract]
public record ChunkShard {
	public ChunkShard() { }

	public ChunkShard(Vec2i ChunkCoords, int[] ShardImage) {
		this.ChunkCoords = ChunkCoords;
		this.ShardImage = ShardImage;
	}

	[ProtoMember(1)]
	public Vec2i ChunkCoords { get; set; }

	[ProtoMember(2)]
	public int[] ShardImage { get; set; }
	
	public SKBitmap ToBitmap() {
		var size = (int)Math.Sqrt(ShardImage.Length);
		if (size * size != ShardImage.Length)
			return null;
		var bitmap = new SKBitmap(size, size, true);
		bitmap.CopyToBitmap(ShardImage);
		return bitmap;
	}

	// Set by the server because there is no way to be sure the client hasn't changed their computer's time.
	[ProtoIgnore]
	public DateTime GenerationTime { get; set; }

	// This is ignored because it's set by the server not the client.
	[ProtoIgnore]
	public string GeneratingPlayerId { get; set; }
}