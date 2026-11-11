using Android.Graphics;
using OpenRA.Mods.OpenKrush.Assets;

namespace ThorKKND2.Deck;

/// The game's own interface art of one faction, read at runtime from the player's imported files
/// (levels/surv.bpk, mute.bpk, robo.bpk): the 32x32 sidebar button plates with their symbols and the bitmap font.
/// Nothing of this is shipped with the app. If a file is missing the deck draws plain plates instead.
public sealed class Art
{
	// frames of the button sheet (third image set of the pack); the same order in all three packs
	public const int Blank = 0, Infantry = 1, Vehicle = 2, Building = 5, Tower = 6, Cycle = 10, Map = 11;

	public readonly Bitmap[] Buttons;
	public readonly (Bitmap? Bmp, int W, int X, int Y)[] Glyphs; // index = character code - 32
	public Bitmap? Logo;

	Art(Bitmap[] buttons, (Bitmap?, int, int, int)[] glyphs)
	{
		Buttons = buttons;
		Glyphs = glyphs;
	}

	public static Art? Load(string levelsDir, string faction)
	{
		try
		{
			var file = System.IO.Path.Combine(levelsDir, faction switch { "evolved" => "mute.bpk", "series9" => "robo.bpk", _ => "surv.bpk" });
			if (!File.Exists(file))
				return null;

			byte[] d;
			using (var raw = File.OpenRead(file))
			using (var data = (MemoryStream)Decompressor.Decompress(raw))
				d = data.ToArray()[8..]; // "DATA" + length, then the container; all offsets count from here

			int I(int o) => BitConverter.ToInt32(d, o);

			// container: type list ("BLIT", offset of its file list), file list = offsets of the images sets
			var types = I(0);
			if (System.Text.Encoding.ASCII.GetString(d, types, 4) != "BLIT")
				return null;
			var list = I(types + 4);

			(Bitmap? Bmp, int W, int X, int Y)[] Set(int index, int max)
			{
				var o = I(list + index * 4);
				var count = Math.Min(I(o), max);
				var pal = new int[256];
				for (var i = 1; i < 256; i++) // colour 0 is see-through
				{
					var c = BitConverter.ToUInt16(d, I(o + 8) + i * 2);
					pal[i] = unchecked((int)0xFF000000) | ((c & 0x7c00) << 9) | ((c & 0x03e0) << 6) | ((c & 0x1f) << 3);
				}

				var frames = new (Bitmap?, int, int, int)[count];
				for (var f = 0; f < count; f++)
				{
					var fo = I(o + 16 + f * 4);
					int w = I(fo), h = I(fo + 4);
					if (w <= 1 || h <= 0 || w > 64 || h > 64)
					{
						frames[f] = (null, 3, 0, 0);
						continue;
					}

					var px = new int[w * h];
					for (var i = 0; i < px.Length; i++)
						px[i] = pal[d[fo + 16 + i]];
					frames[f] = (Bitmap.CreateBitmap(px, w, h, Bitmap.Config.Argb8888!), w, I(fo + 8), I(fo + 12));
				}

				return frames;
			}

			var font = Set(0, 224); // ASCII and Latin-1, then the three faction emblems at 104..106
			var art = new Art(Set(2, 12).Select(f => f.Bmp!).ToArray(), font);
			art.Logo = font[faction switch { "evolved" => 105, "series9" => 106, _ => 104 }].Bmp;
			return art.Buttons.Length > Map && art.Buttons.All(b => b != null) ? art : null;
		}
		catch (Exception e)
		{
			Android.Util.Log.Warn(GameMain.Tag, "deck art: " + e.Message);
			return null;
		}
	}
}
