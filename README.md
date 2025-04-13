[![Build status](https://ci.appveyor.com/api/projects/status/015k6sl9g3p6lfsm?svg=true)](https://ci.appveyor.com/project/hfiref0x/windepends)

# WinDepends
## Windows Dependencies

#### System Requirements

##### Windows Operating System:
+ Microsoft Windows 10/11 (Including Server variants)
+ Windows 8.1 (Not Officially Supported)
+ Windows 7 (Not Officially Supported, refer to https://github.com/hfiref0x/WinDepends/issues/11 for more info)

##### Runtime Frameworks:
+ [.NET Desktop Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)


# Project Overview

WinDepends is a rewrite of the [Dependency Walker](https://www.dependencywalker.com/) utility, which for a long time was a "must-have" tool when it comes to Windows PE files analysis and building a hierarchical tree diagram of all dependent modules. Unfortunately, development of this tool stopped around the Windows Vista release, and since that time, Microsoft introduced a lot of new features "under the hood" of the loader that eventually broke Dependency Walker and made its use painful, especially on the newest Windows versions with tons of artificial DLLs, a.k.a. ApiSet contracts. Unfortunately, none of the existing "replacements" are even slightly comparable to the original in terms of implementation or features. That's why this project was born. It was in the mind for many years but has never had enough time or will to be implemented until these days.

<img src="https://raw.githubusercontent.com/hfiref0x/WinDepends.Docs/master/help/img/MainWindowConsent.png" width="1010" />

### Utility Features

* Scans any 32-bit or 64-bit Windows module (exe, dll, ocx, sys, etc.) and builds a hierarchical tree diagram of all dependent modules. For each module found, it lists all the functions that are exported by that module, and which of those functions are actually being called by other modules. Another view displays the minimum set of required files, along with detailed information about each file including a full path to the file, base address, version numbers, machine type, debug information, and more.
*  Supports delay-load dlls, ApiSet contracts, bound import, and Side-by-Side modules.
   * Supported ApiSet schema versions are: V2 (Win7), V4 (Win8/8.1), V6 (Win10 and above) 
*  Supports drag and drop with included most recently used files list.
*  Supports custom configuration, external viewer, external help command, module path resolution, search order and PE loader relocations settings.
*  Supports Microsoft Debug Symbols to provide more information on modules exports/imports.
*  Supports C++ function name undecorating to provide human readable C++ function prototypes including function names, return types, and parameter types.
*  Has an ability to save current session into a file and restore it back in program.
*  Client-server architecture, client is a WinForms .NET application providing a graphical user interface while server is a windowless C application responsible for parsing PE files.

### Missing features / Known issues

* Program current state is BETA. Several features of original Dependency Walker are not yet available or were considered obsolete and not implemented (e.g. profiling).
* MDI GUI mess has been discontinued, if you want to analyze several files at the same time you can launch another copy of program.
* Help file is currently missing, we plan to move all documentation on a separate public repository and publish it as a PDF file.
* Several functionally may not work as expected or be disabled due to beta state.
* CLI version is not yet implemented, we are still not sure is it needed or not.
* ARM binaries are not tested in native environment as we don't have it in a bare metal.
* Some limitations comes from Windows OS support.
* If you found a bug, have a suggestion or want to do pull request - feel free to do so, we appreciate your input.

# Installation and Usage

WinDepends repository contain directory named "bin":
+ WinDepends.exe - main GUI application (client)
+ WinDepends.Core.exe - windowless application (server) which is launched by client
+ PDB files for both client/server

There are no specific installation requirements, just copy this folder somewhere on your computer (rename it if you wish) and run WinDepends.exe from it. Uninstallation is also simple - terminate client, server process and delete files.

# Documentation and Help

* Under development, will be located here https://github.com/hfiref0x/WinDepends.Docs

# Building and Other Information

+ Build platform is a Microsoft Visual Studio 2022 with latest SDK installed.
+ Application (client) is written in C#, using Windows Forms and .NET 8.0.
+ Server is written in C, with no special headers or SDK used.
+ Source code also include server test application (WinDepends.Core.Tests) and simple fuzzer (WinDepends.Core.Fuzz).
+ Modern toolbar images are from https://icons8.com/ collection.

We are not chasing the latest versions of frameworks, SDKs or language standards, so for example, if the .NET platform is updated, it will happen in the next LTS release (.NET 10).

# License

MIT

# Authors

(c) 2024 -2025 WinDepends Project
