# Define the two versions of our system: MicroTCC and BaseTCC in a list
systems=("BaseTCC", "µTCC")
data_contention=("low", "high")
test_VUs=(1, 10, 100, 1000)

for i in $systems
do
    # Run the pipeline for each system
    # 1. Disable/ Enable wrappers
    cd ..
    if [ $i == "µTCC" ]; then
        python3 testing_scripts/Config_EnableDisableWrappers.py --thesisWrapper
    else
        python3 testing_scripts/Config_EnableDisableWrappers.py
    fi

    # 2. Build the docker images
    docker-compose build;

done



