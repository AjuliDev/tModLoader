using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Xna.Framework.Audio;
using Terraria.Audio;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader.Engine;

[SupportedOSPlatform("windows")]
internal static class AudioDeviceMonitor
{
	private static volatile bool _deviceChangePending;
	private static NotificationClient _client;
	private static IMMDeviceEnumerator _enumerator;
	public static void SignalDeviceChange() => _deviceChangePending = true;
	public static void Initialize()
	{
		if (!OperatingSystem.IsWindows())
			return;

		try {
			var clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
			_enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
				Type.GetTypeFromCLSID(clsid));
			_client = new NotificationClient();
			_enumerator.RegisterEndpointNotificationCallback(_client);
			Logging.tML.Info("AudioDeviceMonitor: watching for device changes");
		}
		catch (Exception e) {
			Logging.tML.Warn("AudioDeviceMonitor: failed to initialize: " + e.Message);
		}
	}

	// Called once per frame from the Update hook
	public static void Update()
	{
		if (!_deviceChangePending)
			return;
		_deviceChangePending = false;
		Reinitialize();
	}

	private static void Reinitialize()
	{
		Logging.tML.Info("AudioDeviceMonitor: reinitializing audio after device change");

		if (Main.audioSystem is not LegacyAudioSystem legacy)
			return;

		try {
			// Stop everything first so FAudio isn't mid-operation
			SoundEngine.StopAmbientSounds();
			SoundEngine.StopTrackedSounds();

			// Dispose and recreate the AudioEngine the same way LegacyAudioSystem does
			var contentManager = (TMLContentManager)Main.instance.Content;
			legacy.Engine.Dispose();
			legacy.Engine = new AudioEngine(contentManager.GetPath("TerrariaMusic.xgs"));
			legacy.SoundBank = new SoundBank(legacy.Engine, contentManager.GetPath("Sound Bank.xsb"));
			legacy.Engine.Update();

			Logging.tML.Info("AudioDeviceMonitor: audio reinitialized OK");
		}
		catch (Exception e) {
			Logging.tML.Warn("AudioDeviceMonitor: reinit failed, muting: " + e.Message);
			// Fall back to disabled audio system rather than crashing
			Main.audioSystem = new DisabledAudioSystem();
		}
	}

	// Inner class implements the COM callback
	private class NotificationClient : IMMNotificationClient
	{
		public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, string _)
		{
			if (flow == EDataFlow.eRender)
				_deviceChangePending = true;
		}
		public void OnDeviceAdded(string _) => _deviceChangePending = true;
		public void OnDeviceRemoved(string _) => _deviceChangePending = true;
		public void OnDeviceStateChanged(string deviceId, int state) { }
		public void OnPropertyValueChanged(string deviceId, PropertyKey key) { }
	}

	// COM interop
	public enum EDataFlow { eRender, eCapture, eAll }
	public enum ERole { eConsole, eMultimedia, eCommunications }
	[StructLayout(LayoutKind.Sequential)]
	public struct PropertyKey { public Guid fmtid; public uint pid; }

	[ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	interface IMMNotificationClient
	{
		void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int state);
		void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
		void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
		void OnDefaultDeviceChanged(EDataFlow flow, ERole role,
			[MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId);
		void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId,
			PropertyKey key);
	}

	[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	interface IMMDeviceEnumerator
	{
		void EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
		void GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr endpoint);
		void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr device);
		void RegisterEndpointNotificationCallback(IMMNotificationClient client);
		void UnregisterEndpointNotificationCallback(IMMNotificationClient client);
	}
}