using System;

using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ServaMap;

public static class Helpers {
	public static BlockPos Target(this BlockEntityTeleporterBase tp) => tp.GetTarget(null).AsBlockPos;

	/// <summary>
	///   Max of 3 ints
	/// </summary>
	public static int Max(int i0, int i1, int i2) =>
			i0 > i1 ? i0 > i2 ? i0 : i2 :
			i1 > i2 ? i1 : i2;

	public static int Max(int i, int? j) {
		var x = j ?? i;
		return i > x ? i : x;
	}

	public static int Min(int i, int? j) {
		var x = j ?? i;
		return i < x ? i : x;
	}

	public static IChatCommand WithSub(this IChatCommand command, string name,
			Action<IChatCommand> sub) {
		var c = command.BeginSub(name);
		sub(c);
		return c.EndSub();
	}
}