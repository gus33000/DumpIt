@echo off

msbuild /m /t:restore,dumpit:publish /p:Platform=arm64 /p:RuntimeIdentifier=win-arm64 /p:PublishDir="%CD%\publish\artifacts\win-arm64\CLI" /p:PublishTrimmed=false /p:Configuration=Release /p:IncludeNativeLibrariesForSelfExtract=true DumpIt.sln