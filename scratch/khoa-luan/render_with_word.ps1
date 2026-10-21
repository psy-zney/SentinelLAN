param([Parameter(Mandatory=$true)][string]$DocumentPath,[Parameter(Mandatory=$true)][string]$PdfPath)
$ErrorActionPreference = 'Stop'
$thesisWord = $null
$thesisDoc = $null
try {
    $thesisWord = New-Object -ComObject Word.Application
    $thesisWord.Visible = $false
    $thesisWord.DisplayAlerts = 0
    $thesisDoc = $thesisWord.Documents.Open($DocumentPath, $false, $false)
    for ($thesisPass = 0; $thesisPass -lt 3; $thesisPass++) {
        $thesisDoc.Fields.Update() | Out-Null
        foreach ($thesisToc in $thesisDoc.TablesOfContents) {
            $thesisToc.Range.Font.Name = 'Times New Roman'
            $thesisToc.Range.Font.Size = 11.5
            $thesisToc.Range.Font.Color = 0
            $thesisToc.Range.ParagraphFormat.SpaceAfter = 2
            $thesisToc.Range.ParagraphFormat.SpaceBefore = 0
            $thesisToc.Range.ParagraphFormat.LineSpacingRule = 0
        }
        $thesisDoc.Repaginate()
    }
    $thesisPages = $thesisDoc.ComputeStatistics(2)
    $thesisDoc.Save()
    $thesisDoc.ExportAsFixedFormat($PdfPath, 17)
    Write-Output "Word PDF exported. Pages: $thesisPages"
}
finally {
    if ($null -ne $thesisDoc) { $thesisDoc.Close(0) }
    if ($null -ne $thesisWord -and $thesisWord.Documents.Count -eq 0) { $thesisWord.Quit() }
    if ($null -ne $thesisDoc) { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($thesisDoc) }
    if ($null -ne $thesisWord) { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($thesisWord) }
}
