#!/bin/bash
# Build project

set -e

# Default values
CONFIGURATION="Debug"
CLEAN=false
QUIET=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        --clean)
            CLEAN=true
            shift
            ;;
        -q|--quiet)
            QUIET=true
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

# Clean if requested
if [ "$CLEAN" = true ]; then
    echo -e "\033[36mCleaning...\033[0m"
    dotnet clean --nologo -v q
fi

echo -e "\033[36mBuilding ($CONFIGURATION)...\033[0m"

# Build
if [ "$QUIET" = true ]; then
    dotnet build --nologo -c "$CONFIGURATION" -v q
else
    dotnet build --nologo -c "$CONFIGURATION"
fi

if [ $? -eq 0 ]; then
    echo -e "\n\033[32m✅ Build succeeded!\033[0m"
else
    echo -e "\n\033[31m❌ Build failed!\033[0m"
    exit 1
fi
