import requests
import os

def rearrange_name(fullname):
    # Split the full name into words
    name_parts = fullname.split()

    # Check if the name has more than one word
    if len(name_parts) > 1:
        # Move the first word (assumed to be the surname) to the end
        rearranged_name = " ".join(name_parts[1:] + [name_parts[0]])
    else:
        # If there's only one word, return it as is
        rearranged_name = fullname

    return rearranged_name

def apply_curl_to_items(api_url, image_data_ids, save_to_folder):
    print("Downloading images:")
    #print(image_data_ids)
    
    for image_id, full_name, display_name in image_data_ids:
        url = f"{api_url}/{image_id}"
        headers = {"accept": "image/jpeg"}

        try:
            response = requests.get(url, headers=headers)

            # Check if the request was successful (status code 200)
            if response.status_code == 200:
                # Save the image data to a file in the specified folder
                file_name = full_name if full_name else display_name
                file_path = os.path.join(save_to_folder, f"{file_name}_{image_id}.jpg")
                with open(file_path, "wb") as file:
                    file.write(response.content)
                print(f"Image saved: {file_path}")
            else:
                print(f"Failed to retrieve image with id: {image_id}, Status code: {response.status_code}")

        except requests.exceptions.RequestException as e:
            print(f"An error occurred while processing image with id: {image_id}, Error: {e}")


if __name__ == '__main__':
    api_url = 'http://10.11.64.18:8098/api/v1/Images'  # Replace with the actual API endpoint for image retrieval
    save_to_folder = "./images/"                  

    # GraphQL endpoint URL
    graphql_url = "http://10.11.64.18:8097/graphql/"

    # GraphQL query
    query = """
    query($skip: Int!, $take: Int!)
    {
      watchlistMembers(
          where: {watchlists: {all: {id: {eq: "innovatrics"}}}},
          skip:$skip,
          take:$take
        ) {
        items {
          displayName
          fullName
          tracklet {
            faces(where: {faceType: {eq: REGULAR}}) {
              imageDataId
              faceType
            }
          }
        }
        totalCount
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
            
            totalCount = data["data"]["watchlistMembers"]["totalCount"]
            for item in data["data"]["watchlistMembers"]["items"]:
                fullname = item["fullName"]
                displayname = item["displayName"]
                for faces in item["tracklet"]["faces"]:
                    # Check if watchlistMember is a string or a dictionary
                    facedata = faces["imageDataId"]
                    
                    if fullname is None and displayname is not None:
                        fullname = displayname

                    fullname = rearrange_name(fullname)

                    image_data_ids.append((facedata, fullname, displayname))
                    print(facedata, fullname)

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
