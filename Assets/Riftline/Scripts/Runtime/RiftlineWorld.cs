using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Riftline
{
    public sealed class RiftlineWorld : MonoBehaviour
    {
        struct Corner
        {
            public int Id;
            public float Distance;
            public int Turn;
        }

        sealed class VisibleEvent
        {
            public GameObject Object;
            public ObstacleKind Kind;
        }

        sealed class PickupFlight
        {
            public VisibleEvent View;
            public Vector3 Start;
            public float Began;
        }

        sealed class ScenerySet
        {
            public int Region;
            public readonly List<Transform> Roots = new List<Transform>();
            public readonly List<Material> Materials = new List<Material>();
            public readonly List<Material> Sources = new List<Material>();
            public readonly List<Color> Colors = new List<Color>();
            public bool Fading;
        }

        sealed class GeneratedEffects
        {
            readonly GameObject owner;
            public bool Alive => owner;
            readonly ParticleSystem[] particles;
            readonly float[] rates, distances;
            readonly int[] counts;
            readonly bool[] emissions, particleLights;
            readonly Light[] lights;
            readonly bool[] lightEnabled;
            readonly Animator[] animators;
            readonly float[] speeds;
            bool frozen;
            bool reducedParticles;

            public GeneratedEffects(GameObject root, bool external)
            {
                owner = root;
                particles = root.GetComponentsInChildren<ParticleSystem>(true);
                rates = new float[particles.Length];
                distances = new float[particles.Length];
                counts = new int[particles.Length];
                emissions = new bool[particles.Length];
                particleLights = new bool[particles.Length];
                for (int i = 0; i < particles.Length; i++)
                {
                    rates[i] = particles[i].emission.rateOverTimeMultiplier;
                    distances[i] = particles[i].emission.rateOverDistanceMultiplier;
                    emissions[i] = particles[i].emission.enabled;
                    counts[i] = particles[i].main.maxParticles;
                    particleLights[i] = particles[i].lights.enabled;
                }
                lights = root.GetComponentsInChildren<Light>(true);
                lightEnabled = new bool[lights.Length];
                for (int i = 0; i < lights.Length; i++) lightEnabled[i] = lights[i].enabled;
                animators = external ? root.GetComponentsInChildren<Animator>(true) : new Animator[0];
                speeds = new float[animators.Length];
                for (int i = 0; i < animators.Length; i++) speeds[i] = animators[i].speed;
            }

            public void Apply(GameSettings settings)
            {
                for (int i = 0; i < particles.Length; i++)
                {
                    if (!particles[i]) continue;
                    var emission = particles[i].emission;
                    var main = particles[i].main;
                    var illumination = particles[i].lights;
                    float amount = settings.ReducedParticles ? .25f : 1;
                    emission.enabled = emissions[i];
                    emission.rateOverTimeMultiplier = rates[i] * amount;
                    emission.rateOverDistanceMultiplier = distances[i] * amount;
                    main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(counts[i] * amount));
                    illumination.enabled = particleLights[i] && !settings.ReducedFlash;
                    if (settings.ReducedParticles && !reducedParticles) particles[i].Clear(true);
                }
                reducedParticles = settings.ReducedParticles;
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i]) lights[i].enabled = lightEnabled[i] && !settings.ReducedFlash;
            }

            public void SetRunning(bool running)
            {
                if (frozen == !running) return;
                frozen = !running;
                foreach (var particle in particles)
                {
                    if (!particle || !particle.gameObject.activeInHierarchy) continue;
                    if (frozen && particle.isPlaying) particle.Pause(true);
                    else if (!frozen && particle.isPaused) particle.Play(true);
                }
                for (int i = 0; i < animators.Length; i++)
                    if (animators[i]) animators[i].speed = frozen ? 0 : speeds[i];
            }
        }

        RiftlineGame game;
        Transform outside;
        Transform runner;
        Transform cockpit;
        Transform bossSetPiece;
        int bossRegion = -1;
        RiftlineRunnerAnimation runnerAnimation;
        readonly List<PickupFlight> pickupFlights = new List<PickupFlight>();
        RiftlineEnvironment environment;
        RiftlineRailArchitecture architecture;
        RiftlineOpeningDirector opening;
        readonly List<Transform> road = new List<Transform>();
        ScenerySet currentScenery, incomingScenery;
        readonly List<GeneratedEffects> effects = new List<GeneratedEffects>();
        readonly Dictionary<LineRenderer, Color> lineColors = new Dictionary<LineRenderer, Color>();
        readonly RiftlineAlignment alignment = new RiftlineAlignment();
        readonly List<Corner> corners = new List<Corner>();
        readonly HashSet<int> cornerIds = new HashSet<int>();
        readonly Dictionary<int, VisibleEvent> visibleEvents = new Dictionary<int, VisibleEvent>();
        readonly Dictionary<ObstacleKind, Stack<GameObject>> pool = new Dictionary<ObstacleKind, Stack<GameObject>>();
        readonly List<int> remove = new List<int>();
        Material glow;
        LineRenderer horizon;
        LineRenderer phaseRing;
        Image[] maskEdges;
        Image safetyFade;
        float safetyOpacity;
        int region = -1;
        int outfit = -1;
        int alignmentMode = -1;
        int visualSettingsKey = -1;
        bool seated;
        bool postureKnown;
        bool presentationDirty = true;
        float runnerDistance = -1;
        float terrainOriginHeight;
        float eyeHeight = 1.6f;
        float maskStrength;
        Vector3 cabinOrigin;
        Quaternion cabinRotation = Quaternion.identity;
        Color regionFog = new Color(.04f, .08f, .14f);
        Material regionBlendMaterial;
        MaterialPropertyBlock lineTint;

        public Vector3 RunnerPosition => runner ? runner.position : new Vector3(0, .5f, 5.5f);
        public OpeningFrame Opening => opening ? opening.Frame : default;

        public Pose TrackPoseAt(float distance)
        {
            Vector3 point = PresentedSample(distance, out float yaw);
            return new Pose(outside.TransformPoint(point), outside.rotation * Quaternion.Euler(0, yaw, 0));
        }

        public void Initialize(RiftlineGame owner)
        {
            game = owner;
            environment = gameObject.AddComponent<RiftlineEnvironment>();
            environment.Initialize(game.Assets);
            lineTint = new MaterialPropertyBlock();
            glow = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            glow.color = Color.white;
            outside = NewTransform("Reconstructed Rail World", transform);
            cockpit = NewTransform("Stable Observation Frame", game.PresentationFrame);
            if (game.Assets && game.Assets.CockpitPrefab)
            {
                var shell = Fit(Instantiate(game.Assets.CockpitPrefab, cockpit), new Vector3(4.8f, 1.1f, .65f), false);
                shell.transform.localPosition += new Vector3(0, -.35f, .15f);
                RegisterEffects(shell, false);
            }
            horizon = Line("Stable Horizon", cockpit, .008f, new Color(.08f, .68f, .85f));
            horizon.positionCount = 2;
            horizon.SetPosition(0, new Vector3(-1.55f, .64f, 1.65f));
            horizon.SetPosition(1, new Vector3(1.55f, .64f, 1.65f));
            for (int i = 0; i < 24; i++)
            {
                var deck = NewTransform("Rail Deck " + i, outside);
                if (game.Assets && game.Assets.PlatformPrefab)
                {
                    var tile = Fit(Instantiate(game.Assets.PlatformPrefab, deck), new Vector3(7.8f, .36f, 6.02f), false);
                    tile.transform.localPosition += Vector3.down * .36f;
                    RegisterEffects(tile, true);
                }
                var edges = Line("Rail Boundaries", deck, .025f, new Color(.06f, .57f, .76f));
                edges.positionCount = 8;
                edges.SetPositions(new[] {
                    new Vector3(-3.9f,.02f,-3),new Vector3(-3.9f,.02f,3),
                    new Vector3(-1.3f,.02f,3),new Vector3(-1.3f,.02f,-3),
                    new Vector3(1.3f,.02f,-3),new Vector3(1.3f,.02f,3),
                    new Vector3(3.9f,.02f,3),new Vector3(3.9f,.02f,-3)
                });
                road.Add(deck);
            }
            var avatarRoot = NewTransform("Eve Runner", game.PresentationFrame);
            runner = avatarRoot;
            if (game.Assets && game.Assets.EvePrefab)
            {
                var eve = Fit(Instantiate(game.Assets.EvePrefab, avatarRoot), new Vector3(.7f, 1.55f, .5f), true);
                runnerAnimation = eve.AddComponent<RiftlineRunnerAnimation>();
                runnerAnimation.Initialize(game.Assets.Animations);
                RegisterEffects(eve, false);
            }
            phaseRing = Line("Local Phase Ripple", runner, .025f, new Color(.12f, 1, 1));
            phaseRing.positionCount = 49;
            phaseRing.enabled = false;
            architecture = NewTransform("Dock Architecture", transform).gameObject.AddComponent<RiftlineRailArchitecture>();
            architecture.Initialize(game, this);
            opening = NewTransform("Opening Presentation", transform).gameObject.AddComponent<RiftlineOpeningDirector>();
            opening.Initialize(game, this);
            BuildComfortMask();
            ResetRun();
            ApplySettings();
        }

        public void ResetRun()
        {
            if (architecture) architecture.ResetRun();
            if (opening) opening.ResetRun();
            foreach (var flight in pickupFlights) Release(flight.View);
            pickupFlights.Clear();
            corners.Clear();
            cornerIds.Clear();
            foreach (var pair in visibleEvents) Release(pair.Value);
            visibleEvents.Clear();
            alignment.Reset();
            alignmentMode = game.Save.Settings.AlignmentMode;
            alignment.Configure(alignmentMode, 0);
            if (game.NavigationRig) game.NavigationRig.ResetHeadingPreservingTracking();
            maskStrength = 0;
            terrainOriginHeight = 0;
            outside.localRotation = Quaternion.identity;
            outside.localPosition = Vector3.zero;
            DestroyScenery(currentScenery);
            DestroyScenery(incomingScenery);
            currentScenery = incomingScenery = null;
            if (bossSetPiece) Destroy(bossSetPiece.gameObject);
            bossSetPiece = null;
            bossRegion = -1;
            region = -1;
            presentationDirty = true;
        }

        public void ApplySettings()
        {
            var settings = game.Save.Settings;
            RenderSettings.fog = !settings.ReducedFog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = regionFog;
            RenderSettings.fogDensity = .0045f;
            if (!postureKnown || seated != settings.Seated)
            {
                if (game.Phase != RunPhase.Running && game.ViewCamera)
                {
                    seated = settings.Seated;
                    postureKnown = true;
                    Recenter(game.ViewCamera.transform);
                }
            }
            // Change navigation behavior between runs; a paused setting must not turn the wearer.
            if (game.Phase == RunPhase.Menu && alignmentMode != settings.AlignmentMode)
            {
                alignmentMode = settings.AlignmentMode;
                alignment.Configure(alignmentMode, game.Run.Heading);
                presentationDirty = true;
            }
            if (!Mathf.Approximately(runnerDistance, settings.RunnerDistance))
            {
                runnerDistance = settings.RunnerDistance;
                presentationDirty = true;
            }
            int key = (settings.HighContrast ? 1 : 0) | (settings.ReducedFlash ? 2 : 0) |
                (settings.ReducedParticles ? 4 : 0);
            if (key != visualSettingsKey)
            {
                visualSettingsKey = key;
                foreach (var pair in lineColors) ApplyLineSettings(pair.Key, pair.Value);
                foreach (var effect in effects) effect.Apply(settings);
            }
            if (!game.Assets) return;
            int equipped = game.Save.EquippedOutfit;
            if (outfit != equipped && runner)
            {
                outfit = equipped;
                ColorUtility.TryParseHtmlString(game.Catalog.Outfits[equipped].ColorHex, out Color tint);
                RiftlineAppearance.Apply(runner.gameObject, equipped, tint);
            }
        }

        public void Recenter(Transform camera)
        {
            if (game.NavigationRig) game.NavigationRig.Calibrate(camera);
            Vector3 head = game.PresentationFrame.InverseTransformPoint(camera.position);
            cabinOrigin = game.NavigationRig ? game.NavigationRig.transform.localPosition : new Vector3(head.x, 0, head.z);
            eyeHeight = head.y;
            cabinRotation = game.NavigationRig ? game.NavigationRig.WorldFrameRotation : Quaternion.identity;
            cockpit.localPosition = Vector3.up * (eyeHeight - 1.6f);
            cockpit.localRotation = Quaternion.identity;
            var ui = GetComponentInChildren<RiftlineUI>();
            if (ui)
            {
                ui.transform.localPosition = new Vector3(0, RiftlineRuntimeMath.MenuHeight(eyeHeight), 1.5f);
                ui.transform.localRotation = Quaternion.identity;
            }
            presentationDirty = true;
        }

        public Pose ControllerFallbackPose(bool left)
        {
            Vector3 local = new Vector3(left ? -.42f : .42f, eyeHeight - .65f, .85f);
            return new Pose(game.PresentationFrame.TransformPoint(local), game.PresentationFrame.rotation);
        }

        public void Present(float dt)
        {
            var run = game.Run;
            ApplySettings();
            bool running = run.Phase == RunPhase.Running;
            alignment.Advance(run.Heading, dt, running);
            if (game.NavigationRig) game.NavigationRig.SetHeading(alignment.Heading);
            if (running || presentationDirty)
            {
                presentationDirty = false;
                RememberCorners(run);
                bool active = run.Phase != RunPhase.Menu;
                float distance = active ? run.Distance : 0;
                if (running) terrainOriginHeight = Mathf.MoveTowards(terrainOriginHeight, run.GroundHeight, dt * .15f);
                float effectiveYaw = alignment.Heading;
                Vector3 runnerWorld = PresentedSample(distance, out float runnerYaw);
                RiftlineRuntimeMath.NavigationCenter(runnerWorld.x, runnerWorld.z, effectiveYaw, runnerDistance,
                    out float navigationX, out float navigationZ);
                var navigation = new Vector3(navigationX, 0, navigationZ);
                Quaternion worldRotation = cabinRotation;
                // The tangent-back anchor is continuous at corners and keeps the runner forward.
                outside.localRotation = worldRotation;
                outside.localPosition = cabinOrigin + Vector3.up * (RiftlineRuntimeMath.RunnerBaseHeight(eyeHeight) - terrainOriginHeight) -
                    worldRotation * navigation;
                float roadStart = Mathf.Floor((distance - 12) / 6) * 6;
                for (int i = 0; i < road.Count; i++)
                {
                    float at = Mathf.Max(0, roadStart + i * 6);
                    road[i].localPosition = PresentedSample(at, out float yaw);
                    float before = GroundAt(at - 3), after = GroundAt(at + 3);
                    float pitch = -Mathf.Atan2(after - before, 6) * Mathf.Rad2Deg;
                    road[i].localRotation = Quaternion.Euler(pitch, yaw, 0);
                }
                Vector3 runnerLocal = new Vector3(active ? run.LaneX : 0,
                        eyeHeight - 1.1f + run.Height + run.GroundHeight - terrainOriginHeight, runnerDistance);
                runner.localPosition = runnerLocal;
                Quaternion avatarRotation = game.PresentationFrame.rotation *
                    Quaternion.Euler(0, runnerYaw - effectiveYaw, 0);
                runner.rotation = Quaternion.RotateTowards(runner.rotation, avatarRotation, dt * 360);
                PresentEvents(run, distance);
                PresentRegion(run);
                PresentScenery(currentScenery, distance);
                PresentScenery(incomingScenery, distance);
                PresentBoss(run, distance);
            }
            // Terminal panels must remain clear even when a hazard was close on the last running frame.
            // Paused/countdown views retain their frozen geometry for orientation and resuming.
            bool showEvents = run.Phase != RunPhase.Menu && run.Phase != RunPhase.Completed && run.Phase != RunPhase.Failed;
            foreach (var view in visibleEvents.Values)
                if (view.Object.activeSelf != showEvents) view.Object.SetActive(showEvents);
            if (runnerAnimation) runnerAnimation.Present(run, dt);
            architecture.Present(run);
            opening.Present(run, dt);
            PresentPickupFlights(run);
            effects.RemoveAll(effect => !effect.Alive);
            foreach (var effect in effects) effect.SetRunning(running);
            PresentPhase(run);
            PresentMask(run, Time.unscaledDeltaTime);
        }

        Vector3 PresentedSample(float distance, out float yaw)
        {
            if (alignmentMode != 2) return Sample(distance, out yaw);
            // Fixed mode unfolds the chosen route into a nonrotating, forward corridor.
            yaw = 0;
            return new Vector3(0, GroundAt(distance), distance);
        }

        void RememberCorners(RunSimulation run)
        {
            foreach (var item in run.Events)
            {
                if ((item.Kind == ObstacleKind.TurnLeft || item.Kind == ObstacleKind.TurnRight) && cornerIds.Add(item.Id))
                    corners.Add(new Corner { Id = item.Id, Distance = item.Distance, Turn = item.Kind == ObstacleKind.TurnLeft ? -90 : 90 });
            }
            corners.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        Vector3 Sample(float distance, out float yaw)
        {
            float previous = 0;
            var point = Vector3.zero;
            yaw = 0;
            foreach (var corner in corners)
            {
                if (corner.Distance > distance) break;
                point += Quaternion.Euler(0, yaw, 0) * Vector3.forward * (corner.Distance - previous);
                previous = corner.Distance;
                yaw += corner.Turn;
            }
            point += Quaternion.Euler(0, yaw, 0) * Vector3.forward * (distance - previous);
            point.y = GroundAt(distance);
            return point;
        }

        float GroundAt(float distance)
        {
            TrackEvent profile = game.Run.GroundProfile;
            float latest = profile == null ? float.NegativeInfinity : profile.GroundTransitionStartDistance;
            foreach (TrackEvent item in game.Run.Events)
            {
                if ((item.Kind != ObstacleKind.Rise && item.Kind != ObstacleKind.Fall) ||
                    item.GroundTransitionStartDistance > distance || item.GroundTransitionStartDistance <= latest) continue;
                profile = item;
                latest = item.GroundTransitionStartDistance;
            }
            return profile == null ? 0 : profile.GroundHeightAtDistance(distance);
        }

        void PresentEvents(RunSimulation run, float distance)
        {
            remove.Clear();
            foreach (var pair in visibleEvents)
            {
                bool keep = false;
                foreach (var item in run.Events)
                    if (item.Id == pair.Key && item.Distance > distance - 8 && item.Distance < distance + 120 &&
                        IsPresentedEvent(run, item) &&
                        !(item.Resolved && (item.Kind == ObstacleKind.Chip || item.Kind == ObstacleKind.Archive)))
                    { keep = true; break; }
                if (!keep) remove.Add(pair.Key);
            }
            foreach (int id in remove)
            {
                VisibleEvent view = visibleEvents[id];
                bool collected = false;
                if (view.Kind == ObstacleKind.Chip || view.Kind == ObstacleKind.Archive)
                    foreach (TrackEvent item in run.Events)
                        if (item.Id == id && item.Resolved) { collected = true; break; }
                if (collected)
                {
                    view.Object.transform.SetParent(game.PresentationFrame, true);
                    pickupFlights.Add(new PickupFlight { View = view, Start = view.Object.transform.localPosition, Began = run.Elapsed });
                }
                else Release(view);
                visibleEvents.Remove(id);
            }
            foreach (var item in run.Events)
            {
                if (item.Distance < distance - 8 || item.Distance > distance + 120 ||
                    !IsPresentedEvent(run, item) ||
                    (item.Resolved && (item.Kind == ObstacleKind.Chip || item.Kind == ObstacleKind.Archive))) continue;
                if (!visibleEvents.TryGetValue(item.Id, out VisibleEvent view))
                {
                    view = Acquire(item.Kind);
                    visibleEvents.Add(item.Id, view);
                }
                Vector3 center = PresentedSample(item.Distance + item.Depth * .5f, out float yaw);
                float x = (Mathf.Clamp(item.Lane, 0, 2) - 1) * 2.6f;
                if (item.Kind == ObstacleKind.TurnLeft || item.Kind == ObstacleKind.TurnRight ||
                    item.Kind == ObstacleKind.Fork || item.Kind == ObstacleKind.Boss) x = 0;
                if (item.Kind == ObstacleKind.MovingGate)
                    x += Mathf.Sin(run.Elapsed * .75f + item.Id) * .32f;
                float y = item.Kind == ObstacleKind.Chip || item.Kind == ObstacleKind.Archive ? .75f :
                    item.Kind == ObstacleKind.HighBeam ? 1.2f : item.Kind == ObstacleKind.Drone ? 1.1f : 0;
                view.Object.transform.localPosition = center + Quaternion.Euler(0, yaw, 0) * new Vector3(x, y, 0);
                view.Object.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                if (item.Kind == ObstacleKind.Chip || item.Kind == ObstacleKind.Archive)
                    view.Object.transform.Rotate(0, run.Elapsed * 75, 0, Space.Self);
            }
        }

        static bool IsPresentedEvent(RunSimulation run, TrackEvent item)
        {
            // The observer follows behind Eve. Cleared hazards must leave the presentation
            // at the runner, before scrolling into the observer's near plane.
            if (item.Passed) return false;
            if (!run.IsEventActive(item)) return false;
            if (item.ForkId < 0) return true;
            // Branch payload appears after selection; preview does not stack conflicting lanes.
            foreach (var fork in run.Events)
                if (fork.Id == item.ForkId) return fork.Resolved;
            return true;
        }

        VisibleEvent Acquire(ObstacleKind kind)
        {
            if (pool.TryGetValue(kind, out var stack) && stack.Count > 0)
            {
                var cached = stack.Pop();
                cached.SetActive(true);
                return new VisibleEvent { Object = cached, Kind = kind };
            }
            var root = new GameObject(kind.ToString());
            root.transform.SetParent(outside, false);
            GameObject prefab = null;
            if (game.Assets)
            {
                int index = (int)kind;
                if (index >= 0 && index < 8 && game.Assets.ObstaclePrefabs != null && index < game.Assets.ObstaclePrefabs.Length)
                    prefab = game.Assets.ObstaclePrefabs[index];
                if (kind == ObstacleKind.Chip || kind == ObstacleKind.Archive) prefab = game.Assets.CollectiblePrefab;
                if (kind == ObstacleKind.Boss && game.Assets.LandmarkPrefabs != null &&
                    game.Assets.LandmarkPrefabs.Length > 0 && region >= 0)
                    prefab = game.Assets.LandmarkPrefabs[Mathf.Clamp(region, 0, game.Assets.LandmarkPrefabs.Length - 1)];
            }
            if (prefab)
            {
                Vector3 size = kind == ObstacleKind.Chip ? Vector3.one * .3f :
                    kind == ObstacleKind.Archive ? Vector3.one * .55f :
                    kind == ObstacleKind.HighBeam ? new Vector3(2.35f, .45f, .8f) :
                    kind == ObstacleKind.LowBarrier ? new Vector3(2.25f, .65f, .45f) :
                    kind == ObstacleKind.Drone ? new Vector3(1.6f, .65f, .8f) :
                    kind == ObstacleKind.Gap || kind == ObstacleKind.Collapse ? new Vector3(2.5f, .15f, 2f) :
                    kind == ObstacleKind.Boss ? new Vector3(15, 12, 8) : new Vector3(2.05f, 1.55f, 1f);
                var mesh = Fit(Instantiate(prefab, root.transform), size, false);
                RegisterEffects(mesh, true);
            }
            if (kind == ObstacleKind.TurnLeft || kind == ObstacleKind.TurnRight || kind == ObstacleKind.Fork)
                AddDirectionIndicator(root.transform, kind);
            else if (kind <= ObstacleKind.Collapse)
                AddDangerIndicator(root.transform, kind);
            else if (kind == ObstacleKind.Chip || kind == ObstacleKind.Archive)
            {
                LineRenderer halo = Line("Collectible Halo", root.transform, .018f,
                    kind == ObstacleKind.Chip ? new Color(2f, 1.1f, .25f) : new Color(.2f, 1.6f, 2f));
                halo.positionCount = 25;
                float radius = kind == ObstacleKind.Chip ? .2f : .35f;
                for (int i = 0; i < halo.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / 24f;
                    halo.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
                }
            }
            return new VisibleEvent { Object = root, Kind = kind };
        }

        void PresentPickupFlights(RunSimulation run)
        {
            for (int i = pickupFlights.Count - 1; i >= 0; i--)
            {
                PickupFlight flight = pickupFlights[i];
                float age = run.Elapsed - flight.Began;
                bool terminal = run.Phase == RunPhase.Menu || run.Phase == RunPhase.Failed || run.Phase == RunPhase.Completed;
                if (terminal || age >= .28f)
                {
                    Release(flight.View);
                    pickupFlights.RemoveAt(i);
                    continue;
                }
                if (run.Phase != RunPhase.Running) continue;
                float t = Mathf.Clamp01(age / .28f);
                float eased = 1f - (1f - t) * (1f - t);
                flight.View.Object.transform.localPosition = Vector3.Lerp(flight.Start, runner.localPosition + Vector3.up * .8f, eased)
                    + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .2f);
                flight.View.Object.transform.localScale = Vector3.one * Mathf.Lerp(1f, .15f, t);
            }
        }

        void Release(VisibleEvent view)
        {
            view.Object.SetActive(false);
            view.Object.transform.SetParent(outside, false);
            view.Object.transform.localScale = Vector3.one;
            if (!pool.TryGetValue(view.Kind, out var stack)) pool[view.Kind] = stack = new Stack<GameObject>();
            stack.Push(view.Object);
        }

        void AddDirectionIndicator(Transform parent, ObstacleKind kind)
        {
            if (kind == ObstacleKind.Fork)
            {
                AddDirectionIndicator(parent, ObstacleKind.TurnLeft);
                AddDirectionIndicator(parent, ObstacleKind.TurnRight);
                return;
            }
            var indicator = Line("Exit Direction", parent, .09f, new Color(1, .65f, .13f));
            float sign = kind == ObstacleKind.TurnLeft ? -1 : 1;
            indicator.positionCount = 5;
            indicator.SetPositions(new[] {
                new Vector3(-sign*1.1f,2.8f,0),new Vector3(sign*1.1f,2.8f,0),
                new Vector3(sign*.35f,3.4f,0),new Vector3(sign*1.1f,2.8f,0),
                new Vector3(sign*.35f,2.2f,0)
            });
        }

        void AddDangerIndicator(Transform parent, ObstacleKind kind)
        {
            var warning = Line("Danger Boundary", parent, .028f,
                kind == ObstacleKind.PhaseWall ? new Color(.05f, 1, 1) : new Color(1, .4f, .06f));
            warning.positionCount = 5;
            warning.SetPositions(new[] { new Vector3(-1.15f,.025f,-.65f),new Vector3(-1.15f,.025f,.65f),
                new Vector3(1.15f,.025f,.65f),new Vector3(1.15f,.025f,-.65f),new Vector3(-1.15f,.025f,-.65f)});
        }

        void PresentRegion(RunSimulation run)
        {
            int from = Mathf.Clamp(run.RegionId, 0, 2);
            int to = from;
            float blend = 0;
            if (run.Endless) RiftlineRuntimeMath.EndlessRegion(run.Elapsed, out from, out to, out blend);
            region = from;
            if (currentScenery == null || currentScenery.Region != from)
            {
                DestroyScenery(currentScenery);
                currentScenery = incomingScenery != null && incomingScenery.Region == from ? incomingScenery : CreateScenery(from);
                if (incomingScenery != currentScenery) DestroyScenery(incomingScenery);
                incomingScenery = null;
            }
            if (to != from && (incomingScenery == null || incomingScenery.Region != to))
            {
                DestroyScenery(incomingScenery);
                incomingScenery = CreateScenery(to);
            }
            SetSceneryOpacity(currentScenery, 1 - blend);
            SetSceneryOpacity(incomingScenery, blend);
            regionFog = Color.Lerp(RegionFog(from), RegionFog(to), blend);
            RenderSettings.fogColor = regionFog;
            game.ViewCamera.backgroundColor = regionFog;
            environment.Present(from, to, blend);
            if (game.Assets && game.Assets.RegionMaterials != null)
            {
                var materials = game.Assets.RegionMaterials;
                Material source = from < materials.Length ? materials[from] : null;
                Material target = to < materials.Length ? materials[to] : null;
                if (source)
                {
                    if (!regionBlendMaterial)
                    {
                        regionBlendMaterial = new Material(source);
                        foreach (var deck in road)
                            foreach (var renderer in deck.GetComponentsInChildren<MeshRenderer>())
                                renderer.sharedMaterial = regionBlendMaterial;
                    }
                    if (regionBlendMaterial.shader != source.shader) regionBlendMaterial.shader = source.shader;
                    regionBlendMaterial.CopyPropertiesFromMaterial(source);
                    if (target && target.shader == source.shader) regionBlendMaterial.Lerp(source, target, blend);
                    regionBlendMaterial.SetTextureScale("_BaseMap", new Vector2(3, 2));
                    regionBlendMaterial.SetColor("_BaseColor", new Color(.72f, .76f, .82f));
                    regionBlendMaterial.SetFloat("_Smoothness", from == 1 ? .5f : .3f);
                }
            }
        }

        ScenerySet CreateScenery(int id)
        {
            var set = new ScenerySet { Region = id };
            if (!game.Assets || game.Assets.LandmarkPrefabs == null ||
                id >= game.Assets.LandmarkPrefabs.Length || !game.Assets.LandmarkPrefabs[id]) return set;
            for (int i = 0; i < 6; i++)
            {
                var root = NewTransform("Region " + id + " Landmark " + i, outside);
                float size = i < 2 ? 8 : i < 4 ? 13 : 19;
                var mesh = Fit(Instantiate(game.Assets.LandmarkPrefabs[id], root), new Vector3(size, size, size), true);
                RegisterEffects(mesh, true);
                foreach (var renderer in mesh.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    var sources = renderer.sharedMaterials;
                    var materials = renderer.materials;
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        var material = materials[materialIndex];
                        string colorProperty = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                        Color color = material.HasProperty(colorProperty) ? material.GetColor(colorProperty) : Color.white;
                        set.Materials.Add(material);
                        set.Sources.Add(sources[materialIndex]);
                        set.Colors.Add(color);
                    }
                    renderer.sharedMaterials = materials;
                }
                set.Roots.Add(root);
            }
            return set;
        }

        static Color RegionFog(int id) => id == 0 ? new Color(.04f, .08f, .14f) :
            id == 1 ? new Color(.09f, .065f, .16f) : new Color(.16f, .22f, .25f);

        static Color RegionAmbient(int id) => id == 2 ? new Color(.42f, .42f, .42f) : new Color(.24f, .30f, .40f);

        static void SetSceneryOpacity(ScenerySet set, float opacity)
        {
            if (set == null) return;
            bool fading = opacity > .0001f && opacity < .9999f;
            if (set.Fading != fading)
            {
                set.Fading = fading;
                for (int i = 0; i < set.Materials.Count; i++)
                {
                    Material material = set.Materials[i];
                    material.CopyPropertiesFromMaterial(set.Sources[i]);
                    if (!fading) continue;
                    if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1);
                    if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0);
                    if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.renderQueue = (int)RenderQueue.Transparent;
                }
            }
            for (int i = 0; i < set.Materials.Count; i++)
            {
                Material material = set.Materials[i];
                Color color = set.Colors[i];
                color.a *= opacity;
                string property = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                if (material.HasProperty(property)) material.SetColor(property, color);
            }
            foreach (var root in set.Roots) root.gameObject.SetActive(opacity > .0001f);
        }

        void DestroyScenery(ScenerySet set)
        {
            if (set == null) return;
            foreach (var root in set.Roots) if (root) Destroy(root.gameObject);
            foreach (var material in set.Materials) if (material) Destroy(material);
        }

        void PresentScenery(ScenerySet set, float distance)
        {
            if (set == null) return;
            for (int i = 0; i < set.Roots.Count; i++)
            {
                float at = Mathf.Floor((distance - 18) / 90) * 90 + 36 + i * 38;
                var center = PresentedSample(at, out float yaw);
                float side = (i % 2 == 0 ? -1 : 1) * (i < 2 ? 14 : i < 4 ? 23 : 38);
                set.Roots[i].localPosition = center + Quaternion.Euler(0, yaw, 0) * new Vector3(side, -2, 0);
                set.Roots[i].localRotation = Quaternion.Euler(0, yaw + (i % 2 == 0 ? 18 : -24), 0);
            }
        }

        void PresentBoss(RunSimulation run, float distance)
        {
            if (!run.BossActive)
            {
                if (bossSetPiece) bossSetPiece.gameObject.SetActive(false);
                return;
            }
            if (!game.Assets || game.Assets.LandmarkPrefabs == null) return;
            int id = Mathf.Clamp(run.RegionId, 0, game.Assets.LandmarkPrefabs.Length - 1);
            if (!game.Assets.LandmarkPrefabs[id]) return;
            if (!bossSetPiece || bossRegion != id)
            {
                if (bossSetPiece) Destroy(bossSetPiece.gameObject);
                bossRegion = id;
                bossSetPiece = NewTransform("Boss Encounter " + id, outside);
                var mesh = Fit(Instantiate(game.Assets.LandmarkPrefabs[id], bossSetPiece),
                    id == 0 ? new Vector3(24, 16, 14) : id == 1 ? new Vector3(28, 20, 15) : new Vector3(25, 22, 15), false);
                foreach (var renderer in mesh.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                RegisterEffects(mesh, true);
            }
            bossSetPiece.gameObject.SetActive(true);
            float progress = Mathf.Clamp01((run.Elapsed - (run.ActiveContract.DurationSeconds - 45)) / 45);
            var center = PresentedSample(distance + 65, out float yaw);
            Vector3 offset = id == 0 ? new Vector3(Mathf.Sin(progress * Mathf.PI * 2) * 9, 3, 0) :
                id == 1 ? new Vector3(Mathf.Sin(progress * Mathf.PI * 4) * 12, 5, 0) :
                new Vector3(0, Mathf.Lerp(14, 2, progress), 0);
            bossSetPiece.localPosition = center + Quaternion.Euler(0, yaw, 0) * offset;
            bossSetPiece.localRotation = Quaternion.Euler(id == 0 ? Mathf.Sin(progress * Mathf.PI) * 18 : 0,
                yaw + (id == 1 ? progress * 180 : id == 2 ? progress * 35 : 0), 0);
        }

        void PresentPhase(RunSimulation run)
        {
            phaseRing.enabled = run.PhaseRemaining > 0;
            if (!phaseRing.enabled) return;
            float radius = game.Save.Settings.ReducedFlash ? .8f : .7f + (1 - run.PhaseRemaining / .8f) * .5f;
            for (int i = 0; i < 49; i++)
            {
                float angle = i * Mathf.PI * 2 / 48;
                phaseRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, .8f + Mathf.Sin(angle) * radius, 0));
            }
        }

        void BuildComfortMask()
        {
            var go = new GameObject("External World Comfort Mask", typeof(Canvas));
            go.transform.SetParent(cockpit, false);
            go.transform.localPosition = new Vector3(0, 1.6f, .7f);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = game.ViewCamera;
            canvas.sortingOrder = -10;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(2.2f, 1.6f);
            var fade = new GameObject("Paused World Fade", typeof(RectTransform), typeof(Image));
            fade.transform.SetParent(go.transform, false);
            safetyFade = fade.GetComponent<Image>();
            safetyFade.raycastTarget = false;
            safetyFade.rectTransform.anchorMin = Vector2.zero;
            safetyFade.rectTransform.anchorMax = Vector2.one;
            safetyFade.rectTransform.offsetMin = safetyFade.rectTransform.offsetMax = Vector2.zero;
            safetyFade.color = new Color(.005f, .012f, .025f, 0);
            maskEdges = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var edge = new GameObject("Peripheral Mask " + i, typeof(RectTransform), typeof(Image));
                edge.transform.SetParent(go.transform, false);
                var image = edge.GetComponent<Image>();
                image.color = new Color(.005f, .012f, .025f, 0);
                image.raycastTarget = false;
                maskEdges[i] = image;
            }
        }

        void PresentMask(RunSimulation run, float dt)
        {
            float preset = game.Save.Settings.ComfortMask / 3f;
            float target = preset * (alignment.Age < .65f ? .35f : .08f);
            if (run.Phase == RunPhase.Failed) target = .35f;
            maskStrength = Mathf.MoveTowards(maskStrength, target, dt / .15f);
            for (int i = 0; i < 4; i++)
            {
                var edge = maskEdges[i].rectTransform;
                float thickness = Mathf.Lerp(.01f, .25f, maskStrength / .35f);
                edge.anchorMin = edge.anchorMax = new Vector2(.5f, .5f);
                edge.sizeDelta = i < 2 ? new Vector2(thickness, 1.6f) : new Vector2(2.2f, thickness * .7f);
                edge.anchoredPosition = i == 0 ? new Vector2(-1.1f + thickness / 2, 0) :
                    i == 1 ? new Vector2(1.1f - thickness / 2, 0) :
                    i == 2 ? new Vector2(0, .8f - thickness * .35f) : new Vector2(0, -.8f + thickness * .35f);
                maskEdges[i].color = new Color(.005f, .012f, .025f, maskStrength / .35f * .95f);
            }
            float fadeTarget = game.TrackingSuspended ? .6f : run.Phase == RunPhase.Paused ? .3f :
                run.Phase == RunPhase.Failed ? .25f : 0;
            safetyOpacity = Mathf.MoveTowards(safetyOpacity, fadeTarget, dt / .15f);
            safetyFade.color = new Color(.005f, .012f, .025f, safetyOpacity);
        }

        static Transform NewTransform(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        LineRenderer Line(string name, Transform parent, float width, Color color)
        {
            var line = NewTransform(name, parent).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = glow;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCornerVertices = 2;
            lineColors.Add(line, color);
            ApplyLineSettings(line, color);
            return line;
        }

        void ApplyLineSettings(LineRenderer line, Color original)
        {
            if (!line) return;
            Color color = original;
            if (game.Save.Settings.HighContrast)
            {
                color = original.r > .7f ? new Color(1, .85f, .15f) : new Color(.25f, 1, 1);
                line.widthMultiplier = 1.4f;
            }
            else line.widthMultiplier = 1;
            line.startColor = line.endColor = color;
            line.GetPropertyBlock(lineTint);
            lineTint.SetColor("_BaseColor", color);
            line.SetPropertyBlock(lineTint);
        }

        void RegisterEffects(GameObject asset, bool external)
        {
            var effect = new GeneratedEffects(asset, external);
            effect.Apply(game.Save.Settings);
            effects.Add(effect);
        }

        public static GameObject Fit(GameObject asset, Vector3 size, bool uniform)
        {
            // Generated prefabs retain their exporter axis conversion on the root.
            // Scale a neutral parent so nonuniform dimensions remain in presentation axes.
            var fitted = NewTransform(asset.name + " Presentation", asset.transform.parent).gameObject;
            asset.transform.SetParent(fitted.transform, false);
            var renderers = asset.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return fitted;
            Bounds bounds = new Bounds();
            bool initialized = false;
            foreach (var renderer in renderers)
            {
                if (renderer is LineRenderer || renderer is ParticleSystemRenderer) continue;
                Bounds local = renderer.localBounds;
                Matrix4x4 relative = fitted.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    point = relative.MultiplyPoint3x4(point);
                    if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!initialized) return fitted;
            Vector3 raw = bounds.size;
            Vector3 scale = new Vector3(size.x / Mathf.Max(.01f, raw.x),
                size.y / Mathf.Max(.01f, raw.y), size.z / Mathf.Max(.01f, raw.z));
            if (uniform) scale = Vector3.one * scale.y;
            fitted.transform.localScale = scale;
            fitted.transform.localPosition = -Vector3.Scale(bounds.center, scale) + new Vector3(0, size.y / 2, 0);
            return fitted;
        }

        void OnDestroy()
        {
            if (glow) Destroy(glow);
            if (regionBlendMaterial) Destroy(regionBlendMaterial);
            DestroyScenery(currentScenery);
            DestroyScenery(incomingScenery);
        }
    }
}
