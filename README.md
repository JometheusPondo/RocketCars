# 🚗 Rocket Cars

<video src="video.mp4" controls width="600"></video>

Rocket Cars is a free, open-source, server-authoritative, physics-based multiplayer car game, inspired by Rocket League. It’s built on Netick, a powerful, free networking library that makes creating complex multiplayer games like Rocket League approachable for indie developers.

⚠️ **Note**: This project is not a Rocket League clone, nor is it a polished commercial game. It’s designed as a simplified learning sample for developers who want to understand how to build physics-based networked games using Netick.

## 🚀 Features

- ⚙️ Full server-authoritative simulation — cheating is impossible.
- 🏎️ Fully predicted physics for all cars and the ball.
- 🚗 Custom vehicle model, inspired by Rocket League.
- 📹 Built-in mid-game replay system.
- 🧠 Clean, documented code designed as a learning resource.

## 🧩 Code Overview
- `GameMode`: Central game manager. Handles car pooling, player management, and game mode selection (e.g., Soccer mode).
- `CarController`: Contains the vehicle simulation logic, using a custom lightweight physics model.
- `ReplaySystem` & `Replayable`: Handles server-side replay recording and playback, using Netick state storing/reseting API.
- `Ball`, `GoalBox`, `Booster`: Core interactive game elements with straightforward behaviors.
- UI Folder: Contains all user interface logic, designed to be independent from the gameplay layer for flexibility.

## 🌐 Networking 
https://netick.net/docs/2/articles/prediction-in-depth.html

In general, and in most games, we only predict the local player character and some environmental objects. However, in Rocket Cars, we predict everything: remote (other) players, and the ball.

To manage all of this, we use Netick to handle the networking, as it takes care of all the prediction and replication logic for us.

However, due to latency, it's impossible to properly predict other (remote) players, since we can't predict their inputs. Thus, the client's prediction of other players will almost always be wrong. To mitigate this problem, we use a feature of NetworkTransform/NetworkRigidbody called Prediction Error Correction Smoothing to smoothly correct the error over multiple frames. Without this smoothing, the game would appear very jittery because each time a correction comes in from the server, we would reconcile with the server and appear in a vastly different location instantly (prediction error magnitude scales with ping).

Any game where you predict other players will need some sort of smooth error correction.

## 📈 Performance
The game is written performantly - there is no per-frame garbage created in the scripts of the game. 

### PhysX Resimulation Overhead
Because Rocket Cars uses physics prediction, it must resimulate the physics engine (Unity's PhysX) during reconciliation. Unfortunately, PhysX is:

- Not deterministic — leading to occasional sync issues, especially under high ping.
- Slow on resimulation — problematic for lower-end or mobile devices.

💡 Future improvement suggestion: Use a deterministic, high-performance third-party physics engine for better performance and accuracy.

## 🧠 Who Is This For?
Rocket Cars is not a full-featured game—it's a carefully designed technical sample for:

- Game developers exploring multiplayer physics-based gameplay.
- Developers looking to understand server-authoritative prediction with Netick.
- Anyone curious how games like Rocket League handle networking and physics.

## 🎨 Third Party Assets

- [ParticlePack](https://assetstore.unity.com/packages/vfx/particles/particle-pack-127325) by Unity Technologies 
- [Low Poly Car Pack 1](https://designersoup.itch.io/low-poly-car-pack-1) by Designer Soup
- [Vehicle - Essentials](https://assetstore.unity.com/packages/audio/sound-fx/transportation/vehicle-essentials-194951) by Nox_Sound - permission was granted from Nox_Sound to include this pack in Rocket Cars.