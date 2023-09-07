# Liveness Test

This script allows you to run a liveness test on a set of images coming from zip files or directories. The resulting log file contains data for each image in a csv format

## How to run

To run the Liveness Test you need python3 environment. Using the code below you can run the script:
``` python3 LivenessTest.py ```

If your environment is missing any package necessary to run the script you can install it using the code:
``` pip3 install <missing-package> ```

You can use the help command to know more about available options:
``` python3 LivenessTest.py --help ``` or ``` python3 LivenessTest.py -h ```

## How to compile into an executable file

If you have your python 3 environment running you can generate an executable file from your current code of the Liveness Test. The **pyinstaller** python package will allow you to do that.

To install pyinstaller you can run the code below:
``` pip3 install pyinstaller ```

To generate an executable file run the code below in the folder with the Liveness Test files:
``` python3 pyinstaller LivenessTest.py --onefile ```

Once the executable file is created you will find it within the newly created folder dist.