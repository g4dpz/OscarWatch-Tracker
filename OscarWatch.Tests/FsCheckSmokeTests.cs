// Feature: performance-optimisations — FsCheck infrastructure smoke tests

using FsCheck;
using FsCheck.Xunit;

namespace OscarWatch.Tests;

public class FsCheckSmokeTests
{
    [Property]
    public bool IntegerAddZeroIsIdentity(int x) => x + 0 == x;

    [Property]
    public bool StringLengthNonNegative(string s) => s.Length >= 0;
}
