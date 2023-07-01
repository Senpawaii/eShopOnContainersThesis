import http from "k6/http";
import { sleep } from "k6";

const baseUrl = 'http://localhost:5142/api/v1/frontend/readbasket?basketId=basket0';

export let options = {
    vus: 10,
    duration: "30s",
    thresholds: {
        http_req_duration: ["p(95)<1500"]
    }
};

export default function() {
    http.get(baseUrl);
    sleep(1);
}