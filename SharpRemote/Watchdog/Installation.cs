﻿namespace SharpRemote.Watchdog
{
	/// <summary>
	/// 
	/// </summary>
	public enum Installation
	{
		/// <summary>
		/// Only install the application if there's no previous installation.
		/// If there is, fail the installation.
		/// </summary>
		FailOnUpgrade,

		/// <summary>
		/// If there's a previous installation, kill all of its running processes
		/// and completely remove the installation before attempting a new install.
		/// </summary>
		CleanInstall,

		/// <summary>
		/// Performs an update of an existing application. All running application instances
		/// are stopped for the durationg of the update and restarted once the update is finished.
		/// </summary>
		/// <remarks>
		/// Files not touched by the current installation are simply from the previous
		/// installation.
		/// </remarks>
		/// <remarks>
		/// The installation will fail if the files being installed are in used.
		/// </remarks>
		/// <remarks>
		/// If the installation fails then the application is very likely in an inconsistent state
		/// (old files alongside new files) and therefore is deemed broken.
		/// In order to remedy this, a <see cref="CleanInstall"/> should be performed.
		/// </remarks>
		ColdUpdate,

		/// <summary>
		/// Simply apply the installation, even if there's an installation and don't kill
		/// its running processes.
		/// </summary>
		/// <remarks>
		/// Files not touched by the current installation are simply from the previous
		/// installation.
		/// </remarks>
		/// <remarks>
		/// The installation will fail if the files being installed are in used.
		/// </remarks>
		/// <remarks>
		/// If the installation fails then the application is very likely in an inconsistent state
		/// (old files alongside new files) and therefore is deemed broken.
		/// In order to remedy this, a <see cref="CleanInstall"/> should be performed.
		/// </remarks>
		HotUpdate,
	}
}