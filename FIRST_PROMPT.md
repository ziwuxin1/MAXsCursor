# FIRST_PROMPT.md

Paste the content below into Claude Code as your first message, then press Enter.

---

I'm building MAXsCursor, a low-latency cursor and keyboard visualization overlay for recording teaching videos at high refresh rates (up to 260Hz) on Windows.

Before writing any code, please:

1. Read `SPEC.md` in full. This is the product specification.
2. Read `CLAUDE.md` in full. These are the working rules for this project.
3. Confirm you understand the stack (.NET 8 + WPF, no third-party UI frameworks), the performance constraints (260Hz, sub-1ms hook callbacks, no per-frame allocations), and the work order in CLAUDE.md.

Then execute Work Order Step 1 only:

> Skeleton: project structure, empty overlay window, tray icon, Alt+F5 hotkey toggles window visibility. Verify no focus stealing and click-through works.

When step 1 is complete, show me a short run-through of how to test it, then stop and wait for me to verify before moving to step 2.

Do not build step 2, step 3, or any v2 features. Do not add dependencies beyond what's in CLAUDE.md.

If anything in SPEC.md or CLAUDE.md is ambiguous, ask me before making assumptions.
