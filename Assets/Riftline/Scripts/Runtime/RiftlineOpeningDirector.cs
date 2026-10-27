using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Riftline
{
    /// <summary>Simulation-clock-only pursuit, extraction and local physical feedback.</summary>
    public sealed class RiftlineOpeningDirector : MonoBehaviour
    {
        readonly RiftlineOpeningPresentation presentation = new RiftlineOpeningPresentation();
        readonly Vector3[] ringPoints = new Vector3[49];
        RiftlineGame game;
        RiftlineWorld world;
        RiftlineAudio audioFeedback;
        RunSimulation observed;
        Transform pursuer, extraction;
        LineRenderer trail, response, landing;
        Light beacon;
        Material glow, exitGlow;
        Mesh archMesh;
        float previousElapsed = -1f, previousLandingPulse;
        bool dirty = true;
        public OpeningFrame Frame { get; private set; }

        public void Initialize(RiftlineGame owner, RiftlineWorld rail)
        {
            game = owner;
            world = rail;
            audioFeedback = game.GetComponent<RiftlineAudio>();
            glow = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Opening Local Energy" };
            glow.SetColor("_BaseColor", Color.white);
            glow.SetFloat("_Surface", 1);
            glow.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glow.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glow.SetFloat("_ZWrite", 0);
            glow.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glow.renderQueue = (int)RenderQueue.Transparent;
            exitGlow = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Extraction Beacons" };
            exitGlow.color = new Color(.12f, .85f, 1f);
            pursuer = new GameObject("Security Pursuer").transform;
            pursuer.SetParent(transform, false);
            GameObject prefab = game.Assets && game.Assets.ObstaclePrefabs != null &&
                game.Assets.ObstaclePrefabs.Length > (int)ObstacleKind.Drone ?
                game.Assets.ObstaclePrefabs[(int)ObstacleKind.Drone] : null;
            if (prefab)
            {
                var model = RiftlineWorld.Fit(Instantiate(prefab, pursuer), new Vector3(2f, 1.6f, 1f), true);
                model.transform.localPosition -= Vector3.up * .8f;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
                // External particle clocks are intentionally not used by this presenter.
                foreach (var particle in model.GetComponentsInChildren<ParticleSystem>(true)) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            }
            beacon = pursuer.gameObject.AddComponent<Light>();
            beacon.type = LightType.Point;
            beacon.range = 5f;
            beacon.shadows = LightShadows.None;
            trail = Line("Pursuer Wake", 2, .06f);
            response = Line("Runner Energy Response", 49, .035f);
            landing = Line("Landing Contact", 49, .055f);
            extraction = new GameObject("Extraction Arch").transform;
            extraction.SetParent(transform, false);
            archMesh = RiftlineRailArchitecture.BuildBoxes("Extraction Shared Arch", new List<Bounds> {
                new Bounds(new Vector3(-4.75f, 2.4f, 0), new Vector3(.16f, 5.3f, .25f)),
                new Bounds(new Vector3(4.75f, 2.4f, 0), new Vector3(.16f, 5.3f, .25f)),
                new Bounds(new Vector3(0, 5.03f, 0), new Vector3(9.66f, .16f, .25f)),
                new Bounds(new Vector3(-5.1f, .1f, 0), new Vector3(.6f, .35f, 1.1f)),
                new Bounds(new Vector3(5.1f, .1f, 0), new Vector3(.6f, .35f, 1.1f))
            });
            RiftlineRailArchitecture.CreateSurface("Extraction Beacons", extraction, archMesh, exitGlow);
            ResetRun();
        }

        LineRenderer Line(string name, int count, float width)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            var line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = glow;
            line.useWorldSpace = true;
            line.positionCount = count;
            line.startWidth = line.endWidth = width;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        public void ResetRun()
        {
            if (observed != null) observed.Feedback -= Feedback;
            observed = null;
            presentation.Reset();
            Frame = default;
            previousElapsed = -1f;
            previousLandingPulse = 0f;
            dirty = true;
            if (pursuer) pursuer.gameObject.SetActive(false);
            if (extraction) extraction.gameObject.SetActive(false);
            if (trail) trail.enabled = false;
            if (response) response.enabled = false;
            if (landing) landing.enabled = false;
        }

        void Feedback(FeedbackKind kind) => presentation.RecordFeedback(kind, observed.Elapsed);

        public void Present(RunSimulation run, float dt)
        {
            if (observed != run)
            {
                ResetRun();
                observed = run;
                observed.Feedback += Feedback;
            }
            Frame = presentation.Evaluate(run);
            bool visible = Frame.Active;
            pursuer.gameObject.SetActive(visible);
            if (!visible)
            {
                extraction.gameObject.SetActive(false);
                trail.enabled = response.enabled = landing.enabled = false;
                previousLandingPulse = 0;
                return;
            }
            bool running = run.Phase == RunPhase.Running;
            if (dirty || running || previousElapsed != run.Elapsed)
            {
                dirty = false;
                previousElapsed = run.Elapsed;
                Pose rail = world.TrackPoseAt(run.Distance + Mathf.Lerp(11f, 4f, Frame.Threat));
                Vector3 offset = new Vector3(4.9f + Frame.PhasePulse * 1.1f + Frame.ComboPulse * .5f,
                    3.05f + Mathf.Sin(run.Elapsed * 2.4f) * .13f, 0);
                pursuer.SetPositionAndRotation(rail.position + rail.rotation * offset,
                    rail.rotation * Quaternion.Euler(0, -150f, Mathf.Sin(run.Elapsed * 2f) * 5f));
                trail.SetPosition(0, pursuer.position);
                trail.SetPosition(1, pursuer.position + rail.rotation * new Vector3(.1f, -.05f, -2.2f));
                if (!run.Endless && run.ActiveContract != null)
                {
                    Pose exit = world.TrackPoseAt(run.ActiveContract.Length);
                    extraction.SetPositionAndRotation(exit.position, exit.rotation);
                }
                UpdateRing(response, Mathf.Max(Frame.ComboPulse, Frame.PhasePulse), .8f, 2.2f, world.RunnerPosition + Vector3.up * .55f, rail.rotation, false);
                Pose ground = world.TrackPoseAt(run.Distance);
                UpdateRing(landing, Frame.LandingPulse, .22f, .95f,
                    ground.position + ground.rotation * new Vector3(run.LaneX, .025f, 0), ground.rotation, true);
            }
            bool reducedFlash = game.Save.Settings.ReducedFlash;
            bool reducedParticles = game.Save.Settings.ReducedParticles;
            ApplyLandingStyle(landing, Frame.LandingPulse, reducedFlash);
            Color threat = Color.Lerp(new Color(1f, .52f, .16f), new Color(1f, .12f, .2f), Frame.Threat);
            beacon.enabled = !reducedFlash;
            beacon.color = threat;
            beacon.intensity = 1.3f + Frame.Threat * 1.5f + Frame.DamagePulse * 1.2f;
            trail.startColor = threat;
            trail.endColor = new Color(threat.r, threat.g, threat.b, .08f);
            trail.enabled = !reducedParticles;
            response.enabled = !reducedParticles && !reducedFlash && Mathf.Max(Frame.ComboPulse, Frame.PhasePulse) > .01f;
            landing.enabled = !reducedParticles && Frame.LandingPulse > .01f;
            bool finalSection = !run.Endless && run.ActiveContract != null &&
                RunSimulation.TimeAtDistance(run.ActiveContract.Length) - run.Elapsed <= 15d;
            extraction.gameObject.SetActive(finalSection);
            if (running && Frame.LandingPulse > previousLandingPulse + .25f)
                audioFeedback.PlayLanding(world.RunnerPosition);
            previousLandingPulse = Frame.LandingPulse;
            audioFeedback.PresentOpening(Frame);
        }

        void UpdateRing(LineRenderer line, float pulse, float minimum, float expansion, Vector3 center, Quaternion orientation, bool horizontal)
        {
            if (pulse <= 0) return;
            float radius = minimum + (1f - pulse) * expansion;
            for (int i = 0; i < ringPoints.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / (ringPoints.Length - 1);
                Vector3 local = horizontal ? new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) :
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                ringPoints[i] = center + orientation * local * radius;
            }
            line.SetPositions(ringPoints);
            line.startColor = line.endColor = new Color(.16f, .85f, 1f, pulse * .75f);
        }

        static void ApplyLandingStyle(LineRenderer line, float pulse, bool reducedFlash)
        {
            line.startWidth = line.endWidth = reducedFlash ? .02f : .055f;
            float alpha = pulse * (reducedFlash ? .18f : .75f);
            line.startColor = line.endColor = new Color(.16f, .85f, 1f, alpha);
        }

        void OnDestroy()
        {
            if (observed != null) observed.Feedback -= Feedback;
            if (glow) Destroy(glow);
            if (exitGlow) Destroy(exitGlow);
            if (archMesh) Destroy(archMesh);
        }
    }
}
