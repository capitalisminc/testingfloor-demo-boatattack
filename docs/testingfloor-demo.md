# Testing Floor demo

This fork adds telemetry to the first human player's boat. The other boats retain their original gameplay logic. Open `Assets/scenes/demo_Island.unity` to race immediately; the upstream menus are optional.

## Setup

1. Clone with Git LFS and run `git lfs pull`.
2. Open with Unity **6000.3.7f1**. The package manifest pins the published Testing Floor Unity SDK; no sibling checkout is required.
3. Copy `TestingFloor.local.example.json` to `TestingFloor.local.json`. Enter your telemetry write key, organization slug, and game ID.
4. Run **Tools → Testing Floor → Boat Attack → Configure from local settings**, then **Open demo** and press Play.

The `.env`, local JSON, generated settings asset, and `Artifacts` directory are ignored by Git. Keep the API upload token in `.env`; the editor reads only `TESTING_FLOOR_WRITE_KEY` from it and embeds that telemetry key in the generated settings asset. The API token is never included in player builds. The settings asset is included in local player builds so they can send telemetry. Do not commit it. `TESTING_FLOOR_WRITE_KEY` and `TESTING_FLOOR_ENDPOINT` can override the key and endpoint at runtime. Editor Play reads the local JSON directly.

The demonstration currently uses `level.id = island` and `app.version = boatattack-tf-0.1.0`. Change the version in `DemoTelemetry.Version` and rerun Configure when publishing a changed level or build.

## Events and useful analytics

| Event | Trigger / example use |
| --- | --- |
| `race_loaded`, `race_started` | Countdown to race-start funnel |
| `player_moved` | SDK movement samples, up to ten per second while moving, for paths and speed maps |
| `checkpoint_reached` | First visit to a checkpoint each lap; segment progression |
| `lap_completed` | Valid lap, with lap number and duration |
| `lap_rejected` | Crossed start without all checkpoints; shortcut and navigation problems |
| `race_completed` | Final place, duration, collision count and reset count |
| `race_abandoned` | Player boat destroyed before finishing, including scene exit |
| `boat_collision` | Impacts above 2 m/s, at most twice per second; other boat versus environment and contact coordinates |
| `boat_reset`, `player_teleported` | Reset distance and SDK discontinuity for accurate paths |
| `boat_off_course`, `boat_returned_to_course` | Crossed track width plus 5 m; sampled twice per second |
| `game_paused`, `game_resumed` | Player pause controls |

All events include race ID, race phase, lap, progress, place, elapsed time, boat speed in m/s, off-course state, level and version. The SDK adds player coordinates, camera pose/FOV, device metadata and analytics session identity. AI-controlled demo runs carry `demo.control_mode = ai_demo`; interactive runs carry `human`.

The game opts into SDK movement tracking with a 0.1-second minimum ping interval and a 0.5-meter movement threshold. The runtime and editor setup share this configuration. At typical racing speeds of about 10 m/s, this gives roughly one meter between samples; the SDK’s 0.5-second default interval would leave about five meters between samples.

Example demo stories: find the bend with the most environment collisions, compare speed through that bend, inspect the corresponding recording, identify missed checkpoints, and compare lap time before and after boat resets. Position units are meters and the map uses X/Z.

## Capture and session pairing

QR heartbeats are enabled by the example local settings for screen recordings. The SDK also accepts the recorder's standard `--testing-floor` launch payload or its session-payload file. That handoff pairs telemetry with the recording and normally suppresses the QR overlay. No custom recording/session identifiers are invented by this game. For external captures, leave QR heartbeats enabled.

## Telemetry field mappings

`testingfloor-telemetry-schema.json` configures this game's level, build version, spatial coordinates and event presentation for analytics. Apply it with the schema CLI/API:

```sh
testingfloor set-telemetry-schema --organization-id YOUR_ORG --game-id YOUR_GAME --schema testingfloor-telemetry-schema.json
```

Use a `telemetry:configure` API token. The same JSON can also be applied in the game's Analytics settings schema editor.

## Analytics map

Stop Play and run **Tools → Testing Floor → Boat Attack → Export island analytics map**. This uses a flat ocean color and excludes sky/fog so coastlines remain readable. It renders an orthographic 2048px PNG covering the course with margins, plus `Artifacts/maps/testingfloor-maps.json`. The bounds come from the same camera used to render the image: +X is right, +Z is up, one world unit is one meter.

Upload with an API token that has `maps:sync` (the telemetry write key cannot upload maps):

```sh
TESTING_FLOOR_API_TOKEN=... npx @testingfloor/cli upload-map \
  --config Artifacts/maps/testingfloor-maps.json
```

The manifest uses your local organization/game and the same level/version emitted by the SDK.

## Build and smoke run

**Tools → Testing Floor → Boat Attack → Build macOS demo** builds Addressables and a development player at `Builds/BoatAttack.app`. Configure local settings first.

For an automated race using the game's real AI and physics:

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.3.7f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -projectPath "$PWD" \
  -executeMethod BoatAttack.Telemetry.Editor.DemoSmokeTest.Run \
  --tf-demo-autoplay -logFile Artifacts/smoke.log
```

This runs for 90 seconds, checks that the race starts and the player boat travels at least 50 m and crosses the start line, captures a gameplay screenshot, and exits with the result. It sends genuine AI gameplay to the configured collector; use a test game/key when appropriate. It requires graphics support, so do not add `-nographics`. For a compiled player, `--tf-demo-autoplay` lets AI steer the observed player boat while retaining the player camera and race UI.

## License

The game and assets retain Unity’s [Unity Companion License](../LICENSE.md). This fork does not change those permissions or represent the game as original Testing Floor work. The SDK has its own license in its package.


## Release builds and deployment

Store `TESTING_FLOOR_API_TOKEN` (with `builds:create`), `TESTING_FLOOR_WRITE_KEY`, the API URL and organization/game IDs in ignored `.env`. `.env.example` shows the fields without credentials.

Batch release entry points are `BoatAttack.Telemetry.Editor.DemoTools.BuildReleaseMac` with `-buildTarget OSXUniversal`, and `BuildReleaseWindows` with `-buildTarget Win64`. Both disable development, script debugging and profiler connections, and build their platform's Addressables. macOS is universal (Intel/Apple Silicon); Windows is x64 Mono. Players default to borderless fullscreen at the display’s native resolution. Configure reapplies these display defaults before builds. Generated reports under `Artifacts/release` record the version and release flag.

`testingfloor-builds.json` describes both upload folders and launch paths. After building, keep any `*_BurstDebugInformation_DoNotShip` directory outside those folders. Deploy using Node's environment-file support:

```sh
node --env-file=.env /path/to/testingfloor-cli/bin/testingfloor.js upload-build \
  --config testingfloor-builds.json --butler-path /path/to/butler
```

Release artifacts are under `Builds/Release/macOS` and `Builds/Release/Windows`. `.env` stays outside both upload roots.


## Presentation and map configuration

`testingfloor-telemetry-schema.json` is the reusable analytics configuration. It defines readable event names, icons, color families, event-specific metadata, conditional labels for boat versus scenery collisions, and noise treatment for movement and duplicate position-reset events. Race/checkpoint progress stays visible alongside collisions, off-course events, resets, pauses and early exits.

The file also configures level/build matching, player and impact coordinates, environment fields and topline properties. It deliberately leaves the elapsed-game-time path unset: `race.elapsed_s` is a race clock that pauses/resets, so timeline timing uses event timestamps.

Apply the configuration with the CLI schema command and upload the matching island PNG/manifest. The API token in ignored `.env` needs `telemetry:configure` and `maps:sync` in addition to `builds:create` for deployments. The analytics map is pinned to the emitted build version and uses Unity X/Z meters. Player movement feeds speed maps; impact coordinates feed collision maps. Direct links for this local deployment are saved in ignored `Artifacts/analytics/views.json`.
