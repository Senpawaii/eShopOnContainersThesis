#!/bin/bash

# Define the two versions of our system: MicroTCC and BaseTCC in a list
# systems=("BaseTCC" "µTCC")
systems=("µTCC")
data_contention=("high" "low")
# data_contention=("high")
test_VUs=(1 5 10 20 40 80 160 240 320 400 480 560 640 720 800 880 960 1040 1120 1200 1280 1360 1440 1520 1600)
test_duration=(30s)
current_date=$(date +%Y-%m-%d_%H-%M-%S)

for system in "${systems[@]}"
do
    echo -e "********************\n Running tests for system: $system \n********************"
    # Run the pipeline for each system
    # 1. Disable/ Enable wrappers
    cd ..
    if [ $system == "µTCC" ]; then
        python3 testing_scripts/Config_EnableDisableWrappers.py --thesisWrapper
    else
        python3 testing_scripts/Config_EnableDisableWrappers.py
    fi

    # 2. Clear any previous state and Build the docker images
    cd src
    sudo docker-compose --log-level ERROR down -v; sudo docker-compose --log-level ERROR build;

    # 3. Run the pipeline for each data contention
    for contention in "${data_contention[@]}"
    do
        echo -e "--------------------\n Running tests for data contention: $contention \n--------------------"
        # Start the system
        sudo docker-compose --log-level ERROR up -d
        # Sleep for 30 seconds (expected time for the system to be up and running)
        sleep 30
        
        cd ..
        for test_vu in "${test_VUs[@]}"
        do
            echo "Running K6 tests for $system with $contention contention and $test_vu VUs"
            if [ $contention == "high" ]; then
                # Warm up the system
                k6 run testing_scripts/k6_scripts/ReadUpdateFunctionality_HighContention.js --vus $test_vu --duration $test_duration

                # Run the tests
                k6 run testing_scripts/k6_scripts/ReadUpdateFunctionality_HighContention.js --vus $test_vu --duration $test_duration > testing_scripts/logs/K6_tests/Thesis_results/$system/$contention/${test_vu}_${current_date}.txt
            else
                # Warm up the system
                k6 run testing_scripts/k6_scripts/ReadUpdateFunctionality_LowContention.js --vus $test_vu --duration $test_duration

                # Run the tests
                k6 run testing_scripts/k6_scripts/ReadUpdateFunctionality_LowContention.js --vus $test_vu --duration $test_duration > testing_scripts/logs/K6_tests/Thesis_results/$system/$contention/${test_vu}_${current_date}.txt
            fi
        done
        cd src
        # Clear any previous state inbetween different data contention tests
        sudo docker-compose --log-level ERROR down -v;
    done
done



