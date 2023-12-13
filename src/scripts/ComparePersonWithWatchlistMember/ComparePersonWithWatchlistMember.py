import requests
import base64
import json
import csv

''' 
Here we try to get similarity between the image and each face of a watchlistmember

Set FULL NAME of the person to test
Set file which to compare
Get all the faces of the person
Do Verification with each face
Write outputs into a csv file
'''

# API endpoint URL
rest_api_url = "http://localhost:8098"

# graphQL API URL
graphql_url = "http://localhost:8097/graphql/"

# Image file path
personToCompare = "lucia-fake-detail.png"
watchlistMember = "Lucia"

# Path to save CSV file
csv_file_path = "data.csv"

def imageFile_to_base64(image_path):
    with open(image_path, "rb") as image_file:
        image_data = image_file.read()
        base64_encoded = base64.b64encode(image_data).decode("utf-8")
        return base64_encoded

def image_to_base64(image_file):
    base64_encoded = base64.b64encode(image_file).decode("utf-8")
    return base64_encoded

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

def fetch_items(query, graphql_url):

    paginated_query = query.replace("{FullName}",watchlistMember)
    response_data = run_graphql_query(paginated_query, graphql_url)

    if response_data is None:
        print("No data was returned")

    # Check for errors in response data
    if 'errors' in response_data:
        print(f"Error in response data: {response_data['errors']}")
        
    items = []
    for item in response_data["data"]["watchlistMembers"]["items"]:
        for face in item["tracklet"]["faces"]:
            items.append(face["imageDataId"])

    return items

def processImageDataIds(api_url, image_data_ids):
    for image_id in image_data_ids:
        url = f"{api_url}/api/v1/Images/{image_id}"
        headers = {"accept": "image/jpeg"}

        try:    
            response = requests.get(url, headers=headers)

        except requests.exceptions.RequestException as e:
            print(f"An error occurred while processing image with id: {image_id}, Error: {e}")

        if response.status_code == 200:
            #print("Request successful!")
            #print("Response JSON:", response.json())


            #print(response.content)

            imagebase64 = image_to_base64(response.content)
        
            if(imagebase64 != None):
                print("Got an image for image id " + image_id + ".")
                
                try:
                    json_payload = {                  
                                    "probeImage": {
                                        "image": {
                                        "data": imagebase64
                                        },
                                        "faceDetectorConfig": {
                                        "minFaceSize": 10,
                                        "maxFaceSize": 600,
                                        "confidenceThreshold": 450
                                        },
                                        "faceDetectorResourceId": "cpu",
                                        "templateGeneratorResourceId": "cpu"
                                    },
                                    "referenceImage": {
                                        "image": {
                                        "data": personToCompare_base64_encoded_image
                                        },
                                        "faceDetectorConfig": {
                                        "minFaceSize": 10,
                                        "maxFaceSize": 600,
                                        "confidenceThreshold": 450
                                        },
                                        "faceDetectorResourceId": "cpu",
                                        "templateGeneratorResourceId": "cpu"
                                    }
                                    }
                
                    #print(json_payload)    
                    
                    url = f"{api_url}/api/v1/Faces/Verify"
                    
                    #print(url)
                    response = requests.post(url, headers=headers, json=json_payload)
                
                except requests.exceptions.RequestException as e:
                    print(f"An error occurred while verifying confidence score for and image: {image_id}, Error: {e}")
                    
                if response.status_code == 200:                    
                    data = json.loads(response.content)
                    if response is None:
                        print("No data was returned")
                    
                    # Check for errors in response data
                    if 'errors' in response:
                        print(f"Error in response data: {data['errors']}")
                    
                    print("confidence: "+ str(data["confidence"]))
                    results.append(image_id + ", " + str(data["confidence"]) + ", " + str(data["probeFaceDetails"]["faceSize"]))
                elif response.status_code == 400:
                    results.append(image_id + ", 0")                    
                else:
                    print("Request failed with status code:", response.status_code)
                    print("Response JSON:", response.json())
                
        else:
            print("Request failed with status code:", response.status_code)
            print("Response text:", response.text)

query = '''
    query {
  watchlistMembers(where: {fullName: {eq: "{FullName}"}}) {
    items {
      displayName
      fullName
      tracklet {
        faces {
          imageDataId
        }
      }
    }
  }
}
    '''
results = []

# we get all the image data for the watchlistmember
items = fetch_items(query, graphql_url)

# We get base 64 of the image you want to compare
personToCompare_base64_encoded_image = imageFile_to_base64(personToCompare)

# we setup a header for the output csv file
results.append("image,confidence, facesize")

# we will process each file in here
processImageDataIds(rest_api_url, items)

# log the outcome
try: 
    with open(csv_file_path, "w") as csv_file:
        for row in results:
            csv_file.write(row+"\n")

except requests.exceptions.RequestException as e:
    print(f"An error occurred while trying to save the results into a CSV file, Error: {e}")

# here we will print the outcome
for item in results:
    print(item)