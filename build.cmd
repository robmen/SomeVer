@setlocal
@pushd %~dp0

dotnet test .\MinVerTests.Lib\ -c Release
@echo.

dotnet build .\SomeVer\ -c Release -nologo

@popd
@endlocal
