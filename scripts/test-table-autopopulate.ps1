#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'MapLab.csproj')
$name = $project.Project.PropertyGroup.AssemblyName
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root "artifacts/build-check/$name.dll"))
$type = $assembly.GetType('TimingTableCalculator.TableAutoPopulate',$true)
$apply = $type.GetMethod('Apply',[Reflection.BindingFlags]'Static,NonPublic')
function Populate([double[,]]$values,[ValueTuple[int,int][]]$selection,[double[]]$x,[double[]]$y) {
    $set = [Collections.Generic.HashSet[ValueTuple[int,int]]]::new()
    foreach ($cell in $selection) { $set.Add($cell) | Out-Null }
    return ,$apply.Invoke($null,@($values,$set,$x,$y))
}
$row = [double[,]]::new(1,4); $row[0,0]=10; $row[0,3]=40
$rowResult = Populate $row @([ValueTuple[int,int]]::new(0,0),[ValueTuple[int,int]]::new(0,1),[ValueTuple[int,int]]::new(0,2),[ValueTuple[int,int]]::new(0,3)) ([double[]](0,1,3,6)) ([double[]](50))
if ($rowResult[0,0] -ne 10 -or $rowResult[0,1] -ne 15 -or $rowResult[0,2] -ne 25 -or $rowResult[0,3] -ne 40) { throw 'Row auto-populate ignored physical X-axis spacing or changed endpoints.' }
$column = [double[,]]::new(3,1); $column[0,0]=10; $column[2,0]=40
$columnResult = Populate $column @([ValueTuple[int,int]]::new(0,0),[ValueTuple[int,int]]::new(1,0),[ValueTuple[int,int]]::new(2,0)) ([double[]](1000)) ([double[]](100,80,20))
if ($columnResult[0,0] -ne 10 -or $columnResult[1,0] -ne 17.5 -or $columnResult[2,0] -ne 40) { throw 'Column auto-populate ignored physical Y-axis spacing or changed endpoints.' }
$surface = [double[,]]::new(3,3); $surface[0,0]=0; $surface[0,2]=20; $surface[2,0]=20; $surface[2,2]=40
$rectangle = for ($r=0;$r -lt 3;$r++) { for ($c=0;$c -lt 3;$c++) { [ValueTuple[int,int]]::new($r,$c) } }
$surfaceResult = Populate $surface $rectangle ([double[]](0,1,2)) ([double[]](0,1,2))
if ($surfaceResult[1,1] -ne 20 -or $surfaceResult[0,0] -ne 0 -or $surfaceResult[2,2] -ne 40) { throw 'Rectangular auto-populate did not create the expected corner-anchored surface.' }
$invalid = Populate $surface @([ValueTuple[int,int]]::new(0,0),[ValueTuple[int,int]]::new(2,2)) ([double[]](0,1,2)) ([double[]](0,1,2))
if ($null -ne $invalid) { throw 'Disconnected selections must not overwrite unselected cells.' }
'Table-cell auto-populate tests passed.'
