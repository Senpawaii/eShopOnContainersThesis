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

const numBaskets = 21;

export let options = {
    vus: 1,
    duration: "60s",
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
    const body7 = {
        "catalogItem": {
            "id": 8,
            "name": "Kudu Purple Hoodie",
            "description": "Kudu Purple Hoodie",
            "price": 8.50,
            "pictureFileName": "8.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 2,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 34,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 8,
            "ItemName": "Kudu Purple Hoodie",
            "ItemBrand": "Other",
            "ItemType": "T-Shirt",
            "DiscountValue": 2
        }
    }
    const body8 = {
        "catalogItem": {
            "id": 9,
            "name": "Cup<T> White Mug",
            "description": "Cup<T> White Mug",
            "price": 12.0,
            "pictureFileName": "9.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 1,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 76,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 9,
            "ItemName": "Cup<T> White Mug",
            "ItemBrand": "Other",
            "ItemType": "Mug",
            "DiscountValue": 2
        }
    }
    const body9 = {
        "catalogItem": {
            "id": 10,
            "name": ".NET Foundation Pin",
            "description": ".NET Foundation Pin",
            "price": 12.00,
            "pictureFileName": "10.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 3,
            "catalogType": null,
            "catalogBrandId": 1,
            "catalogBrand": null,
            "availableStock": 11,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 10,
            "ItemName": ".NET Foundation Pin",
            "ItemBrand": ".NET",
            "ItemType": "Pin",
            "DiscountValue": 2       }
    }
    const body10 = {
        "catalogItem": {
            "id": 11,
            "name": "Cup<T> Pin",
            "description": "Cup<T> Pin",
            "price": 8.50,
            "pictureFileName": "11.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 3,
            "catalogType": null,
            "catalogBrandId": 1,
            "catalogBrand": null,
            "availableStock": 3,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 11,
            "ItemName": "Cup<T> Pin",
            "ItemBrand": ".NET",
            "ItemType": "Pin",
            "DiscountValue": 2
        }
    }
    const body11 = {
        "catalogItem": {
            "id": 12,
            "name": "Prism White Bag TShirt",
            "description": "Prism White TShirt",
            "price": 12.00,
            "pictureFileName": "12.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 2,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 0,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 12,
            "ItemName": "Prism White Bag TShirt",
            "ItemBrand": "Other",
            "ItemType": "T-Shirt",
            "DiscountValue": 2
        }
    }
    const body12 = {
        "catalogItem": {
            "id": 13,
            "name": "Modern .NET Black & White Mug",
            "description": "Modern .NET Black & White Mug",
            "price": 8.50,
            "pictureFileName": "13.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 1,
            "catalogType": null,
            "catalogBrandId": 1,
            "catalogBrand": null,
            "availableStock": 89,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": true
        },
        "discountItem": {
            "id": 13,
            "ItemName": "Modern .NET Black & White Mug",
            "ItemBrand": ".NET",
            "ItemType": "Mug",
            "DiscountValue": 2
        }
    }
    const body13 = {
        "catalogItem": {
            "id": 14,
            "name": "Modern Cup<T> White Mug",
            "description": "Modern Cup<T> White Mug",
            "price": 12.00,
            "pictureFileName": "14.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 1,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 76,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 14,
            "ItemName": "Modern Cup<T> White Mug",
            "ItemBrand": "Other",
            "ItemType": "Mug",
            "DiscountValue": 2
        }
    }
    const body14 = {
        "catalogItem": {
            "id": 15,
            "name": "Table",
            "description": "ExampleDescription",
            "price": 100.00,
            "pictureFileName": "15.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 4,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 100,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 15,
            "ItemName": "Table",
            "ItemBrand": "Other",
            "ItemType": "furniture",
            "DiscountValue": 2
        }
    }
    const body15 = {
        "catalogItem": {
            "id": 16,
            "name": "Chair",
            "description": "ExampleDescription",
            "price": 100.00,
            "pictureFileName": "1.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 4,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 100,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 16,
            "ItemName": "Chair",
            "ItemBrand": "Other",
            "ItemType": "furniture",
            "DiscountValue": 2
        }
    }
    const body16 = {
        "catalogItem": {
            "id": 17,
            "name": "Bed",
            "description": "ExampleDescription",
            "price": 100.00,
            "pictureFileName": "1.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 4,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 100,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 17,
            "ItemName": "Bed",
            "ItemBrand": "Other",
            "ItemType": "furniture",
            "DiscountValue": 2
        }
    }
    const body17 = {
        "catalogItem": {
            "id": 18,
            "name": "Bureau",
            "description": "ExampleDescription",
            "price": 100.00,
            "pictureFileName": "1.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 4,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 100,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 18,
            "ItemName": "Bureau",
            "ItemBrand": "Other",
            "ItemType": "furniture",
            "DiscountValue": 2
        }
    }
    const body18 = {
        "catalogItem": {
            "id": 19,
            "name": "Bookcase",
            "description": "ExampleDescription",
            "price": 100.00,
            "pictureFileName": "1.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 4,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 100,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 19,
            "ItemName": "Bookcase",
            "ItemBrand": "Other",
            "ItemType": "furniture",
            "DiscountValue": 2
        }
    }
    const body19 = {
        "catalogItem": {
            "id": 20,
            "name": "Console",
            "description": "ExampleDescription",
            "price": 100.00,
            "pictureFileName": "1.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 4,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 100,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 20,
            "ItemName": "Console",
            "ItemBrand": "Other",
            "ItemType": "furniture",
            "DiscountValue": 2
        }
    }
    const body20 = {
        "catalogItem": {
            "id": 21,
            "name": "Chaise longue",
            "description": "ExampleDescription",
            "price": 100.00,
            "pictureFileName": "1.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 4,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 100,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 21,
            "ItemName": "Chaise longue",
            "ItemBrand": "Other",
            "ItemType": "furniture",
            "DiscountValue": 2
        }
    }
    const body21 = {
        "catalogItem": {
            "id": 22,
            "name": "Cabinetry",
            "description": "ExampleDescription",
            "price": 100.00,
            "pictureFileName": "1.png",
            "pictureUri": "http://host.docker.internal:5202/c/api/v1/catalog/items/1/pic/",
            "catalogTypeId": 4,
            "catalogType": null,
            "catalogBrandId": 2,
            "catalogBrand": null,
            "availableStock": 100,
            "restockThreshold": 0,
            "maxStockThreshold": 0,
            "onReorder": false
        },
        "discountItem": {
            "id": 22,
            "ItemName": "Cabinetry",
            "ItemBrand": "Other",
            "ItemType": "furniture",
            "DiscountValue": 2
        }
    }

    let bodies = [ body1, body2, body3, body4, body5, body6, body7, body8, body9, body10, body11, body12, body13, body14, body15, body16, body17, body18, body19, body20, body21 ];
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
    let success = false;
    let iterations = 0;

    // Get a random number between 1 and numBaskets
    const randomBasket = Math.floor(Math.random() * (numBaskets) + 1);

    const start = new Date().getTime();
    let basket = null;
    while(!success) {
        const res = http.get(readBasketUrl + randomBasket);

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
            sleep(1);
        }
        check(res, {
            "is status 200": (r) => r.status === 200,
        });
        iterations++;
    }
    const end = new Date().getTime();
    const duration = end - start - (iterations - 1) * 1000;
    check((basket), {
        "is price coherent": (basket) => basket.items[0].unitPrice === (basket.items[0].discount * 10),
    });
    // Log current date with milliseconds precision
    console.log(`Date: ${new Date().getTime()} Read operation duration: ${duration} VU: ${__VU}, Iterations taken: ${iterations}`);
    // console.log(`VU: ${__VU}, Iterations taken: ${iterations}`);
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