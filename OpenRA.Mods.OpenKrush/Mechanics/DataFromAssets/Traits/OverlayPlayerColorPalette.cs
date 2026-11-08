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

namespace OpenRA.Mods.OpenKrush.Mechanics.DataFromAssets.Traits;

using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Like PlayerColorPalette, but tints with the overlay blend mode (KKND2 sprites).")]
public class OverlayPlayerColorPaletteInfo : TraitInfo
{
	[PaletteReference]
	[Desc("The name of the palette to base off.")]
	public readonly string BasePalette = "";

	[PaletteDefinition(true)]
	[Desc("The prefix for the resulting player palettes.")]
	public readonly string BaseName = "player";

	[Desc("Remap these indices to player colors.")]
	public readonly int[] RemapIndex = Array.Empty<int>();

	[Desc("Remap every blue palette entry (KKND2 sprites mark the player colour in blue) instead of a fixed RemapIndex list.")]
	public readonly bool AutoRemapBlue;

	[Desc("Allow palette modifiers to change the palette.")]
	public readonly bool AllowModifiers = true;

	public override object Create(ActorInitializer init)
	{
		return new OverlayPlayerColorPalette(this);
	}
}

public class OverlayPlayerColorPalette : ILoadsPlayerPalettes
{
	private readonly OverlayPlayerColorPaletteInfo info;

	public OverlayPlayerColorPalette(OverlayPlayerColorPaletteInfo info)
	{
		this.info = info;
	}

	public void LoadPlayerPalettes(WorldRenderer wr, string playerName, Color color, bool replaceExisting)
	{
		var pal = new MutablePalette(wr.Palette(this.info.BasePalette).Palette);

		var indices = this.info.AutoRemapBlue ? BlueIndices(pal) : this.info.RemapIndex;
		foreach (var i in indices)
		{
			var bw = (((pal[i] & 0xff) + ((pal[i] >> 8) & 0xff) + ((pal[i] >> 16) & 0xff)) / 3) / 255f;
			float Mix(byte c) => bw > .5f ? 1 - (1 - 2 * (bw - .5f)) * (1 - c / 255f) : 2 * bw * (c / 255f);
			// tone the overlay down (Thor: OLED + saturated lobby colours looked garish): 60 % tint, 40 % original shade
			uint Ch(int shift, byte c) => (uint)Math.Clamp(Mix(c) * 255 * .6f + ((pal[i] >> shift) & 0xff) * .4f, 0, 255);
			pal[i] = (pal[i] & 0xff000000) | (Ch(16, color.R) << 16) | (Ch(8, color.G) << 8) | Ch(0, color.B);
		}

		wr.AddPalette(this.info.BaseName + playerName, new ImmutablePalette(pal), this.info.AllowModifiers, replaceExisting);
	}

	private static int[] BlueIndices(MutablePalette pal)
	{
		var list = new List<int>();
		for (var i = 1; i < Palette.Size; i++)
		{
			var c = pal[i];
			float r = (c >> 16) & 0xff, g = (c >> 8) & 0xff, b = c & 0xff;
			var max = Math.Max(r, Math.Max(g, b));
			var min = Math.Min(r, Math.Min(g, b));
			if (max < 24 || max - min < 0.2f * max || b != max)
				continue;

			// hue 190..260 degrees: blue
			var hue = 60 * (4 + (r - g) / (max - min));
			if (hue >= 190 && hue <= 260)
				list.Add(i);
		}

		return list.ToArray();
	}
}
