using TestingFloorSdk;
using UnityEngine;
using Tf = TestingFloorSdk.TestingFloor;

namespace BoatAttack.Telemetry
{
    [DisallowMultipleComponent]
    public sealed class OpponentBoatTelemetry : MonoBehaviour
    {
        private Boat _boat;
        private TrackedObject _tracked;
        private int _boatNumber;
        private long _resets;

        public string ObjectId { get; private set; }
        public string Label { get; private set; }
        public bool IsTracked => _tracked != null && _tracked.IsTracked;

        public void Initialize(int boatNumber)
        {
            _tracked?.Remove("replaced");
            _boat = GetComponent<Boat>();
            _boatNumber = boatNumber;
            ObjectId = "boat-" + boatNumber;
            Label = string.IsNullOrWhiteSpace(name) ? "Boat " + boatNumber : name + " (Boat " + boatNumber + ")";
            Register();
        }

        private void OnEnable()
        {
            if (_boat && _tracked == null) Register();
        }

        private void Register()
        {
            _tracked = Tf.TrackObject("boat", transform, ObjectId)
                .SetState("label", Label)
                .SetState("boat.number", (long)_boatNumber)
                .SetState("boat.control_mode", "ai")
                .SetState("boat.reset_count", _resets)
                .SetState("boat.hull", _boat.hullName ?? "boat");
            UpdateState();
        }

        public void BeginReset()
        {
            _tracked?.Remove("boat_reset");
            _tracked = null;
        }

        public void EndReset()
        {
            _resets++;
            Register();
        }

        private void LateUpdate()
        {
            UpdateState();
        }

        private void UpdateState()
        {
            if (!_boat || _tracked == null) return;
            _tracked.SetState("race.lap", (long)Mathf.Max(1, _boat.LapCount))
                .SetState("race.place", (long)_boat.Place)
                .SetState("race.finished", _boat.MatchComplete)
                .SetState("race.phase", _boat.MatchComplete ? "finished" : RaceManager.RaceStarted ? "racing" : "countdown");
        }

        private void OnDisable()
        {
            _tracked?.Remove("disabled");
            _tracked = null;
        }
    }
}
