import requests
import os
import re # Make sure to import the re module for regular expressions

def sanitize_filename(filename):
    """Replace invalid characters in the filename."""
    return re.sub(r'[<>:"/\\|?*]', '_', filename)

def apply_curl_to_items(api_url, image_data_ids, save_to_folder):
    print("Downloading images:")
    #print(image_data_ids)
    
    for image_id, full_name, display_name, createdAt in image_data_ids:
        url = f"{api_url}/{image_id}"
        headers = {"accept": "image/jpeg"}

        try:
            response = requests.get(url, headers=headers)

            # Check if the request was successful (status code 200)
            if response.status_code == 200:
                
                # Sanitize the createdAt timestamp
                sanitized_createdAt = sanitize_filename(createdAt)
                
                # Save the image data to a file in the specified folder
                file_name = full_name if full_name else display_name
                file_path = os.path.join(save_to_folder, f"{sanitized_createdAt}_{file_name}_{image_id}.jpg")
                with open(file_path, "wb") as file:
                    file.write(response.content)
                print(f"Image saved: {file_path}")
            else:
                print(f"Failed to retrieve image with id: {image_id}, Status code: {response.status_code}")

        except requests.exceptions.RequestException as e:
            print(f"An error occurred while processing image with id: {image_id}, Error: {e}")


if __name__ == '__main__':
    api_url = 'http://192.168.101.10:8098/api/v1/Images'  # Replace with the actual API endpoint for image retrieval
    save_to_folder = "./images/"                  

    # GraphQL endpoint URL
    graphql_url = "http://192.168.101.10:8097/graphql/"

    # GraphQL query
    query = """
   query($skip: Int!, $take: Int!)
    {
          matchResults(
              skip: $skip, take: $take, order: {createdAt: ASC}, 
              where: {
                    createdAt: {gte: "2024-09-25T09:00:00.000Z"}
                    }
          ) 
          
        {  # Add skip and take here
            items {
            createdAt
            watchlistMemberFullName
            watchlistMemberDisplayName
            faceId
            frameId
            faceOrder
            age
            gender
            frame {
                id
                imageDataId
            }
            }
            totalCount
            pageInfo {
            hasNextPage
            hasPreviousPage
            }
        }
    }
    """

    take = 100  # Number of items to fetch per request
    skip = 0    # Starting point for each batch
    has_more_data = True
    all_data = []
    image_data_ids = []  # Initialize this at a higher scope so it can be used later

    # Set up headers for the request
    headers = {
        "Content-Type": "application/json",
    }

    image_data_ids = []
    while has_more_data:
        # Set up the variables for the GraphQL query
        variables = {
            "skip": skip,
            "take": take
        }

        try:
            # Make the GraphQL request with variables
            response = requests.post(graphql_url, json={"query": query, "variables": variables}, headers=headers)
            response.raise_for_status()  # Check for HTTP errors
            data = response.json()

            if "errors" in data:
                print(f"Error: {data['errors']}")
                break

            # Update skip for the next batch
            skip += take

            # Extract imageDataId values
            
            totalCount = data["data"]["matchResults"]["totalCount"]
            for item in data["data"]["matchResults"]["items"]:
                createdAt = item["createdAt"]
                fullname = item["watchlistMemberFullName"]
                displayname = item["watchlistMemberDisplayName"]
                frameImageId = item["frame"]["imageDataId"]
                image_data_ids.append((frameImageId, fullname, displayname, createdAt))
            
                print(frameImageId, fullname, displayname, createdAt)

            # Check if we've fetched all items
            if skip >= totalCount:
                has_more_data = False

        except requests.exceptions.RequestException as e:
            print("An error occurred:", e)
            print("Response content:", response.content)
            break

    #After fetching all the data, call apply_curl_to_items to download images
    if image_data_ids:
        try:
            apply_curl_to_items(api_url, image_data_ids, save_to_folder)
        except requests.exceptions.RequestException as e:
            print("Error while downloading an image:", e)
            print("Response content:", response.content)
