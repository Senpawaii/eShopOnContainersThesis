import http from "k6/http";
import { sleep } from "k6";
import { Counter } from "k6/metrics";
import { check } from "k6";

const baseUrl = 'http://localhost:5142/api/v1/frontend/updatepricediscount';
const thesisFrontendPort = 5142;
const getCatalogItemUrl = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/readcatalogitem/';
const getDiscountItemUrl = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/readdiscounts/';
const readBasketUrl = 'http://localhost:5142/api/v1/frontend/readbasket?basketId=basket';
const addItemToBasketUrl = 'http://localhost:5142/api/v1/frontend/additemtobasket';

const readOperationCounter = new Counter("Read_Operations");
const writeOperationCounter = new Counter("Write_Operations");

const numBaskets = 6;

export let options = {
    vus: 400,
    duration: "20s",
    thresholds: {
        http_req_duration: ["p(95)<1500"]
    }
};


export function setup() {
    const body1 = {
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
    const body2 = {
        "catalogItem": {
            "id": 3,
            "name": "Prism White T-Shirt",
            "description": "Prism White T-Shirt",
            "price": 12.00,
            "pictureFileName": "3.png",
            "pictureUri": "http://docker.for.linux.localhost:5202/c/api/v1/catalog/items/3/pic/",
            "catalogTypeId": 2,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 56,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 3,
            "ItemName": "Prism White T-Shirt",
            "ItemBrand": "Other",
            "ItemType": "T-Shirt",
            "DiscountValue": 2
        }
    }
    const body3 = {
        "catalogItem": {
            "id": 4,
            "name": ".NET Foundation T-shirt",
            "description": ".NET Foundation T-shirt",
            "price": 12.00,
            "pictureFileName": "4.png",
            "pictureUri": "http://docker.for.linux.localhost:5202/c/api/v1/catalog/items/4/pic/",
            "catalogTypeId": 2,
            "catalogType": null,
            "catalogBrandId": 1,
            "catalogBrand": null,
            "availableStock": 120,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 4,
            "ItemName": ".NET Foundation T-shirt",
            "ItemBrand": ".NET",
            "ItemType": "T-Shirt",
            "DiscountValue": 2
        }
    }
    const body4 = {
        "catalogItem": {
            "id": 5,
            "name": "Roslyn Red Pin",
            "description": "Roslyn Red Pin",
            "price": 8.50,
            "pictureFileName": "5.png",
            "pictureUri": "http://docker.for.linux.localhost:5202/c/api/v1/catalog/items/5/pic/",
            "catalogTypeId": 3,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 55,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 5,
            "ItemName": "Roslyn Red Pin",
            "ItemBrand": "Other",
            "ItemType": "Pin",
            "DiscountValue": 2
        }
    }
    const body5 = {
        "catalogItem": {
            "id": 6,
            "name": ".NET Blue Hoodie",
            "description": ".NET Blue Hoodie",
            "price": 12.00,
            "pictureFileName": "6.png",
            "pictureUri": "http://docker.for.linux.localhost:5202/c/api/v1/catalog/items/6/pic/",
            "catalogTypeId": 2,
            "catalogType": null,
            "catalogBrandId": 1,
            "catalogBrand": null,
            "availableStock": 17,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 6,
            "ItemName": ".NET Blue Hoodie",
            "ItemBrand": ".NET",
            "ItemType": "T-Shirt",
            "DiscountValue": 2
        }
    }
    const body6 = {
        "catalogItem": {
            "id": 7,
            "name": "Roslyn Red T-Shirt",
            "description": "Roslyn Red T-Shirt",
            "price": 12.00,
            "pictureFileName": "7.png",
            "pictureUri": "http://docker.for.linux.localhost:5202/c/api/v1/catalog/items/7/pic/",
            "catalogTypeId": 2,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 8,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 7,
            "ItemName": "Roslyn Red T-Shirt",
            "ItemBrand": "Other",
            "ItemType": "T-Shirt",
            "DiscountValue": 2
        }
    }

    let bodies = [ body1, body2, body3, body4, body5, body6 ];
    addItemToBaskets(bodies);

    return bodies;
}

export function addItemToBaskets(bodies) {
    for(let i = 1; i <= numBaskets; i++) {
        const product = bodies[i - 1];
        let body = {
            "CatalogItemId": product.catalogItem.id,
            "BasketId":"basket" + i,
            "Quantity": 1,
            "CatalogItemName": product.catalogItem.name,
            "CatalogItemBrandName": product.discountItem.ItemBrand,
            "CatalogItemTypeName": product.discountItem.ItemType
        }
        const JSONBody = JSON.stringify(body);
        const res = http.post(addItemToBasketUrl, JSONBody, { headers: { "Content-Type": "application/json" } });
        // console.log("Added item to basket" + i);
        // console.log(JSONBody);
        // console.log(res);
        // console.log(product)
    }
}

// Define Read Basket function
export function readBasket() {
    // Get a random number between 1 and 6
    const randomBasket = Math.floor(Math.random() * (numBaskets) + 1);

    const res = http.get(readBasketUrl + randomBasket);

    // Check if the the price item and discount are coeherent
    const basket = JSON.parse(res.body);
    const price = basket.items[0].unitPrice;
    const discount = basket.items[0].discount;
    check(res, {
        "is status 200": (r) => r.status === 200,
        "is price coherent": (r) => price === (discount * 10),
    });

    readOperationCounter.add(1);
    sleep(1);
}

// Define Update Price and Discount function
export function updatePriceAndDiscount(body) {
    const randomPrice = (Math.floor(Math.random() * 10000) + 1) * 10;
    const associatedDiscount = randomPrice / 10;

    body.catalogItem.price = randomPrice;
    body.discountItem.DiscountValue = associatedDiscount;
    
    const res = http.put(baseUrl, JSON.stringify(body), { headers: { "Content-Type": "application/json" } });
    writeOperationCounter.add(1);
    sleep(0.5);
}

export default function(bodies) {
    const operation = Math.random();
    if(operation < 0.8) {
        readBasket();
    } else {
        const product = bodies[Math.floor(Math.random() * bodies.length)];
        updatePriceAndDiscount(product);
    }
    return;
}