using System;
using System.IO;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands.Gestures;
using Unity.XR.CoreUtils;

namespace Riftline
{
    public static class RiftlineSceneBuilder
    {
        public const string ScenePath = "Assets/Riftline/Scenes/Riftline.unity";
        public const string LibraryPath = "Assets/Riftline/Art/RiftlineAssets.asset";
        const string RootName = "RIFTLINE • Navigation Core";
        const string KeyLightName = "Shipyard Key Light";
        const string RimLightName = "Runner Warm Rim";
        const string FillLightName = "Navigation Runner Fill";
        const string VolumeName = "Presentation Color and Light";
        const string VolumePath = "Assets/Riftline/Art/PresentationVolume.asset";
        const string HandRootPath = "Assets/Riftline/HandTracking";
        const string HandGesturePath = HandRootPath + "/Gestures";
        const string HandResourcesPath = HandRootPath + "/Resources";
        const string HandSettingsPath = HandResourcesPath + "/RiftlineHandControlSettings.asset";
        const string GestureSamplePath = "Assets/Samples/XR Hands/1.7.3/Gestures/Examples/";
        static readonly string[] RuntimeShaderNames =
        {
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Particles/Unlit"
        };

        [MenuItem("Riftline/Create or Update Game Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building the scene.");
            Directory.CreateDirectory("Assets/Riftline/Scenes");
            Directory.CreateDirectory("Assets/Riftline/Art");
            AssetDatabase.Refresh();
            EnsureRuntimeShaders();
            EnsureHandControlAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var library = AssetDatabase.LoadAssetAtPath<RiftlineAssets>(LibraryPath);
            if (!library)
            {
                library = ScriptableObject.CreateInstance<RiftlineAssets>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            PopulateLibrary(library);
            var root = new GameObject(RootName);
            var game = root.AddComponent<RiftlineGame>();
            game.Assets = library;
            var navigationObject = new GameObject("Route Navigation Rig");
            navigationObject.transform.SetParent(root.transform, false);
            game.NavigationRig = navigationObject.AddComponent<RiftlineNavigationRig>();
            var originObject = new GameObject("XR Origin");
            originObject.transform.SetParent(navigationObject.transform, false);
            game.NavigationRig.TrackingOrigin = originObject.transform;
            var origin = originObject.AddComponent<XROrigin>();
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 1.6f;
            var offset = new GameObject("Head Tracking Offset");
            offset.transform.SetParent(originObject.transform, false);
            offset.transform.localPosition = new Vector3(0, 1.6f, 0);
            origin.CameraFloorOffsetObject = offset;
            game.TrackingSpace = offset.transform;
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 250;
            camera.fieldOfView = 72;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(.015f, .03f, .065f);
            cameraObject.AddComponent<AudioListener>();
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.None;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            var pose = cameraObject.AddComponent<TrackedPoseDriver>();
            pose.positionInput = new InputActionProperty(new InputAction("Head Position", InputActionType.Value, "<XRHMD>/centerEyePosition"));
            pose.rotationInput = new InputActionProperty(new InputAction("Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));
            pose.trackingStateInput = new InputActionProperty(new InputAction("Head Tracking", InputActionType.Value, "<XRHMD>/trackingState"));
            pose.enabled = true;
            cameraObject.AddComponent<RiftlineHeadTracking>();
            origin.Camera = camera;
            game.ViewCamera = camera;
            originObject.AddComponent<PXR_Manager>();
            var sun = new GameObject(KeyLightName);
            sun.transform.rotation = Quaternion.Euler(35, -25, 0);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.6f;
            light.color = new Color(.94f, .97f, 1);
            light.shadows = LightShadows.Soft;
            var warm = new GameObject(RimLightName);
            warm.transform.SetParent(navigationObject.transform, false);
            warm.transform.localPosition = new Vector3(-4, 5, 7);
            var rim = warm.AddComponent<Light>();
            rim.type = LightType.Point;
            rim.color = new Color(1, .72f, .42f);
            rim.intensity = 14;
            rim.range = 18;
            rim.shadows = LightShadows.None;
            var fillObject = new GameObject(FillLightName);
            fillObject.transform.SetParent(navigationObject.transform, false);
            fillObject.transform.localPosition = new Vector3(0, 3.2f, 1);
            fillObject.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -1.8f, 4.5f));
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Spot;
            fill.color = new Color(.88f, .94f, 1);
            fill.intensity = 36;
            fill.range = 22;
            fill.spotAngle = 85;
            fill.innerSpotAngle = 55;
            fill.shadows = LightShadows.None;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.58f, .68f, .82f);
            RenderSettings.ambientEquatorColor = new Color(.48f, .57f, .7f);
            RenderSettings.ambientGroundColor = new Color(.24f, .29f, .36f);
            RenderSettings.skybox = library.SkyMaterial;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = library.RegionSkies[0];
            RenderSettings.reflectionIntensity = .65f;
            BuildPostProcessing(root.transform);
            EnablePipelineHdr();
            QualitySettings.vSyncCount = 0;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("[Riftline] Game scene saved: " + ScenePath);
        }

        public static void PopulateLibrary(RiftlineAssets library)
        {
            const string art = "Assets/Riftline/Art/Generated/";
            const string audio = "Assets/Riftline/Audio/Generated/";
            library.EvePrefab = Load<GameObject>(art + "EveRigged.prefab") ?? Load<GameObject>(art + "Eve.prefab");
            library.CockpitPrefab = Load<GameObject>(art + "Cockpit.prefab");
            library.PlatformPrefab = Load<GameObject>(art + "Platform.prefab");
            library.CollectiblePrefab = Load<GameObject>(art + "Collectible.prefab");
            library.ControllerPrefab = Load<GameObject>(art + "Controller.prefab");
            library.MenuArtwork = Load<Texture2D>(art + "MenuArtwork.png");
            library.ActionIcons = Load<Texture2D>(art + "ActionIcons.png");
            string[] obstacles = { "Crate", "LowBarrier", "HighBeam", "MovingGate", "Drone", "Gap", "PhaseWall", "Collapse" };
            library.ObstaclePrefabs = new GameObject[8];
            for (int i = 0; i < 8; i++) library.ObstaclePrefabs[i] = Load<GameObject>(art + obstacles[i] + ".prefab");
            string[] regions = { "Shipyard", "Raincity", "Quantum" };
            library.LandmarkPrefabs = new GameObject[3];
            library.RegionMaterials = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                library.LandmarkPrefabs[i] = Load<GameObject>(art + regions[i] + "Landmark.prefab");
                library.RegionMaterials[i] = Load<Material>(art + regions[i] + "RailPBR.mat") ??
                    Load<Material>(art + regions[i] + "Surface.mat");
            }
            string[] skies = { "OrbitalDocksSky", "NeonRainSky", "QuantumRuinsSky" };
            library.RegionSkies = new Cubemap[3];
            for (int i = 0; i < 3; i++) library.RegionSkies[i] = Load<Cubemap>(art + "Skies/" + skies[i] + ".png");
            string skyPath = "Assets/Riftline/Art/RegionalSky.mat";
            library.SkyMaterial = Load<Material>(skyPath);
            if (!library.SkyMaterial)
            {
                library.SkyMaterial = new Material(Shader.Find("Riftline/Regional Sky"));
                AssetDatabase.CreateAsset(library.SkyMaterial, skyPath);
            }
            library.SkyMaterial.SetTexture("_TexA", library.RegionSkies[0]);
            library.SkyMaterial.SetTexture("_TexB", library.RegionSkies[0]);
            library.SkyMaterial.SetFloat("_Exposure", 1.05f);
            library.OutfitMaterials = new Material[8];
            for (int i = 0; i < 8; i++) library.OutfitMaterials[i] = Load<Material>(art + "Outfit" + i + ".mat");
            string[] animations = { "Idle", "Run", "Jump", "Slide", "Phase", "Turn", "Fail", "Victory",
                "Land", "LaneLeft", "LaneRight", "Fall", "Recover", "Start", "FastLand" };
            library.Animations = new AnimationClip[animations.Length];
            for (int i = 0; i < animations.Length; i++) library.Animations[i] = Load<AnimationClip>(art + "Animations/" + animations[i] + ".anim");
            var gait = Load<AnimationClip>(art + "Animations/RunStationary.anim");
            if (gait)
            {
                gait.name = "RunStationary";
                library.Animations[1] = gait;
                EditorUtility.SetDirty(gait);
            }
            library.Music = Load<AudioClip>(audio + "Music.wav");
            library.Sounds = new AudioClip[Enum.GetValues(typeof(FeedbackKind)).Length];
            foreach (FeedbackKind kind in Enum.GetValues(typeof(FeedbackKind)))
            {
                AudioClip clip = Load<AudioClip>(audio + kind + ".wav");
                if (!clip && kind == FeedbackKind.Damage) clip = Load<AudioClip>(audio + "Fail.wav");
                if (!clip && kind == FeedbackKind.Combo) clip = Load<AudioClip>(audio + "Chip.wav");
                library.Sounds[(int)kind] = clip;
            }
        }

        static void BuildPostProcessing(Transform parent)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }

            ConfigurePostProcessing(profile);
            var volumeTransform = parent.Find(VolumeName);
            var volumeObject = volumeTransform ? volumeTransform.gameObject : new GameObject(VolumeName);
            if (!volumeTransform) volumeObject.transform.SetParent(parent, false);
            var volume = volumeObject.GetComponent<Volume>() ?? volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
        }

        public static void RefreshPresentationLighting()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before refreshing presentation lighting.");
            EnsureRuntimeShaders();
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) throw new InvalidOperationException("Open the Riftline scene before refreshing presentation lighting.");
            var root = FindSceneObject(scene, RootName);
            if (!root) throw new InvalidOperationException("The open scene does not contain " + RootName + ".");
            var library = AssetDatabase.LoadAssetAtPath<RiftlineAssets>(LibraryPath);
            if (!library || !library.SkyMaterial)
                throw new InvalidOperationException("The Riftline asset library and sky material must exist before refreshing lighting.");

            ConfigureLight(scene, KeyLightName, 2.6f, new Color(.94f, .97f, 1));
            ConfigureLight(scene, FillLightName, 36, new Color(.88f, .94f, 1));
            ConfigureLight(scene, RimLightName, 14, new Color(1, .72f, .42f));
            library.SkyMaterial.SetFloat("_Exposure", 1.05f);
            EditorUtility.SetDirty(library.SkyMaterial);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.58f, .68f, .82f);
            RenderSettings.ambientEquatorColor = new Color(.48f, .57f, .7f);
            RenderSettings.ambientGroundColor = new Color(.24f, .29f, .36f);
            RenderSettings.skybox = library.SkyMaterial;
            BuildPostProcessing(root.transform);
            EnablePipelineHdr();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Riftline] Presentation lighting refreshed without rebuilding the scene.");
        }

        static void EnsureRuntimeShaders()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets.Length == 0)
                throw new InvalidOperationException("GraphicsSettings asset is unavailable.");

            var settings = new SerializedObject(assets[0]);
            SerializedProperty included = settings.FindProperty("m_AlwaysIncludedShaders");
            foreach (string shaderName in RuntimeShaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (!shader)
                    throw new InvalidOperationException("Required runtime shader is unavailable: " + shaderName);

                bool present = false;
                for (int i = 0; i < included.arraySize; i++)
                {
                    if (included.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        present = true;
                        break;
                    }
                }
                if (present) continue;

                int index = included.arraySize;
                included.InsertArrayElementAtIndex(index);
                included.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(assets[0]);
        }

        public static RiftlineHandControlSettings EnsureHandControlAssets()
        {
            Directory.CreateDirectory(HandGesturePath);
            Directory.CreateDirectory(HandResourcesPath);
            AssetDatabase.Refresh();

            string openPath = HandGesturePath + "/Open Hand Shape.asset";
            string fistPath = HandGesturePath + "/Fist Hand Shape.asset";
            string pausePath = HandGesturePath + "/Open Palm Up.asset";
            CopyAssetIfMissing(GestureSamplePath + "Hand Shapes/Open Hand Shape.asset", openPath);
            CopyAssetIfMissing(GestureSamplePath + "Hand Shapes/Fist Hand Shape.asset", fistPath);
            CopyAssetIfMissing(GestureSamplePath + "Hand Poses/Open Palm Up.asset", pausePath);

            var open = AssetDatabase.LoadAssetAtPath<XRHandShape>(openPath);
            var fist = AssetDatabase.LoadAssetAtPath<XRHandShape>(fistPath);
            var pause = AssetDatabase.LoadAssetAtPath<XRHandPose>(pausePath);
            if (!open || !fist || !pause)
                throw new InvalidOperationException("Production XR Hands gesture assets could not be loaded.");

            if (pause.handShape != open)
            {
                pause.handShape = open;
                EditorUtility.SetDirty(pause);
            }

            var settings = AssetDatabase.LoadAssetAtPath<RiftlineHandControlSettings>(HandSettingsPath);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<RiftlineHandControlSettings>();
                AssetDatabase.CreateAsset(settings, HandSettingsPath);
            }
            settings.Thresholds = HandGestureThresholds.Default;
            settings.OpenShape = open;
            settings.FistShape = fist;
            settings.PausePose = pause;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        static void CopyAssetIfMissing(string source, string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destination)) return;
            if (!AssetDatabase.LoadMainAssetAtPath(source))
                throw new FileNotFoundException("Required XR Hands sample asset is missing.", source);
            if (!AssetDatabase.CopyAsset(source, destination))
                throw new IOException("Could not copy XR Hands sample asset to " + destination);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        }

        static void ConfigurePostProcessing(VolumeProfile profile)
        {
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.Override(1.3f);
            bloom.intensity.Override(.18f);
            bloom.scatter.Override(.5f);
            var tone = GetOrAdd<Tonemapping>(profile);
            tone.active = true;
            tone.mode.Override(TonemappingMode.Neutral);
            var color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.postExposure.Override(.45f);
            color.contrast.Override(4);
            color.saturation.Override(6);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(tone);
            EditorUtility.SetDirty(color);
            EditorUtility.SetDirty(profile);
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static void ConfigureLight(Scene scene, string name, float intensity, Color color)
        {
            var lightObject = FindSceneObject(scene, name);
            var light = lightObject ? lightObject.GetComponent<Light>() : null;
            if (!light) throw new InvalidOperationException("The open scene is missing the named light " + name + ".");
            light.intensity = intensity;
            light.color = color;
            EditorUtility.SetDirty(light);
        }

        static GameObject FindSceneObject(Scene scene, string name)
        {
            foreach (var sceneRoot in scene.GetRootGameObjects())
                foreach (var transform in sceneRoot.GetComponentsInChildren<Transform>(true))
                    if (transform.name == name) return transform.gameObject;
            return null;
        }

        static void EnablePipelineHdr()
        {
            var active = QualitySettings.renderPipeline as UniversalRenderPipelineAsset ??
                GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var defaultPipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            EnablePipelineHdr(active);
            if (defaultPipeline != active) EnablePipelineHdr(defaultPipeline);
        }

        static void EnablePipelineHdr(UniversalRenderPipelineAsset pipeline)
        {
            if (!pipeline) return;
            pipeline.supportsHDR = true;
            EditorUtility.SetDirty(pipeline);
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var exact = AssetDatabase.LoadAssetAtPath<T>(path);
            if (exact) return exact;
            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) return null;
            string stem = Path.GetFileNameWithoutExtension(path);
            foreach (string guid in AssetDatabase.FindAssets(stem, new[] { folder }))
            {
                string found = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(found) != stem) continue;
                var asset = AssetDatabase.LoadAssetAtPath<T>(found);
                if (asset) return asset;
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(found)) if (sub is T typed) return typed;
            }
            return null;
        }
    }
}
