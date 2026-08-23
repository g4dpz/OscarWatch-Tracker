# OscarWatch Performance Optimization Branches

## Overview
This document tracks the performance optimization branches created to improve OscarWatch's efficiency during active satellite tracking sessions.

## Branch Status

### 🔥 **Priority 1: Hot Path Memory Allocations**

#### `opt/tracking-orchestrator-remove-satellite`
- **Target**: `TrackingOrchestrator.RemoveSatellite` LINQ elimination
- **Impact**: 10-20% reduction in tracking loop allocation
- **Files**: `OscarWatch.Core/Services/TrackingOrchestrator.cs`
- **Status**: ⏳ Ready for implementation
- **Description**: Replace `_cachedEnabledSats.Where().ToList()` with in-place removal to eliminate O(n) allocation on each satellite removal

#### `opt/live-tracking-snapshot-pooling`  
- **Target**: `LiveTrackingService.PublishSnapshot` array allocation
- **Impact**: 5-10% reduction in tracking service allocation
- **Files**: `OscarWatch.Core/Services/LiveTrackingService.cs`
- **Status**: ⏳ Ready for implementation  
- **Description**: Replace `states.ToArray()` with object pooling to reuse arrays across 250ms refresh cycles

### ⭐ **Priority 2: Mathematical Calculation Optimizations**

#### `opt/sun-position-caching`
- **Target**: `SunPositionCalculator.GetPosition` caching
- **Impact**: 15-25% CPU reduction in mathematical calculations
- **Files**: `OscarWatch.Core/Orbit/SunPositionCalculator.cs`
- **Status**: ⏳ Ready for implementation
- **Description**: Cache sun position calculations for 1-minute intervals since sun moves slowly relative to tracking frequency

#### `opt/trigonometric-precomputation`
- **Target**: Repeated Math.PI, trigonometric calculations
- **Impact**: 5-15% mathematical computation improvement
- **Files**: `OscarWatch.Core/Orbit/*Calculator.cs`, mathematical utilities
- **Status**: ⏳ Ready for implementation
- **Description**: Pre-compute common constants and cache repeated trigonometric operations

### 🎯 **Priority 3: UI Rendering Optimizations**

#### `opt/timeline-smart-invalidation`
- **Target**: `PassElevationTimelineControl` unnecessary redraws
- **Impact**: 30-50% reduction in unnecessary rendering
- **Files**: `OscarWatch/Controls/PassElevationTimelineControl.cs`
- **Status**: ⏳ Ready for implementation
- **Description**: Replace blanket `InvalidateVisual()` every second with data-driven dirty flagging

#### `opt/stringbuilder-pooling`
- **Target**: StringBuilder allocation across communication layers
- **Impact**: 5-15% allocation reduction in string operations
- **Files**: Various transport and communication classes
- **Status**: ⏳ Ready for implementation
- **Description**: Pool and reuse StringBuilder instances in hot communication paths

### 🔄 **Priority 4: Background Computation Optimizations**

#### `opt/ground-track-scheduling`
- **Target**: Enhanced ground track computation scheduling
- **Impact**: 10-20% improvement in background computation efficiency
- **Files**: `OscarWatch.Core/Services/TrackingOrchestrator.cs`
- **Status**: ⏳ Ready for implementation
- **Description**: Improve staggered recomputation with intelligent priority scheduling based on visibility

#### `opt/elevation-profile-batching`
- **Target**: Elevation profile computation optimization
- **Impact**: 5-15% improvement in pass calculation efficiency
- **Files**: `OscarWatch/Controls/PassElevationTimelineControl.cs`
- **Status**: ⏳ Ready for implementation
- **Description**: Batch multiple profile calculations and optimize background computation patterns

### 📡 **Priority 5: String Operations & Communications**

#### `opt/radio-command-templates`
- **Target**: Radio command formatting optimization
- **Impact**: 5-10% improvement in CAT command performance
- **Files**: `OscarWatch.Core/Radio/FlexSmartSdrCodec.cs`, radio drivers
- **Status**: ⏳ Ready for implementation
- **Description**: Pre-format common commands and use string templates instead of StringBuilder construction

#### `opt/status-parsing-spans`
- **Target**: String parsing operations in status processing
- **Impact**: 10-15% improvement in serial communication parsing
- **Files**: Radio transport classes, status parsing
- **Status**: ⏳ Ready for implementation
- **Description**: Use ReadOnlySpan<char> and pre-compiled patterns for repeated string operations

## Implementation Guidelines

### Branch Naming Convention
- `opt/{component}-{optimization-type}`
- Examples: `opt/sun-position-caching`, `opt/timeline-smart-invalidation`

### Development Workflow
1. **Implement**: Focus on one optimization per branch
2. **Test**: Include performance benchmarks and unit tests
3. **Measure**: Document before/after performance metrics
4. **Review**: Create focused PR for each optimization
5. **Merge**: Independent merging allows incremental improvements

### Testing Requirements
- **Unit tests**: Verify functional equivalence
- **Performance tests**: Measure allocation reduction and timing improvements
- **Integration tests**: Ensure no regression in tracking accuracy
- **Stress tests**: Validate under high satellite count scenarios

### Success Metrics
- **Memory allocation reduction**: Target 20-30% overall reduction in hot paths
- **CPU usage improvement**: Target 15-25% reduction in mathematical calculations
- **UI responsiveness**: Target 30-50% reduction in unnecessary rendering
- **Throughput improvement**: Maintain tracking accuracy while improving performance

## Combined Impact Estimate
- **Total allocation reduction**: 25-40%
- **CPU usage improvement**: 20-35% 
- **UI rendering efficiency**: 40-60%
- **Overall tracking performance**: 30-50% improvement during active sessions

## Dependencies
- Some optimizations can be developed in parallel
- Sun position caching should be implemented early (used by many components)
- StringBuilder pooling affects multiple communication layers
- Timeline optimizations are independent and can be done separately

## Notes
- All optimizations maintain identical functional behavior
- Focus on hot paths during active satellite tracking (250ms cycles)
- Each branch targets specific, measurable performance improvements
- Optimizations complement existing work (LINQ hotpath, community status, flex parsing)