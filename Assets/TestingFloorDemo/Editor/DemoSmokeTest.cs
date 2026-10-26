using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Tf = TestingFloorSdk.TestingFloor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoatAttack.Telemetry.Editor
{
    [InitializeOnLoad]
    public static class DemoSmokeTest
    {
        private const string ActiveKey = "BoatAttack.TF.SmokeTest";
        private static double _startedAt;
        private static Vector3? _startPosition;
        private static bool _raceStarted;
        private static int _maxLap;
        private static float _distance;
        private static Vector3 _previousPosition;
        private static bool _captured;
        private static int _maxTrackedOpponents;
        private static bool _resetOpponent;
        private static readonly Dictionary<string, Vector3> PreviousOpponents = new();
        private static readonly Dictionary<string, float> OpponentDistances = new();

        [Serializable] private class SmokeResult
        {
            public bool success, opponentReset;
            public string sdkVersion, sessionId;
            public float playerDistance;
            public int trackedOpponents, droppedEvents;
            public string[] opponentIds;
            public float[] opponentDistances;
        }

        static DemoSmokeTest() { EditorApplication.update += Tick; }

        public static void Run()
        {
            if (!DemoTelemetry.Autoplay) throw new InvalidOperationException("Use --tf-demo-autoplay for the smoke test.");
            DemoTools.Configure();
            EditorSceneManager.OpenScene(DemoTools.DemoScene);
            DemoTools.ExportMap();
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            if (_startedAt == 0) _startedAt = EditorApplication.timeSinceStartup;
            var elapsed = EditorApplication.timeSinceStartup - _startedAt;
            var player = UnityEngine.Object.FindFirstObjectByType<BoatTelemetry>();
            if (player)
            {
                var position = player.transform.position;
                if (_startPosition.HasValue) _distance += Vector3.Distance(position, _previousPosition);
                else _startPosition = position;
                _previousPosition = position;
                _raceStarted |= RaceManager.RaceStarted;
                _maxLap = Mathf.Max(_maxLap, player.GetComponent<Boat>().LapCount);
            }
            var opponents = UnityEngine.Object.FindObjectsByType<OpponentBoatTelemetry>(FindObjectsSortMode.None);
            _maxTrackedOpponents = Mathf.Max(_maxTrackedOpponents, opponents.Count(opponent => opponent.IsTracked));
            foreach (var opponent in opponents)
            {
                var position = opponent.transform.position;
                var id = opponent.ObjectId;
                if (PreviousOpponents.TryGetValue(id, out var previous))
                    OpponentDistances[id] += Vector3.Distance(position, previous);
                else
                    OpponentDistances[id] = 0;
                PreviousOpponents[id] = position;
            }
            if (elapsed >= 30 && !_resetOpponent && opponents.Length > 0)
            {
                opponents.OrderBy(opponent => opponent.ObjectId).First().GetComponent<Boat>().ResetPosition();
                _resetOpponent = true;
            }
            if (elapsed < 90) return;
            if (!_captured)
            {
                _captured = true;
                Directory.CreateDirectory("Artifacts");
                ScreenCapture.CaptureScreenshot("Artifacts/smoke-gameplay.png");
                return;
            }
            if (elapsed < 92) return;
            SessionState.SetBool(ActiveKey, false);
            var sdkVersion = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Tf).Assembly).version;
            var expectedOpponents = RaceManager.RaceData.boatCount - 1;
            var success = _raceStarted && _distance > 50f && _maxLap >= 1
                && _maxTrackedOpponents == expectedOpponents && OpponentDistances.Count == expectedOpponents
                && OpponentDistances.Values.All(distance => distance > 50f)
                && _resetOpponent && Version.Parse(sdkVersion) >= new Version(0, 4, 3) && Tf.DroppedEventCount == 0;
            File.WriteAllText("Artifacts/smoke-result.json", JsonUtility.ToJson(new SmokeResult
            {
                success = success, opponentReset = _resetOpponent, sdkVersion = sdkVersion, sessionId = Tf.PlaySessionId,
                playerDistance = _distance, trackedOpponents = _maxTrackedOpponents,
                opponentIds = OpponentDistances.Keys.ToArray(),
                opponentDistances = OpponentDistances.Values.ToArray(), droppedEvents = Tf.DroppedEventCount
            }, true));
            Debug.Log($"[Boat Attack TF] Smoke result: raceStarted={_raceStarted}, distance={_distance:F1}m, lap={_maxLap}, trackedOpponents={_maxTrackedOpponents}, sdk={sdkVersion}, dropped={Tf.DroppedEventCount}, success={success}");
            EditorApplication.delayCall += () =>
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
            };
        }
    }
}
