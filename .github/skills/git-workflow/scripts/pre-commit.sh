#!/bin/bash
# Pre-commit checks

set -e

# Default values
SKIP_FORMAT=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-format)
            SKIP_FORMAT=true
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

echo -e "\033[36mRunning pre-commit checks...\033[0m"
echo ""

# 1. Build
echo -e "\033[33m1. Building project...\033[0m"
if dotnet build --nologo -v q; then
    echo -e "   \033[32m✅ Build succeeded\033[0m"
else
    echo -e "\033[31m❌ Build failed!\033[0m"
    exit 1
fi

# 2. Test
echo -e "\033[33m2. Running tests...\033[0m"
if dotnet test --nologo -v q; then
    echo -e "   \033[32m✅ All tests passed\033[0m"
else
    echo -e "\033[31m❌ Tests failed!\033[0m"
    exit 1
fi

# 3. Format (optional)
if [ "$SKIP_FORMAT" = false ]; then
    echo -e "\033[33m3. Checking format...\033[0m"
    if dotnet csharpier . --check; then
        echo -e "   \033[32m✅ Format OK\033[0m"
    else
        echo -e "   \033[33m⚠️ Formatting issues found, fixing...\033[0m"
        dotnet csharpier .
        echo -e "   \033[32m✅ Format OK\033[0m"
    fi
fi

echo ""
echo -e "\033[32m✅ All pre-commit checks passed!\033[0m"
echo -e "\033[36mReady to commit.\033[0m"
