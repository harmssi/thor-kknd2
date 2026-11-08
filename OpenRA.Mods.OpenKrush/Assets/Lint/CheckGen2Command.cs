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

namespace OpenRA.Mods.OpenKrush.Assets.Lint;

using FileFormats;
using JetBrains.Annotations;
using OpenRA.Primitives;

[UsedImplicitly]
public class CheckGen2Command : IUtilityCommand
{
	string IUtilityCommand.Name => "--check-gen2";

	bool IUtilityCommand.ValidateArguments(string[] args) => true;

	[Desc("Load every sequence frame with the real game data and compare building palettes.")]
	void IUtilityCommand.Run(Utility utility, string[] args)
	{
		var modData = Game.ModData = utility.ModData;
		var errors = 0;
		var checkedFrames = 0;

		foreach (var (tileset, provider) in modData.DefaultSequences)
		{
			Console.WriteLine($"Tileset {tileset}");

			foreach (var image in provider.Images.OrderBy(i => i))
			{
				foreach (var name in provider.Sequences(image))
				{
					try
					{
						var seq = provider.GetSequence(image, name);

						if (seq.Length <= 0)
							throw new InvalidDataException("no frames");

						for (var frame = 0; frame < seq.Length; frame++)
						{
							if (seq.GetSprite(frame) == null)
								throw new InvalidDataException($"missing sprite for frame {frame}");

							for (var f = 0; f < Math.Max(1, Math.Abs(seq.Facings)); f++)
								if (seq.GetSprite(frame, new WAngle(f * 1024 / Math.Max(1, Math.Abs(seq.Facings)))) == null)
									throw new InvalidDataException($"missing sprite for frame {frame} facing {f}");

							checkedFrames++;
						}
					}
					catch (Exception e)
					{
						errors++;
						Console.WriteLine($"ERR {image}.{name}: {e.GetType().Name}: {e.Message}");
					}
				}
			}
		}

		Console.WriteLine($"{checkedFrames} frames checked, {errors} sequence errors.");

		foreach (var prefix in new[] { "S_B_", "M_B_", "R_B_", "S_I_", "M_I_", "R_I_" })
		{
			var hashes = new SortedDictionary<string, List<string>>();

			foreach (var file in ((OpenRA.FileSystem.FileSystem)modData.DefaultFileSystem).MountedPackages.First(p => p.Name.EndsWith("sprite.lpk", StringComparison.OrdinalIgnoreCase)).Contents.Where(f => f.StartsWith(prefix, StringComparison.Ordinal)))
			{
				try
				{
					var palInfo = new Mechanics.DataFromAssets.Traits.PaletteFromMobdInfo();
					FieldLoader.Load(palInfo, new MiniYaml("", new List<MiniYamlNode> { new("Name", "x"), new("Filename", $"sprites|{file}") }));
					var read = ((OpenRA.Traits.IProvidesCursorPaletteInfo)palInfo).ReadPalette(modData.DefaultFileSystem);
					if (read[1] == 0 && read[2] == 0 && read[3] == 0)
						Console.WriteLine($"ERR PaletteFromMobd empty for {file}");

					using var stream = modData.DefaultFileSystem.Open($"sprites|{file}");
					var seg = (SegmentStream)stream;
					seg.BaseStream.Position = seg.BaseOffset;
					var mobd = new Mobd(seg.BaseStream);
					var first = mobd.RotationalAnimations.Concat(mobd.SimpleAnimations).SelectMany(a => a.Frames).FirstOrDefault();
					var pal = first?.RenderFlags.Palette;
					var key = pal == null ? "none" : string.Join(",", pal.Take(256).Select(c => c.ToString("x")).ToArray()).GetHashCode().ToString("x");
					if (!hashes.TryGetValue(key, out var list))
						hashes[key] = list = new();
					list.Add(file);
				}
				catch (Exception e)
				{
					Console.WriteLine($"ERR palette {file}: {e.Message}");
				}
			}

			foreach (var (key, files) in hashes)
				Console.WriteLine($"palette {prefix}* {key}: {files.Count} files, e.g. {string.Join(" ", files.Take(6).ToArray())}");
		}
	}
}
