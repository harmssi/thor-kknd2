using Path = System.IO.Path;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using OpenRA.Mods.OpenKrush.Thor;

namespace ThorKKND2.Deck;

/// The Thor's lower screen in the look of the player's faction: minimap, oil economy, build menus with queues, research,
/// selection, control groups and command buttons.
/// Drawn in a 1240x1080 design space and scaled to the real display.
public class DeckView : View
{
	public static readonly string[][] Tabs = { new[] { "infantry" }, new[] { "vehicle", "beast", "aircraft" }, new[] { "building" }, new[] { "tower", "wall" } };
	static readonly string[] TabNamesEn = { "INFANTRY", "VEHICLES", "BUILDINGS", "DEFENSE" };
	static readonly string[] TabNamesDe = { "INFANTERIE", "FAHRZEUGE", "GEBÄUDE", "ABWEHR" };
	static readonly (string Id, string En, string De)[] Commands =
	{
		("deploy", "DEPLOY", "AUFBAUEN"), ("stop", "STOP", "STOPP"), ("attack", "ATTACK", "ANGRIFF"), ("move", "MOVE", "BEWEGEN"), ("waypoint", "WAYPOINT", "WEGPUNKT"), ("scatter", "SCATTER", "STREUEN"),
		("stance", "STANCE", "HALTUNG"), ("sell", "SELL", "VERKAUFEN"), ("repair", "REPAIR", "REPARIEREN"), ("army", "ALL ARMY", "ARMEE"), ("type", "SAME TYPE", "GLEICHE"), ("base", "BASE", "BASIS"),
		("alert", "ALERT", "ALARM"), ("pause", "PAUSE", "PAUSE"), ("speed", "SPEED", "TEMPO"), ("find", "FIND", "FINDEN"), ("save", "SAVE", "SPEICHERN"), ("menu", "MENU", "MENÜ"),
	};

	public const int TabCount = 5, TechTab = 4; // four build menus + research

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

	public static DeckSnapshot.Queue[] QueuesForTab(DeckSnapshot s, int tab) => tab >= Tabs.Length ? Array.Empty<DeckSnapshot.Queue>() : s.Queues.Where(q => Tabs[tab].Contains(q.Category)).ToArray();

	public static DeckSnapshot.Queue? CurrentQueue(DeckSnapshot s)
	{
		var qs = QueuesForTab(s, Tab);
		return qs.Length == 0 ? null : qs[Math.Clamp(Producer, 0, qs.Length - 1)];
	}

	// ---- the faction's own look: button plates and bitmap font from the player's game files ----
	Art? art;
	string? artFaction;
	int accent, bg;
	readonly Paint pix = new() { FilterBitmap = false }, fontPaint = new(); // art pixels stay sharp
	readonly Rect src = new();
	readonly RectF dst = new(); // reused: the deck redraws ten times a second
	readonly Dictionary<int, PorterDuffColorFilter> filters = new();
	const float P = 3; // screen pixels per art pixel

	PorterDuffColorFilter Filter(int color)
	{
		if (!filters.TryGetValue(color, out var f))
			filters[color] = f = new PorterDuffColorFilter(new Color(color), PorterDuff.Mode.Multiply!);
		return f;
	}

	void LoadArt()
	{
		if (snap.Faction == artFaction)
			return;
		artFaction = snap.Faction;
		art = Art.Load(Path.Combine(Paths.Content(Context!), "levels"), snap.Faction);
		(accent, bg) = snap.Faction switch
		{
			"evolved" => (unchecked((int)0xFFFFA428), unchecked((int)0xFF151413)),
			"series9" => (unchecked((int)0xFFFFD630), unchecked((int)0xFF0E1218)),
			_ => (unchecked((int)0xFFFF6A48), unchecked((int)0xFF17120F)),
		};
	}

	/// A button or panel in the faction's plate (stretched from the game's blank 32x32 sidebar button, corners kept).
	void Plate(Canvas c, RectF r, bool on = false, bool dim = false, bool panel = false)
	{
		if (art == null)
		{
			Ui.PanelRect(c, r, 6, panel ? Ui.Bg2 : on ? Ui.Alpha(accent, 0.25f) : Ui.Panel2, on ? accent : Ui.Line2, scratch);
			return;
		}

		pix.SetColorFilter(Filter(panel ? unchecked((int)0xFF4A4A4A) : dim ? unchecked((int)0xFF808080) : Shade)); // a little darker than in the game: its light font has to read on it
		const int k = 7;
		var b = art.Buttons[Art.Blank];
		float l = MathF.Round(r.Left), t = MathF.Round(r.Top), rr = MathF.Round(r.Right), bb = MathF.Round(r.Bottom); // whole pixels: no seams between the nine parts
		float[] dx = { l, l + k * P, rr - k * P, rr }, dy = { t, t + k * P, bb - k * P, bb };
		int[] sx = { 0, k, b.Width - k, b.Width }, sy = { 0, k, b.Height - k, b.Height };
		for (var i = 0; i < 3; i++)
			for (var j = 0; j < 3; j++)
			{
				src.Set(sx[i], sy[j], sx[i + 1], sy[j + 1]);
				dst.Set(dx[i], dy[j], dx[i + 1], dy[j + 1]);
				c.DrawBitmap(b, src, dst, pix);
			}

		pix.SetColorFilter(null);
		if (on)
			Outline(c, r, accent);
	}

	void Outline(Canvas c, RectF r, int color)
	{
		scratch.SetStyle(Paint.Style.Fill);
		scratch.Color = new Color(Ui.Alpha(color, 0.22f));
		c.DrawRect(r.Left + 6, r.Top + 6, r.Right - 6, r.Bottom - 6, scratch);
		scratch.SetStyle(Paint.Style.Stroke);
		scratch.StrokeWidth = P;
		scratch.Color = new Color(color);
		c.DrawRect(r.Left + 4.5f, r.Top + 4.5f, r.Right - 4.5f, r.Bottom - 4.5f, scratch);
		scratch.SetStyle(Paint.Style.Fill);
	}

	/// One of the game's own sidebar buttons (plate with symbol), e.g. the infantry or building button.
	void Symbol(Canvas c, RectF r, int frame, bool on, bool dim)
	{
		if (art == null)
			return;
		var s = Math.Min(r.Width(), r.Height());
		pix.SetColorFilter(Filter(dim ? unchecked((int)0xFF808080) : Shade));
		c.DrawBitmap(art.Buttons[frame], null, new RectF(r.CenterX() - s / 2, r.CenterY() - s / 2, r.CenterX() + s / 2, r.CenterY() + s / 2), pix);
		pix.SetColorFilter(null);
		if (on)
			Outline(c, r, accent);
	}

	/// Text in the game's bitmap font (11 px capitals); top = upper edge of the line. Returns the width.
	float Txt(Canvas c, string s, float x, float top, float sc = 2, int color = 0, bool draw = true)
	{
		if (art == null)
		{
			var p = Ui.TextPaint(Ui.InterBold, 9.5f * sc, color == 0 ? Ui.Text : color);
			if (draw)
				c.DrawText(s, x, top + 11.5f * sc, p);
			return p.MeasureText(s);
		}

		fontPaint.SetColorFilter(color == 0 ? null : Filter(color));
		fontPaint.FilterBitmap = sc != MathF.Floor(sc);
		var x0 = x;
		foreach (var ch in s)
		{
			var i = ch - 32;
			var g = art.Glyphs[i < 0 || i >= art.Glyphs.Length ? '?' - 32 : i];
			if (draw && g.Bmp != null)
			{
				dst.Set(x + g.X * sc, top + g.Y * sc, x + (g.X + g.W) * sc, top + (g.Y + g.Bmp.Height) * sc);
				c.DrawBitmap(g.Bmp, null, dst, fontPaint);
			}

			x += (g.W + 1) * sc;
		}

		return x - x0;
	}

	float TxtW(string s, float sc = 2) => Txt(null!, s, 0, 0, sc, 0, false);
	void TxtRight(Canvas c, string s, float right, float top, float sc = 2, int color = 0) => Txt(c, s, right - TxtW(s, sc), top, sc, color);
	void TxtCenter(Canvas c, string s, float cx, float top, float sc = 2, int color = 0) => Txt(c, s, cx - TxtW(s, sc) / 2, top, sc, color);

	string Fit(string s, float max, float sc = 2)
	{
		if (TxtW(s, sc) <= max)
			return s;
		while (s.Length > 1 && TxtW(s + "..", sc) > max)
			s = s[..^1];
		return s + "..";
	}

	void Bar(Canvas c, RectF r, float fraction, int color)
	{
		scratch.SetStyle(Paint.Style.Fill);
		scratch.Color = Color.Black;
		c.DrawRect(r, scratch);
		scratch.Color = new Color(color);
		c.DrawRect(r.Left + P, r.Top + P, r.Left + P + (r.Width() - 2 * P) * Math.Clamp(fraction, 0, 1), r.Bottom - P, scratch);
	}

	void Cameo(Canvas c, RectF box, string? actor, bool dim)
	{
		if (actor == null || Icon(actor) is not { } bmp)
			return;
		var s = Math.Min(box.Width() / bmp.Width, box.Height() / bmp.Height);
		s = s >= 1 ? (float)Math.Floor(s) : s;
		float w = bmp.Width * s, h = bmp.Height * s;
		iconPaint.Alpha = dim ? 90 : 255;
		iconPaint.FilterBitmap = s < 1; // integer upscale stays pixel-sharp
		c.DrawBitmap(bmp, null, new RectF(box.CenterX() - w / 2, box.CenterY() - h / 2, box.CenterX() + w / 2, box.CenterY() + h / 2), iconPaint);
	}

	const int Shade = unchecked((int)0xFFC4C4C4), Dim = unchecked((int)0xFF8C8C8C), Good = unchecked((int)0xFF58D868), Bad = unchecked((int)0xFFFF4840);
	static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";
	static string SpeedText() => ThorDeckBridge.SpeedPercent switch { > 190 => "2X", > 140 => "1.5X", < 60 => "0.5X", _ => "1X" };

	protected override void OnDraw(Canvas c)
	{
		scale = Math.Min(Width / W, Height / H);
		offX = (Width - W * scale) / 2;
		offY = (Height - H * scale) / 2;
		LoadArt();
		c.DrawColor(new Color(bg));
		c.Save();
		c.Translate(offX, offY);
		c.Scale(scale, scale);
		hits.Clear();

		DrawHeader(c);
		DrawMinimap(c, new RectF(16, 96, 436, 516));
		DrawSelection(c, new RectF(16, 524, 436, 676));
		DrawPages(c, new RectF(448, 96, 1224, 676));
		DrawGroups(c, new RectF(16, 684, 1224, 796));
		DrawCommands(c, new RectF(16, 804, 1224, 1060));

		if (snap.GameOver)
		{
			var banner = new RectF(448, 380, 1224, 500);
			Plate(c, banner, true);
			TxtCenter(c, snap.Won ? (de ? "SIEG" : "VICTORY") : (de ? "NIEDERLAGE" : "DEFEAT"), banner.CenterX(), banner.Top + 30, 4, snap.Won ? Good : Bad);
		}

		if (Java.Lang.JavaSystem.CurrentTimeMillis() < toastUntil)
		{
			var tw = TxtW(toast, 3);
			var r = new RectF(620 - tw / 2 - 36, 440, 620 + tw / 2 + 36, 530);
			Plate(c, r, true);
			Txt(c, toast, r.Left + 36, r.Top + 24, 3);
		}

		c.Restore();
	}

	void DrawHeader(Canvas c)
	{
		if (art?.Logo is { } logo)
			c.DrawBitmap(logo, null, new RectF(16, 8, 16 + 24 * P, 8 + 24 * P), pix);
		var faction = snap.Faction switch { "survivors" => "SURVIVORS", "evolved" => "EVOLVED", "series9" => "SERIES 9", _ => "KKND2 THOR" };
		Txt(c, faction, 104, 8, 3, accent);
		if (snap.MapWidth > 0)
		{
			var x = 104 + Txt(c, Clock(snap.Seconds), 104, 50) + 24;
			x += Txt(c, SpeedText(), x, 50, 2, Dim) + 24;
			if (ThorDeckBridge.IsPaused)
				Txt(c, de ? "PAUSE" : "PAUSED", x, 50, 2, accent);

			// oil economy: drill rigs, tankers, power stations and what is left under the rigs
			var eco = de ? $"BOHRTURM {snap.Rigs}  TANKER {snap.Tankers}  KRAFTWERK {snap.Stations}" : $"RIGS {snap.Rigs}  TANKERS {snap.Tankers}  POWER {snap.Stations}";
			Txt(c, eco, 448, 14, 2, Dim);
			Txt(c, (de ? "IM BODEN " : "IN GROUND ") + snap.OilLeft.ToString("N0"), 448, 50, 2, snap.Rigs > 0 && snap.OilLeft < 5000 ? Bad : Dim);
		}

		if (snap.MapWidth == 0)
			return;
		var value = (snap.Cash + snap.Resources).ToString("N0");
		TxtRight(c, value, 1224, 20, 4);
		TxtRight(c, de ? "ÖL" : "OIL", 1224 - TxtW(value, 4) - 20, 42, 2, accent);
	}

	void DrawMinimap(Canvas c, RectF r)
	{
		Plate(c, r, panel: true);
		if (snap.MapWidth == 0)
		{
			TxtCenter(c, de ? "KEINE KARTE" : "NO MAP", r.CenterX(), r.CenterY() - 12, 2, Dim);
			minimapRect = new RectF();
			return;
		}

		LoadMinimap();
		var inner = new RectF(r.Left + 12, r.Top + 12, r.Right - 12, r.Bottom - 12);
		var b = snap.Bounds;
		float sx = inner.Width() / b.Width, sy = inner.Height() / b.Height, s = Math.Min(sx, sy);
		var ox = inner.Left + (inner.Width() - b.Width * s) / 2;
		var oy = inner.Top + (inner.Height() - b.Height * s) / 2;
		RectF Cell(float x, float y, float w, float h) => new(ox + (x - b.X) * s, oy + (y - b.Y) * s, ox + (x - b.X + w) * s, oy + (y - b.Y + h) * s);

		c.Save();
		c.ClipRect(inner);
		scratch.Color = Color.Black;
		c.DrawRect(inner, scratch);
		if (minimap != null)
			c.DrawBitmap(minimap, null, Cell(0, 0, snap.MapWidth, snap.MapHeight), new Paint(PaintFlags.FilterBitmap)); // .mim covers the whole map (incl. border)

		foreach (var d in snap.Dots)
		{
			scratch.Color = new Color(d.Argb | unchecked((int)0xFF000000));
			var sz = d.Building ? 2.2f : 1.2f;
			c.DrawRect(Cell(d.X - sz / 2, d.Y - sz / 2, sz, sz), scratch);
		}

		scratch.SetStyle(Paint.Style.Stroke);
		scratch.StrokeWidth = 2;
		scratch.Color = new Color(Ui.Text);
		c.DrawRect(Cell(snap.CamX, snap.CamY, snap.CamW, snap.CamH), scratch);

		// attack alerts: blinking rings
		if (Java.Lang.JavaSystem.CurrentTimeMillis() / 300 % 2 == 0)
			foreach (var p in snap.Pings)
			{
				scratch.StrokeWidth = 4;
				scratch.Color = new Color(Bad);
				var o = Cell(p.X, p.Y, 0, 0);
				c.DrawCircle(o.Left, o.Top, 16, scratch);
			}

		scratch.SetStyle(Paint.Style.Fill);
		c.Restore();

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

	void DrawSelection(Canvas c, RectF r)
	{
		Plate(c, r, panel: true);
		if (snap.Selection.Length == 0)
		{
			Txt(c, de ? "NICHTS GEWÄHLT" : "NOTHING SELECTED", r.Left + 20, r.Top + 18, 2, Dim);
			return;
		}

		var y = r.Top + 12;
		foreach (var s in snap.Selection.Take(3))
		{
			Cameo(c, new RectF(r.Left + 12, y, r.Left + 52, y + 40), s.Name, false);
			var count = "x" + s.Count;
			TxtRight(c, count, r.Right - 16, y, 2, accent);
			Txt(c, Fit(s.Title, r.Width() - 90 - TxtW(count), 2), r.Left + 62, y);
			Bar(c, new RectF(r.Left + 62, y + 28, r.Right - 16, y + 40), s.HealthPercent / 100f, s.HealthPercent > 50 ? Good : s.HealthPercent > 25 ? accent : Bad);
			y += 44;
		}
	}

	/// What one card of the right-hand list shows; the build pages and the research page share it.
	sealed record CardData(string Icon, string Title, string Line, int LineColor, string Status, string Badge, int Progress, bool On, bool Dim, Action? Tap, Action? Long);

	List<CardData> Cards()
	{
		var list = new List<CardData>();
		var oil = snap.Cash + snap.Resources;
		if (Tab == TechTab)
		{
			foreach (var t in snap.Techs)
			{
				var done = t.Level >= t.MaxLevel;
				var id = t.ActorId;
				var can = !done && (t.Busy || snap.FreeLabs > 0);
				list.Add(new(t.Name, t.Title, (de ? "STUFE " : "LEVEL ") + $"{t.Level}/{t.MaxLevel}", done ? Good : 0,
					t.Busy ? (de ? "FORSCHT" : "RESEARCHING") : done ? "MAX" : (de ? "ÖL " : "OIL ") + t.Cost, "", -1, t.Busy, !can && !t.Busy,
					can ? () => { ThorDeckBridge.Research(id); Toast(t.Busy ? (de ? "Forschung gestoppt" : "Research stopped") : (de ? "Forschung gestartet" : "Research started")); } : null, null));
			}

			return list;
		}

		var q = CurrentQueue(snap);
		if (q == null)
			return list;
		foreach (var it in q.Items)
		{
			var item = it.Name;
			var actorId = q.ActorId;
			var running = it.Progress >= 0 && !it.Paused;
			var poor = it.Cost > oil && !it.Locked;
			list.Add(new(it.Name, it.Title, (de ? "ÖL " : "OIL ") + it.Cost, poor ? Bad : 0,
				it.Locked ? (de ? "GESPERRT" : "LOCKED") : it.Paused ? (de ? "ANGEHALTEN" : "ON HOLD") : it.Building && it.Active ? (de ? "OBEN SETZEN" : "PLACE ON TOP") : "",
				it.Count == 0 || it.Building ? "" : it.Count < 0 ? "INF" : it.Count.ToString(), it.Progress, it.Active, it.Locked || (poor && it.Count == 0),
				it.Locked ? null : () => ThorDeckBridge.Produce(actorId, item, 0),
				it.Locked ? null : () =>
				{
					ThorDeckBridge.Produce(actorId, item, 1);
					Toast(running ? (de ? "Angehalten" : "On hold") : (de ? "Abgebrochen" : "Cancelled"));
				}));
		}

		return list;
	}

	void DrawPages(Canvas c, RectF r)
	{
		// tabs: the game's own sidebar buttons for the four build menus, then research
		int[] symbols = { Art.Infantry, Art.Vehicle, Art.Building, Art.Tower };
		const float ts = 128;
		for (var i = 0; i < TabCount; i++)
		{
			var t = i < 4 ? new RectF(r.Left + i * (ts + 8), r.Top, r.Left + i * (ts + 8) + ts, r.Top + ts) : new RectF(r.Left + 4 * (ts + 8), r.Top, r.Right, r.Top + ts);
			var on = i == Tab;
			if (i == TechTab)
			{
				Plate(c, t, on);
				Txt(c, de ? "FORSCHUNG" : "RESEARCH", t.Left + 22, t.Top + 20, 2, on ? accent : 0);
				var labs = snap.Labs == 0 ? (de ? "KEIN LABOR" : "NO LAB") : (de ? $"LABOR {snap.FreeLabs}/{snap.Labs} FREI" : $"LABS {snap.FreeLabs}/{snap.Labs} FREE");
				Txt(c, labs, t.Left + 22, t.Top + 54, 2, Dim);
				var busyTech = snap.Techs.Count(x => x.Busy);
				if (busyTech > 0)
					Txt(c, (de ? "LÄUFT " : "RUNNING ") + busyTech, t.Left + 22, t.Top + 88, 2, accent);
			}
			else
			{
				var qs = QueuesForTab(snap, i);
				if (art != null)
					Symbol(c, t, symbols[i], on, qs.Length == 0);
				else
				{
					Plate(c, t, on);
					TxtCenter(c, (de ? TabNamesDe : TabNamesEn)[i][..3], t.CenterX(), t.Top + 50, 2, qs.Length == 0 ? Dim : 0);
				}

				var busy = qs.Sum(q => q.Items.Count(it => it.Progress >= 0));
				if (busy > 0)
					TxtRight(c, busy.ToString(), t.Right - 14, t.Top + 10, 2, accent);
			}

			var tab = i;
			hits.Add((t, () => { Tab = tab; Producer = 0; Focus = -1; ScrollRow = 0; }, null));
		}

		var queues = QueuesForTab(snap, Tab);
		// start of a game: open the first tab that can produce something (usually Buildings after deploying the base)
		if (!autoTabDone && snap.Queues.Length > 0)
		{
			autoTabDone = true;
			if (queues.Length == 0 && Tab != TechTab)
				for (var t = 0; t < Tabs.Length; t++)
					if (QueuesForTab(snap, t).Length > 0) { Tab = t; queues = QueuesForTab(snap, t); break; }
		}

		var y = r.Top + ts + 8;
		if (queues.Length > 1)
		{
			// producer chips: one per factory, each with its own queue
			var x = r.Left;
			for (var i = 0; i < queues.Length && x < r.Right - 120; i++)
			{
				var label = $"{queues[i].Producer.ToUpperInvariant()} {i + 1}";
				var w = TxtW(label) + 36;
				var chip = new RectF(x, y, x + w, y + 48);
				Plate(c, chip, i == Producer);
				Txt(c, label, chip.Left + 18, chip.Top + 11, 2, i == Producer ? accent : 0);
				var idx = i;
				hits.Add((chip, () => { Producer = idx; Focus = -1; }, null));
				x += w + 8;
			}

			y += 56;
		}

		var cards = Cards();
		if (cards.Count == 0)
		{
			var msg = snap.MapWidth == 0 ? (de ? "STARTE EIN GEFECHT." : "START A SKIRMISH.")
				: Tab == TechTab ? (de ? "NOCH NICHTS ZU ERFORSCHEN." : "NOTHING TO RESEARCH YET.")
				: de ? "ERST DAS PASSENDE GEBÄUDE BAUEN." : "BUILD THE MATCHING STRUCTURE FIRST.";
			Txt(c, msg, r.Left + 12, y + 24, 2, Dim);
			productionRect = new RectF();
			return;
		}

		const int cols = 2;
		const float ch = 104;
		// lists longer than the panel scroll by dragging (first visible row)
		var visibleRows = (int)((r.Bottom - y + 8) / (ch + 8));
		var totalRows = (cards.Count + cols - 1) / cols;
		var cw = (r.Width() - (cols - 1) * 8 - (totalRows > visibleRows ? 18 : 0)) / cols;
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
		for (var i = 0; i < cards.Count; i++)
		{
			var col = i % cols;
			var row = i / cols - ScrollRow;
			if (row < 0)
				continue;
			if (row >= visibleRows)
				break;

			var card = new RectF(r.Left + col * (cw + 8), y + row * (ch + 8), r.Left + col * (cw + 8) + cw, y + row * (ch + 8) + ch);
			var d = cards[i];
			Plate(c, card, d.On, d.Dim);
			Cameo(c, new RectF(card.Left + 12, card.Top + 10, card.Left + 96, card.Bottom - 10), d.Icon, d.Dim);
			var tx = card.Left + 106;
			var badgeW = d.Badge == "" ? 0 : TxtW(d.Badge, 3) + 10;
			Txt(c, Fit(d.Title, card.Right - tx - 14 - badgeW), tx, card.Top + 12, 2, d.Dim ? Dim : 0);
			if (d.Badge != "")
				TxtRight(c, d.Badge, card.Right - 14, card.Top + 10, 3);
			var lw = Txt(c, d.Line, tx, card.Top + 42, 2, d.LineColor != 0 ? d.LineColor : d.Dim ? Dim : accent);
			if (d.Status != "" && TxtW(d.Status) <= card.Right - tx - lw - 30)
				Txt(c, d.Status, tx + lw + 16, card.Top + 42, 2, d.Dim ? Dim : 0);
			else if (d.Status != "")
				Txt(c, d.Status, tx, card.Top + 68, 2, d.Dim ? Dim : 0); // too long for the same line: third line
			if (d.Progress >= 0)
				Bar(c, new RectF(tx, card.Bottom - 26, card.Right - 16, card.Bottom - 12), d.Progress / 100f, accent);
			if (focusOn && Focus == i)
				Outline(c, card, Ui.Text);
			if (d.Tap != null)
				hits.Add((card, d.Tap, d.Long));
		}

		if (MaxScrollRow > 0)
		{
			// scroll bar: drag the list to scroll
			var track = new RectF(r.Right - 10, y, r.Right, y + visibleRows * (ch + 8) - 8);
			scratch.Color = Color.Black;
			c.DrawRect(track, scratch);
			scratch.Color = new Color(accent);
			var th = track.Height() * visibleRows / totalRows;
			var tt = track.Top + (track.Height() - th) * ScrollRow / MaxScrollRow;
			c.DrawRect(track.Left, tt, track.Right, tt + th, scratch);
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

	void DrawGroups(Canvas c, RectF r)
	{
		var gw = MathF.Floor((r.Width() - 9 * 6) / 10);
		for (var i = 0; i < 10; i++)
		{
			var g = new RectF(r.Left + i * (gw + 6), r.Top, r.Left + i * (gw + 6) + gw, r.Bottom);
			var n = snap.Groups[i];
			Plate(c, g, false, n == 0);
			var key = ((i + 1) % 10).ToString();
			Txt(c, key, g.Left + 12, g.Top + 8, 3, n > 0 ? accent : Dim);
			if (n > 0)
			{
				Cameo(c, new RectF(g.Left + 40, g.Top + 14, g.Right - 10, g.Bottom - 34), snap.GroupIcons[i], false);
				TxtRight(c, "x" + n, g.Right - 12, g.Bottom - 34, 2);
			}

			var grp = i;
			hits.Add((g, () => ThorDeckBridge.Group(grp, false), () => { ThorDeckBridge.Group(grp, true); Toast((de ? "Gruppe " : "Group ") + key); }));
		}
	}

	void DrawCommands(Canvas c, RectF r)
	{
		const int cols = 6, rows = 3;
		float bw = MathF.Floor((r.Width() - (cols - 1) * 8) / cols), bh = MathF.Floor((r.Height() - (rows - 1) * 8) / rows);
		for (var i = 0; i < Commands.Length; i++)
		{
			var b = new RectF(r.Left + i % cols * (bw + 8), r.Top + i / cols * (bh + 8), r.Left + i % cols * (bw + 8) + bw, r.Top + i / cols * (bh + 8) + bh);
			var (id, en, deLabel) = Commands[i];
			var on = id switch
			{
				"attack" => snap.Mode == "AttackMoveOrderGenerator",
				"sell" => snap.Mode == "SellOrderGenerator",
				"repair" => snap.Mode == "TechnicianEnterOrderGenerator",
				"move" => ThorInput.ForceMove,
				"waypoint" => ThorDeckBridge.WaypointMode,
				"pause" => ThorDeckBridge.IsPaused,
				"alert" => snap.Pings.Length > 0,
				_ => false,
			};
			var state = id switch
			{
				"stance" => snap.Stance switch
				{
					"HoldFire" => de ? "FEUER AUS" : "HOLD FIRE", "ReturnFire" => de ? "ERWIDERN" : "RETURN FIRE",
					"Defend" => de ? "VERTEIDIGEN" : "DEFEND", "AttackAnything" => de ? "ALLES" : "ATTACK ALL", _ => "",
				},
				"speed" => SpeedText(),
				_ => "",
			};
			var dim = id is "stance" && snap.Stance == "" || id is "find" && snap.Selection.Length == 0;
			Plate(c, b, on, dim);
			TxtCenter(c, de ? deLabel : en, b.CenterX(), state == "" ? b.Top + 25 : b.Top + 10, 2, dim ? Dim : on ? accent : 0);
			if (state != "")
				TxtCenter(c, state, b.CenterX(), b.Top + 42, 2, dim ? Dim : accent);
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
			case "scatter": ThorDeckBridge.Scatter(); break;
			case "stance": ThorDeckBridge.NextStance(); break;
			case "army": ThorDeckBridge.SelectArmy(false); break;
			case "type": ThorDeckBridge.SelectArmy(true); break;
			case "base": ThorDeckBridge.NextBase(); break;
			case "alert": ThorDeckBridge.LastEvent(); break;
			case "pause": ThorDeckBridge.TogglePause(); break;
			case "speed": ThorDeckBridge.NextSpeed(); break;
			case "menu": ThorInput.PressKey(ThorInput.KeyEscape); break;
			case "find": ThorDeckBridge.FindSelection(); break;
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

	/// Controller: L1/R1 move the focus ring over the cards (build items or research rows), A activates the focused card.
	static int CardCount(DeckSnapshot s) => Tab == TechTab ? s.Techs.Length : CurrentQueue(s)?.Items.Length ?? 0;

	public static void MoveFocus(int delta)
	{
		var n = CardCount(ThorDeckBridge.Current);
		if (n == 0)
			return;
		var now = Java.Lang.JavaSystem.CurrentTimeMillis();
		Focus = Focus < 0 || now > FocusUntil ? 0 : (Focus + delta + n) % n;
		FocusUntil = now + 4000;
	}

	public static bool ActivateFocus()
	{
		var s = ThorDeckBridge.Current;
		if (Focus < 0 || Java.Lang.JavaSystem.CurrentTimeMillis() > FocusUntil || Focus >= CardCount(s))
			return false;
		if (Tab == TechTab)
			ThorDeckBridge.Research(s.Techs[Focus].ActorId);
		else
		{
			var q = CurrentQueue(s)!;
			ThorDeckBridge.Produce(q.ActorId, q.Items[Focus].Name, false);
		}

		FocusUntil = Java.Lang.JavaSystem.CurrentTimeMillis() + 4000;
		return true;
	}
}
