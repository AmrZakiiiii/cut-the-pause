using System.Security.Cryptography;
using System.Text;
using CutThePause.Core.Models;

namespace CutThePause.Core.Services;

public static class ExportRequestFingerprint
{
    public static string Compute(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var builder = new StringBuilder();
        AppendPath(builder, request.InputPath);
        AppendPath(builder, request.OutputPath);
        Append(builder, request.SourceDuration.Ticks);
        Append(builder, (int)request.Preset);

        try
        {
            var sourcePath = Path.GetFullPath(request.InputPath);
            if (File.Exists(sourcePath))
            {
                var sourceInfo = new FileInfo(sourcePath);
                Append(builder, sourceInfo.Length);
                Append(builder, sourceInfo.LastWriteTimeUtc.Ticks);
            }
        }
        catch (ArgumentException)
        {
            Append(builder, "invalid-source-path");
        }
        catch (IOException)
        {
            Append(builder, "unreadable-source-metadata");
        }

        foreach (var cut in request.CutCandidates.OrderBy(static candidate => candidate.Start))
        {
            Append(builder, cut.Start.Ticks);
            Append(builder, cut.End.Ticks);
            Append(builder, cut.Reason);
            Append(builder, cut.IsEnabled);
        }

        foreach (var keepSegment in request.KeepSegments.OrderBy(static segment => segment.Order))
        {
            Append(builder, keepSegment.Order);
            Append(builder, keepSegment.Start.Ticks);
            Append(builder, keepSegment.End.Ticks);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void Append(StringBuilder builder, object? value) =>
        builder.Append(value?.ToString() ?? "<null>").Append('\u001F');

    private static void AppendPath(StringBuilder builder, string path)
    {
        try
        {
            Append(builder, Path.GetFullPath(path));
        }
        catch (ArgumentException)
        {
            Append(builder, path);
        }
    }
}
