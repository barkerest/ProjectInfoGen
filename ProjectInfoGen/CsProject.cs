using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;

namespace ProjectInfoGen;

public class CsProject
{
    private const string PropertyGroupName = "PropertyGroup";

    public string ProjectFile { get; }

    public string ProjectDirectory { get; }

    public string InfoFile { get; }

    private readonly Config _config;

    private readonly XmlDocument _xml;
    private readonly XmlElement  _docRoot;
    private readonly XmlElement  _firstPropGroup;

    public CsProject(string path, Config config)
    {
        _config = config;

        if (string.IsNullOrWhiteSpace(path) || path == ".")
        {
            path = Environment.CurrentDirectory;
        }

        if (Directory.Exists(path))
        {
            ProjectDirectory = Path.GetFullPath(path);
            var candidates = Directory.GetFiles(ProjectDirectory, "*.csproj", SearchOption.TopDirectoryOnly);
            if (candidates.Length == 0) throw AppError.NoProjectInDirectory();
            if (candidates.Length > 1) throw AppError.TooManyProjectsInDirectory();
            ProjectFile = candidates[0];
        }
        else if (File.Exists(path))
        {
            if (!string.Equals(Path.GetExtension(path), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                throw AppError.ProjectFileIsNotCsProj();
            }

            ProjectFile      = Path.GetFullPath(path);
            ProjectDirectory = Path.GetDirectoryName(path) ?? throw AppError.ProjectFileInRoot();
            if (Directory.GetFiles(ProjectDirectory, "*.csproj", SearchOption.TopDirectoryOnly).Length > 1)
            {
                throw AppError.TooManyProjectsInDirectory();
            }
        }
        else
        {
            throw AppError.ProjectDoesNotExist();
        }

        InfoFile = Path.Join(ProjectDirectory, "InternalProjectInfo.cs");

        _xml = new XmlDocument();
        var docSettings = new XmlReaderSettings()
                          {
                              ConformanceLevel             = ConformanceLevel.Document,
                              IgnoreWhitespace             = true,
                              IgnoreProcessingInstructions = true,
                              DtdProcessing                = DtdProcessing.Ignore,
                              ValidationFlags              = XmlSchemaValidationFlags.None,
                          };
        using (var stream = File.OpenRead(ProjectFile))
        using (var reader = XmlReader.Create(stream, docSettings))
        {
            _xml.Load(reader);
        }

        if (_xml.DocumentElement is not { Name: "Project" } docRoot)
        {
            throw AppError.NotAProjectFile();
        }

        _docRoot = docRoot;

        _firstPropGroup = _docRoot.GetElementsByTagName(PropertyGroupName).Cast<XmlElement>().FirstOrDefault()
                          ?? (XmlElement)_docRoot.AppendChild(_xml.CreateElement(PropertyGroupName))!;

        // init the properties.
        _ = Product;
        _ = Version;
        _ = Authors;
        _ = Company;
        _ = Copyright;
    }

    #region Property Access Methods

    private string ReadPropertyOrDefault(string propName, string defaultValue)
    {
        var prop = _firstPropGroup.GetElementsByTagName(propName).Cast<XmlElement>().FirstOrDefault();
        if (prop is null)
        {
            WriteProperty(propName, defaultValue);
            return defaultValue;
        }

        return prop.InnerText;
    }

    private void WriteProperty(string propName, string value)
    {
        var propGroups = _docRoot.GetElementsByTagName(PropertyGroupName).Cast<XmlElement>().ToList();
        foreach (var pg in propGroups)
        {
            var el = pg!.GetElementsByTagName(propName).Cast<XmlElement>().FirstOrDefault()
                     ?? pg.AppendChild(_xml.CreateElement(propName))!;
            el.InnerText = value;
        }
    }

    #endregion

    #region Properties

    public string Product
    {
        get => ReadPropertyOrDefault("Product", Path.GetFileNameWithoutExtension(ProjectFile));
        set => WriteProperty("Product", value);
    }

    public string Version
    {
        get => ReadPropertyOrDefault("Version", _config.DefaultVersion);
        set => WriteProperty("Version", value);
    }

    public string Authors
    {
        get => ReadPropertyOrDefault("Authors", _config.DefaultAuthors);
        set => WriteProperty("Authors", value);
    }

    public string Company
    {
        get => ReadPropertyOrDefault("Company", _config.DefaultCompany);
        set => WriteProperty("Company", value);
    }

    public string Copyright
    {
        get => ReadPropertyOrDefault("Copyright", $"Copyright © {DateTime.Today.Year} {Company}");
        set => WriteProperty("Copyright", value);
    }

    #endregion

    #region Encode

    private static readonly IReadOnlyDictionary<char, string> EncodePairs = new Dictionary<char, string>()
                                                                            {
                                                                                { '\\', "\\\\" },
                                                                                { '\"', "\\\"" },
                                                                                { '\0', "\\0" },
                                                                                { '\a', "\\a" },
                                                                                { '\b', "\\b" },
                                                                                { '\f', "\\f" },
                                                                                { '\n', "\\n" },
                                                                                { '\r', "\\r" },
                                                                                { '\t', "\\t" },
                                                                                { '\v', "\\v" },
                                                                            };

    private static string Encode(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value)
        {
            if (EncodePairs.ContainsKey(c))
            {
                sb.Append(EncodePairs[c]);
            }
            else if (c < ' ' ||
                     c > 127)
            {
                sb.Append("\\u").Append(((int)c).ToString("X4"));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    #endregion

    #region Save

    public void SaveProjectFile()
    {
        using (var stream = File.Open(ProjectFile, FileMode.Truncate, FileAccess.Write, FileShare.None))
        using (var writer = XmlWriter.Create(stream,
                                             new XmlWriterSettings()
                                             {
                                                 Indent = true, OmitXmlDeclaration = true, Encoding = Encoding.UTF8,
                                             }))
        {
            _xml.WriteTo(writer);
            writer.Flush();
            stream.Flush();
        }
    }

    public void SaveInternalProjectInfoFile()
    {
        var asm = typeof(CsProject).Assembly.GetName();
        var years = CopyrightFormat.Match(Copyright) is { Success: true } match
                        ? match.Groups["Y2"].Success
                              ? $"new []{{ {match.Groups["Y1"].Value}, {match.Groups["Y2"].Value} }}"
                              : $"new []{{ {match.Groups["Y1"].Value} }}"
                        : "System.Array.Empty<int>()";

        File.WriteAllText(
                          InfoFile,
                          $@"/*
 * Internal Project Information
 *
 * Generated by {asm.Name} v{asm.Version} at {DateTime.Now:HH:mm:ss' on 'yyyy-MM-dd}.
 *
 * WARNING: This file is automatically generated by the above utility, you should not manually modify this file. 
 */

namespace {Product}
{{
    internal static class InternalProjectInfo
    {{
		public const           string Product        = ""{Encode(Product)}"";
		public const           string Version        = ""{Encode(Version)}"";
		public const           string Authors        = ""{Encode(Authors)}"";
		public const           string Company        = ""{Encode(Company)}"";
		public const           string Copyright      = ""{Encode(Copyright)}"";
        public static readonly int[]  CopyrightYears = {years};
    }}
}}",
                          Encoding.UTF8);
    }
    
    #endregion

    #region Version Helpers
    
    private Version GetVersion() => System.Version.TryParse(Version, out var version) ? version : new Version(0, 1);
    
    public void IncrementMajorVersion()
    {
        var version = GetVersion();
        var newVer  = new Version(version.Major + 1, 0, 0, 0);
        Version = newVer.ToString();
    }

    public void IncrementMinorVersion()
    {
        var version = GetVersion();
        var newVer  = new Version(version.Major, version.Minor + 1, 0, 0);
        Version = newVer.ToString();
    }

    public void IncrementRevision()
    {
        var version = GetVersion();
        var newVer  = new Version(version.Major, version.Minor, 0, version.Revision + 1);
        Version = newVer.ToString();
    }

    public void IncrementBuild()
    {
        var version = GetVersion();
        var newVer  = new Version(version.Major, version.Minor, version.Build + 1, version.Revision);
        Version = newVer.ToString();
    }
    
    #endregion

    #region Copyright Helpers
    
    public static readonly Regex CopyrightFormat =
        new(@"^copyright\s+(?:©|\(c\)|\\u00A9)?\s*(?<Y1>\d+(?:\s*-\s*(?<Y2>\d+))?)\s+(?<COMP>.*)$", RegexOptions.IgnoreCase); 
    
    public void UpdateCopyright()
    {
        var current = Copyright;
        var lines   = current.Replace("\r\n", "\n").Split('\n');
        var newVal  = new StringBuilder();
        foreach (var line in lines)
        {
            if (newVal.Length > 0) newVal.Append('\n');
            var m = CopyrightFormat.Match(line.Trim());
            if (m.Success && string.Equals(m.Groups["COMP"].Value, Company, StringComparison.OrdinalIgnoreCase))
            {
                var y1 = int.Parse(m.Groups["Y1"].Value);
                var y2 = int.Parse(m.Groups["Y2"].Success ? m.Groups["Y2"].Value : m.Groups["Y1"].Value);
                if (y2 != DateTime.Today.Year)
                {
                    newVal.Append($"Copyright © {y1}-{DateTime.Today.Year} {Company}");
                    continue;
                }
            }
            newVal.Append(line);
        }

        Copyright = newVal.ToString();
    }

    #endregion
}
