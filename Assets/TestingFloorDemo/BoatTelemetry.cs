using System;
using TestingFloor;
using UnityEngine;
using Tf = TestingFloor.TestingFloor;

namespace BoatAttack.Telemetry
{
    [DefaultExecutionOrder(100)]
    public sealed class BoatTelemetry : MonoBehaviour, ITelemetryContextProvider
    {
        private Boat _boat;
        private bool _started;
        private bool _finished;
        private bool _offCourse;
        private float _nextCollision;
        private float _nextCourseSample;
        private float _lapStart;
        private int _collisions;
        private int _resets;
        private string _raceId;

        private void Awake()
        {
            _boat = GetComponent<Boat>();
            _raceId = Guid.NewGuid().ToString("N");
            Tf.RegisterContextProvider(this);
            Tf.SetPositionSource(transform);
            Tf.UseMainCamera();
            Tf.SetMovementTrackingEnabled(true);
            Tf.Track("race_loaded").Send();
        }

        private void LateUpdate()
        {
            if (!_started && RaceManager.RaceStarted)
            {
                _started = true;
                Tf.Track("race_started").Send();
            }
            if (!_started || _finished || !WaypointGroup.Instance || Time.unscaledTime < _nextCourseSample) return;
            _nextCourseSample = Time.unscaledTime + 0.5f;
            var group = WaypointGroup.Instance;
            var nearest = group.GetClosestPointOnPath(transform.position, out var segment);
            var width = Mathf.Max(segment.Item1.width, segment.Item2.width);
            var distance = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(nearest.x, nearest.z));
            var offCourse = distance > width + 5f;
            if (offCourse == _offCourse) return;
            _offCourse = offCourse;
            Tf.Track(offCourse ? "boat_off_course" : "boat_returned_to_course")
                .Set("course.distance_m", distance).Send();
        }

        public void Checkpoint(int index, bool firstVisit)
        {
            if (!_started || _finished || !firstVisit) return;
            Tf.Track("checkpoint_reached").Set("checkpoint.index", index).Send();
        }

        public void LapRejected(int completed, int required)
        {
            if (!_started || _finished) return;
            Tf.Track("lap_rejected").Set("checkpoint.completed", completed).Set("checkpoint.required", required).Send();
        }

        public void LapCrossed(int newLap)
        {
            if (!_started || _finished) return;
            if (newLap > 1)
                Tf.Track("lap_completed").Set("lap.number", newLap - 1)
                    .Set("lap.duration_s", RaceManager.RaceTime - _lapStart).Send();
            _lapStart = RaceManager.RaceTime;
            if (newLap <= RaceManager.GetLapCount()) return;
            _finished = true;
            Tf.Track("race_completed").Set("race.duration_s", RaceManager.RaceTime)
                .Set("race.collision_count", _collisions).Set("race.reset_count", _resets).Send();
            Tf.SetMovementTrackingEnabled(false);
            _ = Tf.FlushAsync(TimeSpan.FromSeconds(2));
        }

        public void ResetBoat(Vector3 start, Vector3 end)
        {
            if (_finished) return;
            _resets++;
            Tf.RecordTeleport(start, end, "boat_reset");
            Tf.Track("boat_reset").Set("reset.distance_m", Vector3.Distance(start, end)).Send();
        }

        public void PauseChanged(bool paused)
        {
            if (_finished) return;
            Tf.Track(paused ? "game_paused" : "game_resumed").Send();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_started || _finished || Time.unscaledTime < _nextCollision || collision.relativeVelocity.magnitude < 2f) return;
            _nextCollision = Time.unscaledTime + 0.5f;
            _collisions++;
            var other = collision.collider.GetComponentInParent<Boat>();
            var evt = Tf.Track("boat_collision").Set("collision.kind", other ? "boat" : "environment")
                .Set("collision.relative_speed_mps", collision.relativeVelocity.magnitude);
            if (collision.contactCount > 0)
            {
                var p = collision.GetContact(0).point;
                evt.Set("collision.position.x", p.x).Set("collision.position.y", p.y).Set("collision.position.z", p.z);
            }
            evt.Send();
        }

        public void FillSnapshot(ref ContextSnapshot snapshot)
        {
            snapshot.Set("level.id", DemoTelemetry.LevelId);
            snapshot.Set("app.version", DemoTelemetry.Version);
            snapshot.Set("demo.control_mode", DemoTelemetry.Autoplay ? "ai_demo" : "human");
            snapshot.Set("race.id", _raceId);
            snapshot.Set("race.phase", _finished ? "finished" : _started ? "racing" : "countdown");
            snapshot.Set("race.elapsed_s", RaceManager.RaceTime);
            snapshot.Set("race.place", _boat.Place);
            snapshot.Set("race.lap", Mathf.Max(1, _boat.LapCount));
            snapshot.Set("race.lap_progress", _boat.LapPercentage);
            snapshot.Set("boat.speed_mps", _boat.engine ? _boat.engine.VelocityMag : 0);
            snapshot.Set("boat.off_course", _offCourse);
            if (RaceManager.RaceData != null)
            {
                snapshot.Set("race.lap_count", RaceManager.RaceData.laps);
                snapshot.Set("race.boat_count", RaceManager.RaceData.boatCount);
                snapshot.Set("race.reversed", RaceManager.RaceData.reversed);
            }
        }

        private void OnDestroy()
        {
            if (_started && !_finished) Tf.Track("race_abandoned").Set("race.duration_s", RaceManager.RaceTime).Send();
            Tf.ClearPositionSource();
            Tf.ClearCameraSource();
            Tf.RegisterContextProvider(null);
        }
    }
}
