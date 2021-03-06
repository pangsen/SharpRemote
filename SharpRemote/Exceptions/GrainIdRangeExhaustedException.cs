﻿using System;
using System.Runtime.Serialization;

// ReSharper disable CheckNamespace
namespace SharpRemote
// ReSharper restore CheckNamespace
{
	/// <summary>
	///     This exception is thrown when no more <see cref="IGrain.ObjectId" />s can be generated
	///     because the key range is exhausted.
	/// </summary>
	[Serializable]
	public class GrainIdRangeExhaustedException
		: SharpRemoteException
	{
		/// <summary>
		///     Deserialization ctor.
		/// </summary>
		/// <param name="info"></param>
		/// <param name="context"></param>
		public GrainIdRangeExhaustedException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}

		/// <summary>
		///     Initializes a new instance of this exception.
		/// </summary>
		public GrainIdRangeExhaustedException()
			: base("The range of available grain ids has been exhausted - no more can be generated")
		{
		}
	}
}