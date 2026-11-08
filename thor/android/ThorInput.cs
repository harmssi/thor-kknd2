using OpenRA.Mods.OpenKrush.Thor;
using SDL2;
using ThorKKND2.Deck;

namespace ThorKKND2;

/// Controller support (OpenRA itself has none): runs every rendered frame on the game thread (Game.FrameHook)
/// and feeds normal SDL mouse/keyboard events, so everything goes through the engine's regular input path.
/// Left stick = cursor, right stick = scroll, A = left click (or build the focused deck card), B = right click,
/// X = stop, Y = attack-move, L3 = deploy, L1/R1 = deck focus, L2/R2 = deck tab, D-pad = groups 1-4, Select = Esc, Start = Enter.
public static class ThorInput
{
	public const SDL.SDL_Keycode KeyEscape = SDL.SDL_Keycode.SDLK_ESCAPE;

	/// Deck toggles, applied as held modifiers: Shift = queue (waypoints), Alt = force move.
	public static volatile bool ForceMove;

	static IntPtr pad, window;
	static bool init;
	static float cx = 960, cy = 540;
	static long lastTicks, lastScan;
	static readonly Dictionary<SDL.SDL_GameControllerButton, bool> held = new();
	static bool l2, r2;
	static readonly System.Collections.Concurrent.ConcurrentQueue<SDL.SDL_Keycode> keyQueue = new();
	static readonly bool[] arrows = new bool[4];

	public static void PressKey(SDL.SDL_Keycode key) => keyQueue.Enqueue(key);

	public static void Install() => OpenRA.Game.FrameHook = Frame;

	static void Frame()
	{
		ThorDeckBridge.Pump();

		// OpenRA renders on its own thread, so the GL context is not current here: find the (only) SDL window instead
		if (window == IntPtr.Zero)
			for (uint id = 1; id < 8 && window == IntPtr.Zero; id++)
				window = SDL.SDL_GetWindowFromID(id);
		if (window == IntPtr.Zero)
			return;

		var mods = SDL.SDL_Keymod.KMOD_NONE;
		if (ThorDeckBridge.WaypointMode) mods |= SDL.SDL_Keymod.KMOD_LSHIFT;
		if (ForceMove) mods |= SDL.SDL_Keymod.KMOD_LALT;
		SDL.SDL_SetModState(mods);

		while (keyQueue.TryDequeue(out var k))
		{
			Key(window, k, true);
			Key(window, k, false);
		}

		Touch(window);

		if (!init)
		{
			init = true;
			SDL.SDL_InitSubSystem(SDL.SDL_INIT_GAMECONTROLLER);
		}

		if (pad == IntPtr.Zero || SDL.SDL_GameControllerGetAttached(pad) == SDL.SDL_bool.SDL_FALSE)
		{
			pad = IntPtr.Zero;
			for (var i = 0; i < SDL.SDL_NumJoysticks(); i++)
				if (SDL.SDL_IsGameController(i) == SDL.SDL_bool.SDL_TRUE)
				{
					pad = SDL.SDL_GameControllerOpen(i);
					Android.Util.Log.Info(GameMain.Tag, $"input: controller {SDL.SDL_GameControllerName(pad)}");
					break;
				}

			if (pad == IntPtr.Zero)
			{
				if (Environment.TickCount64 - lastScan > 5000)
				{
					lastScan = Environment.TickCount64;
					var n = SDL.SDL_NumJoysticks();
					var names = string.Join(", ", Enumerable.Range(0, n).Select(i => $"{SDL.SDL_JoystickNameForIndex(i)} gc={SDL.SDL_IsGameController(i)}"));
					Android.Util.Log.Info(GameMain.Tag, $"input: {n} joysticks: {names}");
				}

				return;
			}
		}

		var now = Environment.TickCount64;
		var dt = Math.Clamp((now - lastTicks) / 1000f, 0, 0.05f);
		lastTicks = now;

		SDL.SDL_GetWindowSize(window, out var ww, out var wh);
		float Axis(SDL.SDL_GameControllerAxis a)
		{
			var v = SDL.SDL_GameControllerGetAxis(pad, a) / 32767f;
			return Math.Abs(v) < 0.18f ? 0 : (v - Math.Sign(v) * 0.18f) / 0.82f;
		}

		// cursor (quadratic response for precision)
		float lx = Axis(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX), ly = Axis(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);
		if (lx != 0 || ly != 0)
		{
			var speed = 1500f * dt;
			cx = Math.Clamp(cx + lx * Math.Abs(lx) * speed, 0, ww - 1);
			cy = Math.Clamp(cy + ly * Math.Abs(ly) * speed, 0, wh - 1);
			Motion(window, (int)cx, (int)cy);
		}

		// right stick -> arrow keys (engine keyboard scrolling)
		float rx = Axis(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX), ry = Axis(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);
		if (ThorDeckBridge.Renderer is { } wr)
		{
			if (rx != 0 || ry != 0)
				wr.Viewport.Scroll(new OpenRA.float2(rx * Math.Abs(rx), ry * Math.Abs(ry)) * (1800f * dt), false);
		}
		else
		{
			Arrow(window, 0, SDL.SDL_Keycode.SDLK_LEFT, rx < -0.3f);
			Arrow(window, 1, SDL.SDL_Keycode.SDLK_RIGHT, rx > 0.3f);
			Arrow(window, 2, SDL.SDL_Keycode.SDLK_UP, ry < -0.3f);
			Arrow(window, 3, SDL.SDL_Keycode.SDLK_DOWN, ry > 0.3f);
		}

		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A, down =>
		{
			if (down && DeckView.ActivateFocus())
				return;
			Click(window, SDL.SDL_BUTTON_LEFT, down);
		}, true);
		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B, down => Click(window, SDL.SDL_BUTTON_RIGHT, down), true);
		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X, down => { if (down) ThorDeckBridge.Stop(); });
		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_Y, down => { if (down) ThorDeckBridge.SetMode("attack"); });
		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK, down => { if (down) ThorDeckBridge.Deploy(); });
		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER, down => { if (down) DeckView.MoveFocus(-1); });
		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER, down => { if (down) DeckView.MoveFocus(1); });
		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK, down => Key(window, SDL.SDL_Keycode.SDLK_ESCAPE, down), true);
		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START, down => Key(window, SDL.SDL_Keycode.SDLK_RETURN, down), true);
		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP, down => { if (down) ThorDeckBridge.Group(0, false); });
		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT, down => { if (down) ThorDeckBridge.Group(1, false); });
		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN, down => { if (down) ThorDeckBridge.Group(2, false); });
		Button(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT, down => { if (down) ThorDeckBridge.Group(3, false); });

		// The Thor reports L2/R2 as digital buttons (raw joystick buttons 15/16), not as the mapped trigger axes
		var joy = SDL.SDL_GameControllerGetJoystick(pad);
		var lt = SDL.SDL_GameControllerGetAxis(pad, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT) > 16000 || SDL.SDL_JoystickGetButton(joy, 15) != 0;
		var rt = SDL.SDL_GameControllerGetAxis(pad, SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT) > 16000 || SDL.SDL_JoystickGetButton(joy, 16) != 0;
		if (lt && !l2) { DeckView.Tab = (DeckView.Tab + 3) % 4; DeckView.Producer = 0; DeckView.ScrollRow = 0; }
		if (rt && !r2) { DeckView.Tab = (DeckView.Tab + 1) % 4; DeckView.Producer = 0; DeckView.ScrollRow = 0; }
		l2 = lt;
		r2 = rt;
	}

	// ---- touch on the top screen (SDL's own touch->mouse emulation is off, see GameMain) ----
	// 1 finger: left button (down after a short delay, so a second finger can turn it into a gesture),
	// 2 fingers: drag = scroll the map, short tap = right click at the midpoint.
	static int touchState; // 0 idle, 1 pending, 2 left held, 3 two-finger gesture
	static long touchStart;
	static float tx, ty, gx, gy, gStartX, gStartY;

	static void Touch(IntPtr window)
	{
		if (SDL.SDL_GetNumTouchDevices() == 0)
			return;

		var dev = SDL.SDL_GetTouchDevice(0);
		var n = SDL.SDL_GetNumTouchFingers(dev);
		SDL.SDL_GetWindowSize(window, out var ww, out var wh);
		var now = Environment.TickCount64;

		(float X, float Y) Finger(int i)
		{
			var f = System.Runtime.InteropServices.Marshal.PtrToStructure<SDL.SDL_Finger>(SDL.SDL_GetTouchFinger(dev, i));
			return (f.x * ww, f.y * wh);
		}

		if (n >= 2)
		{
			var (ax, ay) = Finger(0);
			var (bx, by) = Finger(1);
			float mx = (ax + bx) / 2, my = (ay + by) / 2;
			if (touchState == 2)
				Click(window, SDL.SDL_BUTTON_LEFT, false, tx, ty); // finish what the first finger started
			if (touchState != 3)
			{
				touchState = 3;
				gStartX = gx = mx;
				gStartY = gy = my;
				touchStart = now;
			}
			else
			{
				ThorDeckBridge.Renderer?.Viewport.Scroll(new OpenRA.float2(gx - mx, gy - my), false);
				gx = mx;
				gy = my;
			}

			return;
		}

		if (n == 1)
		{
			var (fx, fy) = Finger(0);
			switch (touchState)
			{
				case 0:
					touchState = 1;
					touchStart = now;
					tx = fx;
					ty = fy;
					cx = fx;
					cy = fy;
					Motion(window, (int)fx, (int)fy);
					break;
				case 1 when now - touchStart > 70 || Math.Abs(fx - tx) + Math.Abs(fy - ty) > 24:
					Click(window, SDL.SDL_BUTTON_LEFT, true, tx, ty);
					touchState = 2;
					goto case 2;
				case 2:
					if (Math.Abs(fx - tx) + Math.Abs(fy - ty) > 0.5f)
					{
						tx = cx = fx;
						ty = cy = fy;
						Motion(window, (int)fx, (int)fy);
					}

					break;
			}

			return;
		}

		// no finger
		switch (touchState)
		{
			case 1:
				Click(window, SDL.SDL_BUTTON_LEFT, true, tx, ty);
				Click(window, SDL.SDL_BUTTON_LEFT, false, tx, ty);
				break;
			case 2:
				Click(window, SDL.SDL_BUTTON_LEFT, false, tx, ty);
				break;
			case 3 when now - touchStart < 350 && Math.Abs(gx - gStartX) + Math.Abs(gy - gStartY) < 30:
				Motion(window, (int)gx, (int)gy);
				Click(window, SDL.SDL_BUTTON_RIGHT, true, gx, gy);
				Click(window, SDL.SDL_BUTTON_RIGHT, false, gx, gy);
				break;
		}

		touchState = 0;
	}

	static void Button(SDL.SDL_GameControllerButton b, Action<bool> onChange, bool sendUp = false)
	{
		var down = SDL.SDL_GameControllerGetButton(pad, b) != 0;
		held.TryGetValue(b, out var was);
		if (down != was)
		{
			held[b] = down;
			if (down || sendUp)
				onChange(down);
		}
	}

	static void Arrow(IntPtr window, int i, SDL.SDL_Keycode key, bool down)
	{
		if (arrows[i] == down)
			return;
		arrows[i] = down;
		Key(window, key, down);
	}

	static void Motion(IntPtr window, int x, int y)
	{
		var e = new SDL.SDL_Event();
		e.type = SDL.SDL_EventType.SDL_MOUSEMOTION;
		e.motion.windowID = SDL.SDL_GetWindowID(window);
		e.motion.x = x;
		e.motion.y = y;
		SDL.SDL_PushEvent(ref e);
		SDL.SDL_WarpMouseInWindow(window, x, y);
	}

	static void Click(IntPtr window, uint button, bool down) => Click(window, button, down, cx, cy);

	static void Click(IntPtr window, uint button, bool down, float x, float y)
	{
		var e = new SDL.SDL_Event();
		e.type = down ? SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN : SDL.SDL_EventType.SDL_MOUSEBUTTONUP;
		e.button.windowID = SDL.SDL_GetWindowID(window);
		e.button.button = (byte)button;
		e.button.state = (byte)(down ? 1 : 0);
		e.button.clicks = 1;
		e.button.x = (int)x;
		e.button.y = (int)y;
		SDL.SDL_PushEvent(ref e);
	}

	static void Key(IntPtr window, SDL.SDL_Keycode key, bool down)
	{
		var e = new SDL.SDL_Event();
		e.type = down ? SDL.SDL_EventType.SDL_KEYDOWN : SDL.SDL_EventType.SDL_KEYUP;
		e.key.windowID = SDL.SDL_GetWindowID(window);
		e.key.state = (byte)(down ? 1 : 0);
		e.key.keysym.sym = key;
		e.key.keysym.scancode = SDL.SDL_GetScancodeFromKey(key);
		SDL.SDL_PushEvent(ref e);
	}
}
