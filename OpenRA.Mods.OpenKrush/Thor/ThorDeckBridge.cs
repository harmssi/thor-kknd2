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

using System.Collections.Concurrent;
using Common.Traits;
using Graphics;
using JetBrains.Annotations;
using Mechanics.Construction.Orders;
using Mechanics.Construction.Traits;
using Mechanics.Researching.Orders;
using Mechanics.Technicians.Orders;
using OpenRA.Traits;
using OpenRA.Widgets;
using Primitives;
using Widgets.Ingame;

/// <summary>Immutable snapshot of what the Thor's lower screen shows. Built on the game thread, read by the deck UI thread.</summary>
public sealed class DeckSnapshot
{
	public sealed record Item(string Name, string Title, int Cost, int Progress, int Count, bool Active, bool Building, bool Locked, bool Paused);

	public sealed record Queue(uint ActorId, string Category, string Producer, Item[] Items);

	public sealed record Selected(string Title, int Count, int HealthPercent);

	public sealed record Dot(float X, float Y, int Argb, bool Building);

	public int Tick;
	public bool Playing;
	public bool GameOver;
	public bool Won;
	public string Faction = "";
	public int PlayerArgb = unchecked((int)0xFFE8B04A);
	public int Cash;
	public int Resources;
	public Queue[] Queues = Array.Empty<Queue>();
	public Selected[] Selection = Array.Empty<Selected>();
	public int[] Groups = new int[10];
	public string Mode = "";
	public bool Waypoint;
	public string? Level;
	public int MapWidth, MapHeight;
	public Rectangle Bounds;
	public float CamX, CamY, CamW, CamH; // in cells
	public Dot[] Dots = Array.Empty<Dot>();
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[TraitLocation(SystemActors.World)]
[Desc("Feeds the Thor deck (lower screen) and executes its commands on the game thread.")]
public class ThorDeckBridgeInfo : TraitInfo
{
	public override object Create(ActorInitializer init)
	{
		return new ThorDeckBridge();
	}
}

public class ThorDeckBridge : ITick, IWorldLoaded, INotifyActorDisposing
{
	/// <summary>Latest snapshot (replaced atomically).</summary>
	public static volatile DeckSnapshot Current = new();

	/// <summary>Called on the game thread every tick (controller input lives in the Android app).</summary>
	public static Action<World, WorldRenderer>? OnTick;

	/// <summary>World renderer of the running game (null in menus), for direct camera scrolling.</summary>
	public static WorldRenderer? Renderer;

	/// <summary>Set by the host: hides OpenRA's own sidebar because the deck replaces it.</summary>
	public static bool HideSidebar;

	/// <summary>Queued orders from the waypoint toggle get Shift.</summary>
	public static bool WaypointMode;

	private static readonly ConcurrentQueue<Action<World, WorldRenderer>> Commands = new();

	private WorldRenderer? wr;

	public static void Post(Action<World, WorldRenderer> command)
	{
		Commands.Enqueue(command);
	}

	/// <summary>Build icons (ARGB, from the owner's game sprites), filled lazily on the render thread.</summary>
	public static readonly ConcurrentDictionary<string, (int Width, int Height, int[] Argb)> Icons = new();

	private static readonly ConcurrentQueue<string> IconRequests = new();
	private static readonly HashSet<string> IconRequested = new();

	public static void RequestIcon(string actor)
	{
		lock (IconRequested)
			if (IconRequested.Add(actor))
				IconRequests.Enqueue(actor);
	}

	private static void BuildIcons(WorldRenderer wr)
	{
		var sheets = new Dictionary<Sheet, byte[]>();
		for (var n = 0; n < 6 && IconRequests.TryDequeue(out var name); n++)
		{
			try
			{
				var world = wr.World;
				if (!world.Map.Rules.Actors.TryGetValue(name, out var info))
					continue;

				var rs = info.TraitInfoOrDefault<Common.Traits.Render.RenderSpritesInfo>();
				var image = rs?.Image ?? name;
				var seqs = world.Map.Rules.Sequences;
				var seq = new[] { "icon", "idle", "make0", "stand" }.FirstOrDefault(s => seqs.HasSequence(image, s));
				if (seq == null)
				{
					Log.Write("debug", $"Thor deck icon {name}: no sequence for image {image}");
					continue;
				}

				var sprite = seqs.GetSequence(image, seq).GetSprite(0, WAngle.FromFacing(96));
				if (sprite == null || sprite.Bounds.Width == 0)
					continue;

				if (!sheets.TryGetValue(sprite.Sheet, out var data))
				{
					data = (byte[])sprite.Sheet.GetData().Clone();
					sprite.Sheet.ReleaseBuffer();
					sheets[sprite.Sheet] = data;
				}

				IPalette? pal = null;
				if (sprite.Sheet.Type == SheetType.Indexed)
				{
					var palName = rs?.Palette ?? (rs?.PlayerPalette ?? "player") + (world.LocalPlayer?.InternalName ?? "");
					pal = wr.Palette(palName)?.Palette;
					if (pal == null)
					{
						Log.Write("debug", $"Thor deck icon {name}: no palette {palName}");
						continue;
					}
				}

				var b = sprite.Bounds;
				var stride = sprite.Sheet.Size.Width * 4;

				// some GLES drivers hand back RGBA although BGRA was requested: pick the plane that holds the sprite
				var ch = (int)sprite.Channel;
				if (pal != null && ch < 3)
				{
					int Count(int c)
					{
						var n = 0;
						for (var y = 0; y < b.Height; y++)
						for (var x = 0; x < b.Width; x++)
							if (data[(b.Y + y) * stride + (b.X + x) * 4 + c] != 0)
								n++;
						return n;
					}

					if (Count(ch) == 0 && Count(2 - ch) > 0)
						ch = 2 - ch;
				}

				var argb = new int[b.Width * b.Height];
				for (var y = 0; y < b.Height; y++)
				for (var x = 0; x < b.Width; x++)
				{
					var o = (b.Y + y) * stride + (b.X + x) * 4;
					argb[y * b.Width + x] = pal != null
						? pal.GetColor(data[o + ch]).ToArgb()
						: (data[o + 3] << 24) | (data[o + 2] << 16) | (data[o + 1] << 8) | data[o];
				}

				Icons[name] = (b.Width, b.Height, argb);
			}
			catch (Exception e)
			{
				Log.Write("debug", $"Thor deck icon {name}: {e.Message}");
			}
		}
	}

	/// <summary>Runs queued deck commands. Call from unsynced code once per frame (like UI input), never from a trait tick.</summary>
	public static void Pump()
	{
		var wr = Renderer;
		if (wr != null && !IconRequests.IsEmpty)
			BuildIcons(wr);

		while (Commands.TryDequeue(out var command))
		{
			if (wr == null || wr.World.IsGameOver && wr.World.Disposing)
				continue;

			try
			{
				Sync.RunUnsynced(wr.World, () => command(wr.World, wr));
			}
			catch (Exception e)
			{
				Log.Write("debug", $"Thor deck command failed: {e}");
			}
		}
	}

	void IWorldLoaded.WorldLoaded(World w, WorldRenderer worldRenderer)
	{
		this.wr = worldRenderer;
		if (w.Type == WorldType.Regular)
			Renderer = worldRenderer;
	}

	void INotifyActorDisposing.Disposing(Actor self)
	{
		Current = new();
		Commands.Clear();
		Icons.Clear();
		lock (IconRequested)
			IconRequested.Clear();
		if (Renderer == this.wr)
			Renderer = null;
	}

	void ITick.Tick(Actor self)
	{
		var world = self.World;
		if (this.wr == null || world.Type != WorldType.Regular)
			return;

		OnTick?.Invoke(world, this.wr);

		if (HideSidebar && Ui.Root.GetOrNull(SidebarWidget.Identifier) is { Visible: true } sidebar)
			sidebar.Visible = false;

		if (world.WorldTick % 3 == 0)
			Current = this.Snapshot(world, this.wr);
	}

	private DeckSnapshot Snapshot(World world, WorldRenderer worldRenderer)
	{
		var player = world.LocalPlayer;
		var map = world.Map;
		var s = new DeckSnapshot
		{
			Tick = world.WorldTick,
			MapWidth = map.MapSize.X,
			MapHeight = map.MapSize.Y,
			Bounds = map.Bounds,
			Level = world.WorldActor.TraitOrDefault<MapdTerrain>()?.Level,
			Mode = world.OrderGenerator?.GetType().Name ?? "",
			Waypoint = WaypointMode,
		};

		// camera rectangle in cells
		var tl = worldRenderer.Viewport.ViewToWorldPx(int2.Zero);
		var br = worldRenderer.Viewport.ViewToWorldPx(new int2(Game.Renderer.Resolution.Width, Game.Renderer.Resolution.Height));
		var ts = map.Grid.TileSize;
		s.CamX = tl.X / (float)ts.Width;
		s.CamY = tl.Y / (float)ts.Height;
		s.CamW = (br.X - tl.X) / (float)ts.Width;
		s.CamH = (br.Y - tl.Y) / (float)ts.Height;

		if (player != null)
		{
			s.Playing = player.WinState == WinState.Undefined;
			s.GameOver = player.WinState != WinState.Undefined;
			s.Won = player.WinState == WinState.Won;
			s.Faction = player.Faction.InternalName;
			s.PlayerArgb = player.Color.ToArgb();
			var res = player.PlayerActor.Trait<PlayerResources>();
			s.Cash = res.Cash;
			s.Resources = res.Resources;

			s.Queues = world.ActorsWithTrait<ProductionQueue>()
				.Where(t => t.Actor.Owner == player && !t.Actor.IsDead && t.Trait.Enabled)
				.Select(t => QueueOf(world, t.Actor, t.Trait))
				.Where(q => q.Items.Any(i => !i.Locked))
				.OrderBy(q => q.Category).ThenBy(q => q.ActorId)
				.ToArray();

			for (var g = 0; g < 10; g++)
				s.Groups[g] = world.ControlGroups.GetActorsInControlGroup(g).Count();
		}

		s.Selection = world.Selection.Actors
			.Where(a => !a.IsDead && a.IsInWorld)
			.GroupBy(a => a.Info.Name)
			.Select(g =>
			{
				var first = g.First();
				var hp = g.Select(a => a.TraitOrDefault<IHealth>()).Where(h => h != null).ToList();
				var pct = hp.Count == 0 ? 100 : (int)hp.Average(h => h!.MaxHP == 0 ? 100 : h.HP * 100L / h.MaxHP);
				return new DeckSnapshot.Selected(first.Info.TraitInfoOrDefault<TooltipInfo>()?.Name ?? first.Info.Name, g.Count(), pct);
			})
			.OrderByDescending(x => x.Count)
			.ToArray();

		// minimap dots: visible units/buildings
		var dots = new List<DeckSnapshot.Dot>();
		foreach (var a in world.Actors)
		{
			if (!a.IsInWorld || a.IsDead || a.Owner.NonCombatant || !a.Info.HasTraitInfo<ISelectableInfo>())
				continue;

			if (player != null && !a.CanBeViewedByPlayer(player))
				continue;

			var c = map.CellContaining(a.CenterPosition);
			dots.Add(new(c.X + 0.5f, c.Y + 0.5f, a.Owner.Color.ToArgb(), a.Info.HasTraitInfo<BuildingInfo>()));
		}

		s.Dots = dots.ToArray();
		return s;
	}

	private static DeckSnapshot.Queue QueueOf(World world, Actor actor, ProductionQueue queue)
	{
		var self = queue is SelfConstructingProductionQueue;
		var buildable = queue.BuildableItems().Select(i => i.Name).ToHashSet();
		var faction = actor.Owner.Faction.InternalName + "_";
		var items = queue.AllItems().Where(i => i.TraitInfoOrDefault<BuildableInfo>() != null && (buildable.Contains(i.Name) || i.Name.StartsWith(faction, StringComparison.Ordinal))).Select(info =>
		{
			// SelfConstructingProductionQueue.AllQueued() fabricates (and ticks) fake items: never call it from a trait tick
			var queued = self ? Array.Empty<ProductionItem>() : queue.AllQueued().Where(q => q.Item == info.Name).ToArray();
			var first = queued.FirstOrDefault();
			var progress = self || first == null || first.TotalTime == 0 ? -1 : (first.TotalTime - first.RemainingTime) * 100 / first.TotalTime;
			var active = self
				? world.OrderGenerator is PlaceSpecificBuildingOrderGenerator p && p.Name == info.Name
				: queued.Length > 0;
			return new DeckSnapshot.Item(
				info.Name,
				info.TraitInfoOrDefault<TooltipInfo>()?.Name ?? info.Name,
				info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0,
				progress,
				first?.Infinite == true ? -1 : queued.Length,
				active,
				info.HasTraitInfo<BuildingInfo>(),
				!buildable.Contains(info.Name),
				first?.Paused == true);
		}).OrderBy(i => i.Locked).ToArray();

		var producer = actor.Info.TraitInfoOrDefault<TooltipInfo>()?.Name ?? actor.Info.Name;
		return new(actor.ActorID, queue.Info.Type, producer, items);
	}

	// ---- commands (called from the deck; executed on the game thread) ----

	public static void Produce(uint actorId, string item, bool cancel) => Produce(actorId, item, cancel ? 2 : 0);

	/// <summary>mode 0 = build (or resume if paused), 1 = hold (pause; cancel if already paused), 2 = cancel one.</summary>
	public static void Produce(uint actorId, string item, int mode)
	{
		Post((world, wr) =>
		{
			var actor = world.GetActorById(actorId);
			var queue = actor?.TraitsImplementing<ProductionQueue>().FirstOrDefault(q => q.BuildableItems().Any(i => i.Name == item));
			if (actor == null || queue == null)
				return;

			var current = queue is SelfConstructingProductionQueue ? null : queue.AllQueued().FirstOrDefault(q => q.Item == item);
			if (mode == 1 && current != null && !current.Paused)
				world.IssueOrder(Order.PauseProduction(actor, item, true));
			else if (mode != 0)
				world.IssueOrder(Order.CancelProduction(actor, item, 1));
			else if (current is { Paused: true })
				world.IssueOrder(Order.PauseProduction(actor, item, false));
			else if (world.Map.Rules.Actors[item].HasTraitInfo<BuildingInfo>())
			{
				if (world.OrderGenerator is PlaceSpecificBuildingOrderGenerator p && p.Name == item)
					world.CancelInputMode();
				else
					world.OrderGenerator = new PlaceSpecificBuildingOrderGenerator(queue, item, wr);
			}
			else
				world.IssueOrder(Order.StartProduction(actor, item, 1));
		});
	}

	public static void JumpTo(float cellX, float cellY)
	{
		Post((world, wr) => wr.Viewport.Center(world.Map.CenterOfCell(new CPos((int)cellX, (int)cellY))));
	}

	public static void Stop()
	{
		Post((world, _) =>
		{
			foreach (var a in world.Selection.Actors.Where(a => a.Owner == world.LocalPlayer && !a.IsDead))
				world.IssueOrder(new Order("Stop", a, false));
		});
	}

	public static void Deploy()
	{
		Post((world, _) =>
		{
			foreach (var a in world.Selection.Actors.Where(a => a.Owner == world.LocalPlayer && !a.IsDead))
				foreach (var d in a.TraitsImplementing<IIssueDeployOrder>())
					if (d.CanIssueDeployOrder(a, false))
					{
						world.IssueOrder(d.IssueDeployOrder(a, false));
						break;
					}
		});
	}

	public static void SetMode(string mode)
	{
		Post((world, _) =>
		{
			var current = world.OrderGenerator?.GetType().Name;
			OpenRA.Orders.IOrderGenerator? next = mode switch
			{
				"attack" => new AttackMoveOrderGenerator(world.Selection.Actors.Where(a => a.Owner == world.LocalPlayer), Game.Settings.Game.MouseButtonPreference.Action),
				"sell" => new Mechanics.Construction.Orders.SellOrderGenerator(),
				"repair" => new TechnicianEnterOrderGenerator(),
				"research" => new ResearchOrderGenerator(),
				_ => null,
			};

			if (next == null || current == next.GetType().Name)
				world.CancelInputMode();
			else
				world.OrderGenerator = next;
		});
	}

	public static void Group(int group, bool create)
	{
		Post((world, wr) =>
		{
			if (create)
				world.ControlGroups.CreateControlGroup(group);
			else
			{
				var before = world.Selection.Actors.ToHashSet();
				world.ControlGroups.SelectControlGroup(group);
				if (before.SetEquals(world.Selection.Actors) && before.Count > 0)
					wr.Viewport.Center(world.Selection.Actors); // second tap jumps to the group
			}
		});
	}

	public static void Save()
	{
		Post((world, _) =>
		{
			if (world.Type == WorldType.Regular && !world.IsReplay && world.LobbyInfo.GlobalSettings.GameSavesEnabled)
				world.RequestGameSave($"thor-{DateTime.Now:yyyyMMdd-HHmmss}.orasav");
		});
	}
}
