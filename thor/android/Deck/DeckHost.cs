using Android.App;
using Android.Content;
using Android.Hardware.Display;
using Android.OS;
using Android.Views;

namespace ThorKKND2.Deck;

/// Shows the deck on the Thor's lower screen while the game activity is in front.
public class DeckHost : Java.Lang.Object, Application.IActivityLifecycleCallbacks
{
	DeckPresentation? deck;

	static bool IsGame(Activity a) => a.Class.SimpleName == "GameActivity";

	public void OnActivityResumed(Activity activity)
	{
		if (!IsGame(activity) || deck?.IsShowing == true)
			return;

		var dm = (DisplayManager)activity.GetSystemService(Context.DisplayService)!;
		var lower = dm.GetDisplays(DisplayManager.DisplayCategoryPresentation)?.FirstOrDefault()
			?? dm.GetDisplays()!.FirstOrDefault(d => d.DisplayId != activity.Display!.DisplayId);
		if (lower == null)
			return;

		deck = new DeckPresentation(activity, lower);
		try { deck.Show(); } catch (WindowManagerBadTokenException) { deck = null; }
	}

	void Close(Activity activity)
	{
		if (!IsGame(activity))
			return;
		deck?.Dismiss();
		deck = null;
	}

	public void OnActivityPaused(Activity activity) { }
	public void OnActivityDestroyed(Activity activity) => Close(activity);
	public void OnActivityCreated(Activity activity, Bundle? savedInstanceState) { }
	public void OnActivitySaveInstanceState(Activity activity, Bundle outState) { }
	public void OnActivityStarted(Activity activity) { }
	public void OnActivityStopped(Activity activity) => Close(activity);
}
