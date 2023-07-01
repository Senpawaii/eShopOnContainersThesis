import sys
import os
import re


def parse_compressed_data(logs_folder):
    """Parse the data from the logs folder(s)"""

    compressed_keys = ['vus', '90th', '95th', 'avg']

    # Get the data from each log folder. Each log folder is a different system variation
    for folder in logs_folder:
        test_data = {key: [] for key in compressed_keys}
        test_name = folder.split("/")[-2] + "_" + folder.split("/")[-1] # For example: Wrapped_500V
        patterns = ["\d+ max VUs", "http_req_duration\.*:\savg=(\d+.?\d*[ms|s]+).*p\(90\)=(\d+.?\d*[ms|s]+).*p\(95\)=(\d+.?\d*[ms|s]+)"]
        print("Parsing compressed data from test <" + test_name + ">")
        # Access each log file present in the subfolder "Compressed"
        for file in os.listdir(os.path.join(folder, "Compressed")):
            # Open the file in read mode
            print("Parsing log file: <" + file + ">")
            with open(os.path.join(folder, "Compressed", file) , "r") as f:
                # Read the file line by line
                for line in f:
                    # Check if the line matches any of the defined patterns
                    for index, pattern in enumerate(patterns):
                        match = re.search(pattern, line)
                        if match:
                            # If it matches, then extract the data
                            data = match.group()
                            key = compressed_keys[index]
                            test_data[key].append(int(data))
                            




def main():
    # Read arguments
    if len(sys.argv) < 2:
        print("Usage: python GeneratePlot_UpdatePriceDiscount_Throughput.py <logs_folder> [1 or more]")
        return
    print("Starting generate plot script")    
    # Get the logs folder path(s)
    logs_folder = [os.path.join(os.getcwd(), sys.argv[i]) for i in range(1, len(sys.argv))]
    # Log the logs folder path(s)
    print("Logs folder(s):")
    for folder in logs_folder:
        print(folder)
    parse_compressed_data(logs_folder)

if __name__ == "__main__":
    main()