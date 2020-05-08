# Script Composer

A simple command line tool that combines multiple files into one single script.

## How to use

In order to use this tool your scripts must be in [MSBuild](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild) project.

To combine script execute Script Composer from your project folder  with namespace as argument

```
ScriptComposer Script.Main
```

Alternativly instead of executing from project folder you can specify path to project file

```
ScriptComposer Script.Main -p path_to_csproj
```

Script will be copied to clipboard.

## How it works

Above in use examples namespace `Script.Main` was chosen as main namespace. Script Composer will take class that inherits `MyGridProgram` from main namespace as root class.

Example of such class

```csharp
namespace Script.Main
{
    Program : MyGridProgram
    {
        // your code
    }
}
```

File (or files in case of partial class) with root class will be scanned for `using statements`. Contents of used namespaces will be recursively added to final result as well as contents of root class.

## Build instructions

To build this program you will need [.NET Core SDK](https://dotnet.microsoft.com/download).

1. Clone or download zip from this repository.
2. Go to src/ScriptComposer folder and execute:
    * for simple build
        ```
        dotnet publish -c Release
        ```
        Compiled program is in bin/Release/netcoreapp3.1/publish
    * for single file build
        ```
        dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true,PublishTrimmed=true
        ```
        Compiled program is in bin/Release/netcoreapp3.1/win-x64/publish
