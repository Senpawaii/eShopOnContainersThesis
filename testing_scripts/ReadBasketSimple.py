from concurrent.futures import ThreadPoolExecutor, wait, ALL_COMPLETED
import math
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
numThreads = 32 # Number of threads to be used in the test
secondsToRun = 20 # Number of seconds to run the test
read_write_ratio = 2 # Scale of 0 to 10, 0 being 100% read, 10 being 100% write
throughput_step = 10 # Number of requests per second to increase throughput by
throughput = 20 # requests per second
max_throughput = 340
contention_rows = 6 # Number of rows to be used in the test
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

def readBasket(basketID: str, clientID: str):
    # Get the thread identity
    address = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/readbasket?basketId=' + basketID

    # Measure time taken to send request with nano seconds precision
    start = time.time_ns()
    success = False
    iterations = 0
    while not success and iterations < 10:
        # Send request
        response = requests.get(address)
        if(response.status_code == 200):
            # Extract the basket items from the response
            basketItems = response.json()["items"]

            # Extract the basket item price and discount from the basket items
            basketItemPrice = basketItems[0]["unitPrice"]
            basketItemDiscount = basketItems[0]["discount"]
            if(basketItemDiscount != basketItemPrice * 0.1):
                print("Anomaly detected")
            else:
                # print("OK")
                success = True
    
    # Stop timer
    end = time.time_ns()
    # Calculate time taken in number of nano seconds
    timeTaken = end - start
    with (open(f"logs/ReadBasketSimple/readBasketSimple_{clientID}.txt", "a") as f):
        f.write("Time taken: " + str(timeTaken / 1000000) + " milliseconds, address: " + address + ". Price: " + str(basketItemPrice) + ", discount: " + str(basketItemDiscount) + "\n")


    print("Time taken: " + str(timeTaken / 1000000) + " milliseconds, address: " + address + ". Price: " + str(basketItemPrice) + ", discount: " + str(basketItemDiscount))
    # time.sleep(0.1)
    return


def exec_functionality(basketID, clientID):
    start_test_time = time.time()
    iterations = 0
    # duration = 10 # seconds
    while iterations < 10000:
        readBasket(f"basket{basketID}", clientID)
        iterations += 1
        # time.sleep(0.5)


def main():
    # Get the first argument passed to the script
    if(len(sys.argv) > 1):
        basketID = sys.argv[1]
        clientID = sys.argv[2]
    exec_functionality(basketID, clientID)


if __name__ == "__main__":
    # Call main function
    main()