#!/bin/bash

# Script to convert type mentions to API documentation links
# Automatically discovers types and namespaces from the source code

set -e

# Function to discover types and their namespaces from source code
discover_types() {
    local src_dir="$1"
    
    echo "Discovering types from source code in: $src_dir"
    
    # Find all C# files and extract public types with their namespaces
    find "$src_dir" -name "*.cs" -type f | while read -r file; do
        # Get namespace from file
        namespace=$(grep "^namespace " "$file" | head -1 | sed 's/namespace //' | sed 's/[;{].*//')
        
        if [[ -n "$namespace" ]]; then
            # Find public types in this file
            grep -E "^[[:space:]]*public (class|interface|enum|struct) " "$file" | while read -r line; do
                # Extract type name
                type_name=$(echo "$line" | sed -E 's/.*public (class|interface|enum|struct) ([A-Za-z_][A-Za-z0-9_]*).*/\2/')
                
                if [[ -n "$type_name" && "$type_name" != "$line" ]]; then
                    echo "${type_name}:${namespace}"
                fi
            done
        fi
    done | sort -u
}

# Function to build type mappings
build_mappings() {
    local script_dir="$1"
    local src_dir="$script_dir/../src"
    
    # Check if src directory exists, if not try parent directory structure
    if [[ ! -d "$src_dir" ]]; then
        src_dir="$script_dir/.."
    fi
    
    echo "Searching for types in: $src_dir"
    
    # Discover types and store in array
    local mappings=()
    while IFS= read -r line; do
        if [[ -n "$line" ]]; then
            mappings+=("$line")
        fi
    done < <(discover_types "$src_dir")
    
    echo "Found ${#mappings[@]} types"
    
    # Return the mappings array
    printf '%s\n' "${mappings[@]}"
}

convert_file() {
    local file="$1"
    shift
    local mappings=("$@")
    
    echo "Processing: $file"
    
    # Create backup
    cp "$file" "${file}.bak"
    
    # Process each mapping
    for mapping in "${mappings[@]}"; do
        IFS=':' read -r type_name namespace <<< "$mapping"
        
        # Skip empty or invalid mappings
        if [[ -z "$type_name" || -z "$namespace" ]]; then
            continue
        fi
        
        # Base API URL for non-generic types
        base_api_url="../../api/${namespace}.${type_name}.html"
        
        # Only process if the type exists in backticks AND is not already a link
        if grep -qF "\`${type_name}\`" "$file" && ! grep -qF "[\`${type_name}\`](" "$file"; then
            # Simple replacement for non-generic types
            sed -i.tmp "s|\`${type_name}\`|[\`${type_name}\`](${base_api_url})|g" "$file"
        fi
        
        # Handle generic types for specific patterns that commonly use generics
        if [[ "$type_name" =~ ^(ModelHandler|TableModel|TableColumnModel|.*Builder)$ ]]; then
            # Check for generic version that isn't already a link
            if grep -qF "\`${type_name}<" "$file" && ! grep -qF "[\`${type_name}<" "$file"; then
                if command -v perl >/dev/null 2>&1; then
                    # Use perl to handle generic type replacements with DocFX naming convention
                    # DocFX converts generic types like MyType<T, K> to MyType-2.html 
                    # where the number after the dash is the count of type parameters
                    perl -i -pe "
                        s|\`${type_name}<([^>]+)>\`|
                            my \$type_params = \$1;
                            my \$param_count = (\$type_params =~ tr/,/,/) + 1;
                            \"[\\\`${type_name}<\$type_params>\\\`](../../api/${namespace}.${type_name}-\$param_count.html)\"
                        |gex" "$file"
                fi
            fi
        fi
        
        # Clean up sed backup file
        rm -f "${file}.tmp"
    done
    
    # Check if file changed
    if ! cmp -s "$file" "${file}.bak"; then
        echo "  ✓ Updated: $file"
        rm "${file}.bak"
    else
        echo "  - No changes needed: $file"
        mv "${file}.bak" "$file"
    fi
}

main() {
    local script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    local verbose=false
    
    # Check for verbose flag
    if [[ "$1" == "-v" || "$1" == "--verbose" ]]; then
        verbose=true
        shift
    fi
    
    echo "Converting type mentions to API documentation links..."
    echo "Working in: $script_dir"
    echo
    
    # Build dynamic type mappings from source code
    echo "Building type mappings from source code..."
    local mappings=()
    while IFS= read -r line; do
        if [[ -n "$line" ]]; then
            mappings+=("$line")
            if [[ "$verbose" == true ]]; then
                echo "  Found: $line"
            fi
        fi
    done < <(build_mappings "$script_dir")
    
    if [[ ${#mappings[@]} -eq 0 ]]; then
        echo "❌ No types found in source code. Check the source directory path."
        exit 1
    fi
    
    echo "Found ${#mappings[@]} types to process"
    echo
    
    # Find all markdown files, excluding api directory
    find "$script_dir" -name "*.md" -type f ! -path "*/api/*" | while read -r file; do
        convert_file "$file" "${mappings[@]}"
    done
    
    echo
    echo "✅ Conversion complete!"
    echo
    echo "Run with -v or --verbose to see all discovered types."
}

main "$@"
