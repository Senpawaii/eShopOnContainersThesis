import requests
import threading
import logging
import os
import datetime
from time import perf_counter_ns

"""	This script is used to test the Catalog.API service. 
    It will update the price on a catalog item with ID 1, while concurrently, Read the contents and Discount of the basket items.
    The script will log the response from the Catalog.API service and the Discount.API service.
    The script will also log the response from the Basket.API service.
"""

catalogServicePort = "5101"
current_directory = os.getcwd()

def ConfigureLoggingSettings():
    # Configure logging settings
    logging_path = os.path.join(current_directory, 'testing_scripts', 'logs')
    os.makedirs(logging_path, exist_ok=True)  # Create the log directory if it doesn't exist
    timestamp = datetime.datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
    log_file_name = f"UpdatePriceAndDiscount_{timestamp}.log"
    log_file_path = os.path.join(logging_path, log_file_name)

    logging.basicConfig(
        level=logging.DEBUG, 
        format='%(asctime)s %(levelname)s %(message)s',
        handlers=[
            logging.FileHandler(log_file_path), # Print to file
            logging.StreamHandler() # Print to console
        ]
    )


def QueryCatalogItemById(id: int) -> dict:
    address = "http://host.docker.internal:" + catalogServicePort + "/catalog-api/api/v1/Catalog/items?ids=" + str(id) + "&interval_low=0&interval_high=0&functionality_ID=func1&timestamp=2025-05-30T14:00:00.0000000Z&tokens=0"
    
    # Log request address
    logging.info("Sending request to address: " + address)

    response = requests.get(address)

    # Log response
    logging.info('Response from Catalog Service: ' + str(response.status_code) + ' ' + str(response.reason))
    return response.json()


# Update Price on Catalog Item with ID 1
def updatePriceOnCatalog(catalogItem: dict, price: int, thread_index: int):
    # Get thread ID
    identity = threading.get_ident()
    
    # Update the price on the catalog item
    catalogItem[0]["price"] = price

     # Get current time in nano seconds precision
    timestamp = datetime.datetime.now().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    address = 'http://host.docker.internal:' + catalogServicePort + '/catalog-api/api/v1/Catalog/items?interval_low=0&interval_high=0&functionality_ID=func' + str(identity) + '&timestamp=' + timestamp + '&tokens=100'

    # Measure time taken to send request with nano seconds precision
    start = perf_counter_ns()

    # Send request
    response = requests.put(address, json=catalogItem[0])

    # Stop timer
    end = perf_counter_ns()
    # Calculate time taken
    timeTaken = end - start

    # Add time taken to list
    timeTakenList[thread_index] = timeTaken
    # Register success if response is 201
    if response.status_code == 201:
        successCount[thread_index] = 1
    #Log response
    logging.info("Thread <" + str(identity) + "> Sent update with price: " + str(price) + ". Time: <" + str(timeTaken) + "> ns. Response: " + str(response.status_code) + " => " + str(response.reason))

numThreads = 30
timeTakenList = [_ for _ in range(numThreads)]
successCount = [0 for _ in range(numThreads)]
def main():
    # Configure logging settings
    ConfigureLoggingSettings()

    # Define Global HTTP Client
    http = requests.Session()

    # Define Global Catalog Item to be used in tests, necessary to fetch Catalog Item Name, Brand ID and Type ID
    catalogItem = QueryCatalogItemById(1)

    # Create 1 thread that updates the price on the catalog item
    threads = []
    for i in range(numThreads):
        price = (i + 1) * 10 
        threads.append(threading.Thread(target=updatePriceOnCatalog, args=(catalogItem, price, i)))
        threads[i].start()
    
    # Wait for all threads to finish
    for i in range(numThreads):
        threads[i].join()

    # Calculate average time taken
    averageTimeTaken = sum(timeTakenList) / len(timeTakenList)
    logging.info("Average time taken: " + str(averageTimeTaken) + " ns (" + str(averageTimeTaken / 1000000000) + " s)")

    # Calculate success rate
    successRate = sum(successCount) / len(successCount)
    logging.info("Success rate: " + str(successRate * 100) + "%")

if __name__ == "__main__":
    # Call main function
    main()