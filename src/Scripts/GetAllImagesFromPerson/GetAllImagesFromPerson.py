import requests
import os

def apply_curl_to_items(api_url, image_data_ids, save_to_folder):
    for image_id in image_data_ids:
        url = f"{api_url}/{image_id}"
        headers = {"accept": "image/jpeg"}

        try:
            response = requests.get(url, headers=headers)

            # Check if the request was successful (status code 200)
            if response.status_code == 200:
                # Save the image data to a file in the specified folder
                file_path = os.path.join(save_to_folder, f"{image_id}.jpg")
                with open(file_path, "wb") as file:
                    file.write(response.content)
                print(f"Image saved: {file_path}")
            else:
                print(f"Failed to retrieve image with id: {image_id}, Status code: {response.status_code}")

        except requests.exceptions.RequestException as e:
            print(f"An error occurred while processing image with id: {image_id}, Error: {e}")

if __name__ == '__main__':
    api_url = 'http://<enter-your-ip>:8098/api/v1/Images'  # Replace with the actual API endpoint for image retrieval
    save_to_folder = "./images/"                  

    # GraphQL endpoint URL
    graphql_url = "http://<enter-your-ip>:8097/graphql/"

    # GraphQL query
    query = """
    query{
    watchlistMembers(where: {displayName: {eq: "Lucia"}}) {
        items {
        id
        fullName
        displayName
        tracklet {
            faces {
            createdAt
            faceType
            imageDataId
            }
        }
        }
    }
    }
    """

    # Set up headers for the request
    headers = {
        "Content-Type": "application/json",
    }
    try:
        response = requests.post(graphql_url, json={"query": query}, headers=headers)
        response.raise_for_status()  # Check for HTTP errors
        data = response.json()

        # Extract imageDataId values
        image_data_ids = []
        for item in data["data"]["watchlistMembers"]["items"]:
            for face in item["tracklet"]["faces"]:
                image_data_ids.append(face["imageDataId"])

        print("imageDataId values:")
        for image_data_id in image_data_ids:
            print(image_data_id)

    except requests.exceptions.RequestException as e:
        print("An error occurred:", e)
        print("Response content:", response.content)
        
        
    try:
        apply_curl_to_items(api_url, image_data_ids, save_to_folder)
        
    except requests.exceptions.RequestException as e:
        print("Error while downloading an image",e)
        print("Response content:", response.content)
        