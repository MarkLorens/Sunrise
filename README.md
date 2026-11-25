# Woohoo Land

_Working title: Tumbal Proyek — Sacrificial Ride_

A darkly whimsical isometric sandbox-puzzle game about a dead prankster getting even.

> Built in Unity 6 (URP) with FMOD. PC. One dense park section, one night, one escalating scheme.

## The story so far

Woohoo Land was built to be a place of endless family happiness. **Ruckus** helped build it. He was a construction
worker and a born prankster, the kind who spent every shift making his coworkers laugh with stunts far more elaborate
than they needed to be.

When the last bolt went in, the people behind Woohoo Land made a quiet choice. In Indonesia, they say a big project
needs a **_tumbal proyek_**, a sacrifice to keep it standing. They picked Ruckus.

Now he's back as an evil spirit, drifting through the park he built. He's small, he's weak, and he can't lift a
finger on his own. But touch a living person and he can slip inside them and take over their body, their job and
whatever's in their pockets. Death hasn't made Ruckus any less mischievous. It's just given him new ways to cause
trouble.

Ruckus wants revenge, and he means to get it the only way he knows: one ridiculous prank at a time, each bigger than
the last, until the whole park comes down.

Behind the bright signs, rounded rides and smiling staff, something about Woohoo Land has always been a little off.
Tonight, it's going to show.

---

## How it plays

**Possession is the main verb.** As a spirit you float anywhere but can't touch anything. Touch someone and press
**Possess** to take over their body: you walk where they can walk, use whatever is in their hands, and get through the
doors their job opens. Leave whenever you like. You can't jump back into the same person for 10 seconds.

| Who                  | What they're good for                                                                        |
| -------------------- | -------------------------------------------------------------------------------------------- |
| Visitors             | Hands for any job. Pressing buttons, turning fans, dipping cables.                           |
| The Electrician      | Carries the keys to the electrical boxes.                                                    |
| The Ride Technician  | Carries the screwdriver. The only one who can open the wheel deck's gate and work the hinge. |
| The Ride Operator    | The only one whose fingerprint opens the control booth.                                      |
| The Hot Dog Man      | Guards his propane fan. Can only be possessed once he's out from behind his stand.           |
| The Mascot           | Can't be possessed (he's all costume). Guards the locker room fans.                          |
| Construction Workers | The clean-up crew. They tape off scenes, carry away bodies and mop up puddles.               |

**The park reacts.** Visitors wander, queue for rides, buy snacks and pose with the mascot. They stare at anyone acting
possessed and scream and run from anything terrible. An announcer tries to keep everyone calm, a crowd gathers at the
info booth to complain, and the whole park gets more on edge with every disaster until, by the finale, nobody is calm.

**Objectives, not instructions.** The top-left list names the goal of each part. _How_ you get there is up to the
park's rules: observe routines, find who you need, and chain cause into effect.

---

## Controls

Rebind in **Options > Game Control**. Two presets come built in, and you can rebind single keys.

| Action               | Control A | Control B  |
| -------------------- | --------- | ---------- |
| Move                 | W A S D   | Arrow keys |
| Possess / leave body | J         | Z          |
| Interact             | K         | X          |
| Objectives           | Tab       | Tab        |
| Skip / next          | Space     | Space      |
| Pause                | Esc       | Esc        |

Menus also work with the mouse and a gamepad.

## Features

- A fully hand-drawn art. From illustration to all of the UI bits.
- Save slots with autosave on every objective, a picture of the park and your current objective on each card.
- Options: closed captions for every sound, window mode, resolution, VSync, anti-aliasing, depth of field, screen shake
  strength, "moody" night lighting, and master, music and SFX volume.
- Night-time park lighting with lamp pools, lit stalls and chasing bulbs on the wheel, plus a glow on whoever you
  control so you never lose yourself.

---

## Running the project

1. Install **Unity 6000.6.3f1** (Unity 6.6) with Unity Hub.
2. Clone the repo and open the folder in Unity Hub.
3. Open `Assets/Scenes/MainMenu` and press Play. The scenes are `MainMenu` (build index 0) and `FerrisWheelLevel` (1).

Audio is an FMOD Studio project in `FMOD/sunrise-audio-system`. Unity uses the built banks, so you only need FMOD Studio
to edit sounds.

### Code layout

All gameplay scripts are in `Assets/Scripts`. Good places to start:

| File                            | What it does                                                                         |
| ------------------------------- | ------------------------------------------------------------------------------------ |
| `Possession.cs`                 | The spirit: floating, possessing, leaving bodies.                                    |
| `Mischief.cs`                   | The objective list and steps. The world's own rules decide the order, not this file. |
| `Visitor.cs`, `StaffWorker.cs`  | Crowd and staff behaviour.                                                           |
| `FerrisWheel.cs`                | The ride, from power cut to wobble to runaway finale.                                |
| `GameText.cs`, `Captions.cs`    | All player-facing text and captions, kept in one place for translation later.        |
| `KeyBindings.cs`, `Controls.cs` | Input. Gameplay reads keys only through these.                                       |
| `SaveGame.cs`                   | Save slots.                                                                          |

---

## Team

| Name           | Role                  |
| -------------- | --------------------- |
| Kennard Mahib  | Game / Level Designer |
| Amanda Dorotea | Game / UI Designer    |
| Danu Raditya   | Artist, Illustrator   |
| Anthony Martin | Sound Designer        |
| Aulia Agustina | Programmer            |
| Mark Pardede   | Programmer            |

Special thanks to **Ruanth Thijssen**, our mentor, supporter, and coffee dispenser.
