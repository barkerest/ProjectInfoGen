namespace ProjectInfoGen;

public class AppError : ApplicationException
{
    public int RetVal { get; }

    private AppError(int retVal, string message) : base(message)
    {
        RetVal = retVal;
    }

    public static AppError ProjectDoesNotExist()    => new(127, "Specified project does not exist");
    public static AppError NoProjectInDirectory()   => new(126, "No project files exist in target directory");
    public static AppError ProjectFileIsNotCsProj() => new(125, "Only CSPROJ files can be processed");
    public static AppError ProjectFileInRoot()      => new(124, "Project file cannot be in root path");

    public static AppError TooManyProjectsInDirectory() =>
        new(123, "More than one project file exists in target directory");

    public static AppError NotAProjectFile() =>
        new(122, "The project file does not appear to be a valid Project XML document");

    public static AppError MissingConfiguration() => new(121, "The configuration is missing a required value");
}
