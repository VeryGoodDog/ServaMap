using System;

namespace ServaMap;

public struct Result<TGood> {
	public TGood Good { get; private set; }
	public Exception Exception { get; private set; }
	public bool IsGood { get; private set; }
	public bool IsException => !IsGood;

	public Result(TGood good) {
		Good = good;
		Exception = default;
		IsGood = true;
	}

	public Result(Exception bad) {
		Good = default;
		Exception = bad;
		IsGood = false;
	}

	public static implicit operator Result<TGood>(TGood good) => new(good);
	public static implicit operator Result<TGood>(Exception bad) => new(bad);
}