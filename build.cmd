@setlocal
@pushd %~dp0

dotnet test .\MinVerTests.Lib\ -c Release --disable-build-servers
@echo.

:: msbuild -Restore .\SomeVer\ -p:Configuration=Release -bl:build.binlog -nologo -tl -nr:false -m
dotnet build .\SomeVer\ -c Release -nologo --disable-build-servers

dotnet test .\SomeVerTests.Packages\ -c Release --disable-build-servers --filter FullyQualifiedName~SomeVerTests.Packages.PackageReference

@popd
@endlocal
