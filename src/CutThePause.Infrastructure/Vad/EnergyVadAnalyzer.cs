using CutThePause.Core.Models;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Vad;

public sealed class EnergyVadAnalyzer : IVadAnalyzer
{
    public Task<VadAnalysisResult> DetectSpeechAsync(
        PcmAudioData audio,
        AnalysisSettings settings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (audio.Samples.Length == 0 || audio.SampleRate <= 0)
        {
            return Task.FromResult(new VadAnalysisResult(Array.Empty<SpeechSegment>(), new[]
            {
                "The extracted audio stream was empty."
            }));
        }

        const int frameDurationMs = 30;
        var frameSize = Math.Max(1, audio.SampleRate * frameDurationMs / 1000);
        var energies = new List<float>();

        for (var offset = 0; offset < audio.Samples.Length; offset += frameSize)
        {
            var length = Math.Min(frameSize, audio.Samples.Length - offset);
            var total = 0f;

            for (var index = 0; index < length; index++)
            {
                var sample = audio.Samples[offset + index];
                total += sample * sample;
            }

            energies.Add(MathF.Sqrt(total / length));
        }

        var orderedEnergies = energies.OrderBy(static energy => energy).ToArray();
        var percentileIndex = Math.Clamp((int)(orderedEnergies.Length * 0.3f), 0, Math.Max(orderedEnergies.Length - 1, 0));
        var noiseFloor = orderedEnergies[percentileIndex];
        var threshold = MathF.Max(0.0125f, noiseFloor * (1.75f + settings.SpeechThreshold));

        var segments = new List<SpeechSegment>();
        int? startFrame = null;

        for (var frameIndex = 0; frameIndex < energies.Count; frameIndex++)
        {
            var isSpeech = energies[frameIndex] >= threshold;

            if (isSpeech && startFrame is null)
            {
                startFrame = frameIndex;
                continue;
            }

            if (!isSpeech && startFrame is not null)
            {
                AppendSegment(audio.SampleRate, frameSize, settings.MinSpeech, segments, startFrame.Value, frameIndex);
                startFrame = null;
            }
        }

        if (startFrame is not null)
        {
            AppendSegment(audio.SampleRate, frameSize, settings.MinSpeech, segments, startFrame.Value, energies.Count);
        }

        return Task.FromResult(new VadAnalysisResult(segments, new[]
        {
            "Silero VAD is not available yet on this machine, so the app used the built-in energy fallback detector."
        }));
    }

    private static void AppendSegment(
        int sampleRate,
        int frameSize,
        TimeSpan minSpeech,
        ICollection<SpeechSegment> segments,
        int startFrame,
        int endFrame)
    {
        var start = TimeSpan.FromSeconds((double)(startFrame * frameSize) / sampleRate);
        var end = TimeSpan.FromSeconds((double)(endFrame * frameSize) / sampleRate);

        if (end - start >= minSpeech)
        {
            segments.Add(new SpeechSegment(start, end, 0.5f));
        }
    }
}
