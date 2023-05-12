import argparse
import configparser
import subprocess
import json
import logging
import os
import datetime

# Parse arguments
parser = argparse.ArgumentParser()

# Add arguments
parser.add_argument('--thesisWrapper', help='Specify thesisWrapper to use/not use (default: False)', action='store_true')
args = parser.parse_args()
current_directory = os.getcwd()

# Configure logging settings
logging_path = os.path.join(current_directory, 'testing_scripts', 'logs')
os.makedirs(logging_path, exist_ok=True)  # Create the log directory if it doesn't exist
timestamp = datetime.datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
log_file_name = f"test1_{timestamp}.log"
log_file_path = os.path.join(logging_path, log_file_name)

logging.basicConfig(
    level=logging.DEBUG, 
    format='%(asctime)s %(levelname)s %(message)s',
    handlers=[
        logging.FileHandler(log_file_path), # Print to file
        logging.StreamHandler() # Print to console
    ]
)

file_path = os.path.join(current_directory, 'src\\Services\\Catalog\\Catalog.API\\appsettings.json')

with open(file_path, 'r', encoding='utf-8-sig') as f:
    data = json.load(f)

    # Change Catalog.API appsettings.json file
    if args.thesisWrapper == True:
        data["ThesisWrapperEnabled"] = True
        logging.info('ThesisWrapperEnabled set to True')
    else:
        data["ThesisWrapperEnabled"] = False
        logging.info('ThesisWrapperEnabled set to False')

# Write changes to Catalog.API appsettings.json file
with open(file_path, 'w') as f:
    json.dump(data, f, indent=4)

