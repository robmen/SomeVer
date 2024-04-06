namespace MinVer.Lib;

public record VersionData(
    string Version,
    int Major,
    int Minor,
    int Patch,
    string Label,
    int Height,
    int FullHeight,
    string Sha,
    string ShortSha
    );
