using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Tests;

public sealed class PcmAudioFileTests
{
    [Fact]
    public async Task DisposeAsync_DoesNotDeleteBorrowedFile()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"cut-the-pause-borrowed-{Guid.NewGuid():N}.f32le");
        await File.WriteAllBytesAsync(path, Array.Empty<byte>());

        try
        {
            await using (var audio = new PcmAudioFile(path, 16_000, 0, ownsFile: false))
            {
            }

            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
