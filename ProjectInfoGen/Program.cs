using System.Text.Json;

namespace ProjectInfoGen;

public static class Program
{
    private static Config GetConfig(string[] args)
    {
        var path = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                             "ProjectInfoGen");
        
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        path = Path.Join(path, "config.json");
        
        var    save = false;
        Config ret;

        if (File.Exists(path))
        {
            ret = JsonSerializer.Deserialize<Config>(File.ReadAllText(path)) ?? new Config();
        }
        else
        {
            save = true;
            ret  = new Config();
        }

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            switch (args[i])
            {
                case "--default-authors":
                case "--default-author":
                    save               = true;
                    ret.DefaultAuthors = args[i];
                    break;
                case "--default-company":
                    save               = true;
                    ret.DefaultCompany = args[i];
                    break;
                case "--default-version":
                    save               = true;
                    ret.DefaultVersion = args[i];
                    break;
            }
        }
        

        if (string.IsNullOrWhiteSpace(ret.DefaultAuthors))
        {
            save = true;
            Console.Write("Enter value for Authors: ");
            var val = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(val)) throw AppError.MissingConfiguration();
            ret.DefaultAuthors = val;
        }

        if (string.IsNullOrWhiteSpace(ret.DefaultCompany))
        {
            save = true;
            Console.Write("Enter value for Company: ");
            var val = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(val)) throw AppError.MissingConfiguration();
            ret.DefaultCompany = val;
        }

        if (string.IsNullOrWhiteSpace(ret.DefaultVersion))
        {
            save               = true;
            ret.DefaultVersion = "0.1.0.0";
        }

        if (save)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(ret));
        }

        return ret;
    }
    
    public static int Main(string[] args)
    {
        try
        {
            var     config     = GetConfig(args);
            
            var     addAuthors = new List<string>();
            string? setCompany = null;

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--update-copyright":
                        config.UpdateCopyright = true;
                        break;
                    case "--no-update-copyright":
                        config.UpdateCopyright = false;
                        break;
                    case "--save-info-class":
                        config.SaveInternalProjectInfoFile = true;
                        break;
                    case "--no-save-info-class":
                        config.SaveInternalProjectInfoFile = false;
                        break;
                    case "--increment-none":
                        config.WhatToIncrement = WhatToIncrement.None;
                        break;
                    case "--increment-major":
                        config.WhatToIncrement = WhatToIncrement.Major;
                        break;
                    case "--increment-minor":
                        config.WhatToIncrement = WhatToIncrement.Minor;
                        break;
                    case "--increment-revision":
                    case "--increment-release":
                        config.WhatToIncrement = WhatToIncrement.Revision;
                        break;
                    case "--increment-build":
                        config.WhatToIncrement = WhatToIncrement.Build;
                        break;
                    case "--add-author":
                        i++;
                        if (i >= args.Length) throw new ArgumentException("missing argument for --add-author");
                        addAuthors.Add(args[i]);
                        break;
                    case "--set-company":
                        i++;
                        if (i >= args.Length) throw new ArgumentException("missing argument for --set-company");
                        setCompany = args[i];
                        break;
                    
                    default:
                        throw new ArgumentException("Unknown argument: " + args[i]);
                }
            }

            // load the project and ensure the properties are populated.
            var project = new CsProject(args.Length == 0 || args[0].StartsWith("--") ? "" : args[0], config);

            if (addAuthors.Any())
            {
                foreach (var author in project.Authors.Split('\n'))
                {
                    if (!addAuthors.Contains(author)) addAuthors.Add(author);
                }
                addAuthors.Sort();
                project.Authors = string.Join("\n", addAuthors);
            }

            if (!string.IsNullOrWhiteSpace(setCompany))
            {
                project.Company = setCompany;
            }
            
            if (config.UpdateCopyright)
            {
                project.UpdateCopyright();
            }

            var before = project.Version;
            
            switch (config.WhatToIncrement)
            {
                case WhatToIncrement.None:
                    break;
                case WhatToIncrement.Major:
                    project.IncrementMajorVersion();
                    break;
                case WhatToIncrement.Minor:
                    project.IncrementMinorVersion();
                    break;
                case WhatToIncrement.Revision:
                    project.IncrementRevision();
                    break;
                case WhatToIncrement.Build:
                    project.IncrementBuild();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var after = project.Version;
            
            
            Console.WriteLine($"Updated {project.Product}  {before} => {after}");
            project.SaveProjectFile();
            if (config.SaveInternalProjectInfoFile)
            {
                project.SaveInternalProjectInfoFile();
            }
        }
        catch (AppError err)
        {
            Console.WriteLine($"ERROR: {err.Message}");
            return err.RetVal;
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine("Argument Error: " + ex.Message);
            return 1;
        }

        return 0;
    }
}
