@echo off

msbuild /m /t:restore,dumpit:publish /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:PublishDir="%CD%\publish\artifacts\win-x64\CLI" /p:PublishTrimmed=false /p:Configuration=Release /p:IncludeNativeLibrariesForSelfExtract=true DumpIt.sln