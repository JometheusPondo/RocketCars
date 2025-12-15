# Rocket Cars

<img width="2692" height="1571" alt="Promo2" src="https://github.com/user-attachments/assets/36167790-b493-4bbb-89e3-28ec38db5a32" />

Rocket Cars is a free open-source server-authoritative physics-based multiplayer car game, inspired by Rocket League. It’s built with Netick, a powerful free networking library that makes creating complex multiplayer games like Rocket League simple and easy for indie developers.

**Note**: This project is not a Rocket League clone. It’s designed as a simplified learning sample for developers who want to understand how to build physics-based networked games using Netick.

Join our [discord](https://discord.com/invite/uV6bfG66Fx) for any questions.

## Features
- Clean and documented code 
- Fully server-authoritative simulation (cheating is impossible)
- Fully predicted physics for all cars and the ball
- Custom vehicle physics model, inspired by Rocket League
- Built-in goal replay system
- Full-match replay mode (using Netick's built-in full-game replay system)
- Custom field shader

## Controls

Rocket Cars uses Unity’s legacy input system (`Input` API).

### Car

These inputs are collected every network tick and sent to the server as part of the player’s `GameInput`:

- **Steering / Movement**
  - `A / D` – steer left / right (also controls pitch up / down while airborne)
  - `W / S` – accelerate / brake
  - `Q / E` – air roll left / right

- **Actions**
  - `Left Mouse Button` (button) – rocket
  - `Right Mouse Button` (button down) – jump

### Camera

- **Space** – switches between:
  - Car-oriented camera
  - Ball-focused camera

- **Middle Mouse Button** (while grounded and on Car-oriented camera)
  - Temporarily reverses the camera direction

### Replay & Spectator Controls

When the game is in replay mode (`Sandbox.IsReplay == true`), normal gameplay input is disabled and the following controls become available:

- **F** – Toggle replay camera between:
  - Free camera
  - Following a selected player

- **Number Keys (1–9)** – Select a specific player to spectate
  - `1` selects player index 0
  - `2` selects player index 1
  - etc.

If the selected player is no longer valid (e.g. disconnected or not spawned), the replay camera automatically falls back to free mode.

## Testing
* Running a client player: start the game and connect to a server.
* Running a host player: start the game and choose host. Now, with proper port-forwarding, other people can join the game by using your IP address.
* Running a dedicated server: by simply starting the game in batch-mode, it would automatically run the game as a dedicated server.

## Code Overview
- `Soccer`: Implements the Soccer game mode and handles overall game management
- `CarController`: Contains the vehicle simulation logic, using a custom lightweight physics model
- `Ball`, `GoalBox`, `Booster`: Core interactive game elements
- `ReplaySystem` & `Replayable`: Handles server-side goal replay system (recording and playback), using Netick state storing/reseting API
- `UI Folder`: Contains all user interface logic, designed to be independent from the gameplay layer for flexibility

## Networking 
In most games, only the local player character and some environmental objects are predicted. However, in Rocket Cars, we predict everything: the local player car + other players and the ball (remote/proxy objects).

However, due to latency, it's impossible to properly predict other (remote) players, since we can't predict their inputs. Thus, the client's prediction of other players will often be wrong, and it gets much worse at higher pings (>100ms). To mitigate this problem, we use a feature of `NetworkTransform`/`NetworkRigidbody` called `Prediction Error Correction Smoothing` to smoothly correct the error over multiple frames. Without that, the game would appear jittery because each time a correction comes in from the server, we would reconcile with the server and appear in a vastly different location instantly (prediction error magnitude scales with ping).

Read Netick's [Prediction In-Depth article](https://netick.net/docs/2/articles/prediction-in-depth.html) to learn more about Rocket Cars networking.

In addition, [this](https://youtu.be/c373LsgiXBc) is a nice video about Rocket League that describes the same networking-related issues inherent to proxy/remote prediction.

## Performance
The game is written performantly, and it should run over 300 FPS even on low end PCs.

### PhysX Resimulation Overhead 
Because Rocket Cars uses physics prediction, it must resimulate the physics engine (Unity's 3D physics engine PhysX) during reconciliation. Unfortunately, PhysX is:

- Slow during resimulation: problematic for lower-end or mobile devices.
- Not deterministic: leading to potential mispredictions that shouldn't happen otherwise, especially at high ping.

Future improvement suggestion: Use a deterministic and CSP-ready third-party physics engine for better performance and accuracy.

## Credits

Massive thank you to [Steak](https://github.com/stinkysteak) and [Milk-Drinker01](https://github.com/Milk-Drinker01) for being invaluable during the development of this sample. Steak completely cleaned up the project and overhauled the graphical style, and Milk-Drinker01 created a beautiful field shader and fixed the camera jitter issue.

## Third Party Assets

- [ParticlePack](https://assetstore.unity.com/packages/vfx/particles/particle-pack-127325) by Unity Technologies 
- [Low Poly Car Pack 1](https://designersoup.itch.io/low-poly-car-pack-1) by Designer Soup
- [Vehicle - Essentials](https://assetstore.unity.com/packages/audio/sound-fx/transportation/vehicle-essentials-194951) by Nox_Sound (permission was granted from Nox_Sound to include this pack in Rocket Cars)
- [Toon Shader](https://github.com/IronWarrior/UnityToonShader) by Erik Roystan
- [Textures](https://freepbr.com/?s=grass) by [FreePBR](https://freepbr.com/) and [free-3dtextureshd.com](https://free-3dtextureshd.com)
- [Font Electrolize](https://fonts.google.com/specimen/Electrolize?preview.text=PUKIS&categoryFilters=Appearance:%2FTheme%2FTechno;Feeling:%2FExpressive%2FFuturistic) from Google Fonts
- [DOTween](https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676) by Demigiant