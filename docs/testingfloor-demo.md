# Unity integration reference

This guide follows the Testing Floor integration in Boat Attack. Use it alongside [BoatTelemetry.cs](../Assets/TestingFloorDemo/BoatTelemetry.cs), which contains most of the example, and the [Unity SDK documentation](https://github.com/capitalisminc/testingfloor-sdk-unity/blob/af5d3ce8414265701d68eb1165b50045eb20bb29/README.md) for the full API.

For instructions to play the game or connect your write key, start with the [README](../README.md).

## Connect the player and camera

[Boat.cs](../Assets/Scripts/Boat/Boat.cs) attaches `BoatTelemetry` when it creates the first player’s boat. In `Awake`, the component registers its context provider and gives the SDK the boat transform and main camera:

```csharp
using Tf = TestingFloorSdk.TestingFloor;

Tf.RegisterContextProvider(this);
Tf.SetPositionSource(transform);
Tf.UseMainCamera();
Tf.SetMovementTrackingEnabled(true);
```

The SDK attaches player coordinates, camera position, camera rotation, and field of view to events, and sends viewport size as session state. Movement tracking also emits `player_moved` samples as the boat travels around the island.

The player uses the SDK’s movement stream. Each opponent has a tracked-object path with its own ID and label, alongside the player’s path.

## Add context once

`BoatTelemetry` implements `ITelemetryContextProvider`. Its `FillSnapshot` method adds useful game state to every event:

```csharp
snapshot.Set("level.id", DemoTelemetry.LevelId);
snapshot.Set("race.id", _raceId);
snapshot.Set("race.lap", Mathf.Max(1, _boat.LapCount));
snapshot.Set("race.place", _boat.Place);
snapshot.Set("boat.speed_mps", _boat.engine ? _boat.engine.VelocityMag : 0);
```

A collision can then be analyzed by speed, lap, race position, or location without repeating those properties at each event call. The full provider also includes race phase, lap progress, elapsed race time, course state, and build version. The SDK adds device information and session identity.

In your game, use the same pattern for state such as the current level, health, equipment, or objective. Keep the provider inexpensive: it runs when events are captured.

## Send persistent values as session state

[RaceTelemetryState.cs](../Assets/TestingFloorDemo/RaceTelemetryState.cs) holds values such as the race’s lap count, boat count, player hull, and graphics quality. It uses the same provider interface as event context:

```csharp
Tf.RegisterStateProvider(new RaceTelemetryState(_boat));
```

The SDK sends these values when they first become available and when they change. Testing Floor applies the state at each moment, so events can still be filtered by it. Keep frequently changing values such as speed, lap, position, and the game clock in event context; put settings and race setup in session state.

## Follow the other boats

[OpponentBoatTelemetry.cs](../Assets/TestingFloorDemo/OpponentBoatTelemetry.cs) registers each opponent when the game creates it:

```csharp
_tracked = Tf.TrackObject("boat", transform, ObjectId)
    .SetState("label", Label)
    .SetState("boat.number", (long)_boatNumber)
    .SetState("boat.control_mode", "ai");
```

IDs such as `boat-2` identify a race slot; labels show the opponent’s name. The component updates the object’s lap, race position, phase, and finish state. The SDK sends state changes and samples the transform, so the game does not need to emit a gameplay event for every opponent movement.

Player collisions include `collision.other_boat_id` and `collision.other_boat_label`. The ID matches the opponent’s tracked path, letting you connect a collision to the boat involved. When a boat is disabled, its handle is removed with a final position; enabling it again starts a new lifetime under the same ID. A boat reset ends the old path before moving, then registers the same ID at the destination. This keeps resets from drawing a line across the jump and records a reset count on the object.

The SDK also offers a **Testing Floor → Tracked Object** component for prefabs that only need a path. Code registration is useful here because the example adds names and race state as the boats are created.

## Track meaningful gameplay moments

Emit events from the gameplay code that already knows something happened. For example, crossing a checkpoint calls:

```csharp
Tf.Track("checkpoint_reached")
    .Set("checkpoint.index", index)
    .Send();
```

The event identifies the moment and its checkpoint; the shared context supplies the rest of the race state.

| Events | What you can learn |
| --- | --- |
| `race_loaded`, `race_started` | How a session reaches the start of a race |
| `checkpoint_reached` | Which checkpoints a player reaches, and in what order |
| `lap_completed`, `lap_rejected` | Lap times and laps invalidated by missed checkpoints |
| `race_completed`, `race_abandoned` | Finishing position, race duration, and early exits |
| `boat_collision` | Where impacts happen, how fast they are, and whether another boat or scenery is involved |
| `boat_off_course`, `boat_returned_to_course` | Where players leave the course and recover |
| `boat_reset`, `player_teleported` | When and how far a player resets |
| `game_paused`, `game_resumed` | Where pauses fit into the session |
| `player_moved` | Movement paths and speed around the island |

Checkpoint events are emitted on the first visit to each checkpoint per lap. Collisions are recorded above a relative speed of 2 m/s, with a half-second cooldown. These are examples of choosing events that describe player behavior without recording every physics callback.

## Choose movement density for your game

[DemoTelemetry.ConfigureTracking](../Assets/TestingFloorDemo/DemoTelemetry.cs) sets a **0.1-second minimum interval** and a **0.5-meter movement threshold**. At a racing speed of about 10 m/s, that produces samples roughly one meter apart. Opponent objects are sampled at **10 Hz**, using the SDK’s 0.25-meter movement threshold.

Use the speed of your player and the scale of your map to choose a useful interval. A slower character may need fewer samples; a fast vehicle needs denser sampling to show its route through small heatmap cells. These settings control SDK movement events, so the game does not need a custom position timer.

Boat resets use the SDK’s teleport API:

```csharp
Tf.RecordTeleport(start, end, "boat_reset");
```

That records both ends of the jump and resets movement sampling at the destination. Use the same pattern for respawns, fast travel, or instant repositioning.

## Put events on the map

With Play mode stopped, choose **Tools → Testing Floor → Boat Attack → Export island analytics map**. The exporter produces:

- `Artifacts/maps/island.png` — an overhead image of the course.
- `Artifacts/maps/testingfloor-maps.json` — the level, build version, and world bounds for that image.

The image and bounds come from the same top-down camera, with the bounds measured at sea level. `game_level.id` identifies the island on tracked-object records; the example also emits `level.id` for its analytics field mapping. Unity **X/Z** is the map plane: +X points right, +Z points up, and one world unit equals one meter. The exporter keeps the game’s water and shoreline shading. A distant perspective camera supports the water shader’s depth calculations, while fog and the sky are excluded to keep the course readable beneath analytics.

Set `organizationId` and `gameId` in your local JSON before exporting for your own Testing Floor game. You can upload the result with the [Testing Floor CLI](https://github.com/capitalisminc/testingfloor-cli):

```sh
testingfloor upload-map --config Artifacts/maps/testingfloor-maps.json
```

This optional upload uses an API token with `maps:sync` access.

The example has two coordinate sources:

| Source | Coordinates | Useful view |
| --- | --- | --- |
| `player_position` | `player.position.x/y/z` | Movement density or average `boat.speed_mps` for `player_moved` events |
| `collision_position` | `collision.position.x/y/z` | Counts of `boat_collision` at the actual point of impact |

Keeping impact coordinates separate from the boat’s center makes collision maps more useful around rocks, shorelines, and other boats.

## Make the analytics readable

[testingfloor-telemetry-schema.json](../testingfloor-telemetry-schema.json) provides the field mappings and presentation rules for this example. Apply it in your game’s Analytics settings schema editor, or with the CLI:

```sh
testingfloor set-telemetry-schema \
  --organization-id YOUR_ORG --game-id YOUR_GAME \
  --schema testingfloor-telemetry-schema.json
```

The CLI command uses an API token with `telemetry:configure` access. The configuration gives events readable titles, icons, colors, and relevant properties. For example, a `boat_collision` becomes “Collision with another boat” or “Hit scenery” according to `collision.kind`, and shows impact speed alongside the player’s speed and lap.

Movement samples are marked as noise so they can be hidden when browsing individual gameplay moments while remaining available for spatial analysis. The configuration also maps `level.id`, build version, and the two coordinate sources to the map. When adapting this example, update these mappings to match the property names your game emits.

The session timeline uses event timestamps. `race.elapsed_s` remains a gameplay property because the race clock pauses and resets between races.

## Connect telemetry to recordings

When the Testing Floor recorder launches the game with a recording session, the SDK uses that handoff to associate telemetry with the recording. You can move from an interesting event or location to the corresponding footage.

For external captures, the local example settings enable QR heartbeats. The visible QR gives Testing Floor timing information to align video and events. See the SDK’s [recording sync documentation](https://github.com/capitalisminc/testingfloor-sdk-unity/blob/af5d3ce8414265701d68eb1165b50045eb20bb29/README.md#recording-sync) for the available controls.

## Adapt the example

Start with the player transform and camera, add context for each moment and state for persistent values, and track a few events that answer questions about your game. Register the NPCs, vehicles, or other objects whose paths help explain what happened. Then export a map and use the presentation configuration to give those events useful names and properties.

For a platformer, those moments might be deaths, checkpoints, and pickups. For an exploration game, they might be discoveries, interactions, and abandoned objectives. The same SDK and map patterns apply; the events should describe what matters in your game.
