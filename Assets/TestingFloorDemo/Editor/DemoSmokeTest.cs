using System;
using System.IO;
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

        static DemoSmokeTest() { EditorApplication.update += Tick; }

        public static void Run()
        {
            if (!DemoTelemetry.Autoplay) throw new InvalidOperationException("Use --tf-demo-autoplay for the smoke test.");
            DemoTools.Configure();
            EditorSceneManager.OpenScene(DemoTools.DemoScene);
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
            var success = _raceStarted && _distance > 50f && _maxLap >= 1;
            Debug.Log($"[Boat Attack TF] Smoke result: raceStarted={_raceStarted}, distance={_distance:F1}m, lap={_maxLap}, success={success}");
            EditorApplication.delayCall += () =>
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
            };
        }
    }
}
