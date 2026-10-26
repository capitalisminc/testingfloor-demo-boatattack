using System;
using System.IO;
using System.Linq;
using TestingFloor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BoatAttack.Telemetry.Editor
{
    public static class DemoTools
    {
        public const string DemoScene = "Assets/scenes/demo_Island.unity";
        private const string SettingsPath = "Assets/Resources/TestingFloorSettings.asset";

        [MenuItem("Tools/Testing Floor/Boat Attack/Configure from local settings")]
        public static void Configure()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TestingFloorSettings>(SettingsPath);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<TestingFloorSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            if (File.Exists("TestingFloor.local.json"))
                DemoTelemetry.ApplyLocalSettings(settings, File.ReadAllText("TestingFloor.local.json"));
            var localWriteKey = ReadLocalEnvironment("TESTING_FLOOR_WRITE_KEY");
            if (!string.IsNullOrWhiteSpace(localWriteKey))
            {
                settings.writeKey = localWriteKey;
                settings.enabled = true;
            }
            DemoTelemetry.ConfigureMovementTracking(settings);
            settings.logErrors = true;
            EditorUtility.SetDirty(settings);
            PlayerSettings.bundleVersion = DemoTelemetry.Version;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[Boat Attack TF] Local telemetry settings prepared. Credentials are excluded from Git.");
        }

        [MenuItem("Tools/Testing Floor/Boat Attack/Open demo")]
        public static void OpenDemo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(DemoScene);
        }

        [MenuItem("Tools/Testing Floor/Boat Attack/Export island analytics map")]
        public static void ExportMap()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before exporting the map.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(DemoScene);
                var course = UnityEngine.Object.FindFirstObjectByType<WaypointGroup>();
                if (!course || course.WPs.Count == 0) throw new InvalidOperationException("Island course was not found.");
                var bounds = new Bounds(course.WPs[0].point, Vector3.zero);
                foreach (var wp in course.WPs)
                {
                    bounds.Encapsulate(wp.point + new Vector3(wp.width, 0, wp.width));
                    bounds.Encapsulate(wp.point - new Vector3(wp.width, 0, wp.width));
                }
                bounds.Expand(80f);
                var size = Mathf.Max(bounds.size.x, bounds.size.z);
                var center = bounds.center;
                const int resolution = 2048;
                Directory.CreateDirectory("Artifacts/maps");
                var cameraObject = new GameObject("Testing Floor map camera");
                var camera = cameraObject.AddComponent<Camera>();
                var texture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
                var pixels = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
                var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ocean.name = "Testing Floor map ocean";
                ocean.transform.position = new Vector3(center.x, 0.05f, center.z);
                ocean.transform.localScale = new Vector3(size / 10f, 1, size / 10f);
                var oceanMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                oceanMaterial.SetColor("_BaseColor", new Color(0.08f, 0.43f, 0.48f));
                ocean.GetComponent<Renderer>().sharedMaterial = oceanMaterial;
                var previousTarget = RenderTexture.active;
                var previousFog = RenderSettings.fog;
                try
                {
                    RenderSettings.fog = false;
                    camera.transform.SetPositionAndRotation(new Vector3(center.x, 200, center.z), Quaternion.LookRotation(Vector3.down, Vector3.forward));
                    camera.orthographic = true;
                    camera.orthographicSize = size * 0.5f;
                    camera.nearClipPlane = 0.3f;
                    camera.farClipPlane = 600f;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.13f, 0.48f, 0.54f);
                    camera.cullingMask = ~LayerMask.GetMask("UI", "3DSkybox", "Player1", "Player2", "Player3", "Player4");
                    camera.targetTexture = texture;
                    camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = texture };
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = texture;
                    pixels.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                    pixels.Apply();
                    var colors = pixels.GetPixels32();
                    var first = colors[0];
                    if (!colors.Any(p => Mathf.Abs(p.r - first.r) + Mathf.Abs(p.g - first.g) + Mathf.Abs(p.b - first.b) > 40))
                        throw new InvalidOperationException("Map render is blank; the image was not exported.");
                    File.WriteAllBytes("Artifacts/maps/island.png", pixels.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previousTarget;
                    RenderSettings.fog = previousFog;
                    UnityEngine.Object.DestroyImmediate(pixels);
                    UnityEngine.Object.DestroyImmediate(texture);
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                    UnityEngine.Object.DestroyImmediate(ocean);
                    UnityEngine.Object.DestroyImmediate(oceanMaterial);
                }
                var config = new MapConfig
                {
                    appVersion = DemoTelemetry.Version,
                    maps = new[] { new Map { bounds = new MapBounds { centerX = center.x, centerZ = center.z, sizeX = size, sizeZ = size } } }
                };
                if (File.Exists("TestingFloor.local.json"))
                {
                    var local = JsonUtility.FromJson<DemoTelemetry.LocalSettings>(File.ReadAllText("TestingFloor.local.json"));
                    config.organizationId = local.organizationId;
                    config.gameId = local.gameId;
                }
                File.WriteAllText("Artifacts/maps/testingfloor-maps.json", JsonUtility.ToJson(config, true) + "\n");
                Debug.Log($"[Boat Attack TF] Map exported: X/Z center ({center.x}, {center.z}), size {size}m. +Z is up, +X is right.");
            }
            finally
            {
                if (previous.Any(scene => scene.isLoaded && !string.IsNullOrEmpty(scene.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        public static void PrepareDemo()
        {
            Configure();
            EditorSceneManager.OpenScene(DemoScene);
            ExportMap();
        }

        [MenuItem("Tools/Testing Floor/Boat Attack/Build macOS demo")]
        public static void BuildMac()
        {
            Configure();
            ExportMap();
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent();
            var report = BuildPipeline.BuildPlayer(new[] { DemoScene }, "Builds/BoatAttack.app", BuildTarget.StandaloneOSX, BuildOptions.Development);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Boat Attack build failed: " + report.summary.result);
        }

        private static string ReadLocalEnvironment(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value)) return value;
            if (!File.Exists(".env")) return null;
            foreach (var line in File.ReadLines(".env"))
            {
                var entry = line.Trim();
                if (entry.StartsWith("#") || !entry.StartsWith(name + "=", StringComparison.Ordinal)) continue;
                return entry.Substring(name.Length + 1).Trim().Trim('"', '\'');
            }
            return null;
        }

        [MenuItem("Tools/Testing Floor/Boat Attack/Build release macOS")]
        public static void BuildReleaseMac()
        {
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 2);
            BuildRelease(BuildTarget.StandaloneOSX, "Builds/Release/macOS/BoatAttack.app");
        }

        [MenuItem("Tools/Testing Floor/Boat Attack/Build release Windows")]
        public static void BuildReleaseWindows()
        {
            BuildRelease(BuildTarget.StandaloneWindows64, "Builds/Release/Windows/BoatAttack.exe");
        }

        private static void BuildRelease(BuildTarget target, string output)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new InvalidOperationException("Launch Unity with -buildTarget matching the release platform.");
            Configure();
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var content);
            if (!string.IsNullOrEmpty(content.Error)) throw new InvalidOperationException(content.Error);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { DemoScene },
                locationPathName = output,
                target = target,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Release build failed: " + report.summary.result);
            if ((report.summary.options & (BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler)) != 0)
                throw new InvalidOperationException("Release build unexpectedly contains development options.");
            Directory.CreateDirectory("Artifacts/release");
            File.WriteAllText("Artifacts/release/" + target + ".json", JsonUtility.ToJson(new ReleaseReport
            {
                platform = target.ToString(), version = DemoTelemetry.Version, development = false,
                output = output, sizeBytes = report.summary.totalSize.ToString()
            }, true));
            Debug.Log("[Boat Attack TF] Release build succeeded: " + target + ", development=false, output=" + output);
        }

        [Serializable] private class ReleaseReport
        {
            public string platform, version, output, sizeBytes;
            public bool development;
        }

        [Serializable] private class MapConfig
        {
            public string organizationId = "YOUR_ORGANIZATION";
            public int gameId;
            public string appVersion;
            public Map[] maps;
        }
        [Serializable] private class Map
        {
            public string levelId = DemoTelemetry.LevelId;
            public string image = "./island.png";
            public string variantKey = "overhead";
            public bool defaultVariant = true;
            public MapBounds bounds;
        }
        [Serializable] private class MapBounds
        {
            public float centerX, centerZ, sizeX, sizeZ;
        }
    }
}
