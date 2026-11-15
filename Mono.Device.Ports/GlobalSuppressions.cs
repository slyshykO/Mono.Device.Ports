// Copyright (c) 2026 Oleksiy Slyshyk
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Reliability",
    "CA2024",
    Justification = "PolyShim 2.16.0 uses EndOfStream in its ReadLinesAsync polyfills; this library does not call them.",
    Scope = "type",
    Target = "~T:MemberPolyfills_Net70_File")]

[assembly: SuppressMessage(
    "Reliability",
    "CA2022",
    Justification = "PolyShim 2.16.0 intentionally drains and discards bytes until EOF while retaining the zlib trailer internally.",
    Scope = "member",
    Target = "~M:System.IO.Compression.ZLibStream.TrailerHoldbackStream.ReadTrailer~System.Byte[]")]
