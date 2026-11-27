namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;

    /// <summary>Explicit cue-content integration; playback retains the ordinary cue acquisition until completion.</summary>
    internal static class ContentAudio
    {
        internal static async UniTask<PlaybackHandle> PlayCueAsync(
            this AuraEngine engine,
            ContentProvider content,
            ResourceId<AudioCue> cue,
            CancellationToken token = default
        )
        {
            var request = await content.LoadAsync(cue, token);
            try
            {
                var voice = engine.Play(request.Asset, retention: request);
                if (voice == null)
                    request.Release();
                return voice;
            }
            catch
            {
                request.Release();
                throw;
            }
        }
    }
}
