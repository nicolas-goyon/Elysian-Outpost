// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Tests;

public sealed class CoreAssemblyTests
{
    [Fact]
    public void Reference_GivesTheCoreAssembly()
    {
        Assert.Equal("ElysianOutpost.Core", CoreAssembly.Reference.GetName().Name);
    }
}
