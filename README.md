# WinDepends
[![Build status](https://ci.appveyor.com/api/projects/status/015k6sl9g3p6lfsm?svg=true)](https://ci.appveyor.com/project/hfiref0x/windepends)
![Visitors](https://api.visitorbadge.io/api/visitors?path=https%3A%2F%2Fgithub.com%2Fhfiref0x%2FWinDepends&label=Visitors&countColor=%23263759&style=flat)

## Windows Dependencies

#### System Requirements

##### Windows Operating System:
+ Microsoft Windows 10/11 (Including Server variants)
+ Windows 8.1 (Not Officially Supported)
+ Windows 7 (Not Officially Supported, refer to https://github.com/hfiref0x/WinDepends/issues/11 for more info)

##### Runtime Frameworks:
+ [.NET Desktop Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)


# Project Overview

WinDepends is a rewrite of the [Dependency Walker](https://www.dependencywalker.com/) utility, which for a long time was a "must-have" tool when it comes to Windows PE files analysis and building a hierarchical tree diagram of all dependent modules. Unfortunately, development of this tool stopped around the Windows Vista release, and since that time, Microsoft introduced a lot of new features "under the hood" of the loader that eventually broke Dependency Walker and made its use painful, especially on the newest Windows versions with tons of artificial DLLs, a.k.a. ApiSet contracts. Unfortunately, none of the existing "replacements" are even slightly comparable to the original in terms of implementation or features. That's why this project was born. It was in mind for many years but has never had enough time or will to be implemented until now.

<img src="https://raw.githubusercontent.com/hfiref0x/WinDepends.Docs/master/help/img/MainWindowConsent.png" width="1010" />

### Utility Features

* Scans any 32-bit or 64-bit Windows module (exe, dll, ocx, sys, etc.) and builds a hierarchical tree diagram of all dependent modules. For each module found, it lists all functions exported by that module and which are called by other modules.
* Displays the minimum set of required files, along with detailed information about each file, including full paths, base addresses, version numbers, machine type, debug information, and more.
*  Supports delay-load DLLs, ApiSet contracts, bound imports, and Side-by-Side modules.
   * Supported ApiSet schema versions: V2 (Win7), V4 (Win8/8.1), V6 (Win10 and above).
*  Drag-and-drop support with a most recently used files list.
*  Supports custom configuration, external viewer, external help command, module path resolution, search order and PE loader relocations settings.
*  Supports Microsoft Debug Symbols to provide more information on modules exports/imports.
*  Supports C++ function name undecorating to provide human readable C++ function prototypes including function names, return types, and parameter types.
*  Save/restore sessions to/from files.
*  Client-server architecture: Client (WinForms .NET app) provides the GUI; server (C application) handles PE parsing.

### Missing features / Known issues

* Current state: **BETA**. Some Dependency Walker features are unimplemented (e.g., profiling).
* MDI GUI discontinued; launch multiple instances to analyze multiple files.
* Some functionality may not work as expected or be disabled in beta.
* CLI version not yet implemented (and unlikely will be).
* ARM binaries untested in native environments (lack of bare-metal hardware).
* Some limitations stem from Windows OS support.
* Found a bug? Have suggestions? Submit issues or pull requests! We appreciate your input!

# Installation and Usage

The WinDepends compiled binaries include:
+ WinDepends.exe: Main GUI (client)
+ WinDepends.Core.exe: Server (launched by the client)
+ PDB files for both
  
They can be found in the Release section of this repository.

No installation required. Copy the folder, run WinDepends.exe. To uninstall, close the client/server and delete files.

# Documentation and Help

* https://github.com/hfiref0x/WinDepends.Docs

# Building and Other Information

+ Build platform: Microsoft Visual Studio 2022 (latest SDK).
+ Client: C# (WinForms, .NET 8.0).
+ Server: C (no special SDKs/headers).
+ Source code includes: Server tests (WinDepends.Core.Tests) and a fuzzer (WinDepends.Core.Fuzz).
+ Modern style toolbar images: https://icons8.com.
+ Frameworks/SDKs updated only with LTS releases (e.g., .NET 10).

# Support Our Work

If you enjoy using this software and would like to help the authors maintain and improve it, please consider supporting us with a donation. Your contribution fuels development, ensures updates, and keeps the project alive.

### Cryptocurrency Donations:

BTC (Bitcoin): bc1qzkvtpa0053cagf35dqmpvv9k8hyrwl7krwdz84q39mcpy68y6tmqsju0g4

This is purely optional, thank you!~

# License

MIT

# Authors

(c) 2024 - 2025 WinDepends Project
