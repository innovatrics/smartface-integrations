import os
import requests

def run_graphql_query(paginated_query, graphql_url):
    headers = {'Content-Type': 'application/json'}
    payload = {'query': paginated_query}
    try:
        response = requests.post(graphql_url, headers=headers, json=payload)
        response_data = response.json()
        return response_data
    except requests.exceptions.RequestException as e:
        print(f"An error occurred during the API request: {e}")
        return None

def fetch_items_with_pagination(query, item_type, graphql_url, timefrom, timeto):
    all_items = []
    skip = 0
    take = 1000
    
    while True:
        paginated_query = query.replace('{take}', str(take)).replace('{skip}', str(skip)).replace('{timefrom}', str(timefrom)).replace('{timeto}', str(timeto))
        response_data = run_graphql_query(paginated_query, graphql_url)

        if response_data is None:
            # Request failed, break the loop
            break

        # Check for errors in response data
        if 'errors' in response_data:
            print(f"Error in response data: {response_data['errors']}")
            break

        data = response_data.get('data', {})
        items = data.get(item_type, {}).get('items', [])

        if not items:
            break
        
        all_items.extend(items)
        skip += take

    return all_items

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
    graphql_url = 'http://YOUR-URL:8097/graphql/'   # Replace with the actual GraphQL API endpoint URL
    api_url = 'http://YOUR-URL:8098/api/v1/Images'  # Replace with the actual API endpoint for image retrieval
    save_to_folder = "./images/"                  
    timefrom = "2023-07-31T12:46:00"                # Replace with your DATETIME
    timeto = "2023-07-31T12:50:00"                  # Replace with your DATETIME

    query = '''
    query {
      faces(take: {take}, skip: {skip}, order: {createdAt:ASC},where: {
        and: [
          { createdAt: { gt: "{timefrom}" }},
          { createdAt: { lt: "{timeto}" }}
        ]
      }) {
        items {
          id,
          createdAt,
          imageDataId
        }
      }
      pedestrians(take: {take}, skip: {skip}, order: {createdAt:ASC},where: {
        and: [
          { createdAt: { gt: "{timefrom}" }},
          { createdAt: { lt: "{timeto}" }}
        ]
      }) {
        items {
          id,
          createdAt,
          imageDataId
        }
      }
      genericObjects(take: {take}, skip: {skip}, order: {createdAt:ASC},where: {
        and: [
          { createdAt: { gt: "{timefrom}" }},
          { createdAt: { lt: "{timeto}" }}
        ]
      }) {
        items {
          id,
          createdAt,
          imageDataId
        }
      }
    }
    '''

    all_image_data_ids = []

    # Fetch faces
    faces_items = fetch_items_with_pagination(query, 'faces', graphql_url, timefrom, timeto)
    for item in faces_items:
        all_image_data_ids.append(item['imageDataId'])

    # Fetch pedestrians
    pedestrians_items = fetch_items_with_pagination(query, 'pedestrians', graphql_url, timefrom, timeto)
    for item in pedestrians_items:
        all_image_data_ids.append(item['imageDataId'])

    # Fetch genericObjects
    generic_objects_items = fetch_items_with_pagination(query, 'genericObjects', graphql_url, timefrom, timeto)
    for item in generic_objects_items:
        all_image_data_ids.append(item['imageDataId'])

    print("All imageDataIds:", all_image_data_ids)

    apply_curl_to_items(api_url, all_image_data_ids, save_to_folder)
