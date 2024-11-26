import requests
import json
import base64
import os

# Define the API endpoint
API_ENDPOINT = "https://api.guard.smartfacecloud.com/api/v1/WatchlistMembers/Register"
WATCHLIST_ID = "0d3b0376-b94a-4195-bbce-980ee2e61a67"
IMAGE_FOLDER = "images"  # Folder where images are stored

countSuccess = 0
countFailed = 0
failedData = []

# Function to read and encode image as base64
def get_image_base64(image_path):
    try:
        # Read the image file and encode it to base64
        with open(image_path, "rb") as image_file:
            encoded_string = base64.b64encode(image_file.read()).decode('utf-8')
        return encoded_string
    except FileNotFoundError:
        print(f"Image file '{image_path}' not found.")
        return ""

# Function to send POST request to the API
def send_data_to_api(name, image_data, recordnumber):
    global countSuccess
    global countFailed
    global failedData
    
    payload = {
        "id": recordnumber,
        "images": [
            {
                "faceId": None,
                "data": image_data  # Base64 image encoded data
            }
        ],
        "watchlistIds": [
            WATCHLIST_ID
        ],
        "faceDetectorConfig": {
            "minFaceSize": 25,
            "maxFaceSize": 600,
            "maxFaces": 20,
            "confidenceThreshold": 300
        },
        "faceValidationMode": "none",
        "faceDetectorResourceId": "cpu",
        "templateGeneratorResourceId": "cpu",
        "keepAutoLearnPhotos": False,
        "displayName": name,
        "fullName": name,
        "note": "",
        "labels": []
    }

   
    headers = {
        "Content-Type": "application/json",
        "Authorization": "Bearer eyJhbGciOiJSUz..."    
    }

    # Sending the POST request to the API
    response = requests.post(API_ENDPOINT, headers=headers, data=json.dumps(payload))
    
    if response.status_code == 201:
        print(f"Success: {name} data sent.")
        countSuccess += 1
    else:
        failedMessage = f"Failed to send data for {name}. Status code: {response.status_code}, Response: {response.text}"
        print(failedMessage)
        countFailed += 1
        failedData.append({
            'name': name,
            'status': response.status_code,
            'response': response.text
        })  

# Function to process images and send data
def process_images_and_send_data():
    recordnumber = 1
    
    for image_filename in os.listdir(IMAGE_FOLDER):
        # Ensure we are only processing image files
        if not image_filename.lower().endswith(('.png', '.jpg', '.jpeg', '.jfif')):
            continue
        
        image_path = os.path.join(IMAGE_FOLDER, image_filename)
        
        # Use the first part of the filename before any underscore as the name
        filename_without_ext = os.path.splitext(image_filename)[0]
        name = filename_without_ext.split('_')[0]  # Take only the part before the first underscore

        # Encode the image to base64
        image_data = get_image_base64(image_path)

        # Call API with the image data
        send_data_to_api(name, image_data, recordnumber)
        recordnumber += 1


# Function to log failed data
def log_failed_data(log_file_path, failed_data):
    # Open the log file in append mode
    with open(log_file_path, 'a', encoding='utf-8') as log_file:
        for fail in failed_data:
            # Print the failed data
            print(fail)

            # Write the failed data to the log file
            log_file.write(f"Name: {fail['name']}\n")
            log_file.write(f"Status Code: {fail['status']}\n")
            log_file.write(f"Response: {fail['response']}\n")
            log_file.write("\n")  # Add a newline to separate entries

# Process images and send data
process_images_and_send_data()

# Example usage after processing all data
log_file_path = 'failed_data_log.txt'

# Call the logging function after image processing is done
if failedData:
    log_failed_data(log_file_path, failedData)

print(f"Total Success: {countSuccess}")
print(f"Total Failed: {countFailed}")
