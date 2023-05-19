import re

def check_discount_from_log_file(file_path):
    pattern = r'Read Basket: Price {([\d.]+)}, Discount: {([\d.]+)}'
    results = {'OK': 0, 'anomalies': 0}
    anomaly_line_presence = []

    with open(file_path, 'r') as file:
        for line in file:
            # Using regex, check if the line matches the pattern
            match = re.search(pattern, line)

            # If the line matches the pattern, check if the discount is 10% of the price
            if match:
                price = float(match.group(1))
                discount = float(match.group(2))
                if discount == price * 0.1:
                    results['OK'] += 1
                else:
                    results['anomalies'] += 1
                    anomaly_line_presence.append(line)

    return results, anomaly_line_presence

# Example usage
log_file_path = "testing_scripts\\logs\\UpdatePriceAndDiscount_2023-05-18_19-28-54.log"
results, anomaly_line_presence = check_discount_from_log_file(log_file_path)

print(f"OK: {results['OK']}")
print(f"Anomalies: {results['anomalies']}")
if results['anomalies'] > 0:
    for line in anomaly_line_presence:
        print(line, end='')

# Output ratio of Anomalies to total in percentage
print(f"Anomalies ratio: {results['anomalies'] / (results['OK'] + results['anomalies']) * 100}%")