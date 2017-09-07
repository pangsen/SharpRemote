﻿// ReSharper disable CheckNamespace

using System;
using System.Net;
using SharpRemote.Exceptions;

namespace SharpRemote
// ReSharper restore CheckNamespace
{
	/// <summary>
	///     The interface for an endpoint that connects to a <see cref="ISocketRemotingEndPointServer"/>.
	///     Once the connection is established, proxies and servants can communicate with each other.
	/// </summary>
	public interface ISocketRemotingEndPointClient
		: ISocketRemotingEndPoint
	{
		/// <summary>
		///     Tries to connect to another endPoint with the given name.
		/// </summary>
		/// <param name="endPointName"></param>
		/// <returns>True when a connection could be established, false otherwise</returns>
		bool TryConnect(string endPointName);

		/// <summary>
		///     Tries to connects to another endPoint with the given name.
		/// </summary>
		/// <param name="endPointName"></param>
		/// <param name="timeout"></param>
		/// <returns>True when the connection succeeded, false otherwise</returns>
		/// <exception cref="ArgumentNullException">When <paramref name="endPointName"/> is null</exception>
		/// <exception cref="ArgumentException">When <paramref name="endPointName"/> is empty</exception>
		/// <exception cref="ArgumentOutOfRangeException">When <paramref name="timeout"/> is less or equal to <see cref="TimeSpan.Zero"/></exception>
		/// <exception cref="InvalidOperationException">When no network service discoverer was specified when creating this client</exception>
		bool TryConnect(string endPointName, TimeSpan timeout);

		/// <summary>
		///     Tries to connects to another endPoint with the given name.
		/// </summary>
		/// <param name="endPoint"></param>
		/// <returns>True when the connection succeeded, false otherwise</returns>
		bool TryConnect(IPEndPoint endPoint);

		/// <summary>
		///     Tries to connect this endPoint to the given one.
		/// </summary>
		/// <param name="endPoint"></param>
		/// <param name="timeout">The amount of time this method should block and await a successful connection from the remote end-point</param>
		/// <exception cref="ArgumentNullException">
		///     When <paramref name="endPoint" /> is null
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///     When <paramref name="timeout" /> is equal or less than <see cref="TimeSpan.Zero" />
		/// </exception>
		/// <exception cref="InvalidOperationException">
		///     When this endPoint is already connected to another endPoint.
		/// </exception>
		bool TryConnect(IPEndPoint endPoint, TimeSpan timeout);

		/// <summary>
		///     Tries to connect this endPoint to the given one.
		/// </summary>
		/// <param name="endPoint"></param>
		/// <param name="timeout">The amount of time this method should block and await a successful connection from the remote end-point</param>
		/// <param name="connectionId"></param>
		/// <param name="exception"></param>
		/// <exception cref="ArgumentNullException">
		///     When <paramref name="endPoint" /> is null
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///     When <paramref name="timeout" /> is equal or less than <see cref="TimeSpan.Zero" />
		/// </exception>
		/// <exception cref="InvalidOperationException">
		///     When this endPoint is already connected to another endPoint.
		/// </exception>
		bool TryConnect(IPEndPoint endPoint, TimeSpan timeout,
		                       out Exception exception,
		                       out ConnectionId connectionId);

		/// <summary>
		///     Connects to another endPoint with the given name.
		/// </summary>
		/// <param name="endPointName"></param>
		/// <param name="timeout"></param>
		/// <exception cref="ArgumentException">
		///     In case <paramref name="endPointName" /> is null
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///     When <paramref name="timeout" /> is equal or less than <see cref="TimeSpan.Zero" />
		/// </exception>
		/// <exception cref="InvalidOperationException">
		///     When this endPoint is already connected to another endPoint.
		/// </exception>
		/// <exception cref="NoSuchIPEndPointException">When no such endPoint could be *found* - it might exist but this one is incapable of establishing a successfuly connection</exception>
		/// <exception cref="AuthenticationException">
		///     - The given endPoint is no <see cref="SocketRemotingEndPointServer" />
		///     - The given endPoint failed authentication
		/// </exception>
		ConnectionId Connect(string endPointName, TimeSpan timeout);

		/// <summary>
		///     Connects this endPoint to the given one.
		/// </summary>
		/// <param name="endPoint"></param>
		/// ///
		/// <exception cref="ArgumentNullException">
		///     When <paramref name="endPoint" /> is null
		/// </exception>
		/// <exception cref="InvalidOperationException">
		///     When this endPoint is already connected to another endPoint.
		/// </exception>
		/// <exception cref="NoSuchIPEndPointException">When no such endPoint could be *found* - it might exist but this one is incapable of establishing a successfuly connection</exception>
		/// <exception cref="AuthenticationException">
		///     - The given endPoint is no <see cref="SocketRemotingEndPointServer" />
		///     - The given endPoint failed authentication
		/// </exception>
		ConnectionId Connect(IPEndPoint endPoint);

		/// <summary>
		///     Connects this endPoint to the given one.
		/// </summary>
		/// <param name="endPoint"></param>
		/// <param name="timeout">The amount of time this method should block and await a successful connection from the remote end-point</param>
		/// <exception cref="ArgumentNullException">
		///     When <paramref name="endPoint" /> is null
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///     When <paramref name="timeout" /> is equal or less than <see cref="TimeSpan.Zero" />
		/// </exception>
		/// <exception cref="InvalidOperationException">
		///     When this endPoint is already connected to another endPoint.
		/// </exception>
		/// <exception cref="NoSuchIPEndPointException">When no such endPoint could be *found* - it might exist but this one is incapable of establishing a successfuly connection</exception>
		/// <exception cref="AuthenticationException">
		///     - The given endPoint is no <see cref="SocketRemotingEndPointServer" />
		///     - The given endPoint failed authentication
		/// </exception>
		/// <exception cref="AuthenticationRequiredException">
		///     - The given endPoint requires authentication, but this one didn't provide any
		/// </exception>
		/// <exception cref="HandshakeException">
		///     - The handshake between this and the given endpoint failed
		/// </exception>
		ConnectionId Connect(IPEndPoint endPoint, TimeSpan timeout);
	}
}