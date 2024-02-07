import http from "k6/http";
import { sleep } from "k6";
import { Counter } from "k6/metrics";
import { check } from "k6";


const readBasketUrl = 'http://localhost:5142/api/v1/frontend/readbasket?basketId=basket0';
const addItemToBasketUrl = 'http://localhost:5142/api/v1/frontend/additemtobasket';

const readOperationCounter = new Counter("Read_Operations");

const test_duration = 60;
export let options = {
    vus: 1,
    duration: test_duration + "s",
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

    // start timer
    // const start = new Date().getTime();
    // // execute readBasket for 10% of the duration of this test
    // let iterations = 0;
    // const duration = test_duration * 1000;
    // console.log(`Date: ${new Date().getTime()}, duration: ${duration}`);
    // while (new Date().getTime() - start < duration * 0.1) {
    //     iterations++;
    //     console.log(`Read operations: iterations: ${iterations}`);
    //     readBasket();
    // }
    // print the number of read operations
}

// Define Read Basket function
export function readBasket() {
    let success = false;

    const start = new Date().getTime();
    while(!success) {
        
        const res = http.get(readBasketUrl);

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