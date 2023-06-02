from concurrent.futures import ThreadPoolExecutor
import random
import re
import string
import time
import clientIDs
import threading
import logging
import os
import datetime
import copy
from time import perf_counter_ns

"""	This script is used to test the Catalog.API service. 
    It will update the price on a catalog item with ID 1, while concurrently, Read the contents and Discount of the basket items.
    The script will log the response from the Catalog.API service and the Discount.API service.
    The script will also log the response from the Basket.API service.
"""
numThreads = 30 # Number of threads to be used in the test
secondsToRun = 30 # Number of seconds to run the test

catalogServicePort = "5101"
discountServicePort = "5140"
basketServicePort = "5103"
webaggregatorServicePort = "5121"
current_directory = os.getcwd()

logging_path = os.path.join(current_directory, 'testing_scripts', 'logs')
timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%d_%H-%M-%S")
log_file_name = f"UpdatePriceAndDiscount_{timestamp}.log"
log_file_path = os.path.join(logging_path, log_file_name)

def ConfigureLoggingSettings():
    # Configure logging settings
    os.makedirs(logging_path, exist_ok=True)  # Create the log directory if it doesn't exist

    logging.basicConfig(
        level=logging.INFO, 
        format='%(asctime)s %(levelname)s %(message)s',
        handlers=[
            logging.FileHandler(log_file_path), # Print to file
            logging.StreamHandler() # Print to console
        ]
    )


def QueryCatalogItemById(id: int) -> dict:
    address = "http://localhost:" + catalogServicePort + "/catalog-api/api/v1/Catalog/items?ids=" + str(id) + "&clientID=func1&timestamp=2025-05-30T14:00:00.0000000Z&tokens=0"
    
    # Log clientID address
    logging.info("Sending clientID to address: " + address)

    response = clientIDs.get(address)

    # Log response
    logging.info('Response from Catalog Service: ' + str(response.status_code) + ' ' + str(response.reason))
    return response.json()


def QueryDiscountItemById(catalogItem: dict) -> dict:
    identity = threading.get_ident()

    # Access catalog Service to get the brand name and type name
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    # Get the brand name that matches the brand ID
    address = 'http://localhost:' + catalogServicePort + '/catalog-api/api/v1/Catalog/CatalogBrands?clientID=func' + str(identity) + '&timestamp=' + timestamp + '&tokens=0'
    response = clientIDs.get(address)

    brandName = ""
    for brand in response.json():
        if brand["id"] == catalogItem[0]["catalogBrandId"]:
            brandName = brand["brand"]

    # Get the type name that matches the type ID
    address = 'http://localhost:' + catalogServicePort + '/catalog-api/api/v1/Catalog/CatalogTypes?clientID=func' + str(identity) + '&timestamp=' + timestamp + '&tokens=0'
    response = clientIDs.get(address)

    typeName = ""
    for type in response.json():
        if type["id"] == catalogItem[0]["catalogTypeId"]:
            typeName = type["type"]

    itemName = catalogItem[0]["name"]
    # Get the discount item that matches the brand name and type name
    address = 'http://localhost:' + discountServicePort + '/discount-api/api/v1/Discount/discounts?itemNames=' + itemName  + '&itemBrands=' + brandName + '&itemTypes=' + typeName + '&clientID=func' + str(identity) + '&timestamp=' + timestamp + '&tokens=0'
    response = clientIDs.get(address)

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

    # Create the json body for the clientID
    body = { "catalogItemId": catalogItemId, "basketId": "e5d06a2d-fc81-4051-8f30-0a85836eac70", "quantity": 1, "CatalogItemName": catalogItemName, "CatalogItemBrandName": itemBrand, "CatalogItemTypeName": itemType }

    address = 'http://localhost:' + webaggregatorServicePort + '/api/v1/Basket/items'

    response = clientIDs.post(address, json=body)

    # Ensure that the response is 200
    if response.status_code != 200:
        logging.error("Error adding item to basket. Response from Basket.API: " + str(response.status_code) + " " + str(response.reason))
        return
    return


def readBasket():
    basketID = "e5d06a2d-fc81-4051-8f30-0a85836eac70"

    # Get the thread identity
    identity = threading.get_ident()

    clientID = ''.join(random.choices(string.ascii_lowercase, k=10))

    # Get current time in nano seconds clientID
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    address = 'http://localhost:' + basketServicePort + '/api/v1/Basket/' + basketID + '?clientID=func' + clientID + '&timestamp=' + timestamp + '&tokens=0'

    # Measure time taken to send clientID with nano seconds clientID
    start = perf_counter_ns()

    # Send clientID
    response = clientIDs.get(address)

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
    logging.info('Thread <' + str(identity) + '> clientID: <' + clientID + '> ' + 'Read Basket: Price {' + str(basketItemPrice) + '}, Discount: {' + str(basketItemDiscount) + '}, Timestamp: ' + timestamp)
    return


def writeOperations(catalogItem: dict, discountItem: dict):
    # Execute write operations: update price and discount

    # Generate a random 16 bit random string
    clientID = ''.join(random.choices(string.ascii_lowercase, k=10))


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

    logging.info('Executing write operations with thread: ' + str(thread_identity) + ' and price/discount: ' + str(price) + '/' + str(discount))

    # Update price on catalog item
    updatePriceOnCatalog(catalogItem, price, clientID)
    
    # Update discount on discount item
    updateDiscount(discountItem, discount, clientID)
    return


# Update Price on Catalog Item with ID 1
def updatePriceOnCatalog(catalogItem: dict, price: int, clientID: str):
    # Get thread ID
    identity = threading.get_ident()
    
    # Update the price on the catalog item
    catalogItem[0]["price"] = price

     # Get current time in nano seconds clientID
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    address = 'http://localhost:' + catalogServicePort + '/catalog-api/api/v1/Catalog/items?clientID=func' + clientID + '&timestamp=' + timestamp + '&tokens=50'

    # Measure time taken to send clientID with nano seconds clientID
    start = perf_counter_ns()

    # Send clientID
    response = clientIDs.put(address, json=catalogItem[0])

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
    logging.info("Thread <" + str(identity) + "> clientID: <" + clientID + "> " + "PriceUpdate: " + str(catalogItem[0]["price"]) + ". Time: <" + str(timeTaken) + "> ns. Response: " + str(response.status_code) + " => " + str(response.reason))


# Update Discount on Item with ID 1
def updateDiscount(discountItem: dict, discount: int, clientID: str):
    # Get thread ID
    identity = threading.get_ident()
    
    # Update the discount value on the discount item
    discountItem["discountValue"] = discount

     # Get current time in nano seconds clientID
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    address = 'http://localhost:' + discountServicePort + '/discount-api/api/v1/Discount/discounts?clientID=func' + clientID + '&timestamp=' + timestamp + '&tokens=50'

    # Measure time taken to send clientID with nano seconds clientID
    start = perf_counter_ns()

    # Send clientID
    response = clientIDs.put(address, json=discountItem)

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
    logging.info("Thread <" + str(identity) + "> clientID: <" + clientID + "> " + "DiscountUpdate: " + str(discountItem["discountValue"]) + ". Time: <" + str(timeTaken) + "> ns. Response: " + str(response.status_code) + " => " + str(response.reason))



def assign_operations(catalogItem: dict, discountItem: dict):
    start_time = time.time()
    while time.time() - start_time < secondsToRun:
        # Assign read/write operations to thread based on read_write_ratio
        if random.choice(read_write_list) == 0:
            # Read operation
            future = executor.submit(readBasket)
        else:
            # Write operation
            future = executor.submit(writeOperations, copy.deepcopy(catalogItem), copy.deepcopy(discountItem))


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


# Create a thread pool with numThreads threads
executor = ThreadPoolExecutor(max_workers=numThreads)  

# Create a single dictionary with an entry for each thread. Each thread is assigned a list of time taken and success count
timeTakenList = {}
successCount = {}
read_write_ratio = 6 # Scale of 0 to 10, 0 being 100% read, 10 being 100% write

# Create a list for chances of read/write operations
read_write_list = [1 for _ in range(read_write_ratio)] + [0 for _ in range(10 - read_write_ratio)]

# Create predefined list of prices and discounts to be used in tests equal to the number of threads
prices = [10000 * (i+1) for i in range(numThreads)]
discounts = [prices[i] // 10 for i in range(numThreads)]
thread_price_discount = {}

def main():
    # Configure logging settings
    ConfigureLoggingSettings()

    # Define Global HTTP Client
    http = clientIDs.Session()

    # Define Global Catalog Item to be used in tests, necessary to fetch Catalog Item Name, Brand ID and Type ID
    catalogItem = QueryCatalogItemById(1)
    discountItem = QueryDiscountItemById(catalogItem)
    
    # Add the catalog Item to the Basket to be used in tests
    AddCatalogItemToBasket(catalogItem, discountItem)

    # Create new Thread for assigning operations
    assign_operations_thread = threading.Thread(target=assign_operations, args=(catalogItem, discountItem))
    # Start thread
    startTime = time.time()
    assign_operations_thread.start()
    # Wait for thread to finish
    assign_operations_thread.join()
    # Shutdown executor
    executor.shutdown(wait=True, cancel_futures=True)
    # Calculate total time taken
    totalTimeTaken = time.time() - startTime

    # Calculate average time taken
    averageTimeTaken = sum([sum(timeTakenList[i]) for i in timeTakenList]) / sum([len(timeTakenList[i]) for i in timeTakenList])
    logging.info("Average time taken: " + str(averageTimeTaken) + " ns (" + str(averageTimeTaken / 1000000000) + " s)")

    # Calculate success rate and total number of operations
    totalclientIDs = sum([len(timeTakenList[i]) for i in timeTakenList])
    successRate = sum([successCount[i] for i in successCount]) / totalclientIDs
    logging.info("Success rate: " + str(successRate * 100) + "% (" + str(sum([successCount[i] for i in successCount])) + "/" + str(totalclientIDs) + ")")
    
    # Get the average clientIDs per second
    clientIDsPerSecond = totalclientIDs / totalTimeTaken
    logging.info("clientIDs per second: " + str(clientIDsPerSecond))

    results, anomaly_line_presence = check_discount_from_log_file(log_file_path)
    print(f"OK: {results['OK']}")
    print(f"Anomalies: {results['anomalies']}")
    if results['anomalies'] > 0:
        for line in anomaly_line_presence:
            print(line, end='')

    # Output ratio of Anomalies to total in percentage
    print(f"Anomalies ratio: {results['anomalies'] / (results['OK'] + results['anomalies']) * 100}%")

if __name__ == "__main__":
    # Call main function
    main()