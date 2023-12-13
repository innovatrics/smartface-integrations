# TrafficLight

This script allows you to run a traffic light server for the purpose of Access Control.

## How to run

To run you need python3 environment. Using the code below you can run the script:
``` python3 trafficlight.py ```

If your environment is missing any package necessary to run the script you can install it using the code:
``` pip3 install <missing-package> ```

Access the binded URL and port to show the Traffic Light on a device with a web browser.

## How to compile into an executable file

If you have your python 3 environment running you can generate an executable file from your current code of the Liveness Test. The **pyinstaller** python package will allow you to do that.

To install pyinstaller you can run the code below:
``` pip3 install pyinstaller ```

To generate an executable file run the code below in the folder with the Liveness Test files:
``` python3 -m PyInstaller trafficlight.py --onefile ```

Once the executable file is created you will find it within the newly created folder dist.