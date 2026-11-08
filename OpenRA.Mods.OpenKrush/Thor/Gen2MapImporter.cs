#region Copyright & License Information

/*
 * Copyright 2007-2026 The OpenKrush Developers (see AUTHORS)
 * This file is part of OpenKrush, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

namespace OpenRA.Mods.OpenKrush.Thor;

using System.Text;
using Graphics;
using OpenRA.FileFormats;

/// <summary>
/// Turns the owner's original KKND2 multiplayer levels into OpenRA map folders (no ModData needed, runs before the game starts).
/// The ground stays in the original file (MapdTerrain); the map stores terrain types, start points and oil patches.
/// </summary>
public static class Gen2MapImporter
{
	public const int Version = 3;

	// CPLC placement types (verified on mlti_01: 8 = player start with 0xAAAA000n, 16 = oil patch)
	private const int PlacementStart = 8;
	private const int PlacementOil = 16;

	/// <summary>Imports levels/multi/mlti_*.lpm from contentDir into outDir/&lt;id&gt;. Returns the number of maps written.</summary>
	public static int ImportAll(string contentDir, string outDir, Action<string>? log = null)
	{
		var multi = Path.Combine(contentDir, "levels", "multi");
		if (!Directory.Exists(multi))
			return 0;

		Directory.CreateDirectory(outDir);
		var count = 0;
		foreach (var file in Directory.GetFiles(multi, "*.lpm").OrderBy(f => f))
		{
			try
			{
				var id = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
				Import(file, Path.Combine(outDir, id), $"levels/multi/{Path.GetFileName(file)}", id, count == 0);
				count++;
			}
			catch (Exception e)
			{
				log?.Invoke($"{file}: {e.Message}");
			}
		}

		File.WriteAllText(outDir.TrimEnd(Path.DirectorySeparatorChar) + ".version", Version.ToString());
		return count;
	}

	/// <summary>True when outDir holds maps written by this importer version.</summary>
	public static bool IsCurrent(string outDir)
	{
		var v = outDir.TrimEnd(Path.DirectorySeparatorChar) + ".version";
		return File.Exists(v) && File.ReadAllText(v).Trim() == Version.ToString();
	}

	public static void Import(string levelFile, string mapDir, string levelPath, string id, bool shellmap = false)
	{
		Gen2Level level;
		using (var s = File.OpenRead(levelFile))
			level = new(s);

		var ground = level.Layers[0];
		int w = ground.TilesX, h = ground.TilesY;
		Directory.CreateDirectory(mapDir);

		// map.bin: tiles (u16 template, u8 index), no heights, no resources; column-major
		using (var bin = new BinaryWriter(File.Create(Path.Combine(mapDir, "map.bin"))))
		{
			bin.Write((byte)2);
			bin.Write((ushort)w);
			bin.Write((ushort)h);
			bin.Write(17u);
			bin.Write(0u);
			bin.Write((uint)(3 * w * h + 17));
			for (var x = 0; x < w; x++)
			for (var y = 0; y < h; y++)
			{
				var border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
				bin.Write(border ? (ushort)4 : Gen2Level.TemplateFor(ground.Attributes[y * w + x]));
				bin.Write((byte)0);
			}

			for (var i = 0; i < w * h; i++)
				bin.Write((ushort)0);
		}

		var starts = level.Placements.Where(p => p.Type == PlacementStart && (p.Extra & 0xffff0000) == 0xaaaa0000)
			.OrderBy(p => p.Extra & 0xff).ToList();
		var oil = level.Placements.Where(p => p.Type == PlacementOil).ToList();

		var number = int.TryParse(id.Replace("mlti_", ""), out var n) ? n : 0;
		var title = number > 0 ? $"KKND2 Multiplayer {number:00}" : id;

		var y2 = new StringBuilder();
		y2.Append($"MapFormat: 11\n\nRequiresMod: openkrush_gen2\n\nTitle: {title}\n\nAuthor: Beam Software (imported)\n\nTileset: kknd2\n\n");
		y2.Append($"MapSize: {w},{h}\n\nBounds: 1,1,{w - 2},{h - 2}\n\nVisibility: {(shellmap ? "Lobby, Shellmap" : "Lobby")}\n\nCategories: KKND2\n\nPlayers:\n");
		y2.Append("\tPlayerReference@Neutral:\n\t\tName: Neutral\n\t\tOwnsWorld: True\n\t\tNonCombatant: True\n\t\tFaction: Random\n");
		var multis = string.Join(", ", Enumerable.Range(0, starts.Count).Select(i => $"Multi{i}"));
		y2.Append($"\tPlayerReference@Creeps:\n\t\tName: Creeps\n\t\tNonCombatant: True\n\t\tFaction: Random\n\t\tEnemies: {multis}\n");
		for (var i = 0; i < starts.Count; i++)
			y2.Append($"\tPlayerReference@Multi{i}:\n\t\tName: Multi{i}\n\t\tPlayable: True\n\t\tFaction: Random\n\t\tEnemies: Creeps\n");

		y2.Append("\nActors:\n");
		var a = 0;
		foreach (var s in starts)
			y2.Append($"\tActor{a++}: mpspawn\n\t\tOwner: Neutral\n\t\tLocation: {s.X / 32},{s.Y / 32}\n");
		foreach (var o in oil)
			y2.Append($"\tActor{a++}: oilpatch\n\t\tOwner: Neutral\n\t\tLocation: {o.X / 32},{o.Y / 32}\n");

		y2.Append($"\nRules:\n\tWorld:\n\t\tMapdTerrain:\n\t\t\tLevel: {levelPath}\n");
		File.WriteAllText(Path.Combine(mapDir, "map.yaml"), y2.ToString());

		WritePreview(level, Path.Combine(mapDir, "map.png"));
	}

	/// <summary>
	/// OpenRA's map preview (lobby, map chooser) expects exactly one pixel per cell of the map bounds; spawn markers are placed
	/// with that assumption. Each pixel is the average colour of the cell's ground tile (both MAPD layers).
	/// </summary>
	private static void WritePreview(Gen2Level level, string png)
	{
		var ground = level.Layers[0];
		int w = ground.TilesX, h = ground.TilesY, bw = w - 2, bh = h - 2;
		var overlay = level.Layers.Length > 1 && level.Layers[1].TilesX == w && level.Layers[1].TilesY == h ? level.Layers[1] : null;
		var rgba = new byte[bw * bh * 4];
		var cache = new Dictionary<int, (int R, int G, int B)>();

		(int R, int G, int B) Average(Gen2Level.Layer layer, int tile, bool transparentZero)
		{
			var px = level.TilePixels(layer, tile, transparentZero);
			long r = 0, g = 0, b = 0, n = 0;
			for (var i = 0; i < px.Length; i += 4)
			{
				if (px[i + 3] == 0)
					continue;
				r += px[i];
				g += px[i + 1];
				b += px[i + 2];
				n++;
			}

			return n == 0 ? (-1, 0, 0) : ((int)(r / n), (int)(g / n), (int)(b / n));
		}

		for (var y = 0; y < bh; y++)
		for (var x = 0; x < bw; x++)
		{
			var c = (R: 0, G: 0, B: 0);
			var t = ground.Tiles[(y + 1) * w + x + 1];
			if (t != 0)
			{
				if (!cache.TryGetValue(t, out c))
					cache[t] = c = Average(ground, t, false);
			}

			if (overlay != null && overlay.Tiles[(y + 1) * w + x + 1] is var o && o != 0)
			{
				var oc = Average(overlay, o, true);
				if (oc.R >= 0)
					c = ((c.R + oc.R * 2) / 3, (c.G + oc.G * 2) / 3, (c.B + oc.B * 2) / 3);
			}

			var k = (y * bw + x) * 4;
			rgba[k] = (byte)Math.Max(0, c.R);
			rgba[k + 1] = (byte)c.G;
			rgba[k + 2] = (byte)c.B;
			rgba[k + 3] = 255;
		}

		new Png(rgba, SpriteFrameType.Rgba32, bw, bh).Save(png);
	}
}
