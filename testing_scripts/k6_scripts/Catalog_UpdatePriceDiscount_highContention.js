import http from "k6/http";
import { sleep } from "k6";
import { Counter } from "k6/metrics";

const baseUrl = 'http://localhost:5101/api/v1/Catalog/items';
const writeOperationCounter = new Counter("Write_Operations");


export let options = {
    vus: 10,
    duration: "60s",
};


export function setup() {
    // const catalogRes = http.get(getCatalogItemUrl + '1'); // Get catalog item with id 1
    // const catalogItem = JSON.parse(catalogRes.body);
    
    const body = {
        "id": 1,
        "name": ".NET Bot Black Hoodie",
        "description": ".NET Bot Black Hoodie, and more",
        "price": 10000,
        "pictureFileName": "1.png",
        "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
        "catalogTypeId": 2,
        "catalogType": null,
        "catalogBrandId": 1,
        "catalogBrand": null,
        "availableStock": 100,
        "restockThreshold": 0,
        "maxStockThreshold": 0,
        "onReorder": false
    }
    return body;
}


export default function(body) {
    let sucess = false;

    const start = new Date().getTime();
    while(!sucess) {
        // Get a random int between 1 and 100
        const randomPrice = (Math.floor(Math.random() * 100) + 1) * 10;

        // Generate a random number between 1 and 100000000
        const randomGuid = Math.floor(Math.random() * 100000000) + 1;

        body.price = randomPrice;
        const url = baseUrl + "?clientID="+ randomGuid +"&timestamp=2023-10-22T11:11:11.1111111Z&tokens=1000000000";
        const res = http.put(url, JSON.stringify(body), { headers: { "Content-Type": "application/json" } });
        if (res.status === 201) {
            sucess = true;
        } else {
            console.log(`Error: ${res.status}`);
        }
    }
    const end = new Date().getTime();
    const duration = end - start;
    // Log current date with milliseconds precision
    console.log(`Date: ${new Date().getTime()} Update operation duration: ${duration}`);
    writeOperationCounter.add(1);
    // sleep(1);
}