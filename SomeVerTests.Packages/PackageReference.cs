using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using MinVerTests.Infra;
using Xunit;

namespace SomeVerTests.Packages;

#pragma warning disable CA1515 // xUnit v2 requires public test classes.
public static class PackageReference
{
    [Fact]
    public static async Task SupportsCentralPackageManagementMultiTargetingTemplatesAndTheSdk()
    {
        var path = MethodBase.GetCurrentMethod().GetTestDirectory();
        var feed = Path.Combine(path, "feed");
        var packages = Path.Combine(path, "packages");
        var directConsumer = Path.Combine(path, "direct-consumer");
        var consumer = Path.Combine(path, "consumer");
        var sdkConsumer = Path.Combine(path, "sdk-consumer");
        var hybridConsumer = Path.Combine(path, "hybrid-consumer");
        _ = Directory.CreateDirectory(feed);
        _ = Directory.CreateDirectory(packages);
        _ = Directory.CreateDirectory(directConsumer);
        _ = Directory.CreateDirectory(consumer);

        var repositoryRoot = Path.GetFullPath(Path.Combine(typeof(PackageReference).Assembly.Location, "../../../../../"));
        var packageOutputDirectory = Path.Combine(repositoryRoot, "SomeVer", "bin", "Release");
        var builtPackage = Directory.EnumerateFiles(packageOutputDirectory, "SomeVer.*.nupkg")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
        var someVerPackage = Path.Combine(feed, Path.GetFileName(builtPackage));
        File.Copy(builtPackage, someVerPackage, overwrite: true);
        var packageVersion = Path.GetFileNameWithoutExtension(someVerPackage).Split("SomeVer.", 2)[1];

        await File.WriteAllTextAsync(
            Path.Combine(consumer, "Directory.Packages.props"),
            $"""
            <Project>
              <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
              <ItemGroup><PackageVersion Include="SomeVer" Version="{packageVersion}" /></ItemGroup>
            </Project>
            """).ConfigureAwait(true);

        await File.WriteAllTextAsync(
            Path.Combine(consumer, "Consumer.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0;net10.0-windows</TargetFrameworks>
                <SomeVerTagPrefix>v</SomeVerTagPrefix>
                <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
                <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="SomeVer" PrivateAssets="all" />
                <VersionTemplate Include="version.txt.template" OutputPath="$(MSBuildProjectDirectory)\generated.txt" />
              </ItemGroup>
            </Project>
            """).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(consumer, "Class1.cs"), "public class Class1 { }").ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(consumer, "version.txt.template"), "{packageversion}|{fileversion}").ConfigureAwait(true);
        await File.WriteAllTextAsync(
            Path.Combine(directConsumer, "Direct.csproj"),
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <SomeVerTagPrefix>v</SomeVerTagPrefix>
                <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
                <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
              </PropertyGroup>
              <ItemGroup><PackageReference Include="SomeVer" Version="{{packageVersion}}" PrivateAssets="all" /></ItemGroup>
            </Project>
            """).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(directConsumer, "Class1.cs"), "public class Class1 { }").ConfigureAwait(true);

        await Git.Init(path).ConfigureAwait(true);
        await Git.Commit(path).ConfigureAwait(true);
        await Git.Tag(path, "v1.2.3").ConfigureAwait(true);

        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["NUGET_PACKAGES"] = packages, };
        _ = await Sdk.DotNet($"restore \"{Path.Combine(directConsumer, "Direct.csproj")}\" --source \"{feed}\" --packages \"{packages}\" -p:NuGetAudit=false", path, envVars: env).ConfigureAwait(true);
        _ = await Sdk.DotNet($"build \"{Path.Combine(directConsumer, "Direct.csproj")}\" --no-restore --configuration Debug --nologo", path, envVars: env).ConfigureAwait(true);
        var directAssemblyInfo = await File.ReadAllTextAsync(Path.Combine(directConsumer, "obj", "Debug", "net10.0", "Direct.AssemblyInfo.cs")).ConfigureAwait(true);
        Assert.Contains("AssemblyVersionAttribute(\"1.0.0.0\")", directAssemblyInfo, StringComparison.Ordinal);
        Assert.Contains("AssemblyFileVersionAttribute(\"1.2.3.0\")", directAssemblyInfo, StringComparison.Ordinal);
        Assert.Contains("AssemblyInformationalVersionAttribute(\"1.2.3\")", directAssemblyInfo, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(directConsumer, "bin", "Debug", "net10.0", "SomeVerTasks.dll")));
        var directPackageOutput = Path.Combine(path, "direct-packages");
        _ = Directory.CreateDirectory(directPackageOutput);
        _ = await Sdk.DotNet($"pack \"{Path.Combine(directConsumer, "Direct.csproj")}\" --no-restore --configuration Debug --output \"{directPackageOutput}\" -p:NoPackageAnalysis=true", path, envVars: env).ConfigureAwait(true);
        Assert.True(File.Exists(Path.Combine(directPackageOutput, "Direct.1.2.3.nupkg")));

        _ = await Sdk.DotNet($"restore \"{Path.Combine(consumer, "Consumer.csproj")}\" --source \"{feed}\" --packages \"{packages}\" -p:NuGetAudit=false", path, envVars: env).ConfigureAwait(true);
        var (buildOutput, _) = await Sdk.DotNet($"build \"{Path.Combine(consumer, "Consumer.csproj")}\" --no-restore --configuration Debug --nologo", path, envVars: env).ConfigureAwait(true);

        Assert.Contains("Consumer ->", buildOutput, StringComparison.Ordinal);
        Assert.Equal("1.2.3|1.2.3.0", (await File.ReadAllTextAsync(Path.Combine(consumer, "generated.txt")).ConfigureAwait(true)).TrimEnd('\r', '\n'));

        foreach (var framework in new[] { "net10.0", "net10.0-windows" })
        {
            var assemblyInfo = await File.ReadAllTextAsync(Path.Combine(consumer, "obj", "Debug", framework, "Consumer.AssemblyInfo.cs")).ConfigureAwait(true);
            Assert.Contains("AssemblyVersionAttribute(\"1.0.0.0\")", assemblyInfo, StringComparison.Ordinal);
            Assert.Contains("AssemblyFileVersionAttribute(\"1.2.3.0\")", assemblyInfo, StringComparison.Ordinal);
            Assert.Contains("AssemblyInformationalVersionAttribute(\"1.2.3\")", assemblyInfo, StringComparison.Ordinal);
        }

        Assert.False(File.Exists(Path.Combine(consumer, "bin", "Debug", "net10.0", "SomeVerTasks.dll")));
        Assert.False(File.Exists(Path.Combine(consumer, "bin", "Debug", "net10.0", "MinVer.Lib.dll")));
        Assert.False(File.Exists(Path.Combine(consumer, "bin", "Debug", "net10.0", "NuGet.Versioning.dll")));

        var packedConsumer = Path.Combine(path, "consumer-packages");
        _ = Directory.CreateDirectory(packedConsumer);
        _ = await Sdk.DotNet($"pack \"{Path.Combine(consumer, "Consumer.csproj")}\" --no-restore --configuration Debug --output \"{packedConsumer}\" -p:NoPackageAnalysis=true", path, envVars: env).ConfigureAwait(true);
        Assert.True(File.Exists(Path.Combine(packedConsumer, "Consumer.1.2.3.nupkg")));

        using (var archive = await ZipFile.OpenReadAsync(someVerPackage).ConfigureAwait(true))
        {
            Assert.Contains(archive.Entries, entry => entry.FullName == "tools/net472/SomeVerTasks.dll");
            Assert.Contains(archive.Entries, entry => entry.FullName == "tools/net10.0/SomeVerTasks.dll");
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal));

            var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
            using var stream = await nuspec.OpenAsync(CancellationToken.None).ConfigureAwait(true);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None).ConfigureAwait(true);
            Assert.Null(document.Descendants().FirstOrDefault(element => element.Name.LocalName == "dependencies"));
        }

        await Git.Commit(path).ConfigureAwait(true);
        _ = await Sdk.DotNet($"build \"{Path.Combine(consumer, "Consumer.csproj")}\" --no-restore --configuration Debug --nologo", path, envVars: env).ConfigureAwait(true);
        Assert.Equal("1.2.4-dev.1|1.2.4.1", (await File.ReadAllTextAsync(Path.Combine(consumer, "generated.txt")).ConfigureAwait(true)).TrimEnd('\r', '\n'));

        var templateAfterVersionChange = await File.ReadAllTextAsync(Path.Combine(consumer, "generated.txt")).ConfigureAwait(true);
        _ = await Sdk.DotNet($"build \"{Path.Combine(consumer, "Consumer.csproj")}\" --no-restore --configuration Debug --nologo -p:SomeVerSkip=true", path, envVars: env).ConfigureAwait(true);
        Assert.Equal(templateAfterVersionChange, await File.ReadAllTextAsync(Path.Combine(consumer, "generated.txt")).ConfigureAwait(true));
        _ = await Sdk.DotNet($"msbuild \"{Path.Combine(consumer, "Consumer.csproj")}\" -t:SomeVerBuild -p:DesignTimeBuild=true -v:minimal", path, envVars: env).ConfigureAwait(true);
        Assert.Equal(templateAfterVersionChange, await File.ReadAllTextAsync(Path.Combine(consumer, "generated.txt")).ConfigureAwait(true));

        _ = Directory.CreateDirectory(sdkConsumer);
        await File.WriteAllTextAsync(
            Path.Combine(sdkConsumer, "global.json"),
            $$"""
            { "msbuild-sdks": { "SomeVer": "{{packageVersion}}" } }
            """).ConfigureAwait(true);
        await File.WriteAllTextAsync(
            Path.Combine(sdkConsumer, "Version.verproj"),
            """
            <Project Sdk="SomeVer">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><SomeVerTagPrefix>v</SomeVerTagPrefix></PropertyGroup>
              <ItemGroup><VersionTemplate Include="version.txt.template" OutputPath="$(MSBuildProjectDirectory)\generated.txt" /></ItemGroup>
            </Project>
            """).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(sdkConsumer, "version.txt.template"), "{packageversion}").ConfigureAwait(true);
        _ = await Sdk.DotNet($"restore \"{Path.Combine(sdkConsumer, "Version.verproj")}\" --source \"{feed}\" --packages \"{packages}\" -p:NuGetAudit=false", sdkConsumer, envVars: env).ConfigureAwait(true);
        _ = await Sdk.DotNet($"build \"{Path.Combine(sdkConsumer, "Version.verproj")}\" --no-restore --nologo", sdkConsumer, envVars: env).ConfigureAwait(true);
        Assert.Equal("1.2.4-dev.1", (await File.ReadAllTextAsync(Path.Combine(sdkConsumer, "generated.txt")).ConfigureAwait(true)).TrimEnd('\r', '\n'));

        _ = Directory.CreateDirectory(hybridConsumer);
        await File.WriteAllTextAsync(Path.Combine(hybridConsumer, "global.json"), await File.ReadAllTextAsync(Path.Combine(sdkConsumer, "global.json")).ConfigureAwait(true)).ConfigureAwait(true);
        await File.WriteAllTextAsync(
            Path.Combine(hybridConsumer, "Hybrid.verproj"),
            $$"""
            <Project Sdk="SomeVer">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><SomeVerTagPrefix>v</SomeVerTagPrefix></PropertyGroup>
              <ItemGroup><PackageReference Include="SomeVer" Version="{{packageVersion}}" PrivateAssets="all" /></ItemGroup>
            </Project>
            """).ConfigureAwait(true);
        _ = await Sdk.DotNet($"restore \"{Path.Combine(hybridConsumer, "Hybrid.verproj")}\" --source \"{feed}\" --packages \"{packages}\" -p:NuGetAudit=false", hybridConsumer, envVars: env).ConfigureAwait(true);
        var (hybridOutput, _) = await Sdk.DotNet($"build \"{Path.Combine(hybridConsumer, "Hybrid.verproj")}\" --no-restore --nologo -p:SomeVerVerbosity=detailed", hybridConsumer, envVars: env).ConfigureAwait(true);
        Assert.DoesNotContain("warning MSB4011", hybridOutput, StringComparison.Ordinal);
        Assert.Equal(1, hybridOutput.Split("SomeVer: [input] MSBuildProjectDirectory=", StringSplitOptions.None).Length - 1);
    }
}
#pragma warning restore CA1515

