import sys
import os
import re
import matplotlib.pyplot as plt
import numpy as np


def match_throughput(lines):
    # Match the 95th percentile

    # Get the throughput values
    throughput_values = []
    latency_values = []
    for line in lines:
        temp_throughput = re.findall(r"Test \d+", line)
        temp_latency = re.findall(r'http_req_duration.*p\(95\)=([\d+]+[.\d+]*)', line)
        if temp_throughput:
            throughput_values.append(int(temp_throughput[0].split(" ")[1]))
        if temp_latency:
            latency_values.append(float(temp_latency[0]))
    
    return latency_values, throughput_values


def match_anomalies_throughput(lines, log_file):
    # Match the anomaly ratio percentage

    # Get the throughput values
    throughput_values = []
    anomalies_values = []
    coherentMatching = False
    tccTest = True if "MicroTCC" in log_file else False
    for line in lines:
        temp_throughput = re.findall(r"Test \d+", line)
        if temp_throughput:
            throughput_values.append(int(temp_throughput[0].split(" ")[1]))
            continue

        if coherentMatching == True:
            temp_anomaly_coherent = re.findall(r'↳\s*[\d+]+%', line)
            anomalies_values.append(100 - int(temp_anomaly_coherent[0].split("%")[0].split(" ")[-1]))
            coherentMatching = False
        else:
            temp_anomaly_coherent = re.findall(r'✗ is price coherent', line)
            if temp_anomaly_coherent:
                if tccTest:
                    anomalies_values.append(0)
                    continue   
                coherentMatching = True
            else:
                temp_coherent = re.findall(r'✓ is price coherent', line)
                if temp_coherent:
                    anomalies_values.append(0)


    return anomalies_values, throughput_values


def clean_data(log_files):


    # Read all the log files
    # plot_latency_throughput(log_files, log_latencies, log_throughputs)

    plot_anomalies_throughput(log_files)


def plot_anomalies_throughput(log_files):
    log_anomalies = []
    log_throughputs = []
    for log_file in log_files:
        with open(log_file, "r") as f:
            lines = f.readlines()
            anomalies, throughputs = match_anomalies_throughput(lines, log_file)

            cwd = os.getcwd() + "/testing_scripts/logs/K6_tests/parsed_logs"
            log_file_name = os.path.basename(log_file) + "_anomalies"

            # Store the throughput values in a file in a "parsed logs" folder
            with open(os.path.join(cwd, log_file_name), "w") as output_file:
                for index, value in enumerate(throughputs):
                    output_file.write("Test "+ str(value) + ": anomalies ratio=" + str(anomalies[index]) + "\n")
            log_anomalies.append(anomalies)
            log_throughputs.append(throughputs)
    plot_name = "Anomalies Ratio vs Throughput"
    y_axis_label = "Taxa de Anomalias (%)"
    plot_data(log_anomalies, log_throughputs, plot_name, y_axis_label)



def plot_latency_throughput(log_files):
    log_latencies = []
    log_throughputs = []
    for log_file in log_files:
        with open(log_file, "r") as f:
            lines = f.readlines()
            latencies, throughputs = match_throughput(lines)

            cwd = os.getcwd() + "/testing_scripts/logs/K6_tests/parsed_logs"
            log_file_name = os.path.basename(log_file)

            # Store the throughput values in a file in a "parsed logs" folder
            with open(os.path.join(cwd, log_file_name), "w") as output_file:
                for index, value in enumerate(throughputs):
                    output_file.write("Test "+ str(value) + ": p95=" + str(latencies[index]) + "\n")
            log_latencies.append(latencies)
            log_throughputs.append(throughputs)
    plot_name = "Latency (95th percentile) vs Throughput"
    y_axis_label = "Latência (Percentil 95)"
    plot_data(log_latencies, log_throughputs, plot_name, y_axis_label)


def plot_data(log_latencies, log_throughputs, plot_name, y_axis_label):
    # Plot the average latency by request vs throughput
    plt.figure(figsize=(10, 5))
    plt.xlabel("Carga (funcionalidades / s)")
    plt.ylabel(y_axis_label)
    
    # Get the maximum throughput value of all tests
    max_throughput = 0
    for test_throughputs in log_throughputs:
        for throughput_value in test_throughputs:
            if throughput_value > max_throughput:
                max_throughput = throughput_value
    
    # Increase the granularity of the x axis
    plt.xticks(np.arange(0, max_throughput + 20, 40.0))

    if "Anomalies" in plot_name:
        plt.yticks(np.arange(0, 100, 10.0))
    else:
        plt.yticks(np.arange(0, 1000, 100.0))

    # Define colors to use for each test
    colors = ["-r", ":r", "-b", ":b"]
    
    # Define legend labels
    # legend_labels = ["µTCC: 1000 Versões/ Produto", "µTCC: 750 Versões/ Produto", "µTCC: 500 Versões/ Produto", "µTCC: 250 Versões/ Produto"]
    legend_labels = ["Sistema Base: Contenção Elevada", "Sistema Base: Contenção Baixa", "µTCC: Contenção Elevada", "µTCC: Contenção Baixa"]
    # legend_labels = ["Sistema Base: Sem contenção", "µTCC: Sem contenção"]

    # Get the throughput and latency values for each test
    for index in range(len(log_throughputs)):
        y_values = log_latencies[index]
        x_values = log_throughputs[index]

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
    plt.savefig(os.path.join(plots_folder, plot_name)+ ".pdf", format="pdf", bbox_inches="tight")


def CollectTestDatafiles():
    """ Search the log directory and return a dictionary of parsed log files. 
        Each key in the dictionary contains the parsed data from that system + system type. 
    """
    # eShopOnContainersThesis/testing_scripts/logs/K6_tests/Thesis_results
    current_path = os.getcwd()
    print("Current path: " + current_path)
    test_logs_path = os.path.join(current_path, "testing_scripts", "logs", "K6_tests", "Thesis_results")
    print("Test logs path: " + test_logs_path)







def main():
    # Read arguments
    if len(sys.argv) < 2:
        print("Usage: python log_parser.py <type of aggregator: minimum=measure/ average=all>")
        return
    
    test_selector_criteria = sys.argv[1]
    if not re.match(r"minimum=\w|average=\w", test_selector_criteria):
        print("The test selector criteria must be in the format minimum=measure or average=all")
        return
    
    log_files_criteria = test_selector_criteria.split("=")[0]
    log_files_measure = test_selector_criteria.split("=")[1]
    
    # Check that the criteria is either "minimum" or "average"
    if log_files_criteria == "minimum":
        if log_files_measure not in ["avg", "min", "max", "med", "p(90)", "p(95)"]:
            print("The test selector criteria must be one of the following: avg, min, max, med, p(90), p(95)")
            return   

    # Get the test data by system and test type
    parsed_data = CollectTestDatafiles()
    


if __name__ == '__main__':
    main()