using UnityEngine;

namespace Riftline
{
    public sealed class RiftlineAudio : MonoBehaviour
    {
        RiftlineGame game;
        AudioSource music;
        AudioSource[] voices;
        readonly float[] gains = new float[8];
        readonly float[] ends = new float[8];
        readonly bool[] gameplay = new bool[8];
        readonly bool[] paused = new bool[8];
        int nextVoice;
        OpeningFrame opening;

        public void Initialize(RiftlineGame owner)
        {
            game = owner;
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.spatialBlend = 0;
            if (game.Assets && game.Assets.Music)
            {
                music.clip = game.Assets.Music;
                music.Play();
            }
            voices = new AudioSource[8];
            for (int i = 0; i < voices.Length; i++)
            {
                var emitter = new GameObject("Spatial Feedback " + i);
                emitter.transform.SetParent(transform, false);
                voices[i] = emitter.AddComponent<AudioSource>();
                voices[i].spatialBlend = .8f;
                voices[i].rolloffMode = AudioRolloffMode.Logarithmic;
                voices[i].minDistance = 4;
                voices[i].maxDistance = 50;
                voices[i].dopplerLevel = 0;
                gains[i] = 1f;
            }
            ApplySettings();
        }

        public void ApplySettings()
        {
            music.volume = game.Save.Settings.MusicVolume * MusicGain();
            for (int i = 0; i < voices.Length; i++) voices[i].volume = game.Save.Settings.SfxVolume * gains[i];
        }

        public void Play(FeedbackKind kind, Vector3 position)
        {
            if (!game.Assets || game.Assets.Sounds == null || game.Assets.Sounds.Length == 0) return;
            int index = kind == FeedbackKind.Damage ? (int)FeedbackKind.Fail :
                kind == FeedbackKind.Combo ? (int)FeedbackKind.NearMiss : (int)kind;
            if (index >= game.Assets.Sounds.Length || !game.Assets.Sounds[index]) return;
            int slot = nextVoice++ % voices.Length;
            var voice = voices[slot];
            voice.Stop();
            paused[slot] = false;
            voice.transform.position = position;
            voice.clip = game.Assets.Sounds[index];
            voice.pitch = kind == FeedbackKind.Chip ? 1f + Mathf.Min(game.Run.ChipStreak, 20) * .018f :
                kind == FeedbackKind.Combo ? 1.15f : kind == FeedbackKind.Damage ? 1.25f : 1f;
            gains[slot] = kind == FeedbackKind.Chip ? .55f : kind == FeedbackKind.Combo ? .8f : 1f;
            voice.volume = game.Save.Settings.SfxVolume * gains[slot];
            gameplay[slot] = kind != FeedbackKind.Fail && kind != FeedbackKind.Complete && kind != FeedbackKind.Ui;
            float duration = kind == FeedbackKind.Damage ? .23f : kind == FeedbackKind.Combo ? .42f : voice.clip.length / voice.pitch;
            ends[slot] = game.Run.Elapsed + duration;
            voice.Play();
        }

        public void PlayLanding(Vector3 position)
        {
            if (!game.Assets || game.Assets.Sounds == null || game.Assets.Sounds.Length <= (int)FeedbackKind.Fail) return;
            AudioClip impact = game.Assets.Sounds[(int)FeedbackKind.Fail];
            if (!impact) return;
            int slot = nextVoice++ % voices.Length;
            var voice = voices[slot];
            voice.Stop();
            voice.transform.position = position;
            voice.clip = impact;
            voice.pitch = 1.35f;
            gains[slot] = .38f;
            voice.volume = game.Save.Settings.SfxVolume * gains[slot];
            gameplay[slot] = true;
            paused[slot] = false;
            // First 150ms of the generated impact: contact, before the power-down tail.
            ends[slot] = game.Run.Elapsed + .15f / voice.pitch;
            voice.Play();
        }

        public void PresentOpening(OpeningFrame frame) => opening = frame;

        float MusicGain()
        {
            if (game.Phase == RunPhase.Paused || game.Phase == RunPhase.Countdown) return .45f;
            if (game.Phase != RunPhase.Running) return .65f;
            return Mathf.Clamp(.72f + opening.Progress * .18f + opening.ComboPulse * .1f + opening.PhasePulse * .1f, .65f, 1f);
        }

        public void Present()
        {
            ApplySettings();
            bool running = game.Phase == RunPhase.Running;
            bool freeze = game.Phase == RunPhase.Paused || game.Phase == RunPhase.Countdown;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!gameplay[i]) continue;
                if (!running && !freeze || game.Run.Elapsed >= ends[i])
                {
                    voices[i].Stop();
                    gameplay[i] = paused[i] = false;
                }
                else if (freeze && !paused[i])
                {
                    voices[i].Pause();
                    paused[i] = true;
                }
                else if (running && paused[i])
                {
                    voices[i].UnPause();
                    paused[i] = false;
                }
            }
        }

        public void ResetRun()
        {
            opening = default;
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i].Stop();
                gameplay[i] = paused[i] = false;
            }
        }
    }
}
