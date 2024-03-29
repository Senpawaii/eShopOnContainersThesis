import sys
import os
import re
import matplotlib.pyplot as plt
import numpy as np

max_throughput = 320

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


def plot_latency_vs_throughput(tests_data: list):
    global wrapper

    # Plot the average latency by request vs throughput
    plt.figure(figsize=(10, 5))
    # plt.title("Average latency by Request vs Throughput")
    # plt.xlabel("Débito (Funcionalidades/s)")
    plt.xlabel("Carga (funcionalidades / s)")
    plt.ylabel("Latência (ms)")
    
    # Get the maximum throughput value of all tests
    max_throughput = 0
    for test_data in tests_data:
        for throughput in test_data.keys():
            if throughput > max_throughput:
                max_throughput = throughput
    
    # Increase the granularity of the x axis
    # plt.xticks(np.arange(0, max([test.throughput for test in tests_data[0]]), 5.0))
    plt.xticks(np.arange(0, max_throughput + 5, 20.0))
    
    # Define colors to use for each test
    colors = ["-r", ":r", "-b", ":b"]
    
    # Define legend labels
    # legend_labels = ["µTCC: 1000 Versões/ Produto", "µTCC: 750 Versões/ Produto", "µTCC: 500 Versões/ Produto", "µTCC: 250 Versões/ Produto"]
    legend_labels = ["Sistema Base: Contenção Baixa", "Sistema Base: Contenção Elevada", "µTCC: Contenção Baixa", "µTCC: Contenção Elevada"]
    # legend_labels = ["Sistema Base: Sem contenção", "µTCC: Sem contenção"]

    for index, test_throughputs_latencies in enumerate(tests_data):
        # Plot the average latency by request vs throughput for each test, each with a different color

        # Sort the test data based on the x_values
        test_throughputs_latencies = dict(sorted(test_throughputs_latencies.items()))

        x_values = []
        y_values = []
        # Get the keys and values of the dictionary
        for k, v in test_throughputs_latencies.items():
            x_values.append(k)
            y_values.append(v)

        

        # Remove outliners: if the difference between the current throughput and the previous one is greater than 10, remove the current throughput and latency
        for i in range(len(x_values) - 1, 0, -1):
            differenceLat = y_values[i] - y_values[i - 1]
            if differenceLat > 30 or differenceLat < -30:
                # Check if the two previous values are also outliners
                if i > 1:
                    differenceLat = y_values[i] - y_values[i - 2]
                    if differenceLat > 30 or differenceLat < -30:
                        del x_values[i]
                        del y_values[i]
                        continue


        line_color_format = colors[index]

        # Plot the average latency by request vs throughput for each test
        line = plt.plot(x_values, y_values, line_color_format)
        legend = legend_labels[index]

        # Add a legend for the line
    plt.legend(legend_labels, loc="upper left")
    
    # Get the current path and create a folder named "plots" if it doesn't exist
    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    
    # Save the plot as a png file
    plot_name = "Latency vs Throughput"
    # Log plot_name
    plt.savefig(os.path.join(plots_folder, plot_name)+ ".pdf", format="pdf", bbox_inches="tight")


def plot_prevalence_anomalies_vs_throuhgput(tests_data: list):
    global wrapper   

    # Plot the average latency by request vs throughput
    plt.figure(figsize=(10, 5))
    plt.xlabel("Carga (funcionalidades / s)")
    plt.ylabel("% Anomalias em Operações de Leitura")
    
    # Get the maximum throughput value of all tests
    max_throughput = 0
    for test_data in tests_data:
        for throughput in test_data.keys():
            if throughput > max_throughput:
                max_throughput = throughput
    
    # Add two lists for our system anomalies detected (which are always zero) for each 20 throughput value until the max throughput value
    throughput_anomalies = {}
    for i in range(30, max_throughput, 10):
        throughput_anomalies[i] = 0
    tests_data.append(throughput_anomalies)
    tests_data.append(throughput_anomalies)


    # Increase the granularity of the x axis
    plt.xticks(np.arange(0, max_throughput + 20, 20.0))
    
    # Define colors to use for each test
    colors = ["-r", ":r", "-b", ":b"]
    
    # Define legend labels
    # legend_labels = ["µTCC: 1000 Versões/ Produto", "µTCC: 750 Versões/ Produto", "µTCC: 500 Versões/ Produto", "µTCC: 250 Versões/ Produto"]
    legend_labels = ["Sistema Base: Contenção Baixa", "Sistema Base: Contenção Elevada", "µTCC: Contenção Baixa", "µTCC: Contenção Elevada"]
    # legend_labels = ["Sistema Base: Sem contenção", "µTCC: Sem contenção"]

    for index, test_throughputs_latencies in enumerate(tests_data):
        # Plot the average latency by request vs throughput for each test, each with a different color

        # Sort the test data based on the x_values
        test_throughputs_latencies = dict(sorted(test_throughputs_latencies.items()))

        x_values = []
        y_values = []
        # Get the keys and values of the dictionary
        for k, v in test_throughputs_latencies.items():
            x_values.append(k)
            y_values.append(v)

        line_color_format = colors[index]

        # Plot the average latency by request vs throughput for each test
        line = plt.plot(x_values, y_values, line_color_format)
        legend = legend_labels[index]

        # Add a legend for the line
    plt.legend(legend_labels, loc="upper left")
    
    # Get the current path and create a folder named "plots" if it doesn't exist
    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    
    # Save the plot as a png file
    plot_name = "Anomalies vs Throughput"
    # Log plot_name
    plt.savefig(os.path.join(plots_folder, plot_name)+ ".pdf", format="pdf", bbox_inches="tight")


def clean_data(logs_folders: str):
    global wrapper
    
    all_tests_lat_throughput = [] # Hold the throughputs_latencies dictionary for each test case
    all_test_anomalies_throughput = [] # Hold the anomalies_detected dictionary for each test case

    # Read all log files in the logs folder
    for logs_folder in logs_folders:
        # Read all log files that include "Throughput" in their name
        throughput_log = []
        
        # Check if the logs folder contains "NoWrapper"
        if "NoWrap" in logs_folder:
            wrapper = False

        for file in os.listdir(logs_folder):
            if "Test" in file:
                throughput_log.append(file)
        
        # Store the data of each test case in a list of Test_data objects
        tests_data = []
        throughputs_latencies = {} # Keys: throughput, Values: average latency by request
        throughput_anomalies = {} # Keys: throughput, Values: number of anomalies detected
        for index, test_case in enumerate(throughput_log):
            test_log_path = os.path.join(logs_folder, test_case)
            with open(test_log_path, "r") as f:
                file_data = f.read()
                throughput = get_throughput(file_data)
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

                if throughput == None:
                    continue

                # Check if the throughput value is already in the throughputs dictionary
                if throughput in throughputs_latencies:
                    # If the average latency by request is lower than the one in the dictionary, update the value
                    if average_latency_by_req < throughputs_latencies[throughput]:
                        throughputs_latencies[throughput] = average_latency_by_req
                else:
                    throughputs_latencies[throughput] = average_latency_by_req
                
                if throughput in throughput_anomalies:
                    if anomalies_ratio > throughput_anomalies[throughput]:
                        throughput_anomalies[throughput] = anomalies_ratio 
                else:
                    throughput_anomalies[throughput] = anomalies_ratio

                test = Test_data(file_path, (index + 1) * 5, read_ratio, total_test_time, average_latency_by_functionality, average_latency_by_req, total_requests, total_read_requests, total_write_requests, anomalies_detected, anomalies_ratio, success_rate)
                tests_data.append(test)

                # Create a Test_data object
                
                # Check if there is already a test with the same throughput value
                # throughput_exists = False
                # for t in tests_data:
                #     if t.throughput == test.throughput:
                #         throughput_exists = True
                #         break
                # if not throughput_exists:
                #     tests_data.append(test)
        all_tests_lat_throughput.append(throughputs_latencies)
        all_test_anomalies_throughput.append(throughput_anomalies)
    
    # Enable what plot to generate
    # plot_latency_vs_throughput(all_tests_lat_throughput)
    plot_prevalence_anomalies_vs_throuhgput(all_test_anomalies_throughput)


wrapper = True

def main():
    # Read arguments
    if len(sys.argv) < 2:
        print("Usage: python GeneratePlot_UpdatePriceDiscount_Throughput.py <logs_folder> [1 or more]")
        return
    
    # Get the logs folder path(s)
    logs_folder = [os.path.join(os.getcwd(), sys.argv[i]) for i in range(1, len(sys.argv))]
    # logs_folder = os.path.join(os.getcwd(), sys.argv[1])

    # Clean data
    clean_data(logs_folder)



if __name__ == "__main__":
    main()