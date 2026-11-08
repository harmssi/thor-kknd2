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

using Graphics;
using JetBrains.Annotations;
using OpenRA.Traits;
using Primitives;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[TraitLocation(SystemActors.World)]
[Desc("Draws the ground of an imported KKND2 level straight from the original level file (all MAPD layers, one tile per cell).")]
public class MapdTerrainInfo : TraitInfo
{
	[Desc("Level file inside the mounted game content, e.g. levels/multi/mlti_01.lpm. Set by the imported map's rules.")]
	public readonly string? Level;

	public override object Create(ActorInitializer init)
	{
		return new MapdTerrain(this);
	}
}

public class MapdTerrain : IRenderTerrain, IWorldLoaded, INotifyActorDisposing
{
	private readonly MapdTerrainInfo info;
	private readonly List<TerrainSpriteLayer> layers = new();
	private SheetBuilder? sheetBuilder;

	public string? Level => this.info.Level;

	public MapdTerrain(MapdTerrainInfo info)
	{
		this.info = info;
	}

	void IWorldLoaded.WorldLoaded(World world, WorldRenderer wr)
	{
		if (string.IsNullOrEmpty(this.info.Level))
			return;

		Gen2Level level;
		using (var s = world.Map.Open(this.info.Level))
			level = new(s);

		this.sheetBuilder = new(SheetType.BGRA, 2048);
		var empty = this.sheetBuilder.Add(new byte[4], SpriteFrameType.Rgba32, new(1, 1));

		for (var li = 0; li < level.Layers.Length; li++)
		{
			var l = level.Layers[li];
			if (l.TilesX != world.Map.MapSize.X || l.TilesY != world.Map.MapSize.Y)
				continue; // e.g. the small 16x16 helper layer

			var cache = new Dictionary<int, Sprite>();
			var layer = new TerrainSpriteLayer(world, wr, empty, BlendMode.Alpha, false);

			for (var y = 0; y < l.TilesY; y++)
			for (var x = 0; x < l.TilesX; x++)
			{
				var tile = l.Tiles[y * l.TilesX + x];
				if (tile == 0)
					continue;

				if (!cache.TryGetValue(tile, out var sprite))
					cache[tile] = sprite = this.sheetBuilder.Add(level.TilePixels(l, tile, li != 0), SpriteFrameType.Rgba32, new(l.TileWidth, l.TileHeight));

				layer.Update(new CPos(x, y), sprite, null, 1f, 1f, true);
			}

			this.layers.Add(layer);
		}

		this.sheetBuilder.Current.ReleaseBuffer();
	}

	void IRenderTerrain.RenderTerrain(WorldRenderer wr, Viewport viewport)
	{
		foreach (var layer in this.layers)
			layer.Draw(viewport);
	}

	void INotifyActorDisposing.Disposing(Actor self)
	{
		foreach (var layer in this.layers)
			layer.Dispose();

		this.sheetBuilder?.Dispose();
	}
}
