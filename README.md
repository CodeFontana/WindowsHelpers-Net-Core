# WindowsHelpers

Useful set of helper methods for C# in the Microsoft Windows environment.

[![NuGet](https://img.shields.io/nuget/v/CodeFoxtrot.WindowsHelpers)](https://www.nuget.org/packages/CodeFoxtrot.WindowsHelpers)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Installation

```powershell
dotnet add package CodeFoxtrot.WindowsHelpers
```

## Features

- Registry, Services, Processes, and Privileges
- File System, Hosts file, and Page file management
- Network adapters and WMI queries
- Native P/Invoke wrappers and unsafe helpers
- Group Policy support (`LocalPolicyLibrary`)

## Quick Example

```csharp
using WindowsLibrary;

var winHelper = new WindowsHelper(logger, registryHelper);
winHelper.RebootSystem(delaySeconds: 30, comment: "Scheduled maintenance");
```

## Target Frameworks

- .NET 8.0 / 9.0 / 10.0 (Windows)

## License

MIT © CodeFoxtrot

Repository: https://github.com/CodeFontana/WindowsHelpers-Net-Core