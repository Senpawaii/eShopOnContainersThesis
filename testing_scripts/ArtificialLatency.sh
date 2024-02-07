#!/bin/bash
# Simulate a Datacenter network with artifical latency

delay_ms=0.5

# Get the container ID of the catalog-api service
container_id=$(sudo docker ps -f "name=catalog*" -q)
echo "Container ID for catalog-api: $container_id"

# Add a 10ms delay to the eth0 interface inside the container
sudo docker exec --privileged $container_id /bin/bash -c "apt-get update; apt install net-tools; apt-get install iproute2 -y; tc qdisc add dev eth0 root netem delay ${delay_ms}ms" > /dev/null

# Get the container ID of the discount-api service
container_id=$(sudo docker ps -f "name=discount*" -q)
echo "Container ID for discount-api: $container_id"
# Add a 10ms delay to the eth0 interface inside the container
sudo docker exec --privileged $container_id /bin/bash -c "apt-get update; apt install net-tools; apt-get install iproute2 -y; tc qdisc add dev eth0 root netem delay ${delay_ms}ms" > /dev/null

# Get the container ID of the basket-api service
container_id=$(sudo docker ps -f "name=basket-api*" -q)
echo "Container ID for basket-api: $container_id"
# Add a 10ms delay to the eth0 interface inside the container
sudo docker exec --privileged $container_id /bin/bash -c "apt-get update; apt install net-tools; apt-get install iproute2 -y; tc qdisc add dev eth0 root netem delay ${delay_ms}ms" > /dev/null

# Get the container ID of the thesisfrontend-api service
container_id=$(sudo docker ps -f "name=thesisfrontend*" -q)
echo "Container ID for thesisfrontend-api: $container_id"
# Add a 10ms delay to the eth0 interface inside the container
sudo docker exec --privileged $container_id /bin/bash -c "apt-get update; apt install net-tools; apt-get install iproute2 -y; tc qdisc add dev eth0 root netem delay ${delay_ms}ms" > /dev/null

# Get the container ID of the coordinator-api service
container_id=$(sudo docker ps -f "name=coordinator*" -q)
echo "Container ID for coordinator-api: $container_id"
# Add a 10ms delay to the eth0 interface inside the container
sudo docker exec --privileged $container_id /bin/bash -c "apt-get update; apt install net-tools; apt-get install iproute2 -y; tc qdisc add dev eth0 root netem delay ${delay_ms}ms" > /dev/null
