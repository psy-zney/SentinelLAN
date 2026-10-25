param(
    [Parameter(Mandatory)][string]$Document,
    [Parameter(Mandatory)][string]$Python,
    [string]$Pdf
)
$ErrorActionPreference = 'Stop'
$reportPath = (Resolve-Path -LiteralPath $Document).Path
if (-not $Pdf) { $Pdf = [IO.Path]::ChangeExtension($reportPath, '.pdf') }
$pdfPath = [IO.Path]::GetFullPath($Pdf)
$wordApp = $null
$report = $null
try {
    $wordApp = New-Object -ComObject Word.Application
    $wordApp.Visible = $false
    $wordApp.DisplayAlerts = 0
    $wordApp.AutomationSecurity = 3
    $report = $wordApp.Documents.Open($reportPath, $false, $false)
    $report.Fields.Update() | Out-Null
    foreach ($toc in $report.TablesOfContents) { $toc.Update() }
    foreach ($list in $report.TablesOfFigures) { $list.Update() }
    # Word-generated field results need all script fonts, including hidden tabs.
    $all = $report.Content
    $all.Font.Name = 'Times New Roman'
    $all.Font.Color = 0
    foreach ($id in @(-20, -21, -22, -36)) {
        $style = $report.Styles.Item($id)
        $style.Font.Name = 'Times New Roman'
        $style.Font.Bold = [int]($id -eq -20)
        $style.Font.Size = $(if ($id -eq -20) { 13 } else { 12 })
        $style.ParagraphFormat.FirstLineIndent = 0
        $indentPoints = [single]0
        if ($id -eq -21) { $indentPoints = [single](0.5 * 72 / 2.54) }
        if ($id -eq -22) { $indentPoints = [single](72 / 2.54) }
        $style.ParagraphFormat.LeftIndent = $indentPoints
        $style.ParagraphFormat.SpaceBefore = $(if ($id -eq -20) { 4 } else { 0 })
        $style.ParagraphFormat.SpaceAfter = 2
        $style.ParagraphFormat.LineSpacingRule = 0
    }
    # Direct formatting survives a cached TOC that inherited the original 1 cm indent.
    foreach ($toc in $report.TablesOfContents) {
        foreach ($paragraph in $toc.Range.Paragraphs) {
            $line = $paragraph.Range.Text
            $chapterLine = $line -match '^(CHƯƠNG [1-7]|TÀI LIỆU THAM KHẢO|PHỤ LỤC [A-E])'
            $paragraph.FirstLineIndent = 0
            $paragraph.LeftIndent = [single]$(if ($chapterLine) { 0 } else { 0.5 * 72 / 2.54 })
            $paragraph.Range.Font.Size = $(if ($chapterLine) { 13 } else { 12 })
            $paragraph.Range.Font.Bold = [int]$chapterLine
            $paragraph.SpaceBefore = $(if ($chapterLine) { 4 } else { 0 })
            $paragraph.SpaceAfter = 2
        }
    }
    foreach ($list in $report.TablesOfFigures) {
        $list.Range.ParagraphFormat.FirstLineIndent = 0
        $list.Range.ParagraphFormat.LeftIndent = 0
        $list.Range.Font.Size = 12
        $list.Range.Font.Bold = 0
        $list.Range.ParagraphFormat.LineSpacingRule = 0
    }
    $report.Repaginate()
    foreach ($toc in $report.TablesOfContents) { $toc.UpdatePageNumbers() }
    foreach ($list in $report.TablesOfFigures) { $list.UpdatePageNumbers() }
    $report.Save()
    $report.Close(0)
    $report = $null
    # Some Office installations reject NameFarEast (0x800A16D4). Set the four
    # OOXML font slots directly after refreshing generated fields instead.
    & $Python (Join-Path $PSScriptRoot 'normalize_fonts.py') $reportPath
    if ($LASTEXITCODE -ne 0) { throw 'OOXML font normalization failed.' }
    $report = $wordApp.Documents.Open($reportPath, $false, $false)
    $report.Repaginate()
    foreach ($toc in $report.TablesOfContents) { $toc.UpdatePageNumbers() }
    foreach ($list in $report.TablesOfFigures) { $list.UpdatePageNumbers() }
    $report.Save()
    $report.ExportAsFixedFormat($pdfPath, 17)
    [pscustomobject]@{ Pages = $report.ComputeStatistics(2); Pdf = $pdfPath; TablesOfContents = $report.TablesOfContents.Count; FigureLists = $report.TablesOfFigures.Count } | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $report) { $report.Close(0) }
    if ($null -ne $wordApp) { $wordApp.Quit() }
}
