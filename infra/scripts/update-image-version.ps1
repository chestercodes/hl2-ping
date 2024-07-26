param($argsJson)

write-host $argsJson
$json = Convertfrom-json $argsJson
$tagValue = $json.tagValue
if($tagValue -eq $null){
    write-host "Could not get tag value!"
    exit 1    
}

write-host "Found tag value $tagValue"
if($tagValue.StartsWith("v") -eq $false){
    write-host "Not pushed version, ignore."
    exit 0
}

write-host "write tag to staging file"
$stagingFile = resolve-path "$PSScriptRoot/../../deploy/chart/versions/staging.yaml"
"image_tag: $tagValue" | out-file $stagingFile
git config --global user.email "auto@chester.com"
git config --global user.name "chester"
git add -A
git commit -am "Update staging to $tagValue"
git push
