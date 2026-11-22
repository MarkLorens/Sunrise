# Future considerations (kean's notes)

> **For agents:** Do **not** read or use this file for development, design decisions, or context unless kean explicitly asks you to. It is a personal scratchpad for later review — not project baseline.

---

## Propane valves

- There are **two** propane tanks (hot dog stand + locker room). Each can be opened once.
- Opening a valve is **not** undone by staff. Unlike the drinking fountain (Mechanic fixes it), propane stays hissing until spent (boom / fire), then it's empty.
- Ruckus valve thoughts (`ThinkValve` randomized pool) fire **once per tank open** → at most twice in a full run if both tanks are used.
- Possible later: staff re-tighten valves, mid-run resets, or separate lines per tank.

---

## Dialogue / copy

- **Tank fan aim (M):** First propane tank-fan aim always says `"Who put a fan next to propane? Legend."`; later propane aims use a randomized pool. Gutter stink fan skips the Legend line and only uses the pool.
- Pattern name idea: **first + randomized pool** (one fixed line on first trigger of a state, then pool).

- **Wheel rolled (V) — undecided.** Still in code as current; revisit later.
  - Current (in game): `"Fare-thee-wheel... Hehehe"`
  - Candidate: `"I'm not even laughing. Okay I am. HAHHAHAHA"`
  - Candidate: `"That's what I call Final Destination!"`
  - Note: climax beat; farewell-pun / generic-mean batches felt weak — try other approaches (callback, quiet, revenge-at-park, craftsman, etc.).

---

## Gameplay / systems

- After the gas cloud reaches the hot dog stand successfully, the chaos event should start **immediately** (don't wait on a later spark / extra beat).

---

## Polish / UX

- After **all asset imports are done**, open the lighting debugger in Play mode (**F1**) and play with the moon and lamp colours. Use **Copy values** and paste the block back when a look should become the default.

---

## Misc

_(add notes as they come up)_
