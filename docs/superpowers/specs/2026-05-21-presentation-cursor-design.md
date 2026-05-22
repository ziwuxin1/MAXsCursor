# Presentation Cursor 演示光标模式 — Design

Date: 2026-05-21
Status: Approved, ready for implementation plan

## Goal

Add a new toggleable teaching mode. When enabled, the cursor is shown as an enlarged
high-contrast marker on the overlay and every mouse click produces a coloured ripple,
so viewers of a recorded course can clearly see where the teacher is pointing and what
they click. The mode is independent and can run alongside the existing cursor ring,
keyboard HUD, and zoom features.

This spec covers the enlarged cursor and the click ripple only. Highlighting or framing
the clicked region or selected content is explicitly deferred to a later iteration.

## Decisions (locked during brainstorming)

1. Synthetic large cursor drawn on the overlay (option A). The native system cursor stays
   visible underneath. We do not magnify the OS cursor itself.
2. Independent new mode (option A). Own on/off toggle and own global hotkey. Coexists with
   the existing ring and HUD. Lives in its own module so it can evolve separately.
3. Click feedback is an expanding ripple ring (water ripple) that fades out. Left, middle,
   and right buttons each get a distinct colour.
4. Large cursor shape is a filled circle with a small centre hole (option B). Bold and
   high-contrast, while the hole lets viewers still see the precise target underneath.
5. Default hotkey Alt+F6, mode starts off on launch. Hotkey is rebindable in settings.

## Architecture

The mode is orchestrated by a `PresentationCursorController` that owns two rendering
pieces and is driven by the existing input event stream.

```
HookManager (hook thread)
   -> EventBus (mouse move + button events, already exists)
        -> UI thread drain (existing RenderClock tick)
             -> PresentationCursorController
                   -> BigCursorWindow      (follows cursor, static bitmap)
                   -> ClickRippleController -> ClickRippleWindow x N (animated)
```

### Rendering approach

Both rendering pieces reuse the proven `NativeCursorWindow` pipeline: a layered
(`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TOOLWINDOW`)
popup window backed by a top-down 32bpp premultiplied BGRA DIB, presented with
`UpdateLayeredWindow`.

- **Big cursor**: the bitmap (filled circle with centre hole, plus border for contrast) is
  drawn once when settings change. Each mouse move is a single `SetWindowPos`, no per-frame
  bitmap upload. Same cost profile as the current cursor ring.
- **Click ripple**: each ripple animates radius and alpha over its lifetime, so its bitmap
  is redrawn each frame and re-uploaded via `UpdateLayeredWindow`. Animation is driven by
  the existing `RenderClock` (`CompositionTarget.Rendering`) tick on the UI thread. Concurrent
  ripples are capped (default 4) via a small pool of reusable ripple windows. Each ripple is
  short lived (about 350 to 450 ms).

### Performance notes

- No allocations in hook callbacks. Clicks and moves are enqueued to the existing `EventBus`
  and drained on the UI thread, same as today.
- Big cursor bitmap is built only when appearance settings change, never per frame.
- Ripple bitmaps are small (sized to max ripple diameter). With the concurrency cap the
  per-frame cost stays within the overlay budget. If profiling shows the ripple uploads are
  too expensive at 260 Hz, fall back to a single shared ripple window that draws all active
  ripples into one bitmap.
- Big cursor and ripple windows are hidden (and ripple animation stopped) when the mode is
  off, so there is zero cost when not presenting.

## Components

### New files

- `Overlay/BigCursorWindow.cs` — layered window drawing the filled circle with centre hole
  and contrast border. Mirrors `NativeCursorWindow`. Exposes `SetVisible`, `FollowCursor`,
  and `ApplyAppearance(...)`.
- `Overlay/ClickRippleWindow.cs` — a single animated ripple. Positioned at the click point,
  draws one ring at a given progress (radius and alpha derived from elapsed time).
- `Overlay/ClickRippleController.cs` — owns the pool of ripple windows, spawns a ripple at a
  click point with the colour for that button, advances all active ripples each tick, retires
  finished ones.
- `Overlay/PresentationCursorController.cs` — orchestrates the mode. Holds on/off state,
  shows or hides the big cursor, forwards mouse-move to `BigCursorWindow.FollowCursor`, and
  forwards button-down events to `ClickRippleController`.

### Changed files

- `Settings/SettingsModel.cs` — new persisted appearance and hotkey fields (see below).
  Update `Clone()` and defaults.
- `Settings/SettingsWindow.xaml` / `.xaml.cs` — new section for the mode: big cursor size,
  fill colour, opacity, hole diameter, border width and colour, three click colours, ripple
  max radius, ripple duration, ripple enabled, and the rebindable hotkey.
- `Settings/Strings.cs` — zh and en strings for the new UI.
- `Core/HotkeyManager.cs` — register and route a third global hotkey (presentation toggle).
- `App.xaml.cs` — construct and wire `PresentationCursorController`, route the new hotkey to
  its toggle, feed it the same mouse event stream used by the ring and HUD.

### Settings model additions

Persisted (appearance only, runtime on/off is not persisted):

- `PresentationHotkeyMods` (default MOD_ALT = 0x0001), `PresentationHotkeyVk` (default
  VK_F6 = 0x75)
- `BigCursorSize` (diameter, dip), `BigCursorColor` (RRGGBB hex), `BigCursorOpacity`
  (0.0 to 1.0), `BigCursorHoleSize` (diameter, dip), `BigCursorBorderThickness` (dip),
  `BigCursorBorderColor` (RRGGBB hex)
- `ClickRippleEnabled` (bool), `LeftClickColor`, `MiddleClickColor`, `RightClickColor`
  (RRGGBB hex), `RippleMaxRadius` (dip), `RippleDurationMs` (int)

Sensible high-contrast defaults to be chosen during implementation (for example bright
fill with white border, yellow left, blue right, green middle).

## Toggle behaviour

- New global hotkey (default Alt+F6) flips the mode.
- On enable: show the big cursor at the current cursor position and start accepting click
  ripples. On disable: hide the big cursor and stop the ripple animation, retire any active
  ripples.
- Mode starts off every launch. The on/off state is intentionally not persisted.
- The mode is independent of the master Alt+F5 toggle in concept, but when the master toggle
  turns all effects off the presentation cursor and ripples are hidden too (consistent with
  how the ring and HUD already respond).

## DPI and multi-monitor

- Sizes are stored in dip and converted to physical pixels using the cursor's current monitor
  DPI, same approach as `NativeCursorWindow`.
- Big cursor and ripple windows follow the cursor across monitors via screen-pixel
  positioning, consistent with the existing ring.

## Out of scope (future iterations)

- Highlighting or framing the clicked region.
- Detecting and showing what UI element was clicked or selected.
- Per-monitor independent presentation configuration.

## Testing

Manual only, consistent with project policy. Acceptance items:

- Press Alt+F6, big cursor appears and follows the mouse across monitors at correct DPI.
- The centre hole shows the target underneath, contrast is clearly readable on a bright and a
  dark background.
- Left, middle, and right clicks each produce a ripple in the configured colour that expands
  and fades.
- Rapid clicking does not exceed the concurrency cap or leak ripple windows.
- Press Alt+F6 again, big cursor and ripples disappear with no residual cost (GPU idle).
- Alt+F5 master off also hides the presentation cursor.
- Change colours and sizes in settings, see live update.
- Restart app, appearance settings persisted, mode starts off.
- Overlay frame cost stays within budget at 260 Hz with the mode on and clicking.
