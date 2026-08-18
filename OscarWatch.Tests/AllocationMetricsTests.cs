using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class AllocationMetricsTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsMetricsWithCorrectValues()
    {
        // Arrange
        const long beforeBytes = 1000L;
        const long afterBytes = 700L;
        var executionTime = TimeSpan.FromMilliseconds(50);
        const int resultCount = 42;

        // Act
        var metrics = AllocationMetrics.Create(beforeBytes, afterBytes, executionTime, resultCount);

        // Assert
        Assert.Equal(beforeBytes, metrics.AllocatedBytesBefore);
        Assert.Equal(afterBytes, metrics.AllocatedBytesAfter);
        Assert.Equal(executionTime, metrics.ExecutionTime);
        Assert.Equal(resultCount, metrics.ResultCount);
    }

    [Fact]
    public void AllocationReduction_WithPositiveReduction_ReturnsCorrectValue()
    {
        // Arrange
        var metrics = AllocationMetrics.Create(1000L, 700L, TimeSpan.FromMilliseconds(50), 10);

        // Act
        var reduction = metrics.AllocationReduction;

        // Assert
        Assert.Equal(300L, reduction);
    }

    [Fact]
    public void AllocationReduction_WithNegativeReduction_ReturnsNegativeValue()
    {
        // Arrange - More allocations after than before (indicates increase)
        var metrics = AllocationMetrics.Create(500L, 800L, TimeSpan.FromMilliseconds(50), 10);

        // Act
        var reduction = metrics.AllocationReduction;

        // Assert
        Assert.Equal(-300L, reduction);
    }

    [Fact]
    public void AllocationReduction_WithNoChange_ReturnsZero()
    {
        // Arrange
        var metrics = AllocationMetrics.Create(500L, 500L, TimeSpan.FromMilliseconds(50), 10);

        // Act
        var reduction = metrics.AllocationReduction;

        // Assert
        Assert.Equal(0L, reduction);
    }

    [Fact]
    public void ReductionPercentage_WithPositiveReduction_ReturnsCorrectPercentage()
    {
        // Arrange
        var metrics = AllocationMetrics.Create(1000L, 700L, TimeSpan.FromMilliseconds(50), 10);

        // Act
        var percentage = metrics.ReductionPercentage;

        // Assert
        Assert.Equal(30.0, percentage);
    }

    [Fact]
    public void ReductionPercentage_WithNegativeReduction_ReturnsNegativePercentage()
    {
        // Arrange
        var metrics = AllocationMetrics.Create(500L, 650L, TimeSpan.FromMilliseconds(50), 10);

        // Act
        var percentage = metrics.ReductionPercentage;

        // Assert
        Assert.Equal(-30.0, percentage);
    }

    [Fact]
    public void ReductionPercentage_WithZeroBaseline_ReturnsZero()
    {
        // Arrange
        var metrics = AllocationMetrics.Create(0L, 100L, TimeSpan.FromMilliseconds(50), 10);

        // Act
        var percentage = metrics.ReductionPercentage;

        // Assert
        Assert.Equal(0.0, percentage);
    }

    [Fact]
    public void ReductionPercentage_WithCompleteReduction_Returns100Percent()
    {
        // Arrange
        var metrics = AllocationMetrics.Create(1000L, 0L, TimeSpan.FromMilliseconds(50), 10);

        // Act
        var percentage = metrics.ReductionPercentage;

        // Assert
        Assert.Equal(100.0, percentage);
    }

    [Fact]
    public void NoAllocations_CreatesMetricsWithZeroAllocations()
    {
        // Arrange
        var executionTime = TimeSpan.FromMilliseconds(25);
        const int resultCount = 15;

        // Act
        var metrics = AllocationMetrics.NoAllocations(executionTime, resultCount);

        // Assert
        Assert.Equal(0L, metrics.AllocatedBytesBefore);
        Assert.Equal(0L, metrics.AllocatedBytesAfter);
        Assert.Equal(executionTime, metrics.ExecutionTime);
        Assert.Equal(resultCount, metrics.ResultCount);
        Assert.Equal(0L, metrics.AllocationReduction);
        Assert.Equal(0.0, metrics.ReductionPercentage);
    }

    [Fact]
    public void ToString_WithNoAllocations_ReturnsFormattedString()
    {
        // Arrange
        var metrics = AllocationMetrics.NoAllocations(TimeSpan.FromMilliseconds(42.5), 7);

        // Act
        var result = metrics.ToString();

        // Assert
        Assert.Contains("No allocations", result);
        Assert.Contains("7 results", result);
        Assert.Contains("42.50ms", result);
    }

    [Fact]
    public void ToString_WithPositiveReduction_ReturnsFormattedString()
    {
        // Arrange
        var metrics = AllocationMetrics.Create(1000L, 700L, TimeSpan.FromMilliseconds(42.5), 15);

        // Act
        var result = metrics.ToString();

        // Assert
        Assert.Contains("+300", result); // Positive reduction
        Assert.Contains("+30.0%", result); // Positive percentage
        Assert.Contains("15 results", result);
        Assert.Contains("42.50ms", result);
    }

    [Fact]
    public void ToString_WithNegativeReduction_ReturnsFormattedString()
    {
        // Arrange
        var metrics = AllocationMetrics.Create(500L, 800L, TimeSpan.FromMilliseconds(67.8), 20);

        // Act
        var result = metrics.ToString();

        // Assert
        Assert.Contains("-300", result); // Negative reduction (increase)
        Assert.Contains("-60.0%", result); // Negative percentage
        Assert.Contains("20 results", result);
        Assert.Contains("67.80ms", result);
    }

    [Fact]
    public void ToString_WithZeroReduction_ReturnsFormattedString()
    {
        // Arrange
        var metrics = AllocationMetrics.Create(500L, 500L, TimeSpan.FromMilliseconds(30.0), 12);

        // Act
        var result = metrics.ToString();

        // Assert
        Assert.Contains("0 bytes", result);
        Assert.Contains("0.0%", result);
        Assert.Contains("12 results", result);
        Assert.Contains("30.00ms", result);
    }

    [Theory]
    [InlineData(1234567L, 234567L, 1000000L)]
    [InlineData(50L, 25L, 25L)]
    [InlineData(0L, 0L, 0L)]
    public void AllocationReduction_WithVariousInputs_CalculatesCorrectly(long before, long after, long expectedReduction)
    {
        // Arrange
        var metrics = AllocationMetrics.Create(before, after, TimeSpan.FromMilliseconds(10), 5);

        // Act & Assert
        Assert.Equal(expectedReduction, metrics.AllocationReduction);
    }

    [Theory]
    [InlineData(1000L, 800L, 20.0)]
    [InlineData(2000L, 500L, 75.0)]
    [InlineData(100L, 150L, -50.0)]
    [InlineData(0L, 100L, 0.0)]
    public void ReductionPercentage_WithVariousInputs_CalculatesCorrectly(long before, long after, double expectedPercentage)
    {
        // Arrange
        var metrics = AllocationMetrics.Create(before, after, TimeSpan.FromMilliseconds(10), 5);

        // Act & Assert
        Assert.Equal(expectedPercentage, metrics.ReductionPercentage, precision: 1);
    }
}