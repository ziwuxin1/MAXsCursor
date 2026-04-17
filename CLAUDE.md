# CLAUDE.md — Working Rules for This Project

This file tells Claude Code how to work on MAXsCursor. Read SPEC.md for product requirements.

## Authoritative Docs
- `SPEC.md` is the single source of truth for what to build. Do not deviate without asking.
- If a feature is not in SPEC.md, ask before building it.

## Stack (fixed, do not change)
- C# 12 / .NET 8 / WPF
- Target: `net8.0-windows`
- No third-party UI frameworks. No MVVM frameworks. Plain code-behind.
- Dependencies allowed: `System.Text.Json` only. Everything else must be justified and approved.

## Performance is a Hard Requirement
- The user records at 144/240/260Hz. Dropped frames are a bug, not a tradeoff.
- Hook callbacks (`WH_MOUSE_LL`, `WH_KEYBOARD_LL`) MUST return in under 1ms.
  - No allocations inside hook callbacks.
  - No `Dispatcher.Invoke` from the hook thread. Enqueue events to a lock-free queue and drain on the UI thread.
- Pre-create and freeze WPF `Brush`, `Pen`, `Geometry` objects. Never create them per frame.
- Avoid WPF `Storyboard` animations for per-frame effects. Use `CompositionTarget.Rendering` tick with manual interpolation.
- Use `DrawingVisual` + `VisualCollection` for custom drawing. Do not use `Canvas` with hundreds of `Ellipse` elements.

## Windows API Etiquette
- Always unregister hooks in a `finally` block AND on `AppDomain.ProcessExit`. Zombie hooks slow down the entire OS.
- Always call `CallNextHookEx` at the end of hook procedures.
- Window must have `WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOPMOST`. Set these via `SetWindowLong` after `SourceInitialized`.
- Set DPI awareness to `PER_MONITOR_AWARE_V2` in `app.manifest` AND as a runtime safety net via `SetProcessDpiAwarenessContext`.

## Coding Style
- Use `file-scoped namespaces` and top-level statements only in `Program.cs`.
- `internal` by default. `public` only when crossing assembly boundaries (there are none in this project).
- `readonly record struct` for event data passed between threads.
- `nullable` enabled in all projects.
- No `async void` except for event handlers.

## Writing Style in Any Output
- **No semicolons in English prose.** Use periods or commas. (This is a user preference that overrides typical writing style.)
- **No em dashes (—) anywhere in English output.** Use commas, colons, or separate sentences.
- Comments can use semicolons if syntactically required, but prose comments should follow the above.

## Work Order
Build in this order. Each step must compile and run before moving to the next.

1. **Skeleton**: project structure, empty overlay window, tray icon, Alt+F5 hotkey toggles window visibility. Verify no focus stealing and click-through works.
2. **Mouse hook + cursor ring**: global mouse hook on a dedicated thread, event queue, render a single static ring around cursor via `DrawingVisual`.
3. **Keyboard hook + HUD**: global keyboard hook, bottom-right HUD showing last 5 keys with modifier grouping.
4. **Settings window + persistence**: JSON settings in `%APPDATA%\MAXsCursor\`, live-apply to overlay.
5. **Multi-monitor**: one overlay window per monitor, cursor hand-off works across monitors.
6. **Polish**: startup, clean shutdown, error handling, acceptance checklist pass.

Do not skip ahead. Do not build v2 stretch features.

## When Stuck
- If a Windows API call doesn't behave as documented, add diagnostic logging to `%TEMP%\MAXsCursor.log` and ask the user to run and share the log.
- If performance is borderline, profile with `dotnet-trace` before optimizing. Do not guess.
- If you need to add a dependency, stop and explain why before adding it.

## Testing
- Manual testing only for v1. No unit tests required (this is a solo tool).
- Before claiming a step is complete, run through the acceptance checklist items relevant to that step.

## Git
- Commit after each work order step completes. Commit messages: `step N: <what>`
- `.gitignore`: standard Visual Studio / Rider ignore (bin, obj, .vs, .idea, *.user)
