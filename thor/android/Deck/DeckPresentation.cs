using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;

namespace ThorKKND2.Deck;

public class DeckPresentation : Presentation
{
	public DeckPresentation(Context outer, Display display) : base(outer, display) { }

	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		Window!.AddFlags(WindowManagerFlags.NotFocusable); // keep controller focus on the game
		SetContentView(new DeckView(Context));
	}
}
