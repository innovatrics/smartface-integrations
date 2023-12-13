import sys
import os
import glob
import json
import zipfile
import datetime
import base64
import requests
import time
import pathlib

SETUP_RESULTS_PREFIX = "DEFAULT"
SETUP_RESTAPIURL = "http://localhost:8098"
SETUP_RESTAPI = SETUP_RESTAPIURL + "/api/v1/Watchlists/Search"
SETUP_TESTWATCHLIST = "CheckLivenessTest"

spoofvalue_minFaceSize = 30
spoofvalue_maxFaceSize = 600
spoofvalue_confidenceThreshold = 450
spoofvalue_distantLivenessScoreThreshold = 90
spoofvalue_distantLivenessConditions = "default"

LOCALPATH = pathlib.Path(__file__).parent.resolve()

def main():
    localpath = LOCALPATH
    inputFiles = []
    ZIPLIST = []
    args = sys.argv[1:]

    print("Liveness Test")

    if (len(args) == 0):
        print("\nThis script will take zip files (*.zip) inside the 'inputs' folder and put the output logs into the 'results' folder. For more information run the script with --help or -h parameter.")

    if len(args) == 1 and (args[0] == '--help' or args[0] == '-h'):
        print("\nPlease adjust the variables inside the script to set up your test. Additional options are possible:\n")
        print("LivenessTest.py --input input.zip => setup another input file using the local path")
        print("LivenessTest.py --input input01.zip input02.zip input03.zip => setup a list of additional zip input files using the local path")

    if len(args) > 1 and args[0] == '--input':
        inputFiles = sys.argv[2:]

    if inputFiles:
        for item in inputFiles:
            ZIPLIST.append(str(localpath) + "/" + item)
    else:
        os.chdir("./inputs")

        filesFound = 0
        for file in glob.glob("*.zip"):
            filesFound += 1
            ZIPLIST.append(str(localpath) + "/inputs/" + file)

        if (filesFound == 0):
            print("\nNo files to process in the 'input' folder.")
            exit()

    if (len(ZIPLIST) == 0):
        print("\nThere is nothing to run.")
        exit()
    else:
        print("\nZip files to be processed:\t"+str(ZIPLIST))

        WatchlistIDSet = checkWatchlist()
        if (not WatchlistIDSet):
            WatchlistIDSet = createWatchlist()
        else:
            print(
                "Watchlist for testing purposes exists, it will be used now: " + WatchlistIDSet)

        now = datetime.datetime.now()
        LOGFILE_NAME = str(localpath) + "/results/" + SETUP_RESULTS_PREFIX + \
            "-" + now.strftime("%Y%m%d-%H_%M_%S") + ".csv"
        print("\nOutput file: " + str(LOGFILE_NAME) + "\n")
        startOverall = time.time()
        
        with open(LOGFILE_NAME, 'a') as LogTest:
            LogTest.write(
                "ZIPNAME, IMAGE_FILE_NAME, FACE_QLT, FACE_SIZE, D_CODE, D_STATUS, D_DIST_LVNS_PERFORMED, D_DIST_LVNS_PASSED, D_DIST_LVNS_SCORE")
            LogTest.close()
        
        for ZIPITEM in ZIPLIST:
            checkZip(ZIPITEM, LOGFILE_NAME, WatchlistIDSet)

        finishOverall = time.time()
        OverallTime = finishOverall - startOverall
        print("Total time to process: " + str(OverallTime))
        print("\n\nThe script has ended successfully")


def checkZip(fileInput, log, watchlistId):
    print("Processing file:" + fileInput)
    StartCurrent = time.time()

    print('{:<32s}{:<35s}{:<20s}{:<30s}{:<6s}{:<20s}{:<7s}{:<7s}{:<20s}'.format(
        "ZIPNAME", "FILE_NAME", "FACEQLT", "FACESIZE", "CODE", "D_STATUS", "PERF", "PASSED", "SCORE"))
    with zipfile.ZipFile(fileInput, mode='r') as ZipInput:
        if (len(ZipInput.filelist) == 0):
            print("\tThe ZIP file does not have any images present.")
            exit()
        else:

            for file in ZipInput.filelist:

                if (not file.is_dir()):

                    IMAGE_FILE_NAME = file.filename.split("/")[-1]

                    with ZipInput.open(file.filename, "r") as image_file:
                        encoded_string = base64.b64encode(
                            image_file.read()).decode('utf-8')

                    PostData = {
                        "image": {
                            "data": encoded_string
                        },
                        "watchlistIds": [
                            watchlistId
                        ],
                        "threshold": 40,
                        "maxResultCount": 1,
                        "faceDetectorConfig": {
                            "minFaceSize": spoofvalue_minFaceSize,
                            "maxFaceSize": spoofvalue_maxFaceSize,
                            "maxFaces": 20,
                            "confidenceThreshold": spoofvalue_confidenceThreshold
                        },
                        "faceDetectorResourceId": "cpu",
                        "templateGeneratorResourceId": "cpu",
                        "faceMaskConfidenceRequest": {
                            "faceMaskThreshold": 3000
                        },
                        "faceFeaturesConfig": {
                            "age": 'false',
                            "gender": 'false',
                            "faceMask": 'true',
                            "noseTip": 'false',
                            "yawAngle": 'false',
                            "pitchAngle": 'false',
                            "rollAngle": 'false'
                        },
                        "spoofDetectorResourceIds": [
                            "liveness_distant_cpu_remote"
                        ],
                        "spoofCheckConfig": {
                            "distantLivenessScoreThreshold": spoofvalue_distantLivenessScoreThreshold,
                            "distantLivenessConditions": spoofvalue_distantLivenessConditions
                        }

                    }

                    PostData = json.dumps(PostData)
                    PostHeaders = {'Accept': 'application/json',
                                   'Content-Type': 'application/json'}

                    try:
                        response = requests.post(
                            SETUP_RESTAPI, data=PostData, headers=PostHeaders)
                        response_info = response.json()

                    except Exception as e:
                        print(
                            '\t\tERROR: POST Request failed. Could not do a liveness check. Reason: %s' % (e))

                    finally:

                        D_CODE = response.status_code
                        FACE_QLT = ""
                        FACE_SIZE = ""

                        if (response.status_code == 200):

                            D_STATUS = "OK"

                            D_DIST_LVNS_PERFORMED = str(
                                response_info[0]['spoofCheckResult']['distantLivenessSpoofCheck']['performed'])
                            D_DIST_LVNS_PASSED = str(
                                response_info[0]['spoofCheckResult']['distantLivenessSpoofCheck']['passed'])
                            D_DIST_LVNS_SCORE = str(
                                response_info[0]['spoofCheckResult']['distantLivenessSpoofCheck']['score'])
                            FACE_QLT = str(response_info[0]['quality'])
                            FACE_SIZE = str(response_info[0]['faceSize'])

                            if (response_info[0]['spoofCheckResult']['distantLivenessSpoofCheck']['performed'] == False):
                                D_STATUS = "No check done"
                            else:
                                if (response_info[0]['spoofCheckResult']['distantLivenessSpoofCheck']['passed'] == True):
                                    D_STATUS = "Live"
                                elif (response_info[0]['spoofCheckResult']['distantLivenessSpoofCheck']['passed'] == False):
                                    D_STATUS = "Spoof"

                        elif (response.status_code == 400):

                            D_STATUS = "No face detected"
                            D_DIST_LVNS_PERFORMED = ""
                            D_DIST_LVNS_PASSED = ""
                            D_DIST_LVNS_SCORE = ""
                            FACE_QLT = "N/A"
                            FACE_SIZE = "N/A"

                        elif (response.status_code == 408):

                            D_STATUS = "Request Time Out"
                            D_DIST_LVNS_PERFORMED = ""
                            D_DIST_LVNS_PASSED = ""
                            D_DIST_LVNS_SCORE = ""

                        else:
                            D_STATUS = "Critical Error/Wrong Response"
                            D_DIST_LVNS_PERFORMED = ""
                            D_DIST_LVNS_PASSED = ""
                            D_DIST_LVNS_SCORE = ""

                        head, tail = os.path.split(fileInput)
                        print('{:<32s}{:<35s}{:<20s}{:<30s}{:<6s}{:<20s}{:<7s}{:<7s}{:<20s}'.format(tail, IMAGE_FILE_NAME, FACE_QLT, FACE_SIZE, str(
                            D_CODE), D_STATUS, D_DIST_LVNS_PERFORMED, D_DIST_LVNS_PASSED, str(D_DIST_LVNS_SCORE)))

                        RUN_TIME = datetime.datetime.now()
                        with open(log, 'a') as LogTest:
                            LogTest.write("\n" + tail + "," + IMAGE_FILE_NAME+"," + FACE_QLT + "," + FACE_SIZE + "," + str(
                                D_CODE) + ","+D_STATUS+","+D_DIST_LVNS_PERFORMED+","+D_DIST_LVNS_PASSED+","+str(D_DIST_LVNS_SCORE))

    StopCurrent = time.time()
    CurrentTotal = StopCurrent - StartCurrent
    print("Time to process zip file: " + str(CurrentTotal))


def checkWatchlist():

    PageNumber = 1
    PageSize = 10
    foundWatchlist = False

    print("Searching for existig watchlist " + SETUP_TESTWATCHLIST)

    while (foundWatchlist == False):
        try:
            checkWatchlistURL = SETUP_RESTAPIURL + "/api/v1/Watchlists/?PageNumber=" + \
                str(PageNumber) + "&PageSize=" + str(PageSize)

            response = requests.get(checkWatchlistURL)
            response_info = response.json()

        except Exception as e:
            print('\t\tERROR: GET Request failed. Could not locate the test watchlist. Could not do a liveness check . Reason: %s' % (e))

        finally:

            if (response.status_code == 200):
                for item in response_info['items']:
                    if (item['fullName'] == SETUP_TESTWATCHLIST):
                        return item['id']
                print("Page: " + str(PageNumber) +
                      "> not found, trying in next set of " + str(PageSize))
                if (response_info['nextPage'] != None):
                    PageNumber += 1
                else:
                    return False
            elif (response.status_code == 404):
                print("\nThe request was not valid.")
                exit()
            else:
                print("Something went wrong.")
                exit()


def createWatchlist():
    print("Creating Watchlist>")

    PostData = {
        "displayName": SETUP_TESTWATCHLIST,
        "fullName": SETUP_TESTWATCHLIST,
        "threshold": 100,
        "previewColor": "#4adf62"
    }
    PostData = json.dumps(PostData)
    PostHeaders = {'Accept': 'application/json',
                   'Content-Type': 'application/json'}

    try:
        response = requests.post(
            SETUP_RESTAPIURL + "/api/v1/Watchlists", data=PostData, headers=PostHeaders)
        response_info = response.json()
    except Exception as e:
        print(
            '\t\tERROR: POST Request failed. Could not create a watchlist. Reason: %s' % (e))
        exit()

    finally:
        if (response.status_code == 201):
            print("New watchlist " + SETUP_TESTWATCHLIST +
                  " created with an id:" + str(response_info['id']))
            return str(response_info['id'])
        else:
            print("Watchlist was not created.")
            exit()


if __name__ == "__main__":
    main()
