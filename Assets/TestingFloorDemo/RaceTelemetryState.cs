using TestingFloorSdk;
using UnityEngine;

namespace BoatAttack.Telemetry
{
    public sealed class RaceTelemetryState : ITelemetryContextProvider
    {
        private readonly Boat _boat;

        public RaceTelemetryState(Boat boat)
        {
            _boat = boat;
        }

        public void FillSnapshot(ref ContextSnapshot snapshot)
        {
            snapshot.Set("app.version", DemoTelemetry.Version);
            snapshot.Set("demo.control_mode", DemoTelemetry.Autoplay ? "ai_demo" : "human");
            snapshot.Set("boat.id", "boat-1");
            snapshot.Set("boat.hull", _boat ? _boat.hullName : "boat");
            snapshot.Set("settings.quality", QualitySettings.names[QualitySettings.GetQualityLevel()]);
            if (RaceManager.RaceData == null) return;
            snapshot.Set("race.lap_count", RaceManager.RaceData.laps);
            snapshot.Set("race.boat_count", RaceManager.RaceData.boatCount);
            snapshot.Set("race.reversed", RaceManager.RaceData.reversed);
        }
    }
}
