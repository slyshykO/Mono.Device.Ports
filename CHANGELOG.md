## [unreleased]

* add a CMake-built Unix serial helper with SafeHandle ownership
* add Zig discovery and Linux glibc/musl cross-compilation presets
* package native assets by runtime identifier and add pseudo-terminal integration tests
* support Linux custom baud rates and operation-wide native I/O timeout deadlines

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
