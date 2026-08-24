using CutThePause.Core.Models;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Runtime.InteropServices;
using System.Reflection;

namespace CutThePause.Infrastructure.Vad;

public sealed class SileroVadAnalyzer : IVadAnalyzer, IStreamingVadAnalyzer
{
    private static readonly object NativeRuntimeLock = new();
    private static bool _nativeRuntimeLoaded;
    private static bool _resolverInstalled;
    private static IntPtr _nativeRuntimeHandle;
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
        return DetectSpeechAsync(
            new ArrayPcmFrameReader(audio.Samples),
            audio.SampleRate,
            settings,
            cancellationToken);
    }

    public Task<VadAnalysisResult> DetectSpeechAsync(
        PcmAudioFile audio,
        AnalysisSettings settings,
        CancellationToken cancellationToken)
    {
        return DetectSpeechAsync(
            new FilePcmFrameReader(audio),
            audio.SampleRate,
            settings,
            cancellationToken);
    }

    private Task<VadAnalysisResult> DetectSpeechAsync(
        IPcmFrameReader frameReader,
        int sampleRate,
        AnalysisSettings settings,
        CancellationToken cancellationToken)
    {
        using (frameReader)
        {
            if (!File.Exists(_modelPath))
            {
                throw new FileNotFoundException("Silero VAD model was not found.", _modelPath);
            }

            EnsureNativeRuntimeLoaded();

            const int frameSize = 512;
            const int contextSize = 64;
            using var session = new InferenceSession(_modelPath);

            var probabilities = new List<float>();
            var state = CreateInitialState();
            var context = new float[contextSize];
            var frameSamples = new float[frameSize];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (frameReader.ReadFrame(frameSamples, cancellationToken) == 0)
                {
                    break;
                }

                var frame = new DenseTensor<float>(new[] { 1, frameSize + contextSize });
                for (var index = 0; index < contextSize; index++)
                {
                    frame[0, index] = context[index];
                }

                for (var index = 0; index < frameSize; index++)
                {
                    frame[0, contextSize + index] = frameSamples[index];
                }

                var sampleRateTensor = new DenseTensor<long>(new[] { 1 });
                sampleRateTensor[0] = sampleRate;

                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(new[]
                {
                    NamedOnnxValue.CreateFromTensor("input", frame),
                    NamedOnnxValue.CreateFromTensor("state", state),
                    NamedOnnxValue.CreateFromTensor("sr", sampleRateTensor)
                });

                var outputs = results.ToArray();
                var probability = outputs[0].AsEnumerable<float>().FirstOrDefault();
                probabilities.Add(probability);
                state = CopyState(outputs[1].AsTensor<float>());
                Array.Copy(frameSamples, frameSamples.Length - contextSize, context, 0, contextSize);
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
                    AppendSegment(sampleRate, settings.MinSpeech, segments, startFrame.Value, frameIndex, frameSize);
                    startFrame = null;
                }
            }

            if (startFrame is not null)
            {
                AppendSegment(sampleRate, settings.MinSpeech, segments, startFrame.Value, probabilities.Count, frameSize);
            }

            return Task.FromResult(new VadAnalysisResult(segments, Array.Empty<string>()));
        }
    }

    private static DenseTensor<float> CreateInitialState()
    {
        var state = new DenseTensor<float>(new[] { 2, 1, 128 });
        state.Buffer.Span.Clear();
        return state;
    }

    private static DenseTensor<float> CopyState(Tensor<float> source)
    {
        var shape = source.Dimensions.ToArray();
        var copiedState = new DenseTensor<float>(shape);
        var sourceValues = source.ToArray();
        sourceValues.CopyTo(copiedState.Buffer.Span);
        return copiedState;
    }

    private static void EnsureNativeRuntimeLoaded()
    {
        if (_nativeRuntimeLoaded)
        {
            return;
        }

        lock (NativeRuntimeLock)
        {
            if (_nativeRuntimeLoaded)
            {
                return;
            }

            if (!_resolverInstalled)
            {
                try
                {
                    NativeLibrary.SetDllImportResolver(typeof(InferenceSession).Assembly, ResolveOnnxRuntimeImport);
                }
                catch (InvalidOperationException)
                {
                    // The assembly already has a resolver; keep going and try direct loading.
                }

                _resolverInstalled = true;
            }

            foreach (var candidate in GetNativeRuntimeCandidates())
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                _nativeRuntimeHandle = NativeLibrary.Load(candidate);
                _nativeRuntimeLoaded = true;
                return;
            }
        }
    }

    private static IntPtr ResolveOnnxRuntimeImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Contains("onnxruntime", StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        if (_nativeRuntimeHandle != IntPtr.Zero)
        {
            return _nativeRuntimeHandle;
        }

        foreach (var candidate in GetNativeRuntimeCandidates())
        {
            if (NativeLibrary.TryLoad(candidate, out var handle))
            {
                _nativeRuntimeHandle = handle;
                _nativeRuntimeLoaded = true;
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> GetNativeRuntimeCandidates()
    {
        var fileName = GetNativeLibraryName();
        if (fileName is null)
        {
            yield break;
        }

        var directories = new[]
        {
            AppContext.BaseDirectory,
            Path.GetDirectoryName(typeof(SileroVadAnalyzer).Assembly.Location)
        }
        .Where(static directory => !string.IsNullOrWhiteSpace(directory))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in directories)
        {
            // Self-contained macOS app bundles place the ONNX Runtime dylib
            // beside the executable rather than under runtimes/*/native.
            yield return Path.Combine(directory!, fileName);

            var runtimesDirectory = Path.Combine(directory!, "runtimes");
            if (!Directory.Exists(runtimesDirectory))
            {
                continue;
            }

            foreach (var nativeDirectory in Directory.EnumerateDirectories(runtimesDirectory, "*", SearchOption.TopDirectoryOnly)
                         .Select(path => Path.Combine(path, "native"))
                         .Where(Directory.Exists))
            {
                yield return Path.Combine(nativeDirectory, fileName);
            }
        }
    }

    private static string? GetNativeLibraryName()
    {
        if (OperatingSystem.IsMacOS())
        {
            return "libonnxruntime.dylib";
        }

        if (OperatingSystem.IsLinux())
        {
            return "libonnxruntime.so";
        }

        if (OperatingSystem.IsWindows())
        {
            return "onnxruntime.dll";
        }

        return null;
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
