## [unreleased]

* target both .NET Standard 2.1 and .NET 10 in one NuGet package, sharing the native runtime assets

## 0.1.0 [2026-11-14]

* add a CMake-built Unix serial helper with SafeHandle ownership
* add Zig discovery and Linux glibc/musl cross-compilation presets
* package native assets by runtime identifier and add pseudo-terminal integration tests
* support Linux custom baud rates and operation-wide native I/O timeout deadlines
* add a Native AOT compatibility test project that analyzes the entire library and treats AOT and trimming warnings as errors
* publish and run Native AOT smoke tests in CI on Windows and Linux (glibc and musl)
* add hardware-free Windows smoke tests for port enumeration, configuration, failed opens, closed-port guards, and disposal
* rename `SerialError` members to `RxOver`, `RxParity`, and `TxFull`, and `SerialPinChange.CDChanged` to `CdChanged` (source-breaking changes)
* preserve upstream copyright notices, include Novell in the packaged license, and add current copyright and MIT SPDX headers
* fix typos in comments and variable names in WinSerialStream.cs
* add .idea directory to .gitignore
* add tags
* update build configuration to use Release mode in GitHub Actions workflow
* remove redundant null checks for buffer in SerialPort and SerialPortStream classes
* configure package
* format ISerialStream.cs
* add CHANGELOG.md

## 0.0.1 [2024-10-28]
* fork from https://github.com/michaldobrodenka/Mono.IO.Ports-managed
