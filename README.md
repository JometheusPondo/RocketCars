# Rocket Cars

Rocket Cars is a free open-source server-authoritative physics-based multiplayer car game, inspired by Rocket League. It’s built with Netick, a powerful free networking library that makes creating complex multiplayer games like Rocket League simple and easy for indie developers.

**Note**: This project is not a Rocket League clone. It’s designed as a simplified learning sample for developers who want to understand how to build physics-based networked games using Netick.

Join our [discord](https://discord.com/invite/uV6bfG66Fx) for any questions.

## Features

- Clean, documented code designed as a learning resource
- Fully server-authoritative simulation (cheating is impossible)
- Fully predicted physics for all cars and the ball
- Custom vehicle physics model, inspired by Rocket League
- Built-in goal replay system
- Full-Match replay (using Netick's built-in full-game replay system)
- Custom field shader

## Code Overview
- `Soccer`: Implements the Soccer game mode and handles overall game management
- `CarController`: Contains the vehicle simulation logic, using a custom lightweight physics model.
- `ReplaySystem` & `Replayable`: Handles server-side goal replay system (recording and playback), using Netick state storing/reseting API.
- `Ball`, `GoalBox`, `Booster`: Core interactive game elements.
- `UI Folder`: Contains all user interface logic, designed to be independent from the gameplay layer for flexibility.

## Networking 
https://netick.net/docs/2/articles/prediction-in-depth.html

In general, and in most games, we only predict the local player character and some environmental objects. However, in Rocket Cars, we predict everything: remote (other) players, and the ball.

To do all of this, we use Netick to handle the networking, as it takes care of all the prediction and replication logic for us. Even at high packet loss (>20%), the game is still going to be smooth and playable.

However, due to latency, it's impossible to properly predict other (remote) players, since we can't predict their inputs. Thus, the client's prediction of other players will often be wrong. To mitigate this problem, we use a feature of `NetworkTransform`/`NetworkRigidbody` called `Prediction Error Correction Smoothing` to smoothly correct the error over multiple frames. Without that, the game would appear jittery because each time a correction comes in from the server, we would reconcile with the server and appear in a vastly different location instantly (prediction error magnitude scales with ping).

Any game where you predict other players will need some sort of smooth error correction.

## Performance
The game is written performantly, and it should run over 300 FPS even on low end PCs.

### PhysX Resimulation Overhead
Because Rocket Cars uses physics prediction, it must resimulate the physics engine (Unity's PhysX) during reconciliation. Unfortunately, PhysX is:

- Not deterministic: leading to potential mispredictions (that shouldn't happen otherwise), especially at high ping.
- Slow during resimulation: problematic for lower-end or mobile devices.

Future improvement suggestion: Use a deterministic and CSP-ready third-party physics engine for better performance and accuracy.

## Who Is This For?
Rocket Cars is not a full-featured game, it's a technical sample for:

- Game developers exploring multiplayer physics-based gameplay.
- Developers looking to understand client-side prediction with Netick.
- Anyone curious how games like Rocket League handle networking and physics.

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