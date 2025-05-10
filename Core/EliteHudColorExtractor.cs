// Add to Core folder
using Serilog;
using System.IO;
using System.Windows.Media;
using System.Xml.Linq;

public class EliteHudColorExtractor
{
    public readonly string GraphicsConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Frontier Developments", "Elite Dangerous", "Options", "Graphics", "GraphicsConfiguration.xml");

    public EliteHudColors ExtractColors()
    {
        try
        {
            if (!File.Exists(GraphicsConfigPath))
            {
                Log.Warning("GraphicsConfiguration.xml not found at {Path}", GraphicsConfigPath);
                return GetDefaultColors();
            }

            var xml = XDocument.Load(GraphicsConfigPath);

            // Find the HUD color matrix
            var guiColour = xml.Descendants("GraphicsConfig")
                               .Elements("GUIColour")
                               .FirstOrDefault();

            if (guiColour != null)
            {
                // Parse the 3x3 matrix values
                var matrix = ParseColorMatrix(guiColour);
                return CalculateHudColors(matrix);
            }

            return GetDefaultColors();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to extract HUD colors");
            return GetDefaultColors();
        }
    }

    private double[,] ParseColorMatrix(XElement guiColour)
    {
        var matrix = new double[3, 3];

        // Elite stores the matrix in this format:
        // MatrixRed, MatrixGreen, MatrixBlue attributes
        // Each contains space-separated RGB values

        var matrixRed = guiColour.Element("MatrixRed")?.Attribute("Red")?.Value;
        var matrixGreen = guiColour.Element("MatrixGreen")?.Attribute("Green")?.Value;
        var matrixBlue = guiColour.Element("MatrixBlue")?.Attribute("Blue")?.Value;

        // Parse the values (they're in format like "1.000000 0.000000 0.000000")
        ParseMatrixRow(matrixRed, matrix, 0);
        ParseMatrixRow(matrixGreen, matrix, 1);
        ParseMatrixRow(matrixBlue, matrix, 2);

        return matrix;
    }

    private void ParseMatrixRow(string values, double[,] matrix, int row)
    {
        if (string.IsNullOrEmpty(values)) return;

        var parts = values.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < Math.Min(parts.Length, 3); i++)
        {
            if (double.TryParse(parts[i], out double value))
            {
                matrix[row, i] = value;
            }
        }
    }

    private EliteHudColors CalculateHudColors(double[,] matrix)
    {
        // Apply the matrix to base Elite colors to get actual HUD colors
        var colors = new EliteHudColors
        {
            // Main HUD color (usually orange by default)
            HudMain = ApplyMatrix(matrix, new Color { R = 255, G = 119, B = 0 }),

            // Secondary colors
            HudSecondary = ApplyMatrix(matrix, new Color { R = 204, G = 95, B = 0 }),

            // Text color
            HudText = ApplyMatrix(matrix, new Color { R = 255, G = 140, B = 0 }),

            // Background color
            HudBackground = ApplyMatrix(matrix, new Color { R = 0, G = 0, B = 0 }),

            // Warning/Alert colors
            HudWarning = ApplyMatrix(matrix, new Color { R = 255, G = 0, B = 0 }),

            // Success/Positive colors
            HudSuccess = ApplyMatrix(matrix, new Color { R = 0, G = 255, B = 0 })
        };

        return colors;
    }

    private System.Windows.Media.Color ApplyMatrix(double[,] matrix, Color baseColor)
    {
        // Apply 3x3 color transformation matrix
        double r = matrix[0, 0] * baseColor.R + matrix[0, 1] * baseColor.G + matrix[0, 2] * baseColor.B;
        double g = matrix[1, 0] * baseColor.R + matrix[1, 1] * baseColor.G + matrix[1, 2] * baseColor.B;
        double b = matrix[2, 0] * baseColor.R + matrix[2, 1] * baseColor.G + matrix[2, 2] * baseColor.B;

        // Clamp values
        r = Math.Max(0, Math.Min(255, r));
        g = Math.Max(0, Math.Min(255, g));
        b = Math.Max(0, Math.Min(255, b));

        return System.Windows.Media.Color.FromRgb((byte)r, (byte)g, (byte)b);
    }

    private EliteHudColors GetDefaultColors()
    {
        // Default Elite orange theme
        return new EliteHudColors
        {
            HudMain = Colors.Orange,
            HudSecondary = Color.FromRgb(204, 95, 0),
            HudText = Color.FromRgb(255, 140, 0),
            HudBackground = Colors.Black,
            HudWarning = Colors.Red,
            HudSuccess = Colors.LimeGreen
        };
    }
}

public class EliteHudColors
{
    public System.Windows.Media.Color HudMain { get; set; }
    public System.Windows.Media.Color HudSecondary { get; set; }
    public System.Windows.Media.Color HudText { get; set; }
    public System.Windows.Media.Color HudBackground { get; set; }
    public System.Windows.Media.Color HudWarning { get; set; }
    public System.Windows.Media.Color HudSuccess { get; set; }
}