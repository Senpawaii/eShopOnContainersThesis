import http from "k6/http";
import { sleep } from "k6";
import { Counter } from "k6/metrics";
import { check } from "k6";

const baseUrl = 'http://localhost:5142/api/v1/frontend/updatepricediscount';
const thesisFrontendPort = 5142;
const getCatalogItemUrl = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/readcatalogitem/';
const getDiscountItemUrl = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/readdiscounts/';
const readBasketUrl = 'http://localhost:5142/api/v1/frontend/readbasket?basketId=basket0';
const addItemToBasketUrl = 'http://localhost:5142/api/v1/frontend/additemtobasket';

const readOperationCounter = new Counter("Read_Operations");
const writeOperationCounter = new Counter("Write_Operations");

export let options = {
    vus: 640,
    stages: [
        { duration: "10s", target: 2 },
        { duration: "50s", target: 2 },
    ],
};


export function addItemToBasket() {
    const body = {
        "CatalogItemId": 1,
        "BasketId":"basket0",
        "Quantity": 1,
        "CatalogItemName": ".NET Bot Black Hoodie",
        "CatalogItemBrandName": ".NET",
        "CatalogItemTypeName": "T-Shirt"
    }

    const JSONBody = JSON.stringify(body);
    http.post(addItemToBasketUrl, JSONBody, { headers: { "Content-Type": "application/json" } });

}

export function setup() {
    addItemToBasket();

    const catalogRes = http.get(getCatalogItemUrl + '1'); // Get catalog item with id 1
    const catalogItem = JSON.parse(catalogRes.body);
    
    const body = {
        "catalogItem": {
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
        },
        "discountItem": {
            "id": 1,
            "ItemName": ".NET Bot Black Hoodie",
            "ItemBrand": ".NET",
            "ItemType": "T-Shirt",
            "DiscountValue": 400
        }
    }

    return body;
}

export function teardown() {
    
}

// Define Read Basket function
export function readBasket() {
    let success = false;
    let iterations = 0;

    const start = new Date().getTime();
    let basket = null;
    while(!success) {
        const res = http.get(readBasketUrl);

        // Check if the the price item and discount are coeherent
        if (res.status !== 200) {
            console.log(`Error: ${res.status}`);
            iterations++;
            sleep(1);
            continue;
        }
        basket = JSON.parse(res.body);
        const price = basket.items[0].unitPrice;
        const discount = basket.items[0].discount;

        if(price === (discount * 10)) {
            console.log(`VU: ${__VU}, iteration: ${iterations}, price: ${price}, discount: ${discount}, price is coherent`)
            success = true;
        } 
        else {
            console.log(`VU: ${__VU}, iteration: ${iterations}, price: ${price}, discount: ${discount}, price is not coherent`)
            iterations++;
            sleep(1);
        }
        check(res, {
            "is status 200": (r) => r.status === 200,
        });
    }
    const end = new Date().getTime();
    const duration = end - start - (iterations * 1000);
    check((basket), {
        "is price coherent": (basket) => basket.items[0].unitPrice === (basket.items[0].discount * 10),
    });
    // Log current date with milliseconds precision
    console.log(`Date: ${new Date().getTime()} Read operation duration: ${duration} VU: ${__VU}, Iterations taken: ${iterations}`);
    readOperationCounter.add(1);
    sleep(1);
}

// Define Update Price and Discount function
export function updatePriceAndDiscount(body) {
    const randomPrice = (Math.floor(Math.random() * 10000) + 1) * 10;
    const associatedDiscount = randomPrice / 10;

    body.catalogItem.price = randomPrice;
    body.discountItem.DiscountValue = associatedDiscount;

    let success = false;
    let iterations = 0;

    const start = new Date().getTime();
    while(!success) {
        iterations++;
        const res = http.put(baseUrl, JSON.stringify(body), { headers: { "Content-Type": "application/json" } });
        if (res.status !== 200) {
            console.log(`Error: ${res.status}`);
            sleep(1);
            continue;
        } else {
            success = true;
        }
    }
    const end = new Date().getTime();
    const duration = end - start;
    // Log current date with milliseconds precision
    console.log(`Date: ${new Date().getTime()} Update operation duration: ${duration}`);
    console.log(`VU: ${__VU}, Iterations taken: ${iterations}`);
    writeOperationCounter.add(1);
    sleep(1);
}

export default function(body) {
    const operation = Math.random();
    if(operation < 0.8) {
        readBasket();
    } else {
        updatePriceAndDiscount(body);
    }
    return;
}