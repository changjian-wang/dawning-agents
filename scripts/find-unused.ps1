$files = Get-ChildItem -Path "src" -Recurse -Filter "*.cs"
foreach ($file in $files) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    $fullText = [System.IO.File]::ReadAllText($file.FullName)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $lineNum = $i + 1
        # Match private field declarations
        if ($line -match '^\s+private\s+(static\s+)?(readonly\s+)?(volatile\s+)?(const\s+)?\S+\s+(\w+)\s*[=;]') {
            $memberName = $Matches[5]
            # Skip methods, classes, nested types
            if ($line -match '\(') { continue }
            if ($line -match '\b(class|record|struct|enum|interface)\b') { continue }
            if ($line -match '\bset\b') { continue }
            # Count word-boundary occurrences
            $pattern = "\b$([regex]::Escape($memberName))\b"
            $count = ([regex]::Matches($fullText, $pattern)).Count
            if ($count -le 2) {
                $rel = $file.FullName.Replace("$PWD\", "")
                Write-Output "${rel}:${lineNum} [${count} refs] $($line.Trim())"
            }
        }
    }
}
