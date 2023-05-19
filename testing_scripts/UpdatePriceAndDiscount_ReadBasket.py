from concurrent.futures import ThreadPoolExecutor
import random
import string
import time
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
discountServicePort = "5140"
basketServicePort = "5103"
webaggregatorServicePort = "5121"
current_directory = os.getcwd()

def ConfigureLoggingSettings():
    # Configure logging settings
    logging_path = os.path.join(current_directory, 'testing_scripts', 'logs')
    os.makedirs(logging_path, exist_ok=True)  # Create the log directory if it doesn't exist
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%d_%H-%M-%S")
    log_file_name = f"UpdatePriceAndDiscount_{timestamp}.log"
    log_file_path = os.path.join(logging_path, log_file_name)

    logging.basicConfig(
        level=logging.INFO, 
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


def QueryDiscountItemById(catalogItem: dict) -> dict:
    identity = threading.get_ident()

    # Access catalog Service to get the brand name and type name
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    # Get the brand name that matches the brand ID
    address = 'http://host.docker.internal:' + catalogServicePort + '/catalog-api/api/v1/Catalog/CatalogBrands?interval_low=0&interval_high=0&functionality_ID=func' + str(identity) + '&timestamp=' + timestamp + '&tokens=0'
    response = requests.get(address)

    brandName = ""
    for brand in response.json():
        if brand["id"] == catalogItem[0]["catalogBrandId"]:
            brandName = brand["brand"]

    # Get the type name that matches the type ID
    address = 'http://host.docker.internal:' + catalogServicePort + '/catalog-api/api/v1/Catalog/CatalogTypes?interval_low=0&interval_high=0&functionality_ID=func' + str(identity) + '&timestamp=' + timestamp + '&tokens=0'
    response = requests.get(address)

    typeName = ""
    for type in response.json():
        if type["id"] == catalogItem[0]["catalogTypeId"]:
            typeName = type["type"]

    itemName = catalogItem[0]["name"]
    # Get the discount item that matches the brand name and type name
    address = 'http://host.docker.internal:' + discountServicePort + '/discount-api/api/v1/Discount/discounts?itemNames=' + itemName  + '&itemBrands=' + brandName + '&itemTypes=' + typeName + '&functionality_ID=func' + str(identity) + '&timestamp=' + timestamp + '&tokens=0'
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

    address = 'http://host.docker.internal:' + webaggregatorServicePort + '/api/v1/Basket/items'

    response = requests.post(address, json=body)

    # Ensure that the response is 200
    if response.status_code != 200:
        logging.error("Error adding item to basket. Response from Basket.API: " + str(response.status_code) + " " + str(response.reason))
        return
    return


def readBasket():
    basketID = "e5d06a2d-fc81-4051-8f30-0a85836eac70"

    # Get the thread identity
    identity = threading.get_ident()

    funcID = ''.join(random.choices(string.ascii_lowercase, k=10))

    # Get current time in nano seconds precision
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    address = 'http://host.docker.internal:' + basketServicePort + '/api/v1/Basket/' + basketID + '?interval_low=0&interval_high=0&functionality_ID=func' + funcID + '&timestamp=' + timestamp + '&tokens=0'

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
    logging.info('Thread <' + str(identity) + '> FuncID: <' + funcID + '> ' + 'Read Basket: Price {' + str(basketItemPrice) + '}, Discount: {' + str(basketItemDiscount) + '}, Time taken: ' + str(timeTaken) + 'ns')
    return


def writeOperations(catalogItem: dict, discountItem: dict):
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
        # Get the price and discount already assigned to the thread
        price, discount = thread_price_discount[thread_identity]

    # Update price on catalog item
    updatePriceOnCatalog(catalogItem, price, funcID)
    
    # Update discount on discount item
    updateDiscount(discountItem, discount, funcID)
    return


# Update Price on Catalog Item with ID 1
def updatePriceOnCatalog(catalogItem: dict, price: int, funcID: str):
    # Get thread ID
    identity = threading.get_ident()
    
    # Update the price on the catalog item
    catalogItem[0]["price"] = price

     # Get current time in nano seconds precision
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    address = 'http://host.docker.internal:' + catalogServicePort + '/catalog-api/api/v1/Catalog/items?interval_low=0&interval_high=0&functionality_ID=func' + funcID + '&timestamp=' + timestamp + '&tokens=50'

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
    logging.info("Thread <" + str(identity) + "> FuncID: <" + funcID + "> " + "PriceUpdate: " + str(price) + ". Time: <" + str(timeTaken) + "> ns. Response: " + str(response.status_code) + " => " + str(response.reason))


# Update Discount on Item with ID 1
def updateDiscount(discountItem: dict, discount: int, funcID: str):
    # Get thread ID
    identity = threading.get_ident()
    
    # Update the discount value on the discount item
    discountItem["discountValue"] = discount

     # Get current time in nano seconds precision
    timestamp = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"

    address = 'http://host.docker.internal:' + discountServicePort + '/discount-api/api/v1/Discount/discounts?interval_low=0&interval_high=0&functionality_ID=func' + funcID + '&timestamp=' + timestamp + '&tokens=50'

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
    logging.info("Thread <" + str(identity) + "> DiscountUpdate: " + str(discount) + ". Time: <" + str(timeTaken) + "> ns. Response: " + str(response.status_code) + " => " + str(response.reason))



def assign_operations(catalogItem: dict, discountItem: dict):
    start_time = time.time()
    while time.time() - start_time < 30:
        # Assign read/write operations to thread based on read_write_ratio
        if random.choice(read_write_list) == 0:
            # Read operation
            future = executor.submit(readBasket)
        else:
            # Write operation
            future = executor.submit(writeOperations, catalogItem, discountItem)


numThreads = 10 # Number of threads to be used in the test
# Create a thread pool with numThreads threads
executor = ThreadPoolExecutor(max_workers=numThreads)  

# Create a single dictionary with an entry for each thread. Each thread is assigned a list of time taken and success count
timeTakenList = {}
successCount = {}
read_write_ratio = 2 # Scale of 0 to 10, 0 being 100% read, 10 being 100% write

# Create a list for chances of read/write operations
read_write_list = [1 for _ in range(read_write_ratio)] + [0 for _ in range(10 - read_write_ratio)]

# Create predefined list of prices and discounts to be used in tests equal to the number of threads
prices = [10 * (i+1) for i in range(numThreads)]
discounts = [prices[i] // 10 for i in range(numThreads)]
thread_price_discount = {}

def main():
    # Configure logging settings
    ConfigureLoggingSettings()

    # Define Global HTTP Client
    http = requests.Session()

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
    totalRequests = sum([len(timeTakenList[i]) for i in timeTakenList])
    successRate = sum([successCount[i] for i in successCount]) / totalRequests
    logging.info("Success rate: " + str(successRate * 100) + "% (" + str(sum([successCount[i] for i in successCount])) + "/" + str(totalRequests) + ")")
    
    # Get the average requests per second
    requestsPerSecond = totalRequests / totalTimeTaken
    logging.info("Requests per second: " + str(requestsPerSecond))

if __name__ == "__main__":
    # Call main function
    main()