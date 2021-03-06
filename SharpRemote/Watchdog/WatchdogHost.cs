﻿using System;
using System.Net;

namespace SharpRemote.Watchdog
{
	/// <summary>
	///     Responsible for hosting a <see cref="IInternalWatchdog" /> instance and exposing it via
	///     a <see cref="ISocketEndPoint" />.
	/// </summary>
	public sealed class WatchdogHost
		: IDisposable
	{
		/// <summary>
		/// 
		/// </summary>
		public const string PeerName = "SharpRemote.Watchdog";

		/// <summary>
		/// 
		/// </summary>
		public const ulong ObjectId = 0;

		private readonly ISocketEndPoint _endPoint;
		private readonly InternalWatchdog _watchdog;

		/// <summary>
		/// Initializes this object.
		/// </summary>
		public WatchdogHost()
		{
			_watchdog = new InternalWatchdog();

			_endPoint = new SocketEndPoint(EndPointType.Server, PeerName);
			_endPoint.CreateServant(ObjectId, (IInternalWatchdog) _watchdog);
			_endPoint.Bind(IPAddress.Any);
		}

		/// <summary>
		/// 
		/// </summary>
		public EndPoint LocalEndPoint => _endPoint.LocalEndPoint;

		/// <inheritdoc />
		public void Dispose()
		{
			_watchdog.Dispose();
			_endPoint.Dispose();
		}
	}
}