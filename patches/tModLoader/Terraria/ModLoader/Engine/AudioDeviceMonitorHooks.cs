using System;
using MonoMod.Cil;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader.Engine;

internal static class AudioDeviceMonitorHooks
{
	public static void Load()
	{
		if (!OperatingSystem.IsWindows())
			return;

		AudioDeviceMonitor.Initialize();

		// Hook Main.Update — same as mods like SpiritMod already do
		Terraria.On_Main.Update += (orig, self, gameTime) => {
			if (OperatingSystem.IsWindows())
				AudioDeviceMonitor.Update();
		};
	}
}