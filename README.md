# Boat Attack × Testing Floor

A playable reference for integrating [Testing Floor](https://testingfloor.com) into a Unity game, based on [Unity’s Boat Attack](https://github.com/Unity-Technologies/BoatAttack).

Follow a race from gameplay events to spatial analytics and the corresponding recording. The example covers movement sampling, shared game context, checkpoints, lap times, collisions, resets, and an overhead map of the island.

## Play the game

Clone with Git LFS so Unity can load the game’s art and other assets:

```sh
git clone --branch testing-floor https://github.com/capitalisminc/testingfloor-demo-boatattack.git
cd testingfloor-demo-boatattack
git lfs pull
```

Open the project in **Unity 6000.3.7f1**, choose **Tools → Testing Floor → Boat Attack → Open demo**, and press **Play**. Use **W/S** or **↑/↓** for throttle, **A/D** or **←/→** to steer, **R** to reset, and **F** to pause.

## Send telemetry to your game

To explore the data in your own Testing Floor game:

1. Copy [TestingFloor.local.example.json](TestingFloor.local.example.json) to `TestingFloor.local.json` and fill in your game’s telemetry `writeKey`.
2. Choose **Tools → Testing Floor → Boat Attack → Configure from local settings**.
3. Play a race, then open your game’s analytics in Testing Floor.

The local settings file is already ignored by Git. The SDK is included in the project; you can also jump straight into the code to see how the integration works.

## Explore the integration

| Start here | What it demonstrates |
| --- | --- |
| [BoatTelemetry.cs](Assets/TestingFloorDemo/BoatTelemetry.cs) | Track gameplay events and attach race state, player position, and camera context |
| [DemoTelemetry.cs](Assets/TestingFloorDemo/DemoTelemetry.cs) | Configure the SDK and tune movement sampling for a fast-moving boat |
| [Boat.cs](Assets/Scripts/Boat/Boat.cs) | Connect telemetry to existing checkpoint, lap, and reset logic |
| [DemoTools.cs](Assets/TestingFloorDemo/Editor/DemoTools.cs) | Export an overhead image with the world bounds needed to align analytics |
| [testingfloor-telemetry-schema.json](testingfloor-telemetry-schema.json) | Give events readable names and map gameplay properties to analytics |

The [integration guide](docs/testingfloor-demo.md) walks through these patterns and how to adapt them to your game.

## Things to investigate

- Find the corners with the most collisions, then inspect the recordings to see what happened.
- Compare boat speed around the island using a heatmap of movement events.
- Look at missed checkpoints, rejected laps, and resets to find where players lose their way.

## About Boat Attack

Boat Attack is Unity’s boat-racing sample for the Universal Render Pipeline, with an island course, buoyancy, water effects, and AI opponents. See the [original project](https://github.com/Unity-Technologies/BoatAttack) for its rendering features and history.

Original game credits:

- [Andre McGrail](http://www.andremcgrail.com) — design, programming, modeling, textures, SFX
- [Alex Best](https://big_ally.artstation.com) — modeling, textures
- [Stintah](https://soundcloud.com/stintah) — soundtrack
- [Felipe Lira](https://github.com/phi-lira) and [Tim Cooper](https://github.com/stramit) — render pipeline work and support

[Unity Companion License](LICENSE.md)
