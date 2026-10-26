using System;
using System.IO;
using TestingFloorSdk;
using UnityEngine;
using Tf = TestingFloorSdk.TestingFloor;

namespace BoatAttack.Telemetry
{
    public static class DemoTelemetry
    {
        public const string LevelId = "island";
        public const string Version = "boatattack-tf-0.2.0";
        public static bool Autoplay => Array.Exists(Environment.GetCommandLineArgs(), a => a == "--tf-demo-autoplay");

        [Serializable]
        public class LocalSettings
        {
            public string organizationId;
            public int gameId;
            public string writeKey;
            public string endpoint = "https://dataentry.testingfloor.com";
            public bool enableInEditor = true;
            public bool qrHeartbeats = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Array.Exists(Environment.GetCommandLineArgs(), a => string.Equals(a, "-runTests", StringComparison.OrdinalIgnoreCase)))
            {
                Tf.SetForceDisabled(true);
                return;
            }
            var settings = TestingFloorSettings.Current;
            var path = Path.Combine(Application.dataPath, "../TestingFloor.local.json");
            if (Application.isEditor && File.Exists(path)) ApplyLocalSettings(settings, File.ReadAllText(path));
            var key = Environment.GetEnvironmentVariable("TESTING_FLOOR_WRITE_KEY");
            if (!string.IsNullOrWhiteSpace(key))
            {
                settings.writeKey = key;
                settings.enabled = true;
                settings.enableInEditor = true;
            }
            var endpoint = Environment.GetEnvironmentVariable("TESTING_FLOOR_ENDPOINT");
            if (!string.IsNullOrWhiteSpace(endpoint)) settings.endpoint = endpoint;
            settings.logErrors = true;
            ConfigureTracking(settings);
        }

        public static void ConfigureTracking(TestingFloorSettings settings)
        {
            settings.objectSampleRateHz = 10f;
            settings.movementTrackingEnabled = true;
            settings.movementMinPingIntervalSeconds = 0.1f;
            settings.movementMinPingDistance = 0.5f;
        }

        public static void ApplyLocalSettings(TestingFloorSettings settings, string json)
        {
            var config = JsonUtility.FromJson<LocalSettings>(json);
            settings.writeKey = config.writeKey;
            settings.endpoint = config.endpoint;
            settings.enableInEditor = config.enableInEditor;
            settings.qrHeartbeatsEnabled = config.qrHeartbeats;
            settings.enabled = !string.IsNullOrWhiteSpace(config.writeKey);
        }
    }
}
