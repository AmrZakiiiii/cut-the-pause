using CutThePause.Core.Models;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Vad;

public sealed class EnergyVadAnalyzer : IVadAnalyzer, IStreamingVadAnalyzer
{
    public Task<VadAnalysisResult> DetectSpeechAsync(
        PcmAudioData audio,
        AnalysisSettings settings,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Analyze(
            new ArrayPcmFrameReader(audio.Samples),
            audio.SampleRate,
            audio.Samples.Length,
            settings,
            cancellationToken));
    }

    public Task<VadAnalysisResult> DetectSpeechAsync(
        PcmAudioFile audio,
        AnalysisSettings settings,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Analyze(
            new FilePcmFrameReader(audio),
            audio.SampleRate,
            audio.SampleCount,
            settings,
            cancellationToken));
    }

    private static VadAnalysisResult Analyze(
        IPcmFrameReader frameReader,
        int sampleRate,
        long sampleCount,
        AnalysisSettings settings,
        CancellationToken cancellationToken)
    {
        using (frameReader)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sampleCount == 0 || sampleRate <= 0)
            {
                return new VadAnalysisResult(Array.Empty<SpeechSegment>(), new[]
                {
                    "The extracted audio stream was empty."
                });
            }

            const int frameDurationMs = 30;
            var frameSize = Math.Max(1, sampleRate * frameDurationMs / 1000);
            var frame = new float[frameSize];
            var energies = new List<float>();

            while (true)
            {
                var length = frameReader.ReadFrame(frame, cancellationToken);
                if (length == 0)
                {
                    break;
                }

                var total = 0f;
                for (var index = 0; index < length; index++)
                {
                    var sample = frame[index];
                    total += sample * sample;
                }

                energies.Add(MathF.Sqrt(total / length));
            }

            if (energies.Count == 0)
            {
                return new VadAnalysisResult(Array.Empty<SpeechSegment>(), new[]
                {
                    "The extracted audio stream was empty."
                });
            }

            var orderedEnergies = energies.OrderBy(static energy => energy).ToArray();
            var percentileIndex = Math.Clamp((int)(orderedEnergies.Length * 0.3f), 0, orderedEnergies.Length - 1);
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
                    AppendSegment(sampleRate, frameSize, settings.MinSpeech, segments, startFrame.Value, frameIndex);
                    startFrame = null;
                }
            }

            if (startFrame is not null)
            {
                AppendSegment(sampleRate, frameSize, settings.MinSpeech, segments, startFrame.Value, energies.Count);
            }

            return new VadAnalysisResult(segments, new[]
            {
                "Silero VAD is not available yet on this machine, so the app used the built-in energy fallback detector."
            });
        }
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
