using QuotaPet.App;

namespace QuotaPet.App.Tests;

public sealed class PetVisualsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "QuotaPetTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoadFrames_MissingAssets_ReturnsFourPlaceholderFrames()
    {
        using var frames = PetVisuals.LoadFrames(_root, "pet-swim", moving: true);

        Assert.True(frames.IsPlaceholder);
        Assert.Equal(4, frames.Images.Length);
        Assert.All(frames.Images, image =>
        {
            Assert.Equal(PetVisuals.FrameSize, image.Width);
            Assert.Equal(PetVisuals.FrameSize, image.Height);
        });
    }

    [Fact]
    public void LoadFrames_MissingOneFrame_FallsBackWithoutThrowing()
    {
        Directory.CreateDirectory(_root);
        for (var index = 1; index <= 3; index++)
        {
            using var image = new Bitmap(PetVisuals.FrameSize, PetVisuals.FrameSize);
            image.Save(Path.Combine(_root, $"pet-rest-{index}.png"));
        }

        using var frames = PetVisuals.LoadFrames(_root, "pet-rest", moving: false);

        Assert.True(frames.IsPlaceholder);
        Assert.Equal(4, frames.Images.Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
