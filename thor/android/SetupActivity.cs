using Path = System.IO.Path;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using ThorKKND2.Deck;

namespace ThorKKND2;

/// Launcher: checks for the owner's game files, imports them from a chosen folder ("Steam-Ordner wählen"), then starts the game.
[Activity(Label = "KKND2 Thor", MainLauncher = true, Exported = true, Theme = "@android:style/Theme.NoTitleBar.Fullscreen",
	ScreenOrientation = Android.Content.PM.ScreenOrientation.SensorLandscape)]
public class SetupActivity : Activity
{
	const int PickFolder = 1;
	TextView? status;
	Button? pick;
	bool busy;

	static bool De => Java.Util.Locale.Default.Language == "de";
	static string T(string en, string de) => De ? de : en;

	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		// launchers like Cocoon may start apps on the lower screen: the game always belongs on the top one (display 0)
		if (Display?.DisplayId is { } id && id != 0 && !Intent!.GetBooleanExtra("moved", false))
		{
			var i = new Intent(Intent).PutExtra("moved", true).AddFlags(ActivityFlags.NewTask);
			StartActivity(i, ActivityOptions.MakeBasic().SetLaunchDisplayId(0).ToBundle());
			Finish();
			return;
		}

		if (Paths.HasContent(this) && !Intent!.GetBooleanExtra("reimport", false))
		{
			StartGame();
			return;
		}

		BuildUi();
	}

	void StartGame()
	{
		StartActivity(new Intent(this, Java.Lang.Class.ForName("de.harmssi.thorkknd2.GameActivity")),
			ActivityOptions.MakeBasic().SetLaunchDisplayId(0).ToBundle());
		Finish();
	}

	void BuildUi()
	{
		Ui.Init(this);
		var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
		root.SetBackgroundColor(new Color(Ui.Bg));
		root.SetPadding(140, 110, 140, 80);

		var label = new TextView(this) { Text = "KKND2 THOR · SETUP" };
		label.SetTextColor(new Color(Ui.Accent));
		label.SetTypeface(Ui.InterSemi, TypefaceStyle.Normal);
		label.LetterSpacing = 0.32f;
		label.TextSize = 13;
		root.AddView(label);

		var title = new TextView(this) { Text = T("Import your game files", "Spieldateien importieren") };
		title.SetTextColor(new Color(Ui.Text));
		title.SetTypeface(Ui.InterBold, TypefaceStyle.Normal);
		title.TextSize = 34;
		title.SetPadding(0, 18, 0, 18);
		root.AddView(title);

		var body = new TextView(this)
		{
			Text = T(
				"KKND2 Thor needs the original files of your own copy of Krush Kill 'N Destroy 2: Krossfire (Steam). " +
				"Pick the game folder (it contains the folder \"levels\"). Only the levels, the music and two intro videos are copied (about 560 MB); " +
				"nothing leaves the device.",
				"KKND2 Thor braucht die Original-Dateien deiner eigenen Kopie von Krush Kill 'N Destroy 2: Krossfire (Steam). " +
				"Wähle den Spielordner (darin liegt der Ordner „levels“). Kopiert werden nur die Levels, die Musik und zwei Intro-Videos (ca. 560 MB); " +
				"nichts verlässt das Gerät."),
		};
		body.SetTextColor(new Color(Ui.Text2));
		body.SetTypeface(Ui.Inter, TypefaceStyle.Normal);
		body.TextSize = 17;
		body.SetLineSpacing(0, 1.25f);
		root.AddView(body);

		pick = new Button(this) { Text = T("CHOOSE STEAM FOLDER", "STEAM-ORDNER WÄHLEN") };
		pick.SetTextColor(new Color(Ui.Bg));
		pick.SetTypeface(Ui.InterBold, TypefaceStyle.Normal);
		pick.LetterSpacing = 0.12f;
		pick.Background = Ui.Pill(Ui.Accent);
		pick.SetPadding(60, 26, 60, 26);
		var lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { TopMargin = 44 };
		pick.Click += (_, _) => Pick();
		root.AddView(pick, lp);

		status = new TextView(this);
		status.SetTextColor(new Color(Ui.Text3));
		status.SetTypeface(Ui.Mono, TypefaceStyle.Normal);
		status.TextSize = 14;
		status.SetPadding(0, 30, 0, 0);
		root.AddView(status);

		var hint = new TextView(this)
		{
			Text = T("Unofficial fan port based on OpenRA and OpenKrush (GPLv3). Not affiliated with the rights holders.",
				"Inoffizieller Fan-Port auf Basis von OpenRA und OpenKrush (GPLv3). Keine Verbindung zu den Rechteinhabern."),
		};
		hint.SetTextColor(new Color(Ui.Text3));
		hint.TextSize = 12;
		hint.SetPadding(0, 40, 0, 0);
		root.AddView(hint);

		SetContentView(root);
		pick.RequestFocus();
	}

	void Pick()
	{
		if (busy)
			return;

		var i = new Intent(Intent.ActionOpenDocumentTree);
		i.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
		StartActivityForResult(i, PickFolder);
	}

	protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
	{
		base.OnActivityResult(requestCode, resultCode, data);
		if (requestCode != PickFolder || resultCode != Result.Ok || data?.Data == null)
			return;

		var tree = data.Data;
		busy = true;
		pick!.Enabled = false;
		new Thread(() => Import(tree)).Start();
	}

	void Say(string s) => RunOnUiThread(() => status!.Text = s);

	void Import(Android.Net.Uri tree)
	{
		try
		{
			var copier = new SafCopier(ContentResolver!, tree);
			var root = copier.Root();
			var levels = copier.Child(root, "levels") ?? (copier.Name(root).Equals("levels", StringComparison.OrdinalIgnoreCase) ? root : null);
			if (levels == null)
				throw new InvalidOperationException(T("No \"levels\" folder found here. Pick the KKND2 game folder.",
					"Hier gibt es keinen Ordner „levels“. Bitte den KKND2-Spielordner wählen."));

			if (copier.Child(levels, "sprite.lpk") == null)
				throw new InvalidOperationException(T("levels/sprite.lpk is missing – is this KKND2 Krossfire?",
					"levels/sprite.lpk fehlt – ist das KKND2 Krossfire?"));

			var content = Paths.Content(this);
			var copied = 0;
			copier.CopyTree(levels, Path.Combine(content, "levels"), name => !name.Equals("custom", StringComparison.OrdinalIgnoreCase),
				name => Say(T($"Copying {name} … ({++copied})", $"Kopiere {name} … ({++copied})")));

			if (copier.Child(root, "music") is { } music)
				copier.CopyTree(music, Path.Combine(content, "music"), _ => true,
					name => Say(T($"Copying {name} … ({++copied})", $"Kopiere {name} … ({++copied})")));

			var fmv = copier.Child(root, "fmv");
			if (fmv != null)
				foreach (var v in new[] { "mh.vbc", "intro.vbc" })
				{
					if (copier.Child(fmv, v) is { } f)
						copier.CopyFile(f, Path.Combine(content, "fmv", v));
					if (copier.Child(fmv, "en") is { } en && copier.Child(en, v) is { } a) // sound track of the video
						copier.CopyFile(a, Path.Combine(content, "fmv", "en", v));
				}

			Say(T("Building maps …", "Erzeuge Karten …"));
			var maps = Paths.ImportedMaps(this);
			if (Directory.Exists(maps))
				Directory.Delete(maps, true);
			var n = OpenRA.Mods.OpenKrush.Thor.Gen2MapImporter.ImportAll(content, maps);
			if (n == 0)
				throw new InvalidOperationException(T("No multiplayer maps found in levels/multi.", "Keine Mehrspieler-Karten in levels/multi gefunden."));

			Say(T($"Done: {copied} files, {n} maps.", $"Fertig: {copied} Dateien, {n} Karten."));
			RunOnUiThread(StartGame);
		}
		catch (Exception e)
		{
			Android.Util.Log.Error(GameMain.Tag, e.ToString());
			Say(e.Message);
			RunOnUiThread(() => { busy = false; pick!.Enabled = true; });
		}
	}
}

/// Minimal Storage Access Framework tree walker (no AndroidX).
sealed class SafCopier
{
	readonly ContentResolver cr;
	readonly Android.Net.Uri tree;
	public SafCopier(ContentResolver cr, Android.Net.Uri tree) { this.cr = cr; this.tree = tree; }

	public string Root() => DocumentsContract.GetTreeDocumentId(tree)!;

	public string Name(string docId)
	{
		using var c = cr.Query(DocumentsContract.BuildDocumentUriUsingTree(tree, docId)!, new[] { DocumentsContract.Document.ColumnDisplayName }, null, null, null);
		return c != null && c.MoveToFirst() ? c.GetString(0) ?? "" : "";
	}

	public IEnumerable<(string Id, string Name, bool Dir)> Children(string docId)
	{
		var list = new List<(string, string, bool)>();
		using var c = cr.Query(DocumentsContract.BuildChildDocumentsUriUsingTree(tree, docId)!,
			new[] { DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName, DocumentsContract.Document.ColumnMimeType },
			null, null, null);
		while (c != null && c.MoveToNext())
			list.Add((c.GetString(0)!, c.GetString(1)!, c.GetString(2) == DocumentsContract.Document.MimeTypeDir));
		return list;
	}

	public string? Child(string docId, string name) =>
		Children(docId).Where(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(c => c.Id).FirstOrDefault();

	public void CopyFile(string docId, string dest)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
		using var input = cr.OpenInputStream(DocumentsContract.BuildDocumentUriUsingTree(tree, docId)!)!;
		var tmp = dest + ".part";
		using (var output = File.Create(tmp))
			input.CopyTo(output);
		File.Move(tmp, dest, true);
	}

	/// Copies a folder recursively; file names are stored lower case (the engine looks them up that way).
	public void CopyTree(string docId, string dest, Func<string, bool> includeDir, Action<string> progress)
	{
		foreach (var (id, name, dir) in Children(docId))
		{
			var target = Path.Combine(dest, name.ToLowerInvariant());
			if (dir)
			{
				if (includeDir(name))
					CopyTree(id, target, includeDir, progress);
			}
			else
			{
				progress(name);
				CopyFile(id, target);
			}
		}
	}
}
