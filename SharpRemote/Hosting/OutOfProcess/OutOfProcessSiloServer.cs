using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using log4net;
using SharpRemote.CodeGeneration;

// ReSharper disable CheckNamespace
namespace SharpRemote.Hosting
// ReSharper restore CheckNamespace
{
	/// <summary>
	///     Used in conjunction with a <see cref="OutOfProcessSilo" /> to host objects in a remote process.
	/// </summary>
	/// <example>
	///     public static void main(string[] arguments)
	///     {
	///        try
	///        {
	///           // Put any additional/required initialization here.
	///           using (var silo = new OutOfProcessSiloServer(arguments))
	///           {
	///              // This is the place to register any additional interfaces with this silo
	///              // silo.CreateServant(id, (IMyCustomInterface)new MyCustomImplementation());
	///              silo.Run();
	///           }
	///        }
	///        catch(Exception e)
	///        {
	///           // This will marshall the exception back to the parent process so you can
	///           // actually know and programmatically react to the failure.
	///           OutOfProcessSiloServer.ReportException(e);
	///        }
	///     }
	/// </example>
	public sealed class OutOfProcessSiloServer
		: IRemotingEndPoint
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private readonly ITypeResolver _customTypeResolver;
		private readonly ISocketEndPoint _endPoint;

		internal ISocketEndPoint EndPoint => _endPoint;

		private readonly Process _parentProcess;
		private readonly int? _parentProcessId;
		private readonly PostMortemSettings _postMortemSettings;
		private readonly DefaultImplementationRegistry _registry;
		private readonly ManualResetEvent _waitHandle;

		/// <summary>
		///     Initializes a new silo server.
		/// </summary>
		/// <param name="args">The command line arguments given to the Main() method</param>
		/// <param name="customTypeResolver">The type resolver, if any, responsible for resolving Type objects by their assembly qualified name</param>
		/// <param name="codeGenerator">The code generator to create proxy and servant types</param>
		/// <param name="postMortemSettings">
		///     Settings to control how and if minidumps are collected - when set to null, default values are used (
		///     <see
		///         cref="PostMortemSettings" />
		///     )
		/// </param>
		/// <param name="heartbeatSettings">The settings for heartbeat mechanism, if none are specified, then default settings are used</param>
		/// <param name="latencySettings">The settings for latency measurements, if none are specified, then default settings are used</param>
		/// <param name="endPointSettings">The settings for the endpoint itself (max. number of concurrent calls, etc...)</param>
		/// <param name="endPointName">The name of this silo, used for debugging (and logging)</param>
		public OutOfProcessSiloServer(string[] args,
		                              ITypeResolver customTypeResolver = null,
		                              ICodeGenerator codeGenerator = null,
		                              PostMortemSettings postMortemSettings = null,
		                              HeartbeatSettings heartbeatSettings = null,
		                              LatencySettings latencySettings = null,
		                              EndPointSettings endPointSettings = null,
		                              string endPointName = null)
		{
			if (postMortemSettings != null && !postMortemSettings.IsValid)
			{
				throw new ArgumentException("postMortemSettings");
			}

			Log.InfoFormat("Silo Server starting, args ({0}): \"{1}\", {2} custom type resolver",
			               args.Length,
			               string.Join(" ", args),
			               customTypeResolver != null ? "with" : "without"
				);

			int pid;
			if (args.Length >= 1 && int.TryParse(args[0], out pid))
			{
				_parentProcessId = pid;
				_parentProcess = Process.GetProcessById(pid);
				_parentProcess.EnableRaisingEvents = true;
				_parentProcess.Exited += ParentProcessOnExited;
			}

			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("Args.Length: {0}", args.Length);
			}

			if (args.Length >= 10)
			{
				if (postMortemSettings != null)
				{
					Log.Info("Ignoring post-mortem settings specified from the command-line");
				}
				else
				{
					var settings = new PostMortemSettings();
					bool.TryParse(args[1], out settings.CollectMinidumps);
					bool.TryParse(args[2], out settings.SuppressErrorWindows);
					bool.TryParse(args[3], out settings.HandleAccessViolations);
					bool.TryParse(args[4], out settings.HandleCrtAsserts);
					bool.TryParse(args[5], out settings.HandleCrtPureVirtualFunctionCalls);
					int tmp;
					int.TryParse(args[6], out tmp);
					settings.RuntimeVersions = (CRuntimeVersions) tmp;
					int.TryParse(args[7], out settings.NumMinidumpsRetained);
					settings.MinidumpFolder = args[8];
					settings.MinidumpName = args[9];

					if (!settings.IsValid)
					{
						Log.ErrorFormat("Received invalid post-mortem debugger settings: {0}", settings);
					}
					else
					{
						postMortemSettings = settings;
					}
				}
			}

			_registry = new DefaultImplementationRegistry();
			_waitHandle = new ManualResetEvent(false);
			_customTypeResolver = customTypeResolver;

			_postMortemSettings = postMortemSettings;
			if (_postMortemSettings != null)
			{
				Log.InfoFormat("Using post-mortem debugger: {0}", _postMortemSettings);

				if (!NativeMethods.LoadPostmortemDebugger())
				{
					int err = Marshal.GetLastWin32Error();
					Log.ErrorFormat("Unable to load the post-mortem debugger dll: {0}",
									err);
				}

				if (_postMortemSettings.CollectMinidumps)
				{
					if (NativeMethods.InitDumpCollection(_postMortemSettings.NumMinidumpsRetained,
					                        _postMortemSettings.MinidumpFolder,
					                        _postMortemSettings.MinidumpName))
					{
						Log.InfoFormat("Installed post-mortem debugger; up to {0} mini dumps will automatically be saved to: {1}",
									   _postMortemSettings.NumMinidumpsRetained,
									   _postMortemSettings.MinidumpFolder
							);
					}
				}

				NativeMethods.InstallPostmortemDebugger(_postMortemSettings.HandleAccessViolations,
				                                        _postMortemSettings.SuppressErrorWindows,
				                                        _postMortemSettings.HandleCrtAsserts,
				                                        _postMortemSettings.HandleCrtPureVirtualFunctionCalls,
				                                        _postMortemSettings.RuntimeVersions);
			}

			_endPoint = new SocketEndPoint(EndPointType.Server,
				endPointName,
				codeGenerator: codeGenerator,
				heartbeatSettings: heartbeatSettings,
				latencySettings: latencySettings,
				endPointSettings: endPointSettings
				);

			_endPoint.OnConnected += EndPointOnOnConnected;
			_endPoint.OnDisconnected += EndPointOnOnDisconnected;
			_endPoint.OnFailure += EndPointOnOnFailure;
		}

		private void EndPointOnOnFailure(EndPointDisconnectReason endPointDisconnectReason, ConnectionId id)
		{
			OnFailure?.Invoke(endPointDisconnectReason, id);
		}

		private void EndPointOnOnDisconnected(EndPoint remoteEndPoint, ConnectionId connectionId)
		{
			OnDisconnected?.Invoke(remoteEndPoint, connectionId);
		}

		private void EndPointOnOnConnected(EndPoint remoteEndPoint, ConnectionId connectionId)
		{
			OnConnected?.Invoke(remoteEndPoint, connectionId);
		}

		/// <summary>
		/// The settings that were used to configure this server's behaviour in case of unexpected
		/// (mostly native) faults.
		/// </summary>
		public PostMortemSettings PostMortemSettings => _postMortemSettings;

		/// <summary>
		///     The process id of the parent process, as specified in the command line arguments or null
		///     when no id was specified.
		/// </summary>
		public int? ParentProcessId => _parentProcessId;

		/// <inheritdoc />
		public void Dispose()
		{
			_waitHandle.Dispose();
			_endPoint.Dispose();
		}

		/// <inheritdoc />
		public string Name => _endPoint.Name;

		/// <inheritdoc />
		public bool IsConnected => _endPoint.IsConnected;

		/// <inheritdoc />
		public long NumProxiesCollected => _endPoint.NumProxiesCollected;

		/// <inheritdoc />
		public long NumServantsCollected => _endPoint.NumServantsCollected;

		/// <inheritdoc />
		public long NumBytesSent => _endPoint.NumBytesSent;

		/// <inheritdoc />
		public long NumBytesReceived => _endPoint.NumBytesReceived;
		
		/// <inheritdoc />
		public long NumMessagesSent => _endPoint.NumMessagesSent;

		/// <inheritdoc />
		public long NumMessagesReceived => _endPoint.NumMessagesReceived;

		/// <inheritdoc />
		public long NumCallsInvoked => _endPoint.NumCallsInvoked;

		/// <inheritdoc />
		public long NumCallsAnswered => _endPoint.NumCallsAnswered;

		/// <inheritdoc />
		public long NumPendingMethodCalls => _endPoint.NumPendingMethodCalls;

		/// <inheritdoc />
		public long NumPendingMethodInvocations => _endPoint.NumPendingMethodInvocations;

		/// <inheritdoc />
		public TimeSpan? AverageRoundTripTime => _endPoint.AverageRoundTripTime;

		/// <inheritdoc />
		public TimeSpan TotalGarbageCollectionTime => _endPoint.TotalGarbageCollectionTime;

		/// <inheritdoc />
		public EndPointSettings EndPointSettings => _endPoint.EndPointSettings;

		/// <inheritdoc />
		public LatencySettings LatencySettings => _endPoint.LatencySettings;

		/// <inheritdoc />
		public HeartbeatSettings HeartbeatSettings => _endPoint.HeartbeatSettings;

		/// <inheritdoc />
		public ConnectionId CurrentConnectionId => _endPoint.CurrentConnectionId;

		/// <inheritdoc />
		public EndPoint LocalEndPoint => _endPoint.LocalEndPoint;

		/// <inheritdoc />
		public EndPoint RemoteEndPoint => _endPoint.RemoteEndPoint;

		/// <inheritdoc />
		public IEnumerable<IProxy> Proxies => _endPoint.Proxies;

		/// <summary>
		/// 
		/// </summary>
		public event Action<EndPoint, ConnectionId> OnConnected;

		/// <summary>
		/// 
		/// </summary>
		public event Action<EndPoint, ConnectionId> OnDisconnected;

		/// <summary>
		/// 
		/// </summary>
		public event Action<EndPointDisconnectReason, ConnectionId> OnFailure;

		/// <inheritdoc />
		public void Disconnect()
		{
			_endPoint.Disconnect();
		}

		/// <inheritdoc />
		public T CreateProxy<T>(ulong objectId) where T : class
		{
			return _endPoint.CreateProxy<T>(objectId);
		}

		/// <inheritdoc />
		public T GetProxy<T>(ulong objectId) where T : class
		{
			return _endPoint.GetProxy<T>(objectId);
		}

		/// <inheritdoc />
		public IServant CreateServant<T>(ulong objectId, T subject) where T : class
		{
			return _endPoint.CreateServant(objectId, subject);
		}

		/// <inheritdoc />
		public T RetrieveSubject<T>(ulong objectId) where T : class
		{
			return _endPoint.RetrieveSubject<T>(objectId);
		}

		/// <inheritdoc />
		public T GetExistingOrCreateNewProxy<T>(ulong objectId) where T : class
		{
			return _endPoint.GetExistingOrCreateNewProxy<T>(objectId);
		}

		/// <inheritdoc />
		public IServant GetExistingOrCreateNewServant<T>(T subject) where T : class
		{
			return _endPoint.GetExistingOrCreateNewServant(subject);
		}

		/// <summary>
		///     Registers a default implementation for the given interface so that
		///     <see cref="ISilo.CreateGrain{T}(object[])" /> can be used to create grains.
		/// </summary>
		/// <typeparam name="TInterface"></typeparam>
		/// <typeparam name="TImplementation"></typeparam>
		public void RegisterDefaultImplementation<TInterface, TImplementation>()
			where TImplementation : TInterface
			where TInterface : class
		{
			_registry.RegisterDefaultImplementation(typeof (TImplementation), typeof (TInterface));
		}

		/// <summary>
		///     Runs the server and blocks until a shutdown command is received because the
		///     <see cref="OutOfProcessSilo" /> is being disposed of or because the parent process
		///     quits unexpectedly.
		/// </summary>
		/// <remarks>
		/// Binds the endpoint to <see cref="IPAddress.Any"/>.
		/// </remarks>
		public void Run()
		{
			Run(IPAddress.Any);
		}

		/// <summary>
		///     Runs the server and blocks until a shutdown command is received because the
		///     <see cref="OutOfProcessSilo" /> is being disposed of or because the parent process
		///     quits unexpectedly.
		/// </summary>
		/// <param name="address">The ip-address this endpoint shall bind itself to</param>
		public void Run(IPAddress address)
		{
			Console.WriteLine(ProcessWatchdog.Constants.BootingMessage);

			try
			{
				using (_endPoint)
				using (var host = new SubjectHost(_endPoint,
				                                  _registry,
				                                  OnSubjectHostDisposed,
				                                  _customTypeResolver))
				{
					_endPoint.CreateServant(OutOfProcessSilo.Constants.SubjectHostId, (ISubjectHost) host);

					_endPoint.Bind(address);
					Console.WriteLine(_endPoint.LocalEndPoint.Port);
					Log.InfoFormat("Port sent to host process");
					Console.WriteLine(ProcessWatchdog.Constants.ReadyMessage);

					_waitHandle.WaitOne();
					Console.WriteLine(ProcessWatchdog.Constants.ShutdownMessage);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("Exception: {0}", e.Message);
			}
		}

		private void ParentProcessOnExited(object sender, EventArgs eventArgs)
		{
			Log.InfoFormat("Parent process terminated unexpectedly (exit code: {0}), shutting down...",
			               _parentProcess.ExitCode
				);

			Shutdown();
		}

		private void Shutdown()
		{
			OnSubjectHostDisposed();
		}

		private void OnSubjectHostDisposed()
		{
			Log.Info("Parent process orders shutdown...");
			_waitHandle.Set();
		}

		/// <summary>
		/// Shall be called by user code when an exception occurred during startup of the server
		/// and shall be reported back to the <see cref="OutOfProcessSilo"/>.
		/// </summary>
		/// <param name="exception"></param>
		public static void ReportException(Exception exception)
		{
			var encodedException = EncodeException(exception);
			Console.WriteLine("{0}{1}",
			                  ProcessWatchdog.Constants.ExceptionMessage,
			                  encodedException);
		}

		internal static string EncodeException(Exception exception)
		{
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream))
			{
				AbstractEndPoint.WriteException(writer, exception);

				var length = (int)stream.Length;
				var data = stream.GetBuffer();
				var encodedException = Convert.ToBase64String(data, 0, length);
				return encodedException;
			}
		}
	}
}