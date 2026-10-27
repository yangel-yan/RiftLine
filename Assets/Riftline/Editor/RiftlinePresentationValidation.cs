#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riftline
{
    public static class RiftlinePresentationValidation
    {
        [MenuItem("Riftline/Validation/Validate Motion and Navigation")]
        public static void RunMenu() => Debug.Log(RunAll());

        public static string RunAll(string output = "/tmp/riftline-presentation-validation.txt")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run deterministic presentation checks from Edit Mode.");
            var checks = new List<string>();
            var library = AssetDatabase.LoadAssetAtPath<RiftlineAssets>(RiftlineSceneBuilder.LibraryPath);
            Require(library && library.EvePrefab && library.Animations.Length >= 15, "Generated motion library", checks);
            var catalog = GameCatalog.CreateDefault();
            var avatar = UnityEngine.Object.Instantiate(library.EvePrefab);
            try
            {
                var motion = avatar.AddComponent<RiftlineRunnerAnimation>();
                motion.Initialize(library.Animations);
                var run = Start(catalog, motion);
                int before = motion.TransitionCount;
                for (int i = 0; i < 200; i++) Advance(run, motion, .01f);
                Require(motion.ActiveMotion == library.Animations[1].name && motion.TransitionCount == before &&
                    motion.SourceTime > 1.5, "Continuous stride keeps clip and source clock", checks);
                run = Start(catalog, motion);
                run.Handle(GameCommand.Left); Advance(run, motion, .05f);
                run.Handle(GameCommand.Right); Advance(run, motion, .2f);
                before = motion.TransitionCount;
                run.Handle(GameCommand.Right); Advance(run, motion, .01f);
                Require(run.Lane == 2 && motion.TransitionCount == before + 1,
                    "Repeated committed same-direction lane action restarts once", checks);
                run = Start(catalog, motion);
                int jumps = 0;
                run.Feedback += kind => { if (kind == FeedbackKind.Jump) jumps++; };
                run.Handle(GameCommand.Jump);
                bool descentObserved = false, descentStayed = true, bufferObserved = false;
                for (int i = 0; i < 100; i++)
                {
                    if (i == 72) run.Handle(GameCommand.Jump);
                    Advance(run, motion, .01f);
                    if (i > 60 && i < 82 && run.Height > 0 && run.Height < .15f)
                    {
                        descentObserved = true;
                        descentStayed &= motion.ActiveMotion != library.Animations[2].name;
                    }
                    if (jumps < 2 || run.Height <= .001f) continue;
                    bufferObserved = motion.SourceTime < .75;
                    break;
                }
                Require(descentObserved && descentStayed, "Descent remains latched near landing", checks);
                Require(jumps == 2 && bufferObserved, "Buffered jump starts a fresh source window", checks);
                run = Start(catalog, motion);
                run.Handle(GameCommand.Jump); Advance(run, motion, .2f);
                var animator = avatar.GetComponentInChildren<Animator>();
                var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Vector3 position = foot.position;
                double source = motion.SourceTime;
                float elapsed = run.Elapsed;
                run.Pause();
                Advance(run, motion, .8f);
                Require(run.Elapsed == elapsed && motion.SourceTime == source &&
                    Vector3.Distance(foot.position, position) < .00001f, "Midair pause freezes source and actual skeleton", checks);
                run.Resume();
                Advance(run, motion, 2.99f);
                Require(run.Elapsed == elapsed && motion.SourceTime == source &&
                    Vector3.Distance(foot.position, position) < .00001f, "Resume countdown holds the exact airborne pose", checks);
                Advance(run, motion, .02f);
                Require(run.Elapsed == elapsed && motion.SourceTime == source, "Countdown completion does not consume gameplay time", checks);
                Advance(run, motion, .02f);
                Require(run.Elapsed > elapsed && motion.SourceTime > source, "Resumed motion continues its clock", checks);
            }
            finally { UnityEngine.Object.DestroyImmediate(avatar); }
            Navigation(checks);
            string report = "PASS: " + checks.Count + " presentation regressions\n" + string.Join("\n", checks);
            File.WriteAllText(output, report);
            return report;
        }

        static RunSimulation Start(GameCatalog catalog, RiftlineRunnerAnimation motion)
        {
            var run = new RunSimulation(catalog);
            motion.Present(run, 0);
            run.Start(catalog.Contracts[1], catalog.Contracts[1].Seed, false);
            motion.Present(run, 0);
            run.Tick(3.001f);
            motion.Present(run, .001f);
            Advance(run, motion, .01f);
            return run;
        }

        static void Advance(RunSimulation run, RiftlineRunnerAnimation motion, float dt)
        {
            run.Tick(dt);
            motion.Present(run, dt);
        }

        static void Navigation(List<string> checks)
        {
            var root = new GameObject("Transient navigation regression");
            try
            {
                var navigation = new GameObject("Navigation").transform;
                navigation.SetParent(root.transform, false);
                var rig = navigation.gameObject.AddComponent<RiftlineNavigationRig>();
                var origin = new GameObject("Tracking").transform;
                origin.SetParent(navigation, false);
                rig.TrackingOrigin = origin;
                var head = new GameObject("Head").transform;
                head.SetParent(origin, false);
                head.localPosition = new Vector3(.12f, 1.6f, -.08f);
                head.localRotation = Quaternion.Euler(5, 17, -2);
                rig.Calibrate(head);
                Vector3 localPosition = head.localPosition;
                Quaternion localRotation = head.localRotation;
                foreach (int mode in new[] { 0, 1, 2 })
                foreach (int direction in new[] { -1, 1 })
                {
                    var alignment = new RiftlineAlignment();
                    alignment.Configure(mode, 0);
                    alignment.Advance(direction * 90, .2f, true);
                    rig.SetHeading(alignment.Heading);
                    float pausedHeading = alignment.Heading;
                    alignment.Advance(direction * 90, 2, false);
                    Require(alignment.Heading == pausedHeading, "Mode " + mode + " direction " + direction + " pause holds yaw", checks);
                    for (int i = 0; i < 100; i++)
                    {
                        alignment.Advance(direction * 90, .01f, true);
                        rig.SetHeading(alignment.Heading);
                    }
                    float expected = mode == 2 ? 0 : direction * 90;
                    Require(Mathf.Abs(Mathf.DeltaAngle(alignment.Heading, expected)) < .001f &&
                        Vector3.Distance(head.localPosition, localPosition) < .00001f &&
                        Quaternion.Angle(head.localRotation, localRotation) < .001f,
                        "Mode " + mode + " direction " + direction + " reaches yaw with unchanged tracked local pose", checks);
                    Vector3 worldPosition = head.position;
                    Quaternion worldRotation = head.rotation;
                    rig.Calibrate(head);
                    Require(Vector3.Distance(head.position, worldPosition) < .00001f &&
                        Quaternion.Angle(head.rotation, worldRotation) < .01f,
                        "Mode " + mode + " direction " + direction + " recenter preserves head world pose", checks);
                    rig.ResetHeadingPreservingTracking();
                    Require(Vector3.Distance(head.position, worldPosition) < .00001f &&
                        Quaternion.Angle(head.rotation, worldRotation) < .01f,
                        "Mode " + mode + " direction " + direction + " reset preserves head world pose", checks);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void Require(bool condition, string label, List<string> checks)
        {
            if (!condition) throw new InvalidOperationException("Presentation regression: " + label);
            checks.Add("PASS " + label);
        }
    }
}
#endif
