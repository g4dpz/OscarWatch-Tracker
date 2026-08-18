#!/bin/bash

# Performance test runner for LINQ hotpath optimization validation
# 
# This script provides a convenient way to run performance tests locally and in CI.
# It supports different test categories and provides clear output for analysis.
#
# Usage:
#   ./scripts/run-performance-tests.sh [category] [options]
#
# Categories:
#   regression    - Run regression detection tests (default)
#   benchmarks    - Run allocation benchmark comparisons
#   profiling     - Run memory profiling tests
#   all          - Run all performance tests
#
# Options:
#   --verbose     - Enable detailed test output
#   --timeout N   - Set test timeout in minutes (default: 15)
#   --output DIR  - Set output directory for results (default: ./test-results)

set -euo pipefail

# Default configuration
CATEGORY="${1:-regression}"
VERBOSE=false
TIMEOUT_MINUTES=15
OUTPUT_DIR="./test-results"
BUILD_CONFIG="Release"

# Parse command line options
shift || true
while [[ $# -gt 0 ]]; do
    case $1 in
        --verbose)
            VERBOSE=true
            shift
            ;;
        --timeout)
            TIMEOUT_MINUTES="$2"
            shift 2
            ;;
        --output)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --debug)
            BUILD_CONFIG="Debug"
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Helper functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Determine test filter based on category
get_test_filter() {
    case "$1" in
        regression)
            echo "Category=Performance&Category=RegressionDetection"
            ;;
        benchmarks)
            echo "FullyQualifiedName~LINQHotPathBenchmarkTests"
            ;;
        profiling)
            echo "Category=Performance&Category=MemoryProfiling"
            ;;
        all)
            echo "Category=Performance"
            ;;
        *)
            log_error "Unknown test category: $1"
            log_info "Available categories: regression, benchmarks, profiling, all"
            exit 1
            ;;
    esac
}

# Setup test environment
setup_environment() {
    log_info "Setting up performance test environment..."
    
    # Create output directory
    mkdir -p "$OUTPUT_DIR"
    
    # Set performance-optimized environment variables
    export DOTNET_gcServer=1
    export DOTNET_gcConcurrent=1
    
    if [[ "$CATEGORY" == "profiling" ]]; then
        export DOTNET_EnableEventLog=1
        log_info "Memory profiling environment enabled"
    fi
    
    # Calculate timeout in milliseconds for dotnet test
    TIMEOUT_MS=$((TIMEOUT_MINUTES * 60 * 1000))
    
    log_info "Test category: $CATEGORY"
    log_info "Build configuration: $BUILD_CONFIG"
    log_info "Timeout: ${TIMEOUT_MINUTES} minutes"
    log_info "Output directory: $OUTPUT_DIR"
}

# Build solution
build_solution() {
    log_info "Building solution..."
    
    if ! dotnet restore OscarWatch.slnx; then
        log_error "Failed to restore solution dependencies"
        exit 1
    fi
    
    if ! dotnet build OscarWatch.slnx -c "$BUILD_CONFIG" --no-restore; then
        log_error "Failed to build solution"
        exit 1
    fi
    
    log_success "Solution build completed"
}

# Run performance tests
run_performance_tests() {
    local test_filter
    test_filter=$(get_test_filter "$CATEGORY")
    
    local result_file="$OUTPUT_DIR/performance-${CATEGORY}-results.trx"
    local verbosity="normal"
    
    if [[ "$VERBOSE" == "true" ]]; then
        verbosity="detailed"
    fi
    
    log_info "Running performance tests..."
    log_info "Filter: $test_filter"
    
    # Prepare dotnet test command
    local test_command=(
        dotnet test OscarWatch.Tests/OscarWatch.Tests.csproj
        -c "$BUILD_CONFIG"
        --no-build
        --logger "trx;LogFileName=$(basename "$result_file")"
        --logger "console;verbosity=$verbosity"
        --filter "$test_filter"
        --results-directory "$OUTPUT_DIR"
        --collect:"XPlat Code Coverage"
        --
        "RunConfiguration.TestSessionTimeout=$TIMEOUT_MS"
    )
    
    # Run tests and capture exit code
    local exit_code=0
    if ! "${test_command[@]}"; then
        exit_code=$?
    fi
    
    # Check if results file was created
    if [[ -f "$result_file" ]]; then
        log_success "Test results saved to: $result_file"
    else
        log_warning "Test results file not found: $result_file"
    fi
    
    return $exit_code
}

# Analyze test results
analyze_results() {
    log_info "Analyzing performance test results..."
    
    local result_file="$OUTPUT_DIR/performance-${CATEGORY}-results.trx"
    
    if [[ ! -f "$result_file" ]]; then
        log_warning "No test results file found for analysis"
        return 0
    fi
    
    # Extract basic test statistics from TRX file
    local total_tests
    local passed_tests  
    local failed_tests
    
    if command -v xmllint >/dev/null 2>&1; then
        # Use xmllint if available for better XML parsing
        total_tests=$(xmllint --xpath "count(//UnitTestResult)" "$result_file" 2>/dev/null || echo "N/A")
        passed_tests=$(xmllint --xpath "count(//UnitTestResult[@outcome='Passed'])" "$result_file" 2>/dev/null || echo "N/A")
        failed_tests=$(xmllint --xpath "count(//UnitTestResult[@outcome='Failed'])" "$result_file" 2>/dev/null || echo "N/A")
    else
        # Fallback to basic grep counting
        total_tests=$(grep -c 'UnitTestResult ' "$result_file" || echo "N/A")
        passed_tests=$(grep -c 'outcome="Passed"' "$result_file" || echo "N/A")
        failed_tests=$(grep -c 'outcome="Failed"' "$result_file" || echo "N/A")
    fi
    
    echo ""
    echo "=== Performance Test Results Summary ==="
    echo "Category: $CATEGORY"
    echo "Total tests: $total_tests"
    echo "Passed: $passed_tests"
    echo "Failed: $failed_tests"
    echo ""
    
    # Category-specific analysis
    case "$CATEGORY" in
        regression)
            echo "Regression Detection Analysis:"
            echo "- Tests verify LINQ optimizations maintain 20%+ allocation reduction"
            echo "- Failures indicate performance regression requiring investigation"
            ;;
        benchmarks) 
            echo "Benchmark Comparison Analysis:"
            echo "- Tests compare original vs optimized allocation patterns"
            echo "- Results show exact allocation reduction percentages"
            ;;
        profiling)
            echo "Memory Profiling Analysis:"
            echo "- Tests profile continuous tracking scenario memory usage"
            echo "- Results include peak memory, allocation rates, and GC statistics"
            ;;
    esac
    
    echo ""
    
    # Find coverage files
    local coverage_files
    coverage_files=$(find "$OUTPUT_DIR" -name "coverage.cobertura.xml" 2>/dev/null | head -5)
    
    if [[ -n "$coverage_files" ]]; then
        log_info "Code coverage files found:"
        echo "$coverage_files"
    fi
}

# Generate performance report
generate_report() {
    local report_file="$OUTPUT_DIR/performance-summary.md"
    
    log_info "Generating performance report: $report_file"
    
    cat > "$report_file" << EOF
# Performance Test Report

## Test Configuration
- **Category**: $CATEGORY  
- **Build**: $BUILD_CONFIG
- **Timeout**: ${TIMEOUT_MINUTES} minutes
- **Date**: $(date -u '+%Y-%m-%d %H:%M:%S UTC')

## LINQ HotPath Optimization Validation

This report validates the effectiveness of LINQ hotpath optimizations in the TrackingOrchestrator class.

### Optimization Targets
- **GetPassesAsync**: Replace LINQ chains with manual enumeration (20-30% allocation reduction)
- **GetMutualPassesAsync**: Replace dual LINQ chains with thread-local buffers (20-30% reduction)  
- **RemoveSatellite**: Replace Where().ToList() with manual iteration (20-30% reduction)

### Test Categories

EOF

    case "$CATEGORY" in
        regression)
            cat >> "$report_file" << EOF
#### Regression Detection Tests
- Validate all optimizations maintain minimum 20% allocation reduction threshold
- Tests FAIL if optimizations are compromised or removed
- Provides early warning of performance regressions in CI/CD pipeline

EOF
            ;;
        benchmarks)
            cat >> "$report_file" << EOF
#### Allocation Benchmark Tests  
- Compare original LINQ implementations vs optimized versions
- Measure exact allocation reduction percentages and execution times
- Provide detailed metrics for optimization effectiveness analysis

EOF
            ;;
        profiling)
            cat >> "$report_file" << EOF
#### Memory Profiling Tests
- Profile memory usage during simulated live tracking scenarios
- Monitor allocation patterns, GC pressure, and peak memory consumption
- Validate optimizations maintain effectiveness under sustained load

EOF
            ;;
        all)
            cat >> "$report_file" << EOF
#### Comprehensive Performance Validation
- Complete test suite covering regression detection, benchmarks, and profiling
- Provides full validation of LINQ hotpath optimization effectiveness
- Recommended for thorough performance analysis and validation

EOF
            ;;
    esac
    
    cat >> "$report_file" << EOF
## Results

See \`$(basename "$OUTPUT_DIR")/performance-${CATEGORY}-results.trx\` for detailed test results.

## Performance Thresholds

- **Allocation Reduction**: Minimum 20% reduction vs original LINQ implementations
- **Peak Memory Usage**: 
  - Single tracking: < 50MB
  - Dual-site tracking: < 75MB  
  - Combined workload: < 100MB
- **Allocation Rate**: 
  - Continuous tracking: < 1MB/s
  - Peak workload: < 2MB/s

## Troubleshooting

If performance tests fail:

1. **Check allocation measurements** - Compare before/after allocation values
2. **Verify optimizations intact** - Ensure LINQ replacements haven't been reverted
3. **Review recent changes** - Look for code changes that might introduce allocation overhead
4. **Profile new code** - Apply similar optimization patterns to new hotpath methods

EOF

    log_success "Performance report generated: $report_file"
}

# Main execution
main() {
    echo "LINQ HotPath Optimization Performance Test Runner"
    echo "================================================"
    echo ""
    
    setup_environment
    build_solution
    
    local test_exit_code=0
    if ! run_performance_tests; then
        test_exit_code=$?
        log_error "Performance tests failed with exit code: $test_exit_code"
    else
        log_success "Performance tests completed successfully"
    fi
    
    analyze_results
    generate_report
    
    echo ""
    echo "Performance test execution completed."
    echo "Results available in: $OUTPUT_DIR"
    
    exit $test_exit_code
}

# Run main function
main "$@"