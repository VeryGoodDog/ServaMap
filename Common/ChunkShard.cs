using System;

using ProtoBuf;

using Vintagestory.API.MathTools;

namespace ServaMap;

[ProtoContract]
public record ChunkShard {
	public ChunkShard() { }

	public ChunkShard(Vec2i ChunkCoords, int[] ShardImage, DateTime GenerationTime) {
		this.ChunkCoords = ChunkCoords;
		this.ShardImage = ShardImage;
		this.GenerationTime = GenerationTime;
	}

	[ProtoMember(1)]
	public Vec2i ChunkCoords { get; set; }

	[ProtoMember(2)]
	public int[] ShardImage { get; set; }

	[ProtoIgnore]
	public int[,] SquareShardImage {
		get {
			var size = (int)Math.Sqrt(ShardImage.Length);
			var normal = new int[size, size];
			for (var index = 0; index < ShardImage.Length; index++) {
				var row = index / size;
				var col = index % size;
				normal[row, col] = ShardImage[index];
			}
			return normal;
		}
	}

	[ProtoMember(3)]
	public DateTime GenerationTime { get; set; }

	// This is ignored because it's set by the server not the client.
	[ProtoIgnore]
	public string GeneratingPlayerId { get; set; }

	[ProtoIgnore]
	public bool IsInvalid => ChunkCoords is null || ShardImage is null;
}