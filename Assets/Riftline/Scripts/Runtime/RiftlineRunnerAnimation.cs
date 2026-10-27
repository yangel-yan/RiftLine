using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Riftline
{
    public sealed class RiftlineRunnerAnimation : MonoBehaviour
    {
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] inputs;
        AnimationClip[] sources;
        double[] times;
        RunSimulation observedRun;
        RunPhase previousPhase;
        int previousLane = 1, previousHeading, activeClip = -1;
        float previousHeight, actionAge, jumpAge;
        float previousElapsed;
        bool previousSlide, pendingJump, pendingSlide, descending;
        public string ActiveMotion => activeClip >= 0 && sources[activeClip] ? sources[activeClip].name : "";
        public double SourceTime => activeClip >= 0 ? times[activeClip] : 0;
        public int TransitionCount { get; private set; }

        public void Initialize(AnimationClip[] clips)
        {
            if (graph.IsValid()) graph.Destroy();
            var animator = GetComponentInChildren<Animator>();
            if (!animator || clips == null || clips.Length < 8) return;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Eve Generated Motion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            sources = clips;
            inputs = new AnimationClipPlayable[clips.Length];
            times = new double[clips.Length];
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                if (!clips[i]) continue;
                inputs[i] = AnimationClipPlayable.Create(graph, clips[i]);
                inputs[i].SetApplyFootIK(false);
                inputs[i].SetSpeed(0);
                graph.Connect(inputs[i], 0, mixer, i);
            }
            var output = AnimationPlayableOutput.Create(graph, "Eve", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        public void Present(RunSimulation run, float dt)
        {
            if (!graph.IsValid() || run == null) return;
            if (observedRun != run)
            {
                if (observedRun != null) observedRun.Feedback -= ObserveFeedback;
                observedRun = run;
                observedRun.Feedback += ObserveFeedback;
                ResetState(run);
            }
            if (previousPhase != RunPhase.Countdown && run.Phase == RunPhase.Countdown && run.Elapsed == 0)
            {
                ResetState(run);
            }
            if (run.Phase == RunPhase.Paused || (run.Phase == RunPhase.Countdown && run.Elapsed > 0))
            {
                graph.Evaluate(0);
                previousPhase = run.Phase;
                return;
            }
            if (run.Phase == RunPhase.Running && Mathf.Approximately(run.Elapsed, previousElapsed))
            {
                graph.Evaluate(0);
                previousPhase = run.Phase;
                return;
            }
            dt = Mathf.Max(0, dt);
            actionAge += dt;
            if (pendingJump) { jumpAge = 0; descending = false; }
            else if (run.Height > .001f && run.Height < previousHeight - .00001f) descending = true;
            if (run.Height > .001f) jumpAge += dt; else jumpAge = 0;
            int desired = activeClip;
            if (run.Phase == RunPhase.Menu || run.Phase == RunPhase.Countdown) desired = 0;
            else if (run.Phase == RunPhase.Failed) desired = 6;
            else if (run.Phase == RunPhase.Completed) desired = 7;
            else if (run.Height > .001f || pendingJump) desired = descending ? 11 : 2;
            else if (previousHeight > .001f) desired = 8;
            else if (run.Sliding) desired = 3;
            else if (run.Heading != previousHeading) desired = 5;
            else if (run.Lane != previousLane) desired = run.Lane < previousLane ? 9 : 10;
            else if (previousSlide) desired = 12;
            else if (activeClip < 5 || activeClip > 13 || actionAge >= Duration(activeClip)) desired = 1;
            // Phase is read through the local effect; it must not interrupt stride or an airborne action.
            if (desired >= sources.Length || !sources[desired]) desired = 1;
            bool committedAction = desired == 2 && pendingJump || desired == 3 && pendingSlide ||
                (desired == 9 || desired == 10) && run.Lane != previousLane ||
                desired == 5 && run.Heading != previousHeading;
            if (desired != activeClip || committedAction)
            {
                activeClip = desired;
                actionAge = 0;
                if (desired != 0 && desired != 1) times[desired] = StartTime(desired);
                TransitionCount++;
            }
            if (activeClip == 0 || activeClip == 1)
            {
                double cadence = activeClip == 0 ? 1 : Mathf.Clamp(run.Speed / 12f, .85f, 1.35f);
                times[activeClip] = (times[activeClip] + dt * cadence) % Mathf.Max(.01f, sources[activeClip].length);
            }
            else
            {
                float age = activeClip == 2 ? jumpAge : actionAge;
                times[activeClip] = Mathf.Lerp(StartTime(activeClip), EndTime(activeClip),
                    Mathf.Clamp01(age / Duration(activeClip)));
            }
            float sum = 0;
            for (int i = 0; i < inputs.Length; i++)
            {
                float weight = Mathf.MoveTowards(mixer.GetInputWeight(i), i == activeClip ? 1 : 0, dt / .12f);
                if (TransitionCount == 1) weight = i == activeClip ? 1 : 0;
                mixer.SetInputWeight(i, weight);
                sum += weight;
                if (inputs[i].IsValid()) inputs[i].SetTime(times[i]);
            }
            if (sum > .0001f)
                for (int i = 0; i < inputs.Length; i++) mixer.SetInputWeight(i, mixer.GetInputWeight(i) / sum);
            graph.Evaluate(0);
            previousLane = run.Lane;
            previousHeading = run.Heading;
            previousHeight = run.Height;
            previousSlide = run.Sliding;
            previousPhase = run.Phase;
            previousElapsed = run.Elapsed;
            pendingJump = pendingSlide = false;
            if (run.Height <= .001f) descending = false;
        }

        void ObserveFeedback(FeedbackKind feedback)
        {
            if (feedback == FeedbackKind.Jump) pendingJump = true;
            if (feedback == FeedbackKind.Slide) pendingSlide = true;
        }

        void ResetState(RunSimulation run)
        {
            activeClip = -1;
            previousLane = run.Lane;
            previousHeading = run.Heading;
            previousElapsed = run.Elapsed;
            previousHeight = actionAge = jumpAge = 0;
            previousSlide = pendingJump = pendingSlide = descending = false;
            for (int i = 0; i < times.Length; i++) { times[i] = 0; mixer.SetInputWeight(i, 0); }
        }

        static float Duration(int i) => i == 2 ? .47f : i == 3 ? .75f : i == 5 ? .65f :
            i == 6 ? 2.2f : i == 7 ? 3f : i == 9 || i == 10 ? .32f : i == 11 ? .4f : .24f;
        static float StartTime(int i) => i == 3 ? .8f : i == 6 || i == 7 ? .2f : .45f;
        float EndTime(int i) => Mathf.Min(sources[i].length - .01f,
            i == 6 ? 3.85f : i == 7 ? 3.8f : i == 3 ? 1.65f : i == 2 ? 1.1f : i == 11 ? 1.3f : 1.05f);

        void OnDestroy()
        {
            if (observedRun != null) observedRun.Feedback -= ObserveFeedback;
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
