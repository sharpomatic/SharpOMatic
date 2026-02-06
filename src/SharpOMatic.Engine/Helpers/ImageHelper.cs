namespace SharpOMatic.Engine.Helpers;

[SupportedOSPlatform("windows")]
public class ImageHelper
{   
    private static readonly Color[] AnnotationColors =
    [
        Color.Red,
        Color.OrangeRed,
        Color.Orange,
        Color.Gold,
        Color.YellowGreen,
        Color.LimeGreen,
        Color.Teal,
        Color.DeepSkyBlue,
        Color.RoyalBlue,
        Color.MediumPurple,
        Color.DeepPink,
        Color.Sienna
    ];

    public static List<byte[]> ExtractRectangles(byte[] imageBytes, IReadOnlyList<RectangleF> rectangles)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Image extraction requires System.Drawing on Windows.");

        if (imageBytes is null || imageBytes.Length == 0)
            throw new ArgumentException("Image bytes cannot be null or empty.", nameof(imageBytes));

        if (rectangles is null || rectangles.Count == 0)
            return [];

        using var input = new MemoryStream(imageBytes);
        using var image = Image.FromStream(input);
        using var bitmap = new Bitmap(image);

        var extractedRectangles = new List<byte[]>(rectangles.Count);

        foreach (var rectangle in rectangles)
        {
            if (!TryConvertToPixels(rectangle, bitmap.Width, bitmap.Height, out var rect))
                continue;

            using var regionBitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(regionBitmap))
            {
                graphics.DrawImage(bitmap, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
            }

            using var output = new MemoryStream();
            regionBitmap.Save(output, ImageFormat.Png);
            extractedRectangles.Add(output.ToArray());
        }

        return extractedRectangles;
    }

    public static byte[] AnnotatePoints(byte[] imageBytes, IReadOnlyList<PointF> points, int radius = 10)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Image annotation requires System.Drawing on Windows.");

        if (imageBytes is null || imageBytes.Length == 0)
            throw new ArgumentException("Image bytes cannot be null or empty.", nameof(imageBytes));

        if (points is null || points.Count == 0)
            return imageBytes;

        if (radius <= 0)
            return imageBytes;

        using var input = new MemoryStream(imageBytes);
        using var image = Image.FromStream(input);
        using var bitmap = new Bitmap(image);
        using var graphics = Graphics.FromImage(bitmap);
        DrawPoints(graphics, bitmap.Width, bitmap.Height, points, radius);

        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return output.ToArray();
    }

    public static byte[] AnnotateRectangles(byte[] imageBytes, IReadOnlyList<RectangleF> rectangles, IReadOnlyList<string>? titles = null)
    {
        if (imageBytes is null || imageBytes.Length == 0)
            throw new ArgumentException("Image bytes cannot be null or empty.", nameof(imageBytes));

        if (rectangles is null || rectangles.Count == 0)
            return imageBytes;

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Image annotation requires System.Drawing on Windows.");

        using var input = new MemoryStream(imageBytes);
        using var image = Image.FromStream(input);
        using var bitmap = new Bitmap(image);
        using var graphics = Graphics.FromImage(bitmap);
        DrawRectangles(graphics, bitmap.Width, bitmap.Height, rectangles, titles);

        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return output.ToArray();
    }

    public static Task<byte[]> AnnotatePolygons(byte[] imageBytes, IReadOnlyList<IReadOnlyList<PointF>> polygons, IReadOnlyList<string>? titles = null)
    {
        if (imageBytes is null || imageBytes.Length == 0)
            throw new ArgumentException("Image bytes cannot be null or empty.", nameof(imageBytes));

        if (polygons is null || polygons.Count == 0)
            return Task.FromResult(imageBytes);

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Image annotation requires System.Drawing on Windows.");

        using var input = new MemoryStream(imageBytes);
        using var image = Image.FromStream(input);
        using var bitmap = new Bitmap(image);
        using var graphics = Graphics.FromImage(bitmap);
        DrawPolygons(graphics, bitmap.Width, bitmap.Height, polygons, titles);

        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return Task.FromResult(output.ToArray());
    }


    private static void DrawRectangles(
        Graphics graphics, 
        int imageWidth, 
        int imageHeight, 
        IReadOnlyList<RectangleF> rectangles, 
        IReadOnlyList<string>? titles = null)
    {
        var penWidth = Math.Max(2, Math.Min(6, imageWidth / 400));
        var fontSize = Math.Max(12, Math.Min(24, imageHeight / 40));
        const int fillAlpha = 26;

        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);

        var colorIndex = 0;
        for(int i=0; i<rectangles.Count; i++)
        {
            var rectangle = rectangles[i];

            if (!TryConvertToPixels(rectangle, imageWidth, imageHeight, out var rect))
                continue;

            var color = AnnotationColors[colorIndex % AnnotationColors.Length];
            colorIndex += 1;

            using var pen = new Pen(color, penWidth);
            using var fillBrush = new SolidBrush(Color.FromArgb(fillAlpha, color));
            graphics.FillRectangle(fillBrush, rect);
            graphics.DrawRectangle(pen, rect);

            if ((titles is not null) && (titles.Count > i) && !string.IsNullOrWhiteSpace(titles[i]))
            {
                using var backgroundBrush = new SolidBrush(color);
                var textX = Math.Clamp(rect.Left + 2, 0, imageWidth - 1);
                var textY = Math.Clamp(rect.Top + 2, 0, imageHeight - 1);
                DrawLabel(graphics, titles[i], font, textBrush, backgroundBrush, imageWidth, imageHeight, textX, textY);
            }
        }
    }

    private static void DrawPolygons(
        Graphics graphics, 
        int imageWidth, 
        int imageHeight, 
        IReadOnlyList<IReadOnlyList<PointF>> polygons, 
        IReadOnlyList<string>? titles = null)
    {
        var penWidth = Math.Max(2, Math.Min(6, imageWidth / 400));
        var fontSize = Math.Max(12, Math.Min(24, imageHeight / 40));
        const int fillAlpha = 26;

        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);

        var colorIndex = 0;
        for (int i = 0; i < polygons.Count; i++)
        {
            var polygon = polygons[i];

            if (!TryConvertToPixels(polygon, imageWidth, imageHeight, out var points, out var bounds))
                continue;

            var color = AnnotationColors[colorIndex % AnnotationColors.Length];
            colorIndex += 1;

            using var pen = new Pen(color, penWidth);
            using var fillBrush = new SolidBrush(Color.FromArgb(fillAlpha, color));
            graphics.FillPolygon(fillBrush, points);
            graphics.DrawPolygon(pen, points);

            if ((titles is not null) && (titles.Count > i) && !string.IsNullOrWhiteSpace(titles[i]))
            {
                using var backgroundBrush = new SolidBrush(color);
                var textX = Math.Clamp(bounds.Left + 2, 0, imageWidth - 1);
                var textY = Math.Clamp(bounds.Top + 2, 0, imageHeight - 1);
                DrawLabel(graphics, titles[i], font, textBrush, backgroundBrush, imageWidth, imageHeight, textX, textY);
            }
        }
    }

    private static void DrawPoints(
        Graphics graphics,
        int imageWidth,
        int imageHeight,
        IReadOnlyList<PointF> points,
        int radius)
    {
        var penWidth = Math.Max(2, Math.Min(6, imageWidth / 400));
        const int fillAlpha = 26;

        var colorIndex = 0;
        foreach (var point in points)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
                continue;

            var x = Math.Clamp(point.X * imageWidth, 0, imageWidth - 1);
            var y = Math.Clamp(point.Y * imageHeight, 0, imageHeight - 1);

            var left = (float)(x - radius);
            var top = (float)(y - radius);
            var diameter = radius * 2f;

            var color = AnnotationColors[colorIndex % AnnotationColors.Length];
            colorIndex += 1;

            using var pen = new Pen(color, penWidth);
            using var fillBrush = new SolidBrush(Color.FromArgb(fillAlpha, color));
            graphics.FillEllipse(fillBrush, left, top, diameter, diameter);
            graphics.DrawEllipse(pen, left, top, diameter, diameter);
        }
    }

    private static void DrawLabel(
        Graphics graphics,
        string text,
        Font font,
        Brush textBrush,
        Brush backgroundBrush,
        int imageWidth,
        int imageHeight,
        float x,
        float y)
    {
        const float padding = 2f;
        var size = graphics.MeasureString(text, font);
        var width = size.Width + (padding * 2);
        var height = size.Height + (padding * 2);

        var clampedX = Math.Clamp(x, 0, imageWidth - 1);
        var clampedY = Math.Clamp(y, 0, imageHeight - 1);
        var maxWidth = Math.Max(1, imageWidth - clampedX);
        var maxHeight = Math.Max(1, imageHeight - clampedY);

        if (width > maxWidth)
            width = maxWidth;
        if (height > maxHeight)
            height = maxHeight;

        var backgroundRect = new RectangleF(clampedX, clampedY, width, height);
        graphics.FillRectangle(backgroundBrush, backgroundRect);
        graphics.DrawString(text, font, textBrush, clampedX + padding, clampedY + padding);
    }

    private static bool TryConvertToPixels(RectangleF region, int imageWidth, int imageHeight, out Rectangle rect)
    {
        rect = Rectangle.Empty;

        var x = region.X;
        var y = region.Y;
        var width = region.Width;
        var height = region.Height;

        // Regions are specified as percentages of the image dimensions.
        x *= imageWidth;
        y *= imageHeight;
        width *= imageWidth;
        height *= imageHeight;

        if (width <= 0 || height <= 0)
            return false;

        var left = (int)Math.Round(x);
        var top = (int)Math.Round(y);
        var right = (int)Math.Round(x + width);
        var bottom = (int)Math.Round(y + height);

        left = Math.Clamp(left, 0, imageWidth - 1);
        top = Math.Clamp(top, 0, imageHeight - 1);
        right = Math.Clamp(right, left + 1, imageWidth);
        bottom = Math.Clamp(bottom, top + 1, imageHeight);

        rect = Rectangle.FromLTRB(left, top, right, bottom);
        return true;
    }

    private static bool TryConvertToPixels(
        IReadOnlyList<PointF> polygon,
        int imageWidth,
        int imageHeight,
        out PointF[] points,
        out RectangleF bounds)
    {
        points = [];
        bounds = RectangleF.Empty;

        if (polygon is null || polygon.Count < 3)
            return false;

        var tmpPoints = new List<PointF>(polygon.Count);
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;

        foreach (var point in polygon)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
                return false;

            var x = Math.Clamp(point.X * imageWidth, 0, imageWidth - 1);
            var y = Math.Clamp(point.Y * imageHeight, 0, imageHeight - 1);

            var xf = (float)x;
            var yf = (float)y;

            tmpPoints.Add(new PointF(xf, yf));

            minX = Math.Min(minX, xf);
            minY = Math.Min(minY, yf);
            maxX = Math.Max(maxX, xf);
            maxY = Math.Max(maxY, yf);
        }

        if (tmpPoints.Count < 3)
            return false;

        points = [.. tmpPoints];
        bounds = RectangleF.FromLTRB(minX, minY, maxX, maxY);
        return true;
    }
}
