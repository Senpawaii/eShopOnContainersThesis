import sys
import os
import re
import matplotlib.pyplot as plt
import numpy as np

max_throughput = 420
max_throughput_plot = 420

class Test_data:
# test = Test_data(throughput, read_ratio, total_test_time, average_latency_by_functionality, average_latency_by_req, total_requests, total_read_requests, total_write_requests, anomalies_detected, anomalies_ratio, success_rate, file_path)

    def __init__(self, test_log_path: str, throughput: int, read_ratio: float, total_test_time: int, average_latency_by_functionality: float, average_latency_by_req: float, total_requests: int, total_read_requests: int, total_write_requests: int, anomalies_detected: int, anomalies_ratio: float, success_rate: float):
        self.throughput = throughput
        self.read_ratio = read_ratio
        self.total_test_time = total_test_time
        self.average_latency_by_functionality = average_latency_by_functionality
        self.average_latency_by_request = average_latency_by_req
        self.total_requests = total_requests
        self.success_rate = success_rate
        self.anomalies_detected = anomalies_detected
        self.anomalies_ratio = anomalies_ratio
        self.total_read_requests = total_read_requests
        self.total_write_requests = total_write_requests
        self.file_path = test_log_path

    def __repr__(self):
        return f"{self.name} {self.throughput} {self.price_discount}"


def get_throughput(file_data) -> int:
    # Match and extract the Throughput value
    match = re.search(r"Answered Requests throughput: ([\d.]+)", file_data)
    if match:
        # Round the throughput to the nearest integer
        return round(float(match.group(1)))
    return None


def get_read_write_ratio(file_data) -> float:
    # Match and extract the read ratio value. To get the write ratio, subtract the read ratio from 100
    match = re.search(r"Read: ([\d.]+)", file_data)
    if match:
        return float(match.group(1))
    return None


def get_total_test_time(file_data) -> int:
    # Match and extract the total_test_time value in seconds
    match = re.search(r"Total test time: ([\d.]+)", file_data)
    if match:
        return int(match.group(1))
    return None


def get_average_latency_by_functionality(file_data) -> float:
    # Match and extract the average latency per functionality in milliseconds
    match = re.search(r"Functionalities per second: ([\d.]+)", file_data)
    if match:
        functionalities_per_sec = float(match.group(1))
        latency_in_secs = 1 / functionalities_per_sec
        return latency_in_secs * 1000 # Convert to milliseconds
    return None


def get_average_latency_by_req(file_data) -> float:
    # Match and extract the average latency per request in milliseconds
    match = re.search(r"Average time/req: ([\d.]+)", file_data)
    if match:
        return float(match.group(1))
    return None


def get_total_requests(file_data) -> int:
    # Match and extract the total requests value (read + write)
    match = re.search(r"Total number of requests: ([\d.]+)", file_data)
    if match:
        return int(match.group(1))
    return None


def get_total_read_requests(file_data) -> int:
    # Match and extract the total read requests value
    match = re.search(r"Number of read operations: ([\d.]+)", file_data)
    if match:
        return int(match.group(1))
    return None


def get_total_write_requests(file_data) -> int:
    # Match and extract the total write requests value
    match = re.search(r"Number of write operations: ([\d.]+)", file_data)
    if match:
        return int(match.group(1))
    return None


def get_anomalies_detected(file_data) -> int:
    # Match and extract the anomalies detected value
    match = re.search(r"([\d.]+) Anomalies", file_data)
    if match:
        return int(match.group(1))
    return None


def get_anomalies_ratio(file_data) -> float:
    # Match and extract the anomalies ratio value
    match = re.search(r"Anomalies ratio: ([\d.]+)", file_data)
    if match:
        return float(match.group(1))
    return None


def get_success_rate(file_data) -> float:
    # Match and extract the success rate value
    match = re.search(r"Success rate: ([\d.]+)", file_data)
    if match:
        return float(match.group(1))
    return None


def plot_anomalies_vs_throughput(tests_data: list):
    global wrapper

    # Plot the average latency by request vs throughput
    plt.figure(figsize=(7, 4))
    # plt.title("Average latency by Request vs Throughput")
    plt.xlabel("Throughput (req/s)")
    plt.ylabel("% Anomaly Read Operation")
    
    # Increase the granularity of the x axis
    # plt.xticks(np.arange(0, max([test.throughput for test in tests_data[0]]), 5.0))
    plt.xticks(np.arange(0, max_throughput_plot, 20.0))
    
    # Define colors to use for each test, where the last is violet
    colors = ["-c", "--b", ":k", "-.r"]

    # Define legend labels
    legend_labels = ["Base System: Low Contention", "Base System: High Contention", "µTCC: Low Contention", "µTCC: High Contention"]

    for index, test_data in enumerate(tests_data):
        # Plot the average latency by request vs throughput for each test, each with a different color

        # Get the throughput and average latency by request values for each test
        x_values = [test.throughput for test in test_data]
        y_values = [test.anomalies_ratio for test in test_data]
        # Sort the test data based on the x_values
        x_values, y_values = zip(*sorted(zip(x_values, y_values)))

        line_color_format = colors[index]

        # Plot the average latency by request vs throughput for each test
        plt.plot(x_values, y_values, line_color_format, alpha=0.5)
        legend = legend_labels[index]

        # Add a legend for the line
    plt.legend(legend_labels, loc="upper left")
    
    # Get the current path and create a folder named "plots" if it doesn't exist
    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    
    # Save the plot as a png file
    temp = tests_data[0][0].file_path.split("/")[-1]
    log_name = tests_data[0][0].file_path.split("/")[-1].split("\\")[0]
    plot_name = log_name
    # Log plot_name
    print(tests_data[0][0].file_path)
    plt.savefig(os.path.join(plots_folder, plot_name) + ".pdf", format="pdf", bbox_inches="tight")



def clean_data(logs_folders: str):
    global wrapper
    
    all_tests_data = []

    # Read all log files in the logs folder
    for logs_folder in logs_folders:
        # Read all log files that include "Throughput" in their name
        throughput_log = []
        
        # Check if the logs folder contains "NoWrapper"
        if "NoWrap" in logs_folder:
            wrapper = False

        for file in os.listdir(logs_folder):
            if "Throughput" in file:
                throughput_log.append(file)
        
        # Store the data of each test case in a list of Test_data objects
        tests_data = []
        for test_case in throughput_log:
            test_log_path = os.path.join(logs_folder, test_case)
            with open(test_log_path, "r") as f:
                file_data = f.read()
                throughput = get_throughput(file_data)
                if throughput >= max_throughput:
                    # Stop reading the file if the throughput is greater than 140
                    continue
                read_ratio = get_read_write_ratio(file_data)
                total_test_time = get_total_test_time(file_data) # in seconds
                average_latency_by_functionality = get_average_latency_by_functionality(file_data)
                average_latency_by_req = get_average_latency_by_req(file_data) # in milliseconds
                total_requests = get_total_requests(file_data)
                total_read_requests = get_total_read_requests(file_data)
                total_write_requests = get_total_write_requests(file_data)
                anomalies_detected = get_anomalies_detected(file_data)
                anomalies_ratio = get_anomalies_ratio(file_data) # percentage of anomalies vs read requests
                success_rate = get_success_rate(file_data) # percentage of successful requests vs error requests (not anomalies)
                file_path = test_log_path

                # Create a Test_data object
                test = Test_data(file_path, throughput, read_ratio, total_test_time, average_latency_by_functionality, average_latency_by_req, total_requests, total_read_requests, total_write_requests, anomalies_detected, anomalies_ratio, success_rate)
                
                # Check if there is already a test with the same throughput value
                # throughput_exists = False
                # for t in tests_data:
                #     if t.throughput == test.throughput:
                #         throughput_exists = True
                #         break
                # if not throughput_exists:
                #     tests_data.append(test)
                tests_data.append(test)
        
        all_tests_data.append(tests_data)
    
    # Add two simulated tests_data with anomalies = 0 with throughput values equal to the ones present in the tests_data
    sample_test_data = all_tests_data[0]
    # Get the throughput values of the tests_data
    throughput_values = [test.throughput for test in sample_test_data]
    # Get the minimum and maximum throughput values
    min_throughput_test_case = min(throughput_values)
    max_throughput_test_case = max(throughput_values)
    # Create two tests_data with throughput values equal to the ones present in the tests_data
    simulated_test_data_1 = []
    simulated_test_data_2 = []
    for throughput in range(min_throughput_test_case, max_throughput_test_case, 20):
        simulated_test_data_1.append(Test_data("", throughput, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
        simulated_test_data_2.append(Test_data("", throughput, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
    simulated_test_data_1.append(Test_data("", 330, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
    simulated_test_data_2.append(Test_data("", 330, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
    all_tests_data.append(simulated_test_data_1)
    all_tests_data.append(simulated_test_data_2)

    
    # Generate plot of latency per request vs throughput
    plot_anomalies_vs_throughput(all_tests_data)


wrapper = True

def main():
    # Read arguments
    if len(sys.argv) < 2:
        print("Usage: python GeneratePlotAnomaliesVsThroughPut.py <logs_folder> [1 or more]")
        return
    
    # Get the logs folder path(s)
    logs_folder = [os.path.join(os.getcwd(), sys.argv[i]) for i in range(1, len(sys.argv))]
    # logs_folder = os.path.join(os.getcwd(), sys.argv[1])

    # Clean data
    clean_data(logs_folder)



if __name__ == "__main__":
    main()