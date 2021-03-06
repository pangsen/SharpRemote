﻿using System;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SharpRemote.CodeGeneration.Remoting;
using SharpRemote.Test.Types.Interfaces.PrimitiveTypes;

namespace SharpRemote.Test.CodeGeneration.Remoting
{
	[TestFixture]
	[Description(
		"Verifies that proxy and subject can communicate to each other- arguments, return values and exceptions are serialized already"
		)]
	public sealed class CreatorTest
	{
		private IEndPointChannel _channel;
		private RemotingProxyCreator _proxyCreator;
		private ServantCreator _servantCreator;
		private IServant _servant;
		private Random _random;
		private IRemotingEndPoint _endPoint;

		private T CreateServantAndProxy<T>(T subject)
		{
			var objectId = (ulong) _random.Next();
			_servant = _servantCreator.CreateServant(_endPoint, _channel, objectId, subject);
			return _proxyCreator.CreateProxy<T>(_endPoint, _channel, objectId);
		}

		[OneTimeSetUp]
		public void TestFixtureSetUp()
		{
			var seed = (int) DateTime.Now.Ticks;
			_random = new Random(seed);

			var endPoint = new Mock<IRemotingEndPoint>();
			_endPoint = endPoint.Object;

			var channel = new Mock<IEndPointChannel>();
			_channel = channel.Object;

			_proxyCreator = new RemotingProxyCreator();
			_servantCreator = new ServantCreator();

			channel.Setup(
				x => x.CallRemoteMethod(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MemoryStream>()))
			       .Returns((ulong objectId, string interfaceName, string methodName, Stream arguments) =>
				       {
					       if (objectId != _servant.ObjectId)
						       throw new NoSuchServantException(objectId);

					       BinaryReader reader = arguments != null ? new BinaryReader(arguments) : null;
					       var ret = new MemoryStream();
					       var writer = new BinaryWriter(ret);

					       _servant.Invoke(methodName, reader, writer);
					       ret.Position = 0;
					       return ret;
				       });
		}

		[Test]
		public void TestGetProperty()
		{
			var subject = new Mock<IGetDoubleProperty>();
			subject.Setup(x => x.Value).Returns(-414442.3213);

			IGetDoubleProperty proxy = CreateServantAndProxy(subject.Object);
			proxy.Value.Should().BeApproximately(subject.Object.Value, 0);
		}
	}
}