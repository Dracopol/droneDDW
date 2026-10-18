# Turns each session CSV in <folder>\CSV into <folder>\Excel\<name>.xlsx: a bold, frozen header row, columns wide
# enough for their contents, ids as numbers and dates / times as real Excel dates, so it opens ready to read on any
# laptop, whatever its language. Builds the .xlsx itself (a zip of XML files), so Excel needn't be installed.
# Called by Pull Sessions.bat. The CSVs are what the app writes (Sessions.cs: ; separated, quoted the RFC 4180 way).
param([Parameter(Mandatory = $true)][string]$Folder)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName Microsoft.VisualBasic
Add-Type -AssemblyName System.IO.Compression
$invariant = [Globalization.CultureInfo]::InvariantCulture
$excelEpoch = [datetime]'1899-12-30'

# Style indexes in styles.xml below
$styleHeader = 1
$styleDate = 2
$styleTime = 3
$styleDateTime = 4

function Read-Csv([string]$path) {
	$parser = New-Object Microsoft.VisualBasic.FileIO.TextFieldParser($path, [Text.Encoding]::UTF8)
	try {
		$parser.SetDelimiters(';')
		$parser.HasFieldsEnclosedInQuotes = $true
		$rows = New-Object System.Collections.Generic.List[string[]]
		while (-not $parser.EndOfData) {
			$rows.Add($parser.ReadFields())
		}
		return , $rows
	}
	finally {
		$parser.Close()
	}
}

function Get-ColumnName([int]$index) {
	$name = ''
	$index++
	while ($index -gt 0) {
		$remainder = ($index - 1) % 26
		$name = [char](65 + $remainder) + $name
		$index = [math]::Floor(($index - 1) / 26)
	}
	return $name
}

# One cell's XML and how many characters wide it shows
function Get-Cell([string]$reference, [string]$text, [bool]$isHeader) {
	$parsed = [datetime]::MinValue
	if ($isHeader) {
		return @("<c r=`"$reference`" s=`"$styleHeader`" t=`"inlineStr`"><is><t>$([Security.SecurityElement]::Escape($text))</t></is></c>", $text.Length)
	}
	if ($text -match '^\d{1,15}$') {
		return @("<c r=`"$reference`"><v>$([long]$text)</v></c>", $text.Length)
	}
	if ([datetime]::TryParseExact($text, 'yyyy-MM-dd HH:mm:ss', $invariant, 'None', [ref]$parsed)) {
		$serial = ($parsed - $excelEpoch).TotalDays.ToString('R', $invariant)
		return @("<c r=`"$reference`" s=`"$styleDateTime`"><v>$serial</v></c>", 19)
	}
	if ([datetime]::TryParseExact($text, 'yyyy-MM-dd', $invariant, 'None', [ref]$parsed)) {
		return @("<c r=`"$reference`" s=`"$styleDate`"><v>$(($parsed - $excelEpoch).TotalDays)</v></c>", 10)
	}
	if ([datetime]::TryParseExact($text, 'HH:mm', $invariant, 'None', [ref]$parsed)) {
		$fraction = $parsed.TimeOfDay.TotalDays.ToString('R', $invariant)
		return @("<c r=`"$reference`" s=`"$styleTime`"><v>$fraction</v></c>", 5)
	}
	return @("<c r=`"$reference`" t=`"inlineStr`"><is><t xml:space=`"preserve`">$([Security.SecurityElement]::Escape($text))</t></is></c>", $text.Length)
}

function Get-SheetXml($rows) {
	$widths = @{}
	$sheetRows = New-Object Text.StringBuilder
	for ($r = 0; $r -lt $rows.Count; $r++) {
		[void]$sheetRows.Append("<row r=`"$($r + 1)`">")
		$fields = $rows[$r]
		for ($c = 0; $c -lt $fields.Length; $c++) {
			$cell = Get-Cell "$(Get-ColumnName $c)$($r + 1)" $fields[$c] ($r -eq 0)
			[void]$sheetRows.Append($cell[0])
			if (-not $widths.ContainsKey($c) -or $widths[$c] -lt $cell[1]) {
				$widths[$c] = $cell[1]
			}
		}
		[void]$sheetRows.Append('</row>')
	}

	$columns = New-Object Text.StringBuilder
	foreach ($c in ($widths.Keys | Sort-Object)) {
		# A little extra for the bold header and the filter-free margin; never narrower than Excel's default
		$width = [math]::Max(9, $widths[$c] + 3)
		[void]$columns.Append("<col min=`"$($c + 1)`" max=`"$($c + 1)`" width=`"$width`" customWidth=`"1`"/>")
	}
	$columnsXml = if ($columns.Length -gt 0) { "<cols>$columns</cols>" } else { '' }

	return '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
		'<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' +
		'<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>' +
		$columnsXml + "<sheetData>$sheetRows</sheetData></worksheet>"
}

function Add-Entry($zip, [string]$name, [string]$content) {
	$writer = New-Object IO.StreamWriter($zip.CreateEntry($name).Open(), (New-Object Text.UTF8Encoding $false))
	try {
		$writer.Write($content)
	}
	finally {
		$writer.Dispose()
	}
}

function Write-Xlsx([string]$path, [string]$sheetName, [string]$sheetXml) {
	if (Test-Path -LiteralPath $path) {
		Remove-Item -LiteralPath $path
	}
	$stream = [IO.File]::Open($path, 'CreateNew')
	$zip = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
	try {
		Add-Entry $zip '[Content_Types].xml' ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
			'<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">' +
			'<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>' +
			'<Default Extension="xml" ContentType="application/xml"/>' +
			'<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>' +
			'<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>' +
			'<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>' +
			'</Types>')
		Add-Entry $zip '_rels/.rels' ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
			'<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
			'<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>' +
			'</Relationships>')
		Add-Entry $zip 'xl/workbook.xml' ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
			'<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">' +
			"<sheets><sheet name=`"$([Security.SecurityElement]::Escape($sheetName))`" sheetId=`"1`" r:id=`"rId1`"/></sheets></workbook>")
		Add-Entry $zip 'xl/_rels/workbook.xml.rels' ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
			'<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
			'<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>' +
			'<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>' +
			'</Relationships>')
		Add-Entry $zip 'xl/styles.xml' ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
			'<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' +
			'<numFmts count="3"><numFmt numFmtId="164" formatCode="yyyy\-mm\-dd"/><numFmt numFmtId="165" formatCode="hh:mm"/>' +
			'<numFmt numFmtId="166" formatCode="yyyy\-mm\-dd hh:mm:ss"/></numFmts>' +
			'<fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>' +
			'<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>' +
			'<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>' +
			'<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>' +
			'<cellXfs count="5"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>' +
			'<xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>' +
			'<xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>' +
			'<xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>' +
			'<xf numFmtId="166" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs>' +
			'</styleSheet>')
		Add-Entry $zip 'xl/worksheets/sheet1.xml' $sheetXml
	}
	finally {
		$zip.Dispose()
		$stream.Dispose()
	}
}

$csvFiles = @(Get-ChildItem -LiteralPath (Join-Path $Folder 'CSV') -Filter *.csv)
$excelFolder = Join-Path $Folder 'Excel'
[void](New-Item -ItemType Directory -Force -Path $excelFolder)
foreach ($csv in $csvFiles) {
	$rows = Read-Csv $csv.FullName
	$sheetName = $csv.BaseName.Substring(0, [math]::Min(31, $csv.BaseName.Length))
	Write-Xlsx (Join-Path $excelFolder ($csv.BaseName + '.xlsx')) $sheetName (Get-SheetXml $rows)
}
