﻿using NUnit.Framework;

namespace SharpRemote.Test.CodeGeneration.Serialization.Json
{
	[TestFixture]
	[Ignore("Not yet implemented")]
	public sealed class JsonSerializerAcceptanceTest
		: AbstractSerializerAcceptanceTest
	{
		protected override ISerializer2 Create()
		{
			return new JsonSerializer();
		}
	}
}