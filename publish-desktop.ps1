#!/usr/bin/env pwsh
# Publish HandsLiftedApp.Desktop as a single-file win-x64 build.
dotnet publish HandsLiftedApp.Desktop/HandsLiftedApp.Desktop.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o HandsLiftedApp.Desktop/bin/Release/net10.0/publish
