#!/bin/bash
# SQL Injection Vulnerability Scanner
# Scans C# files for unsafe SQL queries

echo "🔍 SQL Injection Vulnerability Scanner"
echo "========================================"
echo ""

# Colors
RED='\033[0;31m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Counters
total_files=0
vulnerable_files=0
total_issues=0

# Output file
output_file="SQL_INJECTION_REPORT.txt"
> "$output_file"

echo "🔍 SQL Injection Vulnerability Report" > "$output_file"
echo "Generated: $(date)" >> "$output_file"
echo "========================================" >> "$output_file"
echo "" >> "$output_file"

# Find all .cs files
find "../Take Time BangPhra" -name "*.cs" -type f | while read -r file; do
    # Skip designer files and auto-generated files
    if [[ "$file" == *.designer.cs ]] || [[ "$file" == *.Designer.cs ]]; then
        continue
    fi

    total_files=$((total_files + 1))

    # Search for dangerous patterns
    vulnerable_lines=$(grep -n "DatabaseQuery.*\+" "$file" 2>/dev/null)
    vulnerable_lines+=$(echo "")
    vulnerable_lines+=$(grep -n "DatabaseInsert.*\+" "$file" 2>/dev/null)
    vulnerable_lines+=$(echo "")
    vulnerable_lines+=$(grep -n "DatabaseInsertReturn.*\+" "$file" 2>/dev/null)

    if [ ! -z "$vulnerable_lines" ]; then
        vulnerable_files=$((vulnerable_files + 1))
        count=$(echo "$vulnerable_lines" | grep -c ":")
        total_issues=$((total_issues + count))

        echo -e "${RED}❌ VULNERABLE:${NC} $file"
        echo ""
        echo "FILE: $file" >> "$output_file"
        echo "ISSUES FOUND: $count" >> "$output_file"
        echo "----------------------------------------" >> "$output_file"
        echo "$vulnerable_lines" | head -20 >> "$output_file"
        if [ $(echo "$vulnerable_lines" | wc -l) -gt 20 ]; then
            echo "... and more (showing first 20)" >> "$output_file"
        fi
        echo "" >> "$output_file"
        echo "" >> "$output_file"
    fi
done

echo ""
echo "========================================"
echo -e "${YELLOW}📊 SUMMARY${NC}"
echo "========================================"
echo -e "${RED}Vulnerable Files: $vulnerable_files${NC}"
echo -e "${RED}Total Issues: $total_issues${NC}"
echo ""
echo "📄 Full report saved to: $output_file"
