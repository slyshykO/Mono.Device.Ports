# Mono.Device.Ports

A serial-port library targeting .NET Standard 2.1. Windows uses the existing
Win32 backend. Unix operations use a small C helper, `MonoDevicePortsNative`,
built with CMake. The public API remains in the `Mono.IO.Ports` namespace.

The helper owns platform headers, `termios`/`ioctl` details, baud-rate mapping,
signals, and synchronous I/O. Managed code owns the descriptor through a
`SafeHandle`. Linux baud rates use kernel `termios2` definitions when available,
including custom rates supported by the driver. This also avoids differences
between newer Zig glibc headers and older target libc baud constants. Unsupported
rates fail instead of silently selecting 38400.
Read/write timeouts cover the whole operation, including interrupted calls and
partial writes. A timed-out write may already have transmitted some bytes.

## Build on Linux

Requires CMake 3.21+, a C compiler, Ninja (or another CMake generator), and a .NET
SDK. The smoke tests target .NET 10. Kernel development headers enable Linux
custom baud rates (`linux-headers` on Alpine).

```sh
cmake -S . -B artifacts/build/local -G Ninja -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTING=ON
cmake --build artifacts/build/local
ctest --test-dir artifacts/build/local --output-on-failure
dotnet build Mono.Device.Ports.sln -c Release
LD_LIBRARY_PATH="$PWD/artifacts/build/local/lib" dotnet run --project tests/SerialSmoke -c Release
```

On this development machine, run Linux commands in `wsl -d WLinux`.
Use separate CMake build directories for Windows and WSL. When building managed
code from both environments, use `--artifacts-path artifacts/managed-wlinux` on
the WSL .NET commands to avoid sharing intermediate files.

## Cross-compile with Zig

CMake presets use `zig cc` and Ninja. Zig provides the Linux libc and headers;
a separate target sysroot is not required. Windows and Linux hosts can build:

| Preset / NuGet RID | Zig target |
| --- | --- |
| `linux-x64` | `x86_64-linux-gnu.2.28` |
| `linux-arm64` | `aarch64-linux-gnu.2.28` |
| `linux-arm` | `arm-linux-gnueabihf.2.28` |
| `linux-musl-x64` | `x86_64-linux-musl` |
| `linux-musl-arm64` | `aarch64-linux-musl` |
| `linux-musl-arm` | `arm-linux-musleabihf` |

The glibc presets use a 2.28 baseline. The ARM presets use the hard-float ABI.
Zig 0.17.1 is used in CI. Cross-compilation does not execute target binaries;
the native and managed integration tests run separately on Linux/glibc and Alpine/musl.

```sh
cmake --preset linux-musl-arm64
cmake --build artifacts/build/linux-musl-arm64
cmake --install artifacts/build/linux-musl-arm64
```

The installed library is staged at
`artifacts/native/runtimes/linux-musl-arm64/native/libMonoDevicePortsNative.so`.
Use a fresh build directory when changing the Zig compiler or target.
For a custom build, pass `-DCMAKE_TOOLCHAIN_FILE=cmake/zig-toolchain.cmake`,
`-DZIG_TARGET=...`, and `-DMDP_RUNTIME_ID=...` to CMake.

### Zig discovery

Discovery follows `orange-iot-thermometer/soft/xtask`:

1. `ZIG_BIN` (environment variable or CMake `-DZIG_BIN=...`) explicitly selects
   an executable or distribution directory. Invalid overrides are errors.
2. Otherwise select the newest working semantic version across `~/bin/zig*`,
   Windows `D:/bin/zig*` and `D:/bin/_compilers/zig*`, and `PATH`.

On Windows, `~` means `USERPROFILE`; elsewhere it means `HOME`. Nested extracted
directories are supported to depth two. Versions come from `zig version`, not
folder names. Development versions are compared numerically, a final release
wins over its prereleases, and build metadata is ignored. Equal versions retain
search order. The selected executable is cached in the CMake build directory.

```powershell
# Optional override; automatic discovery supports the compiler directories above.
$env:ZIG_BIN = 'D:/bin/_compilers/zig-x86_64-windows-0.17.1'
cmake --preset linux-x64
cmake --build artifacts/build/linux-x64
cmake --install artifacts/build/linux-x64
```

## Package

Build and install each preset before packing. A normal `dotnet build` does not
build native code or create a package. `dotnet pack` checks that the six default
Linux assets exist, then includes the staged `runtimes/` tree. Package consumers
do not need CMake or Zig; .NET selects the native asset for their runtime.

```powershell
foreach ($rid in 'linux-x64','linux-arm64','linux-arm','linux-musl-x64','linux-musl-arm64','linux-musl-arm') {
    cmake --preset $rid
    if ($LASTEXITCODE) { throw "Configure failed: $rid" }
    cmake --build "artifacts/build/$rid"
    if ($LASTEXITCODE) { throw "Build failed: $rid" }
    cmake --install "artifacts/build/$rid"
    if ($LASTEXITCODE) { throw "Install failed: $rid" }
}
dotnet pack Mono.Device.Ports/Mono.IO.Ports.csproj -c Release -o artifacts/packages
```

For a deliberately limited local package, set `-p:NativeRuntimeIdentifiers=linux-x64`
and stage that RID. `NativeArtifactsPath` overrides the staging root. These
properties control validation and location; every file in the staging root's
`runtimes/` tree is included, so use a separate staging root for a limited package.

To test native asset selection from a locally packed NuGet package on Linux:

```sh
dotnet build tests/SerialSmoke -c Release --artifacts-path artifacts/package-test \
  -p:UsePackedLibrary=true -p:RestoreSources="$PWD/artifacts/packages" \
  -p:RestorePackagesPath="$PWD/artifacts/package-cache"
dotnet artifacts/package-test/bin/SerialSmoke/release/SerialSmoke.dll
```

The smoke tests use pseudo-terminals and require no serial hardware. They cover
binary transfers, baud rates, timeouts, disconnects, disposal, and failed-open
cleanup. Hardware flow control, electrical signals, and actual UART timing still
need device tests. macOS can build the native sources locally, but the supplied
presets and CI package assets currently cover Linux only. The existing managed
text decoding and event API limitations are unchanged.
