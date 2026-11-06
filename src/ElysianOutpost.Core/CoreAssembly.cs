// SPDX-License-Identifier: MPL-2.0

using System.Reflection;

namespace ElysianOutpost.Core;

/// <summary>
/// Gives the assembly of the core library, for tests and tools.
/// </summary>
public static class CoreAssembly
{
    /// <summary>
    /// Gets the assembly of the core library.
    /// </summary>
    public static Assembly Reference => typeof(CoreAssembly).Assembly;
}
