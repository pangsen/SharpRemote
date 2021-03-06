﻿using System;
using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using SharpRemote.Hosting;

namespace SharpRemote.Test.Hosting
{
	[TestFixture]
	public sealed class ProcessWatchdogTest
	{
		[Test]
		[Description("Verifies that starting a process again immediately after having been killed works")]
		public void TestStartKillStart()
		{
			using (var watchdog = new ProcessWatchdog())
			{
				watchdog.Start();
				watchdog.IsProcessRunning.Should().BeTrue();
				watchdog.HasProcessFailed.Should().BeFalse();

				var pid = watchdog.HostedProcessId.Value;
				var proc = Process.GetProcessById(pid);
				proc.Kill();

				watchdog.Start();
				watchdog.IsProcessRunning.Should().BeTrue("Because we've just started that process again");
				watchdog.HasProcessFailed.Should().BeFalse("Because we've just started that process again");
			}
		}

		[Test]
		[Description("Verifies that after the process has been killed, the watchdog no longer reports the host process as alive - nor its current port")]
		public void TestTryKill()
		{
			using (var watchdog = new ProcessWatchdog())
			{
				watchdog.Start();

				watchdog.RemotePort.Should().HaveValue();
				watchdog.IsProcessRunning.Should().BeTrue();
				watchdog.HasProcessFailed.Should().BeFalse();

				watchdog.TryKill();
				watchdog.RemotePort.Should().NotHaveValue();
				watchdog.IsProcessRunning.Should().BeFalse();
				watchdog.HasProcessFailed.Should().BeTrue();
			}
		}

		[Test]
		public void TestDispose1()
		{
			var watchdog = new ProcessWatchdog();
			new Action(watchdog.Dispose).ShouldNotThrow();
		}

		[Test]
		public void TestDispose2()
		{
			ProcessWatchdog watchdog;
			using (watchdog = new ProcessWatchdog())
			{
				watchdog.RemotePort.Should().NotHaveValue();
				watchdog.HostedProcessId.Should().NotHaveValue();

				watchdog.Start();
				watchdog.RemotePort.Should().HaveValue();
				watchdog.HostedProcessId.Should().HaveValue();
			}

			watchdog.RemotePort.Should().NotHaveValue();
			watchdog.HostedProcessId.Should().NotHaveValue();
		}
	}
}