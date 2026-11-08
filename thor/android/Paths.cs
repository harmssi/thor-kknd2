using Android.Content;

namespace ThorKKND2;

/// Where game content and saves live: the app's external files dir with the most free space (SD card on the Thor).
public static class Paths
{
	public static string Support(Context ctx)
	{
		var prefs = ctx.GetSharedPreferences("thor", FileCreationMode.Private)!;
		var chosen = prefs.GetString("support", null);
		if (chosen != null && Directory.Exists(chosen))
			return chosen;

		var best = ctx.GetExternalFilesDirs(null)!.Where(d => d != null)
			.OrderByDescending(d => d!.FreeSpace).First()!.AbsolutePath;
		var dir = Path.Combine(best, "support");
		Directory.CreateDirectory(dir);
		prefs.Edit()!.PutString("support", dir)!.Apply();
		return dir;
	}

	public static string Content(Context ctx) => Path.Combine(Support(ctx), "Content", "openkrush_gen2");
	public static string ImportedMaps(Context ctx) => Path.Combine(Support(ctx), "maps", "openkrush_gen2", "imported");
	// maps are (re)built from the imported levels by GameMain when missing or outdated
	public static bool HasContent(Context ctx) => File.Exists(Path.Combine(Content(ctx), "levels", "sprite.lpk"));
}
