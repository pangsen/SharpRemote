﻿using System;
using System.Collections.Generic;
using SharpRemote.CodeGeneration;
using SharpRemote.Extensions;

namespace SharpRemote.Hosting
{
	/// <summary>
	/// <see cref="ISubjectHost"/> implementation that uses an <see cref="Activator"/> to
	/// create object instances.
	/// </summary>
	internal sealed class SubjectHost
		: ISubjectHost
	{
		private readonly ITypeResolver _customTypeResolver;
		private readonly IRemotingEndPoint _endpoint;
		private readonly Dictionary<ulong, IServant> _subjects;
		private readonly object _syncRoot;
		private ulong _nextServantId;
		private readonly Action _onDisposed;
		private bool _isDisposed;

		private Type GetType(string assemblyQualifiedName)
		{
			if (_customTypeResolver != null)
				return _customTypeResolver.GetType(assemblyQualifiedName);

			return TypeResolver.GetType(assemblyQualifiedName);
		}

		public SubjectHost(IRemotingEndPoint endpoint,
			ulong firstServantId,
			Action onDisposed = null,
			ITypeResolver customTypeResolver = null)
		{
			if (endpoint == null) throw new ArgumentNullException("endpoint");

			_customTypeResolver = customTypeResolver;
			_endpoint = endpoint;
			_nextServantId = firstServantId;
			_onDisposed = onDisposed;
			_syncRoot = new object();
			_subjects = new Dictionary<ulong, IServant>();
		}

		public ulong CreateSubject1(Type type, Type interfaceType)
		{
			var subject = Activator.CreateInstance(type);

			lock (_syncRoot)
			{
				var servantId = _nextServantId++;
				var method = typeof(IRemotingEndPoint).GetMethod("CreateServant").MakeGenericMethod(interfaceType);
				var servant = (IServant)method.Invoke(_endpoint, new[] { servantId, subject });
				_subjects.Add(servantId, servant);

				return servantId;
			}
		}

		public ulong CreateSubject2(string assemblyQualifiedTypeName, Type interfaceType)
		{
			var type = GetType(assemblyQualifiedTypeName);
			return CreateSubject1(type, interfaceType);
		}

		public void Dispose()
		{
			lock (_syncRoot)
			{
				if (_isDisposed)
					return;

				// TODO: Remove / dispose all subjects...
				foreach (var subject in _subjects.Values)
				{
					var disp = subject.Subject as IDisposable;
					if (disp != null)
						disp.TryDispose();
				}
				_subjects.Clear();

				if (_onDisposed != null)
					_onDisposed();

				_isDisposed = true;
			}
		}
	}
}