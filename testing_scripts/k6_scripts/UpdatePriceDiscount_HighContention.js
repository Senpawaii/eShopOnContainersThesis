import http from "k6/http";
import { sleep } from "k6";

const baseUrl = 'http://localhost:5142/api/v1/frontend/updatepricediscount';
const thesisFrontendPort = 5142;
const getCatalogItemUrl = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/readcatalogitem/';
const getDiscountItemUrl = 'http://localhost:' + thesisFrontendPort + '/api/v1/frontend/readdiscounts/';


export let options = {
    vus: 1,
    duration: "60s",
    thresholds: {
        http_req_duration: ["p(95)<1500"]
    }
};


export function setup() {
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


export default function(body) {
    // Get a random int between 1 and 100
    const randomPrice = (Math.floor(Math.random() * 100) + 1) * 10;
    const associatedDiscount = randomPrice / 10;

    body.catalogItem.price = randomPrice;
    body.discountItem.DiscountValue = associatedDiscount;
    
    const res = http.put(baseUrl, JSON.stringify(body), { headers: { "Content-Type": "application/json" } });
    sleep(1);
}