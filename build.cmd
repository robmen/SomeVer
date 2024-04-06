@setlocal
@pushd %~dp0

dotnet test .\MinVerTests.Lib\ -c Release
@echo.

:: msbuild -Restore .\SomeVer\ -p:Configuration=Release -bl:build.binlog -nologo -tl -nr:false -m
dotnet build .\SomeVer\ -c Release -nologo

@popd
@endlocal
