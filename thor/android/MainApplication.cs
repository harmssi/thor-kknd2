using System.Runtime.InteropServices;
using Android.App;
using Android.Runtime;

namespace ThorKKND2;

[Application]
public class MainApplication : Application
{
	delegate int MainFn(int argc, IntPtr argv);

	// kept alive for the lifetime of the process; SDL calls it from its own thread
	static readonly MainFn Entry = (_, _) => GameMain.Run();

	[DllImport("main")]
	static extern void thor_set_main(IntPtr fn);

	public static MainApplication Instance { get; private set; } = null!;

	public MainApplication(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer) { }

	public override void OnCreate()
	{
		base.OnCreate();
		Instance = this;
		thor_set_main(Marshal.GetFunctionPointerForDelegate(Entry));
		RegisterActivityLifecycleCallbacks(new Deck.DeckHost());
	}
}
