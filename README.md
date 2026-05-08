<!--
  PASTE THIS BLOCK AT THE TOP OF THE EXISTING README.md, ABOVE THE "# Rocket Cars" HEADER.
  The upstream README content stays below as-is.
-->

> ## About this fork
>
> This is a fork of [NetickNetworking/NetickRocketCars](https://github.com/NetickNetworking/NetickRocketCars), maintained by [Joseph Ahrens](https://github.com/JometheusPondo). The upstream sample's README is preserved below.
>
> ### My contributions on this fork
>
> - **Vehicle physics replacement (C++ → C#).** Ported a reverse-engineered Rocket League physics library from C++ into C# and integrated it into the Unity project, replacing the upstream sample's vehicle physics with a model that more closely matches the source game's car-and-ball dynamics.
>   - Source library: RocketSim by ZealanL: https://github.com/ZealanL/RocketSim
>   - Primary affected systems: `CarController`, `Ball`, related physics integration code
> - **Vehicle handling overhaul.** Rebuilt steering response and implemented air-roll (Q/E) controls to match competitive Rocket League feel.
> - **Prototype vehicles on the same physics framework.** Built two offshoot vehicle types reusing the ported physics core:
> - **Networking integration.** Adapted the ported physics to work inside Netick's server-authoritative prediction model, including testing and tuning prediction-error correction smoothing at higher pings.
>
> ---
>
> *Original README from the upstream sample follows.*

# Rocket Cars

<img width="2692" height="467" alt="Promo3" src="https://github.com/user-attachments/assets/25c18252-dc77-46fc-b923-c1b182b92550" />

https://github.com/user-attachments/assets/f5c14de4-470b-4205-a0e4-b706cb5730e0

[Longer video here](https://youtu.be/leNmsjLQ-74)

Rocket Cars is a free open-source server-authoritative physics-based multiplayer car game sample, inspired by Rocket League. It’s made in Unity using [Netick](https://github.com/NetickNetworking/NetickForUnity), a powerful free networking solution that makes creating complex multiplayer games like Rocket League simple and easy for indie developers.

Join our [discord](https://discord.com/invite/uV6bfG66Fx) for support.

**Support Policy**: This is an educational resource. Support is provided for learning purposes only. Asset flips or minimal-effort derivatives will receive no support.

## Features
- Clean and documented code 
- Fully server-authoritative simulation (cheating is impossible)
- Fully predicted physics for all cars and the ball
- Custom vehicle physics model, inspired by Rocket League
- Goal replay system
- Full-match replay mode (using Netick's built-in full-game replay system)
- Custom field shader
- Camera Effects
- In-Game Text Chat

## Controls

Rocket Cars uses Unity’s legacy input system (`Input` API).

### Car Controls

#### **Steering / Movement**
- A / D – steer left / right 
- W / S – accelerate / brake (also controls pitch up / down while airborne)
- Q / E – air roll left / right

#### **Actions**
- Left Mouse Button (button) – rocket
- Right Mouse Button (button down) – jump

### Camera Controls
- Space: switches between car-oriented and ball-oriented camera modes
- Middle Mouse Button (while grounded and on car-oriented camera): reverses the camera direction

### Replay & Spectator Controls

When the game is in replay mode (`Sandbox.IsReplay == true`), normal gameplay input is disabled and the following controls become available:

#### F Key – Toggle replay camera between:
- Free camera
- Following a selected player

#### Number Keys (1–9) – Select a specific player to spectate
- `1` selects player index 0
- `2` selects player index 1
- etc.

If the selected player is no longer valid (e.g. disconnected or not spawned), the replay camera automatically falls back to free mode.

## Testing
- Running a client player: start the game and connect to a server
- Running a host player: start the game and choose host. Now, with proper port-forwarding, other people can join the game by using your IP address
- Running a dedicated server: by simply starting the game in batch-mode, it would automatically run the game as a dedicated server

## Code Overview
- `Soccer`: Implements the Soccer game mode and handles overall game management
- `CarController`: Contains the vehicle simulation logic, using a custom lightweight physics model
- `Ball`, `GoalBox`, `Booster`: Core interactive game elements
- `GoalReplaySystem` & `GoalReplayable`: Handles server-side goal replay system (recording and playback), using Netick state storing/reseting API
- `UI Folder`: Contains all user interface logic, designed to be independent from the gameplay layer

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

### Third-Party Assets

| Asset | Source | License / Terms |
| :--- | :--- | :--- |
| **DOTween** | [Demigiant](https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676) | [Standard EULA](http://dotween.demigiant.com/license.php) |
| **Low Poly Car Pack 1** | [Designer Soup](https://designersoup.itch.io/low-poly-car-pack-1) | Custom |
| **Toon Shader** | [Erik Roystan](https://github.com/IronWarrior/UnityToonShader) | MIT |
| **Textures** | [FreePBR](https://freepbr.com/) & [Free3DTextures](https://free-3dtextureshd.com) | Custom |
| **ParticlePack** | [Unity Technologies](https://assetstore.unity.com/packages/vfx/particles/particle-pack-127325) | [Unity Asset Store EULA](https://unity.com/legal/as-terms) |
| **Game Countdown 62** | [Pixabay](https://pixabay.com/) | [Pixabay License](https://pixabay.com/service/license-summary/) |
| **car-tire-squeal-skid-loop** | [opengameart.org](https://opengameart.org/content/car-tire-squeal-skid-loop) | CC BY 3.0 |
| **Vehicle - Essentials** | [Nox_Sound](https://assetstore.unity.com/packages/audio/sound-fx/transportation/vehicle-essentials-194951) | [Unity Asset Store EULA](https://unity.com/legal/as-terms) (permission granted for this project) |
| **Font Electrolize** | [Google Fonts](https://fonts.google.com/specimen/Electrolize) | Open Font License (OFL) |
