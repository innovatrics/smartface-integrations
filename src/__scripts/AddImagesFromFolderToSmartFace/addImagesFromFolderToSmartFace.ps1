$apiEndpoint = "http://<add-smartface-url>/api/v1/WatchlistMembers/Register"
$directory = "<add-full-path-to-your-folder>"
$watchlistId = "<add-watchlistId>"

# Get all JPG files in the directory
$files = Get-ChildItem -Path $directory -Filter "*.jpg" -File

echo $files


# Iterate through each file
foreach ($file in $files) {
    $imageData = [System.Convert]::ToBase64String((Get-Content -Path $file.FullName -Encoding Byte))
    $fileName = $file.Name
    $fileNameWithoutExtension = $file.BaseName
	
    $json = @{
        "id"                          = $fileNameWithoutExtension
        "images"                      = @(
            @{
                "faceId" = $null
                "data"   = $imageData
            }
        )
        "watchlistIds"                = @(
            $watchlistId
        )
        "faceDetectorConfig"          = @{
            "minFaceSize"         = 35
            "maxFaceSize"         = 600
            "maxFaces"            = 20
            "confidenceThreshold" = 450
        }
        "faceDetectorResourceId"      = "cpu"
        "templateGeneratorResourceId" = "cpu"
        "keepAutoLearnPhotos"         = $False
        "displayName"                 = $fileNameWithoutExtension
        "fullName"                    = $fileNameWithoutExtension
        "labels"                      = $null
    } | ConvertTo-Json

    # Make the API call
    $response = Invoke-RestMethod -Uri $apiEndpoint -Method Post -Body $json -ContentType "application/json"

    # Process the API response if needed
    # For example, you can output the response to the console
    Write-Host "API Response for " +$fileName+":`n$response`n"
    Write-Host ""
}
