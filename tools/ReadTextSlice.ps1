param(
    [Parameter(Mandatory)][string]$Path,
    [string]$Pattern,
    [ValidateRange(1, 2147483647)][int]$StartLine = 1,
    [ValidateRange(1, 200)][int]$Count = 20,
    [ValidateRange(40, 2000)][int]$MaxChars = 240
)
$ErrorActionPreference = 'Stop'
$file = Get-Item -LiteralPath $Path
if ($file.PSIsContainer) { throw 'Path must name a text file.' }
$expression = if ($Pattern) { [regex]::new($Pattern) } else { $null }
$reader = [IO.StreamReader]::new($file.FullName, [Text.Encoding]::UTF8, $true)
try {
    $lineNumber = 0
    $emitted = 0
    while ($null -ne ($line = $reader.ReadLine())) {
        $lineNumber++
        if ($lineNumber -lt $StartLine) { continue }
        $match = if ($expression) { $expression.Match($line) } else { $null }
        if ($expression -and !$match.Success) { continue }
        $offset = if ($match) { [Math]::Max(0, $match.Index - [int]($MaxChars / 3)) } else { 0 }
        $length = [Math]::Min($MaxChars, $line.Length - $offset)
        [pscustomobject]@{
            Path = $file.FullName; Line = $lineNumber; Column = $offset + 1
            Text = $line.Substring($offset, $length)
            Truncated = $offset -gt 0 -or $length -lt $line.Length
        }
        $emitted++
        if ($emitted -ge $Count) { break }
    }
}
finally { $reader.Dispose() }
