#!/bin/bash

# Documentation generation script
# This script converts type links and then builds the documentation with DocFX

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to show usage
usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -s, --serve [PORT]    Serve documentation after building (default port: 8080)"
    echo "  -v, --verbose         Verbose output"
    echo "  -m, --metadata-only   Generate only metadata (API reference)"
    echo "  -h, --help            Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                    Generate documentation"
    echo "  $0 --serve            Generate and serve documentation"
    echo "  $0 --serve 8081       Generate and serve on port 8081"
    echo "  $0 --verbose          Generate with verbose output"
}

# Default values
SERVE=false
PORT=8080
VERBOSE=false
METADATA_ONLY=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -s|--serve)
            SERVE=true
            # Check if next argument is a port number
            if [[ $2 =~ ^[0-9]+$ ]]; then
                PORT=$2
                shift
            fi
            shift
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -m|--metadata-only)
            METADATA_ONLY=true
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            usage
            exit 1
            ;;
    esac
done

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

print_status "Starting documentation generation..."
print_status "Working directory: $SCRIPT_DIR"

# Check if required files exist
if [[ ! -f "docfx.json" ]]; then
    print_error "docfx.json not found in current directory"
    exit 1
fi

if [[ ! -f "convert-type-links.sh" ]]; then
    print_error "convert-type-links.sh not found in current directory"
    exit 1
fi

# --- Always clean output directories before building ---
print_status "Cleaning output directories..."
[[ -d "_site" ]] && rm -rf _site && print_success "Removed _site directory"
if [[ -d "api" ]]; then
    # Only remove .yml and .manifest files, keep index.md
    find api -type f \( -name '*.yml' -o -name '.manifest' \) -delete
    print_success "Removed .yml and .manifest files from api directory (preserved index.md)"
fi

# --- Step 1: Convert type links ---
print_status "Step 1: Converting type mentions to API documentation links..."
[[ "$VERBOSE" == true ]] && bash ./convert-type-links.sh --verbose || bash ./convert-type-links.sh
if [[ $? -eq 0 ]]; then
    print_success "Type link conversion completed"
else
    print_error "Type link conversion failed"
    exit 1
fi

echo ""

# --- Step 2: Generate documentation with DocFX ---
print_status "Step 2: Generating documentation with DocFX..."
if ! command -v docfx &> /dev/null; then
    print_error "DocFX is not installed or not in PATH"
    print_error "Please install DocFX: https://dotnet.github.io/docfx/"
    exit 1
fi

# --- Build DocFX command ---
build_docfx_cmd() {
    local cmd="docfx docfx.json"
    if [[ "$METADATA_ONLY" == true ]]; then
        cmd="docfx metadata docfx.json"
    elif [[ "$SERVE" == true ]]; then
        cmd="docfx docfx.json --serve --port $PORT"
    fi
    [[ "$VERBOSE" == true ]] && cmd="$cmd --logLevel Verbose"
    echo "$cmd"
}
DOCFX_CMD=$(build_docfx_cmd)

print_status "Running: $DOCFX_CMD"
echo ""

if [[ "$SERVE" == true && "$METADATA_ONLY" == false ]]; then
    eval "$DOCFX_CMD" &
    DOCFX_PID=$!
    sleep 3
    if kill -0 $DOCFX_PID 2>/dev/null; then
        print_success "Documentation server started successfully!"
        print_success "Open your browser to: http://localhost:$PORT"
        echo ""
        print_status "Press Ctrl+C to stop the server"
        wait $DOCFX_PID
    else
        print_error "Failed to start documentation server"
        exit 1
    fi
else
    if eval "$DOCFX_CMD"; then
        echo ""
        if [[ "$METADATA_ONLY" == true ]]; then
            print_success "API metadata generation completed!"
            print_success "Metadata files generated in: api/"
        else
            print_success "Documentation generation completed!"
            print_success "Documentation generated in: _site/"
            print_status "To serve locally, run: docfx docfx.json --serve"
        fi
    else
        print_error "Documentation generation failed"
        exit 1
    fi
fi

print_success "All operations completed successfully!"
