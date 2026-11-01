using System.Drawing.Drawing2D;

namespace QuotaPet.App;

internal sealed class PetFrameSet(Image[] images, bool isPlaceholder) : IDisposable
{
    public Image[] Images { get; } = images;
    public bool IsPlaceholder { get; } = isPlaceholder;

    public void Dispose()
    {
        foreach (var image in Images) image.Dispose();
    }
}

internal static class PetVisuals
{
    public const int FrameSize = 112;
    private const int FrameCount = 4;

    public static PetFrameSet LoadFrames(string assetsPath, string prefix, bool moving)
    {
        var paths = Enumerable.Range(1, FrameCount)
            .Select(index => Path.Combine(assetsPath, $"{prefix}-{index}.png"))
            .ToArray();
        if (paths.Any(path => !File.Exists(path))) return CreatePlaceholderFrames(moving);

        var images = new List<Image>(FrameCount);
        try
        {
            foreach (var path in paths) images.Add(Image.FromFile(path));
            return new PetFrameSet(images.ToArray(), isPlaceholder: false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
        {
            foreach (var image in images) image.Dispose();
            return CreatePlaceholderFrames(moving);
        }
    }

    private static PetFrameSet CreatePlaceholderFrames(bool moving)
    {
        var frames = Enumerable.Range(0, FrameCount)
            .Select(index => CreatePlaceholderFrame(index, moving))
            .ToArray();
        return new PetFrameSet(frames, isPlaceholder: true);
    }

    private static Image CreatePlaceholderFrame(int index, bool moving)
    {
        var image = new Bitmap(FrameSize, FrameSize);
        using var graphics = Graphics.FromImage(image);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var bob = moving ? new[] { -2, 0, 2, 0 }[index] : new[] { 0, 1, 0, -1 }[index];
        var bodyColor = moving ? Color.FromArgb(61, 183, 178) : Color.FromArgb(107, 153, 185);
        var accentColor = moving ? Color.FromArgb(255, 188, 92) : Color.FromArgb(126, 182, 218);

        using var shadow = new SolidBrush(Color.FromArgb(42, 35, 49, 61));
        using var body = new SolidBrush(bodyColor);
        using var accent = new SolidBrush(accentColor);
        using var ink = new SolidBrush(Color.FromArgb(34, 49, 61));
        using var eye = new SolidBrush(Color.White);
        using var outline = new Pen(Color.FromArgb(34, 49, 61), 3);

        graphics.FillEllipse(shadow, 20, 91, 72, 10);
        graphics.FillEllipse(body, 17, 40 + bob, 78, 54);
        graphics.FillEllipse(body, 30, 22 + bob, 52, 48);
        graphics.DrawEllipse(outline, 30, 22 + bob, 52, 48);
        graphics.FillPolygon(accent, new[]
        {
            new Point(56, 27 + bob),
            new Point(63, 13 + bob),
            new Point(72, 26 + bob)
        });
        graphics.FillEllipse(accent, 20, 71 + bob, 24, 13);
        graphics.FillEllipse(accent, 68, 71 + bob, 24, 13);
        graphics.FillEllipse(eye, 43, 40 + bob, 10, 11);
        graphics.FillEllipse(eye, 60, 40 + bob, 10, 11);
        graphics.FillEllipse(ink, 47, 43 + bob, 5, 6);
        graphics.FillEllipse(ink, 64, 43 + bob, 5, 6);
        graphics.DrawArc(outline, 50, 48 + bob, 14, 10, 18, 144);

        if (moving)
        {
            using var motion = new Pen(Color.FromArgb(125, 61, 183, 178), 3);
            var offset = new[] { 0, 3, 6, 3 }[index];
            graphics.DrawArc(motion, 3 + offset, 53, 16, 25, 255, 210);
            graphics.DrawArc(motion, 91 - offset, 53, 16, 25, 75, 210);
        }

        return image;
    }
}
