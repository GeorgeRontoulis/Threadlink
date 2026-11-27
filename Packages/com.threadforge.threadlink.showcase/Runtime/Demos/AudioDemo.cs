namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Aura cues, categories, fades and the music channel.</summary>
    public sealed class AudioDemo : ShowcaseDemo
    {
        // A project uses its generated AudioCategories; the Showcase cannot assume a project's categories.
        private static readonly AudioCategory Effects = new AudioCategory("Effects");

        private readonly List<Object> created = new List<Object>();
        private AudioCue tone,
            music;

        public override string Title => "Audio";

        public override string Summary =>
            "Aura plays AudioCue assets: variations, categories with voice limits and volumes, per-voice fades, and one crossfading music channel. No audio component needs to be placed; scene music is a SceneMusic component designers add to a scene.";

        public override string[] Packages => new[] { "aura" };

        private void Prepare()
        {
            if (Object.FindAnyObjectByType<AudioListener>() == null)
                created.Add(new GameObject("Showcase Listener").AddComponent<AudioListener>().gameObject);
            tone = Cue("Tone", 660, false, "Effects");
            music = Cue("Drone", 220, true, "Music");
        }

        private AudioCue Cue(string name, int frequency, bool loop, string category)
        {
            const int rate = 44100;
            var samples = new float[rate / 4];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / rate) * 0.2f * (1 - (float)i / samples.Length);
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            var cue = ScriptableObject.CreateInstance<AudioCue>();
            cue.name = name;
            cue.Variations = new[] { new AudioCue.Variation { Clip = clip } };
            cue.Loop = loop;
            cue.Category = category;
            created.Add(clip);
            created.Add(cue);
            return cue;
        }

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            Prepare();
            var volume = new Slider("Effects volume", 0, 1) { value = 1 };
            volume.RegisterValueChangedCallback(change => Aura.SetVolume(Effects, change.newValue));
            panel.Add(volume);
            panel.Add(Row(
                Action(context, "Play a cue", () => context.Log(Aura.Play(tone) != null ? "Playing." : "Rejected by the voice limit.")),
                Action(context, "Fade in from silence", async () =>
                {
                    var voice = Aura.Play(tone);
                    if (voice != null)
                        await voice.FadeToAsync(0.2f, 0.01f);
                }),
                Action(context, "Start music", () => Aura.PlayMusic(music, 1f)),
                Action(context, "Stop music", () => Aura.StopMusic(1f))));
        }

        public override async UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            Prepare();
            var voice = Aura.Play(tone);
            Check(voice != null && voice.IsPlaying, "a cue plays");
            await voice.FadeToAsync(0, 0.05f, stopWhenFinished: true);
            Check(!voice.IsPlaying, "a fade can stop its voice");
            Aura.PlayMusic(music, 0);
            Check(Aura.CurrentMusic == music, "the music channel plays a looping cue");
            Aura.PlayMusic(music, 0);
            Check(Aura.CurrentMusic == music, "playing the same music again changes nothing");
            Aura.StopMusic(0);
            Check(Aura.CurrentMusic == null, "music stops");
            Aura.SetMute(Effects, true);
            Aura.SetMute(Effects, false);
        }

        public override void Dispose()
        {
            if (Aura.TryGetSingleton(out _))
                Aura.StopMusic(0);
            foreach (var item in created)
                if (item != null)
                    Object.Destroy(item);
            created.Clear();
        }
    }
}
