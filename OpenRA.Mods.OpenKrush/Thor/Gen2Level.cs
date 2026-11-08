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

using Assets;
using Assets.FileFormats;
using OpenRA.FileSystem;
using Primitives;

/// <summary>
/// Reader for a KKND2 level (.lpm/.lps): ground layers (MAPD, tile based), per-cell attributes and placed objects (CPLC).
/// Layer header (gen2): "SCRL", tileW, tileH, tilesX, tilesY, pixelW, pixelH, attributeOffset; then tilesX*tilesY tile offsets.
/// All offsets are relative to the start of the level data.
/// </summary>
public sealed class Gen2Level
{
	public sealed class Layer
	{
		public int TileWidth, TileHeight, TilesX, TilesY;
		public int[] Tiles = Array.Empty<int>(); // offset of the 8-bit tile pixels, 0 = empty
		public byte[] Attributes = Array.Empty<byte>();
	}

	public readonly record struct Placement(int Type, int X, int Y, int Class, int Sub, uint Extra);

	public readonly Layer[] Layers;
	public readonly byte[] Palette; // RGBA
	public readonly List<Placement> Placements = new();
	readonly byte[] data;

	public Gen2Level(Stream compressedOrRaw)
	{
		var s = Decompressor.Decompress(compressedOrRaw); // "DATA" + size + level data
		s.Position = 8;
		this.data = s.ReadBytes((int)(s.Length - 8));

		var lvl = new Lvl(new MemoryStream(this.data), "level", new EmptyFileSystem());
		var mapd = OffsetOf(lvl, "0.mapd");

		var p = mapd + 4; // gen2: leading unknown
		var layerCount = this.I32(p);
		p += 4;
		var layerOffsets = new int[layerCount];
		for (var i = 0; i < layerCount; i++, p += 4)
			layerOffsets[i] = this.I32(p);

		var colors = this.I32(p);
		p += 4;
		this.Palette = new byte[colors * 4];
		for (var i = 0; i < colors; i++, p += 2)
		{
			var c = BitConverter.ToUInt16(this.data, p);
			this.Palette[i * 4] = (byte)((c & 0x7c00) >> 7);
			this.Palette[i * 4 + 1] = (byte)((c & 0x03e0) >> 2);
			this.Palette[i * 4 + 2] = (byte)((c & 0x001f) << 3);
			this.Palette[i * 4 + 3] = 0xff;
		}

		this.Layers = new Layer[layerCount];
		for (var i = 0; i < layerCount; i++)
		{
			var o = layerOffsets[i];
			var l = new Layer { TileWidth = this.I32(o + 4), TileHeight = this.I32(o + 8), TilesX = this.I32(o + 12), TilesY = this.I32(o + 16) };
			var attributeOffset = this.I32(o + 28);
			l.Tiles = new int[l.TilesX * l.TilesY];
			for (var t = 0; t < l.Tiles.Length; t++)
				l.Tiles[t] = this.I32(o + 32 + t * 4) & ~3;

			l.Attributes = new byte[l.Tiles.Length];
			if (attributeOffset > 0 && attributeOffset + l.Tiles.Length <= this.data.Length)
				Array.Copy(this.data, attributeOffset, l.Attributes, 0, l.Tiles.Length);

			this.Layers[i] = l;
		}

		if (lvl.Contains("0.cplc"))
			this.ReadPlacements(OffsetOf(lvl, "0.cplc"), (int)lvl.GetStream("0.cplc")!.Length);
	}

	static int OffsetOf(Lvl lvl, string name)
	{
		return lvl.GetStream(name) is SegmentStream seg ? (int)seg.BaseOffset : throw new InvalidDataException($"{name} missing");
	}

	int I32(int p) => BitConverter.ToInt32(this.data, p);

	/// <summary>CPLC is a linked list of placed objects: type, x*256, y*256, 0, 4 pointers into the list, class, sub, ..., extra.</summary>
	void ReadPlacements(int start, int length)
	{
		bool InList(int v) => v >= start && v < start + length;
		for (var p = start; p + 14 * 4 <= start + length; p += 4)
		{
			int V(int k) => this.I32(p + k * 4);
			var type = V(0);
			if (type is < 0 or >= 1000 || V(3) != 0 || !InList(V(6)))
				continue;

			if (!new[] { 4, 5, 7 }.All(k => V(k) == 0 || InList(V(k))))
				continue;

			var x = V(1);
			var y = V(2);
			if (x <= 0 || y <= 0 || x >= 1 << 24 || y >= 1 << 24)
				continue;

			this.Placements.Add(new(type, x >> 8, y >> 8, V(8), V(9), (uint)V(13)));
		}
	}

	/// <summary>RGBA pixels of one tile; palette index 0 is transparent on overlay layers.</summary>
	public byte[] TilePixels(Layer layer, int offset, bool transparentZero)
	{
		var n = layer.TileWidth * layer.TileHeight;
		var rgba = new byte[n * 4];
		for (var i = 0; i < n; i++)
		{
			var index = this.data[offset + i];
			if (index == 0 && transparentZero)
				continue;

			Array.Copy(this.Palette, index * 4, rgba, i * 4, 4);
		}

		return rgba;
	}

	/// <summary>Terrain template id (see tilesets/kknd2) for a KKND2 attribute value (editor attrib.lst + observed maps).</summary>
	public static ushort TemplateFor(byte attribute)
	{
		return attribute switch
		{
			0 or 2 or 7 or 8 or (>= 19 and <= 34) => 0, // all walk
			6 => 1, // road
			4 or 5 or 35 => 2, // infantry only
			9 or 10 => 3, // water
			_ => 4, // no-go (cliffs, rocks, lava, map border)
		};
	}

	sealed class EmptyFileSystem : IReadOnlyFileSystem
	{
		public Stream Open(string filename) => throw new FileNotFoundException(filename);
		public bool TryGetPackageContaining(string path, out IReadOnlyPackage package, out string filename) { package = null!; filename = null!; return false; }
		public bool TryOpen(string filename, out Stream s) { s = null!; return false; }
		public bool Exists(string filename) => false;
		public bool IsExternalModFile(string filename) => false;
	}
}
