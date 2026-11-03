As for 03/11/2026 (SSMS 22)

SSMS 22 is based on the Visual Studio 18 (2026) shell (64-bit) and supports VSIX install through its own installer:

`C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe`

# Publish
Build the `SSMSPlus` project in Release with Visual Studio 2022, then publish `src\SSMSPlus\bin\Release\SSMSPlus.vsix` as the release asset.

Bump the version in both `src\SSMSPlus\Properties\AssemblyInfo.cs` (`AssemblyVersion` build number drives the db upgrade scripts) and `src\SSMSPlus\source.extension.vsixmanifest`.