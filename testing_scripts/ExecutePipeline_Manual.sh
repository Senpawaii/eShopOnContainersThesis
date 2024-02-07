#!/bin/bash

# Launch the ReadBasketSimple.py client N times, according to the test being executed
# test_VUs=(240 280 320 360 400 440 480 520 560 600 640)
test_VUs=(30)
# test_VUs=(40 80 120 160 200 240 280 320 360 400 440 480 520 560 600 640)
# test_VUs=(600 640)
# test_VUs=(35)
# test_VUs=(10 20 30 40 50 60 70 80 90 100 110 120 130 140)
# test_VUs=(320 360 400)
# test_VUs=(1)
# test_VUs=(310 320 330 340 350 360 370 380 390 400)
# test_VUs=(110 120 130 140 150 160 170 180 190 200 210 220 230 240 250 260 270 280 290 300)
# test_VUs=(410 420 430 440 450 460 470 480 490 500 510 520 530 540 550 560 570 580 590 600 610 620 630 640)
# test_VUs=(510 520 530 540 550 560 570 580 590 600 610 620 630 640)
# test_VUs=(800 1000 1200 1400 1600)

# system="BaseTCC"
system="µTCC"
# contention="high"
contention="low"
# K6_script="UpdatePriceDiscount"
# K6_script="ReadBasket"
K6_script="UpdatePriceDiscountReadBasket"
# functionality="ReadBasket_only"
# functionality="UpdatePriceDiscount_only"
functionality="UpdatePriceDiscountReadBasket"
versions="25"
withOrwithoutEvents="with"

cd ..


# Function to restart the system
restart_system() {
    cd src;
    sudo docker-compose down 
    sudo docker-compose up rabbitmq -d
    echo "RabbitMQ started. Waiting for it to boot up..."
    sleep 20
    sudo docker-compose up -d
    echo "System restarted. Waiting for it to boot up..."
    sleep 20  # Adjust the sleep duration as needed to allow the system to boot up
    cd ..;
}

event_test() {
    for test_vu in "${test_VUs[@]}"
    do
        for i in {1..1}
        do  
            restart_system
            k6 run testing_scripts/k6_scripts/${K6_script}_${contention}Contention.js --vus ${test_vu} --stage 20s:${test_vu} --stage 50s:${test_vu} --console-output "testing_scripts/logs/K6_tests/Thesis_results/${system}/${contention}_${functionality}_${withOrwithoutEvents}Events/${versions}_versions/${test_vu}_${i}.txt"
        done
    done
}

memory_test() {
    for test_vu in "${test_VUs[@]}"
    do
        for i in {1..1}
        do  
            # Measure memory usage of the containers
            catalog_container_id=$(sudo docker ps -f "name=catalog*" -q)
            echo "Container ID for Catalog-api: $catalog_container_id"
            coordinator_container_id=$(sudo docker ps -f "name=coordinator*" -q)
            echo "Container ID for Coordinator-api: $coordinator_container_id"
            discount_container_id=$(sudo docker ps -f "name=discount*" -q)
            echo "Container ID for Discount-api: $discount_container_id"
            basket_container_id=$(sudo docker ps -f "name=basket*" -q)
            echo "Container ID for Basket-api: $basket_container_id"

            k6 run testing_scripts/k6_scripts/${K6_script}_${contention}Contention.js --vus ${test_vu} --stage 20s:${test_vu} --stage 50s:${test_vu} --console-output "testing_scripts/logs/K6_tests/Thesis_results/${system}/${contention}_${functionality}_${withOrwithoutEvents}Events/${versions}_versions/tmp_memory/${test_vu}_${i}.txt" &
            k6_pid=$!  # Get the process ID of the k6 run command
            
            # Run docker stats command concurrently with k6
            for ((j=0; j<600; j++)); do
                # sudo docker stats --no-stream --format '{{.MemUsage}}' $catalog_container_id | cut -d '/' -f 1 >> testing_scripts/logs/K6_tests/Thesis_results/${system}/${contention}_${functionality}_${withOrwithoutEvents}Events/${versions}_versions_memory_test/${test_vu}_${i}_memory_catalog.txt &
                # catalog_stats_pid=$!  # Get the process ID of the docker stats command
                
                # sudo docker stats --no-stream --format '{{.MemUsage}}' $coordinator_container_id | cut -d '/' -f 1 >> testing_scripts/logs/K6_tests/Thesis_results/${system}/${contention}_${functionality}_${withOrwithoutEvents}Events/${versions}_versions_memory_test/${test_vu}_${i}_memory_coordinator.txt &
                # coordinator_stats_pid=$!  # Get the process ID of the docker stats command
                
                # sudo docker stats --no-stream --format '{{.MemUsage}}' $discount_container_id | cut -d '/' -f 1 >> testing_scripts/logs/K6_tests/Thesis_results/${system}/${contention}_${functionality}_${withOrwithoutEvents}Events/${versions}_versions_memory_test/${test_vu}_${i}_memory_discount.txt &
                # discount_stats_pid=$!  # Get the process ID of the docker stats command

                # sudo docker stats --no-stream --format '{{.MemUsage}}' $basket_container_id | cut -d '/' -f 1 >> testing_scripts/logs/K6_tests/Thesis_results/${system}/${contention}_${functionality}_${withOrwithoutEvents}Events/${versions}_versions_memory_test/${test_vu}_${i}_memory_basket.txt &
                # basket_stats_pid=$!  # Get the process ID of the docker stats command
                
                sleep 0.1
            done

            # Wait for k6 run command and docker stats command to finish before proceeding to the next iteration
            wait $k6_pid
            wait $catalog_stats_pid
            wait $coordinator_stats_pid
            wait $discount_stats_pid

            restart_system
        done
    done
}


# for test_vu in "${test_VUs[@]}"
# do
#     for i in {1..3}
#     do  
#         k6 run testing_scripts/k6_scripts/${K6_script}_${contention}Contention.js --vus ${test_vu} --stage 20s:${test_vu} --stage 50s:${test_vu} --console-output "testing_scripts/logs/K6_tests/Thesis_results/${system}/${contention}_${functionality}_${withOrwithoutEvents}Events/${versions}_versions/tmp/${test_vu}_${i}.txt"
#     done
# done

event_test

sudo docker-compose down 

# k6 run testing_scripts/k6_scripts/Catalog_UpdatePriceDiscount_highContention.js --vus ${test_vu} --stage 10s:${test_vu} --stage 50s:${test_vu} --console-output "testing_scripts/logs/K6_tests/Thesis_results/${system}/${contention}_${functionality}_${withOrwithoutEvents}Events/${versions}_versions/${test_vu}.txt"

# for test_vu in "${test_VUs[@]}"
#     do  
#         k6 run testing_scripts/k6_scripts/${K6_script}_${contention}Contention.js --vus ${test_vu} --stage 10s:${test_vu} --stage 50s:${test_vu} --console-output "testing_scripts/logs/K6_tests/Thesis_results/${system}/${contention}_${functionality}_${withOrwithoutEvents}Events/${versions}_versions/${test_vu}.txt"
#         # k6 run testing_scripts/k6_scripts/Catalog_UpdatePriceDiscount_highContention.js --vus ${test_vu} --stage 10s:${test_vu} --stage 50s:${test_vu} --console-output "testing_scripts/logs/K6_tests/Thesis_results/${system}/${contention}_${functionality}_${withOrwithoutEvents}Events/${versions}_versions/${test_vu}.txt"
#     done
