import requests
import threading
import logging
import os
import datetime

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
    address = "http://host.docker.internal:" + catalogServicePort + "/catalog-api/api/v1/Catalog/items?ids=" + str(id) + "&interval_low=0&interval_high=0&functionality_ID=func1&timestamp=2024-05-30T14:00:00.0000000Z&tokens=0"
    
    # Log request address
    logging.info("Sending request to address: " + address)

    response = requests.get(address)

    # Log response
    logging.info('Response from Catalog Service: ' + str(response.status_code) + ' ' + str(response.reason))
    return response.json()


# Update Price on Catalog Item with ID 1
def updatePriceOnCatalog(catalogItem: dict, price: int):
    # Get thread ID
    identity = threading.get_ident()
    itemName = catalogItem[0]['name']
    itemBrandId = catalogItem[0]['catalogBrandId']
    itemTypeId = catalogItem[0]['catalogTypeId']

    address = 'http://host.docker.internal:' + catalogServicePort + '/catalog-api/api/v1/Catalog/items/price?name=' + itemName + '&brandId=' + str(itemBrandId) + '&typeId=' + str(itemTypeId) + '&price=' + str(price) + '&interval_low=0&interval_high=0&functionality_ID=func' + str(identity) + '&timestamp=2024-05-30T14:00:00.0000000Z&tokens=100'

    # Log request address
    logging.info("Sending POST request to address: " + address + " with body: " + str(catalogItem))

    response = requests.post(address)
    #Log response
    logging.info('Response from Catalog Service: ' + str(response.status_code) + ' ' + str(response.reason))
    

def main():
    # Configure logging settings
    ConfigureLoggingSettings()

    # Define Global HTTP Client
    http = requests.Session()

    # Define Global Catalog Item to be used in tests
    catalogItem = QueryCatalogItemById(1)

    # Create 1 thread that updates the price on the catalog item
    numThreads = 1
    threads = []
    for i in range(numThreads):
        price = (i + 1) * 10 
        threads.append(threading.Thread(target=updatePriceOnCatalog, args=(catalogItem, price,)))
        threads[i].start()
    
    # Wait for all threads to finish
    for i in range(numThreads):
        threads[i].join()

if __name__ == "__main__":
    # Call main function
    main()