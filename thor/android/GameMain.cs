using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Android.Util;

namespace ThorKKND2;

/// Managed entry point, runs on the SDL thread: unpacks engine/mod data, then starts OpenRA.
public static class GameMain
{
	public const string Tag = "ThorKKND2";
	public static string DataDir = "", SupportDir = "";

	public static int Run()
	{
		try
		{
			var app = MainApplication.Instance;
			DataDir = Path.Combine(app.FilesDir!.AbsolutePath, "game");
			SupportDir = Paths.Support(app);
			Directory.CreateDirectory(SupportDir);
			UnpackData(app);
			var maps = Paths.ImportedMaps(app);
			if (!OpenRA.Mods.OpenKrush.Thor.Gen2MapImporter.IsCurrent(maps))
			{
				// importer changed with an app update: rebuild the maps from the already imported levels
				if (Directory.Exists(maps))
					Directory.Delete(maps, true);
				OpenRA.Mods.OpenKrush.Thor.Gen2MapImporter.ImportAll(Paths.Content(app), maps, m => Log.Warn(Tag, m));
			}
			MapNativeLibraries();
			Environment.SetEnvironmentVariable("OPENRA_BUNDLED_ASSEMBLIES", "1");
			OpenRA.Mods.OpenKrush.Thor.ThorDeckBridge.HideSidebar = true;
			ThorInput.Install();
			SDL2.SDL.SDL_SetHint(SDL2.SDL.SDL_HINT_TOUCH_MOUSE_EVENTS, "0"); // touch is translated in ThorInput

			var args = new List<string>
			{
				"Game.Mod=openkrush_gen2",
				"Engine.EngineDir=" + Path.Combine(DataDir, "engine"),
				"Engine.ModSearchPaths=" + Path.Combine(DataDir, "mods") + "," + Path.Combine(DataDir, "engine", "mods"),
				"Engine.SupportDir=" + SupportDir,
				"Graphics.GLProfile=Embedded",
				"Graphics.Mode=Fullscreen",
				"Graphics.UIScale=1.5",
				"Graphics.ViewportDistance=Close", // world at an integer 2x zoom (960x540 view); "Medium" gave 1.5x = uneven pixels
				"Graphics.DisableHardwareCursors=True", // Android shows no SDL cursor without a mouse; the stick needs a visible one // 6" 1080p screen: OpenRA's UI at 1280x720 logical
				// offline handheld: no telemetry, news or version checks; touch/stick instead of edge scrolling
				"Debug.SendSystemInformation=False",
				"Debug.SystemInformationVersionPrompt=4",
				"Debug.CheckVersion=False",
				"Game.FetchNews=False",
				"Game.ViewportEdgeScroll=False",
				"Game.IntroductionPromptVersion=1",
				"Game.UseClassicMouseStyle=True", // touch: tap selects AND commands (move/attack); B / two-finger tap = deselect
			};
			var extra = Path.Combine(SupportDir, "launch-args.txt"); // optional debug args, one per line
			if (File.Exists(extra))
				foreach (var line in File.ReadAllLines(extra).Where(l => l.Contains('=')))
				{
					args.RemoveAll(a => a.StartsWith(line.Substring(0, line.IndexOf('=') + 1)));
					args.Add(line.Trim());
				}

			// debug: "Thor.Sidebar=1" in launch-args.txt shows OpenKrush's own sidebar again
			if (args.Remove("Thor.Sidebar=1"))
				OpenRA.Mods.OpenKrush.Thor.ThorDeckBridge.HideSidebar = false;

			Log.Info(Tag, "starting OpenRA: " + string.Join(" ", args));
			return (int)OpenRA.Game.InitializeAndRun(args.ToArray());
		}
		catch (Exception e)
		{
			Log.Error(Tag, e.ToString());
			try { File.WriteAllText(Path.Combine(SupportDir, "thor-crash.txt"), e.ToString()); } catch { }
			return 1;
		}
	}

	static void UnpackData(Android.Content.Context ctx)
	{
		var stamp = ctx.PackageManager!.GetPackageInfo(ctx.PackageName!, 0)!.LastUpdateTime.ToString();
		var stampFile = Path.Combine(DataDir, ".stamp");
		if (File.Exists(stampFile) && File.ReadAllText(stampFile) == stamp)
			return;

		if (Directory.Exists(DataDir))
			Directory.Delete(DataDir, true);
		Directory.CreateDirectory(DataDir);
		using (var asset = ctx.Assets!.Open("data.zip"))
		using (var zip = new ZipArchive(asset))
			zip.ExtractToDirectory(DataDir);
		File.WriteAllText(stampFile, stamp);
	}

	// OpenRA's bindings use desktop library names; map them to the .so files shipped in the APK.
	static void MapNativeLibraries()
	{
		static string? Map(string name) =>
			name.Contains("SDL2", StringComparison.OrdinalIgnoreCase) ? "libSDL2.so" :
			name.Contains("oal", StringComparison.OrdinalIgnoreCase) || name.Contains("openal", StringComparison.OrdinalIgnoreCase) ? "libsoft_oal.so" :
			name.Contains("freetype", StringComparison.OrdinalIgnoreCase) ? "libfreetype6.so" :
			name.Contains("lua", StringComparison.OrdinalIgnoreCase) ? "liblua51.so" : null;

		foreach (var asm in new[] { "SDL2-CS", "OpenAL-CS.Core", "OpenRA.Platforms.Default", "Eluant", "OpenRA.Game" }
			.Select(TryLoad).OfType<Assembly>())
		{
			try
			{
				NativeLibrary.SetDllImportResolver(asm, (name, _, _) =>
					Map(name) is { } so && NativeLibrary.TryLoad(so, out var h) ? h : IntPtr.Zero);
			}
			catch (InvalidOperationException) { } // resolver already set
		}
	}

	static Assembly? TryLoad(string name)
	{
		try { return Assembly.Load(new AssemblyName(name)); } catch { return null; }
	}
}
