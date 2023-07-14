import http from "k6/http";
import { sleep } from "k6";
import { Counter } from "k6/metrics";
import { check } from "k6";


const readBasketUrl = 'http://localhost:5142/api/v1/frontend/readbasket?basketId=basket';
const addItemToBasketUrl = 'http://localhost:5142/api/v1/frontend/additemtobasket';

const readOperationCounter = new Counter("Read_Operations");

const numBaskets = 6;

const test_duration = 60;
export let options = {
    vus: 1,
    duration: test_duration + "s",
};

export function addItemToBasket() {
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

export function setup() {
    // addItemToBasket(); # Should not be disabled if the baskets are not already created
}

// Define Read Basket function
export function readBasket() {
    // Get a random number between 1 and 6
    const randomBasket = Math.floor(Math.random() * (numBaskets) + 1);

    let success = false;

    const start = new Date().getTime();
    while(!success) {
        
        const res = http.get(readBasketUrl + randomBasket);

        // Check if the the price item and discount are coeherent
        const basket = JSON.parse(res.body);
        const price = basket.items[0].unitPrice;
        const discount = basket.items[0].discount;
        check(res, {
            "is status 200": (r) => r.status === 200,
            "is price coherent": (r) => price === (discount * 10),
        });
        success = price === (discount * 10);
    }
    const end = new Date().getTime();
    const duration = end - start;
    // Log current date with milliseconds precision
    console.log(`Date: ${new Date().getTime()} Read operation duration: ${duration}`);
    readOperationCounter.add(1);
    sleep(1);
}

export default function() {
    readBasket();
}