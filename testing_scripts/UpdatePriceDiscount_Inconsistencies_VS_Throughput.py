from concurrent.futures import ThreadPoolExecutor, wait, ALL_COMPLETED
import random
import re
import string
import time
import requests
import threading
import logging
import os
import datetime
import copy
from time import perf_counter_ns
import timeit
# from ratelimiter import RateLimiter, sleep_and_retry, limits

"""	This script is used to test the Catalog.API service. 
    It will update the price on a catalog item with ID 1, while concurrently, Read the contents and Discount of the basket items.
    The script will log the response from the Catalog.API service and the Discount.API service.
    The script will also log the response from the Basket.API service.
"""
numThreads = 32 # Number of threads to be used in the test
secondsToRun = 60 # Number of seconds to run the test
read_write_ratio = 2 # Scale of 0 to 10, 0 being 100% read, 10 being 100% write
throughput_step = 5
throughput = 40 # requests per second
max_throughput = 150
wrappers = True # True if the test is being run with wrappers, False if the test is being run without wrappers

catalogServicePort = "5101"
discountServicePort = "5140"
basketServicePort = "5103"
webaggregatorServicePort = "5121"
current_directory = os.path.dirname(os.path.abspath(__file__))

# Store the number of write operations
writeLock = threading.Lock()
writeOperationsCount = 0
# Store the number of read operations
readLock = threading.Lock()
readOperationsCount = 0

# Configure root logger
base_logging_path = os.path.join(current_directory, 'logs')
os.makedirs(base_logging_path, exist_ok=True)  # Create the log directory if it doesn't exist
timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%d_%H-%M-%S")
if wrappers:
    test_logging_path = os.path.join(base_logging_path, "Wrapper_UpdatePriceAndDiscount_Inconsistencies_VS_Throughput_" + timestamp)
else:
    test_logging_path = os.path.join(base_logging_path, "NoWrapper_UpdatePriceAndDiscount_Inconsistencies_VS_Throughput_" + timestamp)
os.makedirs(test_logging_path, exist_ok=True)  # Create the test directory if it doesn't exist
log_file_name = f"{timestamp}.log" # root log file name
logging.basicConfig(level=logging.INFO, format='%(asctime)s %(levelname)s %(message)s', filename=os.path.join(test_logging_path, log_file_name), filemode='w')


def ConfigureLoggingSettings(throughput: int) -> str:
    # Configure logging settings
    log_file_name = f"Throughput-{throughput}.log"
    logger_name = f"Throughput-{throughput}"
    log_file_path = os.path.join(test_logging_path, log_file_name)

    logger = logging.getLogger(logger_name)
    logger.setLevel(logging.INFO)
    formatter = logging.Formatter('%(asctime)s %(levelname)s %(message)s')

    file_handler = logging.FileHandler(log_file_path)
    file_handler.setFormatter(formatter)
    logger.addHandler(file_handler)

    stream_handler = logging.StreamHandler()
    stream_handler.setFormatter(formatter)
    logger.addHandler(stream_handler)

    return logger, log_file_path


def QueryCatalogItemById(id: int) -> dict:
    address = "http://localhost:" + catalogServicePort + "/catalog-api/api/v1/Catalog/items?ids=" + str(id) + "&interval_low=0&interval_high=0&functionality_ID=func1&timestamp=2025-05-30T14:00:00.0000000Z&tokens=0"
    
    # Log request address
    logging.info("Sending request to address: " + address)

    response = requests.get(address)

    # Log response
    logging.info('Response from Catalog Service: ' + str(response.status_code) + ' ' + str(response.reason))
    # Log found object
    logging.info("Object queried:" + str(response.json()))
    return response.json()


def QueryDiscountItemById(catalogItem: dict) -> dict:
    identity = threading.get_ident()

    # Access catalog Service to get the brand name and type name
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    # Get the brand name that matches the brand ID
    address = 'http://localhost:' + catalogServicePort + '/catalog-api/api/v1/Catalog/CatalogBrands?interval_low=0&interval_high=0&functionality_ID=func' + str(identity) + '&timestamp=' + timestamp + '&tokens=0'

    # Log request address
    logging.info("Sending request to address: " + address)

    response = requests.get(address)

    brandName = ""
    for brand in response.json():
        if brand["id"] == catalogItem[0]["catalogBrandId"]:
            brandName = brand["brand"]

    # Get the type name that matches the type ID
    address = 'http://localhost:' + catalogServicePort + '/catalog-api/api/v1/Catalog/CatalogTypes?interval_low=0&interval_high=0&functionality_ID=func' + str(identity) + '&timestamp=' + timestamp + '&tokens=0'
    
    # Log request address
    logging.info("Sending request to address: " + address)

    response = requests.get(address)

    typeName = ""
    for type in response.json():
        if type["id"] == catalogItem[0]["catalogTypeId"]:
            typeName = type["type"]

    itemName = catalogItem[0]["name"]
    # Get the discount item that matches the brand name and type name
    address = 'http://localhost:' + discountServicePort + '/discount-api/api/v1/Discount/discounts?itemNames=' + itemName  + '&itemBrands=' + brandName + '&itemTypes=' + typeName + '&functionality_ID=func' + str(identity) + '&timestamp=' + timestamp + '&tokens=0'
    
    # Log request address
    logging.info("Sending request to address: " + address)

    response = requests.get(address)

    # Extract the first (and only) discount item from the response list of discounts
    discountItem = response.json()[0]

    return discountItem


def AddCatalogItemToBasket(catalogItem: dict, discountItem: dict):
    # Get the CatalogItemId from the catalogItem
    catalogItemId = catalogItem[0]["id"]
    catalogItemName = catalogItem[0]["name"]

    # Get the ItemBrand and ItemType from the discountItem
    itemBrand = discountItem["itemBrand"]
    itemType = discountItem["itemType"]

    # Create the json body for the request
    body = { "catalogItemId": catalogItemId, "basketId": "e5d06a2d-fc81-4051-8f30-0a85836eac70", "quantity": 1, "CatalogItemName": catalogItemName, "CatalogItemBrandName": itemBrand, "CatalogItemTypeName": itemType }

    address = 'http://localhost:' + webaggregatorServicePort + '/api/v1/Basket/items'

    response = requests.post(address, json=body)

    # Ensure that the response is 200
    if response.status_code != 200:
        logging.error("Error adding item to basket. Response from Basket.API: " + str(response.status_code) + " " + str(response.reason))
        return
    return


def readBasket(timeTakenList: dict, successCount: dict, logger: logging.Logger):
    basketID = "e5d06a2d-fc81-4051-8f30-0a85836eac70"

    # Get the thread identity
    identity = threading.get_ident()

    funcID = ''.join(random.choices(string.ascii_lowercase, k=10))

    # Get current time in nano seconds precision
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    address = 'http://localhost:' + basketServicePort + '/api/v1/Basket/' + basketID + '?interval_low=0&interval_high=0&functionality_ID=func' + funcID + '&timestamp=' + timestamp + '&tokens=0'

    # Measure time taken to send request with nano seconds precision
    start = perf_counter_ns()

    # Send request
    response = requests.get(address)

    # Stop timer
    end = perf_counter_ns()
    # Calculate time taken
    timeTaken = end - start

    # Add time taken to list
    if identity not in timeTakenList:
        timeTakenList[identity] = [timeTaken]
    else:
        timeTakenList[identity].append(timeTaken)

    # Register success if response is 200
    if response.status_code == 200:
        if identity not in successCount:
            successCount[identity] = 1
        else:
            successCount[identity] += 1
    
    # Extract the basket items from the response
    basketItems = response.json()["items"]

    # Extract the basket item price and discount from the basket items
    basketItemPrice = basketItems[0]["unitPrice"]
    basketItemDiscount = basketItems[0]["discount"]

    #Log response
    logger.info('Thread <' + str(identity) + '> FuncID: <' + funcID + '> ' + 'Read Basket: Price {' + str(basketItemPrice) + '}, Discount: {' + str(basketItemDiscount) + '}, Timestamp: ' + timestamp)
    return


def writeOperations(catalogItem: dict, discountItem: dict, timeTakenList: dict, successCount: dict, logger: logging.Logger):
    # Execute write operations: update price and discount

    # Generate a random 16 bit random string
    funcID = ''.join(random.choices(string.ascii_lowercase, k=10))


    thread_identity = threading.get_ident()

    # Check if the thread has already a pair price and discount already assigned in the dicionary of key/value: thread_id/(price, discount)
    if thread_identity not in thread_price_discount:
            # Pick a random price and discount from the predefined list and remove it from the list of predefined prices and discounts
            price, discount = prices.pop(), discounts.pop()
            # Add the price and discount to the dictionary of key/value: thread_id/(price, discount)
            thread_price_discount[thread_identity] = (price, discount)
    else:
        # Update the price and discount in the dictionary of key/value: thread_id/(price, discount)
        price, discount = thread_price_discount[thread_identity]
        thread_price_discount[thread_identity] = (price + 10, discount + 1)
        # Get the price and discount already assigned to the thread
        price, discount = thread_price_discount[thread_identity]

    # logger.info('Executing write operations with thread: ' + str(thread_identity) + ' and price/discount: ' + str(price) + '/' + str(discount))

    # Update price on catalog item
    updatePriceOnCatalog(catalogItem, price, funcID, timeTakenList, successCount, logger)
    
    # Update discount on discount item
    updateDiscount(discountItem, discount, funcID, timeTakenList, successCount, logger)
    return


# Update Price on Catalog Item with ID 1
def updatePriceOnCatalog(catalogItem: dict, price: int, funcID: str, timeTakenList: dict, successCount: dict, logger: logging.Logger):
    # Get thread ID
    identity = threading.get_ident()
    
    # Update the price on the catalog item
    catalogItem[0]["price"] = price

     # Get current time in nano seconds precision
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    address = 'http://localhost:' + catalogServicePort + '/catalog-api/api/v1/Catalog/items?interval_low=0&interval_high=0&functionality_ID=func' + funcID + '&timestamp=' + timestamp + '&tokens=50'

    # Measure time taken to send request with nano seconds precision
    start = perf_counter_ns()

    # Send request
    response = requests.put(address, json=catalogItem[0])

    # Stop timer
    end = perf_counter_ns()
    # Calculate time taken
    timeTaken = end - start

    # Add time taken to list
    if identity not in timeTakenList:
        timeTakenList[identity] = [timeTaken]
    else:
        timeTakenList[identity].append(timeTaken)

    # Register success if response is 201
    if response.status_code == 201:
        if identity not in successCount:
            successCount[identity] = 1
        else:
            successCount[identity] += 1
    #Log response
    # logger.info("Thread <" + str(identity) + "> FuncID: <" + funcID + "> " + "PriceUpdate: " + str(catalogItem[0]["price"]) + ". Time: <" + str(timeTaken) + "> ns. Response: " + str(response.status_code) + " => " + str(response.reason))


# Update Discount on Item with ID 1
def updateDiscount(discountItem: dict, discount: int, funcID: str, timeTakenList: dict, successCount: dict, logger: logging.Logger):
    # Get thread ID
    identity = threading.get_ident()
    # logger.info("Thread <" + str(identity) + "> FuncID: <" + funcID + "> " + "DiscountUpdate: " + str(discount) + ".")
    # Update the discount value on the discount item
    discountItem["discountValue"] = discount

     # Get current time in nano seconds precision
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    address = 'http://localhost:' + discountServicePort + '/discount-api/api/v1/Discount/discounts?interval_low=0&interval_high=0&functionality_ID=func' + funcID + '&timestamp=' + timestamp + '&tokens=50'

    # Measure time taken to send request with nano seconds precision
    start = perf_counter_ns()

    # Send request
    response = requests.put(address, json=discountItem)

    # Stop timer
    end = perf_counter_ns()
    # Calculate time taken
    timeTaken = end - start

    # Add time taken to list
    if identity not in timeTakenList:
        timeTakenList[identity] = [timeTaken]
    else:
        timeTakenList[identity].append(timeTaken)

    # Register success if response is 201
    if response.status_code == 201:
        if identity not in successCount:
            successCount[identity] = 1
        else:
            successCount[identity] += 1
    #Log response
    # logger.info("Thread <" + str(identity) + "> FuncID: <" + funcID + "> " + "DiscountUpdate: " + str(discountItem["discountValue"]) + ". Time: <" + str(timeTaken) + "> ns. Response: " + str(response.status_code) + " => " + str(response.reason))



def assign_operations(executor: ThreadPoolExecutor, futuresThreads: list, catalogItem: dict, discountItem: dict, read_write_list: list, timeTakenList: dict, successCount: dict, secondsToRun: int, logger: logging.Logger, throughput: int):
    request_interval = 1 / throughput
    global readOperationsCount
    global writeOperationsCount
    total_active_time = 0
    # nanossecs_to_run = secondsToRun * 1000000000
    start_test_time = time.time()

    while time.time() < start_test_time + secondsToRun:
        # Assign read/write operations to thread based on read_write_ratio
        if random.choice(read_write_list) == 0:
            # Read operation
            readOperationsCount += 1
            future = executor.submit(readBasket, timeTakenList, successCount, logger)
            
            futuresThreads.append(future)
            # Limit the throughput to throughtput request per second
            time.sleep(request_interval)
        else:
            # Write operation
            writeOperationsCount += 1
            future = executor.submit(writeOperations, copy.deepcopy(catalogItem), copy.deepcopy(discountItem), timeTakenList, successCount, logger)
            futuresThreads.append(future)
            # Limit the throughput to 2 * throughtput request per second: 1 for catalogpPriceUpdate and 1 for discountUpdate
            time.sleep(2 * request_interval)

    for future in futuresThreads:
        future.cancel()
    wait(futuresThreads, return_when=ALL_COMPLETED)


# @sleep_and_retry
# @limits(calls=50, period=1)
# def operations_balancer(read_write_list: list, timeTakenList: dict, successCount: dict, logger: logging.Logger):
#     if random.choice(read_write_list) == 0:
#         # Read operation
#         readBasket(timeTakenList, successCount, logger)

#         global readOperationsCount
#         readLock.acquire()
#         try:
#             # Read operation
#             readOperationsCount += 1
#         finally:
#             readLock.release()



def check_discount_from_log_file(file_path):
    pattern = r'Read Basket: Price {([\d.]+)}, Discount: {([\d.]+)}'
    results = {'OK': 0, 'anomalies': 0}
    anomaly_line_presence = []

    with open(file_path, 'r') as file:
        for line in file:
            # Using regex, check if the line matches the pattern
            match = re.search(pattern, line)

            # If the line matches the pattern, check if the discount is 10% of the price
            if match:
                price = float(match.group(1))
                discount = float(match.group(2))
                if discount == price * 0.1:
                    results['OK'] += 1
                else:
                    results['anomalies'] += 1
                    anomaly_line_presence.append(line)

    return results, anomaly_line_presence


# def generatePlots():


# Create predefined list of prices and discounts to be used in tests equal to the number of threads
prices = [10000000 * (i+1) for i in range(numThreads)]
discounts = [prices[i] // 10 for i in range(numThreads)]
thread_price_discount = {}

def main():
    # Define Global HTTP Client
    http = requests.Session()

    # Define Global Catalog Item to be used in tests, necessary to fetch Catalog Item Name, Brand ID and Type ID
    catalogItem = QueryCatalogItemById(1)
    discountItem = QueryDiscountItemById(catalogItem)
    
    # Add the catalog Item to the Basket to be used in tests
    AddCatalogItemToBasket(catalogItem, discountItem)

    # Store the results of the tests for each read/write ratio
    resultsList = []

    # Store the lines of anomalies for each read/write ratio
    anomalyLinePresenceList = []
    
    executor = ThreadPoolExecutor(max_workers=numThreads)  
    
    # while the throughput is less than 130
    global throughput
    while throughput <= max_throughput:
        # Create a thread pool with numThreads threads
        
        # Configure logging settings for each read/write ratio test
        logger, log_file = ConfigureLoggingSettings(throughput)
        
        read_ratio = (10 - read_write_ratio) * 10
        write_ratio = 100 - read_ratio 
        
                     
        # Create a list for chances of read/write operations
        read_write_list = [1 for _ in range(read_write_ratio)] + [0 for _ in range(10 - read_write_ratio)]
        
        # Create a single dictionary with an entry for each thread. Each thread is assigned a list of time taken and success count
        timeTakenList = {}
        successCount = {}

        global readOperationsCount
        global writeOperationsCount
        readOperationsCount = 0
        writeOperationsCount = 0

        # Create a pool of futures
        futuresThreads = []

        # Create new Thread for assigning operations
        assign_operations_thread = threading.Thread(target=assign_operations, args=(executor, futuresThreads, catalogItem, discountItem, read_write_list, timeTakenList, successCount, secondsToRun, logger, throughput))
        # Start thread
        assign_operations_thread.start()
        # Wait for thread to finish
        assign_operations_thread.join()
        

        # Open RESULTS log tag
        logger.info("-------------TEST RESULTS-------------")
        logger.info("Throughput: " + str(throughput) + " req/sec.")
        logger.info("Read: " + str(read_ratio) + "%, Write: " + str(write_ratio) + "%")

        # Calculate total time taken
        total_active_time_taken = sum([sum(timeTakenList[i]) for i in timeTakenList])

        # Log total times
        logger.info("Total test time: " + str(secondsToRun) + " seconds")
        logger.info("Total active time taken: " + str(total_active_time_taken / 1000000000) + " seconds")

        # Calculate average time taken by each request
        total_requests = sum([len(timeTakenList[i]) for i in timeTakenList])
        averageTimeTaken = total_active_time_taken / total_requests

        # Log average time taken by each request
        logger.info("Average time/req: " + str(averageTimeTaken / 1000000) + " milliseconds")

        logger.info("Total number of requests: " + str(total_requests))
    
        # Calculate the calculated throughput
        answered_requests_throughput = total_requests / (secondsToRun)

        # Log the calculated throughput
        logger.info("Answered Requests throughput: " + str(answered_requests_throughput) + " req/sec.")

        # Log the number of each type of request
        logger.info("Number of read operations: " + str(readOperationsCount) + ", Number of write operations: " + str(writeOperationsCount))

        # Calculate success rate and total number of operations
        total_success_count = sum([successCount[i] for i in successCount])
        successRate = total_success_count / total_requests

        # Log success rate and total number of operations
        logger.info("Success rate: " + str(successRate * 100) + "% - (" + str(sum([successCount[i] for i in successCount])) + "/" + str(total_requests) + ")")

        # Log the average functionalities per seconds
        logger.info("Functionalities per second: " + str((readOperationsCount + writeOperationsCount) / (secondsToRun)))

        results, anomaly_line_presence = check_discount_from_log_file(log_file)
        resultsList.append(results)
        anomalyLinePresenceList.append(anomaly_line_presence)
        # Log the results
        logger.info("Results: " + str(results['OK']) + " OK Reads, " + str(results['anomalies']) + " Anomalies")
        if results['anomalies'] > 0:
            for line in anomaly_line_presence:
                logger.info(line)
        logger.info("Anomalies ratio: " + str((results['anomalies'] / readOperationsCount) * 100) + "% (" + str(results['anomalies']) + "/" + str(readOperationsCount) + ")")
        logger.info("=========================================")

        throughput += throughput_step

    # Shutdown executor
    executor.shutdown(wait=True)

    # generatePlots()

    # logging.info("Average time taken for each read/write ratio:")
    # for i in range(len(read_write_ratio)):
    #     logging.info(str(read_write_ratio[i]) + "/10: " + str(averageTimeTakenList[i]) + " ns")
    #     logging.info("Success rate: " + str(successRatioList[i] * 100) + "%" + str(sum(successRatioList[j] for j in successRatioList[i])) + "/" + str(totalRequestsList[i]) + ")")
    #     logging.info("Requests per second: " + str(requestsPerSecond[i]))
    #     logging.info("Total time taken: " + str(totalTimeTakenList[i]) + " s")
    #     logging.info("Total requests: " + str(totalRequestsList[i]))
    #     logging.info("Success count: " + str(successCountList[i]))
    #     logging.info("Results: " + str(resultsList[i]['OK']) + " OK, " + str(resultsList[i]['anomalies']) + " anomalies")
    #     if resultsList[i]['anomalies'] > 0:
    #         for line in anomalyLinePresenceList[i]:
    #             logging.info(line, end='')
        
    #     logging.info("Anomalies ratio: " + str(resultsList[i]['anomalies'] / (resultsList[i]['OK'] + resultsList[i]['anomalies']) * 100) + "%")


if __name__ == "__main__":
    # Call main function
    main()