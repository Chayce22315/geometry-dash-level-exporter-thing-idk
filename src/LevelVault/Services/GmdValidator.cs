using System.Xml.Linq;

namespace LevelVault.Services;

public sealed record GmdValidationResult(bool IsValid, string Summary, IReadOnlyList<string> Keys);

public static class GmdValidator
{
    public static GmdValidationResult Validate(string path)
    {
        try
        {
            var document = XDocument.Load(path, LoadOptions.None);
            var root = document.Root;
            if (root?.Name.LocalName != "plist")
                return new(false, "The file is XML, but its root element is not a plist.", []);

            var dict = root.Elements().FirstOrDefault(e => e.Name.LocalName == "dict");
            if (dict is null)
                return new(false, "No plist dictionary was found.", []);

            var keys = dict.Elements()
                .Where(e => e.Name.LocalName == "key")
                .Select(e => e.Value)
                .ToArray();

            var hasLevelData = keys.Any(k => k is "k4" or "kCEK");
            var hasName = keys.Contains("k2");
            if (!hasLevelData)
                return new(false, "Valid plist container, but no recognized Geometry Dash level-data key (k4/kCEK) was found.", keys);

            return new(true, hasName
                ? "Recognized Geometry Dash plist structure. Level data key found."
                : "Recognized level-data key found; name metadata is absent.", keys);
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException or UnauthorizedAccessException)
        {
            return new(false, $"Could not parse file: {ex.Message}", []);
        }
    }
}
