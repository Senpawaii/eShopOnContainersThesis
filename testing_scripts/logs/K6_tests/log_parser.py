import sys
import os
import re
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

MIX_FUNCS = 1
READ_BASKET_FUNC = 2
UPDATE_DISCOUNT_PRICE_FUNC = 3


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


def plot_parsed_data(parsed_data, operation):
    colors_dict = {"BaseTCC_high": "-r", "BaseTCC_low": ":r", "µTCC_high": "-b", "µTCC_low": ":b"}
    colors_dict_READ_only = {"BaseTCC_high_ReadBasket_only": "-r", "BaseTCC_low_ReadBasket_only": ":r", "µTCC_high_ReadBasket_only": "-b", "µTCC_low_ReadBasket_only": ":b"}
    default_legend_labels = {"BaseTCC_high": "Sistema Base: Contenção Elevada", "BaseTCC_low": "Sistema Base: Contenção Baixa", "µTCC_high": "µTCC: Contenção Elevada", "µTCC_low": "µTCC: Contenção Baixa"}
    default_legend_labels_READ_only = {"BaseTCC_high_ReadBasket_only": "Sistema Base: Contenção Elevada", "BaseTCC_low_ReadBasket_only": "Sistema Base: Contenção Baixa", "µTCC_high_ReadBasket_only": "µTCC: Contenção Elevada", "µTCC_low_ReadBasket_only": "µTCC: Contenção Baixa"}
    
    legend_labels = []
    # Plot the 95th percentile latency by request vs throughput
    plt.figure(figsize=(10, 5))
    plt.xlabel("Carga (funcionalidades / s)")
    plt.ylabel("Latência (Percentil 95)")

    max_throughput = 0
    for system_cont in parsed_data:
        for test in parsed_data[system_cont]:
            if int(test) > max_throughput:
                max_throughput = int(test)

    # Increase the granularity of the x axis
    plt.xticks(np.arange(0, max_throughput + 20, 40.0))

    for system_plus_cont in parsed_data:
        # Get the throughput and latency values for each test
        y_values = []
        x_values = []
        for test in parsed_data[system_plus_cont]:
            y_values.append(parsed_data[system_plus_cont][test]["latency"]["p(95)"])
            x_values.append(int(test))

        xy = pd.DataFrame({'x': x_values, 'y': y_values})
        xy.sort_values('x', inplace=True)
        # Plot the 95th percentile latency by request vs throughput for each test
        if operation == READ_BASKET_FUNC:
            line_color_format = colors_dict_READ_only[system_plus_cont]
            legend_labels.append(default_legend_labels_READ_only[system_plus_cont])
        else:
            line_color_format = colors_dict[system_plus_cont]
            legend_labels.append(default_legend_labels[system_plus_cont])

        plt.plot(xy['x'], xy['y'], line_color_format)

    
    plt.legend(legend_labels, loc="upper left")
    # Get the current path and create a folder named "plots" if it doesn't exist
    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "Thesis_results", "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    
    # Save the plot as a png file
    if operation == READ_BASKET_FUNC:
        plt.savefig(os.path.join(plots_folder, f"Latency (95th percentile) vs Throughput [Read Basket only]")+ ".pdf", format="pdf", bbox_inches="tight")
    else:
        plt.savefig(os.path.join(plots_folder, f"Latency (95th percentile) vs Throughput")+ ".pdf", format="pdf", bbox_inches="tight")


def calculate_penalty(parsed_data, operation):
    low_cont_penalty_ratio = 0
    high_cont_penalty_ratio = 0

    if operation == READ_BASKET_FUNC:
        baseTCC_low_cont = parsed_data["BaseTCC_low_ReadBasket_only"]
        baseTCC_high_cont = parsed_data["BaseTCC_high_ReadBasket_only"]
        microTCC_low_cont = parsed_data["µTCC_low_ReadBasket_only"]
        microTCC_high_cont = parsed_data["µTCC_high_ReadBasket_only"]
    else:
        baseTCC_low_cont = parsed_data["BaseTCC_low"]
        baseTCC_high_cont = parsed_data["BaseTCC_high"]
        microTCC_low_cont = parsed_data["µTCC_low"]
        microTCC_high_cont = parsed_data["µTCC_high"]

    baseTCC_low_cont_sorted_keys = sorted(baseTCC_low_cont, key=lambda x: int(x))
    baseTCC_high_cont_sorted_keys = sorted(baseTCC_high_cont, key=lambda x: int(x))
    microTCC_low_cont_sorted_keys = sorted(microTCC_low_cont, key=lambda x: int(x))
    microTCC_high_cont_sorted_keys = sorted(microTCC_high_cont, key=lambda x: int(x))

    average_baseTCC_low_latency = 0
    average_baseTCC_high_latency = 0
    average_microTCC_low_latency = 0
    average_microTCC_high_latency = 0

    for index, test_key in enumerate(baseTCC_low_cont_sorted_keys):
        test_results = baseTCC_low_cont[test_key]
        # if (index in [0, 1, len(baseTCC_low_cont_sorted_keys) - 1, len(baseTCC_low_cont_sorted_keys) - 2]): # Skip the first and last tests
            # continue
        average_baseTCC_low_latency += test_results["latency"]["p(95)"]
    average_baseTCC_low_latency = average_baseTCC_low_latency / (len(baseTCC_low_cont))

    for index, test_key in enumerate(baseTCC_high_cont_sorted_keys):
        test_results = baseTCC_high_cont[test_key]
        # if (index in [0, 1, len(baseTCC_high_cont) - 1, len(baseTCC_high_cont) - 2]): # Skip the first and last tests
            # continue
        average_baseTCC_high_latency += test_results["latency"]["p(95)"]
    average_baseTCC_high_latency = average_baseTCC_high_latency / (len(baseTCC_high_cont))

    for index, test_key in enumerate(microTCC_low_cont_sorted_keys):
        test_results = microTCC_low_cont[test_key]
        # if (index in [0, 1, len(microTCC_low_cont) - 1, len(microTCC_low_cont) - 2]):
            # continue
        average_microTCC_low_latency += test_results["latency"]["p(95)"]
    average_microTCC_low_latency = average_microTCC_low_latency / (len(microTCC_low_cont))

    for index, test_key in enumerate(microTCC_high_cont_sorted_keys):
        test_results = microTCC_high_cont[test_key]
        # if (index in [0, 1, len(microTCC_high_cont) - 1, len(microTCC_high_cont) - 2]):
            # continue
        average_microTCC_high_latency += test_results["latency"]["p(95)"]
    average_microTCC_high_latency = average_microTCC_high_latency / (len(microTCC_high_cont))

    low_cont_penalty_ration = average_microTCC_low_latency / average_baseTCC_low_latency * 100 # In percentage, the higher the worse
    high_cont_penalty_ration = average_microTCC_high_latency / average_baseTCC_high_latency * 100 # In percentage, the higher the worse

    print("Average latency for BaseTCC with low contention: " + str(average_baseTCC_low_latency))
    print("Average latency for BaseTCC with high contention: " + str(average_baseTCC_high_latency))
    print("Average latency for µTCC with low contention: " + str(average_microTCC_low_latency))
    print("Average latency for µTCC with high contention: " + str(average_microTCC_high_latency))
    print("Penalty ratio for low contention: " + str(low_cont_penalty_ration - 100))
    print("Penalty ratio for high contention: " + str(high_cont_penalty_ration - 100))


def parse_data(log_file: list[str]) -> dict:
    """
    Parse the data from a log file and return a dictionary with the parsed data.
    The dictionary includes: 
        - Latency
            - Minimum
            - Maximum
            - Average
            - Median
            - 90th percentile
            - 95th percentile
    """
    
    parsed_data = {}
    parsed_data["latency"] = {}
    parsed_data["latency"]["min"] = 0
    parsed_data["latency"]["max"] = 0
    parsed_data["latency"]["avg"] = 0
    parsed_data["latency"]["med"] = 0
    parsed_data["latency"]["p(90)"] = 0
    parsed_data["latency"]["p(95)"] = 0

    date_latency_ms_pairs = []
    # Start parsing the data, line by line
    for line in log_file:
        date_and_latency = re.findall(r'Date: ([\d]+) \w+ operation duration: ([\d]+)', line)
        if date_and_latency:
            date_ms = int(date_and_latency[0][0])
            operation_duration = int(date_and_latency[0][1])

            date_latency_ms_pair = (date_ms, operation_duration)
            date_latency_ms_pairs.append(date_latency_ms_pair)
    
    # Sort the list of tuples by the first element of the tuple (date)
    date_latency_ms_pairs.sort(key=lambda tup: tup[0])

    # Identify the pairs from the initial 10 seconds (warmup period)
    initial_date_ms = date_latency_ms_pairs[0][0]
    frontier_index_to_remove_up_to = 0
    for index, pair in enumerate(date_latency_ms_pairs):
        if pair[0] - initial_date_ms < 5000:
            frontier_index_to_remove_up_to = index
    
    # Remove the initial pairs from the list
    date_latency_ms_pairs = date_latency_ms_pairs[frontier_index_to_remove_up_to + 1:]
        
    # Identify the pairs from the final 10 seconds (teardown period)
    final_date_ms = date_latency_ms_pairs[-1][0]
    frontier_index_to_remove_up_to = 0
    for index, pair in enumerate(reversed(date_latency_ms_pairs)):
        if final_date_ms - pair[0] < 5000:
            frontier_index_to_remove_up_to = index
    
    # Remove the final pairs from the list
    date_latency_ms_pairs = date_latency_ms_pairs[:-frontier_index_to_remove_up_to - 1]

    # Calculate the latency values
    parsed_data["latency"]["min"] = min(date_latency_ms_pairs, key=lambda tup: tup[1])[1]
    parsed_data["latency"]["max"] = max(date_latency_ms_pairs, key=lambda tup: tup[1])[1]
    parsed_data["latency"]["avg"] = sum([pair[1] for pair in date_latency_ms_pairs]) / len(date_latency_ms_pairs)
    parsed_data["latency"]["med"] = np.median([pair[1] for pair in date_latency_ms_pairs])
    parsed_data["latency"]["p(90)"] = np.percentile([pair[1] for pair in date_latency_ms_pairs], 90)
    parsed_data["latency"]["p(95)"] = np.percentile([pair[1] for pair in date_latency_ms_pairs], 95)
     
    return parsed_data

def parse_data_old(log_file: list[str]) -> dict:
    """
    Parse the data from a log file and return a dictionary with the parsed data.
    The dictionary includes: 
        - Latency
            - Minimum
            - Maximum
            - Average
            - Median
            - 90th percentile
            - 95th percentile
        - Anomalies ratio
    """
    parsed_data = {}
    parsed_data["latency"] = {}
    parsed_data["latency"]["min"] = 0
    parsed_data["latency"]["max"] = 0
    parsed_data["latency"]["avg"] = 0
    parsed_data["latency"]["med"] = 0
    parsed_data["latency"]["p(90)"] = 0
    parsed_data["latency"]["p(95)"] = 0
    parsed_data["anomalies"] = 0

    # Start parsing the data, line by line
    for line in log_file:
        # Apply all the regexes to the line
        temp_latency_min = re.findall(r'http_req_duration.*min=([\d+]+[.\d+]*)(s?)', line)
        temp_latency_max = re.findall(r'http_req_duration.*max=([\d+]+[.\d+]*)(s?)', line)
        temp_latency_avg = re.findall(r'http_req_duration.*avg=([\d+]+[.\d+]*)(s?)', line)
        temp_latency_med = re.findall(r'http_req_duration.*med=([\d+]+[.\d+]*)(s?)', line)
        temp_latency_p90 = re.findall(r'http_req_duration.*p\(90\)=([\d+]+[.\d+]*)(s?)', line)
        temp_latency_p95 = re.findall(r'http_req_duration.*p\(95\)=([\d+]+[.\d+]*)(s?)', line)
        temp_anomalies = re.findall(r'↳\s*[\d+]+%', line)

        # If the line contains latency data, add it to the parsed data
        if temp_latency_min:
            if temp_latency_min[0][1] == "s":
                parsed_data["latency"]["min"] = float(temp_latency_min[0][0]) * 1000
            else:
                parsed_data["latency"]["min"] = float(temp_latency_min[0][0])
        if temp_latency_max:
            if temp_latency_max[0][1] == "s":
                parsed_data["latency"]["max"] = float(temp_latency_max[0][0]) * 1000
            else:
                parsed_data["latency"]["max"] = float(temp_latency_max[0][0])
        if temp_latency_avg:
            if temp_latency_avg[0][1] == "s":
                parsed_data["latency"]["avg"] = float(temp_latency_avg[0][0]) * 1000
            else:
                parsed_data["latency"]["avg"] = float(temp_latency_avg[0][0])
        if temp_latency_med:
            if temp_latency_med[0][1] == "s":
                parsed_data["latency"]["med"] = float(temp_latency_med[0][0]) * 1000
            else:
                parsed_data["latency"]["med"] = float(temp_latency_med[0][0])
        if temp_latency_p90:
            if temp_latency_p90[0][1] == "s":
                parsed_data["latency"]["p(90)"] = float(temp_latency_p90[0][0]) * 1000
            else:
                parsed_data["latency"]["p(90)"] = float(temp_latency_p90[0][0])
        if temp_latency_p95:
            if temp_latency_p95[0][1] == "s":
                parsed_data["latency"]["p(95)"] = float(temp_latency_p95[0][0]) * 1000 # Convert to ms
            else:
                parsed_data["latency"]["p(95)"] = float(temp_latency_p95[0][0])
        if temp_anomalies:
            parsed_data["anomalies"] = float(temp_anomalies[0].split("%")[0].split(" ")[-1])
    return parsed_data



def CollectTestDatafiles(operation: int):
    """ Search the log directory and return a dictionary of parsed log files. 
        Each key in the dictionary contains the parsed data from that system + system type. 
    """
    results_dict = {}

    current_path = os.getcwd()
    print("Current path: " + current_path)
    test_logs_path = os.path.join(current_path, "testing_scripts", "logs", "K6_tests", "Thesis_results")
    print("Test logs path: " + test_logs_path)
    
    
    tested_systems = os.listdir(test_logs_path)
    for system in tested_systems:
        if system != "BaseTCC" and system != "µTCC":
            continue
        system_path = os.path.join(test_logs_path, system)
        system_contention = os.listdir(system_path)
        for contention in system_contention:
            if (operation == MIX_FUNCS and contention != "high" and contention != "low"):
                continue
            elif (operation == READ_BASKET_FUNC and contention != "high_ReadBasket_only" and contention != "low_ReadBasket_only"):
                continue
            elif (operation == UPDATE_DISCOUNT_PRICE_FUNC and contention != "high_UpdateDiscountPrice_only" and contention != "low_UpdateDiscountPrice_only"):
                continue
            
            log_files_data = {}
            type_path = os.path.join(system_path, contention)
            log_files = os.listdir(type_path)
            for log_file in log_files:
                vus = log_file.split(".")[0] # Get the number of VUs from the log file name
                log_file_path = os.path.join(type_path, log_file)
                if not os.path.isfile(log_file_path):
                    print("The file " + log_file_path + " does not exist/ is not a file.")
                    continue
                # open the file and parse the data
                with open(log_file_path, "r") as f:
                    file_lines = f.readlines() # Get all the lines of the file
                    parsed_data = parse_data(file_lines)
                    log_files_data[vus] = parsed_data
            results_dict[system + "_" + contention] = log_files_data
    return results_dict


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
    # mix_parsed_data = CollectTestDatafiles(MIX_FUNCS)
    read_basket_parsed_data = CollectTestDatafiles(READ_BASKET_FUNC)
    # update_discount_price_parsed_data = CollectTestDatafiles(UPDATE_DISCOUNT_PRICE_FUNC)

    plot_parsed_data(read_basket_parsed_data, READ_BASKET_FUNC)
    calculate_penalty(read_basket_parsed_data, READ_BASKET_FUNC)
    


if __name__ == '__main__':
    main()