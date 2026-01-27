#!/bin/bash
# Run all tests

set -e

# Default values
FILTER=""
COVERAGE=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -f|--filter)
            FILTER="$2"
            shift 2
            ;;
        --coverage)
            COVERAGE=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Navigate to project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../../.."

echo -e "\033[36mRunning tests...\033[0m"

# Build args
ARGS="test --nologo"

if [ -n "$FILTER" ]; then
    ARGS="$ARGS --filter FullyQualifiedName~$FILTER"
    echo -e "\033[90m  Filter: $FILTER\033[0m"
fi

if [ "$COVERAGE" = true ]; then
    ARGS="$ARGS --collect:\"XPlat Code Coverage\""
    echo -e "\033[90m  Coverage: enabled\033[0m"
fi

# Run tests
dotnet $ARGS

if [ $? -eq 0 ]; then
    echo -e "\n\033[32m✅ All tests passed!\033[0m"
else
    echo -e "\n\033[31m❌ Some tests failed!\033[0m"
    exit 1
fi
