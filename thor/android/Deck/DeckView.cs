using Path = System.IO.Path;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using OpenRA.Mods.OpenKrush.Thor;

namespace ThorKKND2.Deck;

/// The Thor's lower screen: minimap, oil, production with queues, selection, control groups and command buttons.
/// Drawn in a 1240x1080 design space and scaled to the real display.
public class DeckView : View
{
	public static readonly string[][] Tabs = { new[] { "infantry" }, new[] { "vehicle", "beast", "aircraft" }, new[] { "building" }, new[] { "tower", "wall" } };
	static readonly string[] TabNamesEn = { "INFANTRY", "VEHICLES", "BUILDINGS", "DEFENSE" };
	static readonly string[] TabNamesDe = { "INFANTERIE", "FAHRZEUGE", "GEBÄUDE", "ABWEHR" };
	static readonly (string Id, string En, string De)[] Commands =
	{
		("deploy", "DEPLOY", "AUFBAUEN"), ("stop", "STOP", "STOPP"), ("attack", "ATTACK", "ANGRIFF"), ("move", "MOVE", "BEWEGEN"), ("waypoint", "WAYPOINT", "WEGPUNKT"),
		("sell", "SELL", "VERKAUFEN"), ("repair", "REPAIR", "REPARIEREN"), ("menu", "MENU", "MENÜ"), ("save", "SAVE", "SPEICHERN"),
	};

	public static int Tab;          // selected production tab
	public static int ScrollRow, MaxScrollRow;
	RectF productionRect = new();
	float dragStartY;
	bool dragging;
	int dragRow0;
	bool autoTabDone;
	public static int Producer;     // index among the tab's queues
	public static int Focus = -1;   // controller focus ring over cards (-1 = hidden)
	public static long FocusUntil;

	const float W = 1240, H = 1080;
	readonly Paint scratch = new(PaintFlags.AntiAlias);
	readonly Handler handler = new(Looper.MainLooper!);
	readonly List<(RectF Rect, Action Tap, Action? Long)> hits = new();
	DeckSnapshot snap = new();
	Bitmap? minimap;
	string? minimapLevel;
	string toast = "";
	long toastUntil;
	float scale = 1, offX, offY;
	readonly bool de = Java.Util.Locale.Default.Language == "de";

	readonly Java.Lang.IRunnable checkLong;
	readonly Dictionary<string, Bitmap> icons = new();
	readonly Paint iconPaint = new();

	public DeckView(Context ctx) : base(ctx)
	{
		checkLong = new Java.Lang.Runnable(CheckLong); // one instance, so RemoveCallbacks really removes it
		Ui.Init(ctx);
		handler.Post(Refresh);
	}

	void Refresh()
	{
		snap = ThorDeckBridge.Current;
		Invalidate();
		handler.PostDelayed(Refresh, 100);
	}

	protected override void OnDetachedFromWindow()
	{
		handler.RemoveCallbacksAndMessages(null);
		base.OnDetachedFromWindow();
	}

	public static DeckSnapshot.Queue[] QueuesForTab(DeckSnapshot s, int tab) => s.Queues.Where(q => Tabs[tab].Contains(q.Category)).ToArray();

	public static DeckSnapshot.Queue? CurrentQueue(DeckSnapshot s)
	{
		var qs = QueuesForTab(s, Tab);
		return qs.Length == 0 ? null : qs[Math.Clamp(Producer, 0, qs.Length - 1)];
	}

	protected override void OnDraw(Canvas c)
	{
		scale = Math.Min(Width / W, Height / H);
		offX = (Width - W * scale) / 2;
		offY = (Height - H * scale) / 2;
		c.DrawColor(new Color(Ui.Bg));
		c.Save();
		c.Translate(offX, offY);
		c.Scale(scale, scale);
		hits.Clear();

		var accent = Ui.AccentFor(snap.Faction);
		DrawHeader(c, accent);
		DrawMinimap(c, new RectF(24, 96, 444, 516), accent);
		DrawSelection(c, new RectF(24, 532, 444, 764), accent);
		DrawProduction(c, new RectF(464, 96, 1216, 764), accent);
		DrawGroups(c, new RectF(24, 780, 1216, 872), accent);
		DrawCommands(c, new RectF(24, 888, 1216, 1056), accent);

		if (snap.GameOver)
		{
			var banner = new RectF(464, 400, 1216, 520);
			Ui.PanelRect(c, banner, 14, Ui.Panel2, snap.Won ? Ui.Green : Ui.Red, scratch);
			var t = snap.Won ? (de ? "SIEG" : "VICTORY") : (de ? "NIEDERLAGE" : "DEFEAT");
			var p = Ui.Label(40, snap.Won ? Ui.Green : Ui.Red);
			c.DrawText(t, banner.CenterX() - p.MeasureText(t) / 2, banner.CenterY() + 14, p);
		}

		if (Java.Lang.JavaSystem.CurrentTimeMillis() < toastUntil)
		{
			var p = Ui.TextPaint(Ui.InterBold, 30, Ui.Text);
			var tw = p.MeasureText(toast);
			var r = new RectF(620 - tw / 2 - 30, 460, 620 + tw / 2 + 30, 530);
			Ui.PanelRect(c, r, 12, Ui.Panel2, accent, scratch);
			c.DrawText(toast, r.Left + 30, r.Bottom - 24, p);
		}

		c.Restore();
	}

	void DrawHeader(Canvas c, int accent)
	{
		c.DrawText("KKND2 THOR", 24, 40, Ui.Label(18, accent));
		var faction = snap.Faction switch { "survivors" => "SURVIVORS", "evolved" => "EVOLVED", "series9" => "SERIES 9", _ => de ? "BEREIT" : "READY" };
		c.DrawText(faction, 24, 70, Ui.Label(14, Ui.Text3));

		var oil = Ui.TextPaint(Ui.MonoBold, 44, Ui.Text);
		var value = (snap.Cash + snap.Resources).ToString("N0");
		var vw = oil.MeasureText(value);
		c.DrawText(value, 1216 - vw, 66, oil);
		var l = Ui.Label(14, accent);
		c.DrawText(de ? "ÖL" : "OIL", 1216 - vw - 24 - l.MeasureText(de ? "ÖL" : "OIL"), 62, l);

		scratch.Color = new Color(Ui.Line);
		c.DrawRect(24, 84, 1216, 85, scratch);
	}

	void DrawMinimap(Canvas c, RectF r, int accent)
	{
		Ui.PanelRect(c, r, 12, Ui.Panel, Ui.Line, scratch);
		if (snap.MapWidth == 0)
		{
			c.DrawText(de ? "KEINE KARTE" : "NO MAP", r.Left + 24, r.CenterY(), Ui.Label(14, Ui.Text3));
			return;
		}

		LoadMinimap();
		var inner = new RectF(r.Left + 8, r.Top + 8, r.Right - 8, r.Bottom - 8);
		var b = snap.Bounds;
		float sx = inner.Width() / b.Width, sy = inner.Height() / b.Height, s = Math.Min(sx, sy);
		var ox = inner.Left + (inner.Width() - b.Width * s) / 2;
		var oy = inner.Top + (inner.Height() - b.Height * s) / 2;
		RectF Cell(float x, float y, float w, float h) => new(ox + (x - b.X) * s, oy + (y - b.Y) * s, ox + (x - b.X + w) * s, oy + (y - b.Y + h) * s);

		if (minimap != null)
		{
			// .mim covers the whole map (incl. border)
			var whole = Cell(0, 0, snap.MapWidth, snap.MapHeight);
			c.Save();
			c.ClipRect(inner);
			c.DrawBitmap(minimap, null, whole, new Paint(PaintFlags.FilterBitmap));
			c.Restore();
		}

		foreach (var d in snap.Dots)
		{
			scratch.Color = new Color(d.Argb | unchecked((int)0xFF000000));
			var sz = d.Building ? 2.2f : 1.2f;
			c.DrawRect(Cell(d.X - sz / 2, d.Y - sz / 2, sz, sz), scratch);
		}

		var cam = (X: snap.CamX, Y: snap.CamY, Width: snap.CamW, Height: snap.CamH);
		scratch.SetStyle(Paint.Style.Stroke);
		scratch.StrokeWidth = 2;
		scratch.Color = new Color(Ui.Text);
		c.Save();
		c.ClipRect(inner);
		c.DrawRect(Cell(cam.X, cam.Y, cam.Width, cam.Height), scratch);
		c.Restore();
		scratch.SetStyle(Paint.Style.Fill);

		hits.Add((inner, () => { }, null)); // handled as drag in OnTouchEvent
		minimapRect = inner;
		minimapToCell = (px, py) => (b.X + (px - ox) / s, b.Y + (py - oy) / s);
	}

	RectF minimapRect = new();
	Func<float, float, (float, float)>? minimapToCell;

	void LoadMinimap()
	{
		if (snap.Level == minimapLevel)
			return;

		minimapLevel = snap.Level;
		minimap = null;
		icons.Clear(); // new game: player colours may differ
		autoTabDone = false;
		if (snap.Level == null)
			return;

		try
		{
			var mim = Path.ChangeExtension(Path.Combine(Paths.Content(Context!), snap.Level), ".mim");
			var d = File.ReadAllBytes(mim);
			int w = BitConverter.ToInt32(d, 4), h = BitConverter.ToInt32(d, 8);
			var px = new int[w * h];
			for (var i = 0; i < px.Length; i++)
			{
				var v = BitConverter.ToUInt16(d, 12 + i * 2);
				px[i] = unchecked((int)0xFF000000) | (((v & 0x7c00) >> 7) << 16) | (((v & 0x03e0) >> 2) << 8) | ((v & 0x1f) << 3);
			}

			minimap = Bitmap.CreateBitmap(px, w, h, Bitmap.Config.Argb8888!);
		}
		catch (Exception e)
		{
			Android.Util.Log.Warn(GameMain.Tag, "minimap: " + e.Message);
		}
	}

	void DrawSelection(Canvas c, RectF r, int accent)
	{
		Ui.PanelRect(c, r, 12, Ui.Panel, Ui.Line, scratch);
		c.DrawText(de ? "AUSWAHL" : "SELECTION", r.Left + 20, r.Top + 34, Ui.Label(13, Ui.Text3));
		if (snap.Selection.Length == 0)
		{
			c.DrawText(de ? "Nichts ausgewählt" : "Nothing selected", r.Left + 20, r.Top + 80, Ui.TextPaint(Ui.Inter, 22, Ui.Text3));
			return;
		}

		var y = r.Top + 76;
		foreach (var s in snap.Selection.Take(4))
		{
			c.DrawText(s.Title, r.Left + 20, y, Ui.TextPaint(Ui.InterSemi, 22, Ui.Text));
			var count = Ui.TextPaint(Ui.MonoBold, 22, accent);
			var txt = "×" + s.Count;
			c.DrawText(txt, r.Right - 20 - count.MeasureText(txt), y, count);
			Ui.Bar(c, new RectF(r.Left + 20, y + 10, r.Right - 20, y + 16), s.HealthPercent / 100f,
				s.HealthPercent > 50 ? Ui.Green : Ui.Red, s.HealthPercent > 50 ? Ui.Green : Ui.Accent, scratch);
			y += 50;
		}
	}

	void DrawProduction(Canvas c, RectF r, int accent)
	{
		Ui.PanelRect(c, r, 12, Ui.Panel, Ui.Line, scratch);

		// tabs
		var tw = (r.Width() - 16 - 3 * 8) / 4;
		for (var i = 0; i < 4; i++)
		{
			var t = new RectF(r.Left + 8 + i * (tw + 8), r.Top + 8, r.Left + 8 + i * (tw + 8) + tw, r.Top + 64);
			var qs = QueuesForTab(snap, i);
			var on = i == Tab;
			Ui.PanelRect(c, t, 8, on ? Ui.Alpha(accent, 0.18f) : Ui.Panel2, on ? accent : Ui.Line, scratch);
			var busy = qs.Sum(q => q.Items.Count(it => it.Progress >= 0));
			c.DrawText((de ? TabNamesDe : TabNamesEn)[i], t.Left + 16, t.Top + 36, Ui.Label(13, qs.Length == 0 ? Ui.Text3 : on ? Ui.Text : Ui.Text2));
			if (busy > 0)
			{
				var bp = Ui.TextPaint(Ui.MonoBold, 18, accent);
				c.DrawText(busy.ToString(), t.Right - 16 - bp.MeasureText(busy.ToString()), t.Top + 38, bp);
			}

			var tab = i;
			hits.Add((t, () => { Tab = tab; Producer = 0; Focus = -1; ScrollRow = 0; }, null));
		}

		var queues = QueuesForTab(snap, Tab);
		// start of a game: open the first tab that can produce something (usually Buildings after deploying the base)
		if (!autoTabDone && snap.Queues.Length > 0)
		{
			autoTabDone = true;
			if (queues.Length == 0)
				for (var t = 0; t < Tabs.Length; t++)
					if (QueuesForTab(snap, t).Length > 0) { Tab = t; queues = QueuesForTab(snap, t); break; }
		}

		var y = r.Top + 76;
		if (queues.Length > 1)
		{
			// producer chips: one per factory, each with its own queue
			var x = r.Left + 8;
			for (var i = 0; i < queues.Length && x < r.Right - 120; i++)
			{
				var label = $"{queues[i].Producer.ToUpperInvariant()} {i + 1}";
				var p = Ui.Label(12, i == Producer ? Ui.Text : Ui.Text2);
				var w = p.MeasureText(label) + 32;
				var chip = new RectF(x, y, x + w, y + 40);
				Ui.PanelRect(c, chip, 20, i == Producer ? Ui.Alpha(accent, 0.25f) : Ui.Panel2, i == Producer ? accent : Ui.Line, scratch);
				c.DrawText(label, chip.Left + 16, chip.Top + 26, p);
				var idx = i;
				hits.Add((chip, () => { Producer = idx; Focus = -1; }, null));
				x += w + 8;
			}

			y += 52;
		}

		var q = CurrentQueue(snap);
		if (q == null)
		{
			c.DrawText(de ? "Keine Produktion – baue zuerst das passende Gebäude." : "No production – build the matching structure first.",
				r.Left + 20, y + 40, Ui.TextPaint(Ui.Inter, 22, Ui.Text3));
			return;
		}

		const int cols = 2;
		var cw = (r.Width() - 16 - (cols - 1) * 8) / cols;
		const float ch = 104;
		// lists longer than the panel scroll by dragging (first visible row)
		var visibleRows = (int)((r.Bottom - 8 - y + 8) / (ch + 8));
		var totalRows = (q.Items.Length + cols - 1) / cols;
		MaxScrollRow = Math.Max(0, totalRows - visibleRows);
		ScrollRow = Math.Clamp(ScrollRow, 0, MaxScrollRow);
		productionRect = new RectF(r.Left, y, r.Right, r.Bottom);
		if (Focus >= 0)
		{
			var fr = Focus / cols;
			if (fr < ScrollRow) ScrollRow = fr;
			if (fr >= ScrollRow + visibleRows) ScrollRow = fr - visibleRows + 1;
		}
		var focusOn = Focus >= 0 && Java.Lang.JavaSystem.CurrentTimeMillis() < FocusUntil;
		for (var i = 0; i < q.Items.Length; i++)
		{
			var col = i % cols;
			var row = i / cols - ScrollRow;
			if (row < 0)
				continue;
			var card = new RectF(r.Left + 8 + col * (cw + 8), y + row * (ch + 8), r.Left + 8 + col * (cw + 8) + cw, y + row * (ch + 8) + ch);
			if (card.Bottom > r.Bottom - 8)
			{
				var more = Ui.Label(11, Ui.Text3);
				var t = de ? "▼ MEHR – WISCHEN" : "▼ MORE – SWIPE";
				c.DrawText(t, r.CenterX() - more.MeasureText(t) / 2, r.Bottom - 6, more);
				break;
			}

			var it = q.Items[i];
			var focused = focusOn && Focus == i;
			var oil = snap.Cash + snap.Resources;
			var dim = it.Locked || (it.Cost > oil && it.Count == 0);
			Ui.PanelRect(c, card, 10, it.Active ? Ui.Alpha(accent, 0.16f) : Ui.Panel2, focused ? Ui.Text : it.Active ? accent : Ui.Line, scratch);
			var iconBox = new RectF(card.Left + 8, card.Top + 8, card.Left + 8 + 80, card.Bottom - 8);
			scratch.Color = new Color(Ui.Bg2);
			c.DrawRoundRect(iconBox, 8, 8, scratch);
			if (Icon(it.Name) is { } bmp)
			{
				var s = Math.Min(iconBox.Width() / bmp.Width, iconBox.Height() / bmp.Height);
				s = s >= 1 ? (float)Math.Floor(s) : s;
				float w = bmp.Width * s, h = bmp.Height * s;
				iconPaint.Alpha = dim ? 90 : 255;
				iconPaint.FilterBitmap = s < 1; // integer upscale stays pixel-sharp
				c.DrawBitmap(bmp, null, new RectF(iconBox.CenterX() - w / 2, iconBox.CenterY() - h / 2, iconBox.CenterX() + w / 2, iconBox.CenterY() + h / 2), iconPaint);
			}

			var tx = iconBox.Right + 14;
			if (focused)
			{
				scratch.SetStyle(Paint.Style.Stroke);
				scratch.StrokeWidth = 3;
				scratch.Color = new Color(Ui.Text);
				c.DrawRoundRect(card, 10, 10, scratch);
				scratch.SetStyle(Paint.Style.Fill);
			}

			var name = Ui.TextPaint(Ui.InterSemi, 19, dim ? Ui.Text3 : Ui.Text);
			c.DrawText(Ellipsize(it.Title, name, card.Right - tx - 44), tx, card.Top + 32, name);
			var costText = (de ? "ÖL " : "OIL ") + it.Cost;
			c.DrawText(costText, tx, card.Top + 60, Ui.TextPaint(Ui.Mono, 16, it.Cost > oil && !it.Locked ? Ui.Red : dim ? Ui.Text3 : Ui.Text2));
			if (it.Locked)
				c.DrawText(de ? "GESPERRT" : "LOCKED", tx, card.Top + 84, Ui.Label(10, Ui.Text3));
			else if (it.Paused)
				c.DrawText(de ? "ANGEHALTEN" : "ON HOLD", tx, card.Top + 84, Ui.Label(10, accent));
			if (it.Count != 0 && !it.Building)
			{
				var badge = it.Count < 0 ? "∞" : it.Count.ToString();
				var bp = Ui.TextPaint(Ui.MonoBold, 22, accent);
				c.DrawText(badge, card.Right - 16 - bp.MeasureText(badge), card.Top + 34, bp);
			}

			if (it.Progress >= 0)
				Ui.Bar(c, new RectF(tx, card.Bottom - 16, card.Right - 14, card.Bottom - 10), it.Progress / 100f, accent, Ui.Text, scratch);
			else if (it.Building && it.Active)
				c.DrawText(de ? "OBEN PLATZIEREN" : "PLACE ON TOP", tx, card.Top + 84, Ui.Label(10, accent));

			if (it.Locked)
				continue;

			var item = it.Name;
			var actorId = q.ActorId;
			var running = it.Progress >= 0 && !it.Paused;
			hits.Add((card, () => ThorDeckBridge.Produce(actorId, item, 0), () =>
			{
				ThorDeckBridge.Produce(actorId, item, 1);
				Toast(running ? (de ? "Angehalten" : "On hold") : (de ? "Abgebrochen" : "Cancelled"));
			}));
		}
	}

	Bitmap? Icon(string actor)
	{
		if (icons.TryGetValue(actor, out var b))
			return b;
		if (!ThorDeckBridge.Icons.TryGetValue(actor, out var px))
		{
			ThorDeckBridge.RequestIcon(actor);
			return null;
		}

		b = Bitmap.CreateBitmap(px.Argb, px.Width, px.Height, Bitmap.Config.Argb8888!)!;
		icons[actor] = b;
		return b;
	}

	static string Ellipsize(string s, Paint p, float max)
	{
		if (p.MeasureText(s) <= max)
			return s;
		while (s.Length > 1 && p.MeasureText(s + "…") > max)
			s = s[..^1];
		return s + "…";
	}

	void DrawGroups(Canvas c, RectF r, int accent)
	{
		var gw = (r.Width() - 9 * 8) / 10;
		for (var i = 0; i < 10; i++)
		{
			var g = new RectF(r.Left + i * (gw + 8), r.Top, r.Left + i * (gw + 8) + gw, r.Bottom);
			var n = snap.Groups[i];
			Ui.PanelRect(c, g, 10, n > 0 ? Ui.Panel2 : Ui.Bg2, n > 0 ? Ui.Line2 : Ui.Line, scratch);
			var key = ((i + 1) % 10).ToString();
			c.DrawText(key, g.Left + 14, g.Top + 34, Ui.TextPaint(Ui.MonoBold, 24, n > 0 ? accent : Ui.Text3));
			if (n > 0)
				c.DrawText(n.ToString(), g.Left + 14, g.Bottom - 16, Ui.TextPaint(Ui.Mono, 18, Ui.Text2));
			var grp = i;
			hits.Add((g, () => ThorDeckBridge.Group(grp, false), () => { ThorDeckBridge.Group(grp, true); Toast((de ? "Gruppe " : "Group ") + key); }));
		}
	}

	void DrawCommands(Canvas c, RectF r, int accent)
	{
		var bw = (r.Width() - (Commands.Length - 1) * 8) / Commands.Length;
		for (var i = 0; i < Commands.Length; i++)
		{
			var b = new RectF(r.Left + i * (bw + 8), r.Top, r.Left + i * (bw + 8) + bw, r.Bottom);
			var (id, en, deLabel) = Commands[i];
			var on = id switch
			{
				"attack" => snap.Mode == "AttackMoveOrderGenerator",
				"sell" => snap.Mode == "SellOrderGenerator",
				"repair" => snap.Mode == "TechnicianEnterOrderGenerator",
				"move" => ThorInput.ForceMove,
				"waypoint" => ThorDeckBridge.WaypointMode,
				_ => false,
			};
			Ui.PanelRect(c, b, 12, on ? Ui.Alpha(accent, 0.22f) : Ui.Panel, on ? accent : Ui.Line, scratch);
			var label = de ? deLabel : en;
			var p = Ui.Label(label.Length > 8 ? 10 : 12, on ? Ui.Text : Ui.Text2);
			c.DrawText(label, b.CenterX() - p.MeasureText(label) / 2, b.CenterY() + 6, p);
			hits.Add((b, () => Command(id), null));
		}
	}

	void Command(string id)
	{
		switch (id)
		{
			case "deploy": ThorDeckBridge.Deploy(); break;
			case "stop": ThorDeckBridge.Stop(); break;
			case "attack": case "sell": case "repair": ThorDeckBridge.SetMode(id); break;
			case "move": ThorInput.ForceMove = !ThorInput.ForceMove; break;
			case "waypoint": ThorDeckBridge.WaypointMode = !ThorDeckBridge.WaypointMode; break;
			case "menu": ThorInput.PressKey(ThorInput.KeyEscape); break;
			case "save":
				ThorDeckBridge.Save();
				Toast(de ? "Gespeichert" : "Saved");
				break;
		}
	}

	public void Toast(string s)
	{
		toast = s;
		toastUntil = Java.Lang.JavaSystem.CurrentTimeMillis() + 1500;
	}

	// ---- touch ----
	long downAt;
	float downX, downY;
	bool longFired, onMinimap;

	public override bool OnTouchEvent(MotionEvent? e)
	{
		if (e == null)
			return false;

		var x = (e.GetX() - offX) / scale;
		var y = (e.GetY() - offY) / scale;
		switch (e.ActionMasked)
		{
			case MotionEventActions.Down:
				downAt = e.EventTime;
				downX = x;
				downY = y;
				longFired = false;
				onMinimap = minimapRect.Contains(x, y);
				dragging = false;
				dragStartY = y;
				dragRow0 = ScrollRow;
				if (onMinimap)
					Jump(x, y);
				else
					handler.PostDelayed(checkLong, 450);
				break;
			case MotionEventActions.Move:
				if (onMinimap && minimapRect.Contains(x, y))
					Jump(x, y);
				else if (!onMinimap && productionRect.Contains(downX, downY) && Math.Abs(y - dragStartY) > 30)
				{
					dragging = true;
					handler.RemoveCallbacks(checkLong);
					ScrollRow = Math.Clamp(dragRow0 - (int)Math.Round((y - dragStartY) / 112f), 0, MaxScrollRow);
					Invalidate();
				}

				break;
			case MotionEventActions.Up:
				handler.RemoveCallbacks(checkLong);
				if (!onMinimap && !longFired && !dragging)
					foreach (var h in hits.ToArray())
						if (h.Rect.Contains(x, y))
						{
							h.Tap();
							break;
						}

				Invalidate();
				break;
		}

		return true;
	}

	void CheckLong()
	{
		foreach (var h in hits.ToArray())
			if (h.Rect.Contains(downX, downY) && h.Long != null)
			{
				longFired = true;
				h.Long();
				PerformHapticFeedback(FeedbackConstants.LongPress);
				Invalidate();
				return;
			}
	}

	void Jump(float x, float y)
	{
		if (minimapToCell == null)
			return;
		var (cx, cy) = minimapToCell(x, y);
		ThorDeckBridge.JumpTo(cx, cy);
	}

	/// Controller: L1/R1 move the focus ring over the build cards, A builds the focused card.
	public static void MoveFocus(int delta)
	{
		var q = CurrentQueue(ThorDeckBridge.Current);
		if (q == null || q.Items.Length == 0)
			return;
		var now = Java.Lang.JavaSystem.CurrentTimeMillis();
		Focus = Focus < 0 || now > FocusUntil ? 0 : (Focus + delta + q.Items.Length) % q.Items.Length;
		FocusUntil = now + 4000;
	}

	public static bool ActivateFocus()
	{
		if (Focus < 0 || Java.Lang.JavaSystem.CurrentTimeMillis() > FocusUntil)
			return false;
		var q = CurrentQueue(ThorDeckBridge.Current);
		if (q == null || Focus >= q.Items.Length)
			return false;
		ThorDeckBridge.Produce(q.ActorId, q.Items[Focus].Name, false);
		FocusUntil = Java.Lang.JavaSystem.CurrentTimeMillis() + 4000;
		return true;
	}
}
