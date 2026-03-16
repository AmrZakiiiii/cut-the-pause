using CutThePause.Core.Models;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace CutThePause.Infrastructure.Vad;

public sealed class SileroVadAnalyzer : IVadAnalyzer
{
    private readonly string _modelPath;

    public SileroVadAnalyzer(string modelPath)
    {
        _modelPath = modelPath;
    }

    public Task<VadAnalysisResult> DetectSpeechAsync(
        PcmAudioData audio,
        AnalysisSettings settings,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_modelPath))
        {
            throw new FileNotFoundException("Silero VAD model was not found.", _modelPath);
        }

        const int frameSize = 512;
        using var session = new InferenceSession(_modelPath);

        var samples = audio.Samples;
        var probabilities = new List<float>();

        for (var offset = 0; offset < samples.Length; offset += frameSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var frame = new DenseTensor<float>(new[] { 1, frameSize });
            for (var index = 0; index < frameSize; index++)
            {
                var sampleIndex = offset + index;
                frame[0, index] = sampleIndex < samples.Length ? samples[sampleIndex] : 0f;
            }

            var sampleRate = new DenseTensor<long>(new[] { 1 });
            sampleRate[0] = audio.SampleRate;

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(new[]
            {
                NamedOnnxValue.CreateFromTensor("input", frame),
                NamedOnnxValue.CreateFromTensor("sr", sampleRate)
            });

            var probability = results.First().AsEnumerable<float>().FirstOrDefault();
            probabilities.Add(probability);
        }

        var segments = new List<SpeechSegment>();
        int? startFrame = null;

        for (var frameIndex = 0; frameIndex < probabilities.Count; frameIndex++)
        {
            var isSpeech = probabilities[frameIndex] >= settings.SpeechThreshold;
            if (isSpeech && startFrame is null)
            {
                startFrame = frameIndex;
                continue;
            }

            if (!isSpeech && startFrame is not null)
            {
                AppendSegment(audio.SampleRate, settings.MinSpeech, segments, startFrame.Value, frameIndex, frameSize);
                startFrame = null;
            }
        }

        if (startFrame is not null)
        {
            AppendSegment(audio.SampleRate, settings.MinSpeech, segments, startFrame.Value, probabilities.Count, frameSize);
        }

        return Task.FromResult(new VadAnalysisResult(segments, Array.Empty<string>()));
    }

    private static void AppendSegment(
        int sampleRate,
        TimeSpan minSpeech,
        ICollection<SpeechSegment> segments,
        int startFrame,
        int endFrame,
        int frameSize)
    {
        var start = TimeSpan.FromSeconds((double)(startFrame * frameSize) / sampleRate);
        var end = TimeSpan.FromSeconds((double)(endFrame * frameSize) / sampleRate);

        if (end - start >= minSpeech)
        {
            segments.Add(new SpeechSegment(start, end));
        }
    }
}
