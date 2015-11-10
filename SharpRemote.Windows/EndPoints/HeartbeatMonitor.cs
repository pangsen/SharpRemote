﻿using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpRemote.Diagnostics;
using log4net;

// ReSharper disable CheckNamespace

namespace SharpRemote
// ReSharper restore CheckNamespace
{
	/// <summary>
	///     Responsible for invoking the heartbeat interface regularly.
	///     Notifies in case of skipped beats.
	/// </summary>
	internal sealed class HeartbeatMonitor
		: IDisposable
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly IDebugger _debugger;
		private readonly bool _enabledWithAttachedDebugger;
		private readonly TimeSpan _failureInterval;
		private readonly IHeartbeat _heartbeat;
		private readonly TimeSpan _interval;
		private readonly object _syncRoot;
		private readonly Task _task;
		private bool _failureDetected;
		private volatile bool _isDisposed;
		private bool _isStarted;
		private DateTime? _lastHeartbeat;
		private long _numHeartbeats;

		/// <summary>
		///     Initializes this heartbeat monitor with the given heartbeat interface and
		///     settings that define how often a heartbeat measurement is performed.
		/// </summary>
		/// <param name="heartbeat"></param>
		/// <param name="debugger"></param>
		/// <param name="settings"></param>
		public HeartbeatMonitor(IHeartbeat heartbeat,
		                        IDebugger debugger,
		                        HeartbeatSettings settings)
			: this(
				heartbeat,
				debugger,
				settings.Interval,
				settings.SkippedHeartbeatThreshold,
				settings.ReportSkippedHeartbeatsAsFailureWithDebuggerAttached)
		{
		}

		/// <summary>
		///     Initializes this heartbeat monitor with the given heartbeat interface and
		///     settings that define how often a heartbeat measurement is performed.
		/// </summary>
		/// <param name="heartbeat"></param>
		/// <param name="debugger"></param>
		/// <param name="heartBeatInterval"></param>
		/// <param name="failureThreshold"></param>
		/// <param name="enabledWithAttachedDebugger"></param>
		public HeartbeatMonitor(IHeartbeat heartbeat,
		                        IDebugger debugger,
		                        TimeSpan heartBeatInterval,
		                        int failureThreshold,
		                        bool enabledWithAttachedDebugger)
		{
			if (heartbeat == null) throw new ArgumentNullException("heartbeat");
			if (debugger == null) throw new ArgumentNullException("debugger");
			if (heartBeatInterval < TimeSpan.Zero) throw new ArgumentOutOfRangeException("heartBeatInterval");
			if (failureThreshold < 1) throw new ArgumentOutOfRangeException("failureThreshold");

			_syncRoot = new object();
			_heartbeat = heartbeat;
			_debugger = debugger;
			_interval = heartBeatInterval;
			_enabledWithAttachedDebugger = enabledWithAttachedDebugger;
			_failureInterval = heartBeatInterval +
			                   TimeSpan.FromMilliseconds(failureThreshold*heartBeatInterval.TotalMilliseconds);
			_task = new Task(MeasureHeartbeats, TaskCreationOptions.LongRunning);
		}

		/// <summary>
		///     The configured heartbeat interval, e.g. the amount of time that shall pass before
		///     a new heartbeat is started.
		/// </summary>
		public TimeSpan Interval
		{
			get { return _interval; }
		}

		/// <summary>
		///     The amount of time for which a heartbeat may not return (e.g. fail) before the connection is assumed
		///     to be dead.
		/// </summary>
		public TimeSpan FailureInterval
		{
			get { return _failureInterval; }
		}

		/// <summary>
		///     The total number of heartbeats performed since <see cref="Start" />.
		/// </summary>
		public long NumHeartbeats
		{
			get
			{
				lock (_syncRoot)
				{
					return _numHeartbeats;
				}
			}
		}

		/// <summary>
		///     The point in time where the last heartbeat was performed.
		/// </summary>
		public DateTime? LastHeartbeat
		{
			get { return _lastHeartbeat; }
		}

		/// <summary>
		///     Whether or not this heartbeat is disposed of.
		/// </summary>
		public bool IsDisposed
		{
			get { return _isDisposed; }
		}

		/// <summary>
		///     Whether or not <see cref="Start()" /> has been called (and <see cref="Stop()" /> has not since then).
		/// </summary>
		public bool IsStarted
		{
			get { return _isStarted; }
		}

		/// <summary>
		///     Whether or not a failure has been detected.
		/// </summary>
		public bool FailureDetected
		{
			get { return _failureDetected; }
		}

		public void Dispose()
		{
			lock (_syncRoot)
			{
				_isStarted = false;
				_isDisposed = true;
			}
		}

		/// <summary>
		///     Starts this heartbeat monitor.
		/// </summary>
		/// <remarks>
		///     Resets the <see cref="FailureDetected" /> property to false.
		/// </remarks>
		public void Start()
		{
			lock (_syncRoot)
			{
				_failureDetected = false;
				_isStarted = true;
			}
			_task.Start();
		}

		/// <summary>
		///     Stops the heartbeat monitor, failures will no longer be reported, nor
		///     will the proxy be accessed in any way.
		/// </summary>
		public void Stop()
		{
			lock (_syncRoot)
			{
				_isStarted = false;
			}
		}

		private void MeasureHeartbeats()
		{
			while (_isStarted)
			{
				try
				{
					DateTime started = DateTime.Now;
					if (!PerformHeartbeat())
						break;

					_lastHeartbeat = DateTime.Now;

					lock (_syncRoot)
					{
						if (_isDisposed)
							break;

						++_numHeartbeats;
					}

					TimeSpan elapsed = DateTime.Now - started;
					TimeSpan remainingSleep = _interval - elapsed;
					if (remainingSleep > TimeSpan.Zero)
						Thread.Sleep(remainingSleep);
				}
				catch (Exception e)
				{
					Log.ErrorFormat("Caught unexpected exception: {0}", e);
				}
			}
		}

		private bool PerformHeartbeat()
		{
			Task task;
			try
			{
				task = _heartbeat.Beat();
			}
			catch (NotConnectedException)
			{
				return false;
			}
			catch (ConnectionLostException)
			{
				return false;
			}

			if (!WaitForHeartbeat(task))
			{
				ReportFailure();
				return false;
			}

			return true;
		}

		/// <summary>
		///     Performs a single heartbeat.
		/// </summary>
		/// <param name="task"></param>
		/// <returns>True when the heartbeat succeeded, false otherwise</returns>
		private bool WaitForHeartbeat(Task task)
		{
			if (task == null)
			{
				return false;
			}

			try
			{
				if (!task.Wait(_failureInterval) &&
				    (!_debugger.IsDebuggerAttached || _enabledWithAttachedDebugger))
				{
					return false;
				}
			}
			catch (AggregateException)
			{
				return false;
			}

			if (task.IsFaulted)
			{
				return false;
			}
			return true;
		}

		private void ReportFailure()
		{
			lock (_syncRoot)
			{
				if (_isDisposed)
					return;

				if (!_isStarted)
					return;
			}

			_failureDetected = true;
			Action fn = OnFailure;
			if (fn != null)
				fn();
		}

		/// <summary>
		///     This event is fired when and if this monitor detects a failure of the heartbeat
		///     interface because too many heartbeats passed
		/// </summary>
		public event Action OnFailure;
	}
}