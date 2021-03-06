﻿using System;
using log4net;

namespace SharpRemote.Extensions
{
	internal static class DisposableExtensions
	{
		private static readonly ILog Log = LogManager.GetLogger("SharpRemote.DisposableExtensions");

		public static void TryDispose(this IDisposable that)
		{
			if (that == null)
				return;

			try
			{
				that.Dispose();
			}
			catch (Exception e)
			{
				Log.WarnFormat("Caught exception while disposing '{0}': {1}", that, e);
			}
		}
	}
}