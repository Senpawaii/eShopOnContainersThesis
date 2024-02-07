import sys
import os
import re
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

MIX_FUNCS = 1
READ_BASKET_FUNC = 2
UPDATE_DISCOUNT_PRICE_FUNC = 3
VERSION_TESTING_LOW = 4
VERSION_TESTING_HIGH = 5
MIX_FUNCS_VERSIONS_HIGH = 6
MIX_FUNCS_VERSIONS_LOW = 7
MEMORY_TEST_LOW = 8

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

def plot_READ_only_data(parsed_data, versions, measure):
    default_colors_dict_READ_only = {"BaseTCC_high_ReadBasket_only": "#FF7742", 
                                     "BaseTCC_low_ReadBasket_only": "#3D48AD", 
                                     "µTCC_high_ReadBasket_only": "#B45FA5", 
                                     "µTCC_low_ReadBasket_only": "#67903D"}

    default_legend_labels_READ_only = {"BaseTCC_high_ReadBasket_only": "Base System: High Contention", 
                                         "BaseTCC_low_ReadBasket_only": "Base System: Low Contention", 
                                         "µTCC_high_ReadBasket_only": "µTCC: High Contention", 
                                         "µTCC_low_ReadBasket_only": "µTCC: Low Contention"}
    
    default_markers_READ_only = {"BaseTCC_high_ReadBasket_only": "o", "BaseTCC_low_ReadBasket_only": "v", "µTCC_high_ReadBasket_only": "s", "µTCC_low_ReadBasket_only": "D"}
    default_linestyle_READ_only = {"BaseTCC_high_ReadBasket_only": "-", "BaseTCC_low_ReadBasket_only": "--", "µTCC_high_ReadBasket_only": "-.", "µTCC_low_ReadBasket_only": ":"}

    legend_labels = []
    plt.figure(figsize=(10, 5))
    plt.xlabel("Functionalities / s")

    if(measure == "p(95)"):
        # Plot the 95th percentile latency by request vs throughput
        plt.ylabel("Latency (95th percentile)")
    elif(measure == "med"):
        # Plot the median latency by request vs throughput
        plt.ylabel("Latência (Mediana)")

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
            if(measure == "p(95)"):
                y_values.append(parsed_data[system_plus_cont][test]["latency"]["p(95)"])
            elif(measure == "med"):
                y_values.append(parsed_data[system_plus_cont][test]["latency"]["med"])
            x_values.append(int(test))

        xy = pd.DataFrame({'x': x_values, 'y': y_values})
        xy.sort_values('x', inplace=True)
        # Plot the 95th percentile latency by request vs throughput for each test
        legend_labels.append(default_legend_labels_READ_only[system_plus_cont])
        plt.plot(xy['x'], xy['y'],
                    marker = default_markers_READ_only[system_plus_cont], 
                    color=default_colors_dict_READ_only[system_plus_cont],
                    linestyle=default_linestyle_READ_only[system_plus_cont])
        
        # Set y min to 0
        plt.ylim(bottom=0, top=90)
        
        
    plt.legend(legend_labels, loc="upper left")
    # Get the current path and create a folder named "plots" if it doesn't exist
    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "Thesis_results", "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    
    plot_names = {"p(95)": "Latency (95th percentile) vs Throughput", "med": "Latency (Median) vs Throughput"}
    plt.savefig(os.path.join(plots_folder, f"{plot_names[measure]} [Read Basket only] [" + str(versions) + " versions]")+ ".pdf", format="pdf", bbox_inches="tight")



def plot_UPDATE_only_data(parsed_data, versions, measure):
    default_colors_dict_UPDATE_only = {"BaseTCC_high_UpdatePriceDiscount_only": "#FF7742", 
                                       "BaseTCC_low_UpdatePriceDiscount_only": "#3D48AD", 
                                       "µTCC_high_UpdatePriceDiscount_only": "#B45FA5", 
                                       "µTCC_low_UpdatePriceDiscount_only": "#67903D"}
    
    default_legend_labels_UPDATE_only = {"BaseTCC_high_UpdatePriceDiscount_only": "Base System: High Contention", 
                                         "BaseTCC_low_UpdatePriceDiscount_only": "Base System: Low Contention", 
                                         "µTCC_high_UpdatePriceDiscount_only": "µTCC: High Contention", 
                                         "µTCC_low_UpdatePriceDiscount_only": "µTCC: Low Contention"}
    
    default_markers_UPDATE_only = {"BaseTCC_high_UpdatePriceDiscount_only": "o", "BaseTCC_low_UpdatePriceDiscount_only": "v", "µTCC_high_UpdatePriceDiscount_only": "s", "µTCC_low_UpdatePriceDiscount_only": "D"}
    default_linestyle_UPDATE_only = {"BaseTCC_high_UpdatePriceDiscount_only": "-", "BaseTCC_low_UpdatePriceDiscount_only": "--", "µTCC_high_UpdatePriceDiscount_only": "-.", "µTCC_low_UpdatePriceDiscount_only": ":"}

    legend_labels = []
    plt.figure(figsize=(10, 5))
    plt.xlabel("Functionalities / s")

    if(measure == "p(95)"):
        # Plot the 95th percentile latency by request vs throughput
        plt.ylabel("Latency (95th percentile)")
    elif(measure == "med"):
        # Plot the median latency by request vs throughput
        plt.ylabel("Latência (Mediana)")

    max_throughput = 0
    for system_cont in parsed_data:
        for test in parsed_data[system_cont]:
            if int(test) > max_throughput:
                max_throughput = int(test)

     # Increase the granularity of the x axis
    plt.xticks(np.arange(0, max_throughput + 20, 20.0))

    for system_plus_cont in parsed_data:
        # Get the throughput and latency values for each test
        y_values = []
        x_values = []
        for test in parsed_data[system_plus_cont]:
            if(measure == "p(95)"):
                y_values.append(parsed_data[system_plus_cont][test]["latency"]["p(95)"])
            elif(measure == "med"):
                y_values.append(parsed_data[system_plus_cont][test]["latency"]["med"])
            x_values.append(int(test))

        xy = pd.DataFrame({'x': x_values, 'y': y_values})
        xy.sort_values('x', inplace=True)
        # Plot the 95th percentile latency by request vs throughput for each test
        legend_labels.append(default_legend_labels_UPDATE_only[system_plus_cont])
        plt.plot(xy['x'], xy['y'],
                    marker = default_markers_UPDATE_only[system_plus_cont], 
                    color=default_colors_dict_UPDATE_only[system_plus_cont],
                    linestyle=default_linestyle_UPDATE_only[system_plus_cont])
        
    plt.legend(legend_labels, loc="upper left")
    # Get the current path and create a folder named "plots" if it doesn't exist
    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "Thesis_results", "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    
    plot_names = {"p(95)": "Latency (95th percentile) vs Throughput", "med": "Latency (Median) vs Throughput"}
    plt.savefig(os.path.join(plots_folder, f"{plot_names[measure]} [Update Discount Price only] [" + str(versions) + " versions]")+ ".pdf", format="pdf", bbox_inches="tight")

def plot_memory_data(parsed_data, log_files_measure):
    width = 0.75

    # Generate Catalog Plot
    fig, ax = plt.subplots(figsize=(10, 5))

    # Get the averages for each vu associated with the Catalog for both systems
    vus = ("40", "80", "120", "160", "200", "240", "280", "320", "360", "400", "440", "480", "520", "560", "600", "640")
    vu_averages_µTCC = [parsed_data["µTCC"]["catalog"][vu]["average"] for vu in vus]
    vu_averages_base_system = [parsed_data["BaseTCC"]["catalog"][vu]["average"] for vu in vus]
    
    vu_averages_μTCC_difference = [vu_averages_µTCC[i] - vu_averages_base_system[i] for i in range(len(vu_averages_µTCC))]
    
    # for index, vu in enumerate(vus):
    #     print("Catalog vu: "+ str(vu) + "µTCC: " + str(vu_averages_µTCC[index]) + " - Base System: " + str(vu_averages_base_system[index]) + " = " + str(vu_averages_µTCC_difference[index]))
    
    # Calculate the ratio percentage for each vu
    vu_ratio = [vu_averages_µTCC[i] / vu_averages_base_system[i] for i in range(len(vu_averages_µTCC))]
    for index, vu in enumerate(vus):
        # print the ratio percentages
        print("Catalog vu: "+ str(vu) + "µTCC: " + str(vu_averages_µTCC[index]) + " / Base System: " + str(vu_averages_base_system[index]) + " = " + str(vu_ratio[index] * 100) + "%")


    weight_counts = {
        "Base System": np.array(vu_averages_base_system), # averages for each vu associated with the Catalog for the Base System
        "µTCC": np.array(vu_averages_μTCC_difference) # averages for each vu associated with the Catalog for µTCC
    }

    bottom_catalog = np.zeros(len(vus))

    for lab, average in weight_counts.items():
        ax.bar(vus, average, width, label=lab, bottom=bottom_catalog)
        bottom_catalog += average

    ax.legend(loc="upper left")
    ax.set_xlabel("Functionalities / s")
    ax.set_ylabel("Memory Usage (MiB)")

    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "Thesis_results", "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    

    # Save the plot as a png file
    plt.savefig(os.path.join(plots_folder, f"Catalog Service [Mix Functionalities] [Memory Usage] [Low Contention]")+ ".pdf", format="pdf", bbox_inches="tight")


    # Generate discount Plot
    fig, ax = plt.subplots(figsize=(10, 5))

    # Get the averages for each vu associated with the discount for both systems
    vus = ("40", "80", "120", "160", "200", "240", "280", "320", "360", "400", "440", "480", "520", "560", "600", "640")
    vu_averages_µTCC = [parsed_data["µTCC"]["discount"][vu]["average"] for vu in vus]
    vu_averages_base_system = [parsed_data["BaseTCC"]["discount"][vu]["average"] for vu in vus]
    
    vu_averages_μTCC_difference = [vu_averages_µTCC[i] - vu_averages_base_system[i] for i in range(len(vu_averages_µTCC))]
     # Calculate the ratio percentage for each vu
    vu_ratio = [vu_averages_µTCC[i] / vu_averages_base_system[i] for i in range(len(vu_averages_µTCC))]
    for index, vu in enumerate(vus):
        # print the ratio percentages
        print("Discount vu: "+ str(vu) + "µTCC: " + str(vu_averages_µTCC[index]) + " / Base System: " + str(vu_averages_base_system[index]) + " = " + str(vu_ratio[index] * 100) + "%")

    # for index, vu in enumerate(vus):
    #     print("discount vu: "+ str(vu) + "µTCC: " + str(vu_averages_µTCC[index]) + " - Base System: " + str(vu_averages_base_system[index]) + " = " + str(vu_averages_µTCC_difference[index]))
    
    weight_counts = {
        "Base System": np.array(vu_averages_base_system), # averages for each vu associated with the discount for the Base System
        "µTCC": np.array(vu_averages_μTCC_difference) # averages for each vu associated with the discount for µTCC
    }

    bottom_discount = np.zeros(len(vus))

    for lab, average in weight_counts.items():
        ax.bar(vus, average, width, label=lab, bottom=bottom_discount)
        bottom_discount += average

    ax.legend(loc="upper left")
    ax.set_xlabel("Functionalities / s")
    ax.set_ylabel("Memory Usage (MiB)")
    
    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "Thesis_results", "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)

    # Save the plot as a png file
    plt.savefig(os.path.join(plots_folder, f"Discount Service [Mix Functionalities] [Memory Usage] [Low Contention]")+ ".pdf", format="pdf", bbox_inches="tight")

    # Generate basket Plot
    fig, ax = plt.subplots(figsize=(10, 5))

    # Get the averages for each vu associated with the basket for both systems
    vus = ("40", "80", "120", "160", "200", "240", "280", "320", "360", "400", "440", "480", "520", "560", "600", "640")
    vu_averages_µTCC = [parsed_data["µTCC"]["basket"][vu]["average"] for vu in vus]
    vu_averages_base_system = [parsed_data["BaseTCC"]["basket"][vu]["average"] for vu in vus]
    
    vu_averages_μTCC_difference = [vu_averages_µTCC[i] - vu_averages_base_system[i] for i in range(len(vu_averages_µTCC))]
    vu_ratio = [vu_averages_µTCC[i] / vu_averages_base_system[i] for i in range(len(vu_averages_µTCC))]
    for index, vu in enumerate(vus):
        # print the ratio percentages
        print("Basket vu: "+ str(vu) + "µTCC: " + str(vu_averages_µTCC[index]) + " / Base System: " + str(vu_averages_base_system[index]) + " = " + str(vu_ratio[index] * 100) + "%")

    # for index, vu in enumerate(vus):
    #     print("basket vu: "+ str(vu) + "µTCC: " + str(vu_averages_µTCC[index]) + " - Base System: " + str(vu_averages_base_system[index]) + " = " + str(vu_averages_µTCC_difference[index]))
    
    weight_counts = {
        "Base System": np.array(vu_averages_base_system), # averages for each vu associated with the basket for the Base System
        "µTCC": np.array(vu_averages_μTCC_difference) # averages for each vu associated with the basket for µTCC
    }

    bottom_basket = np.zeros(len(vus))

    for lab, average in weight_counts.items():
        ax.bar(vus, average, width, label=lab, bottom=bottom_basket)
        bottom_basket += average

    ax.legend(loc="upper left")
    ax.set_xlabel("Functionalities / s")
    ax.set_ylabel("Memory Usage (MiB)")

    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "Thesis_results", "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    
    # Save the plot as a png file
    plt.savefig(os.path.join(plots_folder, f"Basket Service [Mix Functionalities] [Memory Usage] [Low Contention]")+ ".pdf", format="pdf", bbox_inches="tight")





def plot_version_MIX_data(parsed_data, measure, operation):
    default_colors_dict_high = {"µTCC_high_5_versions_UpdatePriceDiscountReadBasket": "#FF7742", 
                           "µTCC_high_10_versions_UpdatePriceDiscountReadBasket": "#3D48AD", 
                           "µTCC_high_25_versions_UpdatePriceDiscountReadBasket": "#B45FA5", 
                           "µTCC_high_40_versions_UpdatePriceDiscountReadBasket": "#67903D"}
    
    default_legend_labels_high = {"µTCC_high_5_versions_UpdatePriceDiscountReadBasket": "µTCC: 5 Versions", 
                             "µTCC_high_10_versions_UpdatePriceDiscountReadBasket": "µTCC: 10 Versions", 
                             "µTCC_high_25_versions_UpdatePriceDiscountReadBasket": "µTCC: 25 Versions", 
                             "µTCC_high_40_versions_UpdatePriceDiscountReadBasket": "µTCC: 40 Versions"}

    default_markers_MIX_high = {"µTCC_high_5_versions_UpdatePriceDiscountReadBasket": "o", 
                           "µTCC_high_10_versions_UpdatePriceDiscountReadBasket": "v", 
                           "µTCC_high_25_versions_UpdatePriceDiscountReadBasket": "s", 
                           "µTCC_high_40_versions_UpdatePriceDiscountReadBasket": "D"}
    
    default_linestyle_MIX_high = {"µTCC_high_5_versions_UpdatePriceDiscountReadBasket": "-", 
                             "µTCC_high_10_versions_UpdatePriceDiscountReadBasket": "--", 
                             "µTCC_high_25_versions_UpdatePriceDiscountReadBasket": "-.", 
                             "µTCC_high_40_versions_UpdatePriceDiscountReadBasket": ":"}
    
    default_colors_dict_low = {"µTCC_low_5_versions_UpdatePriceDiscountReadBasket": "#FF7742", 
                           "µTCC_low_10_versions_UpdatePriceDiscountReadBasket": "#3D48AD", 
                           "µTCC_low_25_versions_UpdatePriceDiscountReadBasket": "#B45FA5", 
                           "µTCC_low_40_versions_UpdatePriceDiscountReadBasket": "#67903D"}
    
    default_legend_labels_low = {"µTCC_low_5_versions_UpdatePriceDiscountReadBasket": "µTCC: 5 Versions", 
                             "µTCC_low_10_versions_UpdatePriceDiscountReadBasket": "µTCC: 10 Versions", 
                             "µTCC_low_25_versions_UpdatePriceDiscountReadBasket": "µTCC: 25 Versions", 
                             "µTCC_low_40_versions_UpdatePriceDiscountReadBasket": "µTCC: 40 Versions"}

    default_markers_MIX_low = {"µTCC_low_5_versions_UpdatePriceDiscountReadBasket": "o", 
                           "µTCC_low_10_versions_UpdatePriceDiscountReadBasket": "v", 
                           "µTCC_low_25_versions_UpdatePriceDiscountReadBasket": "s", 
                           "µTCC_low_40_versions_UpdatePriceDiscountReadBasket": "D"}
    
    default_linestyle_MIX_low = {"µTCC_low_5_versions_UpdatePriceDiscountReadBasket": "-", 
                             "µTCC_low_10_versions_UpdatePriceDiscountReadBasket": "--", 
                             "µTCC_low_25_versions_UpdatePriceDiscountReadBasket": "-.", 
                             "µTCC_low_40_versions_UpdatePriceDiscountReadBasket": ":"}

    legend_labels = []
    plt.figure(figsize=(10, 5))
    plt.xlabel("Functionalities / s")

    if(measure == "p(95)"):
        # Plot the 95th percentile latency by request vs throughput
        plt.ylabel("Latency (95th percentile)")
    elif(measure == "med"):
        # Plot the median latency by request vs throughput
        plt.ylabel("Latência (Mediana)")

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
            if(measure == "p(95)"):
                y_values.append(parsed_data[system_plus_cont][test]["latency"]["p(95)"])
            elif(measure == "med"):
                y_values.append(parsed_data[system_plus_cont][test]["latency"]["med"])
            x_values.append(int(test))

        xy = pd.DataFrame({'x': x_values, 'y': y_values})
        xy.sort_values('x', inplace=True)
        # Plot the 95th percentile latency by request vs throughput for each test
        
        if operation == MIX_FUNCS_VERSIONS_HIGH:
            legend_labels.append(default_legend_labels_high[system_plus_cont])
            plt.plot(xy['x'], xy['y'], 
                    marker = default_markers_MIX_high[system_plus_cont],
                    color = default_colors_dict_high[system_plus_cont],
                    linestyle = default_linestyle_MIX_high[system_plus_cont])
        elif operation == MIX_FUNCS_VERSIONS_LOW:
            legend_labels.append(default_legend_labels_low[system_plus_cont])
            plt.plot(xy['x'], xy['y'], 
                    marker = default_markers_MIX_low[system_plus_cont],
                    color = default_colors_dict_low[system_plus_cont],
                    linestyle = default_linestyle_MIX_low[system_plus_cont])

    plt.legend(legend_labels, loc="upper left")
    # Get the current path and create a folder named "plots" if it doesn't exist
    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "Thesis_results", "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    
    plot_names = {"p(95)": "Latency (95th percentile) vs Throughput", "med": "Latency (Median) vs Throughput"}

    # Save the plot as a png file
    if operation == MIX_FUNCS_VERSIONS_HIGH:
        plt.savefig(os.path.join(plots_folder, f"{plot_names[measure]} [Mix Functionalities] [Version Analysis] [High Contention]")+ ".pdf", format="pdf", bbox_inches="tight")
    elif operation == MIX_FUNCS_VERSIONS_LOW:
        plt.savefig(os.path.join(plots_folder, f"{plot_names[measure]} [Mix Functionalities] [Version Analysis] [Low Contention]")+ ".pdf", format="pdf", bbox_inches="tight")



def plot_MIX_data(parsed_data, versions, measure):
    default_colors_dict = {"BaseTCC_high_UpdatePriceDiscountReadBasket": "#FF7742", 
                           "BaseTCC_low_UpdatePriceDiscountReadBasket": "#3D48AD", 
                           "µTCC_high_UpdatePriceDiscountReadBasket": "#B45FA5", 
                           "µTCC_low_UpdatePriceDiscountReadBasket": "#67903D"}
    
    default_legend_labels = {"BaseTCC_high_UpdatePriceDiscountReadBasket": "Base System: High Contention", 
                             "BaseTCC_low_UpdatePriceDiscountReadBasket": "Base System: Low Contention", 
                             "µTCC_high_UpdatePriceDiscountReadBasket": "µTCC: High Contention", 
                             "µTCC_low_UpdatePriceDiscountReadBasket": "µTCC: Low Contention"}

    default_markers_MIX = {"BaseTCC_high_UpdatePriceDiscountReadBasket": "o", 
                           "BaseTCC_low_UpdatePriceDiscountReadBasket": "v", 
                           "µTCC_high_UpdatePriceDiscountReadBasket": "s", 
                           "µTCC_low_UpdatePriceDiscountReadBasket": "D"}
    
    default_linestyle_MIX = {"BaseTCC_high_UpdatePriceDiscountReadBasket": "-", 
                             "BaseTCC_low_UpdatePriceDiscountReadBasket": "--", 
                             "µTCC_high_UpdatePriceDiscountReadBasket": "-.", 
                             "µTCC_low_UpdatePriceDiscountReadBasket": ":"}

    legend_labels = []
    plt.figure(figsize=(10, 5))
    plt.xlabel("Functionalities / s")

    if(measure == "p(95)"):
        # Plot the 95th percentile latency by request vs throughput
        plt.ylabel("Latency (95th percentile)")
    elif(measure == "med"):
        # Plot the median latency by request vs throughput
        plt.ylabel("Latency (Median)")
    elif(measure == "avg"):
        # Plot the average latency by request vs throughput
        plt.ylabel("Latency (Average)")

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
            if(measure == "p(95)"):
                y_values.append(parsed_data[system_plus_cont][test]["latency"]["p(95)"])
            elif(measure == "med"):
                y_values.append(parsed_data[system_plus_cont][test]["latency"]["med"])
            elif(measure == "avg"):
                y_values.append(parsed_data[system_plus_cont][test]["latency"]["avg"])
            x_values.append(int(test))

        xy = pd.DataFrame({'x': x_values, 'y': y_values})
        xy.sort_values('x', inplace=True)
        # Plot the 95th percentile latency by request vs throughput for each test
        
        legend_labels.append(default_legend_labels[system_plus_cont])
        plt.plot(xy['x'], xy['y'], 
                marker = default_markers_MIX[system_plus_cont],
                color = default_colors_dict[system_plus_cont],
                linestyle = default_linestyle_MIX[system_plus_cont])
    
    plt.legend(legend_labels, loc="upper left")
    # Get the current path and create a folder named "plots" if it doesn't exist
    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "Thesis_results", "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    
    plot_names = {"p(95)": "Latency (95th percentile) vs Throughput", "med": "Latency (Median) vs Throughput", "avg": "Latency (Average) vs Throughput"}

    # Save the plot as a png file
    plt.savefig(os.path.join(plots_folder, f"{plot_names[measure]} [Mix Functionalities] [" + str(versions) + " versions]")+ ".pdf", format="pdf", bbox_inches="tight")


def plot_parsed_data(parsed_data, operation, versions, measure):
    default_colors_dict = {"BaseTCC_high_UpdatePriceDiscountReadBasket": "#FF7742", 
                           "BaseTCC_low_UpdatePriceDiscountReadBasket": "#3D48AD", 
                           "µTCC_high_UpdatePriceDiscountReadBasket": "#B45FA5", 
                           "µTCC_low_UpdatePriceDiscountReadBasket": "#67903D"}
    
    default_colors_dict_READ_only = {"BaseTCC_high_ReadBasket_only": "-r", 
                                     "BaseTCC_low_ReadBasket_only": ":r", 
                                     "µTCC_high_ReadBasket_only": "-b", 
                                     "µTCC_low_ReadBasket_only": ":b"}
    default_colors_dict_UPDATE_only = {"BaseTCC_high_UpdatePriceDiscount_only": "#FF7742", 
                                       "BaseTCC_low_UpdatePriceDiscount_only": "#3D48AD", 
                                       "µTCC_high_UpdatePriceDiscount_only": "#B45FA5", 
                                       "µTCC_low_UpdatePriceDiscount_only": "#67903D"}
    default_colors_VERSION_TESTING_low = {"µTCC_low_UpdatePriceDiscountReadBasket_1_versions": "r", 
                                          "µTCC_low_UpdatePriceDiscountReadBasket_100_versions": "b", 
                                          "µTCC_low_UpdatePriceDiscountReadBasket_300_versions": "g", 
                                          "µTCC_low_UpdatePriceDiscountReadBasket_500_versions": "orange"}
    default_colors_VERSION_TESTING_high = {"µTCC_high_UpdatePriceDiscountReadBasket_1_versions": "r", 
                                           "µTCC_high_UpdatePriceDiscountReadBasket_100_versions": "b", 
                                           "µTCC_high_UpdatePriceDiscountReadBasket_300_versions": "g", 
                                           "µTCC_high_UpdatePriceDiscountReadBasket_500_versions": "orange"}

    default_legend_labels = {"BaseTCC_high_UpdatePriceDiscountReadBasket": "Sistema Base: Contenção Elevada", "BaseTCC_low_UpdatePriceDiscountReadBasket": "Sistema Base: Contenção Baixa", "µTCC_high_UpdatePriceDiscountReadBasket": "µTCC: Contenção Elevada", "µTCC_low_UpdatePriceDiscountReadBasket": "µTCC: Contenção Baixa"}
    default_legend_labels_READ_only = {"BaseTCC_high_ReadBasket_only": "Sistema Base: Contenção Elevada", "BaseTCC_low_ReadBasket_only": "Sistema Base: Contenção Baixa", "µTCC_high_ReadBasket_only": "µTCC: Contenção Elevada", "µTCC_low_ReadBasket_only": "µTCC: Contenção Baixa"}
    default_legend_labels_UPDATE_only = {"BaseTCC_high_UpdatePriceDiscount_only": "Sistema Base: Contenção Elevada", "BaseTCC_low_UpdatePriceDiscount_only": "Sistema Base: Contenção Baixa", "µTCC_high_UpdatePriceDiscount_only": "µTCC: Contenção Elevada", "µTCC_low_UpdatePriceDiscount_only": "µTCC: Contenção Baixa"}
    default_legend_labels_VERSION_TESTING_low = { "µTCC_low_UpdatePriceDiscountReadBasket_1_versions": "µTCC: 1 Versão/ Produto", "µTCC_low_UpdatePriceDiscountReadBasket_100_versions": "µTCC: 100 Versões/ Produto", "µTCC_low_UpdatePriceDiscountReadBasket_300_versions": "µTCC: 300 Versões/ Produto", "µTCC_low_UpdatePriceDiscountReadBasket_500_versions": "µTCC: 500 Versões/ Produto"}
    default_legend_labels_VERSION_TESTING_high = { "µTCC_high_UpdatePriceDiscountReadBasket_1_versions": "µTCC: 1 Versão/ Produto", "µTCC_high_UpdatePriceDiscountReadBasket_100_versions": "µTCC: 100 Versões/ Produto", "µTCC_high_UpdatePriceDiscountReadBasket_300_versions": "µTCC: 300 Versões/ Produto", "µTCC_high_UpdatePriceDiscountReadBasket_500_versions": "µTCC: 500 Versões/ Produto"}

    default_markers_UPDATE_only = {"BaseTCC_high_UpdatePriceDiscount_only": "o", "BaseTCC_low_UpdatePriceDiscount_only": "v", "µTCC_high_UpdatePriceDiscount_only": "s", "µTCC_low_UpdatePriceDiscount_only": "D"}
    default_markers_MIX = {"BaseTCC_high_UpdatePriceDiscountReadBasket": "o", "BaseTCC_low_UpdatePriceDiscountReadBasket": "v", "µTCC_high_UpdatePriceDiscountReadBasket": "s", "µTCC_low_UpdatePriceDiscountReadBasket": "D"}
    default_markers_VERSION_TESTING_low = { "µTCC_low_UpdatePriceDiscountReadBasket_1_versions": "o", "µTCC_low_UpdatePriceDiscountReadBasket_100_versions": "v", "µTCC_low_UpdatePriceDiscountReadBasket_300_versions": "s", "µTCC_low_UpdatePriceDiscountReadBasket_500_versions": "D"}
    default_markers_VERSION_TESTING_high = { "µTCC_high_UpdatePriceDiscountReadBasket_1_versions": "o", "µTCC_high_UpdatePriceDiscountReadBasket_100_versions": "v", "µTCC_high_UpdatePriceDiscountReadBasket_300_versions": "s", "µTCC_high_UpdatePriceDiscountReadBasket_500_versions": "D"}
    
    default_linestyle_UPDATE_only = {"BaseTCC_high_UpdatePriceDiscount_only": "-", "BaseTCC_low_UpdatePriceDiscount_only": "--", "µTCC_high_UpdatePriceDiscount_only": "-.", "µTCC_low_UpdatePriceDiscount_only": ":"}
    default_linestyle_MIX = {"BaseTCC_high_UpdatePriceDiscountReadBasket": "-", "BaseTCC_low_UpdatePriceDiscountReadBasket": "--", "µTCC_high_UpdatePriceDiscountReadBasket": "-.", "µTCC_low_UpdatePriceDiscountReadBasket": ":"}
    default_linestyle_VERSION_TESTING_low = { "µTCC_low_UpdatePriceDiscountReadBasket_1_versions": "-", "µTCC_low_UpdatePriceDiscountReadBasket_100_versions": "--", "µTCC_low_UpdatePriceDiscountReadBasket_300_versions": "-.", "µTCC_low_UpdatePriceDiscountReadBasket_500_versions": ":"}
    default_linestyle_VERSION_TESTING_high = { "µTCC_high_UpdatePriceDiscountReadBasket_1_versions": "-", "µTCC_high_UpdatePriceDiscountReadBasket_100_versions": "--", "µTCC_high_UpdatePriceDiscountReadBasket_300_versions": "-.", "µTCC_high_UpdatePriceDiscountReadBasket_500_versions": ":"}

    legend_labels = []
    plt.figure(figsize=(10, 5))
    plt.xlabel("Carga (funcionalidades / s)")

    if(measure == "p(95)"):
        # Plot the 95th percentile latency by request vs throughput
        plt.ylabel("Latency (95th percentile)")
    elif(measure == "med"):
        # Plot the median latency by request vs throughput
        plt.ylabel("Latência (Mediana)")

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
            if(measure == "p(95)"):
                y_values.append(parsed_data[system_plus_cont][test]["latency"]["p(95)"])
            elif(measure == "med"):
                y_values.append(parsed_data[system_plus_cont][test]["latency"]["med"])
            x_values.append(int(test))

        xy = pd.DataFrame({'x': x_values, 'y': y_values})
        xy.sort_values('x', inplace=True)
        # Plot the 95th percentile latency by request vs throughput for each test
        if operation == READ_BASKET_FUNC:
            line_color_format = default_colors_dict_READ_only[system_plus_cont]
            legend_labels.append(default_legend_labels_READ_only[system_plus_cont])
        
        elif operation == UPDATE_DISCOUNT_PRICE_FUNC:
            legend_labels.append(default_legend_labels_UPDATE_only[system_plus_cont])
            plt.plot(xy['x'], xy['y'],
                     marker = default_markers_UPDATE_only[system_plus_cont], 
                     color=default_colors_dict_UPDATE_only[system_plus_cont],
                     linestyle=default_linestyle_UPDATE_only[system_plus_cont])
        
        elif operation == VERSION_TESTING_LOW:
            legend_labels.append(default_legend_labels_VERSION_TESTING_low[system_plus_cont])
            plt.plot(xy['x'], xy['y'],
                     marker = default_markers_VERSION_TESTING_low[system_plus_cont], 
                     color=default_colors_VERSION_TESTING_low[system_plus_cont],
                     linestyle=default_linestyle_VERSION_TESTING_low[system_plus_cont])
        elif operation == VERSION_TESTING_HIGH:
            legend_labels.append(default_legend_labels_VERSION_TESTING_high[system_plus_cont])
            plt.plot(xy['x'], xy['y'],
                     marker = default_markers_VERSION_TESTING_high[system_plus_cont], 
                     color=default_colors_VERSION_TESTING_high[system_plus_cont],
                     linestyle=default_linestyle_VERSION_TESTING_high[system_plus_cont])
        elif operation == MIX_FUNCS:
            legend_labels.append(default_legend_labels[system_plus_cont])
            plt.plot(xy['x'], xy['y'], 
                    marker = default_markers_MIX[system_plus_cont],
                    color = default_colors_dict[system_plus_cont],
                    linestyle = default_linestyle_MIX[system_plus_cont])
        else:
            line_color_format = default_colors_dict[system_plus_cont]
            legend_labels.append(default_legend_labels[system_plus_cont])


    
    plt.legend(legend_labels, loc="upper left")
    # Get the current path and create a folder named "plots" if it doesn't exist
    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "Thesis_results", "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    
    plot_names = {"p(95)": "Latency (95th percentile) vs Throughput", "med": "Latency (Median) vs Throughput"}

    # Save the plot as a png file
    if operation == READ_BASKET_FUNC:
        plt.savefig(os.path.join(plots_folder, f"{plot_names[measure]} [Read Basket only] [" + str(versions) + " versions]")+ ".pdf", format="pdf", bbox_inches="tight")
    elif operation == UPDATE_DISCOUNT_PRICE_FUNC:
        plt.savefig(os.path.join(plots_folder, f"{plot_names[measure]} [Update Discount Price only] [" + str(versions) + " versions]")+ ".pdf", format="pdf", bbox_inches="tight")
    elif operation == VERSION_TESTING_LOW:
        plt.savefig(os.path.join(plots_folder, f"{plot_names[measure]} [µTCC - LOW]")+ ".pdf", format="pdf", bbox_inches="tight")
    elif operation == VERSION_TESTING_HIGH:
        plt.savefig(os.path.join(plots_folder, f"{plot_names[measure]} [µTCC - HIGH]")+ ".pdf", format="pdf", bbox_inches="tight")
    else:
        plt.savefig(os.path.join(plots_folder, f"{plot_names[measure]} [Mix Functionalities] [" + str(versions) + " versions]")+ ".pdf", format="pdf", bbox_inches="tight")


def plot_abort_rate_versions(parsed_data, operation, log_files_measure):
    default_colors_dict_high = {"µTCC_high_5_versions_UpdatePriceDiscountReadBasket": "#FF7742", 
                           "µTCC_high_10_versions_UpdatePriceDiscountReadBasket": "#3D48AD", 
                           "µTCC_high_25_versions_UpdatePriceDiscountReadBasket": "#B45FA5", 
                           "µTCC_high_40_versions_UpdatePriceDiscountReadBasket": "#67903D"}
    
    default_legend_labels_high = {"µTCC_high_5_versions_UpdatePriceDiscountReadBasket": "µTCC: 5 Versions", 
                             "µTCC_high_10_versions_UpdatePriceDiscountReadBasket": "µTCC: 10 Versions", 
                             "µTCC_high_25_versions_UpdatePriceDiscountReadBasket": "µTCC: 25 Versions", 
                             "µTCC_high_40_versions_UpdatePriceDiscountReadBasket": "µTCC: 40 Versions"}

    default_markers_MIX_high = {"µTCC_high_5_versions_UpdatePriceDiscountReadBasket": "o", 
                           "µTCC_high_10_versions_UpdatePriceDiscountReadBasket": "v", 
                           "µTCC_high_25_versions_UpdatePriceDiscountReadBasket": "s", 
                           "µTCC_high_40_versions_UpdatePriceDiscountReadBasket": "D"}
    
    default_linestyle_MIX_high = {"µTCC_high_5_versions_UpdatePriceDiscountReadBasket": "-", 
                             "µTCC_high_10_versions_UpdatePriceDiscountReadBasket": "--", 
                             "µTCC_high_25_versions_UpdatePriceDiscountReadBasket": "-.", 
                             "µTCC_high_40_versions_UpdatePriceDiscountReadBasket": ":"}

    default_colors_dict_low = {"µTCC_low_5_versions_UpdatePriceDiscountReadBasket": "#FF7742", 
                           "µTCC_low_10_versions_UpdatePriceDiscountReadBasket": "#3D48AD", 
                           "µTCC_low_25_versions_UpdatePriceDiscountReadBasket": "#B45FA5", 
                           "µTCC_low_40_versions_UpdatePriceDiscountReadBasket": "#67903D"}
    
    default_legend_labels_low = {"µTCC_low_5_versions_UpdatePriceDiscountReadBasket": "µTCC: 5 Versions", 
                             "µTCC_low_10_versions_UpdatePriceDiscountReadBasket": "µTCC: 10 Versions", 
                             "µTCC_low_25_versions_UpdatePriceDiscountReadBasket": "µTCC: 25 Versions", 
                             "µTCC_low_40_versions_UpdatePriceDiscountReadBasket": "µTCC: 40 Versions"}

    default_markers_MIX_low = {"µTCC_low_5_versions_UpdatePriceDiscountReadBasket": "o", 
                           "µTCC_low_10_versions_UpdatePriceDiscountReadBasket": "v", 
                           "µTCC_low_25_versions_UpdatePriceDiscountReadBasket": "s", 
                           "µTCC_low_40_versions_UpdatePriceDiscountReadBasket": "D"}
    
    default_linestyle_MIX_low = {"µTCC_low_5_versions_UpdatePriceDiscountReadBasket": "-", 
                             "µTCC_low_10_versions_UpdatePriceDiscountReadBasket": "--", 
                             "µTCC_low_25_versions_UpdatePriceDiscountReadBasket": "-.", 
                             "µTCC_low_40_versions_UpdatePriceDiscountReadBasket": ":"}

    legend_labels = []
    plt.figure(figsize=(10, 5))
    plt.xlabel("Functionalities / s")

    plt.ylabel("Abort Rate (%)")

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
            y_values.append(parsed_data[system_plus_cont][test]["abort_rate"])
            x_values.append(int(test))

        xy = pd.DataFrame({'x': x_values, 'y': y_values})
        xy.sort_values('x', inplace=True)
        # Plot the 95th percentile latency by request vs throughput for each test
        
        if operation == MIX_FUNCS_VERSIONS_HIGH:
            legend_labels.append(default_legend_labels_high[system_plus_cont])
            plt.plot(xy['x'], xy['y'], 
                    marker = default_markers_MIX_high[system_plus_cont],
                    color = default_colors_dict_high[system_plus_cont],
                    linestyle = default_linestyle_MIX_high[system_plus_cont])
        elif operation == MIX_FUNCS_VERSIONS_LOW:
            legend_labels.append(default_legend_labels_low[system_plus_cont])
            plt.plot(xy['x'], xy['y'], 
                    marker = default_markers_MIX_low[system_plus_cont],
                    color = default_colors_dict_low[system_plus_cont],
                    linestyle = default_linestyle_MIX_low[system_plus_cont])
                                
    plt.legend(legend_labels, loc="upper left")
    # Get the current path and create a folder named "plots" if it doesn't exist
    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "Thesis_results", "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    
    # Save the plot as a png file
    if operation == MIX_FUNCS_VERSIONS_HIGH:
        plt.savefig(os.path.join(plots_folder, f"Average Abort Rate [Mix Functionalities] [Version Analysis] [High Contention]")+ ".pdf", format="pdf", bbox_inches="tight")
    elif operation == MIX_FUNCS_VERSIONS_LOW:
        plt.savefig(os.path.join(plots_folder, f"Average Abort Rate [Mix Functionalities] [Version Analysis] [Low Contention]")+ ".pdf", format="pdf", bbox_inches="tight")




def plot_abort_rate(parsed_data, operation, versions, log_files_measure):
    default_colors_dict = {"BaseTCC_high_UpdatePriceDiscountReadBasket": "#FF7742", 
                           "BaseTCC_low_UpdatePriceDiscountReadBasket": "#3D48AD", 
                           "µTCC_high_UpdatePriceDiscountReadBasket": "#B45FA5", 
                           "µTCC_low_UpdatePriceDiscountReadBasket": "#67903D"}
    
    default_legend_labels = {"BaseTCC_high_UpdatePriceDiscountReadBasket": "Base System: High Contention", 
                             "BaseTCC_low_UpdatePriceDiscountReadBasket": "Base System: Low Contention", 
                             "µTCC_high_UpdatePriceDiscountReadBasket": "µTCC: High Contention", 
                             "µTCC_low_UpdatePriceDiscountReadBasket": "µTCC: Low Contention"}
    
    default_markers_MIX = {"BaseTCC_high_UpdatePriceDiscountReadBasket": "o", 
                           "BaseTCC_low_UpdatePriceDiscountReadBasket": "v", 
                           "µTCC_high_UpdatePriceDiscountReadBasket": "s", 
                           "µTCC_low_UpdatePriceDiscountReadBasket": "D"}
    
    default_linestyle_MIX = {"BaseTCC_high_UpdatePriceDiscountReadBasket": "-", 
                             "BaseTCC_low_UpdatePriceDiscountReadBasket": "--", 
                             "µTCC_high_UpdatePriceDiscountReadBasket": "-.", 
                             "µTCC_low_UpdatePriceDiscountReadBasket": ":"}

    legend_labels = []
    plt.figure(figsize=(10, 5))
    plt.xlabel("Functionalities / s")

    plt.ylabel("Abort Rate (%)")

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
            y_values.append(parsed_data[system_plus_cont][test]["abort_rate"])
            x_values.append(int(test))

        xy = pd.DataFrame({'x': x_values, 'y': y_values})
        xy.sort_values('x', inplace=True)
        # Plot the 95th percentile latency by request vs throughput for each test
        if operation == MIX_FUNCS:
            legend_labels.append(default_legend_labels[system_plus_cont])
            plt.plot(xy['x'], xy['y'], 
                    marker = default_markers_MIX[system_plus_cont],
                    color = default_colors_dict[system_plus_cont],
                    linestyle = default_linestyle_MIX[system_plus_cont])
        else:
            raise Exception("Operation not supported")
        
    plt.legend(legend_labels, loc="upper left")
    # Get the current path and create a folder named "plots" if it doesn't exist
    current_path = os.path.dirname(os.path.realpath(__file__))
    plots_folder = os.path.join(current_path, "Thesis_results", "plots")
    if not os.path.exists(plots_folder):
        os.makedirs(plots_folder)
    
    # Save the plot as a png file
    if operation == MIX_FUNCS:
        plt.savefig(os.path.join(plots_folder, f"Average Abort Rate [Mix Functionalities] [" + str(versions) + " versions]")+ ".pdf", format="pdf", bbox_inches="tight")

# def calculate_penalty_versions(parsed_data, operation):
#     low_cont_penalty_ratio = 0
#     high_cont_penalty_ratio = 0

#     if operation == MIX_FUNCS_VERSIONS_HIGH:
#         microTCC_high_cont_5_versions = parsed_data["µTCC_high_5_versions_UpdatePriceDiscountReadBasket"]
#         microTCC_high_cont_10_versions = parsed_data["µTCC_high_10_versions_UpdatePriceDiscountReadBasket"]
#         microTCC_high_cont_25_versions = parsed_data["µTCC_high_25_versions_UpdatePriceDiscountReadBasket"]
#         microTCC_high_cont_40_versions = parsed_data["µTCC_high_40_versions_UpdatePriceDiscountReadBasket"]

#     microTCC_high_cont_5_verions_sorted_keys = sorted(microTCC_high_cont_5_versions, key=lambda x: int(x))
#     microTCC_high_cont_10_verions_sorted_keys = sorted(microTCC_high_cont_10_versions, key=lambda x: int(x))
#     microTCC_high_cont_25_verions_sorted_keys = sorted(microTCC_high_cont_25_versions, key=lambda x: int(x))
#     microTCC_high_cont_40_verions_sorted_keys = sorted(microTCC_high_cont_40_versions, key=lambda x: int(x))


#     for test_key in microTCC_high_cont_5_verions_sorted_keys:
#         microTCC_5ver_result = microTCC_high_cont_5_versions[test_key]["latency"]["p(95)"]
#         microTCC_result = microTCC_low_cont[test_key]["latency"]["p(95)"]
#         low_cont_penalty_ratio = microTCC_result / baseTCC_result
#         print("Penalty ratio for low contention: VUs:" + test_key + " = " + str(low_cont_penalty_ratio))

#     for test_key in baseTCC_high_cont_sorted_keys:
#         baseTCC_result = baseTCC_high_cont[test_key]["latency"]["p(95)"]
#         microTCC_result = microTCC_high_cont[test_key]["latency"]["p(95)"]
#         high_cont_penalty_ratio = microTCC_result / baseTCC_result
#         print("Penalty ratio for high contention: VUs:" + test_key + " = " + str(high_cont_penalty_ratio))


#     average_baseTCC_low_latency = 0
#     average_baseTCC_high_latency = 0
#     average_microTCC_low_latency = 0
#     average_microTCC_high_latency = 0



#     for index, test_key in enumerate(baseTCC_low_cont_sorted_keys):
#         test_results = baseTCC_low_cont[test_key]
#         # if (index in [0, 1, len(baseTCC_low_cont_sorted_keys) - 1, len(baseTCC_low_cont_sorted_keys) - 2]): # Skip the first and last tests
#             # continue
#         average_baseTCC_low_latency += test_results["latency"]["p(95)"]
#     average_baseTCC_low_latency = average_baseTCC_low_latency / (len(baseTCC_low_cont))

#     for index, test_key in enumerate(baseTCC_high_cont_sorted_keys):
#         test_results = baseTCC_high_cont[test_key]
#         # if (index in [0, 1, len(baseTCC_high_cont) - 1, len(baseTCC_high_cont) - 2]): # Skip the first and last tests
#             # continue
#         average_baseTCC_high_latency += test_results["latency"]["p(95)"]
#     average_baseTCC_high_latency = average_baseTCC_high_latency / (len(baseTCC_high_cont))

#     for index, test_key in enumerate(microTCC_low_cont_sorted_keys):
#         test_results = microTCC_low_cont[test_key]
#         # if (index in [0, 1, len(microTCC_low_cont) - 1, len(microTCC_low_cont) - 2]):
#             # continue
#         average_microTCC_low_latency += test_results["latency"]["p(95)"]
#     average_microTCC_low_latency = average_microTCC_low_latency / (len(microTCC_low_cont))

#     for index, test_key in enumerate(microTCC_high_cont_sorted_keys):
#         test_results = microTCC_high_cont[test_key]
#         # if (index in [0, 1, len(microTCC_high_cont) - 1, len(microTCC_high_cont) - 2]):
#             # continue
#         average_microTCC_high_latency += test_results["latency"]["p(95)"]
#     average_microTCC_high_latency = average_microTCC_high_latency / (len(microTCC_high_cont))

#     low_cont_penalty_ration = average_microTCC_low_latency / average_baseTCC_low_latency * 100 # In percentage, the higher the worse
#     high_cont_penalty_ration = average_microTCC_high_latency / average_baseTCC_high_latency * 100 # In percentage, the higher the worse

#     print("Average latency for BaseTCC with low contention: " + str(average_baseTCC_low_latency))
#     print("Average latency for BaseTCC with high contention: " + str(average_baseTCC_high_latency))
#     print("Average latency for µTCC with low contention: " + str(average_microTCC_low_latency))
#     print("Average latency for µTCC with high contention: " + str(average_microTCC_high_latency))
#     print("Penalty ratio for low contention: " + str(low_cont_penalty_ration - 100))
#     print("Penalty ratio for high contention: " + str(high_cont_penalty_ration - 100))

def calculate_version_penalty(parsed_data, operation):
    low_cont_penalty_ratio = 0
    high_cont_penalty_ratio = 0

    if operation == MIX_FUNCS_VERSIONS_HIGH:
        microTCC_high_cont_5_versions = parsed_data["µTCC_high_5_versions_UpdatePriceDiscountReadBasket"]
        microTCC_high_cont_10_versions = parsed_data["µTCC_high_10_versions_UpdatePriceDiscountReadBasket"]
        microTCC_high_cont_25_versions = parsed_data["µTCC_high_25_versions_UpdatePriceDiscountReadBasket"]
        microTCC_high_cont_40_versions = parsed_data["µTCC_high_40_versions_UpdatePriceDiscountReadBasket"]
    # elif operation == VERSION_TESTING_LOW:
    #     microTCC_low_cont_5_versions = parsed_data["µTCC_low_5_versions_UpdatePriceDiscountReadBasket"]
    #     microTCC_low_cont_10_versions = parsed_data["µTCC_low_10_versions_UpdatePriceDiscountReadBasket"]
    #     microTCC_low_cont_25_versions = parsed_data["µTCC_low_25_versions_UpdatePriceDiscountReadBasket"]
    #     microTCC_low_cont_40_versions = parsed_data["µTCC_low_40_versions_UpdatePriceDiscountReadBasket"]

    microTCC_high_cont_5_verions_sorted_keys = sorted(microTCC_high_cont_5_versions, key=lambda x: int(x))
    microTCC_high_cont_10_verions_sorted_keys = sorted(microTCC_high_cont_10_versions, key=lambda x: int(x))
    microTCC_high_cont_25_verions_sorted_keys = sorted(microTCC_high_cont_25_versions, key=lambda x: int(x))
    microTCC_high_cont_40_verions_sorted_keys = sorted(microTCC_high_cont_40_versions, key=lambda x: int(x))

    # microTCC_low_cont_5_verions_sorted_keys = sorted(microTCC_low_cont_5_versions, key=lambda x: int(x))
    # microTCC_low_cont_10_verions_sorted_keys = sorted(microTCC_low_cont_10_versions, key=lambda x: int(x))
    # microTCC_low_cont_25_verions_sorted_keys = sorted(microTCC_low_cont_25_versions, key=lambda x: int(x))
    # microTCC_low_cont_40_verions_sorted_keys = sorted(microTCC_low_cont_40_versions, key=lambda x: int(x))

    # Compare the latency for each vu between 25 versions and 40 versions results
    for test_key in microTCC_high_cont_25_verions_sorted_keys:
        microTCC_25ver_result = microTCC_high_cont_25_versions[test_key]["latency"]["p(95)"]
        microTCC_40ver_result = microTCC_high_cont_40_versions[test_key]["latency"]["p(95)"]
        high_cont_penalty_ratio = microTCC_40ver_result / microTCC_25ver_result
        print("Penalty ratio for high contention: VUs:" + test_key + " = " + str(high_cont_penalty_ratio))


def calculate_penalty(parsed_data, operation, measure):
    low_cont_penalty_ratio = 0
    high_cont_penalty_ratio = 0

    if operation == READ_BASKET_FUNC:
        baseTCC_low_cont = parsed_data["BaseTCC_low_ReadBasket_only"]
        baseTCC_high_cont = parsed_data["BaseTCC_high_ReadBasket_only"]
        microTCC_low_cont = parsed_data["µTCC_low_ReadBasket_only"]
        microTCC_high_cont = parsed_data["µTCC_high_ReadBasket_only"]
    elif operation == UPDATE_DISCOUNT_PRICE_FUNC:
        baseTCC_low_cont = parsed_data["BaseTCC_low_UpdatePriceDiscount_only"]
        baseTCC_high_cont = parsed_data["BaseTCC_high_UpdatePriceDiscount_only"]
        microTCC_low_cont = parsed_data["µTCC_low_UpdatePriceDiscount_only"]
        microTCC_high_cont = parsed_data["µTCC_high_UpdatePriceDiscount_only"]
    elif operation == MIX_FUNCS:
        baseTCC_low_cont = parsed_data["BaseTCC_low_UpdatePriceDiscountReadBasket"]
        baseTCC_high_cont = parsed_data["BaseTCC_high_UpdatePriceDiscountReadBasket"]
        microTCC_low_cont = parsed_data["µTCC_low_UpdatePriceDiscountReadBasket"]
        microTCC_high_cont = parsed_data["µTCC_high_UpdatePriceDiscountReadBasket"]


    baseTCC_low_cont_sorted_keys = sorted(baseTCC_low_cont, key=lambda x: int(x))
    baseTCC_high_cont_sorted_keys = sorted(baseTCC_high_cont, key=lambda x: int(x))
    microTCC_low_cont_sorted_keys = sorted(microTCC_low_cont, key=lambda x: int(x))
    microTCC_high_cont_sorted_keys = sorted(microTCC_high_cont, key=lambda x: int(x))

    if measure == "p(95)":
        for test_key in baseTCC_low_cont_sorted_keys:
            baseTCC_result = baseTCC_low_cont[test_key]["latency"]["p(95)"]
            microTCC_result = microTCC_low_cont[test_key]["latency"]["p(95)"]
            low_cont_penalty_ratio = microTCC_result / baseTCC_result
            print("BaseTCC: " + str(baseTCC_result) + "ms || microTCC " + str(microTCC_result) + "ms. Penalty ratio for low contention: VUs:" + test_key + " = " + str(low_cont_penalty_ratio))

        for test_key in baseTCC_high_cont_sorted_keys:
            baseTCC_result = baseTCC_high_cont[test_key]["latency"]["p(95)"]
            microTCC_result = microTCC_high_cont[test_key]["latency"]["p(95)"]
            high_cont_penalty_ratio = microTCC_result / baseTCC_result
            print("BaseTCC: " + str(baseTCC_result) + "ms || microTCC " + str(microTCC_result) + "ms. Penalty ratio for high contention: VUs:" + test_key + " = " + str(high_cont_penalty_ratio))
    elif measure == "med":
        for test_key in baseTCC_low_cont_sorted_keys:
            baseTCC_result = baseTCC_low_cont[test_key]["latency"]["med"]
            microTCC_result = microTCC_low_cont[test_key]["latency"]["med"]
            low_cont_penalty_ratio = microTCC_result / baseTCC_result
            print("BaseTCC: " + str(baseTCC_result) + "ms || microTCC " + str(microTCC_result) + "ms. Penalty ratio for low contention: VUs:" + test_key + " = " + str(low_cont_penalty_ratio))
        for test_key in baseTCC_high_cont_sorted_keys:
            baseTCC_result = baseTCC_high_cont[test_key]["latency"]["med"]
            microTCC_result = microTCC_high_cont[test_key]["latency"]["med"]
            high_cont_penalty_ratio = microTCC_result / baseTCC_result
            print("BaseTCC: " + str(baseTCC_result) + "ms || microTCC " + str(microTCC_result) + "ms. Penalty ratio for high contention: VUs:" + test_key + " = " + str(high_cont_penalty_ratio))
    # average_baseTCC_low_latency = 0
    # average_baseTCC_high_latency = 0
    # average_microTCC_low_latency = 0
    # average_microTCC_high_latency = 0



    # for index, test_key in enumerate(baseTCC_low_cont_sorted_keys):
    #     test_results = baseTCC_low_cont[test_key]
    #     # if (index in [0, 1, len(baseTCC_low_cont_sorted_keys) - 1, len(baseTCC_low_cont_sorted_keys) - 2]): # Skip the first and last tests
    #         # continue
    #     average_baseTCC_low_latency += test_results["latency"]["p(95)"]
    # average_baseTCC_low_latency = average_baseTCC_low_latency / (len(baseTCC_low_cont))

    # for index, test_key in enumerate(baseTCC_high_cont_sorted_keys):
    #     test_results = baseTCC_high_cont[test_key]
    #     # if (index in [0, 1, len(baseTCC_high_cont) - 1, len(baseTCC_high_cont) - 2]): # Skip the first and last tests
    #         # continue
    #     average_baseTCC_high_latency += test_results["latency"]["p(95)"]
    # average_baseTCC_high_latency = average_baseTCC_high_latency / (len(baseTCC_high_cont))

    # for index, test_key in enumerate(microTCC_low_cont_sorted_keys):
    #     test_results = microTCC_low_cont[test_key]
    #     # if (index in [0, 1, len(microTCC_low_cont) - 1, len(microTCC_low_cont) - 2]):
    #         # continue
    #     average_microTCC_low_latency += test_results["latency"]["p(95)"]
    # average_microTCC_low_latency = average_microTCC_low_latency / (len(microTCC_low_cont))

    # for index, test_key in enumerate(microTCC_high_cont_sorted_keys):
    #     test_results = microTCC_high_cont[test_key]
    #     # if (index in [0, 1, len(microTCC_high_cont) - 1, len(microTCC_high_cont) - 2]):
    #         # continue
    #     average_microTCC_high_latency += test_results["latency"]["p(95)"]
    # average_microTCC_high_latency = average_microTCC_high_latency / (len(microTCC_high_cont))

    # low_cont_penalty_ration = average_microTCC_low_latency / average_baseTCC_low_latency * 100 # In percentage, the higher the worse
    # high_cont_penalty_ration = average_microTCC_high_latency / average_baseTCC_high_latency * 100 # In percentage, the higher the worse

    # print("Average latency for BaseTCC with low contention: " + str(average_baseTCC_low_latency))
    # print("Average latency for BaseTCC with high contention: " + str(average_baseTCC_high_latency))
    # print("Average latency for µTCC with low contention: " + str(average_microTCC_low_latency))
    # print("Average latency for µTCC with high contention: " + str(average_microTCC_high_latency))
    # print("Penalty ratio for low contention: " + str(low_cont_penalty_ration - 100))
    # print("Penalty ratio for high contention: " + str(high_cont_penalty_ration - 100))


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
    
    aborted_transaction = 0
    successfull_transaction = 0

    parsed_data = {}
    parsed_data["latency"] = {}
    parsed_data["latency"]["min"] = 0
    parsed_data["latency"]["max"] = 0
    parsed_data["latency"]["avg"] = 0
    parsed_data["latency"]["med"] = 0
    parsed_data["latency"]["p(90)"] = 0
    parsed_data["latency"]["p(95)"] = 0
    parsed_data["abort_rate"] = 0

    date_latency_ms_pairs = []
    # Start parsing the data, line by line
    for line in log_file:
        date_and_latency = re.findall(r'Date: ([\d]+) \w+ operation duration: ([\d]+)', line)
        if date_and_latency:
            date_ms = int(date_and_latency[0][0])
            operation_duration = int(date_and_latency[0][1])

            date_latency_ms_pair = (date_ms, operation_duration)
            date_latency_ms_pairs.append(date_latency_ms_pair)
        elif(re.findall(r'price is not coherent', line)):
            aborted_transaction+=1
        elif(re.findall(r'price is coherent', line)):
            successfull_transaction+=1
    
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
    parsed_data["abort_rate"] = aborted_transaction / (aborted_transaction + successfull_transaction) * 100
     
    return parsed_data


def CollectDatafiles(operation: int, versions: int, events: bool):
    if operation == UPDATE_DISCOUNT_PRICE_FUNC:
        if not events:
            return CollectUpdatePriceDiscountNoEvents(versions)
    elif operation == READ_BASKET_FUNC:
        if not events:
            return CollectReadBasketDatafiles(versions)
    elif operation == MIX_FUNCS:
        if not events:
            return CollectMixDatafiles(versions)
    elif operation == MIX_FUNCS_VERSIONS_HIGH:
        if not events:
            return CollectVersionMixDatafilesHigh()
    elif operation == MEMORY_TEST_LOW:
        if not events:
            return CollectMemoryTestLow()
        
def CollectMemoryTestLow():
    current_path = os.getcwd()
    test_logs_path = os.path.join(current_path, "testing_scripts", "logs", "K6_tests", "Thesis_results")
    print("Test logs path: " + test_logs_path)
    data_paths = [os.path.join(test_logs_path, "µTCC", "low_UpdatePriceDiscountReadBasket_withoutEvents", "25_versions_memory_test"),
                  os.path.join(test_logs_path, "BaseTCC", "low_UpdatePriceDiscountReadBasket_withoutEvents", "25_versions_memory_test")]
    systems = ["µTCC", "BaseTCC"]
    key_names = ["catalog", "basket", "discount", "coordinator"]
    results_dict = {}
    
    for index, path in enumerate(data_paths):
        # For each file in different systems
        log_files = os.listdir(path)
        log_files_data = {}
        for key_name in key_names:
            log_files_data[key_name] = {}
        for log_file in log_files:
            # For each file in the system
            if "memory" not in log_file:
                continue
            vus = log_file.split("_")[0] # Get the number of VUs from the log file name
            service_name = log_file.split(".")[0].split("_")[-1]
            log_files_data[service_name][vus] = parse_MemoryData_logs(log_file, path)
        dic_key = "µTCC" if "µTCC" in path else "BaseTCC"
        results_dict[dic_key] = log_files_data
    return results_dict


def parse_MemoryData_logs(log_file, path):
    log_file_path = os.path.join(path, log_file)
    if not os.path.isfile(log_file_path):
        print("The file " + log_file_path + " does not exist/ is not a file.")
        return
    # open the file and parse the data
    with open(log_file_path, "r") as f:
        file_lines = f.readlines() # Get all the lines of the file
        parsed_data = parse_data_MemoryData(file_lines)
    return parsed_data


def parse_data_MemoryData(log_file: list[str]) -> dict:
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
    
    aborted_transaction = 0
    successfull_transaction = 0

    parsed_data = {}
    parsed_data["average"] = {}

    data_usages = []
    # Start parsing the data, line by line
    for line in log_file:
        data_usage_str = line.split("M")[0]
        try:
            data_usages.append(float(data_usage_str))
        except:
            data_usage_str = line.split("G")[0]
            data_usages.append(float(data_usage_str) * 1000)
    
    # Calculate the average data usage
    parsed_data["average"] = sum(data_usages) / len(data_usages)

    return parsed_data


def CollectVersionMixDatafilesHigh():
    current_path = os.getcwd()
    test_logs_path = os.path.join(current_path, "testing_scripts", "logs", "K6_tests", "Thesis_results")
    print("Test logs path: " + test_logs_path)
    data_paths = [os.path.join(test_logs_path, "µTCC", "high_UpdatePriceDiscountReadBasket_withoutEvents", "5_versions"),
                  os.path.join(test_logs_path, "µTCC", "high_UpdatePriceDiscountReadBasket_withoutEvents", "10_versions"),
                  os.path.join(test_logs_path, "µTCC", "high_UpdatePriceDiscountReadBasket_withoutEvents", "25_versions"),
                  os.path.join(test_logs_path, "µTCC", "high_UpdatePriceDiscountReadBasket_withoutEvents", "40_versions")]
    key_names = [("µTCC_high", "5_versions"), ("µTCC_high", "10_versions"), ("µTCC_high", "25_versions"), ("µTCC_high", "40_versions")]
    results_dict = {}
    for index, path in enumerate(data_paths):
        print("Test: " + key_names[index][0] + "_" + key_names[index][1])
        log_files = os.listdir(path)
        log_files_data = {}
        for log_file in log_files:
            vus = log_file.split(".")[0] # Get the number of VUs from the log file name
            print("\tVUs: " + vus)
            log_files_data[vus] = parse_MixData_logs(log_file, path)
        results_dict[key_names[index][0] + "_" + key_names[index][1] + "_UpdatePriceDiscountReadBasket"] = log_files_data
    return results_dict


def CollectMixDatafiles(versions: int):
    current_path = os.getcwd()
    test_logs_path = os.path.join(current_path, "testing_scripts", "logs", "K6_tests", "Thesis_results")
    print("Test logs path: " + test_logs_path)
    data_paths = [os.path.join(test_logs_path, "µTCC", "high_UpdatePriceDiscountReadBasket_withoutEvents", str(versions)+"_versions"),
                  os.path.join(test_logs_path, "µTCC", "low_UpdatePriceDiscountReadBasket_withoutEvents", str(versions)+"_versions"),
                  os.path.join(test_logs_path, "BaseTCC", "high_UpdatePriceDiscountReadBasket_withoutEvents", str(versions)+"_versions"),
                  os.path.join(test_logs_path, "BaseTCC", "low_UpdatePriceDiscountReadBasket_withoutEvents", str(versions)+"_versions")]
    key_names = [("µTCC", "high"), ("µTCC", "low"), ("BaseTCC", "high"), ("BaseTCC", "low")]
    results_dict = {}
    for index, path in enumerate(data_paths):
        print("Test: " + key_names[index][0] + "_" + key_names[index][1])
        log_files = os.listdir(path)
        log_files_data = {}
        for log_file in log_files:
            vus = log_file.split(".")[0] # Get the number of VUs from the log file name
            print("\tVUs: " + vus)
            log_files_data[vus] = parse_MixData_logs(log_file, path)
        results_dict[key_names[index][0] + "_" + key_names[index][1] + "_UpdatePriceDiscountReadBasket"] = log_files_data
    return results_dict

def CollectReadBasketDatafiles(versions: int):
    current_path = os.getcwd()
    test_logs_path = os.path.join(current_path, "testing_scripts", "logs", "K6_tests", "Thesis_results")
    print("Test logs path: " + test_logs_path)
    data_paths = [os.path.join(test_logs_path, "µTCC", "high_ReadBasket_only_withoutEvents", str(versions)+"_versions"),
                  os.path.join(test_logs_path, "µTCC", "low_ReadBasket_only_withoutEvents", str(versions)+"_versions"),
                  os.path.join(test_logs_path, "BaseTCC", "high_ReadBasket_only_withoutEvents", str(versions)+"_versions"),
                  os.path.join(test_logs_path, "BaseTCC", "low_ReadBasket_only_withoutEvents", str(versions)+"_versions")]
    key_names = [("µTCC", "high"), ("µTCC", "low"), ("BaseTCC", "high"), ("BaseTCC", "low")]
    results_dict = {}
    for index, path in enumerate(data_paths):
        log_files = os.listdir(path)
        log_files_data = {}
        for log_file in log_files:
            vus = log_file.split(".")[0] # Get the number of VUs from the log file name
            log_files_data[vus] = parse_ReadBasket_logs(log_file, path)
        results_dict[key_names[index][0] + "_" + key_names[index][1] + "_ReadBasket_only"] = log_files_data
    return results_dict


def CollectUpdatePriceDiscountNoEvents(versions: int):
    current_path = os.getcwd()
    test_logs_path = os.path.join(current_path, "testing_scripts", "logs", "K6_tests", "Thesis_results")
    print("Test logs path: " + test_logs_path)
    data_paths = [os.path.join(test_logs_path, "µTCC", "high_UpdatePriceDiscount_only_withoutEvents", str(versions)+"_versions"),
                  os.path.join(test_logs_path, "µTCC", "low_UpdatePriceDiscount_only_withoutEvents", str(versions)+"_versions"),
                  os.path.join(test_logs_path, "BaseTCC", "high_UpdatePriceDiscount_only_withoutEvents", str(versions)+"_versions"),
                  os.path.join(test_logs_path, "BaseTCC", "low_UpdatePriceDiscount_only_withoutEvents", str(versions)+"_versions")]
    key_names = [("µTCC", "high"), ("µTCC", "low"), ("BaseTCC", "high"), ("BaseTCC", "low")]
    results_dict = {}
    for index, path in enumerate(data_paths):
        log_files = os.listdir(path)
        log_files_data = {}
        for log_file in log_files:
            vus = log_file.split(".")[0] # Get the number of VUs from the log file name
            log_files_data[vus] = parse_UpdatePriceDiscount_logs(log_file, path)
        results_dict[key_names[index][0] + "_" + key_names[index][1] + "_UpdatePriceDiscount_only"] = log_files_data
    return results_dict


def CollectTestDatafiles(operation: int, versions: int):
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
            if (operation == MIX_FUNCS and contention != "high_UpdatePriceDiscountReadBasket" and contention != "low_UpdatePriceDiscountReadBasket"):
                continue
            elif (operation == READ_BASKET_FUNC and contention != "high_ReadBasket_only" and contention != "low_ReadBasket_only"):
                continue
            elif (operation == UPDATE_DISCOUNT_PRICE_FUNC 
                  and contention != "high_UpdatePriceDiscount_only_withoutEvents"
                  and contention != "high_UpdatePriceDiscount_only_withEvents" 
                  and contention != "low_UpdatePriceDiscount_only"):
                continue
            elif (operation == VERSION_TESTING_LOW and contention != "low_UpdatePriceDiscountReadBasket"):
                continue
            elif (operation == VERSION_TESTING_HIGH and contention != "high_UpdatePriceDiscountReadBasket"):
                continue
            
            type_path = os.path.join(system_path, contention)
            if(operation != VERSION_TESTING_LOW and operation != VERSION_TESTING_HIGH):
                type_path = os.path.join(type_path, str(versions)+"_versions")


            if(operation == VERSION_TESTING_LOW or operation == VERSION_TESTING_HIGH):
                version_directories = os.listdir(type_path)
                for versions_test in version_directories:
                    log_files_data = {}
                    version_directory_path = os.path.join(type_path, versions_test)
                    log_files = os.listdir(version_directory_path)
                    for log_file in log_files:
                        parse_individual_log(log_file, version_directory_path, log_files_data)
                    results_dict[system + "_" + contention + "_" + versions_test] = log_files_data
            else:
                log_files_data = {}
                log_files = os.listdir(type_path)
                for log_file in log_files:
                    parse_individual_log(log_file, type_path, log_files_data)
                results_dict[system + "_" + contention] = log_files_data
    return results_dict

def parse_MixData_logs(log_file, path):
    log_file_path = os.path.join(path, log_file)
    if not os.path.isfile(log_file_path):
        print("The file " + log_file_path + " does not exist/ is not a file.")
        return
    # open the file and parse the data
    with open(log_file_path, "r") as f:
        file_lines = f.readlines() # Get all the lines of the file
        parsed_data = parse_data_MixData(file_lines)
    return parsed_data


def parse_ReadBasket_logs(log_file, path):
    log_file_path = os.path.join(path, log_file)
    if not os.path.isfile(log_file_path):
        print("The file " + log_file_path + " does not exist/ is not a file.")
        return
    # open the file and parse the data
    with open(log_file_path, "r") as f:
        file_lines = f.readlines() # Get all the lines of the file
        parsed_data = parse_data_ReadBasket(file_lines)
    return parsed_data


def parse_UpdatePriceDiscount_logs(log_file, path):
    
    log_file_path = os.path.join(path, log_file)
    if not os.path.isfile(log_file_path):
        print("The file " + log_file_path + " does not exist/ is not a file.")
        return
    # open the file and parse the data
    with open(log_file_path, "r") as f:
        file_lines = f.readlines() # Get all the lines of the file
        parsed_data = parse_data_updatePriceDiscount(file_lines)
    return parsed_data


def parse_data_MixData(log_file: list[str]) -> dict:
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
    
    aborted_transaction = 0
    successfull_transaction = 0

    parsed_data = {}
    parsed_data["latency"] = {}
    parsed_data["latency"]["min"] = 0
    parsed_data["latency"]["max"] = 0
    parsed_data["latency"]["avg"] = 0
    parsed_data["latency"]["med"] = 0
    parsed_data["latency"]["p(90)"] = 0
    parsed_data["latency"]["p(95)"] = 0
    parsed_data["abort_rate"] = 0

    date_latency_ms_pairs = []
    # Start parsing the data, line by line
    for line in log_file:
        date_and_latency = re.findall(r'Date: ([\d]+) \w+ operation duration: ([\d]+)', line)
        if date_and_latency:
            date_ms = int(date_and_latency[0][0])
            operation_duration = int(date_and_latency[0][1])

            date_latency_ms_pair = (date_ms, operation_duration)
            date_latency_ms_pairs.append(date_latency_ms_pair)
        elif(re.findall(r'price is not coherent', line)):
            aborted_transaction+=1
        elif(re.findall(r'price is coherent', line)):
            successfull_transaction+=1
    
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
    parsed_data["abort_rate"] = aborted_transaction / (aborted_transaction + successfull_transaction) * 100
    print("\t\tAborted transactions percentage: " + str(parsed_data["abort_rate"]))

    return parsed_data


def parse_data_ReadBasket(log_file: list[str]) -> dict:
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

def parse_data_updatePriceDiscount(log_file: list[str]) -> dict:
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


def parse_individual_log(log_file, type_path, log_files_data):
    vus = log_file.split(".")[0] # Get the number of VUs from the log file name
    log_file_path = os.path.join(type_path, log_file)
    if not os.path.isfile(log_file_path):
        print("The file " + log_file_path + " does not exist/ is not a file.")
        return
    # open the file and parse the data
    with open(log_file_path, "r") as f:
        file_lines = f.readlines() # Get all the lines of the file
        parsed_data = parse_data(file_lines)
        log_files_data[vus] = parsed_data


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
    # operation = UPDATE_DISCOUNT_PRICE_FUNC
    # operation = READ_BASKET_FUNC
    operation = READ_BASKET_FUNC
    # operation = VERSION_TESTING_LOW
    # operation = MIX_FUNCS_VERSIONS_HIGH
    # operation = MEMORY_TEST_LOW
    versions = 25
    events = False
    # parsed_data = CollectTestDatafiles(MIX_FUNCS)
    # parsed_data = CollectTestDatafiles(READ_BASKET_FUNC)
    # parsed_data = CollectTestDatafiles(operation, versions)
    parsed_data = CollectDatafiles(operation, versions, events)

    # # Log the abort rate
    # for system_plus_cont in parsed_data:
    #     # Get pairs of throughput and latency values for each test in a list
    #     results = []

    #     for test in parsed_data[system_plus_cont]:
    #         result_tuple = (int(test), parsed_data[system_plus_cont][test])
    #         results.append(result_tuple)

    #     # Sort the list of tuples by the first element of the tuple (throughput)
    #     results.sort(key=lambda tup: tup[0])

    #     print("Test results for " + system_plus_cont + ":")
    #     print("\tP(95) latency: ")
    #     for test_result in results:
    #         print("\t\t" + str(test_result[0]) + " VUs: " + str(test_result[1]["latency"]["p(95)"]) + "ms")

    #     print("\tAbort Rate: ")
    #     for test_result in results:
    #         print("\t\t" + str(test_result[0]) + " VUs: " + str(test_result[1]["abort_rate"]) + "%")
    #     print("\n")

    if operation == UPDATE_DISCOUNT_PRICE_FUNC:
        if not events:
            plot_UPDATE_only_data(parsed_data, versions, log_files_measure)
    elif operation == READ_BASKET_FUNC:
        if not events:
            plot_READ_only_data(parsed_data, versions, log_files_measure)
    elif operation == MIX_FUNCS:
        if not events:
            log_files_measure = "p(95)"
            # log_files_measure = "med"
            plot_MIX_data(parsed_data, versions, log_files_measure)
    elif operation == MIX_FUNCS_VERSIONS_HIGH or operation == MIX_FUNCS_VERSIONS_LOW:
        if not events:
            plot_version_MIX_data(parsed_data, log_files_measure, operation)
    elif operation == MEMORY_TEST_LOW:
        plot_memory_data(parsed_data, log_files_measure)

    # plot_parsed_data(parsed_data, operation, versions, log_files_measure)
    # plot_abort_rate(parsed_data, operation, versions, log_files_measure)
    # plot_abort_rate_versions(parsed_data, operation, log_files_measure)
    # calculate_version_penalty(parsed_data, operation)
    
    calculate_penalty(parsed_data, operation, log_files_measure)
    
if __name__ == '__main__':
    main()