Invoke-RestMethod -ContentType "application/json" -Method Post -Uri http://192.168.0.110:8097/graphql/ -Body (@{variables = $null; query = `
'query {
  watchlistMembers(take: 1000) {
    items {
      fullName
      displayName
      id


    }
  }
}'} | ConvertTo-Json) | `
Select-Object -ExpandProperty data | `
Select-Object -ExpandProperty watchlistMembers | `
Select-Object -ExpandProperty items | `
ForEach-Object { 

Invoke-RestMethod -Method Delete -Uri http://192.168.0.110:8098/api/v1/WatchlistMembers/$($_.id)  
Write-Host "$($_.id) $($_.fullName) $($_.displayName)"
}