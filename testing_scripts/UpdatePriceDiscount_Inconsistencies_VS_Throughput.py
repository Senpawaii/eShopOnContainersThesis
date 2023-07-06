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
import sys

"""	This script is used to test the Catalog.API service. 
    It will update the price on a catalog item with ID 1, while concurrently, Read the contents and Discount of the basket items.
    The script will log the response from the Catalog.API service and the Discount.API service.
    The script will also log the response from the Basket.API service.
"""
numThreads = 128 # Number of threads to be used in the test
secondsToRun = 20 # Number of seconds to run the test
read_write_ratio = 2 # Scale of 0 to 10, 0 being 100% read, 10 being 100% write
throughput_step = 30 # Number of requests per second to increase throughput by
throughput = 40 # requests per second
max_throughput = 700
contention_rows = 24 # Number of rows to be used in the test
# wrappers = True # True if the test is being run with wrappers, False if the test is being run without wrappers

thesisFrontendPort = "5142"
catalogServicePort = "5101"
discountServicePort = "5140"
basketServicePort = "5103"
webaggregatorServicePort = "5121"

# Store the number of write operations
writeOperationsCount = 0
# Store the number of read operations
readOperationsCount = 0

def configureRootLogger(contention: str, wrappers: str) -> str:
    # Configure root logger
    current_directory = os.path.dirname(os.path.abspath(__file__))
    base_logging_path = os.path.join(current_directory, 'logs')
    os.makedirs(base_logging_path, exist_ok=True)  # Create the log directory if it doesn't exist
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%d_%H-%M-%S")

    if wrappers == "1":
        test_logging_path = os.path.join(base_logging_path, f"Wrap_UpdPriceDiscount_Lat_v_Thrghpt_{contention}_Cont_" + timestamp)
    else:
        test_logging_path = os.path.join(base_logging_path, f"NoWrap_UpdPriceDiscount_Lat_v_Thrghpt_{contention}_Cont_" + timestamp)

    os.makedirs(test_logging_path, exist_ok=True)  # Create the test directory if it doesn't exist
    log_file_name = f"{timestamp}.log" # root log file name
    logging.basicConfig(level=logging.INFO, format='%(asctime)s %(levelname)s %(message)s', filename=os.path.join(test_logging_path, log_file_name), filemode='w')
    return test_logging_path


def ConfigureLoggingSettings(testNum: int, throughput: int, test_logging_path: str) -> str:
    # Configure logging settings
    log_file_name = f"Test<{testNum}> Throughput-{throughput}.log"
    logger_name = f"Test<{testNum}> Throughput-{throughput}"
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


def QueryCatalogItemById(ids: list[id]) -> dict:
    catalogItems = []
    for id in ids:
        address = "http://localhost:" + thesisFrontendPort + "/api/v1/frontend/readcatalogitem/" + str(id)
    
        # Log request address
        logging.info("Sending request to address: " + address)

        response = requests.get(address)

        # Log response
        logging.info('Response from Catalog Service: ' + str(response.status_code) + ' ' + str(response.reason))
        # Log found object
        logging.info("Object queried:" + str(response.json()))

        catalogItems.append(response.json())
    return catalogItems


def QueryDiscountItemById(catalogItems: list[dict]) -> dict:
    identity = threading.get_ident()

    # Access catalog Service to get the brand name and type name

    discountItems = []
    for item in catalogItems:
        # Get the brand name that matches the brand ID
        address = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/catalogbrands'

        # Log request address
        logging.info("Sending request to address: " + address)

        response = requests.get(address)

        brandName = ""
        for brand in response.json():
            if brand["id"] == item["catalogBrandId"]:
                brandName = brand["brand"]

        # Get the type name that matches the type ID
        address = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/catalogtypes'
        
        # Log request address
        logging.info("Sending request to address: " + address)

        response = requests.get(address)

        typeName = ""
        for type in response.json():
            if type["id"] == item["catalogTypeId"]:
                typeName = type["type"]

        itemName = item["name"].replace("&", "%26")
        # Get the discount item that matches the brand name and type name
        address = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/readdiscounts?itemNames=' + itemName  + '&itemBrands=' + brandName + '&itemTypes=' + typeName
        
        # Log request address
        logging.info("Sending request to address: " + address)

        response = requests.get(address)

        # Extract the first (and only) discount item from the response list of discounts
        discountItem = response.json()[0]
        discountItems.append(discountItem)
    return discountItems


def AddCatalogItemToBasket(catalogItem: dict, discountItem: dict, basketID: str):
    # Get the CatalogItemId from the catalogItem
    catalogItemId = catalogItem["id"]
    catalogItemName = catalogItem["name"]

    # Get the ItemBrand and ItemType from the discountItem
    itemBrand = discountItem["itemBrand"]
    itemType = discountItem["itemType"]

    # Create the json body for the request
    body = { 
        "CatalogItemId": catalogItemId, 
        "BasketId": basketID, 
        "Quantity": 1, 
        "CatalogItemName": catalogItemName, 
        "CatalogItemBrandName": itemBrand, 
        "CatalogItemTypeName": itemType 
    }

    address = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/additemtobasket'

    response = requests.post(address, json=body)

    # Ensure that the response is 200
    if response.status_code != 200:
        logging.error("Error adding item to basket. Response from Basket.API: " + str(response.status_code) + " " + str(response.reason))
        return
    return


def readBasket(timeTakenList: dict, successCount: dict, logger: logging.Logger, basketID: str, contention: str):
    # Get the thread identity
    identity = threading.get_ident()

    address = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/readbasket?basketId=' + basketID

    # Measure time taken to send request with nano seconds precision
    start = perf_counter_ns()

    success = False
    while not success:
        try:
            # Send request
            response = requests.get(address)
            if(response.status_code == 200): 
                success = True
        except:
            # Sleep for 10ms
            logging.info("Error reading basket. Retrying in 10ms")
            time.sleep(0.01)
            continue

    # Stop timer
    end = perf_counter_ns()
    # Calculate time taken
    timeTaken = end - start

    # Log the time taken in milliseconds
    logger.info("Read Ops: Time taken: " + str(timeTaken / 1000000) + " milliseconds")

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
    else:
        logger.error("Error reading basket. Response from Basket.API: " + str(response.status_code))
        return
    
    # Extract the basket items from the response
    basketItems = response.json()["items"]

    # Extract the basket item price and discount from the basket items
    basketItemPrice = basketItems[0]["unitPrice"]
    basketItemDiscount = basketItems[0]["discount"]

    #Log response
    logger.log(logging.INFO, 'Thread <' + str(identity) + '> ' + 'Read Basket <' + basketID + '>: Price {' + str(basketItemPrice) + '}, Discount: {' + str(basketItemDiscount) + '}')
    return


def writeOperations(catalogItem: dict, discountItem: dict, timeTakenList: dict, successCount: dict, logger: logging.Logger):
    # Execute write operations: update price and discount

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

    # # Update price on catalog item
    # updatePriceOnCatalog(catalogItem, price, timeTakenList, successCount, logger)
    
    # # Update discount on discount item
    # updateDiscount(discountItem, discount, timeTakenList, successCount, logger)
    
    # Update the price and discount on the catalog item and discount item
    updatePriceAndDiscount(catalogItem, discountItem, price, discount, timeTakenList, successCount, logger)

    return


def updatePriceAndDiscount(catalogItem: dict, discountItem: dict, price: int, discount: int, timeTakenList: dict, successCount: dict, logger: logging.Logger):
    # Contact the frontend service and update the price and discount of the item

    # Update the price on the catalog item
    catalogItem["price"] = price

    # update the discount value on the discount item
    discountItem["discountValue"] = discount

    # Build the payload
    payload = {
        "CatalogItem": catalogItem,
        "DiscountItem": discountItem, 
    }

    address = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/updatepricediscount'

    # Measure time taken to send request with nano seconds precision
    start = perf_counter_ns()

    success = False
    while not success:
        try:
            # Send request
            response = requests.put(address, json=payload)
            if(response.status_code == 200): 
                success = True
        except:
            # Sleep for 10ms
            logging.info("Error updating price and discount Status_Code:"+ response.status_code +". Retrying in 10ms")
            time.sleep(0.01)
            continue

    # Stop timer
    end = perf_counter_ns()

    # Calculate time taken
    timeTaken = end - start

    # Log the time taken in milliseconds
    logger.info("Write Ops: Time taken: " + str(timeTaken / 1000000) + " milliseconds")

    # Get thread ID
    identity = threading.get_ident()

    # Add time taken to list
    if identity not in timeTakenList:
        timeTakenList[identity] = [timeTaken]
    else:
        timeTakenList[identity].append(timeTaken)

    # Register success if response is 201
    if response.status_code == 200:
        if identity not in successCount:
            successCount[identity] = 1
        else:
            successCount[identity] += 1
    else:
        logger.error("Error updating price on catalog item. Response from Catalog.API: " + str(response.status_code))
        return


def assign_operations(executor: ThreadPoolExecutor, futuresThreads: list, catalogItems: list[dict], discountItems: list[dict], read_write_list: list, timeTakenList: dict, successCount: dict, secondsToRun: int, logger: logging.Logger, throughput: int, basket_IDs_assigned: dict, contention: str):
    request_interval = 1 / throughput
    global readOperationsCount
    global writeOperationsCount
    total_active_time = 0
    # nanossecs_to_run = secondsToRun * 1000000000
    start_test_time = time.time()

    while time.time() < start_test_time + secondsToRun:
        # Get a random index from the list of catalog items and discount items
        index = random.randint(0, len(catalogItems) - 1)
        # Assign read/write operations to thread based on read_write_ratio
        random_choice = random.choice(read_write_list)
        if random_choice == 0:
            # Read operation
            # readOperationsCount += 1
            future = executor.submit(readBasket, timeTakenList, successCount, logger, f"basket{index}", contention)
            
            futuresThreads.append(future)
            # Limit the throughput to throughtput request per second
            time.sleep(request_interval)
        else:
            # Write operation
            # writeOperationsCount += 1
            future = executor.submit(writeOperations, copy.deepcopy(catalogItems[index]), copy.deepcopy(discountItems[index]), timeTakenList, successCount, logger)
            futuresThreads.append(future)
            # Limit the throughput to 2 * throughtput request per second: 1 for catalogpPriceUpdate and 1 for discountUpdate
            time.sleep(request_interval)
        # future = executor.submit(microDiscountTest)
        # time.sleep(request_interval)

    for future in futuresThreads:
        future.cancel()
    wait(futuresThreads, return_when=ALL_COMPLETED)


def check_discount_from_log_file(file_path):
    pattern = r'Price {([\d.]+)}, Discount: {([\d.]+)}'
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


# Create predefined list of prices and discounts to be used in tests equal to the number of threads
prices = [10000000 * (i+1) for i in range(numThreads)]
discounts = [prices[i] // 10 for i in range(numThreads)]
basket_IDs = ["basket" + str(i) for i in range(numThreads)]

thread_price_discount = {}


def main():
    contention = sys.argv[1] # 0 for low contention, 1 high contention
    wrappers = sys.argv[2] # 0 for no wrappers, 1 for wrappers
    test_logging_path = configureRootLogger(contention, wrappers)
    # microDiscountTest()
    # Define Global HTTP Client
    http = requests.Session()

    # Define Global Catalog Item to be used in tests, necessary to fetch Catalog Item Name, Brand ID and Type ID
    if(contention == "0"):
        # low contention
        catalogItemIDs = [i for i in range(1, contention_rows + 1)]
    else:
        # high contention
        catalogItemIDs = [1]
    
    # Get the catalog items with the IDs defined above
    catalogItems = QueryCatalogItemById(catalogItemIDs)
    # Get the discount items that match the catalog items
    discountItems = QueryDiscountItemById(catalogItems)
    
    # Add each pair of catalog item and discount item to the basket
    for index, (catalogItem, discountItem) in enumerate(zip(catalogItems, discountItems)):
        AddCatalogItemToBasket(catalogItem, discountItem, basket_IDs[index])

    # Store the results of the tests for each read/write ratio
    resultsList = []

    # Store the lines of anomalies for each read/write ratio
    anomalyLinePresenceList = []
    
    executor = ThreadPoolExecutor(max_workers=numThreads)  
    
    # while the throughput is less than 130
    global throughput
    testNum = 0
    while throughput <= max_throughput:
        testNum += 1
        
        # Configure logging settings for each read/write ratio test
        logger, log_file = ConfigureLoggingSettings(testNum, throughput, test_logging_path)
        logger.log(logging.INFO, "Logging")

        read_ratio = (10 - read_write_ratio) * 10
        write_ratio = 100 - read_ratio 
        
                     
        # Create a list for chances of read/write operations
        read_write_list = [1 for _ in range(read_write_ratio)] + [0 for _ in range(10 - read_write_ratio)]
        
        # Create a single dictionary with an entry for each thread. Each thread is assigned a list of time taken and success count
        timeTakenList = {}
        successCount = {}

        # Create a dictionary of basket ID assigned to each thread
        basket_IDs_assigned = {}

        global readOperationsCount
        global writeOperationsCount
        readOperationsCount = 0
        writeOperationsCount = 0

        # Create a pool of futures
        futuresThreads = []

        # Create new Thread for assigning operations
        assign_operations_thread = threading.Thread(target=assign_operations, args=(executor, futuresThreads, catalogItems, discountItems, read_write_list, timeTakenList, successCount, secondsToRun, logger, throughput, basket_IDs_assigned, contention))
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
        logger.info("Functionalities per second: " + str(total_success_count / (secondsToRun)))

        results, anomaly_line_presence = check_discount_from_log_file(log_file)
        resultsList.append(results)
        anomalyLinePresenceList.append(anomaly_line_presence)
        # Log the results
        logger.info("Results: " + str(results['OK']) + " OK Reads, " + str(results['anomalies']) + " Anomalies")
        if results['anomalies'] > 0:
            for line in anomaly_line_presence:
                logger.info(line)
        # logger.info("Anomalies ratio: " + str((results['anomalies'] / readOperationsCount) * 100) + "% (" + str(results['anomalies']) + "/" + str(readOperationsCount) + ")")
        logger.info("=========================================")

        throughput += throughput_step

    # Shutdown executor
    executor.shutdown(wait=True)


if __name__ == "__main__":
    # Call main function
    main()
