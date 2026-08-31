# HexaDock

HexaDock is a compact, searchable file dock for Windows and Linux. It keeps files where they already are while providing a faster way to search, filter, favorite, and reopen them.

## Features

- Open from the movable desktop logo or `Ctrl + Alt + Space`.
- Index useful files one folder deep on the personal and shared Desktop while skipping generated project trees.
- Scan folders you explicitly add up to two levels deep, with a safety limit to keep startup responsive.
- Open the Windows Recycle Bin directly from the main toolbar.
- Search nested files with fuzzy matching and filter by their actual file category.
- Sort, favorite, and revisit recent items.
- Use high-resolution native Windows file icons and photo previews.
- Save a separate dock position for each monitor.
- Keep the dock inside the visible monitor area, including after display changes.
- Optionally start with Windows and hide the standard Desktop icons.
- Protect the interface with an offline PIN.
- Import encrypted copies into a local AES-GCM vault without changing the originals.

## Privacy

HexaDock has no account, analytics, advertising, cookies, or cloud service. It indexes file names and paths locally on the device. Settings, recent-item history, favorites, PIN hashes, and encrypted vault data remain under the current user's local application-data folder.

HexaDock does not upload or transmit user files. Vault encryption keys are protected with Windows Data Protection API for the current Windows account.

## Download

Download the Windows installer or portable ZIP from the [latest release](https://github.com/Hexxis-cmd/HexaDock/releases/latest).

Windows 10 or Windows 11 on an x64 processor is recommended. The Linux AppImage is tested on Ubuntu 22.04 x64 with X11/XWayland. Both packaged releases are self-contained and do not require a separate .NET installation.

On Linux, mark the AppImage executable and launch it:

```sh
chmod +x HexaDock-1.0.0-linux-x86_64.AppImage
./HexaDock-1.0.0-linux-x86_64.AppImage
```

The global `Ctrl + Alt + Space` shortcut uses X11/XWayland. Native Wayland compositors may restrict global shortcuts; the dock icon remains available in that case.

## Build from source

Requirements:

- Windows
- .NET 9 SDK

```powershell
dotnet build HexaDock.csproj
dotnet run --project HexaDock.csproj
```

The optional installer definition is in `Packaging/HexaDock.iss` and uses Inno Setup.

## Local data

HexaDock stores its settings and vault under:

```text
%LOCALAPPDATA%\HexaDock
```

Removing that folder deletes HexaDock's settings and encrypted vault data. Export anything needed from the vault before deleting it.

## Release

Current release: **HexaDock 1.0.0**

## License and credit

HexaDock is open-source software released under the [MIT License](LICENSE). Forks and redistributed copies must retain the copyright and license notice crediting **Daymien Vanhorn (Hexxis-cmd)**.
