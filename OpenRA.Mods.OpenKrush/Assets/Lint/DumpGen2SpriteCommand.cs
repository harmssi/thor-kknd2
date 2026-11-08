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
using Graphics;
using JetBrains.Annotations;
using OpenRA.FileFormats;
using Primitives;

/// <summary>Debug: writes the frames of a MOBD (from the mounted game files) as PNGs with the sprite's own palette.</summary>
[UsedImplicitly]
public class DumpGen2SpriteCommand : IUtilityCommand
{
	string IUtilityCommand.Name => "--dump-gen2-sprite";

	bool IUtilityCommand.ValidateArguments(string[] args) => args.Length >= 3;

	[Desc("PACKAGE|FILE.mobd OUTDIR [FRAME...]", "Dump MOBD frames as PNG (raw size) and print frame sizes and render flags.")]
	void IUtilityCommand.Run(Utility utility, string[] args)
	{
		var modData = Game.ModData = utility.ModData;
		if (args[1] == "--actor")
		{
			var ai = modData.DefaultRules.Actors[args[2]];
			var rs = ai.TraitInfo<Common.Traits.Render.RenderSpritesInfo>();
			Console.WriteLine($"{args[2]}: Image={rs.Image} Palette={rs.Palette} PlayerPalette={rs.PlayerPalette}");
			return;
		}

		var name = args[1].Contains("|") ? args[1] : args[1].Replace("@", "|"); // "@" avoids shell pipes
		var s = modData.DefaultFileSystem.Open(name);
		var seg = (SegmentStream)s;
		seg.BaseStream.Position = seg.BaseOffset;
		var mobd = new Mobd(seg.BaseStream);
		var frames = mobd.RotationalAnimations.SelectMany(a => a.Frames).Concat(mobd.SimpleAnimations.SelectMany(a => a.Frames)).ToArray();
		var only = args.Skip(3).Select(int.Parse).ToHashSet();
		Directory.CreateDirectory(args[2]);
		uint[]? pal = frames.Select(f => f.RenderFlags.Palette).FirstOrDefault(p => p != null);
		var info = new Mechanics.DataFromAssets.Traits.PaletteFromMobdInfo();
		typeof(Mechanics.DataFromAssets.Traits.PaletteFromMobdInfo).GetField("Filename")!.SetValue(info, name);
		var read = ((OpenRA.Traits.IProvidesCursorPaletteInfo)info).ReadPalette(modData.DefaultFileSystem);
		var diff = Enumerable.Range(1, 255).Count(k => pal != null && read[k] != pal[k]);
		Console.WriteLine($"PaletteFromMobd vs frame palette: {diff} entries differ; sample 5: {read[5]:x8} vs {pal?[5]:x8}");
		var pals = frames.Select(f => f.RenderFlags.Palette).Where(p => p != null).Select(p => string.Join(",", p!.Take(256))).Distinct().Count();
		Console.WriteLine($"palette hash {(pal == null ? 0 : string.Join(",", pal).GetHashCode()):x8} distinct-per-frame {pals}");
		for (var i = 0; i < frames.Length; i++)
		{
			var img = frames[i].RenderFlags.Image;
			Console.WriteLine($"{i}: {img.Width}x{img.Height} offset {frames[i].OffsetX},{frames[i].OffsetY} flags 0x{img.Flags:x8}");
			if (only.Count > 0 && !only.Contains(i))
				continue;

			var p = frames[i].RenderFlags.Palette ?? pal ?? new uint[256];
			var colors = p.Select(c => Color.FromArgb((int)c)).ToArray();
			colors[0] = Color.FromArgb(0);
			new Png(img.Pixels, SpriteFrameType.Indexed8, img.Width, img.Height, colors).Save(Path.Combine(args[2], $"{Path.GetFileNameWithoutExtension(args[1].Split('|').Last())}_{i}.png"));
		}
	}
}
