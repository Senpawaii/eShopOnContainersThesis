# Define the two versions of our system: MicroTCC and BaseTCC in a list
# systems=("BaseTCC", "µTCC")
systems=("BaseTCC")
# data_contention=("high", "low")
data_contention=("high")
test_VUs=(1)
test_duration=(30s)
current_date=$(date +%Y-%m-%d_%H-%M-%S)

for i in $systems
do
    echo -e "********************\n Running tests for system: $i \n********************"
    # Run the pipeline for each system
    # 1. Disable/ Enable wrappers
    cd ..
    if [ $i == "µTCC" ]; then
        python3 testing_scripts/Config_EnableDisableWrappers.py --thesisWrapper
    else
        python3 testing_scripts/Config_EnableDisableWrappers.py
    fi

    # 2. Clear any previous state and Build the docker images
    cd src
    sudo docker-compose --log-level ERROR down -v; sudo docker-compose --log-level ERROR build;

    # 3. Run the pipeline for each data contention
    for j in $data_contention
    do
        echo -e "--------------------\n Running tests for data contention: $j \n--------------------"
        # Start the system
        sudo docker-compose --log-level ERROR up -d
        # Sleep for 30 seconds (expected time for the system to be up and running)
        sleep 30
        
        cd ..
        for k in $test_VUs
        do
            echo "Running K6 tests for $i with $j contention and $k VUs"
            if [ $j == "high" ]; then
                # Warm up the system
                k6 run testing_scripts/k6_scripts/ReadUpdateFunctionality_HighContention.js --vus $k --duration $test_duration

                # Run the tests
                k6 run testing_scripts/k6_scripts/ReadUpdateFunctionality_HighContention.js --vus $k --duration $test_duration > testing_scripts/logs/K6_tests/Thesis_results/$i/$j/${k}_${current_date}.txt
            else
                # Warm up the system
                k6 run testing_scripts/k6_scripts/ReadUpdateFunctionality_LowContention.js --vus $k --duration $test_duration

                # Run the tests
                k6 run testing_scripts/k6_scripts/ReadUpdateFunctionality_LowContention.js --vus $k --duration $test_duration > testing_scripts/logs/K6_tests/Thesis_results/$i/$j/${k}_${current_date}.txt
            fi
        done
        cd src
        # Clear any previous state inbetween different data contention tests
        sudo docker-compose --log-level ERROR down -v;
    done
done



