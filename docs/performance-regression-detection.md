# Performance Regression Detection

This document describes the performance regression detection system for LINQ hotpath optimizations in OscarWatch-Tracker.

## Overview

The performance regression detection system ensures that LINQ hotpath optimizations maintain their effectiveness over time. It provides automated testing that fails if allocation reduction targets are not met, preventing performance regressions from being introduced into the codebase.

## Architecture

### Components

1. **Performance Regression Tests** (`OscarWatch.Tests/Performance/PerformanceRegressionTests.cs`)
   - Validates allocation reduction targets are met (20-30% minimum)
   - Fails CI builds if optimizations are compromised
   - Tests all optimized methods: GetPassesAsync, GetMutualPassesAsync, RemoveSatellite

2. **Memory Profiling Tests** (`OscarWatch.Tests/Performance/LiveTrackingMemoryProfiler.cs`)
   - Profiles memory usage during simulated live tracking scenarios
   - Monitors allocation patterns, GC pressure, and peak memory consumption
   - Validates sustained performance under realistic workloads

3. **Allocation Benchmarks** (`OscarWatch.Tests/Performance/LINQHotPathBenchmarkTests.cs`)
   - Compares original LINQ implementations vs optimized versions
   - Provides detailed allocation measurements and execution times
   - Serves as baseline for performance analysis

4. **CI Integration** (`.github/workflows/performance-validation.yml`)
   - Runs performance tests on pull requests and weekly schedules
   - Provides automated performance validation in CI/CD pipeline
   - Generates performance reports and artifacts

## Usage

### Local Development

#### Quick Performance Check
```bash
# Run regression detection tests (fastest)
./scripts/run-performance-tests.sh regression

# Run with verbose output
./scripts/run-performance-tests.sh regression --verbose

# Run allocation benchmarks
./scripts/run-performance-tests.sh benchmarks
```

#### Comprehensive Analysis
```bash
# Run all performance tests
./scripts/run-performance-tests.sh all --verbose --timeout 30

# Run memory profiling (slower)
./scripts/run-performance-tests.sh profiling --timeout 30
```

#### Using dotnet test directly
```bash
# Regression tests only
dotnet test --filter "Category=Performance&Category=RegressionDetection" -c Release

# Memory profiling tests
dotnet test --filter "Category=Performance&Category=MemoryProfiling" -c Release

# All performance tests
dotnet test --filter "Category=Performance" -c Release
```

### CI/CD Pipeline

The performance validation workflow automatically runs:

- **Pull Requests**: Regression detection tests on code changes
- **Manual Dispatch**: Full performance analysis including memory profiling
- **Weekly Schedule**: Continuous monitoring for performance drift

#### Workflow Triggers

```yaml
# Automatic on PR to main branch
on:
  pull_request:
    branches: [main]
    paths:
      - 'OscarWatch.Core/**/*.cs'
      - 'OscarWatch/**/*.cs'

# Manual execution with options
workflow_dispatch:
  inputs:
    enable_memory_profiling:
      description: 'Run memory profiling tests'
      default: 'false'
      type: boolean

# Weekly monitoring
schedule:
  - cron: '0 6 * * 0'  # Sundays at 06:00 UTC
```

## Performance Thresholds

### Allocation Reduction Targets

All optimized methods must achieve **minimum 20% allocation reduction** compared to original LINQ implementations:

| Method | Original Pattern | Optimized Pattern | Target Reduction |
|--------|-----------------|-------------------|------------------|
| `GetPassesAsync` | LINQ chain: `Where().SelectMany().Where().OrderBy().ToList()` | Manual enumeration with thread-local buffer | 20-30% |
| `GetMutualPassesAsync` | Dual LINQ chains with intermediate collections | Dual manual enumeration with separate buffers | 20-30% |
| `RemoveSatellite` | `Where(predicate).ToList()` | Manual enumeration with pre-sized list | 20-30% |

### Memory Usage Limits

Memory profiling tests validate usage stays within these bounds:

| Scenario | Peak Memory Limit | Average Allocation Rate |
|----------|------------------|------------------------|
| Single-site tracking | 50 MB | 1 MB/s |
| Dual-site tracking | 75 MB | 1.5 MB/s |
| Combined workload | 100 MB | 2 MB/s |

## Test Categories and Traits

Tests are organized using xUnit traits for flexible filtering:

```csharp
[Trait("Category", "Performance")]
[Trait("Category", "RegressionDetection")]
public void GetPassesAsync_AllocationReduction_MustMeetOrExceedThreshold()

[Trait("Category", "Performance")]  
[Trait("Category", "MemoryProfiling")]
public async Task ProfileContinuousPassPredictions_LiveTrackingScenario()
```

### Available Filters

- `Category=Performance` - All performance tests
- `Category=RegressionDetection` - Critical regression tests (fast)
- `Category=MemoryProfiling` - Memory usage profiling (slower)
- `FullyQualifiedName~LINQHotPathBenchmarkTests` - Allocation benchmarks

## Interpreting Results

### Successful Test Output

```
✅ GetPassesAsync allocation reduction: 24.3% (threshold: 20.0%)
✅ GetMutualPassesAsync allocation reduction: 27.1% (threshold: 20.0%)  
✅ RemoveSatellite allocation reduction: 31.8% (threshold: 20.0%)
```

### Regression Detection Failure

```
PERFORMANCE REGRESSION DETECTED: GetPassesAsync allocation reduction is 15.2%, 
which is below the required 20.0% threshold. 
Original allocations: 45,672 bytes, Optimized allocations: 38,738 bytes. 
This indicates the LINQ hotpath optimizations have been compromised.
```

### Memory Profiling Report

```
=== Continuous Pass Predictions Memory Profile Report ===
Duration: 120.1s
Peak Memory: 42.3 MB
Average Allocation Rate: 823.4 KB/s
Total Allocations: 96.7 MB
GC Collections: Gen0=145, Gen1=12, Gen2=2
Sample Count: 1201
Allocation Spikes: 3
```

## Troubleshooting Performance Issues

### Common Regression Causes

1. **Reverted Optimizations**: LINQ chains accidentally restored during refactoring
2. **New Allocations**: Additional code added that introduces allocation overhead
3. **Configuration Changes**: Test environment differences affecting measurements
4. **Framework Updates**: .NET runtime changes impacting allocation behavior

### Debugging Steps

1. **Compare Implementations**: Verify optimized code matches expected patterns
   ```csharp
   // ❌ Reverted to LINQ (causes regression)
   return tasks.Where(t => t.IsCompletedSuccessfully).SelectMany(t => t.Result).ToList();
   
   // ✅ Optimized manual enumeration
   var results = HotPathCollections.GetPassInfoBuffer();
   foreach (var task in tasks) { /* manual enumeration */ }
   ```

2. **Check Thread-Local Collections**: Ensure HotPathCollections is being used
   ```csharp
   // Verify this pattern is used in optimized methods
   var results = HotPathCollections.GetPassInfoBuffer(); // Pre-allocated buffer
   // ... populate results ...
   return new List<PassInfo>(results); // Defensive copy
   ```

3. **Profile Specific Methods**: Run targeted benchmarks to isolate issues
   ```bash
   # Test specific optimization
   dotnet test --filter "FullyQualifiedName~GetPassesAsync_benchmark_allocation_reduction"
   ```

4. **Review Allocation Metrics**: Analyze detailed allocation measurements
   ```
   Original: 45,672 bytes, 12.34ms
   Optimized: 38,738 bytes, 10.87ms  
   Reduction: 15.2% (6,934 bytes saved)
   ```

### Performance Analysis Tools

#### Local Profiling
```bash
# Run with detailed output for analysis
./scripts/run-performance-tests.sh benchmarks --verbose --output ./perf-analysis

# Generate performance report
./scripts/run-performance-tests.sh all --output ./detailed-report
```

#### Memory Profiling
```bash
# Profile memory usage patterns
./scripts/run-performance-tests.sh profiling --timeout 30

# Check for allocation spikes and GC pressure
dotnet test --filter "Category=MemoryProfiling" --logger "console;verbosity=detailed"
```

## Extending Performance Tests

### Adding New Optimization Tests

1. **Create Test Method**: Add regression test for new optimization
   ```csharp
   [Fact]
   public void NewOptimizedMethod_AllocationReduction_MustMeetThreshold()
   {
       // Measure original vs optimized implementation
       // Assert minimum 20% reduction
   }
   ```

2. **Add to CI Filter**: Update workflow to include new tests
   ```yaml
   --filter "Category=Performance&Category=RegressionDetection"
   ```

3. **Update Documentation**: Document new thresholds and patterns

### Custom Performance Scenarios

1. **Scenario-Specific Tests**: Create tests for specific usage patterns
   ```csharp
   [Trait("Category", "Performance")]
   [Trait("Scenario", "HighFrequency")]
   public void CustomScenario_PerformanceValidation() { }
   ```

2. **Flexible Thresholds**: Adjust thresholds for different scenarios
   ```csharp
   const double customThreshold = 15.0; // Lower threshold for complex scenarios
   ```

## Monitoring and Maintenance

### Weekly Performance Reports

The CI system generates weekly reports showing:
- Performance trend analysis
- Allocation reduction effectiveness
- Memory usage patterns over time
- Early warning of performance drift

### Performance Baseline Updates

When making intentional performance changes:

1. **Document Changes**: Update thresholds and expected behavior
2. **Validate Improvements**: Ensure changes improve rather than regress performance  
3. **Update Tests**: Adjust test expectations if baseline changes significantly

### Long-term Performance Health

- Monitor weekly CI reports for gradual performance degradation
- Review performance metrics during major framework updates
- Validate optimizations remain effective as codebase evolves
- Consider additional optimizations based on profiling results

## Related Documentation

- [LINQ HotPath Optimization Design](../specs/linq-hotpath-optimization/design.md)
- [Performance Testing Strategy](../specs/linq-hotpath-optimization/requirements.md)
- [CI/CD Performance Validation Workflow](../.github/workflows/performance-validation.yml)