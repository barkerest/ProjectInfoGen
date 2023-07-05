namespace ProjectInfoGen;

public class Config
{
    public string DefaultVersion { get; set; } = "0.1.0.0";
    
    public string DefaultAuthors { get; set; } = "";
    
    public string DefaultCompany { get; set; } = "";

    public WhatToIncrement WhatToIncrement { get; set; } = WhatToIncrement.Revision;

    public bool UpdateCopyright { get; set; } = true;

    public bool SaveInternalProjectInfoFile { get; set; } = true;
}
