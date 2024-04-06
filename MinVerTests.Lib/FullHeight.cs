using System.Reflection;
using MinVer.Lib;
using MinVerTests.Infra;
using MinVerTests.Lib.Infra;
using Xunit;
using static MinVerTests.Infra.Git;

namespace MinVerTests.Lib;

public static class FullHeight
{
    [Theory]
    [InlineData("1.2.3", "1.2.4-dev.2", 2, 2)]
    [InlineData("1.2.3-rc.1", "1.2.3-rc.1.dev.2", 2, 2)]
    [InlineData("0.0.0", "0.0.1-dev.1", 1, 1)]
    public static async Task ExpectedFullHeight(string tag, string expectedVersion, int additionalCommits, int expectedFullHeight)
    {
        // arrange
        var path = MethodBase.GetCurrentMethod().GetTestDirectory(tag);
        await EnsureEmptyRepositoryAndCommit(path);
        await Tag(path, tag);

        for (var i = 0; i < additionalCommits; i++)
        {
            await Commit(path);
        }

        // act
        var actualData = Versioner.GetVersionData(path, "", MajorMinor.Default, "", default, new[] { "dev" }, false, true, NullLogger.Instance);

        // assert
        Assert.Equal(expectedVersion, actualData.Version);
        Assert.Equal(expectedFullHeight, actualData.FullHeight);
    }

    [Theory]
    [InlineData("1.2.3", 2, "1.2.3-rc.1", 2, "1.2.3-rc.1.2", 4)]
    [InlineData("1.2.3-rc.1", 1, "1.2.3-rc.2", 3, "1.2.3-rc.2.3", 4)]
    [InlineData("0.0.0", 1, "", 5, "0.0.1-dev.6", 6)]
    public static async Task ExpectedFullHeightWithMultipleTags(string tag, int additionalCommits, string additionalTag, int moreCommits, string expectedVersion, int expectedFullHeight)
    {
        // arrange
        var path = MethodBase.GetCurrentMethod().GetTestDirectory(tag);
        await EnsureEmptyRepositoryAndCommit(path);
        await Tag(path, tag);

        for (var i = 0; i < additionalCommits; i++)
        {
            await Commit(path);
        }

        if (!string.IsNullOrEmpty(additionalTag))
        {
            await Tag(path, additionalTag);
        }

        for (var i = 0; i < moreCommits; i++)
        {
            await Commit(path);
        }

        // act
        var actualData = Versioner.GetVersionData(path, "", MajorMinor.Default, "", default, new[] { "dev" }, false, false, NullLogger.Instance);

        // assert
        Assert.Equal(expectedVersion, actualData.Version);
        Assert.Equal(expectedFullHeight, actualData.FullHeight);
    }
}
