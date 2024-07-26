param($argsJson)

write-host $argsJson
$json = Convertfrom-json $argsJson
$tagValue = $json.tagValue
if($tagValue -eq $null){
    write-host "Could not get tag value!"
    exit 1    
}

write-host "Found tag value $tagValue"
if($tagValue.StartsWith("env-") -eq $false){
    write-host "is not env- tag, do not proceed"
    exit 0
}

if($tagValue -ne "env-staging"){
    write-host "is not env-staging, do not proceed"
    exit 0
}

git config --global user.email "auto@chester.com"
git config --global user.name "chester"

write-host "replace production file with staging file"
$stagingFile = resolve-path "$PSScriptRoot/../../deploy/chart/versions/staging.yaml"
$productionFile = resolve-path "$PSScriptRoot/../../deploy/chart/versions/production.yaml"
rm $productionFile
cp $stagingFile $productionFile

# need to create 2 commits, first of the updated app version then of the updated infra hash
git add -A
git commit -am "Update production to $tagValue"

$productionTargetRevisionFile = resolve-path "$PSScriptRoot/../chart/versions/production.yaml"
$commitId = git show-ref HEAD -s
rm $productionTargetRevisionFile
"production_deploy_target_revision: $commitId" | out-file $productionTargetRevisionFile

git add -A
git commit -am "Update deploy version to to $commitId"

git push
