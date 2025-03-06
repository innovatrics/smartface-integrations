param (
    [string]$version
)

if (-not $version) {
    Write-Host "Usage: .\release.ps1 <version>"
    exit 1
}

$repository = "registry.gitlab.com/innovatrics/smartface/integrations-auto-enroll"

# Build the Docker image
Write-Host "Building Docker image with tag $version..."
docker build -f src/AutoEnrollment/Dockerfile -t "$repository`:$version" .

# Tag the image as latest
Write-Host "Tagging the image as latest..."
docker tag "$repository`:$version" "$repository`:latest"

# Push the versioned image
Write-Host "Pushing versioned image $version..."
docker push "$repository`:$version"

# Push the latest tag
Write-Host "Pushing latest image..."
docker push "$repository`:latest"

Write-Host "Docker build and push process completed."