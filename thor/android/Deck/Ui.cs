using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;

namespace ThorKKND2.Deck;

/// Toravo-style design tokens (same family as Thor Conquer / Heroes III Thor): near-black surfaces, 1-px lines,
/// Inter for UI with wide-tracked uppercase labels, JetBrains Mono for numbers. Accent follows the faction.
public static class Ui
{
	public const int Bg = unchecked((int)0xFF08090B);
	public const int Bg2 = unchecked((int)0xFF0D0F12);
	public const int Panel = unchecked((int)0xFF121418);
	public const int Panel2 = unchecked((int)0xFF181B20);
	public const int Line = unchecked((int)0xFF252931);
	public const int Line2 = unchecked((int)0xFF323844);
	public const int Text = unchecked((int)0xFFF2F2EF);
	public const int Text2 = unchecked((int)0xFFA7ABB3);
	public const int Text3 = unchecked((int)0xFF6A707B);
	public const int Accent = unchecked((int)0xFFD8732E); // rust (Survivors / neutral)
	public const int Green = unchecked((int)0xFF4CC38A);
	public const int Red = unchecked((int)0xFFE5484D);

	public static int AccentFor(string faction) => faction switch
	{
		"evolved" => unchecked((int)0xFF8CC63F),
		"series9" => unchecked((int)0xFF4FB3D9),
		_ => Accent,
	};

	public static Typeface Inter = Typeface.SansSerif!, InterSemi = Typeface.SansSerif!, InterBold = Typeface.DefaultBold!, Mono = Typeface.Monospace!, MonoBold = Typeface.Monospace!;
	static bool loaded;

	public static void Init(Context ctx)
	{
		if (loaded)
			return;
		loaded = true;
		Typeface L(string f, Typeface fb) { try { return Typeface.CreateFromAsset(ctx.Assets, "fonts/" + f)!; } catch { return fb; } }
		Inter = L("Inter-Regular.ttf", Inter);
		InterSemi = L("Inter-SemiBold.ttf", Inter);
		InterBold = L("Inter-Bold.ttf", InterBold);
		Mono = L("JetBrainsMono-Regular.ttf", Mono);
		MonoBold = L("JetBrainsMono-Bold.ttf", Mono);
	}

	public static int Alpha(int color, float a) => (color & 0x00FFFFFF) | ((int)Math.Round(a * 255) << 24);

	public static Paint Label(float size, int color) => new Paint(PaintFlags.AntiAlias) { Color = new Color(color), TextSize = size, LetterSpacing = 0.32f }.With(p => p.SetTypeface(InterSemi));
	public static Paint TextPaint(Typeface tf, float size, int color) => new Paint(PaintFlags.AntiAlias) { Color = new Color(color), TextSize = size }.With(p => p.SetTypeface(tf));

	static Paint With(this Paint p, Action<Paint> a) { a(p); return p; }

	public static void PanelRect(Canvas c, RectF r, float radius, int fill, int line, Paint scratch)
	{
		scratch.SetStyle(Paint.Style.Fill);
		scratch.SetShader(null);
		scratch.Color = new Color(fill);
		c.DrawRoundRect(r, radius, radius, scratch);
		scratch.SetStyle(Paint.Style.Stroke);
		scratch.StrokeWidth = 1;
		scratch.Color = new Color(line);
		c.DrawRoundRect(new RectF(r.Left + 0.5f, r.Top + 0.5f, r.Right - 0.5f, r.Bottom - 0.5f), radius, radius, scratch);
		scratch.SetStyle(Paint.Style.Fill);
	}

	public static void Bar(Canvas c, RectF r, float fraction, int from, int to, Paint scratch)
	{
		var rad = r.Height() / 2;
		scratch.SetShader(null);
		scratch.Color = new Color(Line);
		c.DrawRoundRect(r, rad, rad, scratch);
		fraction = Math.Clamp(fraction, 0, 1);
		if (fraction > 0)
		{
			scratch.SetShader(new LinearGradient(r.Left, 0, r.Right, 0, new Color(from), new Color(to), Shader.TileMode.Clamp!));
			c.DrawRoundRect(new RectF(r.Left, r.Top, r.Left + r.Width() * fraction, r.Bottom), rad, rad, scratch);
			scratch.SetShader(null);
		}
	}

	public static Drawable Pill(int color)
	{
		var d = new GradientDrawable();
		d.SetColor(color);
		d.SetCornerRadius(14);
		return d;
	}
}
