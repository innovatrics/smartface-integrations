import csv
import requests
import json
import base64
import os

# Define the API endpoint
API_ENDPOINT = "http://sface-integ-2u:8098/api/v1/WatchlistMembers/Register"
WATCHLIST_ID = "13d80d4b-f512-46ca-9d36-3732fd615f04"
IMAGE_FOLDER = "images"  # Folder where images are stored

countSuccess = 0
countFailed = 0
failedData = []

# Function to read and encode image as base64
def get_image_base64(image_filename):
    # Construct the full path to the image file
    image_path = os.path.join(IMAGE_FOLDER, image_filename)
    
    try:
        # Read the image file and encode it to base64
        with open(image_path, "rb") as image_file:
            encoded_string = base64.b64encode(image_file.read()).decode('utf-8')
        return encoded_string
    except FileNotFoundError:
        print(f"Image file '{image_filename}' not found.")
        return ""

# Function to send POST request to the API
def send_data_to_api(name, full_name, note, image_data, years,recordnumber):
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
        "faceDetectorResourceId": "cpu",
        "templateGeneratorResourceId": "cpu",
        "keepAutoLearnPhotos": False,
        "displayName": name,
        "fullName": name,
        "note": years,
        "labels": [{
            
            "key": "years",
            "value": years
            }
        ]
    }

    headers = {'Content-Type': 'application/json'}
    
    # Sending the POST request to the API
    response = requests.post(API_ENDPOINT, headers=headers, data=json.dumps(payload))
    
    if response.status_code == 201:
        print(f"Success: {note} data sent.")
        countSuccess += 1
    else:
        failedMessage = f"Failed to send data for {note}. Status code: {response.status_code}, Response: {response.text}"
        print(failedMessage)
        countFailed += 1
        failedData.append({
            'name': note,
            'status': response.status_code,
            'response': response.text
        })  

# Function to read CSV and process data
def process_csv_and_send_data(csv_file_path):
    with open(csv_file_path, mode='r', encoding='utf-8-sig') as file:
        csv_reader = csv.DictReader(file, quotechar='"')
        
        # Print the headers to check if they are correct
        headers = csv_reader.fieldnames
        print("CSV Headers:", headers)  # This will show what headers are actually being recognized
        
        recordnumber = 1
        
        for row in csv_reader:
            # Check if the 'Name' column exists
            if 'Name' not in row:
                print("Error: 'Name' column not found in the CSV. Check the headers.")
                return  # Stop the script if 'Name' column is missing
            
            # Extract relevant data from CSV
            name = row['Name']
            note = row['Name and Surname']
            image_file = row['Images']  # Get the image filename from the 'Images' column
            years = row['Years']
            
            # Encode the image to base64
            image_data = get_image_base64(image_file) if image_file else ""

            # Call API with the extracted data
            send_data_to_api(name, name, note, image_data, years, recordnumber)
            recordnumber += 1

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

# Specify the path to your CSV file
csv_file_path = r'c:\Users\jberes\Documents\work\Projects\2024\20th-Anniversary\scripts\seedingScript\employees_v01.csv'

# Process CSV and send data
process_csv_and_send_data(csv_file_path)

# Example usage after processing all data
log_file_path = 'failed_data_log.txt'

# Call the logging function after your CSV processing is done
if failedData:
    log_failed_data(log_file_path, failedData)

print(f"Total Success: {countSuccess}")
print(f"Total Failed: {countFailed}")