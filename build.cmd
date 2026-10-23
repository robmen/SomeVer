@setlocal
@pushd %~dp0

@echo === MinVer tests ===
dotnet test .\MinVerTests.Lib\ -c Release --disable-build-servers
@if errorlevel 1 exit /b 1
@echo.

:: msbuild -Restore .\SomeVer\ -p:Configuration=Release -bl:build.binlog -nologo -tl -nr:false -m
@echo === Build SomeVer ===
dotnet build .\SomeVer\ -c Release -nologo --disable-build-servers
@if errorlevel 1 exit /b 1
@echo.

@echo === PackageReference integration tests ===
dotnet test .\SomeVerTests.Packages\ -c Release --disable-build-servers --filter FullyQualifiedName~SomeVerTests.Packages.PackageReference
@if errorlevel 1 exit /b 1

@popd
@endlocal
